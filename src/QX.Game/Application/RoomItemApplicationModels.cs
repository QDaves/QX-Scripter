using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomFloorItemUseRequest(Id ItemId, int State = 0);

public sealed record RoomWallItemUseRequest(Id ItemId, int State = 0);

public sealed record RoomOneWayDoorEnterRequest(Id ItemId);

public sealed record RoomDiceRequest(Id ItemId);

public sealed record RoomWallItemRemoveRequest(Id ItemId);

public sealed record RoomStickySetRequest(Id ItemId, string Color, string Text);

public sealed record RoomPostItPlaceRequest(Id ItemId, string WallLocation);

public sealed record RoomPostItAddRequest(
    Id ItemId,
    string WallLocation,
    string Color,
    string Text);

public sealed record RoomItemDispatchResult(
    ClientType Client,
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
