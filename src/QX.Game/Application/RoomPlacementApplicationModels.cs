using Qx.Model;

namespace Qx.Game.Application;

public enum RoomPlacementItemKind
{
    Floor,
    Wall
}

public enum RoomPlacementOperationKind
{
    PlaceFloor,
    PlaceWall,
    MoveFloor,
    MoveWall,
    Pickup
}

public enum RoomPlacementChangeKind
{
    FloorAdded,
    FloorUpdated,
    FloorRemoved,
    WallAdded,
    WallUpdated,
    WallRemoved,
    RoomReset
}

public sealed record RoomPlacementFloorPosition(int X, int Y, int Direction);

public sealed record RoomPlacementWallPosition(
    int WallX,
    int WallY,
    int OffsetX,
    int OffsetY,
    string Orientation);

public sealed record RoomPlacementFloorPlaceRequest(
    Id InventoryItemId,
    RoomPlacementFloorPosition Target,
    Id? ExpectedRoomItemId = null,
    long? ExpectedInventoryRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomPlacementWallPlaceRequest(
    Id InventoryItemId,
    RoomPlacementWallPosition Target,
    Id? ExpectedRoomItemId = null,
    long? ExpectedInventoryRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomPlacementFloorMoveRequest(
    Id RoomItemId,
    RoomPlacementFloorPosition Target,
    RoomPlacementFloorPosition? ExpectedSource = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomPlacementWallMoveRequest(
    Id RoomItemId,
    RoomPlacementWallPosition Target,
    RoomPlacementWallPosition? ExpectedSource = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomPlacementPickupRequest(
    Id RoomItemId,
    RoomPlacementItemKind ItemKind,
    bool Confirmed = false,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomPlacementDispatchReceipt(
    RoomPlacementOperationKind Operation,
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    long? InventoryRevision,
    Id? InventoryItemId,
    Id RoomItemId,
    RoomPlacementItemKind ItemKind,
    RoomPlacementFloorPosition? FloorTarget,
    RoomPlacementWallPosition? WallTarget,
    bool Confirmed);

public sealed record RoomPlacementItemView(
    Id RoomItemId,
    RoomPlacementItemKind ItemKind,
    RoomPlacementFloorPosition? FloorPosition,
    RoomPlacementWallPosition? WallPosition);

public sealed record RoomPlacementChanged(
    RoomPlacementChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType Client,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    RoomPlacementItemView? Previous,
    RoomPlacementItemView? Current,
    Id? PickerId,
    bool? IsExpired,
    int? Delay);

public sealed record RoomPlacementPickupConfirmation(
    DateTimeOffset ReceivedAtUtc,
    ClientType Client,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int Category,
    Id RoomItemId,
    string Title,
    string Body);
