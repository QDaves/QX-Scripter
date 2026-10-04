using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to use a floor item in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemFloorUse"/>.
/// </remarks>
/// <param name="ItemId">The id of the floor item.</param>
/// <param name="State">The state value sent with the request.</param>
public sealed record RoomFloorItemUseRequest(Id ItemId, int State = 0);

/// <summary>
/// Represents a request to use a wall item in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemWallUse"/>.
/// </remarks>
/// <param name="ItemId">The id of the wall item.</param>
/// <param name="State">The state value sent with the request.</param>
public sealed record RoomWallItemUseRequest(Id ItemId, int State = 0);

/// <summary>
/// Represents a request to click a floor item in the current room without using it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemFloorClick"/>. The hotel answers with what a click means
/// for the item, such as walking up to a teleporter.
/// </remarks>
/// <param name="ItemId">The id of the floor item.</param>
public sealed record RoomFloorItemClickRequest(Id ItemId);

/// <summary>
/// Represents a request to click a wall item in the current room without using it.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemWallClick"/>.
/// </remarks>
/// <param name="ItemId">The id of the wall item.</param>
public sealed record RoomWallItemClickRequest(Id ItemId);

/// <summary>
/// Represents a request to enter a one-way door in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemOneWayDoorEnter"/>.
/// </remarks>
/// <param name="ItemId">The id of the one-way door.</param>
public sealed record RoomOneWayDoorEnterRequest(Id ItemId);

/// <summary>
/// Represents a request to throw or clear a dice in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemDiceThrow"/> and <see cref="ApplicationMemberIds.RoomItemDiceClear"/>.
/// </remarks>
/// <param name="ItemId">The id of the dice.</param>
public sealed record RoomDiceRequest(Id ItemId);

/// <summary>
/// Represents a request to remove a wall item from the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemWallRemove"/>. Post-it notes removed this way are deleted.
/// </remarks>
/// <param name="ItemId">The id of the wall item.</param>
public sealed record RoomWallItemRemoveRequest(Id ItemId);

/// <summary>
/// Represents a request to set the color and text of a post-it note in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemStickySet"/>.
/// </remarks>
/// <param name="ItemId">The id of the post-it note.</param>
/// <param name="Color">The note color.</param>
/// <param name="Text">The note text.</param>
public sealed record RoomStickySetRequest(Id ItemId, string Color, string Text);

/// <summary>
/// Represents a request to place an empty post-it note on a wall of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemPostItPlace"/>.
/// </remarks>
/// <param name="ItemId">The id of the post-it note item.</param>
/// <param name="WallLocation">The wall location of the target position, in the form <c>:w=x,y l=x,y o</c>.</param>
public sealed record RoomPostItPlaceRequest(Id ItemId, string WallLocation);

/// <summary>
/// Represents a request to place a post-it note on a wall of the current room with its color and text.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomItemPostItAdd"/>.
/// </remarks>
/// <param name="ItemId">The id of the post-it note item.</param>
/// <param name="WallLocation">The wall location of the target position, in the form <c>:w=x,y l=x,y o</c>.</param>
/// <param name="Color">The note color.</param>
/// <param name="Text">The note text.</param>
public sealed record RoomPostItAddRequest(
    Id ItemId,
    string WallLocation,
    string Color,
    string Text);

/// <summary>
/// Represents the result of sending a room item message to the hotel.
/// </summary>
/// <remarks>
/// Returned by every <c>room.item.*</c> member. The message is only sent while the room generation is
/// unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the current room, or <see langword="null"/> when no room is loaded.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the action, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomItemDispatchResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
