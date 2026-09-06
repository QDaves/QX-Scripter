using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

internal enum EarningRequestRoute
{
    Status,
    Claim
}

internal enum EarningStateChangeKind
{
    Snapshot,
    Claimed,
    Notification,
    Request,
    Reset
}

internal sealed record EarningState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long StatusRevision,
    long BaselineRevision,
    long ClaimRevision,
    long NotificationRevision,
    bool Loaded,
    EarningStatus Status);

internal sealed record EarningClaimCommit(
    EarningClaimResult Result,
    bool StatusChanged);

internal sealed record EarningStateUpdate(
    EarningStateChangeKind Kind,
    EarningState State,
    object? Value,
    EarningRequestRoute? Route,
    int? Category,
    long RequestEpoch,
    int OutstandingRequests,
    long PublicationEpoch);

internal readonly record struct EarningStatusCorrelation(
    EarningState State,
    long RequestEpoch,
    int OutstandingRequests);

internal readonly record struct EarningClaimCorrelation(
    EarningState State,
    long RequestEpoch,
    int OutstandingRequests);

/// <summary>
/// Mirrors the earnings vault: what each source has paid out and is waiting to be claimed.
/// </summary>
/// <remarks>
/// <para>
/// The hotel sends the whole vault at once and never amends it in place. A claim is answered with a
/// result rather than a fresh list, and the client zeroes what it just claimed on its own, which is
/// what happens here: a successful claim for one category drops that category's lines, a successful
/// claim-all drops everything, and a refused claim changes nothing.
/// </para>
/// <para>
/// The status and claim result have separate Flash and Unity codecs. The reward notification is
/// Flash only because the Unity catalogue has no corresponding message. Unity sends are still
/// subject to the verified outgoing-schema gate.
/// </para>
/// </remarks>
public sealed class EarningsManager : GameStateManager
{
    private sealed class ClaimRequestTracker
    {
        public long Epoch { get; set; }
        public int Outstanding { get; set; }
        public long CleanEpoch { get; set; }
    }

    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<EarningStateUpdate> publications = [];
    private readonly Dictionary<int, ClaimRequestTracker> claim_requests = [];
    private readonly HashSet<int> claim_journal = [];
    private EarningState state = InitialState();
    private IEarningOperations? operations;
    private long status_request_epoch;
    private int status_outstanding;
    private long clean_status_epoch;
    private long clean_status_journal_revision;
    private long claim_journal_revision;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private int refresh_on_notification = 1;
    private bool claim_all_journaled;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    /// <summary>Everything the vault is holding, as the hotel last reported it.</summary>
    public EarningStatus Status => State.Status;

    /// <summary>Whether the hotel has sent the vault this session.</summary>
    public bool IsLoaded => State.Loaded;

    /// <summary>
    /// Whether a notification is answered by asking the hotel for the vault again.
    /// </summary>
    /// <remarks>
    /// The client refreshes only while its earnings window is open. The equivalent here is having
    /// asked once: nothing is sent until something has read the vault, after which it is kept
    /// current. Turn this off to stop the manager sending anything on its own.
    /// </remarks>
    public bool RefreshOnNotification
    {
        get => Volatile.Read(ref refresh_on_notification) != 0;
        set => Volatile.Write(ref refresh_on_notification, value ? 1 : 0);
    }

    /// <summary>Raised when the vault arrived or was changed by a claim.</summary>
    public event Action<EarningStatus>? StatusChanged;

    /// <summary>Raised when the hotel answered a claim, whether it went through or not.</summary>
    public event Action<EarningClaimResult>? Claimed;

    /// <summary>Raised when the hotel says a category gained something.</summary>
    public event Action<EarningCategory>? RewardAvailable;
    internal event Action<EarningStateUpdate>? StateCommitted;
    internal event Action<EarningStateUpdate>? StateChanged;

    internal EarningState State => Volatile.Read(ref state);

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Earnings.StatusRequest,
            (_, generation) => ObserveStatusRequest(generation));
        OnOutgoing(
            MessageContracts.Earnings.Claim,
            (message, generation) => ObserveClaimRequest(message, generation));
        OnIncoming(MessageContracts.Earnings.StatusSnapshot, ApplyStatus);
        OnIncoming(MessageContracts.Earnings.Claimed, ApplyClaim);
        OnIncoming(MessageContracts.Earnings.Notification, ApplyNotification);
    }

    /// <summary>Asks the hotel for the vault.</summary>
    public void Request() => Operations().RequestStatus();

    /// <summary>
    /// Claims one category.
    /// </summary>
    /// <remarks>
    /// The vault is not changed here. The hotel answers with a result, and the held copy follows
    /// that answer, so a refused claim leaves the figures standing.
    /// </remarks>
    /// <param name="category">
    /// The category to claim. <see cref="EarningCategory.All"/> claims every category, which is the
    /// same request the client's claim-all button sends.
    /// </param>
    public void Claim(EarningCategory category) => Operations().Claim(category);

    /// <summary>Claims every category in one request.</summary>
    public void ClaimAll() => Claim(EarningCategory.All);

    /// <summary>
    /// Whether the connected client supports the earnings vault.
    /// </summary>
    /// <remarks>
    /// Both do. Unity was held out while its layout was unproven; the native IR catalogue since gave
    /// the reads for the vault and for the answer to a claim, and both are what QX already parses —
    /// the vault once its count was read as an array's two bytes rather than as four.
    /// </remarks>
    public bool IsSupported => true;

    /// <summary>
    /// Returns the vault, asking the hotel for it when it has not been seen.
    /// </summary>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="TimeoutException">The hotel did not answer in time.</exception>
    public Task<EarningStatus> EnsureLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureLoadedAsync(timeoutMs, cancellationToken);
    }

    internal void BindOperations(IEarningOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Earning operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IEarningOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal EarningStatusCorrelation CaptureStatusCorrelation(
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            return new EarningStatusCorrelation(
                state,
                status_request_epoch,
                status_outstanding);
        }
    }

    internal EarningClaimCorrelation CaptureClaimCorrelation(
        int category,
        Session expected_session,
        long expected_session_generation)
    {
        RequireCategory(category);
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            ClaimRequestTracker tracker = ClaimTracker(category);
            return new EarningClaimCorrelation(state, tracker.Epoch, tracker.Outstanding);
        }
    }

    internal long AdvanceLegacyStatusRequest(
        Session expected_session,
        long expected_session_generation) => AdvanceStatusRequest(
            null,
            expected_session,
            expected_session_generation,
            out _);

    internal long AdvanceTypedStatusRequest(
        long baseline,
        Session expected_session,
        long expected_session_generation) => AdvanceStatusRequest(
            baseline,
            expected_session,
            expected_session_generation,
            out _);

    internal bool TryAdvanceTypedStatusRequestIfUnloaded(
        long baseline,
        Session expected_session,
        long expected_session_generation,
        out long request_epoch,
        out EarningState current_state)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        EarningStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                current_state = state;
                if (current_state.Loaded)
                {
                    request_epoch = status_request_epoch;
                    return false;
                }
                if (status_request_epoch != baseline || status_outstanding != 0)
                {
                    throw new InvalidOperationException(
                        "The earnings status request is no longer safe to dispatch.");
                }
                update = BeginStatusRequestUnsafe(
                    expected_session,
                    expected_session_generation);
                request_epoch = update.RequestEpoch;
                current_state = state;
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return true;
    }

    internal bool TryAdvanceNotificationStatusRequest(
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        EarningStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (!RequestScopeCurrent(expected_session, expected_session_generation) ||
                    !state.Loaded ||
                    !RefreshOnNotification)
                {
                    return false;
                }
                update = BeginStatusRequestUnsafe(
                    expected_session,
                    expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return true;
    }

    internal long AdvanceLegacyClaimRequest(
        int category,
        Session expected_session,
        long expected_session_generation) => AdvanceClaimRequest(
            category,
            null,
            expected_session,
            expected_session_generation);

    internal long AdvanceTypedClaimRequest(
        int category,
        long baseline,
        Session expected_session,
        long expected_session_generation) => AdvanceClaimRequest(
            category,
            baseline,
            expected_session,
            expected_session_generation);

    internal bool IsCurrentPublication(EarningStateUpdate update) => UpdateCurrent(update);

    internal static int NormalizeCategory(EarningCategory category) =>
        unchecked((sbyte)(int)category);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private long AdvanceStatusRequest(
        long? baseline,
        Session expected_session,
        long expected_session_generation,
        out EarningState current_state)
    {
        if (baseline is < 0)
            throw new ArgumentOutOfRangeException(nameof(baseline));
        ArgumentNullException.ThrowIfNull(expected_session);
        EarningStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                current_state = state;
                if (baseline is long expected &&
                    (status_request_epoch != expected || status_outstanding != 0))
                {
                    throw new InvalidOperationException(
                        "The earnings status request is no longer safe to dispatch.");
                }
                update = BeginStatusRequestUnsafe(
                    expected_session,
                    expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    private long AdvanceClaimRequest(
        int category,
        long? baseline,
        Session expected_session,
        long expected_session_generation)
    {
        RequireCategory(category);
        if (baseline is < 0)
            throw new ArgumentOutOfRangeException(nameof(baseline));
        ArgumentNullException.ThrowIfNull(expected_session);
        EarningStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                ClaimRequestTracker tracker = ClaimTracker(category);
                if (baseline is long expected &&
                    (tracker.Epoch != expected || tracker.Outstanding != 0))
                {
                    throw new InvalidOperationException(
                        "The earnings claim request is no longer safe to dispatch.");
                }
                update = BeginClaimRequestUnsafe(
                    category,
                    tracker,
                    expected_session,
                    expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    private void ObserveStatusRequest(long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        EarningStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (RequestScopeCurrent(active_session, state_generation))
                {
                    update = BeginStatusRequestUnsafe(active_session, state_generation);
                }
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private void ObserveClaimRequest(EarningClaimRequest message, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        int category = NormalizeCategory(message.Category);
        EarningStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (RequestScopeCurrent(active_session, state_generation))
                {
                    update = BeginClaimRequestUnsafe(
                        category,
                        ClaimTracker(category),
                        active_session,
                        state_generation);
                }
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private EarningStateUpdate BeginStatusRequestUnsafe(
        Session expected_session,
        long expected_session_generation)
    {
        long next = checked(status_request_epoch + 1);
        int previous = status_outstanding;
        int outstanding = checked(previous + 1);
        if (!ApplyIfCurrent(
                expected_session_generation,
                expected_session,
                () =>
                {
                    if (previous == 0)
                    {
                        clean_status_epoch = next;
                        clean_status_journal_revision = claim_journal_revision;
                    }
                    else
                    {
                        clean_status_epoch = 0;
                    }
                    status_request_epoch = next;
                    status_outstanding = outstanding;
                }))
        {
            throw new InvalidOperationException(
                "The hotel session changed before the earnings status request could be dispatched.");
        }
        return new EarningStateUpdate(
            EarningStateChangeKind.Request,
            state,
            null,
            EarningRequestRoute.Status,
            null,
            next,
            outstanding,
            publication_epoch);
    }

    private EarningStateUpdate BeginClaimRequestUnsafe(
        int category,
        ClaimRequestTracker tracker,
        Session expected_session,
        long expected_session_generation)
    {
        long next = checked(tracker.Epoch + 1);
        int previous = tracker.Outstanding;
        int outstanding = checked(previous + 1);
        long clean_epoch = previous == 0 ? next : 0;
        if (!ApplyIfCurrent(
                expected_session_generation,
                expected_session,
                () =>
                {
                    tracker.Epoch = next;
                    tracker.Outstanding = outstanding;
                    tracker.CleanEpoch = clean_epoch;
                }))
        {
            throw new InvalidOperationException(
                "The hotel session changed before the earnings claim request could be dispatched.");
        }
        return new EarningStateUpdate(
            EarningStateChangeKind.Request,
            state,
            null,
            EarningRequestRoute.Claim,
            category,
            next,
            outstanding,
            publication_epoch);
    }

    private void ApplyStatus(EarningStatus message, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            EarningStateUpdate update;
            lock (state_sync)
            {
                EarningState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return;
                int previous = status_outstanding;
                int outstanding = previous == 0 ? 0 : previous - 1;
                long response_epoch = previous == 1 ? clean_status_epoch : 0;
                bool clean = response_epoch > 0 &&
                    clean_status_journal_revision == claim_journal_revision;
                EarningStatus received = clean ? message : ApplyJournal(message);
                var updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    StatusRevision = checked(current.StatusRevision + 1),
                    BaselineRevision = checked(current.BaselineRevision + 1),
                    Loaded = true,
                    Status = received
                };
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        status_outstanding = outstanding;
                        if (clean)
                            ClearJournalUnsafe();
                        if (previous <= 1)
                            clean_status_epoch = 0;
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new EarningStateUpdate(
                            EarningStateChangeKind.Snapshot,
                            updated,
                            received,
                            EarningRequestRoute.Status,
                            null,
                            response_epoch,
                            outstanding,
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

    private void ApplyClaim(EarningClaimResult message, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        int category = NormalizeCategory(message.Category);
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            EarningStateUpdate update;
            lock (state_sync)
            {
                EarningState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return;
                ClaimRequestTracker tracker = ClaimTracker(category);
                int previous = tracker.Outstanding;
                int outstanding = previous == 0 ? 0 : previous - 1;
                long response_epoch = previous == 1 ? tracker.CleanEpoch : 0;
                bool status_changed = message.Success && current.Loaded;
                EarningStatus remaining = status_changed
                    ? Remaining(current.Status, category)
                    : current.Status;
                var updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    StatusRevision = status_changed
                        ? checked(current.StatusRevision + 1)
                        : current.StatusRevision,
                    ClaimRevision = checked(current.ClaimRevision + 1),
                    Status = remaining
                };
                var commit = new EarningClaimCommit(message, status_changed);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        tracker.Outstanding = outstanding;
                        if (previous <= 1)
                            tracker.CleanEpoch = 0;
                        if (message.Success)
                            JournalClaimUnsafe(category);
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new EarningStateUpdate(
                            EarningStateChangeKind.Claimed,
                            updated,
                            commit,
                            EarningRequestRoute.Claim,
                            category,
                            response_epoch,
                            outstanding,
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

    private void ApplyNotification(EarningNotification message, long state_generation)
    {
        (EarningStateUpdate? update, Exception? publication_failure) = StoreNotification(
            message,
            state_generation);
        Exception? request_failure = null;
        if (update?.State is { Loaded: true, Session: not null } && RefreshOnNotification)
        {
            try
            {
                RequestAfterNotification(update);
            }
            catch (Exception error)
            {
                request_failure = error;
            }
        }
        ThrowFailures(publication_failure, request_failure);
    }

    private (EarningStateUpdate? Update, Exception? Failure) StoreNotification(
        EarningNotification message,
        long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return (null, null);
        EarningStateUpdate update = null!;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                EarningState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return (null, null);
                var updated = current with
                {
                    Revision = checked(current.Revision + 1),
                    NotificationRevision = checked(current.NotificationRevision + 1)
                };
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new EarningStateUpdate(
                            EarningStateChangeKind.Notification,
                            updated,
                            message,
                            null,
                            NormalizeCategory(message.Category),
                            0,
                            0,
                            publication_epoch);
                    }))
                {
                    return (null, null);
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        Exception? failure = committed_failure is null
            ? publication_failure
            : publication_failure is null
                ? committed_failure
                : new AggregateException(committed_failure, publication_failure);
        return (update, failure);
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
            EarningStateUpdate update;
            lock (state_sync)
            {
                EarningState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new EarningState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.StatusRevision + 1),
                    current.BaselineRevision,
                    checked(current.ClaimRevision + 1),
                    checked(current.NotificationRevision + 1),
                    false,
                    EmptyStatus());
                Volatile.Write(ref state, updated);
                status_request_epoch = 0;
                status_outstanding = 0;
                clean_status_epoch = 0;
                clean_status_journal_revision = 0;
                claim_requests.Clear();
                claim_journal.Clear();
                claim_all_journaled = false;
                claim_journal_revision = 0;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new EarningStateUpdate(
                    EarningStateChangeKind.Reset,
                    updated,
                    null,
                    null,
                    null,
                    0,
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
            EarningStateUpdate update;
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

    private Exception? NotifyLegacy(EarningStateUpdate update, Exception? failure)
    {
        switch (update.Kind)
        {
            case EarningStateChangeKind.Snapshot:
                return Notify(
                    StatusChanged,
                    (EarningStatus)update.Value!,
                    update,
                    failure);
            case EarningStateChangeKind.Claimed:
            {
                var commit = (EarningClaimCommit)update.Value!;
                failure = Notify(Claimed, commit.Result, update, failure);
                return commit.StatusChanged
                    ? Notify(StatusChanged, update.State.Status, update, failure)
                    : failure;
            }
            case EarningStateChangeKind.Notification:
                return Notify(
                    RewardAvailable,
                    ((EarningNotification)update.Value!).Category,
                    update,
                    failure);
            case EarningStateChangeKind.Request:
            case EarningStateChangeKind.Reset:
                return failure;
            default:
                throw new ArgumentOutOfRangeException(nameof(update));
        }
    }

    private bool UpdateCurrent(EarningStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            EarningState current = State;
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

    private Exception? NotifyCommitted(EarningStateUpdate update) =>
        Notify(StateCommitted, update, update, null, false);

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        EarningStateUpdate update,
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

    private bool StateCurrent(
        EarningState current,
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
        EarningState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The earnings request correlation cannot be {operation} for a stale hotel session.");
        }
    }

    private ClaimRequestTracker ClaimTracker(int category)
    {
        if (!claim_requests.TryGetValue(category, out ClaimRequestTracker? tracker))
        {
            tracker = new ClaimRequestTracker();
            claim_requests.Add(category, tracker);
        }
        return tracker;
    }

    private void JournalClaimUnsafe(int category)
    {
        claim_journal_revision = checked(claim_journal_revision + 1);
        if (category == (int)EarningCategory.All)
        {
            claim_all_journaled = true;
            claim_journal.Clear();
        }
        else if (!claim_all_journaled)
        {
            claim_journal.Add(category);
        }
    }

    private EarningStatus ApplyJournal(EarningStatus received)
    {
        if (claim_all_journaled)
            return EmptyStatus();
        if (claim_journal.Count == 0)
            return received;
        return new EarningStatus(
            received.Entries
                .Where(entry => !claim_journal.Contains(NormalizeCategory(entry.Category)))
                .ToArray());
    }

    private void ClearJournalUnsafe()
    {
        claim_journal.Clear();
        claim_all_journaled = false;
    }

    private static EarningStatus Remaining(EarningStatus current, int category) =>
        category == (int)EarningCategory.All
            ? EmptyStatus()
            : new EarningStatus(
                current.Entries
                    .Where(entry => NormalizeCategory(entry.Category) != category)
                    .ToArray());

    private IEarningOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Earning operations are unavailable until the application runtime is active.");

    private void RequestAfterNotification(EarningStateUpdate update)
    {
        IEarningOperations? current = Volatile.Read(ref operations);
        if (current is null || update.State.Session is not { } expected_session)
            return;
        try
        {
            current.RequestStatusAfterNotification(
                expected_session,
                update.State.SessionGeneration);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static EarningState InitialState() => new(
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        EmptyStatus());

    private static EarningStatus EmptyStatus() => new(Array.Empty<EarningEntry>());

    private static void RequireCategory(int category)
    {
        if (category is < sbyte.MinValue or > sbyte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(category));
    }
}
