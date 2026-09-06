using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class RoomPlacementApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor FloorPlace { get; } = new(
        ApplicationMemberIds.RoomPlacementFloorPlace,
        "Place floor item",
        "Dispatches one inventory floor item placement without assuming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomPlacementFloorPlaceRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            InventoryItemId(),
            new("target", typeof(RoomPlacementFloorPosition), true, null, "Floor tile and direction."),
            ExpectedRoomItemId(),
            ExpectedInventoryRevision(),
            SessionGeneration(),
            RoomGeneration()
        ],
        [
            ApplicationStateKey.HotelConnected,
            ApplicationStateKey.RoomReady,
            ApplicationStateKey.InventoryFurniLoaded
        ],
        [
            new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Reads)
        ],
        [Send(MessageKeys.Room.Item.Place, "unityFloorItemPlacementSchema")],
        new(false, false, false, true));

    public static ApplicationDescriptor WallPlace { get; } = new(
        ApplicationMemberIds.RoomPlacementWallPlace,
        "Place wall item",
        "Dispatches one inventory wall item placement without assuming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomPlacementWallPlaceRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            InventoryItemId(),
            new("target", typeof(RoomPlacementWallPosition), true, null, "Wall and offset coordinates with orientation."),
            ExpectedRoomItemId(),
            ExpectedInventoryRevision(),
            SessionGeneration(),
            RoomGeneration()
        ],
        [
            ApplicationStateKey.HotelConnected,
            ApplicationStateKey.RoomReady,
            ApplicationStateKey.InventoryFurniLoaded
        ],
        [
            new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Reads)
        ],
        [Send(MessageKeys.Room.Item.Place, "unityWallItemPlacementSchema")],
        new(false, false, false, true));

    public static ApplicationDescriptor FloorMove { get; } = Move<RoomPlacementFloorMoveRequest>(
        ApplicationMemberIds.RoomPlacementFloorMove,
        "Move floor item",
        "Dispatches one current floor item move without assuming hotel acceptance.",
        MessageKeys.Room.FloorItem.Move,
        typeof(RoomPlacementFloorPosition));

    public static ApplicationDescriptor WallMove { get; } = Move<RoomPlacementWallMoveRequest>(
        ApplicationMemberIds.RoomPlacementWallMove,
        "Move wall item",
        "Dispatches one current wall item move without assuming hotel acceptance.",
        MessageKeys.Room.WallItem.Move,
        typeof(RoomPlacementWallPosition));

    public static ApplicationDescriptor Pickup { get; } = new(
        ApplicationMemberIds.RoomPlacementPickup,
        "Pick up room item",
        "Dispatches one current room-item pickup without assuming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(RoomPlacementPickupRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            RoomItemId(),
            new("item_kind", typeof(RoomPlacementItemKind), true, null, "Expected floor or wall item kind."),
            new("confirmed", typeof(bool), false, false, "Flash pickup-confirmation response."),
            SessionGeneration(),
            RoomGeneration()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [Send(MessageKeys.Room.Item.Pickup)],
        new(false, true, false, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.RoomPlacementChanged,
        "Room placement changed",
        "Publishes immutable single-item placement changes and room lifecycle invalidation.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomPlacementChanged),
        state_effects:
        [
            new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Changes),
            new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Invalidates)
        ],
        messages:
        [
            Observe(MessageKeys.Room.FloorItem.Added),
            Observe(MessageKeys.Room.FloorItem.Updated),
            Observe(MessageKeys.Room.FloorItem.Removed),
            Observe(MessageKeys.Room.WallItem.Added),
            Observe(MessageKeys.Room.WallItem.Updated),
            Observe(MessageKeys.Room.WallItem.Removed)
        ]);

    public static ApplicationDescriptor PickupConfirmation { get; } = new(
        ApplicationMemberIds.RoomPlacementPickupConfirmation,
        "Room-item pickup confirmation",
        "Publishes the bounded Flash pickup-confirmation prompt without treating it as an acknowledgement.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(RoomPlacementPickupConfirmation),
        messages: [Observe(MessageKeys.Room.Item.PickupConfirmation)]);

    private static ApplicationDescriptor Move<TRequest>(
        string id,
        string title,
        string description,
        MessageKey key,
        Type position_type) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TRequest),
        typeof(RoomPlacementDispatchReceipt),
        [
            RoomItemId(),
            new("target", position_type, true, null, "Target room position."),
            new("expected_source", position_type, false, null, "Optional exact source-position guard."),
            SessionGeneration(),
            RoomGeneration()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [Send(key)],
        new(false, false, false, true));

    private static ApplicationParameterDescriptor InventoryItemId() => new(
        "inventory_item_id",
        typeof(Id),
        true,
        null,
        "Nonzero inventory item identifier.",
        IdConstraint());

    private static ApplicationParameterDescriptor RoomItemId() => new(
        "room_item_id",
        typeof(Id),
        true,
        null,
        "Nonzero current room-item identifier.",
        IdConstraint());

    private static ApplicationParameterDescriptor ExpectedRoomItemId() => new(
        "expected_room_item_id",
        typeof(Id?),
        false,
        null,
        "Optional expected room-item identifier mapped by the inventory snapshot.",
        IdConstraint());

    private static ApplicationParameterDescriptor ExpectedInventoryRevision() => new(
        "expected_inventory_revision",
        typeof(long?),
        false,
        null,
        "Optional exact immutable furni-inventory snapshot revision.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor SessionGeneration() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active hotel-session generation guard.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor RoomGeneration() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional ready-room generation guard.",
        new(Minimum: 0));

    private static ApplicationParameterConstraints IdConstraint() =>
        new(Pattern: "^-?[1-9][0-9]*$");

    private static ApplicationMessageRequirement Send(
        MessageKey key,
        string? schema_capability = null) =>
        new(
            key,
            Direction.Out,
            ApplicationMessageRole.Send,
            SchemaCapability: schema_capability);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);
}
