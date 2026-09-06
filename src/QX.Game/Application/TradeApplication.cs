using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal sealed class TradeApplication : IApplicationFeature
{
    private const int commit_history_limit = 32;
    private const int snapshot_lease_limit = 16;

    private readonly IConnection connection;
    private readonly RoomManager room;
    private readonly ProfileManager profile;
    private readonly TradeManager trade;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<TradeChanged> changed;
    private readonly object updates_sync = new();
    private readonly List<TradeStateUpdate> inventory_updates = [];
    private readonly object leases_sync = new();
    private readonly Dictionary<long, NftInventoryLease> leases = [];
    private readonly Queue<long> lease_order = [];
    private long lease_revision;
    private int disposed;

    public TradeApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        room = game.Room;
        profile = game.Profile;
        trade = game.Trade;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<TradeChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<TradeStateRequest, TradeStateView>(
                TradeApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<TradeOpenRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.Open,
                Open),
            new ApplicationCallBinding<TradeItemsAddRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.ItemsAdd,
                AddItems),
            new ApplicationCallBinding<TradeItemRemoveRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.ItemRemove,
                RemoveItem),
            new ApplicationCallBinding<TradeCommandRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.Accept,
                Accept),
            new ApplicationCallBinding<TradeCommandRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.Unaccept,
                Unaccept),
            new ApplicationCallBinding<TradeCommandRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.Confirm,
                Confirm),
            new ApplicationCallBinding<TradeCommandRequest, TradeDispatchResult>(
                TradeApplicationDescriptors.Close,
                Close),
            new ApplicationCallBinding<TradeNftInventoryPageRequest, TradeNftInventoryPage>(
                TradeApplicationDescriptors.NftInventoryList,
                (request, _) => ValueTask.FromResult(NftInventory(request))),
            new ApplicationCallBinding<TradeNftInventoryRefreshRequest, TradeNftInventoryPage>(
                TradeApplicationDescriptors.NftInventoryRefresh,
                RefreshNftInventory),
            new ApplicationEventBinding<TradeChanged>(
                TradeApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        trade.StateCommitted += OnStateCommitted;
        trade.StateChanged += OnStateChanged;
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public TradeStateView ReadState(TradeStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOutputLimit(request.OfferItemLimit, nameof(request.OfferItemLimit));
        ValidateOutputLimit(request.NftOfferLimit, nameof(request.NftOfferLimit));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            TradeState state = trade.State;
            Session? session = connection.Session;
            if (!ReferenceEquals(state.Session, session))
                continue;
            long room_generation = room.Capture(static current => current.Generation);
            TradeStateView view = StateView(
                state,
                room_generation,
                request.OfferItemLimit,
                request.NftOfferLimit);
            if (ReferenceEquals(trade.State, state) &&
                ReferenceEquals(connection.Session, session) &&
                room.Capture(static current => current.Generation) == room_generation)
            {
                return view;
            }
        }
        throw new InvalidOperationException("The trade session changed while its state was being projected.");
    }

    public ValueTask<TradeDispatchResult> Open(
        TradeOpenRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.UserIndex);
        TradeDispatchScope scope = CaptureDispatchScope(
            false,
            null,
            request.ExpectedSessionGeneration,
            request.ExpectedRevision,
            request.ExpectedEpoch,
            request.ExpectedRoomGeneration,
            request.UserIndex,
            request.ExpectedUserId,
            false,
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Trade.OpenRequest,
            new OpenTradeRequest(request.UserIndex),
            scope.Session,
            cancellation_token,
            () => RequireDispatchScope(scope));
        return DispatchResult(scope);
    }

    public ValueTask<TradeDispatchResult> AddItems(
        TradeItemsAddRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ItemIds);
        TradeDispatchScope scope = CaptureDispatchScope(
            true,
            TradePhase.Trading,
            request.ExpectedSessionGeneration,
            request.ExpectedRevision,
            request.ExpectedEpoch,
            null,
            null,
            null,
            false,
            cancellation_token);
        Id[] item_ids = request.ItemIds.ToArray();
        ValidateItemIds(item_ids, scope.Session.Client);
        message_dispatcher.Dispatch(
            MessageContracts.Trade.ItemsAdd,
            new AddTradeItemsRequest(Array.AsReadOnly(item_ids)),
            scope.Session,
            cancellation_token,
            () => RequireDispatchScope(scope));
        return DispatchResult(scope);
    }

    public ValueTask<TradeDispatchResult> RemoveItem(
        TradeItemRemoveRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        TradeDispatchScope scope = CaptureDispatchScope(
            true,
            TradePhase.Trading,
            request.ExpectedSessionGeneration,
            request.ExpectedRevision,
            request.ExpectedEpoch,
            null,
            null,
            null,
            false,
            cancellation_token);
        ValidateItemId(request.ItemId, scope.Session.Client, nameof(request.ItemId));
        message_dispatcher.Dispatch(
            MessageContracts.Trade.ItemRemove,
            new RemoveTradeItemRequest(request.ItemId),
            scope.Session,
            cancellation_token,
            () => RequireDispatchScope(scope));
        return DispatchResult(scope);
    }

    public ValueTask<TradeDispatchResult> Accept(
        TradeCommandRequest request,
        CancellationToken cancellation_token) => DispatchCommand(
        request,
        MessageContracts.Trade.Accept,
        new AcceptTradeRequest(),
        TradePhase.Trading,
        true,
        cancellation_token);

    public ValueTask<TradeDispatchResult> Unaccept(
        TradeCommandRequest request,
        CancellationToken cancellation_token) => DispatchCommand(
        request,
        MessageContracts.Trade.Unaccept,
        new UnacceptTradeRequest(),
        null,
        false,
        cancellation_token);

    public ValueTask<TradeDispatchResult> Confirm(
        TradeCommandRequest request,
        CancellationToken cancellation_token) => DispatchCommand(
        request,
        MessageContracts.Trade.Confirm,
        new ConfirmTradeRequest(),
        TradePhase.AwaitingConfirmation,
        true,
        cancellation_token);

    public ValueTask<TradeDispatchResult> Close(
        TradeCommandRequest request,
        CancellationToken cancellation_token) => DispatchCommand(
        request,
        MessageContracts.Trade.Close,
        new CloseTradeRequest(),
        null,
        false,
        cancellation_token);

    public TradeNftInventoryPage NftInventory(TradeNftInventoryPageRequest request)
    {
        ThrowIfDisposed();
        ValidatePage(request);
        NftInventoryLease lease = request.SnapshotRevision is long revision
            ? Lease(revision)
            : StoreLease(CaptureStableState());
        return Page(lease, request.Offset, request.Limit);
    }

    public async ValueTask<TradeNftInventoryPage> RefreshNftInventory(
        TradeNftInventoryRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        TradeSessionScope scope = CaptureSessionScope(cancellation_token);
        long baseline = -1;
        TradeStateUpdate? accepted = null;
        int armed = 0;
        await requests.RequestAsync(
            MessageContracts.Trade.NftInventoryRequest,
            new GetNftTradeInventoryRequest(),
            MessageContracts.Trade.NftInventory,
            scope.Session,
            match: response =>
            {
                if (Volatile.Read(ref armed) == 0 || !SessionScopeActive(scope))
                    return false;
                TradeStateUpdate? update = FindNftInventoryUpdate(
                    scope.SessionGeneration,
                    Volatile.Read(ref baseline),
                    response);
                if (update is null)
                    return false;
                Volatile.Write(ref accepted, update);
                return true;
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireSessionScope(scope);
                lock (updates_sync)
                {
                    Volatile.Write(ref baseline, trade.State.NftInventory.Revision);
                    Volatile.Write(ref armed, 1);
                }
            }).ConfigureAwait(false);
        RequireSessionScope(scope);
        TradeStateUpdate update = Volatile.Read(ref accepted)
            ?? throw new InvalidOperationException(
                "The accepted NFT inventory response was not committed by the passive trade state owner.");
        return Page(StoreLease(update.State), 0, request.Limit);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        trade.StateCommitted -= OnStateCommitted;
        trade.StateChanged -= OnStateChanged;
        lock (updates_sync)
            inventory_updates.Clear();
        ClearLeases();
        changed.Dispose();
    }

    private ValueTask<TradeDispatchResult> DispatchCommand<T>(
        TradeCommandRequest request,
        MessageContract<T> contract,
        T message,
        TradePhase? required_phase,
        bool require_revision,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        TradeDispatchScope scope = CaptureDispatchScope(
            true,
            required_phase,
            request.ExpectedSessionGeneration,
            request.ExpectedRevision,
            request.ExpectedEpoch,
            null,
            null,
            null,
            require_revision,
            cancellation_token);
        message_dispatcher.Dispatch(
            contract,
            message,
            scope.Session,
            cancellation_token,
            () => RequireDispatchScope(scope));
        return DispatchResult(scope);
    }

    private ValueTask<TradeDispatchResult> DispatchResult(TradeDispatchScope scope) =>
        ValueTask.FromResult(new TradeDispatchResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            scope.RoomGeneration,
            scope.StateRevision,
            scope.Epoch));

    private void OnStateCommitted(TradeStateUpdate update)
    {
        if (update.Kind is TradeStateChangeKind.Reset)
        {
            lock (updates_sync)
                inventory_updates.Clear();
            ClearLeases();
            return;
        }
        if (update.Kind is not TradeStateChangeKind.NftInventoryUpdated)
            return;
        lock (updates_sync)
        {
            inventory_updates.Add(update);
            if (inventory_updates.Count > commit_history_limit)
            {
                inventory_updates.RemoveRange(
                    0,
                    inventory_updates.Count - commit_history_limit);
            }
        }
    }

    private void OnStateChanged(TradeStateUpdate update)
    {
        TradeState current = trade.State;
        if (!ReferenceEquals(update.State.Session, connection.Session) ||
            !ReferenceEquals(current.Session, update.State.Session) ||
            current.Generation != update.State.Generation)
        {
            return;
        }
        TradeAcceptanceChange? acceptance = update.Value is TradeAcceptanceCommit accepted
            ? new TradeAcceptanceChange(accepted.UserId, accepted.Accepted)
            : null;
        TradeCloseResult? close = update.Value is TradeCloseCommit closed
            ? new TradeCloseResult(closed.UserId, closed.Reason)
            : null;
        TradeOpenFailure? failure = update.Value is TradeOpenFailureCommit failed
            ? new TradeOpenFailure(failed.Reason, failed.OtherUserName)
            : null;
        changed.Publish(new TradeChanged(
            ChangeKind(update.Kind),
            time_provider.GetUtcNow(),
            StateSummary(update.State),
            update.PreviousEpoch is null ? null : EpochSummary(update.PreviousEpoch),
            acceptance,
            close,
            failure));
    }

    private TradeDispatchScope CaptureDispatchScope(
        bool active,
        TradePhase? required_phase,
        long? expected_session_generation,
        long? expected_revision,
        long? expected_epoch,
        long? expected_room_generation,
        int? target_index,
        Id? expected_user_id,
        bool safety_gated,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        Session session = connection.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(connection.Session, session))
                throw new InvalidOperationException("The hotel session changed before the trade operation started.");
            if (!current_room.IsReady)
                throw new InvalidOperationException("A ready hotel room is required for trade operations.");
            if (expected_room_generation is long room_generation &&
                room_generation != current_room.Generation)
            {
                throw new InvalidOperationException("The expected room generation is no longer active.");
            }
            Id? target_user_id = null;
            if (target_index is int index)
            {
                if (current_room.AvatarByIndex(index) is not User target)
                    throw new InvalidOperationException("The target room index is not an active user.");
                if (expected_user_id is Id user_id && user_id != target.Id)
                    throw new InvalidOperationException("The expected trade target no longer owns the room index.");
                ProfileState current_profile = profile.State;
                if (ReferenceEquals(current_profile.Session, session) &&
                    current_profile.Identity?.Id == target.Id)
                {
                    throw new InvalidOperationException("A user cannot open a trade with their own room avatar.");
                }
                target_user_id = target.Id;
            }
            TradeState current = trade.State;
            if (!ReferenceEquals(current.Session, session))
                throw new InvalidOperationException("The trade state is not bound to the active hotel session.");
            if (expected_session_generation is long session_generation &&
                session_generation != current.Generation)
            {
                throw new InvalidOperationException("The expected trade session generation is no longer active.");
            }
            if (expected_revision is long revision && revision != current.Revision)
                throw new InvalidOperationException("The expected trade state revision is no longer active.");
            if (expected_epoch is long epoch && epoch != current.Epoch)
                throw new InvalidOperationException("The expected trade epoch is no longer active.");
            TradeEpochState? active_state = current.Active;
            if (active && active_state is null)
                throw new InvalidOperationException("An active trade is required.");
            if (!active && current.Active is not null)
                throw new InvalidOperationException("A trade is already active.");
            if (required_phase is TradePhase phase && active_state!.Phase != phase)
                throw new InvalidOperationException($"The trade must be in the '{phase}' phase.");
            Id? local_user_id = null;
            if (safety_gated)
            {
                ProfileState profile_state = profile.State;
                if (!ReferenceEquals(profile_state.Session, session) || profile_state.Identity is not { } identity)
                    throw new InvalidOperationException("The active local profile is required for trade confirmation.");
                TradeParticipantState? participant = active_state!.FirstParticipant.UserId == identity.Id
                    ? active_state.FirstParticipant
                    : active_state.SecondParticipant.UserId == identity.Id
                        ? active_state.SecondParticipant
                        : null;
                if (participant is null)
                    throw new InvalidOperationException("The local user is not a participant in the active trade.");
                if (!participant.CanTrade)
                    throw new InvalidOperationException("The hotel marked the local participant as unable to trade.");
                if (!SilverFeeReached(active_state))
                    throw new InvalidOperationException("The active trade has not reached its required silver fee.");
                local_user_id = identity.Id;
            }
            return new TradeDispatchScope(
                session,
                current.Generation,
                current_room.Generation,
                current.Revision,
                current.Epoch,
                active,
                required_phase,
                expected_revision.HasValue,
                safety_gated,
                safety_gated ? active_state : null,
                local_user_id,
                target_index,
                target_user_id);
        });
    }

    private bool DispatchScopeActive(TradeDispatchScope scope) => room.Capture(current_room =>
    {
        TradeState current = trade.State;
        if (Volatile.Read(ref disposed) != 0 ||
            !ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(current.Session, scope.Session) ||
            current.Generation != scope.SessionGeneration ||
            !current_room.IsReady ||
            current_room.Generation != scope.RoomGeneration ||
            current.Epoch != scope.Epoch ||
            scope.RequireRevision && current.Revision != scope.StateRevision)
        {
            return false;
        }
        if (!scope.Active)
        {
            if (current.Active is not null)
                return false;
            if (scope.TargetIndex is not int target_index)
                return true;
            if (current_room.AvatarByIndex(target_index) is not User target ||
                target.Id != scope.TargetUserId)
            {
                return false;
            }
            ProfileState current_profile = profile.State;
            return !ReferenceEquals(current_profile.Session, scope.Session) ||
                current_profile.Identity?.Id != target.Id;
        }
        return current.Active is { } active &&
            active.Epoch == scope.Epoch &&
            (scope.RequiredPhase is null || active.Phase == scope.RequiredPhase) &&
            SafetyScopeActive(scope, active);
    });

    private bool SafetyScopeActive(TradeDispatchScope scope, TradeEpochState active)
    {
        if (!scope.SafetyGated)
            return true;
        if (!ReferenceEquals(active, scope.ActiveState))
            return false;
        ProfileState profile_state = profile.State;
        if (!ReferenceEquals(profile_state.Session, scope.Session) ||
            profile_state.Identity?.Id != scope.LocalUserId)
        {
            return false;
        }
        TradeParticipantState? participant = active.FirstParticipant.UserId == scope.LocalUserId
            ? active.FirstParticipant
            : active.SecondParticipant.UserId == scope.LocalUserId
                ? active.SecondParticipant
                : null;
        return participant?.CanTrade == true && SilverFeeReached(active);
    }

    private void RequireDispatchScope(TradeDispatchScope scope)
    {
        ThrowIfDisposed();
        if (!DispatchScopeActive(scope))
            throw new InvalidOperationException("The hotel session, room, trade epoch, or required phase changed before dispatch.");
    }

    private TradeSessionScope CaptureSessionScope(CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        TradeState current = trade.State;
        Session session = current.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The trade state is not bound to the active hotel session.");
        return new TradeSessionScope(session, current.Generation);
    }

    private TradeState CaptureStableState()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            TradeState state = trade.State;
            Session? session = connection.Session;
            if (ReferenceEquals(state.Session, session) &&
                ReferenceEquals(trade.State, state) &&
                ReferenceEquals(connection.Session, session))
            {
                return state;
            }
        }
        throw new InvalidOperationException("The trade session changed while its NFT inventory was being captured.");
    }

    private bool SessionScopeActive(TradeSessionScope scope)
    {
        TradeState current = trade.State;
        return Volatile.Read(ref disposed) == 0 &&
            ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(current.Session, scope.Session) &&
            current.Generation == scope.SessionGeneration;
    }

    private void RequireSessionScope(TradeSessionScope scope)
    {
        ThrowIfDisposed();
        if (!SessionScopeActive(scope))
            throw new InvalidOperationException("The hotel session changed during the trade inventory operation.");
    }

    private TradeStateUpdate? FindNftInventoryUpdate(
        long session_generation,
        long baseline_revision,
        TradeNftAssetInventory response)
    {
        lock (updates_sync)
        {
            for (int index = inventory_updates.Count - 1; index >= 0; index--)
            {
                TradeStateUpdate update = inventory_updates[index];
                if (update.State.Generation != session_generation ||
                    update.State.NftInventory.Revision <= baseline_revision)
                {
                    continue;
                }
                if (TradeManager.EquivalentNftInventory(update.State.NftInventory, response))
                    return update;
            }
        }
        return null;
    }

    private NftInventoryLease StoreLease(TradeState state)
    {
        long revision = Interlocked.Increment(ref lease_revision);
        var lease = new NftInventoryLease(
            revision,
            state.Session,
            state.Generation,
            state.Revision,
            state.NftInventory);
        lock (leases_sync)
        {
            leases.Add(revision, lease);
            lease_order.Enqueue(revision);
            while (leases.Count > snapshot_lease_limit && lease_order.TryDequeue(out long expired))
                leases.Remove(expired);
        }
        return lease;
    }

    private NftInventoryLease Lease(long revision)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out NftInventoryLease? lease) || !LeaseActive(lease))
                throw new InvalidOperationException("The trade NFT inventory snapshot lease is unavailable for the active session.");
            return lease;
        }
    }

    private bool LeaseActive(NftInventoryLease lease)
    {
        TradeState current = trade.State;
        return ReferenceEquals(current.Session, lease.Session) &&
            ReferenceEquals(connection.Session, lease.Session) &&
            current.Generation == lease.SessionGeneration;
    }

    private TradeNftInventoryPage Page(NftInventoryLease lease, int offset, int limit)
    {
        if (!LeaseActive(lease))
            throw new InvalidOperationException("The trade NFT inventory snapshot lease is no longer active.");
        IReadOnlyList<TradeNftAssetState> values = Slice(lease.Inventory.Assets, offset, limit);
        IReadOnlyList<TradeNftAssetView> assets = Array.AsReadOnly(
            values.Select(NftAssetView).ToArray());
        bool connected = lease.Session is not null && ReferenceEquals(connection.Session, lease.Session);
        var page = new TradeNftInventoryPage(
            connected,
            connected ? lease.Session!.Client : null,
            lease.SessionGeneration,
            lease.StateRevision,
            lease.Revision,
            lease.Inventory.Revision,
            lease.Inventory.Loaded,
            lease.Inventory.Assets.Count,
            offset,
            NextOffset(offset, assets.Count, lease.Inventory.Assets.Count),
            assets);
        if (!LeaseActive(lease))
            throw new InvalidOperationException("The trade NFT inventory snapshot lease changed while its page was being projected.");
        return page;
    }

    private TradeStateView StateView(
        TradeState state,
        long room_generation,
        int offer_limit,
        int nft_limit)
    {
        bool connected = state.Session is not null && ReferenceEquals(connection.Session, state.Session);
        return new TradeStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.Generation,
            room_generation,
            state.Revision,
            state.Epoch,
            state.Active is null ? null : EpochView(state.Active, offer_limit, nft_limit),
            NftInventorySummary(state.NftInventory));
    }

    private TradeStateSummary StateSummary(TradeState state)
    {
        bool connected = state.Session is not null && ReferenceEquals(connection.Session, state.Session);
        return new TradeStateSummary(
            connected,
            connected ? state.Session!.Client : null,
            state.Generation,
            state.Revision,
            state.Epoch,
            state.Active is null ? null : EpochSummary(state.Active),
            NftInventorySummary(state.NftInventory));
    }

    private static TradeEpochView EpochView(
        TradeEpochState state,
        int offer_limit,
        int nft_limit) => new(
        state.Epoch,
        state.Phase,
        ParticipantView(state.FirstParticipant),
        ParticipantView(state.SecondParticipant),
        state.FirstOffer is null ? null : OfferView(state.FirstOffer, offer_limit),
        state.SecondOffer is null ? null : OfferView(state.SecondOffer, offer_limit),
        state.NftOffers is null ? null : NftOfferView(state.NftOffers.OwnAssets, nft_limit),
        state.NftOffers is null ? null : NftOfferView(state.NftOffers.OtherAssets, nft_limit),
        state.OwnSilver,
        state.OtherSilver,
        state.SilverFee,
        SilverFeeReached(state));

    private static TradeEpochSummary EpochSummary(TradeEpochState state) => new(
        state.Epoch,
        state.Phase,
        ParticipantView(state.FirstParticipant),
        ParticipantView(state.SecondParticipant),
        state.FirstOffer is null ? null : OfferSummary(state.FirstOffer),
        state.SecondOffer is null ? null : OfferSummary(state.SecondOffer),
        state.NftOffers?.OwnAssets.Count ?? 0,
        state.NftOffers?.OtherAssets.Count ?? 0,
        state.OwnSilver,
        state.OtherSilver,
        state.SilverFee,
        SilverFeeReached(state));

    private static TradeOfferView OfferView(TradeOfferState state, int limit)
    {
        IReadOnlyList<TradeItemView> items = Array.AsReadOnly(
            state.Items.Take(limit).Select(ItemView).ToArray());
        return new TradeOfferView(
            state.UserId,
            state.FurniCount,
            state.CreditCount,
            state.Items.Count,
            items.Count,
            items.Count != state.Items.Count,
            items);
    }

    private static TradeNftOfferView NftOfferView(
        IReadOnlyList<TradeNftAssetState> values,
        int limit)
    {
        IReadOnlyList<TradeNftAssetView> assets = Array.AsReadOnly(
            values.Take(limit).Select(NftAssetView).ToArray());
        return new TradeNftOfferView(
            values.Count,
            assets.Count,
            assets.Count != values.Count,
            assets);
    }

    private static TradeParticipantView ParticipantView(TradeParticipantState state) => new(
        state.UserId,
        state.CanTrade,
        state.Accepted);

    private static TradeOfferSummary OfferSummary(TradeOfferState state) => new(
        state.UserId,
        state.FurniCount,
        state.CreditCount,
        state.Items.Count);

    private static TradeItemView ItemView(TradeItemState state) => new(
        state.ItemId,
        state.Type,
        state.Id,
        state.Kind,
        state.Category,
        state.IsGroupable,
        state.Data,
        state.CreationDay,
        state.CreationMonth,
        state.CreationYear,
        state.Extra);

    private static TradeNftAssetView NftAssetView(TradeNftAssetState state) => new(
        state.AssetId,
        state.ProductTypeId,
        state.ItemTypeId,
        state.Score,
        state.PetFigureString,
        state.FigureSetIds,
        state.ProductCode,
        state.Rarity);

    private static TradeNftInventorySummary NftInventorySummary(TradeNftInventoryState state) => new(
        state.Revision,
        state.Loaded,
        state.Assets.Count);

    private static bool SilverFeeReached(TradeEpochState state) =>
        (long)state.OwnSilver + state.OtherSilver >= state.SilverFee;

    private static TradeChangeKind ChangeKind(TradeStateChangeKind kind) => kind switch
    {
        TradeStateChangeKind.Opened => TradeChangeKind.Opened,
        TradeStateChangeKind.OffersUpdated => TradeChangeKind.OffersUpdated,
        TradeStateChangeKind.AcceptanceUpdated => TradeChangeKind.AcceptanceUpdated,
        TradeStateChangeKind.Confirmation => TradeChangeKind.Confirmation,
        TradeStateChangeKind.Completed => TradeChangeKind.Completed,
        TradeStateChangeKind.Closed => TradeChangeKind.Closed,
        TradeStateChangeKind.OpenFailed => TradeChangeKind.OpenFailed,
        TradeStateChangeKind.NftOffersUpdated => TradeChangeKind.NftOffersUpdated,
        TradeStateChangeKind.SilverUpdated => TradeChangeKind.SilverUpdated,
        TradeStateChangeKind.SilverFeeUpdated => TradeChangeKind.SilverFeeUpdated,
        TradeStateChangeKind.NftInventoryUpdated => TradeChangeKind.NftInventoryUpdated,
        TradeStateChangeKind.RoomChanged => TradeChangeKind.RoomChanged,
        TradeStateChangeKind.Reset => TradeChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private void ClearLeases()
    {
        lock (leases_sync)
        {
            leases.Clear();
            lease_order.Clear();
        }
    }

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }

    private static void ValidatePage(TradeNftInventoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ValidateLimit(request.Limit);
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.Offset != 0 && request.SnapshotRevision is null)
            throw new ArgumentException("Continuation pages require a snapshot revision.", nameof(request.SnapshotRevision));
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateOutputLimit(int limit, string name)
    {
        if (limit is < 0 or > 500)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateItemIds(IReadOnlyList<Id> item_ids, ClientType client)
    {
        if (item_ids.Count is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(item_ids));
        var distinct = new HashSet<long>();
        foreach (Id item_id in item_ids)
        {
            ValidateItemId(item_id, client, nameof(item_ids));
            if (!distinct.Add(item_id))
                throw new ArgumentException("Trade item identifiers must be distinct.", nameof(item_ids));
        }
    }

    private static void ValidateItemId(Id item_id, ClientType client, string name)
    {
        long value = item_id;
        bool valid = client switch
        {
            ClientType.Flash => value != 0 && value is >= int.MinValue and <= int.MaxValue,
            ClientType.Unity => value != 0,
            _ => false
        };
        if (!valid)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct TradeDispatchScope(
        Session Session,
        long SessionGeneration,
        long RoomGeneration,
        long StateRevision,
        long Epoch,
        bool Active,
        TradePhase? RequiredPhase,
        bool RequireRevision,
        bool SafetyGated,
        TradeEpochState? ActiveState,
        Id? LocalUserId,
        int? TargetIndex,
        Id? TargetUserId);

    private readonly record struct TradeSessionScope(
        Session Session,
        long SessionGeneration);

    private sealed record NftInventoryLease(
        long Revision,
        Session? Session,
        long SessionGeneration,
        long StateRevision,
        TradeNftInventoryState Inventory);
}
