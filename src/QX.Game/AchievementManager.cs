using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

public sealed record AchievementCategory(string Code, IReadOnlyList<Achievement> Achievements)
{
    public const string Archive = "archive";
    public const string New = "new";
    public const string WiredGames = "wired_games";

    public int Progress => Achievements.Sum(achievement => achievement.LevelsAchieved);
    public int MaxProgress => Achievements.Sum(achievement => achievement.LevelCount);
    public double Completion => MaxProgress <= 0 ? 0 : (double)Progress / MaxProgress;
    public bool IsComplete => MaxProgress > 0 && Progress >= MaxProgress;
    public bool IsListed => Code != New && Code != WiredGames;
}

internal enum AchievementRequestRoute
{
    List,
    PointLimits
}

internal enum AchievementStateChangeKind
{
    Snapshot,
    Updated,
    Score,
    PointLimits,
    NewCodes,
    Request,
    Reset
}

internal sealed record AchievementState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long ListRevision,
    long BaselineRevision,
    bool Loaded,
    IReadOnlyList<Achievement> Achievements,
    string DefaultCategory,
    long ScoreRevision,
    bool ScoreLoaded,
    int Score,
    long PointLimitsRevision,
    bool PointLimitsLoaded,
    BadgePointLimits PointLimits,
    long NewCodesRevision,
    IReadOnlyList<string> NewCodes);

internal sealed record AchievementSnapshotCommit(
    IReadOnlyList<Achievement> Items,
    string DefaultCategory);

internal sealed record AchievementDeltaCommit(
    Achievement Current,
    Achievement? Previous);

internal sealed record AchievementStateUpdate(
    AchievementStateChangeKind Kind,
    AchievementState State,
    object? Value,
    AchievementRequestRoute? Route,
    long RequestEpoch,
    long PublicationEpoch);

public sealed class AchievementManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<AchievementStateUpdate> publications = [];
    private AchievementState state = InitialState();
    private IAchievementOperations? operations;
    private long list_request_epoch;
    private long point_limits_request_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    public IReadOnlyList<string> NewAchievementCodes
    {
        get => ReadOnly(State.NewCodes);
        set
        {
            string[] codes = value is null
                ? []
                : [.. value.Where(code => code.Length > 0)];
            StoreExternal(
                AchievementStateChangeKind.NewCodes,
                ReadOnly(codes),
                current => current with
                {
                    Revision = checked(current.Revision + 1),
                    NewCodesRevision = checked(current.NewCodesRevision + 1),
                    NewCodes = ReadOnly(codes)
                });
        }
    }

    public IReadOnlyList<Achievement> All => Clone(State.Achievements);
    public bool IsLoaded => State.Loaded;
    public int Score => State.Score;
    public bool IsScoreLoaded => State.ScoreLoaded;
    public string DefaultCategory => State.DefaultCategory;
    public BadgePointLimits PointLimits => Clone(State.PointLimits);
    public bool ArePointLimitsLoaded => State.PointLimitsLoaded;

    internal AchievementState State => Volatile.Read(ref state);

    public Achievement? ById(int id)
    {
        Achievement? value = State.Achievements.FirstOrDefault(achievement => achievement.Id == id);
        return value is null ? null : Clone(value);
    }

    public Achievement? ByCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string wanted = Achievement.CodeOf(code);
        Achievement? value = State.Achievements.FirstOrDefault(achievement =>
            string.Equals(achievement.Code, wanted, StringComparison.OrdinalIgnoreCase));
        return value is null ? null : Clone(value);
    }

    public Achievement? ByBadge(string badgeCode) => ByCode(badgeCode);

    public IReadOnlyList<AchievementCategory> Categories => CategoriesFor(State);

    public AchievementCategory? Category(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Categories.FirstOrDefault(category =>
            string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public int Progress => Listed(State).Sum(achievement => achievement.LevelsAchieved);
    public int MaxProgress => Listed(State).Sum(achievement => achievement.LevelCount);

    public double Completion
    {
        get
        {
            AchievementState snapshot = State;
            int progress = Listed(snapshot).Sum(achievement => achievement.LevelsAchieved);
            int maximum = Listed(snapshot).Sum(achievement => achievement.LevelCount);
            return maximum <= 0 ? 0 : (double)progress / maximum;
        }
    }

    public IReadOnlyList<Achievement> Unfinished => Clone(
        Listed(State).Where(achievement => !achievement.IsFinalLevel));

    public IReadOnlyList<Achievement> Finished => Clone(
        Listed(State).Where(achievement => achievement.IsFinalLevel));

    public IReadOnlyList<Achievement> Closest(int count = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return Clone(Listed(State)
            .Where(achievement => !achievement.IsFinalLevel && achievement.ShowsProgress)
            .OrderByDescending(achievement => achievement.Progress)
            .ThenBy(achievement => achievement.PointsToNextLevel)
            .Take(count));
    }

    public NextBadge? Next(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        AchievementState snapshot = State;
        string wanted = Achievement.CodeOf(code);
        Achievement? achievement = snapshot.Achievements.FirstOrDefault(value =>
            string.Equals(value.Code, wanted, StringComparison.OrdinalIgnoreCase));
        if (achievement is null || achievement.NextBadgeCode is not { } badge)
            return null;

        int limit = snapshot.PointLimits.Limit(achievement.Code, achievement.Level + 1)
            ?? achievement.ScoreLimit;
        return new NextBadge(
            Clone(achievement),
            badge,
            achievement.Level + 1,
            limit,
            achievement.CurrentPoints,
            achievement.PointsToNextLevel);
    }

    public event Action<IReadOnlyList<Achievement>>? ListChanged;
    public event Action<Achievement>? Updated;
    public event Action<Achievement, Achievement>? LevelUp;
    public event Action<int>? ScoreChanged;
    public event Action? Changed;
    internal event Action<AchievementStateUpdate>? StateCommitted;
    internal event Action<AchievementStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Achievements.Request,
            (_, generation) => ObserveRequest(AchievementRequestRoute.List, generation));
        OnOutgoing(
            MessageContracts.Achievements.PointLimitsRequest,
            (_, generation) => ObserveRequest(
                AchievementRequestRoute.PointLimits,
                generation));
        OnIncoming(MessageContracts.Achievements.Snapshot, ApplySnapshot);
        OnIncoming(MessageContracts.Achievements.Updated, ApplyUpdate);
        OnIncoming(
            ClientType.Flash,
            MessageContracts.Achievements.Score,
            ApplyScore);
        OnIncoming(MessageContracts.Achievements.PointLimits, ApplyPointLimits);
    }

    public void Request() => Operations().RequestAchievements();

    public void RequestPointLimits() => Operations().RequestPointLimits();

    public bool IsFlashOnlyDataSupported =>
        (Interceptor.Session?.Client ?? Interceptor.Messages.ActiveClient) is not ClientType.Unity;

    public Task<IReadOnlyList<Achievement>> EnsureLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureAchievementsLoadedAsync(timeoutMs, cancellationToken);
    }

    public Task<BadgePointLimits> EnsurePointLimitsLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsurePointLimitsLoadedAsync(timeoutMs, cancellationToken);
    }

    internal void BindOperations(IAchievementOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Achievement operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IAchievementOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal long CaptureRequestEpoch(
        AchievementRequestRoute route,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            return RequestEpoch(route);
        }
    }

    internal long AdvanceRequestEpoch(
        AchievementRequestRoute route,
        long baseline,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        AchievementStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (RequestEpoch(route) != baseline)
                {
                    throw new InvalidOperationException(
                        "Another achievement request was dispatched before the operation could send.");
                }
                long next = checked(baseline + 1);
                if (!ApplyIfCurrent(
                        expected_session_generation,
                        expected_session,
                        () => SetRequestEpoch(route, next)))
                {
                    throw new InvalidOperationException(
                        "The hotel session changed before the achievement request could be dispatched.");
                }
                update = new AchievementStateUpdate(
                    AchievementStateChangeKind.Request,
                    state,
                    null,
                    route,
                    next,
                    publication_epoch);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal bool TryAdvanceRequestEpochIfUnloaded(
        AchievementRequestRoute route,
        long baseline,
        Session expected_session,
        long expected_session_generation,
        out long request_epoch,
        out AchievementState current_state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        AchievementStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (RequestEpoch(route) != baseline)
                {
                    throw new InvalidOperationException(
                        "Another achievement request was dispatched before the operation could send.");
                }
                current_state = state;
                bool loaded = route is AchievementRequestRoute.List
                    ? current_state.Loaded
                    : current_state.PointLimitsLoaded;
                if (loaded)
                {
                    request_epoch = baseline;
                    return false;
                }
                long next = checked(baseline + 1);
                if (!ApplyIfCurrent(
                        expected_session_generation,
                        expected_session,
                        () => SetRequestEpoch(route, next)))
                {
                    throw new InvalidOperationException(
                        "The hotel session changed before the achievement request could be dispatched.");
                }
                request_epoch = next;
                current_state = state;
                update = new AchievementStateUpdate(
                    AchievementStateChangeKind.Request,
                    current_state,
                    null,
                    route,
                    request_epoch,
                    publication_epoch);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return true;
    }

    internal bool RequestEpochIsCurrent(
        AchievementRequestRoute route,
        long expected_epoch,
        Session expected_session,
        long expected_session_generation)
    {
        lock (state_sync)
        {
            AchievementState current = state;
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

    internal bool IsCurrentPublication(AchievementStateUpdate update) => UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void ApplySnapshot(Achievements message, long state_generation)
    {
        ReadOnlyCollection<Achievement> received = Clone(message.Items);
        ReadOnlyCollection<Achievement> canonical = Canonical(received);
        var commit = new AchievementSnapshotCommit(received, message.DefaultCategory);
        Store(
            state_generation,
            AchievementStateChangeKind.Snapshot,
            AchievementRequestRoute.List,
            commit,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                ListRevision = checked(current.ListRevision + 1),
                BaselineRevision = checked(current.BaselineRevision + 1),
                Loaded = true,
                Achievements = canonical,
                DefaultCategory = message.DefaultCategory
            });
    }

    private void ApplyUpdate(AchievementUpdate message, long state_generation)
    {
        Achievement current_value = Clone(message.Achievement);
        Achievement? previous_value = null;
        Store(
            state_generation,
            AchievementStateChangeKind.Updated,
            AchievementRequestRoute.List,
            null,
            current =>
            {
                Achievement[] items = current.Achievements.Select(Clone).ToArray();
                int index = Array.FindIndex(items, item => item.Id == current_value.Id);
                if (index < 0)
                    items = [.. items, Clone(current_value)];
                else
                {
                    previous_value = Clone(items[index]);
                    items[index] = Clone(current_value);
                }
                return current with
                {
                    Revision = checked(current.Revision + 1),
                    ListRevision = checked(current.ListRevision + 1),
                    Achievements = Array.AsReadOnly(items)
                };
            },
            () => new AchievementDeltaCommit(
                Clone(current_value),
                previous_value is null ? null : Clone(previous_value)));
    }

    private void ApplyScore(AchievementScore message, long state_generation) => Store(
        state_generation,
        AchievementStateChangeKind.Score,
        null,
        message.Score,
        current => current with
        {
            Revision = checked(current.Revision + 1),
            ScoreRevision = checked(current.ScoreRevision + 1),
            ScoreLoaded = true,
            Score = message.Score
        });

    private void ApplyPointLimits(BadgePointLimits message, long state_generation)
    {
        BadgePointLimits limits = Clone(message);
        Store(
            state_generation,
            AchievementStateChangeKind.PointLimits,
            AchievementRequestRoute.PointLimits,
            limits,
            current => current with
            {
                Revision = checked(current.Revision + 1),
                PointLimitsRevision = checked(current.PointLimitsRevision + 1),
                PointLimitsLoaded = true,
                PointLimits = limits
            });
    }

    private void ObserveRequest(AchievementRequestRoute route, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        AchievementStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                AchievementState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                long next = checked(RequestEpoch(route) + 1);
                if (ApplyIfCurrent(
                    state_generation,
                    active_session,
                    () => SetRequestEpoch(route, next)))
                {
                    update = new AchievementStateUpdate(
                        AchievementStateChangeKind.Request,
                        current,
                        null,
                        route,
                        next,
                        publication_epoch);
                }
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private void Store(
        long state_generation,
        AchievementStateChangeKind kind,
        AchievementRequestRoute? route,
        object? value,
        Func<AchievementState, AchievementState> mutation,
        Func<object?>? committed_value = null)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            AchievementStateUpdate update;
            lock (state_sync)
            {
                AchievementState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                AchievementState updated = mutation(current);
                long request_epoch = route is { } request_route
                    ? RequestEpoch(request_route)
                    : 0;
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new AchievementStateUpdate(
                            kind,
                            updated,
                            committed_value?.Invoke() ?? value,
                            route,
                            request_epoch,
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

    private void StoreExternal(
        AchievementStateChangeKind kind,
        object value,
        Func<AchievementState, AchievementState> mutation)
    {
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            AchievementStateUpdate update;
            lock (state_sync)
            {
                AchievementState updated = mutation(state);
                Volatile.Write(ref state, updated);
                update = new AchievementStateUpdate(
                    kind,
                    updated,
                    value,
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
            AchievementStateUpdate update;
            lock (state_sync)
            {
                AchievementState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new AchievementState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.ListRevision + 1),
                    current.BaselineRevision,
                    false,
                    ReadOnly(Array.Empty<Achievement>()),
                    "",
                    checked(current.ScoreRevision + 1),
                    false,
                    0,
                    checked(current.PointLimitsRevision + 1),
                    false,
                    new BadgePointLimits(ReadOnly(Array.Empty<BadgePointLimit>())),
                    checked(current.NewCodesRevision + 1),
                    ReadOnly(Array.Empty<string>()));
                Volatile.Write(ref state, updated);
                list_request_epoch = 0;
                point_limits_request_epoch = 0;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new AchievementStateUpdate(
                    AchievementStateChangeKind.Reset,
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
            AchievementStateUpdate update;
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

    private bool UpdateCurrent(AchievementStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            AchievementState current = State;
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

    private Exception? NotifyCommitted(AchievementStateUpdate update) =>
        Notify(StateCommitted, update, update, null);

    private Exception? NotifyLegacy(
        AchievementStateUpdate update,
        Exception? failure)
    {
        switch (update.Kind)
        {
            case AchievementStateChangeKind.Snapshot:
            {
                var commit = (AchievementSnapshotCommit)update.Value!;
                failure = Notify(
                    ListChanged,
                    () => Clone(commit.Items),
                    update,
                    failure);
                return Notify(Changed, update, failure);
            }
            case AchievementStateChangeKind.Updated:
            {
                var commit = (AchievementDeltaCommit)update.Value!;
                failure = Notify(Updated, () => Clone(commit.Current), update, failure);
                if (commit.Previous is { } previous && commit.Current.Level > previous.Level)
                {
                    failure = Notify(
                        LevelUp,
                        () => (Clone(previous), Clone(commit.Current)),
                        update,
                        failure);
                }
                return Notify(Changed, update, failure);
            }
            case AchievementStateChangeKind.Score:
                failure = Notify(ScoreChanged, () => (int)update.Value!, update, failure);
                return Notify(Changed, update, failure);
            case AchievementStateChangeKind.PointLimits:
            case AchievementStateChangeKind.NewCodes:
                return Notify(Changed, update, failure);
            case AchievementStateChangeKind.Reset:
            case AchievementStateChangeKind.Request:
                return failure;
            default:
                throw new ArgumentOutOfRangeException(nameof(update));
        }
    }

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        AchievementStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (!UpdateCurrent(update))
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

    private Exception? Notify<T>(
        Action<T>? listeners,
        Func<T> value,
        AchievementStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                listener(value());
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private Exception? Notify(
        Action<Achievement, Achievement>? listeners,
        Func<(Achievement Previous, Achievement Current)> value,
        AchievementStateUpdate update,
        Exception? failure)
    {
        if (listeners is null)
            return failure;
        foreach (Action<Achievement, Achievement> listener in listeners
            .GetInvocationList()
            .Cast<Action<Achievement, Achievement>>())
        {
            if (!UpdateCurrent(update))
                break;
            try
            {
                (Achievement previous, Achievement current) = value();
                listener(previous, current);
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
        AchievementStateUpdate update,
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

    private void RequireRequestScope(
        Session expected_session,
        long expected_session_generation,
        string operation)
    {
        AchievementState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The achievement request epoch cannot be {operation} for a stale hotel session.");
        }
    }

    private long RequestEpoch(AchievementRequestRoute route) => route switch
    {
        AchievementRequestRoute.List => list_request_epoch,
        AchievementRequestRoute.PointLimits => point_limits_request_epoch,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private void SetRequestEpoch(AchievementRequestRoute route, long value)
    {
        switch (route)
        {
            case AchievementRequestRoute.List:
                list_request_epoch = value;
                break;
            case AchievementRequestRoute.PointLimits:
                point_limits_request_epoch = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private IAchievementOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Achievement operations are unavailable until the application runtime is active.");

    private static AchievementState InitialState() => new(
        null,
        0,
        0,
        0,
        0,
        false,
        ReadOnly(Array.Empty<Achievement>()),
        "",
        0,
        false,
        0,
        0,
        false,
        new BadgePointLimits(ReadOnly(Array.Empty<BadgePointLimit>())),
        0,
        ReadOnly(Array.Empty<string>()));

    private static ReadOnlyCollection<Achievement> Canonical(
        IReadOnlyList<Achievement> values)
    {
        var positions = new Dictionary<int, int>();
        var result = new List<Achievement>(values.Count);
        foreach (Achievement value in values)
        {
            Achievement copy = Clone(value);
            if (positions.TryGetValue(copy.Id, out int index))
                result[index] = copy;
            else
            {
                positions.Add(copy.Id, result.Count);
                result.Add(copy);
            }
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static IReadOnlyList<AchievementCategory> CategoriesFor(AchievementState snapshot)
    {
        var by_code = new Dictionary<string, List<Achievement>>(StringComparer.Ordinal);
        var order = new List<string>();
        var archive = new List<Achievement>();
        var wired = new List<Achievement>();
        var fresh = new List<Achievement>();
        List<Achievement>? misc = null;
        foreach (Achievement achievement in snapshot.Achievements)
        {
            if (!achievement.IsListed)
                continue;
            List<Achievement> bucket;
            if (achievement.IsArchived)
                bucket = archive;
            else if (achievement.Category == AchievementCategory.WiredGames)
                bucket = wired;
            else if (!by_code.TryGetValue(achievement.Category, out List<Achievement>? existing))
            {
                bucket = [];
                by_code[achievement.Category] = bucket;
                if (achievement.Category == "misc")
                    misc = bucket;
                else
                    order.Add(achievement.Category);
            }
            else
                bucket = existing;
            bucket.Add(Clone(achievement));
            if (snapshot.NewCodes.Contains(achievement.Code, StringComparer.Ordinal))
                fresh.Add(Clone(achievement));
        }
        var categories = new List<AchievementCategory>();
        foreach (string code in order)
            categories.Add(new AchievementCategory(code, ReadOnly(by_code[code])));
        if (misc is not null)
            categories.Add(new AchievementCategory("misc", ReadOnly(misc)));
        categories.Add(new AchievementCategory(AchievementCategory.Archive, ReadOnly(archive)));
        categories.Add(new AchievementCategory(AchievementCategory.WiredGames, ReadOnly(wired)));
        if (fresh.Count > 0)
            categories.Add(new AchievementCategory(AchievementCategory.New, ReadOnly(fresh)));
        return Array.AsReadOnly(categories.ToArray());
    }

    private static IEnumerable<Achievement> Listed(AchievementState snapshot) =>
        snapshot.Achievements.Where(achievement => achievement.IsListed);

    internal static Achievement Clone(Achievement value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Achievement
        {
            Id = value.Id,
            Level = value.Level,
            BadgeCode = value.BadgeCode,
            BaseProgress = value.BaseProgress,
            MaxProgress = value.MaxProgress,
            LevelRewardPoints = value.LevelRewardPoints,
            LevelRewardPointType = value.LevelRewardPointType,
            CurrentProgress = value.CurrentProgress,
            IsComplete = value.IsComplete,
            Category = value.Category,
            Subcategory = value.Subcategory,
            MaxLevel = value.MaxLevel,
            DisplayMethod = value.DisplayMethod,
            State = value.State
        };
    }

    internal static BadgePointLimits Clone(BadgePointLimits value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BadgePointLimits(ReadOnly(value.Limits.Select(limit => limit with { })));
    }

    private static ReadOnlyCollection<Achievement> Clone(IEnumerable<Achievement> values) =>
        ReadOnly(values.Select(Clone));

    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static void ThrowFailure(Exception? failure)
    {
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void ThrowFailures(Exception? first, Exception? second)
    {
        if (first is not null && second is not null)
            throw new AggregateException(first, second);
        ThrowFailure(first ?? second);
    }
}

public sealed record NextBadge(
    Achievement Achievement,
    string BadgeCode,
    int Level,
    int PointLimit,
    int CurrentPoints,
    int PointsToGo);
