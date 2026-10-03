using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to walk the local avatar to a tile in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarWalk"/>.
/// </remarks>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
public sealed record RoomAvatarWalkRequest(int X, int Y);

/// <summary>
/// Represents a request to turn the local avatar toward a tile in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarLook"/>.
/// </remarks>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
public sealed record RoomAvatarLookRequest(int X, int Y);

/// <summary>
/// Represents a request to start or stop a dance on the local avatar.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarDance"/>.
/// </remarks>
/// <param name="Style">The dance style, or 0 to stop dancing.</param>
public sealed record RoomAvatarDanceRequest(int Style);

/// <summary>
/// Represents a request to play an expression, such as a wave, on the local avatar.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarExpression"/>.
/// </remarks>
/// <param name="Expression">The expression id.</param>
public sealed record RoomAvatarExpressionRequest(int Expression);

/// <summary>
/// Represents a request to set the posture of the local avatar.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarPosture"/>.
/// </remarks>
/// <param name="Posture">The posture id.</param>
public sealed record RoomAvatarPostureRequest(int Posture);

/// <summary>
/// Represents a request to hold up a sign over the local avatar.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarSign"/>.
/// </remarks>
/// <param name="Sign">The sign id.</param>
public sealed record RoomAvatarSignRequest(int Sign);

/// <summary>
/// Represents a request to select the effect the local avatar wears.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarEffect"/>.
/// </remarks>
/// <param name="Effect">The effect id.</param>
public sealed record RoomAvatarEffectRequest(int Effect);

/// <summary>
/// Represents a request to show or hide the typing indicator over the local avatar.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomAvatarTyping"/>.
/// </remarks>
/// <param name="Active">Whether the typing indicator is shown.</param>
public sealed record RoomAvatarTypingRequest(bool Active);

/// <summary>
/// Represents the result of sending a local avatar action to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomAvatarWalk"/>, <see cref="ApplicationMemberIds.RoomAvatarLook"/>,
/// <see cref="ApplicationMemberIds.RoomAvatarDance"/>, <see cref="ApplicationMemberIds.RoomAvatarExpression"/>,
/// <see cref="ApplicationMemberIds.RoomAvatarPosture"/>, <see cref="ApplicationMemberIds.RoomAvatarSign"/>,
/// <see cref="ApplicationMemberIds.RoomAvatarEffect"/> and <see cref="ApplicationMemberIds.RoomAvatarTyping"/>.
/// The message is only sent while the room generation is unchanged, and the hotel's response is not awaited.
/// </remarks>
/// <param name="RoomId">The id of the current room, or <see langword="null"/> when no room is loaded.</param>
/// <param name="RoomGeneration">The room state generation the message was sent in.</param>
/// <param name="Dispatched">Whether the message was sent.</param>
/// <param name="ServerConfirmed">Whether the hotel confirmed the action, which is always <see langword="false"/>.</param>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
public sealed record RoomAvatarDispatchResult(
    Id? RoomId,
    long RoomGeneration,
    bool Dispatched,
    bool ServerConfirmed,
    DateTimeOffset DispatchedAtUtc);
