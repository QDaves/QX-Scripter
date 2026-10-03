using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Specifies whether a room item stands on the floor or hangs on a wall.
/// </summary>
public enum RoomPlacementItemKind
{
    /// <summary>A floor item.</summary>
    Floor,
    /// <summary>A wall item.</summary>
    Wall
}

/// <summary>
/// Specifies the placement operation a <see cref="RoomPlacementDispatchReceipt"/> reports.
/// </summary>
public enum RoomPlacementOperationKind
{
    /// <summary>An inventory floor item was placed in the room.</summary>
    PlaceFloor,
    /// <summary>An inventory wall item was placed in the room.</summary>
    PlaceWall,
    /// <summary>A floor item in the room was moved.</summary>
    MoveFloor,
    /// <summary>A wall item in the room was moved.</summary>
    MoveWall,
    /// <summary>A room item was picked up.</summary>
    Pickup
}

/// <summary>
/// Specifies the kind of change reported by <see cref="RoomPlacementChanged"/>.
/// </summary>
public enum RoomPlacementChangeKind
{
    /// <summary>A floor item was added to the room.</summary>
    FloorAdded,
    /// <summary>A floor item in the room was updated.</summary>
    FloorUpdated,
    /// <summary>A floor item was removed from the room.</summary>
    FloorRemoved,
    /// <summary>A wall item was added to the room.</summary>
    WallAdded,
    /// <summary>A wall item in the room was updated.</summary>
    WallUpdated,
    /// <summary>A wall item was removed from the room.</summary>
    WallRemoved,
    /// <summary>The room was left or its state was cleared, so every item in it is gone.</summary>
    RoomReset
}

/// <summary>
/// Represents the tile and direction of a floor item.
/// </summary>
/// <param name="X">The tile x coordinate, 0 or greater.</param>
/// <param name="Y">The tile y coordinate, 0 or greater.</param>
/// <param name="Direction">The direction the item faces, from 0 to 7.</param>
public sealed record RoomPlacementFloorPosition(int X, int Y, int Direction);

/// <summary>
/// Represents where a wall item hangs.
/// </summary>
/// <remarks>
/// The values correspond to the wall location text form <c>:w=WallX,WallY l=OffsetX,OffsetY Orientation</c>.
/// </remarks>
/// <param name="WallX">The x coordinate of the wall tile.</param>
/// <param name="WallY">The y coordinate of the wall tile.</param>
/// <param name="OffsetX">The x offset on the wall tile.</param>
/// <param name="OffsetY">The y offset on the wall tile.</param>
/// <param name="Orientation">The side of the wall the item hangs on, <c>l</c> for left or <c>r</c> for right.</param>
public sealed record RoomPlacementWallPosition(
    int WallX,
    int WallY,
    int OffsetX,
    int OffsetY,
    string Orientation);

/// <summary>
/// Represents a request to place a floor item from the inventory in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPlacementFloorPlace"/>. The room must be ready and the furni
/// inventory must be loaded and current. The message is sent without waiting for the hotel to accept it.
/// </remarks>
/// <param name="InventoryItemId">The inventory item id of the floor item to place.</param>
/// <param name="Target">The tile and direction to place the item at.</param>
/// <param name="ExpectedRoomItemId">
/// The furni id the inventory item must map to, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedInventoryRevision">
/// The furni inventory snapshot revision that must still be current, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The inventory state generation the hotel session must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomPlacementFloorPlaceRequest(
    Id InventoryItemId,
    RoomPlacementFloorPosition Target,
    Id? ExpectedRoomItemId = null,
    long? ExpectedInventoryRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to place a wall item from the inventory in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPlacementWallPlace"/>. The room must be ready and the furni
/// inventory must be loaded and current. The message is sent without waiting for the hotel to accept it.
/// </remarks>
/// <param name="InventoryItemId">The inventory item id of the wall item to place.</param>
/// <param name="Target">The wall location to place the item at.</param>
/// <param name="ExpectedRoomItemId">
/// The furni id the inventory item must map to, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedInventoryRevision">
/// The furni inventory snapshot revision that must still be current, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The inventory state generation the hotel session must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomPlacementWallPlaceRequest(
    Id InventoryItemId,
    RoomPlacementWallPosition Target,
    Id? ExpectedRoomItemId = null,
    long? ExpectedInventoryRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to move a floor item in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPlacementFloorMove"/>. The room must be ready and the item must
/// be in it. The message is sent without waiting for the hotel to accept it.
/// </remarks>
/// <param name="RoomItemId">The id of the floor item in the room.</param>
/// <param name="Target">The tile and direction to move the item to.</param>
/// <param name="ExpectedSource">
/// The tile and direction the item must currently have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The inventory state generation the hotel session must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomPlacementFloorMoveRequest(
    Id RoomItemId,
    RoomPlacementFloorPosition Target,
    RoomPlacementFloorPosition? ExpectedSource = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to move a wall item in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPlacementWallMove"/>. The room must be ready and the item must
/// be in it. The message is sent without waiting for the hotel to accept it.
/// </remarks>
/// <param name="RoomItemId">The id of the wall item in the room.</param>
/// <param name="Target">The wall location to move the item to.</param>
/// <param name="ExpectedSource">
/// The wall location the item must currently have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The inventory state generation the hotel session must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomPlacementWallMoveRequest(
    Id RoomItemId,
    RoomPlacementWallPosition Target,
    RoomPlacementWallPosition? ExpectedSource = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to pick up an item in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomPlacementPickup"/>. The room must be ready and the item must be
/// in it. The message is sent without waiting for the hotel to accept it.
/// </remarks>
/// <param name="RoomItemId">The id of the item in the room.</param>
/// <param name="ItemKind">Whether the item is a floor or a wall item.</param>
/// <param name="Confirmed">
/// Whether the pickup answers a confirmation prompt from the hotel, reported by
/// <see cref="ApplicationMemberIds.RoomPlacementPickupConfirmation"/>.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The inventory state generation the hotel session must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomPlacementPickupRequest(
    Id RoomItemId,
    RoomPlacementItemKind ItemKind,
    bool Confirmed = false,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents the receipt of a placement message sent to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomPlacementFloorPlace"/>,
/// <see cref="ApplicationMemberIds.RoomPlacementWallPlace"/>, <see cref="ApplicationMemberIds.RoomPlacementFloorMove"/>,
/// <see cref="ApplicationMemberIds.RoomPlacementWallMove"/> and <see cref="ApplicationMemberIds.RoomPlacementPickup"/>.
/// The receipt only confirms that the message was sent, and the room changes are reported by
/// <see cref="ApplicationMemberIds.RoomPlacementChanged"/>.
/// </remarks>
/// <param name="Operation">The operation that was sent.</param>
/// <param name="DispatchedAtUtc">The time the receipt was created after the message was sent.</param>
/// <param name="SessionGeneration">The inventory state generation of the hotel session the message was sent in.</param>
/// <param name="RoomId">The id of the room.</param>
/// <param name="RoomGeneration">The generation of the room.</param>
/// <param name="RoomRevision">The room state revision checked when the message was sent.</param>
/// <param name="InventoryRevision">
/// The furni inventory snapshot revision for a placement, or <see langword="null"/> for a move or pickup.
/// </param>
/// <param name="InventoryItemId">
/// The inventory item id for a placement, or <see langword="null"/> for a move or pickup.
/// </param>
/// <param name="RoomItemId">The id of the room item, which for a placement is the furni id the inventory item maps to.</param>
/// <param name="ItemKind">Whether the item is a floor or a wall item.</param>
/// <param name="FloorTarget">
/// The target of a floor placement or move, or <see langword="null"/> for other operations.
/// </param>
/// <param name="WallTarget">
/// The target of a wall placement or move, or <see langword="null"/> for other operations.
/// </param>
/// <param name="Confirmed">Whether a pickup was sent as confirmed, which is <see langword="false"/> for other operations.</param>
public sealed record RoomPlacementDispatchReceipt(
    RoomPlacementOperationKind Operation,
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

/// <summary>
/// Represents the position of one room item at a point in time.
/// </summary>
/// <param name="RoomItemId">The id of the item in the room.</param>
/// <param name="ItemKind">Whether the item is a floor or a wall item.</param>
/// <param name="FloorPosition">The tile and direction of a floor item, or <see langword="null"/> for a wall item.</param>
/// <param name="WallPosition">The wall location of a wall item, or <see langword="null"/> for a floor item.</param>
public sealed record RoomPlacementItemView(
    Id RoomItemId,
    RoomPlacementItemKind ItemKind,
    RoomPlacementFloorPosition? FloorPosition,
    RoomPlacementWallPosition? WallPosition);

/// <summary>
/// Represents a change of one item in the current room, or the reset of the room.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.RoomPlacementChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="SessionGeneration">The room state generation of the hotel session the change was received in.</param>
/// <param name="RoomId">The id of the room.</param>
/// <param name="RoomGeneration">The generation of the room.</param>
/// <param name="RoomRevision">The room state revision of the change.</param>
/// <param name="Previous">
/// The item before the change, or <see langword="null"/> for an added item, an update of an unknown item or a
/// <see cref="RoomPlacementChangeKind.RoomReset"/>.
/// </param>
/// <param name="Current">
/// The item after the change, or <see langword="null"/> for a removed item or a
/// <see cref="RoomPlacementChangeKind.RoomReset"/>.
/// </param>
/// <param name="PickerId">
/// The id of the user who picked up a removed item, or <see langword="null"/> for other changes.
/// </param>
/// <param name="IsExpired">
/// Whether a removed floor item expired, or <see langword="null"/> for other changes.
/// </param>
/// <param name="Delay">
/// The removal delay the hotel sent for a removed floor item, or <see langword="null"/> for other changes.
/// </param>
public sealed record RoomPlacementChanged(
    RoomPlacementChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    RoomPlacementItemView? Previous,
    RoomPlacementItemView? Current,
    Id? PickerId,
    bool? IsExpired,
    int? Delay);

/// <summary>
/// Represents a prompt from the hotel asking to confirm picking up a room item.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.RoomPlacementPickupConfirmation"/>. The prompt is not an
/// acknowledgement of a pickup. Send the pickup again with <see cref="RoomPlacementPickupRequest.Confirmed"/>
/// set to confirm it.
/// </remarks>
/// <param name="ReceivedAtUtc">The time the prompt was published.</param>
/// <param name="SessionGeneration">The room state generation of the hotel session the prompt was received in.</param>
/// <param name="RoomId">The id of the room.</param>
/// <param name="RoomGeneration">The generation of the room.</param>
/// <param name="RoomRevision">The room state revision when the prompt was received.</param>
/// <param name="Category">The item category, 1 for a wall item or 2 for a floor item.</param>
/// <param name="RoomItemId">The id of the item to pick up.</param>
/// <param name="Title">The title of the confirmation dialog.</param>
/// <param name="Body">The body text of the confirmation dialog.</param>
public sealed record RoomPlacementPickupConfirmation(
    DateTimeOffset ReceivedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int Category,
    Id RoomItemId,
    string Title,
    string Body);
