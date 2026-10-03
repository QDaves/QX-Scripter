using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to enter a room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomEnter"/>. Only the entry request is sent, and the room load
/// is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the room.</param>
/// <param name="Password">The room password, or an empty string when the room has none.</param>
/// <param name="EntryPoint">The entry point sent with the request, or -1 for none.</param>
public sealed record RoomEnterRequest(
    Id RoomId,
    string Password = "",
    long EntryPoint = -1);

/// <summary>
/// Represents a request to leave the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomLeave"/>.
/// </remarks>
public sealed record RoomLeaveRequest;

/// <summary>
/// Represents the result of sending a room enter or leave request to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomEnter"/> and <see cref="ApplicationMemberIds.RoomLeave"/>.
/// A leave request is only sent while the room generation is unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">
/// The requested room id when entering, or the id of the room being left, which is <see langword="null"/>
/// when no room is loaded.
/// </param>
/// <param name="RoomGeneration">The room state generation when the request was sent.</param>
/// <param name="Dispatched">Whether the request was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the request, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the request was sent.</param>
public sealed record RoomLifecycleDispatchResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
