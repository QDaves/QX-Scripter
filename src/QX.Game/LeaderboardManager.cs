using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

/// <summary>
/// Specifies which players a leaderboard ranks.
/// </summary>
public enum LeaderboardScope
{
    /// <summary>All players in the hotel.</summary>
    Total,
    /// <summary>The local user's friends.</summary>
    Friends,
    /// <summary>The members of the local user's favorite group.</summary>
    Groups
}

internal readonly record struct LeaderboardRoute(LeaderboardScope Scope, bool Weekly);

internal enum LeaderboardStateChangeKind
{
    Snapshot,
    Settings,
    Request,
    Reset
}

internal sealed record LeaderboardState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long BoardsRevision,
    long SettingsRevision,
    IReadOnlyDictionary<LeaderboardRoute, Leaderboard> Boards,
    WeeklyLeaderboardPeriod? Period,
    int FavouriteGroupId,
    int WeekOffset,
    int LastGameTypeId);

internal sealed record LeaderboardStateUpdate(
    LeaderboardStateChangeKind Kind,
    LeaderboardState State,
    LeaderboardRoute? Route,
    Leaderboard? Board,
    long RequestEpoch,
    long PublicationEpoch);

internal readonly record struct LeaderboardRequestCorrelation(
    LeaderboardState State,
    long RequestEpoch,
    int OutstandingRequests);

internal interface ILeaderboardOperations
{
    void Request(
        int game_type_id,
        LeaderboardScope scope,
        bool weekly,
        int start_rank,
        int direction);
}

/// <summary>
/// Manages the game leaderboards received by the local user.
/// </summary>
/// <remarks>
/// <para>
/// All members are safe to call from any thread. One board is kept per scope and period, and a new
/// response replaces the stored board for that scope and period. Events are raised in the order the
/// state changed and are not raised when the hotel session changed before they could be delivered.
/// </para>
/// <para>
/// The state is cleared when the hotel connection closes and when a new session connects.
/// </para>
/// </remarks>
public sealed class LeaderboardManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<LeaderboardStateUpdate> publications = [];
    private readonly Dictionary<LeaderboardRoute, long> request_epochs = [];
    private readonly Dictionary<LeaderboardRoute, int> outstanding_requests = [];
    private readonly Dictionary<LeaderboardRoute, long> clean_request_epochs = [];
    private LeaderboardState state = InitialState();
    private ILeaderboardOperations? operations;
    private long publication_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    /// <summary>
    /// Gets the view size sent with each leaderboard request.
    /// </summary>
    /// <remarks>
    /// Read from the <c>games.highscores.viewSize</c> game data variable. Defaults to 8.
    /// </remarks>
    public int ViewSize { get; internal set; } = 8;

    /// <summary>
    /// Gets the window size sent with each leaderboard request.
    /// </summary>
    /// <remarks>
    /// Read from the <c>games.highscores.windowSize</c> game data variable. Defaults to 50.
    /// <see cref="RequestPreviousPage"/> moves back by this many ranks.
    /// </remarks>
    public int WindowSize { get; internal set; } = 50;

    /// <summary>
    /// Gets the week covered by the last weekly board, or <see langword="null"/> when none was received.
    /// </summary>
    public WeeklyLeaderboardPeriod? Period => State.Period;

    /// <summary>
    /// Gets the id of the local user's favorite group as reported by the last group board.
    /// </summary>
    /// <remarks>
    /// 0 until a group board is received.
    /// </remarks>
    public int FavouriteGroupId => State.FavouriteGroupId;

    /// <summary>
    /// Occurs when a leaderboard is received.
    /// </summary>
    /// <remarks>
    /// The arguments are the board's scope, whether it is a weekly board, and the board.
    /// </remarks>
    public event Action<LeaderboardScope, bool, Leaderboard>? BoardReceived;
    internal event Action<LeaderboardStateUpdate>? StateCommitted;
    internal event Action<LeaderboardStateUpdate>? StateChanged;

    internal LeaderboardState State => Volatile.Read(ref state);

    /// <summary>
    /// Gets the last board received for a scope and period.
    /// </summary>
    /// <param name="scope">The scope of the board.</param>
    /// <param name="weekly">Whether to get the weekly board instead of the all-time board.</param>
    /// <returns>The board, or <see langword="null"/> when none was received.</returns>
    public Leaderboard? Board(LeaderboardScope scope, bool weekly = false) =>
        State.Boards.GetValueOrDefault(new LeaderboardRoute(scope, weekly));

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Total, false),
            MessageContracts.Leaderboards.Total.Request,
            MessageContracts.Leaderboards.Total.Snapshot,
            message => (message.Board, null, null));
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Friends, false),
            MessageContracts.Leaderboards.Friends.Request,
            MessageContracts.Leaderboards.Friends.Snapshot,
            message => (message.Board, null, null));
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Groups, false),
            MessageContracts.Leaderboards.Groups.Request,
            MessageContracts.Leaderboards.Groups.Snapshot,
            message => (message.Board, null, message.FavouriteGroupId));
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Total, true),
            MessageContracts.Leaderboards.WeeklyTotal.Request,
            MessageContracts.Leaderboards.WeeklyTotal.Snapshot,
            message => (message.Board, message.Period, null));
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Friends, true),
            MessageContracts.Leaderboards.WeeklyFriends.Request,
            MessageContracts.Leaderboards.WeeklyFriends.Snapshot,
            message => (message.Board, message.Period, null));
        BindRoute(
            new LeaderboardRoute(LeaderboardScope.Groups, true),
            MessageContracts.Leaderboards.WeeklyGroups.Request,
            MessageContracts.Leaderboards.WeeklyGroups.Snapshot,
            message => (message.Board, message.Period, message.FavouriteGroupId));
    }

    /// <summary>
    /// Requests a leaderboard for a game type from the server.
    /// </summary>
    /// <param name="gameTypeId">The id of the game type.</param>
    /// <param name="scope">The scope of the board.</param>
    /// <param name="weekly">Whether to request the weekly board instead of the all-time board.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    /// <remarks>
    /// The request carries <see cref="ViewSize"/> and <see cref="WindowSize"/>, and a weekly request
    /// carries <see cref="WeekOffset"/>. The response is stored and raised through
    /// <see cref="BoardReceived"/>.
    /// </remarks>
    public void Request(int gameTypeId, LeaderboardScope scope, bool weekly = false) =>
        Operations().Request(gameTypeId, scope, weekly, -1, 0);

    /// <summary>
    /// Requests the ranks below the stored board for a scope and period.
    /// </summary>
    /// <param name="scope">The scope of the board.</param>
    /// <param name="weekly">Whether to page the weekly board instead of the all-time board.</param>
    /// <returns>
    /// <see langword="true"/> when a request was sent; <see langword="false"/> when no board is stored
    /// or it has no more ranks below.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    /// <remarks>
    /// The request uses the game type of the last leaderboard request and starts one rank after the
    /// stored board's last rank.
    /// </remarks>
    public bool RequestNextPage(LeaderboardScope scope, bool weekly = false)
    {
        LeaderboardState current = State;
        Leaderboard? board = current.Boards.GetValueOrDefault(new LeaderboardRoute(scope, weekly));
        if (board is not { HasMoreBelow: true })
            return false;
        Operations().Request(
            current.LastGameTypeId,
            scope,
            weekly,
            board.LastRank + 1,
            0);
        return true;
    }

    /// <summary>
    /// Requests the ranks above the stored board for a scope and period.
    /// </summary>
    /// <param name="scope">The scope of the board.</param>
    /// <param name="weekly">Whether to page the weekly board instead of the all-time board.</param>
    /// <returns>
    /// <see langword="true"/> when a request was sent; <see langword="false"/> when no board is stored
    /// or it has no more ranks above.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    /// <remarks>
    /// The request uses the game type of the last leaderboard request and starts
    /// <see cref="WindowSize"/> ranks before the stored board's first rank, but not before rank 1.
    /// </remarks>
    public bool RequestPreviousPage(LeaderboardScope scope, bool weekly = false)
    {
        LeaderboardState current = State;
        Leaderboard? board = current.Boards.GetValueOrDefault(new LeaderboardRoute(scope, weekly));
        if (board is not { HasMoreAbove: true })
            return false;
        Operations().Request(
            current.LastGameTypeId,
            scope,
            weekly,
            Math.Max(1, board.FirstRank - WindowSize),
            1);
        return true;
    }

    /// <summary>
    /// Sets how many weeks back the next weekly leaderboard request looks.
    /// </summary>
    /// <param name="offset">The number of weeks back from the current week.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is negative.</exception>
    /// <remarks>
    /// The value is capped at the <see cref="WeeklyLeaderboardPeriod.MaxOffset"/> of the stored
    /// <see cref="Period"/>. No request is sent.
    /// </remarks>
    public void SetWeekOffset(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        LeaderboardStateUpdate? update = null;
        Exception? failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                LeaderboardState current = state;
                int value = Math.Min(offset, current.Period?.MaxOffset ?? int.MaxValue);
                if (value == current.WeekOffset)
                    return;
                LeaderboardState changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    SettingsRevision = checked(current.SettingsRevision + 1),
                    WeekOffset = value
                };
                Volatile.Write(ref state, changed);
                update = new LeaderboardStateUpdate(
                    LeaderboardStateChangeKind.Settings,
                    changed,
                    null,
                    null,
                    0,
                    publication_epoch);
                publications.Enqueue(update);
                drain = !publishing;
                publishing = true;
                failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(failure, publication_failure);
    }

    /// <summary>
    /// Gets how many weeks back weekly leaderboard requests look.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="SetWeekOffset"/> and updated from each weekly board received.
    /// </remarks>
    public int WeekOffset => State.WeekOffset;

    internal void BindOperations(ILeaderboardOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Leaderboard operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(ILeaderboardOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal LeaderboardRequestCorrelation CaptureRequestCorrelation(
        LeaderboardRoute route,
        Session expected_session,
        long expected_generation)
    {
        lock (state_sync)
        {
            RequireScope(expected_session, expected_generation);
            return new LeaderboardRequestCorrelation(
                state,
                request_epochs.GetValueOrDefault(route),
                outstanding_requests.GetValueOrDefault(route));
        }
    }

    internal long AdvanceTypedRequest(
        LeaderboardRoute route,
        long baseline,
        int game_type_id,
        Session expected_session,
        long expected_generation)
    {
        LeaderboardStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireScope(expected_session, expected_generation);
                if (request_epochs.GetValueOrDefault(route) != baseline ||
                    outstanding_requests.GetValueOrDefault(route) != 0)
                {
                    throw new InvalidOperationException(
                        "The leaderboard request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(route, game_type_id);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal long AdvanceLegacyRequest(
        LeaderboardRoute route,
        int game_type_id,
        Session expected_session,
        long expected_generation)
    {
        LeaderboardStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireScope(expected_session, expected_generation);
                update = BeginRequestUnsafe(route, game_type_id);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal bool IsCurrentPublication(LeaderboardStateUpdate update) => UpdateCurrent(update);

    /// <inheritdoc/>
    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void BindRoute<TRequest, TResponse>(
        LeaderboardRoute route,
        MessageContract<TRequest> request,
        MessageContract<TResponse> response,
        Func<TResponse, (Leaderboard Board, WeeklyLeaderboardPeriod? Period, int? Favourite)> select)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        OnOutgoing(request, (message, generation) =>
        {
            int game_type_id = message switch
            {
                LeaderboardRequest value => value.GameTypeId,
                WeeklyLeaderboardRequest value => value.GameTypeId,
                _ => 0
            };
            ObserveRequest(route, game_type_id, generation);
        });
        OnIncoming(response, (message, generation) =>
        {
            var value = select(message);
            Store(route, value.Board, value.Period, value.Favourite, generation);
        });
    }

    private void ObserveRequest(LeaderboardRoute route, int game_type_id, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        LeaderboardStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (StateCurrent(state, active, generation))
                    update = BeginRequestUnsafe(route, game_type_id);
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private LeaderboardStateUpdate BeginRequestUnsafe(LeaderboardRoute route, int game_type_id)
    {
        long next = checked(request_epochs.GetValueOrDefault(route) + 1);
        int previous = outstanding_requests.GetValueOrDefault(route);
        request_epochs[route] = next;
        outstanding_requests[route] = checked(previous + 1);
        clean_request_epochs[route] = previous == 0 ? next : 0;
        LeaderboardState changed = state with { LastGameTypeId = game_type_id };
        Volatile.Write(ref state, changed);
        return new LeaderboardStateUpdate(
            LeaderboardStateChangeKind.Request,
            changed,
            route,
            null,
            next,
            publication_epoch);
    }

    private void Store(
        LeaderboardRoute route,
        Leaderboard board,
        WeeklyLeaderboardPeriod? period,
        int? favourite,
        long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        LeaderboardStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                LeaderboardState current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                int previous = outstanding_requests.GetValueOrDefault(route);
                long response_epoch = previous == 1
                    ? clean_request_epochs.GetValueOrDefault(route)
                    : 0;
                if (previous > 0)
                    outstanding_requests[route] = previous - 1;
                if (previous <= 1)
                    clean_request_epochs[route] = 0;
                var boards = new Dictionary<LeaderboardRoute, Leaderboard>(current.Boards)
                {
                    [route] = Clone(board)
                };
                Leaderboard stored = boards[route];
                LeaderboardState changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    BoardsRevision = checked(current.BoardsRevision + 1),
                    Boards = new ReadOnlyDictionary<LeaderboardRoute, Leaderboard>(boards),
                    Period = period is null ? current.Period : period with { },
                    FavouriteGroupId = favourite ?? current.FavouriteGroupId,
                    WeekOffset = period?.CurrentOffset ?? current.WeekOffset
                };
                Volatile.Write(ref state, changed);
                update = new LeaderboardStateUpdate(
                    LeaderboardStateChangeKind.Snapshot,
                    changed,
                    route,
                    stored,
                    response_epoch,
                    publication_epoch);
                publications.Enqueue(update);
                drain = !publishing;
                publishing = true;
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void CommitReset(Session? active)
    {
        long generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            LeaderboardStateUpdate update;
            lock (state_sync)
            {
                LeaderboardState current = state;
                if (generation < committed_generation ||
                    generation == reset_generation && ReferenceEquals(current.Session, active))
                {
                    return;
                }
                publication_epoch = checked(publication_epoch + 1);
                request_epochs.Clear();
                outstanding_requests.Clear();
                clean_request_epochs.Clear();
                var changed = new LeaderboardState(
                    active,
                    generation,
                    checked(current.Revision + 1),
                    checked(current.BoardsRevision + 1),
                    checked(current.SettingsRevision + 1),
                    EmptyBoards(),
                    null,
                    0,
                    0,
                    0);
                Volatile.Write(ref state, changed);
                committed_generation = generation;
                reset_generation = generation;
                publications.Clear();
                update = new LeaderboardStateUpdate(
                    LeaderboardStateChangeKind.Reset,
                    changed,
                    null,
                    null,
                    0,
                    publication_epoch);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private Exception? DrainIfNeeded(bool drain)
    {
        if (!drain)
            return null;
        try
        {
            DrainPublications();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            LeaderboardStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
                delivering = true;
                delivery_thread_id = Environment.CurrentManagedThreadId;
            }
            try
            {
                if (!UpdateCurrent(update))
                    continue;
                failure = Notify(StateChanged, update, update, failure);
                if (update.Kind is LeaderboardStateChangeKind.Snapshot &&
                    update.Route is { } route &&
                    update.Board is { } board &&
                    UpdateCurrent(update))
                {
                    failure = Notify(
                        BoardReceived,
                        route.Scope,
                        route.Weekly,
                        board,
                        update,
                        failure);
                }
            }
            finally
            {
                lock (publication_sync)
                {
                    delivering = false;
                    delivery_thread_id = 0;
                    Monitor.PulseAll(publication_sync);
                }
            }
        }
        ThrowFailure(failure);
    }

    private bool UpdateCurrent(LeaderboardStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            LeaderboardState current = State;
            if (current.SessionGeneration != update.State.SessionGeneration ||
                !ReferenceEquals(current.Session, update.State.Session))
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active, update.State.Session);
    }

    private Exception? NotifyCommitted(LeaderboardStateUpdate update) =>
        Notify(StateCommitted, update, update, null, false);

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        LeaderboardStateUpdate update,
        Exception? failure,
        bool require_current = true)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (require_current && !UpdateCurrent(update))
                break;
            try
            {
                listener(value);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? Notify<T1, T2, T3>(
        Action<T1, T2, T3>? listeners,
        T1 first,
        T2 second,
        T3 third,
        LeaderboardStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T1, T2, T3> listener in listeners.GetInvocationList().Cast<Action<T1, T2, T3>>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener(first, second, third);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private void RequireScope(Session expected, long generation)
    {
        LeaderboardState current = state;
        if (!ReferenceEquals(current.Session, expected) ||
            current.SessionGeneration != generation ||
            committed_generation != generation)
        {
            throw new InvalidOperationException(
                "The leaderboard request correlation belongs to a stale hotel session.");
        }
    }

    private bool StateCurrent(LeaderboardState current, Session active, long generation) =>
        generation == committed_generation &&
        current.SessionGeneration == generation &&
        ReferenceEquals(current.Session, active);

    private ILeaderboardOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Leaderboard operations are unavailable until the application runtime is active.");

    private static LeaderboardState InitialState() => new(
        null,
        0,
        0,
        0,
        0,
        EmptyBoards(),
        null,
        0,
        0,
        0);

    private static IReadOnlyDictionary<LeaderboardRoute, Leaderboard> EmptyBoards() =>
        new ReadOnlyDictionary<LeaderboardRoute, Leaderboard>(
            new Dictionary<LeaderboardRoute, Leaderboard>());

    private static Leaderboard Clone(Leaderboard value) => value with
    {
        Entries = value.Entries.Select(entry => entry with { }).ToArray()
    };

    private static void ThrowFailures(Exception? first, Exception? second)
    {
        if (first is not null && second is not null)
            throw new AggregateException(first, second);
        if (first is not null)
            ExceptionDispatchInfo.Capture(first).Throw();
        if (second is not null)
            ExceptionDispatchInfo.Capture(second).Throw();
    }

    private static void ThrowFailure(Exception? failure)
    {
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
