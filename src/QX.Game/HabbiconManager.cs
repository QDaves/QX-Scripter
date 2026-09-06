using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

internal enum HabbiconRequestRoute
{
    Shop,
    Info
}

internal readonly record struct HabbiconRequestKey(HabbiconRequestRoute Route, int TargetId);

internal enum HabbiconStateChangeKind
{
    ShopSnapshot,
    InventorySnapshot,
    Status,
    Info,
    RoomUsed,
    Settings,
    Request,
    Reset
}

internal sealed record HabbiconStateData(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long ShopRevision,
    long UserRevision,
    long StatusRevision,
    long InfoRevision,
    long RoomRevision,
    long SettingsRevision,
    bool ShopLoaded,
    bool UserLoaded,
    bool Enabled,
    IReadOnlyList<HabbiconCollection> Collections,
    IReadOnlyDictionary<int, HabbiconState> UserStates,
    IReadOnlyList<int> RecentHabbiconIds,
    Habbicon? LastInfo,
    RoomUseHabbicon? LastRoomUse);

internal sealed record HabbiconStateUpdate(
    HabbiconStateChangeKind Kind,
    HabbiconStateData State,
    HabbiconRequestKey? Request,
    HabbiconShopData? Shop,
    UserHabbicons? Inventory,
    UserHabbiconStatusChanged? Status,
    Habbicon? Info,
    RoomUseHabbicon? RoomUse,
    IReadOnlyList<int> Gained,
    long RequestEpoch,
    long PublicationEpoch);

internal readonly record struct HabbiconRequestCorrelation(
    HabbiconStateData State,
    long RequestEpoch,
    int OutstandingRequests);

internal interface IHabbiconOperations
{
    void RequestShopData();
    void RequestInfo(int habbicon_id);
    void Buy(int habbicon_id);
    void BuyCollection(int collection_id);
    void Claim(int habbicon_id);
    void Favorite(int habbicon_id);
    void Unfavorite(int habbicon_id);
    Task<IReadOnlyList<HabbiconCollection>> EnsureShopLoadedAsync(
        int timeout_ms,
        CancellationToken cancellation_token);
}

public sealed class HabbiconManager : GameStateManager
{
    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<HabbiconStateUpdate> publications = [];
    private readonly Dictionary<HabbiconRequestKey, long> request_epochs = [];
    private readonly Dictionary<HabbiconRequestKey, int> outstanding_requests = [];
    private readonly Dictionary<HabbiconRequestKey, long> clean_request_epochs = [];
    private HabbiconStateData state = InitialState();
    private IHabbiconOperations? operations;
    private long publication_epoch;
    private long committed_generation;
    private long reset_generation = -1;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    public IReadOnlyList<HabbiconCollection> Collections =>
        State.Collections.Select(CloneCollection).ToArray();

    public IReadOnlyDictionary<int, HabbiconState> OwnedStates =>
        new ReadOnlyDictionary<int, HabbiconState>(new Dictionary<int, HabbiconState>(State.UserStates));

    public IReadOnlyList<int> RecentHabbiconIds => State.RecentHabbiconIds.ToArray();

    public bool IsShopLoaded => State.ShopLoaded;

    public bool IsUserLoaded => State.UserLoaded;

    public bool IsEnabled
    {
        get => State.Enabled;
        internal set => StoreEnabled(value);
    }

    public IReadOnlyList<Habbicon> Icons => IconsFor(State);

    public IReadOnlyList<Habbicon> Owned => Icons.Where(icon => icon.IsOwned).ToArray();

    public IReadOnlyList<Habbicon> Favorites =>
        Icons.Where(icon => icon.State is HabbiconState.Favorite).ToArray();

    public IReadOnlyList<Habbicon> Claimable => Icons.Where(icon => icon.IsClaimable).ToArray();

    public event Action<IReadOnlyList<HabbiconCollection>>? ShopDataChanged;
    public event Action<UserHabbicons>? UserHabbiconsChanged;
    public event Action<UserHabbiconStatusChanged>? StatusChanged;
    public event Action<int>? IconGained;
    public event Action<Habbicon>? InfoReceived;
    public event Action<RoomUseHabbicon>? UsedInRoom;
    internal event Action<HabbiconStateUpdate>? StateCommitted;
    internal event Action<HabbiconStateUpdate>? StateChanged;

    internal HabbiconStateData State => Volatile.Read(ref state);

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.Habbicons.ShopRequest,
            (_, generation) => ObserveRequest(ShopRequestKey(), generation));
        OnOutgoing(
            MessageContracts.Habbicons.InfoRequest,
            (message, generation) => ObserveRequest(InfoRequestKey(message.HabbiconId), generation));
        OnIncoming(
            MessageContracts.Habbicons.ShopSnapshot,
            (message, generation) => StoreShop(message, generation));
        OnIncoming(
            MessageContracts.Habbicons.InventorySnapshot,
            (message, generation) => StoreInventory(message, generation));
        OnIncoming(
            MessageContracts.Habbicons.StatusUpdated,
            (message, generation) => StoreStatus(message, generation));
        OnIncoming(
            MessageContracts.Habbicons.InfoSnapshot,
            (message, generation) => StoreInfo(message.Habbicon, generation));
        OnIncoming(
            MessageContracts.Habbicons.RoomUsed,
            (message, generation) => StoreRoomUse(message, generation));
    }

    public void RequestShopData() => Operations().RequestShopData();

    public void RequestInfo(int habbiconId) => Operations().RequestInfo(habbiconId);

    public void Buy(int habbiconId) => Operations().Buy(habbiconId);

    public void BuyCollection(int collectionId) => Operations().BuyCollection(collectionId);

    public void Claim(int habbiconId) => Operations().Claim(habbiconId);

    public void Favorite(int habbiconId) => Operations().Favorite(habbiconId);

    public void Unfavorite(int habbiconId) => Operations().Unfavorite(habbiconId);

    public Task<IReadOnlyList<HabbiconCollection>> EnsureShopLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureShopLoadedAsync(timeoutMs, cancellationToken);
    }

    internal void BindOperations(IHabbiconOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Habbicon operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IHabbiconOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal HabbiconRequestCorrelation CaptureRequestCorrelation(
        HabbiconRequestKey request,
        Session expected_session,
        long expected_generation)
    {
        lock (state_sync)
        {
            RequireScope(expected_session, expected_generation);
            return new HabbiconRequestCorrelation(
                state,
                request_epochs.GetValueOrDefault(request),
                outstanding_requests.GetValueOrDefault(request));
        }
    }

    internal long AdvanceTypedRequest(
        HabbiconRequestKey request,
        long baseline,
        Session expected_session,
        long expected_generation)
    {
        HabbiconStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireScope(expected_session, expected_generation);
                if (request_epochs.GetValueOrDefault(request) != baseline ||
                    outstanding_requests.GetValueOrDefault(request) != 0)
                {
                    throw new InvalidOperationException(
                        "The habbicon request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(request);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal long? AdvanceTypedShopRequestIfUnloaded(
        long baseline,
        Session expected_session,
        long expected_generation)
    {
        HabbiconStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireScope(expected_session, expected_generation);
                if (state.ShopLoaded)
                    return null;
                HabbiconRequestKey request = ShopRequestKey();
                if (request_epochs.GetValueOrDefault(request) != baseline ||
                    outstanding_requests.GetValueOrDefault(request) != 0)
                {
                    throw new InvalidOperationException(
                        "The habbicon shop request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(request);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal long AdvanceLegacyRequest(
        HabbiconRequestKey request,
        Session expected_session,
        long expected_generation)
    {
        HabbiconStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireScope(expected_session, expected_generation);
                update = BeginRequestUnsafe(request);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal bool IsCurrentPublication(HabbiconStateUpdate update) => UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void ObserveRequest(HabbiconRequestKey request, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (StateCurrent(state, active, generation))
                    update = BeginRequestUnsafe(request);
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private HabbiconStateUpdate BeginRequestUnsafe(HabbiconRequestKey request)
    {
        long next = checked(request_epochs.GetValueOrDefault(request) + 1);
        int previous = outstanding_requests.GetValueOrDefault(request);
        request_epochs[request] = next;
        outstanding_requests[request] = checked(previous + 1);
        clean_request_epochs[request] = previous == 0 ? next : 0;
        return Update(
            HabbiconStateChangeKind.Request,
            state,
            request,
            request_epoch: next);
    }

    private void StoreShop(HabbiconShopData message, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                var stored = new HabbiconShopData(
                    message.Collections.Select(CloneCollection).ToArray());
                long response_epoch = ConsumeResponseUnsafe(ShopRequestKey());
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    ShopRevision = checked(current.ShopRevision + 1),
                    ShopLoaded = true,
                    Collections = stored.Collections
                };
                Volatile.Write(ref state, changed);
                update = Update(
                    HabbiconStateChangeKind.ShopSnapshot,
                    changed,
                    ShopRequestKey(),
                    shop: stored,
                    request_epoch: response_epoch);
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void StoreInventory(UserHabbicons message, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                var states = new Dictionary<int, HabbiconState>();
                var gained = new List<int>();
                foreach (UserHabbiconState entry in message.Habbicons)
                {
                    if (!IsStored(entry.State))
                        continue;
                    states[entry.HabbiconId] = entry.State;
                    if (!current.UserLoaded)
                        continue;
                    if (!current.UserStates.TryGetValue(entry.HabbiconId, out HabbiconState before) ||
                        IsClaimedRewardTransition(before, entry.State))
                    {
                        gained.Add(entry.HabbiconId);
                    }
                }
                var stored = new UserHabbicons(
                    message.Habbicons.Select(entry => entry with { }).ToArray(),
                    message.RecentHabbiconIds.ToArray())
                {
                    RecentHabbiconIdsPresent = message.RecentHabbiconIdsPresent
                };
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    UserRevision = checked(current.UserRevision + 1),
                    UserLoaded = true,
                    UserStates = ReadOnlyStates(states),
                    RecentHabbiconIds = Array.AsReadOnly(stored.RecentHabbiconIds.ToArray())
                };
                Volatile.Write(ref state, changed);
                update = Update(
                    HabbiconStateChangeKind.InventorySnapshot,
                    changed,
                    inventory: stored,
                    gained: gained.ToArray());
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void StoreStatus(UserHabbiconStatusChanged message, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                var states = new Dictionary<int, HabbiconState>(current.UserStates);
                bool gained = false;
                if (IsStored(message.State))
                {
                    gained = !states.TryGetValue(message.HabbiconId, out HabbiconState before) ||
                        IsClaimedRewardTransition(before, message.State);
                    states[message.HabbiconId] = message.State;
                }
                else
                {
                    states.Remove(message.HabbiconId);
                }
                var stored = message with { };
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    StatusRevision = checked(current.StatusRevision + 1),
                    UserStates = ReadOnlyStates(states)
                };
                Volatile.Write(ref state, changed);
                update = Update(
                    HabbiconStateChangeKind.Status,
                    changed,
                    status: stored,
                    gained: gained ? [message.HabbiconId] : []);
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void StoreInfo(Habbicon message, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                Habbicon stored = message with { };
                HabbiconRequestKey request = InfoRequestKey(stored.HabbiconId);
                long response_epoch = ConsumeResponseUnsafe(request);
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    InfoRevision = checked(current.InfoRevision + 1),
                    LastInfo = stored
                };
                Volatile.Write(ref state, changed);
                update = Update(
                    HabbiconStateChangeKind.Info,
                    changed,
                    request,
                    info: stored,
                    request_epoch: response_epoch);
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void StoreRoomUse(RoomUseHabbicon message, long generation)
    {
        Session? active = CurrentSession;
        if (active is null)
            return;
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (!StateCurrent(current, active, generation))
                    return;
                RoomUseHabbicon stored = message with { };
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    RoomRevision = checked(current.RoomRevision + 1),
                    LastRoomUse = stored
                };
                Volatile.Write(ref state, changed);
                update = Update(HabbiconStateChangeKind.RoomUsed, changed, room_use: stored);
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void StoreEnabled(bool enabled)
    {
        HabbiconStateUpdate? update = null;
        Exception? committed_failure = null;
        bool drain = false;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (current.Enabled == enabled)
                    return;
                HabbiconStateData changed = current with
                {
                    Revision = checked(current.Revision + 1),
                    SettingsRevision = checked(current.SettingsRevision + 1),
                    Enabled = enabled
                };
                Volatile.Write(ref state, changed);
                update = Update(HabbiconStateChangeKind.Settings, changed);
                EnqueueUnsafe(update, out drain);
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private long ConsumeResponseUnsafe(HabbiconRequestKey request)
    {
        int previous = outstanding_requests.GetValueOrDefault(request);
        long response_epoch = previous == 1
            ? clean_request_epochs.GetValueOrDefault(request)
            : 0;
        if (previous > 0)
            outstanding_requests[request] = previous - 1;
        if (previous <= 1)
            clean_request_epochs[request] = 0;
        return response_epoch;
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
            HabbiconStateUpdate update;
            lock (state_sync)
            {
                HabbiconStateData current = state;
                if (generation < committed_generation ||
                    generation == reset_generation && ReferenceEquals(current.Session, active))
                {
                    return;
                }
                publication_epoch = checked(publication_epoch + 1);
                request_epochs.Clear();
                outstanding_requests.Clear();
                clean_request_epochs.Clear();
                HabbiconStateData changed = new(
                    active,
                    generation,
                    checked(current.Revision + 1),
                    checked(current.ShopRevision + 1),
                    checked(current.UserRevision + 1),
                    checked(current.StatusRevision + 1),
                    checked(current.InfoRevision + 1),
                    checked(current.RoomRevision + 1),
                    checked(current.SettingsRevision + 1),
                    false,
                    false,
                    false,
                    EmptyCollections(),
                    EmptyStates(),
                    EmptyRecent(),
                    null,
                    null);
                Volatile.Write(ref state, changed);
                committed_generation = generation;
                reset_generation = generation;
                publications.Clear();
                update = Update(HabbiconStateChangeKind.Reset, changed);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private HabbiconStateUpdate Update(
        HabbiconStateChangeKind kind,
        HabbiconStateData current,
        HabbiconRequestKey? request = null,
        HabbiconShopData? shop = null,
        UserHabbicons? inventory = null,
        UserHabbiconStatusChanged? status = null,
        Habbicon? info = null,
        RoomUseHabbicon? room_use = null,
        IReadOnlyList<int>? gained = null,
        long request_epoch = 0) =>
        new(
            kind,
            current,
            request,
            shop,
            inventory,
            status,
            info,
            room_use,
            gained ?? EmptyRecent(),
            request_epoch,
            publication_epoch);

    private void EnqueueUnsafe(HabbiconStateUpdate update, out bool drain)
    {
        publications.Enqueue(update);
        drain = !publishing;
        publishing = true;
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
            HabbiconStateUpdate update;
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
                switch (update.Kind)
                {
                    case HabbiconStateChangeKind.ShopSnapshot when update.Shop is not null:
                        failure = Notify(
                            ShopDataChanged,
                            update.Shop.Collections,
                            update,
                            failure);
                        break;
                    case HabbiconStateChangeKind.InventorySnapshot when update.Inventory is not null:
                        failure = Notify(UserHabbiconsChanged, update.Inventory, update, failure);
                        failure = NotifyGained(update, failure);
                        break;
                    case HabbiconStateChangeKind.Status when update.Status is not null:
                        failure = Notify(StatusChanged, update.Status, update, failure);
                        failure = NotifyGained(update, failure);
                        break;
                    case HabbiconStateChangeKind.Info when update.Info is not null:
                        failure = Notify(InfoReceived, update.Info, update, failure);
                        break;
                    case HabbiconStateChangeKind.RoomUsed when update.RoomUse is not null:
                        failure = Notify(UsedInRoom, update.RoomUse, update, failure);
                        break;
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

    private Exception? NotifyGained(HabbiconStateUpdate update, Exception? failure)
    {
        foreach (int id in update.Gained)
        {
            if (!UpdateCurrent(update))
                break;
            failure = Notify(IconGained, id, update, failure);
        }
        return failure;
    }

    private bool UpdateCurrent(HabbiconStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            HabbiconStateData current = State;
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

    private Exception? NotifyCommitted(HabbiconStateUpdate update) =>
        Notify(StateCommitted, update, update, null, false);

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        HabbiconStateUpdate update,
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

    private void RequireScope(Session expected, long generation)
    {
        HabbiconStateData current = state;
        if (!ReferenceEquals(current.Session, expected) ||
            current.SessionGeneration != generation ||
            committed_generation != generation)
        {
            throw new InvalidOperationException(
                "The habbicon request correlation belongs to a stale hotel session.");
        }
    }

    private bool StateCurrent(HabbiconStateData current, Session active, long generation) =>
        generation == committed_generation &&
        current.SessionGeneration == generation &&
        ReferenceEquals(current.Session, active);

    private IHabbiconOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Habbicon operations are unavailable until the application runtime is active.");

    private static HabbiconRequestKey ShopRequestKey() =>
        new(HabbiconRequestRoute.Shop, 0);

    private static HabbiconRequestKey InfoRequestKey(int habbicon_id) =>
        new(HabbiconRequestRoute.Info, habbicon_id);

    private static bool IsStored(HabbiconState value) =>
        value is HabbiconState.Claimable or HabbiconState.Owned or HabbiconState.Favorite;

    private static bool IsClaimedRewardTransition(HabbiconState before, HabbiconState after) =>
        before is HabbiconState.Claimable &&
        after is HabbiconState.Owned or HabbiconState.Favorite;

    private static IReadOnlyList<Habbicon> IconsFor(HabbiconStateData value) =>
        value.Collections
            .SelectMany(collection => collection.Habbicons)
            .Select(icon => value.UserStates.TryGetValue(icon.HabbiconId, out HabbiconState state)
                ? icon with { State = state }
                : icon with { })
            .ToArray();

    private static HabbiconCollection CloneCollection(HabbiconCollection value) => value with
    {
        Habbicons = value.Habbicons.Select(icon => icon with { }).ToArray()
    };

    private static HabbiconStateData InitialState() => new(
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
        EmptyCollections(),
        EmptyStates(),
        EmptyRecent(),
        null,
        null);

    private static IReadOnlyList<HabbiconCollection> EmptyCollections() =>
        Array.AsReadOnly(Array.Empty<HabbiconCollection>());

    private static IReadOnlyList<int> EmptyRecent() => Array.AsReadOnly(Array.Empty<int>());

    private static IReadOnlyDictionary<int, HabbiconState> EmptyStates() =>
        ReadOnlyStates(new Dictionary<int, HabbiconState>());

    private static IReadOnlyDictionary<int, HabbiconState> ReadOnlyStates(
        IDictionary<int, HabbiconState> values) =>
        new ReadOnlyDictionary<int, HabbiconState>(
            new Dictionary<int, HabbiconState>(values));

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
