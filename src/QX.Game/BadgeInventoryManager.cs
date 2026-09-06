using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

internal enum BadgeInventoryStateChangeKind
{
    Request,
    Fragment,
    Loaded,
    Mutation,
    Selected,
    CorrelationFailed,
    Reset
}

internal enum BadgeMutationKind
{
    Added,
    Updated,
    Removed
}

internal sealed record BadgeSelectedState(
    UserBadges Value,
    long Revision);

internal sealed record BadgeInventoryState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long InventoryRevision,
    long BaselineRevision,
    long SelectedRevision,
    long LoadGeneration,
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    long RecoveryRetiredRequestEpoch,
    long RecoveryActiveRequestEpoch,
    int ExpectedFragments,
    int ReceivedFragments,
    IReadOnlyList<OwnedBadge> OwnedBadges,
    IReadOnlyList<BadgeSelectedState> SelectedBadgeSets);

internal sealed record BadgeFragmentCommit(
    BadgeInventory Fragment,
    long RequestEpoch);

internal sealed record BadgeMutation(
    BadgeMutationKind Kind,
    OwnedBadge Badge);

internal sealed record BadgeMutationCommit(
    IReadOnlyList<BadgeMutation> Mutations);

internal sealed record BadgeInventoryStateUpdate(
    BadgeInventoryStateChangeKind Kind,
    BadgeInventoryState State,
    object? Value,
    long RequestEpoch,
    long PublicationEpoch);

internal sealed record BadgeInventoryDelta(
    Id BadgeId,
    string Code,
    int? OwnerCount,
    int? RarityId,
    bool Removed,
    bool RemoveBeforeUpsert,
    long Order);

public sealed class BadgeInventoryManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<BadgeInventoryStateUpdate> publications = [];
    private readonly Dictionary<int, IReadOnlyList<OwnedBadge>> pending_fragments = [];
    private readonly Dictionary<string, BadgeInventoryDelta> journal =
        new(StringComparer.OrdinalIgnoreCase);
    private BadgeInventoryState state = InitialState();
    private IBadgeInventoryOperations? operations;
    private int expected_fragments = -1;
    private bool restart_on_index_zero;
    private long? retired_request_epoch;
    private long fragment_request_epoch;
    private long request_epoch;
    private long active_request_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private long journal_order;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    public BadgeInventoryManager()
    {
    }

    internal BadgeInventoryManager(TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(time_provider);
    }

    public IReadOnlyCollection<OwnedBadge> OwnedBadges =>
        State.OwnedBadges.ToArray();

    public IReadOnlyCollection<UserBadges> SelectedBadgeSets =>
        State.SelectedBadgeSets.Select(selected => Clone(selected.Value)).ToArray();

    public bool IsLoaded => State.Loaded;
    public bool IsLoading => State.Loading;
    public bool IsStale => State.Stale;
    public long Generation => State.LoadGeneration;
    public int ExpectedFragments => State.ExpectedFragments;
    public int ReceivedFragments => State.ReceivedFragments;

    internal BadgeInventoryState State => Volatile.Read(ref state);

    public event Action? Loaded;
    public event Action<OwnedBadge>? BadgeAdded;
    public event Action<OwnedBadge>? BadgeUpdated;
    public event Action<OwnedBadge>? BadgeRemoved;
    public event Action<UserBadges>? SelectedBadgesUpdated;
    internal event Action<BadgeInventoryStateUpdate>? StateCommitted;
    internal event Action<BadgeInventoryStateUpdate>? StateChanged;

    public TResult Capture<TResult>(Func<BadgeInventoryManager, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        lock (state_sync)
            return projection(this);
    }

    public OwnedBadge? Badge(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        foreach (OwnedBadge badge in State.OwnedBadges)
        {
            if (string.Equals(badge.Code, code, StringComparison.OrdinalIgnoreCase))
                return badge;
        }
        return null;
    }

    public OwnedBadge? Badge(int badge_id)
    {
        foreach (OwnedBadge badge in State.OwnedBadges)
        {
            if ((long)badge.NativeBadgeId is >= int.MinValue and <= int.MaxValue &&
                badge.BadgeId == badge_id)
            {
                return badge;
            }
        }
        return null;
    }

    public OwnedBadge? Badge(Id badge_id)
    {
        foreach (OwnedBadge badge in State.OwnedBadges)
        {
            if (badge.NativeBadgeId == badge_id)
                return badge;
        }
        return null;
    }

    public UserBadges? SelectedBadgeSet(Id user_id)
    {
        BadgeSelectedState? selected = State.SelectedBadgeSets.FirstOrDefault(
            value => value.Value.UserId == user_id);
        return selected is null ? null : Clone(selected.Value);
    }

    public IReadOnlyList<SelectedBadge> SelectedBadgesFor(Id user_id)
    {
        BadgeSelectedState? selected = State.SelectedBadgeSets.FirstOrDefault(
            value => value.Value.UserId == user_id);
        return selected?.Value.Badges.ToArray() ?? [];
    }

    public Task<IReadOnlyCollection<OwnedBadge>> EnsureLoadedAsync(
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_ms, 0);
        cancellation_token.ThrowIfCancellationRequested();
        return Operations().EnsureLoadedAsync(timeout_ms, cancellation_token);
    }

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Badges.Request,
            (_, generation) => ObserveRequest(generation));
        OnIncoming(MessageContracts.Badges.Snapshot, ApplyFragment);
        OnIncoming(MessageContracts.Badges.Received, ApplyReceived);
        OnIncoming(MessageContracts.Badges.Selected, ApplySelected);
        OnIncoming(MessageContracts.Achievements.Notification, ApplyAchievement);
    }

    internal void BindOperations(IBadgeInventoryOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Badge inventory operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IBadgeInventoryOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal long CaptureRequestEpoch(
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            return request_epoch;
        }
    }

    internal long AdvanceRequestEpoch(
        long baseline,
        Session expected_session,
        long expected_session_generation,
        bool retire_response_free)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        bool drain;
        Exception? committed_failure;
        long next;
        lock (publication_sync)
        {
            BadgeInventoryStateUpdate update;
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (request_epoch != baseline)
                {
                    throw new InvalidOperationException(
                        "Another badge inventory request was dispatched before the operation could send.");
                }
                next = checked(baseline + 1);
                BadgeInventoryState current = state;
                BadgeInventoryState updated;
                if (retire_response_free)
                {
                    retired_request_epoch = baseline;
                    pending_fragments.Clear();
                    ClearJournal();
                    expected_fragments = -1;
                    fragment_request_epoch = 0;
                    restart_on_index_zero = true;
                    updated = current with
                    {
                        Revision = checked(current.Revision + 1),
                        InventoryRevision = checked(current.InventoryRevision + 1),
                        LoadGeneration = checked(current.LoadGeneration + 1),
                        Loaded = false,
                        Loading = true,
                        Stale = current.OwnedBadges.Count > 0,
                        RecoveryPending = false,
                        RecoveryRetiredRequestEpoch = 0,
                        RecoveryActiveRequestEpoch = 0,
                        ExpectedFragments = -1,
                        ReceivedFragments = 0
                    };
                }
                else
                {
                    if (expected_fragments >= 0 || current.Loaded)
                        restart_on_index_zero = true;
                    updated = current with
                    {
                        Revision = checked(current.Revision + 1),
                        InventoryRevision = checked(current.InventoryRevision + 1),
                        Loading = true,
                        Stale = !current.Loaded && current.OwnedBadges.Count > 0 || current.Stale
                    };
                }
                update = null!;
                if (!ApplyIfCurrent(expected_session_generation, expected_session, () =>
                    {
                        request_epoch = next;
                        active_request_epoch = next;
                        Volatile.Write(ref state, updated);
                        committed_generation = expected_session_generation;
                        reset_generation = -1;
                        update = new BadgeInventoryStateUpdate(
                            BadgeInventoryStateChangeKind.Request,
                            updated,
                            null,
                            next,
                            publication_epoch);
                    }))
                {
                    throw new InvalidOperationException(
                        "The hotel session changed before the badge inventory request could be dispatched.");
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
        return next;
    }

    internal bool TryAdvanceRequestEpochIfUnloaded(
        long baseline,
        Session expected_session,
        long expected_session_generation,
        bool retire_response_free,
        out long advanced_request_epoch,
        out BadgeInventoryState current_state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        bool drain;
        Exception? committed_failure;
        long next;
        lock (publication_sync)
        {
            BadgeInventoryStateUpdate update;
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (request_epoch != baseline)
                {
                    throw new InvalidOperationException(
                        "Another badge inventory request was dispatched before the operation could send.");
                }
                BadgeInventoryState current = state;
                if (current.Loaded)
                {
                    advanced_request_epoch = baseline;
                    current_state = current;
                    return false;
                }
                next = checked(baseline + 1);
                BadgeInventoryState updated;
                if (retire_response_free)
                {
                    retired_request_epoch = baseline;
                    pending_fragments.Clear();
                    ClearJournal();
                    expected_fragments = -1;
                    fragment_request_epoch = 0;
                    restart_on_index_zero = true;
                    updated = current with
                    {
                        Revision = checked(current.Revision + 1),
                        InventoryRevision = checked(current.InventoryRevision + 1),
                        LoadGeneration = checked(current.LoadGeneration + 1),
                        Loaded = false,
                        Loading = true,
                        Stale = current.OwnedBadges.Count > 0,
                        RecoveryPending = false,
                        RecoveryRetiredRequestEpoch = 0,
                        RecoveryActiveRequestEpoch = 0,
                        ExpectedFragments = -1,
                        ReceivedFragments = 0
                    };
                }
                else
                {
                    if (expected_fragments >= 0 || current.Loaded)
                        restart_on_index_zero = true;
                    updated = current with
                    {
                        Revision = checked(current.Revision + 1),
                        InventoryRevision = checked(current.InventoryRevision + 1),
                        Loading = true,
                        Stale = !current.Loaded && current.OwnedBadges.Count > 0 || current.Stale
                    };
                }
                update = null!;
                if (!ApplyIfCurrent(expected_session_generation, expected_session, () =>
                    {
                        request_epoch = next;
                        active_request_epoch = next;
                        Volatile.Write(ref state, updated);
                        committed_generation = expected_session_generation;
                        reset_generation = -1;
                        update = new BadgeInventoryStateUpdate(
                            BadgeInventoryStateChangeKind.Request,
                            updated,
                            null,
                            next,
                            publication_epoch);
                    }))
                {
                    throw new InvalidOperationException(
                        "The hotel session changed before the badge inventory request could be dispatched.");
                }
                advanced_request_epoch = next;
                current_state = updated;
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
        return true;
    }

    internal bool RequestEpochIsCurrent(
        long expected_epoch,
        Session expected_session,
        long expected_session_generation)
    {
        lock (state_sync)
        {
            BadgeInventoryState current = state;
            if (!ReferenceEquals(current.Session, expected_session) ||
                current.SessionGeneration != expected_session_generation ||
                request_epoch != expected_epoch ||
                active_request_epoch != expected_epoch)
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

    internal bool IsCurrentPublication(BadgeInventoryStateUpdate update) =>
        UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void ObserveRequest(long state_generation)
    {
        Session? session = CurrentSession;
        if (session is null)
            return;
        long baseline;
        lock (state_sync)
        {
            if (state_generation != committed_generation ||
                state.SessionGeneration != state_generation ||
                !ReferenceEquals(state.Session, session))
            {
                return;
            }
            baseline = request_epoch;
        }
        AdvanceRequestEpoch(baseline, session, state_generation, false);
    }

    private void ApplyFragment(BadgeInventory fragment, long state_generation)
    {
        Validate(fragment);
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            BadgeInventoryStateUpdate? update;
            lock (state_sync)
            {
                BadgeInventoryState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                if (!PrepareGeneration(fragment, current, out long load_generation))
                    return;
                ReadOnlyCollection<OwnedBadge> badges = ReadOnly(fragment.Badges);
                pending_fragments[fragment.CurrentPage] = badges;
                int received = pending_fragments.Count;
                BadgeInventoryState updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    InventoryRevision = checked(current.InventoryRevision + 1),
                    LoadGeneration = load_generation,
                    Loaded = false,
                    Loading = true,
                    Stale = current.OwnedBadges.Count > 0,
                    ExpectedFragments = expected_fragments,
                    ReceivedFragments = received
                };
                BadgeInventoryStateChangeKind kind = BadgeInventoryStateChangeKind.Fragment;
                object value = new BadgeFragmentCommit(
                    new BadgeInventory(fragment.TotalPages, fragment.CurrentPage, badges),
                    fragment_request_epoch);
                long update_request_epoch = fragment_request_epoch;
                if (received == expected_fragments && GenerationComplete())
                {
                    if (fragment_request_epoch != 0 &&
                        fragment_request_epoch != active_request_epoch)
                    {
                        long retired = fragment_request_epoch;
                        long active = active_request_epoch;
                        pending_fragments.Clear();
                        expected_fragments = -1;
                        fragment_request_epoch = 0;
                        restart_on_index_zero = true;
                        active_request_epoch = 0;
                        updated = updated with
                        {
                            Revision = checked(updated.Revision + 1),
                            InventoryRevision = checked(updated.InventoryRevision + 1),
                            LoadGeneration = checked(updated.LoadGeneration + 1),
                            Loaded = false,
                            Loading = false,
                            Stale = updated.OwnedBadges.Count > 0,
                            RecoveryPending = true,
                            RecoveryRetiredRequestEpoch = retired,
                            RecoveryActiveRequestEpoch = active,
                            ExpectedFragments = -1,
                            ReceivedFragments = 0
                        };
                        kind = BadgeInventoryStateChangeKind.CorrelationFailed;
                        value = new FragmentedLoadCorrelationException(
                            "badge inventory",
                            retired,
                            active);
                        update_request_epoch = active;
                    }
                    else
                    {
                        ReadOnlyCollection<OwnedBadge> replacement = BuildReplacement();
                        int completed_fragments = expected_fragments;
                        pending_fragments.Clear();
                        ClearJournal();
                        expected_fragments = -1;
                        fragment_request_epoch = 0;
                        restart_on_index_zero = false;
                        retired_request_epoch = null;
                        active_request_epoch = 0;
                        updated = updated with
                        {
                            Revision = checked(updated.Revision + 1),
                            InventoryRevision = checked(updated.InventoryRevision + 1),
                            BaselineRevision = checked(updated.BaselineRevision + 1),
                            Loaded = true,
                            Loading = false,
                            Stale = false,
                            RecoveryPending = false,
                            RecoveryRetiredRequestEpoch = 0,
                            RecoveryActiveRequestEpoch = 0,
                            ExpectedFragments = completed_fragments,
                            ReceivedFragments = completed_fragments,
                            OwnedBadges = replacement
                        };
                        kind = BadgeInventoryStateChangeKind.Loaded;
                        value = replacement;
                    }
                }
                update = null;
                ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new BadgeInventoryStateUpdate(
                            kind,
                            updated,
                            value,
                            update_request_epoch,
                            publication_epoch);
                    });
            }
            if (update is null)
                return;
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private bool PrepareGeneration(
        BadgeInventory fragment,
        BadgeInventoryState current,
        out long load_generation)
    {
        load_generation = current.LoadGeneration;
        if (restart_on_index_zero)
        {
            if (fragment.CurrentPage != 0 &&
                retired_request_epoch is null &&
                active_request_epoch == 0 &&
                !current.RecoveryPending)
            {
                return false;
            }
            BeginGeneration(fragment.TotalPages, TakeRequestEpoch());
            restart_on_index_zero = false;
            load_generation = checked(current.LoadGeneration + 1);
        }
        else if (current.Loaded)
        {
            if (fragment.CurrentPage != 0)
                return false;
            BeginGeneration(fragment.TotalPages, active_request_epoch);
            load_generation = checked(current.LoadGeneration + 1);
        }
        else if (expected_fragments < 0)
        {
            BeginGeneration(fragment.TotalPages, active_request_epoch);
            load_generation = checked(current.LoadGeneration + 1);
        }
        else if (expected_fragments != fragment.TotalPages)
        {
            if (fragment.CurrentPage != 0)
                return false;
            BeginGeneration(fragment.TotalPages, fragment_request_epoch);
            load_generation = checked(current.LoadGeneration + 1);
        }
        else if (fragment.CurrentPage == 0 && pending_fragments.ContainsKey(0))
        {
            BeginGeneration(fragment.TotalPages, fragment_request_epoch);
            load_generation = checked(current.LoadGeneration + 1);
        }
        return true;
    }

    private void BeginGeneration(int total, long request)
    {
        expected_fragments = total;
        pending_fragments.Clear();
        fragment_request_epoch = request;
    }

    private long TakeRequestEpoch()
    {
        if (retired_request_epoch is not { } retired)
            return active_request_epoch;
        retired_request_epoch = null;
        return retired;
    }

    private bool GenerationComplete()
    {
        if (pending_fragments.Count != expected_fragments)
            return false;
        for (int index = 0; index < expected_fragments; index++)
        {
            if (!pending_fragments.ContainsKey(index))
                return false;
        }
        return true;
    }

    private ReadOnlyCollection<OwnedBadge> BuildReplacement()
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var replacement = new List<OwnedBadge>();
        for (int page = 0; page < expected_fragments; page++)
        {
            foreach (OwnedBadge badge in pending_fragments[page])
                Upsert(replacement, positions, badge);
        }
        foreach (BadgeInventoryDelta delta in journal.Values.OrderBy(delta => delta.Order))
        {
            if (delta.Removed)
                Remove(replacement, positions, delta.Code);
            else
            {
                if (delta.RemoveBeforeUpsert)
                    Remove(replacement, positions, delta.Code);
                Upsert(replacement, positions, delta);
            }
        }
        return Array.AsReadOnly(replacement.ToArray());
    }

    private void ApplyReceived(BadgeReceived received, long state_generation)
    {
        Validate(received);
        StoreMutations(
            state_generation,
            current =>
            {
                var values = current.OwnedBadges.ToList();
                var positions = Positions(values);
                bool existed = positions.ContainsKey(received.Code);
                OwnedBadge badge = Upsert(values, positions, received);
                if (JournalActive(current))
                {
                    bool remove_before_upsert = journal.TryGetValue(
                        received.Code,
                        out BadgeInventoryDelta? previous) &&
                        (previous.Removed || previous.RemoveBeforeUpsert);
                    StoreJournalDelta(new BadgeInventoryDelta(
                        received.BadgeId,
                        received.Code,
                        received.OwnerCount,
                        received.RarityId,
                        false,
                        remove_before_upsert,
                        0));
                }
                return (
                    Array.AsReadOnly(values.ToArray()),
                    ReadOnly(new[]
                    {
                        new BadgeMutation(
                            existed ? BadgeMutationKind.Updated : BadgeMutationKind.Added,
                            badge)
                    }));
            });
    }

    private void ApplyAchievement(
        AchievementNotification notification,
        long state_generation)
    {
        StoreMutations(
            state_generation,
            current =>
            {
                bool has_rarity_data = current.Session is { } session &&
                    ClientTypes.IsFlash(session.Client);
                var values = current.OwnedBadges.ToList();
                var positions = Positions(values);
                var mutations = new List<BadgeMutation>(2);
                if (!string.IsNullOrWhiteSpace(notification.BadgeCode))
                {
                    bool existed = positions.ContainsKey(notification.BadgeCode);
                    var delta = new BadgeInventoryDelta(
                        notification.BadgeId,
                        notification.BadgeCode,
                        has_rarity_data ? notification.OwnerCount : null,
                        has_rarity_data ? notification.BadgeRarityId : null,
                        false,
                        journal.TryGetValue(
                            notification.BadgeCode,
                            out BadgeInventoryDelta? previous) &&
                            (previous.Removed || previous.RemoveBeforeUpsert),
                        0);
                    OwnedBadge badge = Upsert(values, positions, delta);
                    mutations.Add(new BadgeMutation(
                        existed ? BadgeMutationKind.Updated : BadgeMutationKind.Added,
                        badge));
                    if (JournalActive(current))
                        StoreJournalDelta(delta);
                }
                if (!string.IsNullOrWhiteSpace(notification.RemovedBadgeCode) &&
                    !string.Equals(
                        notification.RemovedBadgeCode,
                        notification.BadgeCode,
                        StringComparison.OrdinalIgnoreCase))
                {
                    OwnedBadge? removed = Remove(
                        values,
                        positions,
                        notification.RemovedBadgeCode);
                    if (removed is { } badge)
                        mutations.Add(new BadgeMutation(BadgeMutationKind.Removed, badge));
                    if (JournalActive(current))
                    {
                        StoreJournalDelta(new BadgeInventoryDelta(
                            default,
                            notification.RemovedBadgeCode,
                            null,
                            null,
                            true,
                            false,
                            0));
                    }
                }
                return (
                    Array.AsReadOnly(values.ToArray()),
                    Array.AsReadOnly(mutations.ToArray()));
            });
    }

    private void StoreMutations(
        long state_generation,
        Func<BadgeInventoryState, (
            IReadOnlyList<OwnedBadge> Badges,
            IReadOnlyList<BadgeMutation> Mutations)> mutation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            BadgeInventoryStateUpdate? update = null;
            lock (state_sync)
            {
                BadgeInventoryState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                (IReadOnlyList<OwnedBadge> badges, IReadOnlyList<BadgeMutation> mutations) =
                    mutation(current);
                if (mutations.Count == 0)
                    return;
                BadgeInventoryState updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    InventoryRevision = checked(current.InventoryRevision + 1),
                    OwnedBadges = badges,
                    Stale = current.Stale || !current.Loaded && badges.Count > 0
                };
                if (ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new BadgeInventoryStateUpdate(
                            BadgeInventoryStateChangeKind.Mutation,
                            updated,
                            new BadgeMutationCommit(mutations),
                            active_request_epoch,
                            publication_epoch);
                    }))
                {
                }
            }
            if (update is null)
                return;
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void ApplySelected(UserBadges selected, long state_generation)
    {
        UserBadges value = Clone(selected);
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            BadgeInventoryStateUpdate? update = null;
            lock (state_sync)
            {
                BadgeInventoryState current = state;
                if (state_generation != committed_generation ||
                    current.SessionGeneration != state_generation ||
                    !ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                long selected_revision = checked(current.SelectedRevision + 1);
                var sets = current.SelectedBadgeSets.ToList();
                int index = sets.FindIndex(entry => entry.Value.UserId == value.UserId);
                var entry = new BadgeSelectedState(value, selected_revision);
                if (index < 0)
                    sets.Add(entry);
                else
                    sets[index] = entry;
                BadgeInventoryState updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    SelectedRevision = selected_revision,
                    SelectedBadgeSets = Array.AsReadOnly(sets.ToArray())
                };
                if (ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new BadgeInventoryStateUpdate(
                            BadgeInventoryStateChangeKind.Selected,
                            updated,
                            entry,
                            0,
                            publication_epoch);
                    }))
                {
                }
            }
            if (update is null)
                return;
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
            BadgeInventoryStateUpdate update;
            lock (state_sync)
            {
                BadgeInventoryState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new BadgeInventoryState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.InventoryRevision + 1),
                    current.BaselineRevision,
                    checked(current.SelectedRevision + 1),
                    checked(current.LoadGeneration + 1),
                    false,
                    false,
                    false,
                    false,
                    0,
                    0,
                    -1,
                    0,
                    ReadOnly(Array.Empty<OwnedBadge>()),
                    ReadOnly(Array.Empty<BadgeSelectedState>()));
                Volatile.Write(ref state, updated);
                pending_fragments.Clear();
                ClearJournal();
                expected_fragments = -1;
                restart_on_index_zero = false;
                retired_request_epoch = null;
                fragment_request_epoch = 0;
                request_epoch = 0;
                active_request_epoch = 0;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new BadgeInventoryStateUpdate(
                    BadgeInventoryStateChangeKind.Reset,
                    updated,
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
            BadgeInventoryStateUpdate update;
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

    private bool UpdateCurrent(BadgeInventoryStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            BadgeInventoryState current = State;
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

    private Exception? NotifyCommitted(BadgeInventoryStateUpdate update) =>
        Notify(StateCommitted, update, update, null);

    private Exception? NotifyLegacy(
        BadgeInventoryStateUpdate update,
        Exception? failure)
    {
        switch (update.Kind)
        {
            case BadgeInventoryStateChangeKind.Loaded:
                return Notify(Loaded, update, failure);
            case BadgeInventoryStateChangeKind.Mutation:
                foreach (BadgeMutation mutation in ((BadgeMutationCommit)update.Value!).Mutations)
                {
                    failure = mutation.Kind switch
                    {
                        BadgeMutationKind.Added => Notify(
                            BadgeAdded,
                            mutation.Badge,
                            update,
                            failure),
                        BadgeMutationKind.Updated => Notify(
                            BadgeUpdated,
                            mutation.Badge,
                            update,
                            failure),
                        BadgeMutationKind.Removed => Notify(
                            BadgeRemoved,
                            mutation.Badge,
                            update,
                            failure),
                        _ => throw new ArgumentOutOfRangeException(nameof(mutation))
                    };
                }
                return failure;
            case BadgeInventoryStateChangeKind.Selected:
                return Notify(
                    SelectedBadgesUpdated,
                    () => Clone(((BadgeSelectedState)update.Value!).Value),
                    update,
                    failure);
            case BadgeInventoryStateChangeKind.Request:
            case BadgeInventoryStateChangeKind.Fragment:
            case BadgeInventoryStateChangeKind.CorrelationFailed:
            case BadgeInventoryStateChangeKind.Reset:
                return failure;
            default:
                throw new ArgumentOutOfRangeException(nameof(update));
        }
    }

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        BadgeInventoryStateUpdate update,
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
        BadgeInventoryStateUpdate update,
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
        Action? listeners,
        BadgeInventoryStateUpdate update,
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
        BadgeInventoryState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The badge inventory request epoch cannot be {operation} for a stale hotel session.");
        }
        if (current.RecoveryPending)
        {
            throw new FragmentedLoadCorrelationException(
                "badge inventory",
                current.RecoveryRetiredRequestEpoch,
                current.RecoveryActiveRequestEpoch);
        }
    }

    private bool JournalActive(BadgeInventoryState current) =>
        !current.Loaded ||
        current.Stale ||
        active_request_epoch != 0 ||
        current.Loading ||
        current.RecoveryPending;

    private void StoreJournalDelta(BadgeInventoryDelta delta)
    {
        bool known = journal.TryGetValue(delta.Code, out BadgeInventoryDelta? previous);
        long order;
        if (!known || previous is { Removed: true } && !delta.Removed)
        {
            journal_order = checked(journal_order + 1);
            order = journal_order;
        }
        else
            order = previous!.Order;
        journal[delta.Code] = delta with { Order = order };
    }

    private void ClearJournal()
    {
        journal.Clear();
        journal_order = 0;
    }

    private static Dictionary<string, int> Positions(IReadOnlyList<OwnedBadge> badges)
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < badges.Count; index++)
            positions[badges[index].Code] = index;
        return positions;
    }

    private static OwnedBadge Upsert(
        IList<OwnedBadge> badges,
        IDictionary<string, int> positions,
        BadgeReceived received) => Upsert(
        badges,
        positions,
        new BadgeInventoryDelta(
            received.BadgeId,
            received.Code,
            received.OwnerCount,
            received.RarityId,
            false,
            false,
            0));

    private static OwnedBadge Upsert(
        IList<OwnedBadge> badges,
        IDictionary<string, int> positions,
        BadgeInventoryDelta delta)
    {
        positions.TryGetValue(delta.Code, out int index);
        bool existed = positions.ContainsKey(delta.Code);
        OwnedBadge previous = existed ? badges[index] : default;
        var badge = new OwnedBadge(
            delta.BadgeId,
            delta.Code,
            delta.OwnerCount ?? previous.OwnerCount,
            delta.RarityId ?? previous.RarityId,
            delta.OwnerCount.HasValue || previous.HasRarityData);
        if (existed)
            badges[index] = badge;
        else
        {
            positions[delta.Code] = badges.Count;
            badges.Add(badge);
        }
        return badge;
    }

    private static void Upsert(
        IList<OwnedBadge> badges,
        IDictionary<string, int> positions,
        OwnedBadge badge)
    {
        if (positions.TryGetValue(badge.Code, out int index))
            badges[index] = badge;
        else
        {
            positions[badge.Code] = badges.Count;
            badges.Add(badge);
        }
    }

    private static OwnedBadge? Remove(
        IList<OwnedBadge> badges,
        IDictionary<string, int> positions,
        string code)
    {
        if (!positions.TryGetValue(code, out int index))
            return null;
        OwnedBadge removed = badges[index];
        badges.RemoveAt(index);
        positions.Clear();
        for (int position = 0; position < badges.Count; position++)
            positions[badges[position].Code] = position;
        return removed;
    }

    private static void Validate(BadgeInventory fragment)
    {
        if (fragment.TotalPages <= 0)
        {
            throw new InvalidDataException(
                $"Badge inventory fragment count must be positive, received {fragment.TotalPages}.");
        }
        if (fragment.CurrentPage < 0 || fragment.CurrentPage >= fragment.TotalPages)
        {
            throw new InvalidDataException(
                $"Badge inventory fragment index {fragment.CurrentPage} is outside 0..{fragment.TotalPages - 1}.");
        }
        if (fragment.Badges.Any(badge => string.IsNullOrWhiteSpace(badge.Code)))
            throw new InvalidDataException("Badge inventory entries must contain a code.");
    }

    private static void Validate(BadgeReceived received)
    {
        if (string.IsNullOrWhiteSpace(received.Code))
            throw new InvalidDataException("Received badge must contain a code.");
        if (received.OwnerCount.HasValue != received.RarityId.HasValue)
            throw new InvalidDataException("Received badge rarity data is incomplete.");
    }

    private IBadgeInventoryOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Badge inventory operations are unavailable until the application runtime is active.");

    private static BadgeInventoryState InitialState() => new(
        null,
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
        0,
        0,
        -1,
        0,
        ReadOnly(Array.Empty<OwnedBadge>()),
        ReadOnly(Array.Empty<BadgeSelectedState>()));

    private static UserBadges Clone(UserBadges value) =>
        new(value.UserId, Array.AsReadOnly(value.Badges.ToArray()));

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
