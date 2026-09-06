using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Quests;

namespace Qx.Game;

internal enum QuestRequestRoute
{
    Available,
    Seasonal,
    Daily
}

internal enum QuestStateChangeKind
{
    Available,
    Seasonal,
    Current,
    Completed,
    Cancelled,
    Daily,
    Request,
    Reset
}

internal sealed record QuestState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long AvailableRevision,
    long SeasonalRevision,
    long CurrentRevision,
    long CompletionRevision,
    long CancellationRevision,
    long DailyRevision,
    bool AvailableLoaded,
    bool SeasonalLoaded,
    bool DailyLoaded,
    bool OpenWindow,
    IReadOnlyList<QuestData> Available,
    IReadOnlyList<QuestData> Seasonal,
    QuestData? Current,
    QuestCompleted? LastCompletion,
    QuestCancelled? LastCancellation,
    QuestDaily? Daily);

internal sealed record QuestStateUpdate(
    QuestStateChangeKind Kind,
    QuestState State,
    object? Value,
    QuestRequestRoute? Route,
    long RequestEpoch,
    long PublicationEpoch);

internal readonly record struct QuestRequestCorrelation(
    QuestState State,
    long RequestEpoch,
    int OutstandingRequests);

public sealed class QuestManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<QuestStateUpdate> publications = [];
    private QuestState state = InitialState();
    private IQuestOperations? operations;
    private long available_request_epoch;
    private int available_outstanding;
    private long available_clean_epoch;
    private long seasonal_request_epoch;
    private int seasonal_outstanding;
    private long seasonal_clean_epoch;
    private long daily_request_epoch;
    private int daily_outstanding;
    private long daily_clean_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    public IReadOnlyList<QuestData> Available => State.Available.Select(Clone).ToArray();
    public IReadOnlyList<QuestData> Seasonal => State.Seasonal.Select(Clone).ToArray();
    public QuestData? Current => State.Current is { } value ? Clone(value) : null;
    public QuestCompleted? LastCompletion =>
        State.LastCompletion is { } value ? Clone(value) : null;
    public QuestCancelled? LastCancellation =>
        State.LastCancellation is { } value ? Clone(value) : null;
    public QuestDaily? Daily => State.Daily is { } value ? Clone(value) : null;
    public bool OpenWindow => State.OpenWindow;

    public event Action<Quests>? AvailableChanged;
    public event Action<QuestsSeasonal>? SeasonalChanged;
    public event Action<QuestData>? CurrentChanged;
    public event Action<QuestCompleted>? Completed;
    public event Action<QuestCancelled>? Cancelled;
    public event Action<QuestDaily>? DailyChanged;
    public event Action? ResetCompleted;
    internal event Action<QuestStateUpdate>? StateCommitted;
    internal event Action<QuestStateUpdate>? StateChanged;

    internal QuestState State => Volatile.Read(ref state);

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Quests.Request,
            (_, generation) => ObserveRequest(QuestRequestRoute.Available, generation));
        OnOutgoing(
            MessageContracts.Quests.SeasonalRequest,
            (_, generation) => ObserveRequest(QuestRequestRoute.Seasonal, generation));
        OnOutgoing(
            MessageContracts.Quests.DailyRequest,
            (_, generation) => ObserveRequest(QuestRequestRoute.Daily, generation));
        OnIncoming(MessageContracts.Quests.Snapshot, ApplyAvailable);
        OnIncoming(MessageContracts.Quests.SeasonalSnapshot, ApplySeasonal);
        OnIncoming(MessageContracts.Quests.Updated, ApplyCurrent);
        OnIncoming(MessageContracts.Quests.Completed, ApplyCompletion);
        OnIncoming(MessageContracts.Quests.Cancelled, ApplyCancellation);
        OnIncoming(MessageContracts.Quests.Daily, ApplyDaily);
    }

    public void RequestAvailable() => Operations().RequestAvailable();

    public Task<IReadOnlyList<QuestData>> EnsureAvailableLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureAvailableLoadedAsync(timeoutMs, cancellationToken);
    }

    public void RequestSeasonal() => Operations().RequestSeasonal();

    public void RequestDaily(bool is_easy, int index) =>
        Operations().RequestDaily(is_easy, index);

    public void Accept(Id quest_id) => Operations().Accept(quest_id);

    public void Activate(Id quest_id) => Operations().Activate(quest_id);

    public void Reject(Id quest_id) => Operations().Reject(quest_id);

    public void Cancel() => Operations().Cancel();

    public void OpenTracker() => Operations().OpenTracker();

    public void CompleteFriendRequestQuest() =>
        Operations().CompleteFriendRequestQuest();

    internal void BindOperations(IQuestOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Quest operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IQuestOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal QuestRequestCorrelation CaptureRequestCorrelation(
        QuestRequestRoute route,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            return new QuestRequestCorrelation(
                state,
                RequestEpoch(route),
                Outstanding(route));
        }
    }

    internal long AdvanceLegacyRequest(
        QuestRequestRoute route,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        QuestStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                update = BeginRequestUnsafe(route, expected_session, expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal long AdvanceTypedRequest(
        QuestRequestRoute route,
        long baseline,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        QuestStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (RequestEpoch(route) != baseline || Outstanding(route) != 0)
                {
                    throw new InvalidOperationException(
                        "The quest request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(route, expected_session, expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal bool TryAdvanceTypedRequestIfUnloaded(
        QuestRequestRoute route,
        long baseline,
        Session expected_session,
        long expected_session_generation,
        out long request_epoch,
        out QuestState current_state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        QuestStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                current_state = state;
                bool loaded = route switch
                {
                    QuestRequestRoute.Available => current_state.AvailableLoaded,
                    QuestRequestRoute.Seasonal => current_state.SeasonalLoaded,
                    QuestRequestRoute.Daily => current_state.DailyLoaded,
                    _ => throw new ArgumentOutOfRangeException(nameof(route))
                };
                if (loaded)
                {
                    request_epoch = RequestEpoch(route);
                    return false;
                }
                if (RequestEpoch(route) != baseline || Outstanding(route) != 0)
                {
                    throw new InvalidOperationException(
                        "The quest request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(route, expected_session, expected_session_generation);
                request_epoch = update.RequestEpoch;
                current_state = state;
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return true;
    }

    internal bool RequestEpochIsCurrent(
        QuestRequestRoute route,
        long expected_epoch,
        Session expected_session,
        long expected_session_generation)
    {
        lock (state_sync)
        {
            QuestState current = state;
            if (!ReferenceEquals(current.Session, expected_session) ||
                current.SessionGeneration != expected_session_generation ||
                RequestEpoch(route) != expected_epoch)
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == expected_session_generation &&
            after == expected_session_generation &&
            ReferenceEquals(active_session, expected_session);
    }

    internal bool IsCurrentPublication(QuestStateUpdate update) => UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void ApplyAvailable(Quests message, long state_generation)
    {
        var snapshot = new Quests(message.Items.Select(Clone).ToArray(), message.OpenWindow);
        Store(
            state_generation,
            QuestStateChangeKind.Available,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                AvailableRevision = checked(current.AvailableRevision + 1),
                AvailableLoaded = true,
                OpenWindow = snapshot.OpenWindow,
                Available = snapshot.Items
            },
            QuestRequestRoute.Available);
    }

    private void ApplySeasonal(QuestsSeasonal message, long state_generation)
    {
        var snapshot = new QuestsSeasonal(message.Items.Select(Clone).ToArray());
        Store(
            state_generation,
            QuestStateChangeKind.Seasonal,
            snapshot,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                SeasonalRevision = checked(current.SeasonalRevision + 1),
                SeasonalLoaded = true,
                Seasonal = snapshot.Items
            },
            QuestRequestRoute.Seasonal);
    }

    private void ApplyCurrent(Quest message, long state_generation)
    {
        QuestData value = Clone(message.Data);
        Store(
            state_generation,
            QuestStateChangeKind.Current,
            value,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                CurrentRevision = checked(current.CurrentRevision + 1),
                Current = value
            });
    }

    private void ApplyCompletion(QuestCompleted message, long state_generation)
    {
        QuestCompleted value = Clone(message);
        Store(
            state_generation,
            QuestStateChangeKind.Completed,
            value,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                CompletionRevision = checked(current.CompletionRevision + 1),
                LastCompletion = value
            });
    }

    private void ApplyCancellation(QuestCancelled message, long state_generation)
    {
        QuestCancelled value = Clone(message);
        Store(
            state_generation,
            QuestStateChangeKind.Cancelled,
            value,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                CancellationRevision = checked(current.CancellationRevision + 1),
                LastCancellation = value
            });
    }

    private void ApplyDaily(QuestDaily message, long state_generation)
    {
        QuestDaily value = Clone(message);
        Store(
            state_generation,
            QuestStateChangeKind.Daily,
            value,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                DailyRevision = checked(current.DailyRevision + 1),
                DailyLoaded = true,
                Daily = value
            },
            QuestRequestRoute.Daily);
    }

    private void ObserveRequest(QuestRequestRoute route, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        QuestStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (RequestScopeCurrent(active_session, state_generation))
                    update = BeginRequestUnsafe(route, active_session, state_generation);
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private QuestStateUpdate BeginRequestUnsafe(
        QuestRequestRoute route,
        Session expected_session,
        long expected_session_generation)
    {
        long next = checked(RequestEpoch(route) + 1);
        int previous = Outstanding(route);
        int outstanding = checked(previous + 1);
        if (!ApplyIfCurrent(
                expected_session_generation,
                expected_session,
                () =>
                {
                    SetRequestEpoch(route, next);
                    SetOutstanding(route, outstanding);
                    SetCleanEpoch(route, previous == 0 ? next : 0);
                }))
        {
            throw new InvalidOperationException(
                "The hotel session changed before the quest request could be dispatched.");
        }
        return new QuestStateUpdate(
            QuestStateChangeKind.Request,
            state,
            null,
            route,
            next,
            publication_epoch);
    }

    private void Store(
        long state_generation,
        QuestStateChangeKind kind,
        object value,
        Func<QuestState, QuestState> mutation,
        QuestRequestRoute? response_route = null)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            QuestStateUpdate update;
            lock (state_sync)
            {
                QuestState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return;
                int previous = response_route is { } route ? Outstanding(route) : 0;
                int outstanding = previous > 0 ? previous - 1 : 0;
                long response_epoch = response_route is { } response && previous == 1
                    ? CleanEpoch(response)
                    : 0;
                QuestState updated = mutation(current);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        if (response_route is { } current_route)
                        {
                            SetOutstanding(current_route, outstanding);
                            if (previous <= 1)
                                SetCleanEpoch(current_route, 0);
                        }
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new QuestStateUpdate(
                            kind,
                            updated,
                            value,
                            response_route,
                            response_epoch,
                            publication_epoch);
                    }))
                {
                    return;
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void CommitReset(Session? active_session)
    {
        long state_generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            QuestStateUpdate update;
            lock (state_sync)
            {
                QuestState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new QuestState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.AvailableRevision + 1),
                    checked(current.SeasonalRevision + 1),
                    checked(current.CurrentRevision + 1),
                    checked(current.CompletionRevision + 1),
                    checked(current.CancellationRevision + 1),
                    checked(current.DailyRevision + 1),
                    false,
                    false,
                    false,
                    false,
                    Array.AsReadOnly(Array.Empty<QuestData>()),
                    Array.AsReadOnly(Array.Empty<QuestData>()),
                    null,
                    null,
                    null,
                    null);
                Volatile.Write(ref state, updated);
                available_request_epoch = 0;
                available_outstanding = 0;
                available_clean_epoch = 0;
                seasonal_request_epoch = 0;
                seasonal_outstanding = 0;
                seasonal_clean_epoch = 0;
                daily_request_epoch = 0;
                daily_outstanding = 0;
                daily_clean_epoch = 0;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new QuestStateUpdate(
                    QuestStateChangeKind.Reset,
                    updated,
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
            QuestStateUpdate update;
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
                if (!UpdateCurrent(update))
                    continue;
                failure = NotifyLegacy(update, failure);
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

    private Exception? NotifyLegacy(QuestStateUpdate update, Exception? failure)
    {
        switch (update.Kind)
        {
            case QuestStateChangeKind.Available:
                return Notify(AvailableChanged, (Quests)update.Value!, update, failure);
            case QuestStateChangeKind.Seasonal:
                return Notify(SeasonalChanged, (QuestsSeasonal)update.Value!, update, failure);
            case QuestStateChangeKind.Current:
                return Notify(CurrentChanged, (QuestData)update.Value!, update, failure);
            case QuestStateChangeKind.Completed:
                return Notify(Completed, (QuestCompleted)update.Value!, update, failure);
            case QuestStateChangeKind.Cancelled:
                return Notify(Cancelled, (QuestCancelled)update.Value!, update, failure);
            case QuestStateChangeKind.Daily:
                return Notify(DailyChanged, (QuestDaily)update.Value!, update, failure);
            case QuestStateChangeKind.Reset:
                return Notify(ResetCompleted, update, failure);
            case QuestStateChangeKind.Request:
                return failure;
            default:
                throw new ArgumentOutOfRangeException(nameof(update));
        }
    }

    private bool UpdateCurrent(QuestStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            QuestState current = State;
            if (current.SessionGeneration != update.State.SessionGeneration ||
                !ReferenceEquals(current.Session, update.State.Session))
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active_session, update.State.Session);
    }

    private Exception? NotifyCommitted(QuestStateUpdate update) =>
        Notify(StateCommitted, update, update, null, false);

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        QuestStateUpdate update,
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

    private Exception? Notify(
        Action? listeners,
        QuestStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action listener in listeners.GetInvocationList().Cast<Action>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener();
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private bool StateCurrent(
        QuestState current,
        Session active_session,
        long state_generation) =>
        state_generation == committed_generation &&
        current.SessionGeneration == state_generation &&
        ReferenceEquals(current.Session, active_session);

    private bool RequestScopeCurrent(Session active_session, long state_generation) =>
        StateCurrent(state, active_session, state_generation);

    private void RequireRequestScope(
        Session expected_session,
        long expected_session_generation,
        string operation)
    {
        QuestState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The quest request correlation cannot be {operation} for a stale hotel session.");
        }
    }

    private long RequestEpoch(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => available_request_epoch,
        QuestRequestRoute.Seasonal => seasonal_request_epoch,
        QuestRequestRoute.Daily => daily_request_epoch,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private int Outstanding(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => available_outstanding,
        QuestRequestRoute.Seasonal => seasonal_outstanding,
        QuestRequestRoute.Daily => daily_outstanding,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private long CleanEpoch(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => available_clean_epoch,
        QuestRequestRoute.Seasonal => seasonal_clean_epoch,
        QuestRequestRoute.Daily => daily_clean_epoch,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private void SetRequestEpoch(QuestRequestRoute route, long value)
    {
        switch (route)
        {
            case QuestRequestRoute.Available:
                available_request_epoch = value;
                return;
            case QuestRequestRoute.Seasonal:
                seasonal_request_epoch = value;
                return;
            case QuestRequestRoute.Daily:
                daily_request_epoch = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private void SetOutstanding(QuestRequestRoute route, int value)
    {
        switch (route)
        {
            case QuestRequestRoute.Available:
                available_outstanding = value;
                return;
            case QuestRequestRoute.Seasonal:
                seasonal_outstanding = value;
                return;
            case QuestRequestRoute.Daily:
                daily_outstanding = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private void SetCleanEpoch(QuestRequestRoute route, long value)
    {
        switch (route)
        {
            case QuestRequestRoute.Available:
                available_clean_epoch = value;
                return;
            case QuestRequestRoute.Seasonal:
                seasonal_clean_epoch = value;
                return;
            case QuestRequestRoute.Daily:
                daily_clean_epoch = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private IQuestOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Quest operations are unavailable until the application runtime is active.");

    private static QuestState InitialState() => new(
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        Array.AsReadOnly(Array.Empty<QuestData>()),
        Array.AsReadOnly(Array.Empty<QuestData>()),
        null,
        null,
        null,
        null);

    private static QuestData Clone(QuestData value) => value with { };

    private static QuestCompleted Clone(QuestCompleted value) =>
        new(Clone(value.Data), value.ShowDialog);

    private static QuestCancelled Clone(QuestCancelled value) =>
        new(value.IsExpired, Clone(value.Data));

    private static QuestDaily Clone(QuestDaily value) =>
        new(
            value.Data is { } data ? Clone(data) : null,
            value.EasyQuestCount,
            value.HardQuestCount);

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
