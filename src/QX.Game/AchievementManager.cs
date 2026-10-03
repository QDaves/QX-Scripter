using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

/// <summary>Represents a category of achievements as the client groups them.</summary>
/// <param name="Code">The category code, such as <see cref="AchievementCategory.Archive"/>.</param>
/// <param name="Achievements">The achievements filed under the category.</param>
public sealed record AchievementCategory(string Code, IReadOnlyList<Achievement> Achievements)
{
    /// <summary>The code of the category that holds archived achievements.</summary>
    public const string Archive = "archive";
    /// <summary>The code of the category that holds achievements marked as new.</summary>
    public const string New = "new";
    /// <summary>The code of the category that holds wired game achievements.</summary>
    public const string WiredGames = "wired_games";

    /// <summary>Gets the number of levels achieved across the category's achievements.</summary>
    public int Progress => Achievements.Sum(achievement => achievement.LevelsAchieved);
    /// <summary>Gets the total number of levels across the category's achievements.</summary>
    public int MaxProgress => Achievements.Sum(achievement => achievement.LevelCount);
    /// <summary>Gets the fraction of levels achieved, from 0 to 1.</summary>
    /// <remarks>Returns 0 when <see cref="MaxProgress"/> is 0.</remarks>
    public double Completion => MaxProgress <= 0 ? 0 : (double)Progress / MaxProgress;
    /// <summary>Gets whether every level in the category has been achieved.</summary>
    /// <remarks>Returns <see langword="false"/> when the category has no levels.</remarks>
    public bool IsComplete => MaxProgress > 0 && Progress >= MaxProgress;
    /// <summary>Gets whether the category is a regular category rather than <see cref="New"/> or <see cref="WiredGames"/>.</summary>
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

/// <summary>Manages the achievements, achievement score and badge point limits of the user.</summary>
/// <remarks>
/// All members are safe to call from any thread. The state is cleared when the hotel connection
/// closes and when a new session connects. Every read returns a copy, so changing a returned
/// <see cref="Achievement"/> does not affect the stored state.
/// </remarks>
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

    /// <summary>Gets or sets the codes of the achievements marked as new.</summary>
    /// <remarks>
    /// Achievements with these codes are listed in the <see cref="AchievementCategory.New"/>
    /// category. Empty codes are dropped when set, and setting the value raises <see cref="Changed"/>.
    /// </remarks>
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

    /// <summary>Gets every known achievement, including those the client does not list.</summary>
    public IReadOnlyList<Achievement> All => Clone(State.Achievements);
    /// <summary>Gets whether the achievement list has been received in the current session.</summary>
    public bool IsLoaded => State.Loaded;
    /// <summary>Gets the user's achievement score.</summary>
    /// <remarks>The score is 0 until <see cref="IsScoreLoaded"/> is <see langword="true"/>.</remarks>
    public int Score => State.Score;
    /// <summary>Gets whether the achievement score has been received in the current session.</summary>
    public bool IsScoreLoaded => State.ScoreLoaded;
    /// <summary>Gets the category the client opens first, as sent with the achievement list.</summary>
    public string DefaultCategory => State.DefaultCategory;
    /// <summary>Gets the points each badge level requires.</summary>
    public BadgePointLimits PointLimits => Clone(State.PointLimits);
    /// <summary>Gets whether the badge point limits have been received in the current session.</summary>
    public bool ArePointLimitsLoaded => State.PointLimitsLoaded;

    internal AchievementState State => Volatile.Read(ref state);

    /// <summary>Gets the achievement with the specified id.</summary>
    /// <param name="id">The id of the achievement.</param>
    /// <returns>The achievement, or <see langword="null"/> if it is not known.</returns>
    public Achievement? ById(int id)
    {
        Achievement? value = State.Achievements.FirstOrDefault(achievement => achievement.Id == id);
        return value is null ? null : Clone(value);
    }

    /// <summary>Gets the achievement with the specified code.</summary>
    /// <remarks>
    /// The code is reduced with <see cref="Achievement.CodeOf(string)"/> first, so a badge code such as
    /// <c>ACH_RoomEntry5</c> matches the <c>RoomEntry</c> achievement. The comparison ignores case.
    /// </remarks>
    /// <param name="code">The achievement code or a badge code of one of its levels.</param>
    /// <returns>The achievement, or <see langword="null"/> if it is not known.</returns>
    public Achievement? ByCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string wanted = Achievement.CodeOf(code);
        Achievement? value = State.Achievements.FirstOrDefault(achievement =>
            string.Equals(achievement.Code, wanted, StringComparison.OrdinalIgnoreCase));
        return value is null ? null : Clone(value);
    }

    /// <summary>Gets the achievement that grants the specified badge.</summary>
    /// <remarks>Equivalent to <see cref="ByCode(string)"/>.</remarks>
    /// <param name="badgeCode">The badge code, such as <c>ACH_RoomEntry5</c>.</param>
    /// <returns>The achievement, or <see langword="null"/> if it is not known.</returns>
    public Achievement? ByBadge(string badgeCode) => ByCode(badgeCode);

    /// <summary>Gets the listed achievements grouped into categories the way the client shows them.</summary>
    /// <remarks>
    /// Categories keep the order in which they first appear, followed by <c>misc</c> when present,
    /// then <see cref="AchievementCategory.Archive"/> and <see cref="AchievementCategory.WiredGames"/>,
    /// which are always included, and <see cref="AchievementCategory.New"/> when any achievement is
    /// marked as new. Archived achievements are only filed under the archive category.
    /// </remarks>
    public IReadOnlyList<AchievementCategory> Categories => CategoriesFor(State);

    /// <summary>Gets the category with the specified code.</summary>
    /// <param name="code">The category code. The comparison ignores case.</param>
    /// <returns>The category, or <see langword="null"/> if there is none with that code.</returns>
    public AchievementCategory? Category(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Categories.FirstOrDefault(category =>
            string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets the number of levels achieved across all listed achievements.</summary>
    public int Progress => Listed(State).Sum(achievement => achievement.LevelsAchieved);
    /// <summary>Gets the total number of levels across all listed achievements.</summary>
    public int MaxProgress => Listed(State).Sum(achievement => achievement.LevelCount);

    /// <summary>Gets the fraction of levels achieved across all listed achievements, from 0 to 1.</summary>
    /// <remarks>Returns 0 when no levels are known.</remarks>
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

    /// <summary>Gets the listed achievements that have not reached their final level.</summary>
    public IReadOnlyList<Achievement> Unfinished => Clone(
        Listed(State).Where(achievement => !achievement.IsFinalLevel));

    /// <summary>Gets the listed achievements that have reached their final level.</summary>
    public IReadOnlyList<Achievement> Finished => Clone(
        Listed(State).Where(achievement => achievement.IsFinalLevel));

    /// <summary>Gets the unfinished listed achievements that are closest to their next level.</summary>
    /// <remarks>
    /// Only achievements that show a progress bar are included. They are ordered by
    /// <see cref="Achievement.Progress"/> descending, then by the fewest points still to earn.
    /// </remarks>
    /// <param name="count">The maximum number of achievements to return.</param>
    /// <returns>Up to <paramref name="count"/> achievements.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public IReadOnlyList<Achievement> Closest(int count = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return Clone(Listed(State)
            .Where(achievement => !achievement.IsFinalLevel && achievement.ShowsProgress)
            .OrderByDescending(achievement => achievement.Progress)
            .ThenBy(achievement => achievement.PointsToNextLevel)
            .Take(count));
    }

    /// <summary>Gets the badge the next level of an achievement grants and the points it requires.</summary>
    /// <remarks>
    /// The point limit comes from <see cref="PointLimits"/> when it has an entry for the next level,
    /// and from the achievement's own <see cref="Achievement.ScoreLimit"/> otherwise.
    /// </remarks>
    /// <param name="code">The achievement code or a badge code of one of its levels.</param>
    /// <returns>
    /// The next badge, or <see langword="null"/> if the achievement is not known, is at its final
    /// level, or has a badge code that does not follow the level pattern.
    /// </returns>
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

    /// <summary>Occurs when the full achievement list is received.</summary>
    /// <remarks>The argument is the list as the server sent it.</remarks>
    public event Action<IReadOnlyList<Achievement>>? ListChanged;
    /// <summary>Occurs when the server updates a single achievement.</summary>
    /// <remarks>The argument is the updated achievement.</remarks>
    public event Action<Achievement>? Updated;
    /// <summary>Occurs when an achievement update raises the achievement's level.</summary>
    /// <remarks>The arguments are the achievement before and after the update. Raised after <see cref="Updated"/>.</remarks>
    public event Action<Achievement, Achievement>? LevelUp;
    /// <summary>Occurs when the achievement score is received.</summary>
    /// <remarks>The argument is the new score.</remarks>
    public event Action<int>? ScoreChanged;
    /// <summary>Occurs when the achievements, score, point limits or new achievement codes change.</summary>
    /// <remarks>Raised after the more specific events. It is not raised when the state is cleared.</remarks>
    public event Action? Changed;
    internal event Action<AchievementStateUpdate>? StateCommitted;
    internal event Action<AchievementStateUpdate>? StateChanged;

    /// <inheritdoc/>
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
        OnIncoming(MessageContracts.Achievements.Score, ApplyScore);
        OnIncoming(MessageContracts.Achievements.PointLimits, ApplyPointLimits);
    }

    /// <summary>Requests the achievement list from the server.</summary>
    /// <remarks>Returns without waiting. The result arrives through <see cref="ListChanged"/>.</remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void Request() => Operations().RequestAchievements();

    /// <summary>Requests the badge point limits from the server.</summary>
    /// <remarks>Returns without waiting. The result is stored in <see cref="PointLimits"/> and raises <see cref="Changed"/>.</remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public void RequestPointLimits() => Operations().RequestPointLimits();

    /// <summary>Requests the achievement list if it has not been loaded and waits for it.</summary>
    /// <remarks>Completes immediately when the list is already loaded. Concurrent callers share one request.</remarks>
    /// <param name="timeoutMs">The time to wait for the list, in milliseconds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with every known achievement.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the list does not arrive within <paramref name="timeoutMs"/>.</exception>
    public Task<IReadOnlyList<Achievement>> EnsureLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureAchievementsLoadedAsync(timeoutMs, cancellationToken);
    }

    /// <summary>Requests the badge point limits if they have not been loaded and waits for them.</summary>
    /// <remarks>Completes immediately when the limits are already loaded. Concurrent callers share one request.</remarks>
    /// <param name="timeoutMs">The time to wait for the limits, in milliseconds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with the badge point limits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    /// <exception cref="RequestTimeoutException">Thrown when the limits do not arrive within <paramref name="timeoutMs"/>.</exception>
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

    /// <inheritdoc/>
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

/// <summary>Represents the badge the next level of an achievement grants.</summary>
/// <param name="Achievement">The achievement at its current level.</param>
/// <param name="BadgeCode">The badge code the next level grants.</param>
/// <param name="Level">The next level.</param>
/// <param name="PointLimit">
/// The point total the badge point limits give for the next level, or the achievement's
/// <see cref="Qx.Model.Messages.Incoming.Achievement.ScoreLimit"/> when no limit was sent for it.
/// </param>
/// <param name="CurrentPoints">The points earned in the current level, counted from where the level started.</param>
/// <param name="PointsToGo">The points still to earn before the next level, never below zero.</param>
public sealed record NextBadge(
    Achievement Achievement,
    string BadgeCode,
    int Level,
    int PointLimit,
    int CurrentPoints,
    int PointsToGo);
