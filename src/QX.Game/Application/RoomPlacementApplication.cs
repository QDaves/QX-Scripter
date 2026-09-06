using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

internal interface IRoomPlacementOperations
{
    RoomPlacementDispatchReceipt PlaceFloor(
        RoomPlacementFloorPlaceRequest request,
        CancellationToken cancellation_token = default);

    RoomPlacementDispatchReceipt PlaceWall(
        RoomPlacementWallPlaceRequest request,
        CancellationToken cancellation_token = default);

    RoomPlacementDispatchReceipt MoveFloor(
        RoomPlacementFloorMoveRequest request,
        CancellationToken cancellation_token = default);

    RoomPlacementDispatchReceipt MoveWall(
        RoomPlacementWallMoveRequest request,
        CancellationToken cancellation_token = default);

    RoomPlacementDispatchReceipt Pickup(
        RoomPlacementPickupRequest request,
        CancellationToken cancellation_token = default);
}

internal sealed class RoomPlacementApplication : IApplicationFeature, IRoomPlacementOperations
{
    private readonly IConnection connection;
    private readonly RoomManager room;
    private readonly InventoryManager inventory;
    private readonly RoomActions room_actions;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<RoomPlacementChanged> changed;
    private readonly ApplicationEventSource<RoomPlacementPickupConfirmation> pickup_confirmation;
    private int disposed;

    public RoomPlacementApplication(
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
        inventory = game.Inventory;
        room_actions = game.RoomActions;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<RoomPlacementChanged>(observer_error);
        pickup_confirmation = new ApplicationEventSource<RoomPlacementPickupConfirmation>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<RoomPlacementFloorPlaceRequest, RoomPlacementDispatchReceipt>(
                RoomPlacementApplicationDescriptors.FloorPlace,
                (request, cancellation_token) =>
                    ValueTask.FromResult(PlaceFloor(request, cancellation_token))),
            new ApplicationCallBinding<RoomPlacementWallPlaceRequest, RoomPlacementDispatchReceipt>(
                RoomPlacementApplicationDescriptors.WallPlace,
                (request, cancellation_token) =>
                    ValueTask.FromResult(PlaceWall(request, cancellation_token))),
            new ApplicationCallBinding<RoomPlacementFloorMoveRequest, RoomPlacementDispatchReceipt>(
                RoomPlacementApplicationDescriptors.FloorMove,
                (request, cancellation_token) =>
                    ValueTask.FromResult(MoveFloor(request, cancellation_token))),
            new ApplicationCallBinding<RoomPlacementWallMoveRequest, RoomPlacementDispatchReceipt>(
                RoomPlacementApplicationDescriptors.WallMove,
                (request, cancellation_token) =>
                    ValueTask.FromResult(MoveWall(request, cancellation_token))),
            new ApplicationCallBinding<RoomPlacementPickupRequest, RoomPlacementDispatchReceipt>(
                RoomPlacementApplicationDescriptors.Pickup,
                (request, cancellation_token) =>
                    ValueTask.FromResult(Pickup(request, cancellation_token))),
            new ApplicationEventBinding<RoomPlacementChanged>(
                RoomPlacementApplicationDescriptors.Changed,
                changed.Subscribe),
            new ApplicationEventBinding<RoomPlacementPickupConfirmation>(
                RoomPlacementApplicationDescriptors.PickupConfirmation,
                pickup_confirmation.Subscribe)
        ]);

        room.PlacementStateCommitted += OnPlacementStateCommitted;
        room.PickupConfirmationReceived += OnPickupConfirmationReceived;
        try
        {
            room_actions.BindPlacementOperations(this);
        }
        catch
        {
            room.PlacementStateCommitted -= OnPlacementStateCommitted;
            room.PickupConfirmationReceived -= OnPickupConfirmationReceived;
            changed.Dispose();
            pickup_confirmation.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public RoomPlacementDispatchReceipt PlaceFloor(
        RoomPlacementFloorPlaceRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        RoomPlacementFloorPosition target = ValidateFloorPosition(request.Target, nameof(request.Target));
        ValidateId(request.InventoryItemId, nameof(request.InventoryItemId));
        ValidateId(request.ExpectedRoomItemId, nameof(request.ExpectedRoomItemId));
        ValidateGeneration(request.ExpectedInventoryRevision, nameof(request.ExpectedInventoryRevision));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        PlaceScope scope = CapturePlaceScope(
            request.InventoryItemId,
            RoomPlacementItemKind.Floor,
            request.ExpectedRoomItemId,
            request.ExpectedInventoryRevision,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.ItemPlace,
            PlaceRoomItemRequest.Floor(scope.InventoryItemId, target.X, target.Y, target.Direction),
            scope.Session,
            cancellation_token,
            () => room_revision = RequirePlaceScope(scope));
        return Receipt(
            RoomPlacementOperationKind.PlaceFloor,
            scope,
            room_revision,
            RoomPlacementItemKind.Floor,
            target,
            null,
            false);
    }

    public RoomPlacementDispatchReceipt PlaceWall(
        RoomPlacementWallPlaceRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        WallLocation target_location = ValidateWallPosition(request.Target, nameof(request.Target));
        ValidateId(request.InventoryItemId, nameof(request.InventoryItemId));
        ValidateId(request.ExpectedRoomItemId, nameof(request.ExpectedRoomItemId));
        ValidateGeneration(request.ExpectedInventoryRevision, nameof(request.ExpectedInventoryRevision));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        PlaceScope scope = CapturePlaceScope(
            request.InventoryItemId,
            RoomPlacementItemKind.Wall,
            request.ExpectedRoomItemId,
            request.ExpectedInventoryRevision,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.ItemPlace,
            PlaceRoomItemRequest.Wall(scope.InventoryItemId, target_location),
            scope.Session,
            cancellation_token,
            () => room_revision = RequirePlaceScope(scope));
        return Receipt(
            RoomPlacementOperationKind.PlaceWall,
            scope,
            room_revision,
            RoomPlacementItemKind.Wall,
            null,
            request.Target,
            false);
    }

    public RoomPlacementDispatchReceipt MoveFloor(
        RoomPlacementFloorMoveRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.RoomItemId, nameof(request.RoomItemId));
        RoomPlacementFloorPosition target = ValidateFloorPosition(request.Target, nameof(request.Target));
        RoomPlacementFloorPosition? expected_source = request.ExpectedSource is null
            ? null
            : ValidateFloorPosition(request.ExpectedSource, nameof(request.ExpectedSource));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        RoomItemScope scope = CaptureRoomItemScope(
            request.RoomItemId,
            RoomPlacementItemKind.Floor,
            expected_source,
            null,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.FloorItemMove,
            new MoveFloorItemRequest(request.RoomItemId, target.X, target.Y, target.Direction),
            scope.Session,
            cancellation_token,
            () => room_revision = RequireRoomItemScope(scope));
        return Receipt(
            RoomPlacementOperationKind.MoveFloor,
            scope,
            room_revision,
            RoomPlacementItemKind.Floor,
            target,
            null,
            false);
    }

    public RoomPlacementDispatchReceipt MoveWall(
        RoomPlacementWallMoveRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.RoomItemId, nameof(request.RoomItemId));
        WallLocation target_location = ValidateWallPosition(request.Target, nameof(request.Target));
        RoomPlacementWallPosition? expected_source = request.ExpectedSource;
        if (expected_source is not null)
            ValidateWallPosition(expected_source, nameof(request.ExpectedSource));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        RoomItemScope scope = CaptureRoomItemScope(
            request.RoomItemId,
            RoomPlacementItemKind.Wall,
            null,
            expected_source,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.WallItemMove,
            new MoveWallItemRequest(request.RoomItemId, target_location),
            scope.Session,
            cancellation_token,
            () => room_revision = RequireRoomItemScope(scope));
        return Receipt(
            RoomPlacementOperationKind.MoveWall,
            scope,
            room_revision,
            RoomPlacementItemKind.Wall,
            null,
            request.Target,
            false);
    }

    public RoomPlacementDispatchReceipt Pickup(
        RoomPlacementPickupRequest request,
        CancellationToken cancellation_token = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.RoomItemId, nameof(request.RoomItemId));
        if (!Enum.IsDefined(request.ItemKind))
            throw new ArgumentOutOfRangeException(nameof(request.ItemKind));
        ValidateGeneration(request.ExpectedSessionGeneration, nameof(request.ExpectedSessionGeneration));
        ValidateGeneration(request.ExpectedRoomGeneration, nameof(request.ExpectedRoomGeneration));
        RoomItemScope scope = CaptureRoomItemScope(
            request.RoomItemId,
            request.ItemKind,
            null,
            null,
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        if (request.Confirmed && scope.Session.Client is ClientType.Unity)
            throw new NotSupportedException("Unity room-item pickup cannot represent Flash confirmation.");
        int category = request.ItemKind is RoomPlacementItemKind.Floor ? 2 : 1;
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Room.ItemPickup,
            new PickupRoomItemRequest(category, request.RoomItemId, request.Confirmed),
            scope.Session,
            cancellation_token,
            () => room_revision = RequireRoomItemScope(scope));
        return Receipt(
            RoomPlacementOperationKind.Pickup,
            scope,
            room_revision,
            request.ItemKind,
            null,
            null,
            request.Confirmed);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        room_actions.UnbindPlacementOperations(this);
        room.PlacementStateCommitted -= OnPlacementStateCommitted;
        room.PickupConfirmationReceived -= OnPickupConfirmationReceived;
        changed.Dispose();
        pickup_confirmation.Dispose();
    }

    private PlaceScope CapturePlaceScope(
        Id inventory_item_id,
        RoomPlacementItemKind item_kind,
        Id? expected_room_item_id,
        long? expected_inventory_revision,
        long? expected_session_generation,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            InventoryState inventory_state = inventory.State;
            Session session = RequireSession(inventory_state, expected_session_generation);
            RequireRoom(current_room, expected_room_generation);
            FurniInventoryState furni = inventory_state.Furni;
            RequireUsableInventory(furni);
            if (expected_inventory_revision is long inventory_revision &&
                inventory_revision != furni.SnapshotRevision)
            {
                throw new InvalidOperationException("The expected furni-inventory snapshot revision is no longer active.");
            }
            if (!furni.Items.TryGetValue(inventory_item_id, out InventoryItemSnapshot? item) ||
                item.ItemId != inventory_item_id)
            {
                throw new InvalidOperationException("The inventory item is not present in the active furni snapshot.");
            }
            ItemType expected_type = ItemTypeOf(item_kind);
            if (InventoryItemType(item) != expected_type)
                throw new InvalidOperationException("The inventory item type does not match the placement operation.");
            ValidateId(item.Id, nameof(item.Id));
            if (expected_room_item_id is Id room_item_id && room_item_id != item.Id)
                throw new InvalidOperationException("The inventory item no longer maps to the expected room-item identifier.");
            return new PlaceScope(
                session,
                inventory_state.Generation,
                (Id)current_room.RoomId,
                current_room.Generation,
                current_room.Revision,
                furni.SnapshotRevision,
                inventory_item_id,
                item.Id,
                item_kind);
        });
    }

    private RoomItemScope CaptureRoomItemScope(
        Id room_item_id,
        RoomPlacementItemKind item_kind,
        RoomPlacementFloorPosition? expected_floor_source,
        RoomPlacementWallPosition? expected_wall_source,
        long? expected_session_generation,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            InventoryState inventory_state = inventory.State;
            Session session = RequireSession(inventory_state, expected_session_generation);
            RequireRoom(current_room, expected_room_generation);
            if (item_kind is RoomPlacementItemKind.Floor)
            {
                FloorItem item = current_room.FloorItem(room_item_id)
                    ?? throw new InvalidOperationException("The floor item is not present in the active room.");
                RoomPlacementFloorPosition source = FloorPosition(item);
                if (expected_floor_source is not null && expected_floor_source != source)
                    throw new InvalidOperationException("The floor item is no longer at the expected source position.");
                return new RoomItemScope(
                    session,
                    inventory_state.Generation,
                    (Id)current_room.RoomId,
                    current_room.Generation,
                    current_room.Revision,
                    room_item_id,
                    item_kind,
                    item.Kind,
                    item,
                    source,
                    null);
            }

            WallItem wall_item = current_room.WallItem(room_item_id)
                ?? throw new InvalidOperationException("The wall item is not present in the active room.");
            RoomPlacementWallPosition wall_source = WallPosition(wall_item);
            if (expected_wall_source is not null && expected_wall_source != wall_source)
                throw new InvalidOperationException("The wall item is no longer at the expected source position.");
            return new RoomItemScope(
                session,
                inventory_state.Generation,
                (Id)current_room.RoomId,
                current_room.Generation,
                current_room.Revision,
                room_item_id,
                item_kind,
                wall_item.Kind,
                wall_item,
                null,
                wall_source);
        });
    }

    private long RequirePlaceScope(PlaceScope scope)
    {
        ThrowIfDisposed();
        return room.Capture(current_room =>
        {
            InventoryState inventory_state = inventory.State;
            RequireSession(scope, inventory_state);
            RequireRoom(scope, current_room);
            FurniInventoryState furni = inventory_state.Furni;
            RequireUsableInventory(furni);
            if (furni.SnapshotRevision != scope.InventoryRevision ||
                !furni.Items.TryGetValue(scope.InventoryItemId, out InventoryItemSnapshot? item) ||
                item.ItemId != scope.InventoryItemId ||
                item.Id != scope.RoomItemId ||
                InventoryItemType(item) != ItemTypeOf(scope.ItemKind))
            {
                throw new InvalidOperationException("The inventory item mapping changed before placement dispatch.");
            }
            return current_room.Revision;
        });
    }

    private long RequireRoomItemScope(RoomItemScope scope)
    {
        ThrowIfDisposed();
        return room.Capture(current_room =>
        {
            InventoryState inventory_state = inventory.State;
            RequireSession(scope, inventory_state);
            RequireRoom(scope, current_room);
            if (scope.ItemKind is RoomPlacementItemKind.Floor)
            {
                FloorItem item = current_room.FloorItem(scope.RoomItemId)
                    ?? throw new InvalidOperationException("The floor item was removed before dispatch.");
                if (!ReferenceEquals(item, scope.ItemIdentity) ||
                    item.Kind != scope.FurniKind ||
                    FloorPosition(item) != scope.FloorSource)
                {
                    throw new InvalidOperationException("The floor item changed before dispatch.");
                }
            }
            else
            {
                WallItem item = current_room.WallItem(scope.RoomItemId)
                    ?? throw new InvalidOperationException("The wall item was removed before dispatch.");
                if (!ReferenceEquals(item, scope.ItemIdentity) ||
                    item.Kind != scope.FurniKind ||
                    WallPosition(item) != scope.WallSource)
                {
                    throw new InvalidOperationException("The wall item changed before dispatch.");
                }
            }
            return current_room.Revision;
        });
    }

    private Session RequireSession(
        InventoryState state,
        long? expected_session_generation)
    {
        Session session = state.Session
            ?? throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(connection.Session, session))
            throw new InvalidOperationException("The inventory state is not bound to the active hotel session.");
        if (expected_session_generation is long session_generation &&
            session_generation != state.Generation)
        {
            throw new InvalidOperationException("The expected hotel-session generation is no longer active.");
        }
        return session;
    }

    private void RequireSession(PlacementScope scope, InventoryState state)
    {
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.Generation != scope.SessionGeneration)
        {
            throw new InvalidOperationException("The hotel session changed before placement dispatch.");
        }
    }

    private static void RequireRoom(RoomManager current_room, long? expected_room_generation)
    {
        if (!current_room.IsReady || current_room.RoomId == 0)
            throw new InvalidOperationException("A ready hotel room is required for placement operations.");
        if (expected_room_generation is long room_generation &&
            room_generation != current_room.Generation)
        {
            throw new InvalidOperationException("The expected room generation is no longer active.");
        }
    }

    private static void RequireRoom(PlacementScope scope, RoomManager current_room)
    {
        if (!current_room.IsReady ||
            current_room.RoomId != scope.RoomId ||
            current_room.Generation != scope.RoomGeneration)
        {
            throw new InvalidOperationException("The ready room changed before placement dispatch.");
        }
    }

    private static void RequireUsableInventory(FurniInventoryState state)
    {
        if (!state.Loaded || state.Stale || state.RecoveryPending)
            throw new InvalidOperationException("A loaded, current furni inventory is required for placement.");
    }

    private RoomPlacementDispatchReceipt Receipt(
        RoomPlacementOperationKind operation,
        PlaceScope scope,
        long room_revision,
        RoomPlacementItemKind item_kind,
        RoomPlacementFloorPosition? floor_target,
        RoomPlacementWallPosition? wall_target,
        bool confirmed) => new(
        operation,
        scope.Session.Client,
        time_provider.GetUtcNow(),
        scope.SessionGeneration,
        scope.RoomId,
        scope.RoomGeneration,
        room_revision,
        scope.InventoryRevision,
        scope.InventoryItemId,
        scope.RoomItemId,
        item_kind,
        floor_target,
        wall_target,
        confirmed);

    private RoomPlacementDispatchReceipt Receipt(
        RoomPlacementOperationKind operation,
        RoomItemScope scope,
        long room_revision,
        RoomPlacementItemKind item_kind,
        RoomPlacementFloorPosition? floor_target,
        RoomPlacementWallPosition? wall_target,
        bool confirmed) => new(
        operation,
        scope.Session.Client,
        time_provider.GetUtcNow(),
        scope.SessionGeneration,
        scope.RoomId,
        scope.RoomGeneration,
        room_revision,
        null,
        null,
        scope.RoomItemId,
        item_kind,
        floor_target,
        wall_target,
        confirmed);

    private void OnPlacementStateCommitted(RoomPlacementStateCommit commit)
    {
        if (Volatile.Read(ref disposed) != 0)
            return;
        changed.Publish(new RoomPlacementChanged(
            ChangeKind(commit.Kind),
            time_provider.GetUtcNow(),
            commit.Client,
            commit.SessionGeneration,
            commit.RoomId,
            commit.RoomGeneration,
            commit.RoomRevision,
            ItemView(commit.Previous),
            ItemView(commit.Current),
            commit.PickerId,
            commit.IsExpired,
            commit.Delay));
    }

    private void OnPickupConfirmationReceived(RoomPickupConfirmationCommit commit)
    {
        if (Volatile.Read(ref disposed) != 0)
            return;
        pickup_confirmation.Publish(new RoomPlacementPickupConfirmation(
            time_provider.GetUtcNow(),
            commit.Client,
            commit.SessionGeneration,
            commit.RoomId,
            commit.RoomGeneration,
            commit.RoomRevision,
            commit.Category,
            commit.RoomItemId,
            commit.Title,
            commit.Body));
    }

    private static RoomPlacementItemView? ItemView(RoomPlacementCommitItem? item)
    {
        if (item is null)
            return null;
        return new RoomPlacementItemView(
            item.RoomItemId,
            ItemKindOf(item.ItemKind),
            item.FloorPosition is Point floor
                ? new RoomPlacementFloorPosition(
                    floor.X,
                    floor.Y,
                    item.Direction ?? throw new InvalidOperationException("A floor placement commit requires a direction."))
                : null,
            item.WallPosition is WallLocation wall ? WallPosition(wall) : null);
    }

    private static RoomPlacementChangeKind ChangeKind(RoomPlacementCommitKind kind) => kind switch
    {
        RoomPlacementCommitKind.FloorAdded => RoomPlacementChangeKind.FloorAdded,
        RoomPlacementCommitKind.FloorUpdated => RoomPlacementChangeKind.FloorUpdated,
        RoomPlacementCommitKind.FloorRemoved => RoomPlacementChangeKind.FloorRemoved,
        RoomPlacementCommitKind.WallAdded => RoomPlacementChangeKind.WallAdded,
        RoomPlacementCommitKind.WallUpdated => RoomPlacementChangeKind.WallUpdated,
        RoomPlacementCommitKind.WallRemoved => RoomPlacementChangeKind.WallRemoved,
        RoomPlacementCommitKind.RoomReset => RoomPlacementChangeKind.RoomReset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static RoomPlacementFloorPosition FloorPosition(FloorItem item) =>
        new(item.X, item.Y, item.Direction);

    private static RoomPlacementWallPosition WallPosition(WallItem item) =>
        WallPosition(item.Location);

    private static RoomPlacementWallPosition WallPosition(WallLocation location) => new(
        location.Wall.X,
        location.Wall.Y,
        location.Offset.X,
        location.Offset.Y,
        location.Orientation.ToString());

    private static RoomPlacementFloorPosition ValidateFloorPosition(
        RoomPlacementFloorPosition? position,
        string name)
    {
        ArgumentNullException.ThrowIfNull(position, name);
        if (position.X < 0 || position.Y < 0)
            throw new ArgumentOutOfRangeException(name);
        if (position.Direction is < 0 or > 7)
            throw new ArgumentOutOfRangeException(name);
        return position;
    }

    private static WallLocation ValidateWallPosition(
        RoomPlacementWallPosition? position,
        string name)
    {
        ArgumentNullException.ThrowIfNull(position, name);
        if (position.Orientation is null ||
            position.Orientation.Length != 1 ||
            position.Orientation[0] is not ('l' or 'r'))
            throw new ArgumentException("Wall orientation must be 'l' or 'r'.", name);
        return new WallLocation(
            position.WallX,
            position.WallY,
            position.OffsetX,
            position.OffsetY,
            WallOrientation.FromChar(position.Orientation[0]));
    }

    private static ItemType InventoryItemType(InventoryItemSnapshot item) =>
        Enum.TryParse(item.Type, false, out ItemType item_type)
            ? item_type
            : ItemType.None;

    private static ItemType ItemTypeOf(RoomPlacementItemKind item_kind) => item_kind switch
    {
        RoomPlacementItemKind.Floor => ItemType.Floor,
        RoomPlacementItemKind.Wall => ItemType.Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(item_kind))
    };

    private static RoomPlacementItemKind ItemKindOf(ItemType item_type) => item_type switch
    {
        ItemType.Floor => RoomPlacementItemKind.Floor,
        ItemType.Wall => RoomPlacementItemKind.Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(item_type))
    };

    private static void ValidateId(Id? value, string name)
    {
        if (value is Id id)
            ValidateId(id, name);
    }

    private static void ValidateId(Id value, string name)
    {
        if ((long)value == 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateGeneration(long? value, string name)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(name);
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private abstract record PlacementScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision);

    private sealed record PlaceScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision,
        long InventoryRevision,
        Id InventoryItemId,
        Id RoomItemId,
        RoomPlacementItemKind ItemKind) : PlacementScope(
            Session,
            SessionGeneration,
            RoomId,
            RoomGeneration,
            RoomRevision);

    private sealed record RoomItemScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision,
        Id RoomItemId,
        RoomPlacementItemKind ItemKind,
        int FurniKind,
        Furni ItemIdentity,
        RoomPlacementFloorPosition? FloorSource,
        RoomPlacementWallPosition? WallSource) : PlacementScope(
            Session,
            SessionGeneration,
            RoomId,
            RoomGeneration,
            RoomRevision);
}
