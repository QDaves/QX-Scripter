using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to answer a user ringing the doorbell of the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomDoorbellAnswer"/>.
/// </remarks>
/// <param name="UserName">The name of the user at the door.</param>
/// <param name="Allow">Whether to let the user in instead of turning them away.</param>
public sealed record RoomDoorbellAnswerRequest(string UserName, bool Allow = true);

/// <summary>
/// Represents a request to drop the item the local avatar is holding.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomHandItemDrop"/>.
/// </remarks>
public sealed record RoomHandItemDropRequest;

/// <summary>
/// Represents a request to give the item the local avatar is holding to another user.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomHandItemPass"/>.
/// </remarks>
/// <param name="UserId">The id of the user who receives the item.</param>
public sealed record RoomHandItemPassRequest(Id UserId);

/// <summary>
/// Represents a request to rate the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomRatingSubmit"/>.
/// </remarks>
/// <param name="Rating">The rating value sent to the hotel.</param>
public sealed record RoomRatingRequest(int Rating);

/// <summary>
/// Represents a request to add a room to or remove it from the staff picks.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomStaffPickSet"/>. The hotel toggles the staff pick, so the
/// request states the current value as the opposite of <paramref name="Pick"/>. The room does not have
/// to be the current room.
/// </remarks>
/// <param name="RoomId">The id of the room.</param>
/// <param name="Pick">Whether to make the room a staff pick instead of removing it.</param>
public sealed record RoomStaffPickRequest(Id RoomId, bool Pick = true);

/// <summary>
/// Represents the result of sending a room control message to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomDoorbellAnswer"/>, <see cref="ApplicationMemberIds.RoomHandItemDrop"/>,
/// <see cref="ApplicationMemberIds.RoomHandItemPass"/>, <see cref="ApplicationMemberIds.RoomRatingSubmit"/> and
/// <see cref="ApplicationMemberIds.RoomStaffPickSet"/>. Messages for the current room are only sent while the
/// room generation is unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">
/// The id of the current room, or <see langword="null"/> when no room is loaded. For a staff pick, the requested room id.
/// </param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the action, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomControlDispatchResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
