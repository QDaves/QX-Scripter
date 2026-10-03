using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

/// <summary>Specifies the state of the user's attempt to enter a room.</summary>
public enum RoomAccessState
{
    /// <summary>No room access attempt.</summary>
    Idle,
    /// <summary>A connection to the room that is being opened.</summary>
    Connecting,
    /// <summary>A wait at the room's doorbell for someone with rights to answer.</summary>
    RingingDoorbell,
    /// <summary>A wait in the room's queue.</summary>
    Queued,
    /// <summary>Access to the room, granted by the server.</summary>
    Accessible,
    /// <summary>Access to the room, denied at the doorbell.</summary>
    Denied,
    /// <summary>A room that the server could not find.</summary>
    NotFound,
    /// <summary>A room connection that failed, described by <see cref="RoomManager.ConnectionFailure"/>.</summary>
    ConnectionError
}

/// <summary>Represents the reason the server gave for refusing a room connection.</summary>
/// <param name="Kind">The kind of failure.</param>
/// <param name="ReasonCode">The reason code the server sent.</param>
/// <param name="Parameter">The parameter the server sent with the reason code.</param>
public sealed record RoomConnectionFailure(
    RoomConnectionFailureKind Kind,
    int ReasonCode,
    string Parameter);

/// <summary>Represents a change of the user's room access state.</summary>
/// <param name="PreviousState">The access state before the change.</param>
/// <param name="CurrentState">The access state after the change.</param>
/// <param name="PreviousRoomId">The room the previous state referred to, or <see langword="null"/> when it referred to none.</param>
/// <param name="CurrentRoomId">The room the current state refers to, or <see langword="null"/> when it refers to none.</param>
/// <param name="Failure">The connection failure reported with the change, or <see langword="null"/> when the change is not a connection failure.</param>
public sealed record RoomAccessTransition(
    RoomAccessState PreviousState,
    RoomAccessState CurrentState,
    Id? PreviousRoomId,
    Id? CurrentRoomId,
    RoomConnectionFailure? Failure);

/// <summary>
/// Represents what the user may do in the current room: ownership, rights and spectator state,
/// together with the room's mute state and moderation levels.
/// </summary>
/// <remarks>
/// The mute and moderation members come from the room details of the guest room result and stay
/// <see langword="null"/> until those details arrive.
/// </remarks>
/// <param name="IsOwner">Whether the user owns the room.</param>
/// <param name="RightsLevel">
/// The user's rights level, or <see langword="null"/> when the server has not sent it. The client's
/// scale is 0 not a controller, 1 room controller (rights), 2 group member, 3 group admin, 4 room
/// owner, 5 moderator.
/// </param>
/// <param name="RightsKnown">
/// Whether the user's rights are known, which is the case for the owner or once a rights level has
/// arrived. While this is <see langword="false"/>, a <paramref name="HasRights"/> of
/// <see langword="false"/> only means "not confirmed yet".
/// </param>
/// <param name="HasRights">Whether the user owns the room or has a rights level above 0.</param>
/// <param name="IsSpectating">Whether the user is spectating, or <see langword="null"/> when the server has not said.</param>
/// <param name="IsRoomMuted">Whether the room is muted for everyone, or <see langword="null"/> before the room details arrive.</param>
/// <param name="CanMute">Whether the user may mute others in the room, or <see langword="null"/> before the room details arrive.</param>
/// <param name="WhoCanMute">Who may mute other users, or <see langword="null"/> before the room details arrive.</param>
/// <param name="WhoCanKick">Who may kick other users, or <see langword="null"/> before the room details arrive.</param>
/// <param name="WhoCanBan">Who may ban other users, or <see langword="null"/> before the room details arrive.</param>
public sealed record RoomAuthorityState(
    bool IsOwner,
    int? RightsLevel,
    bool RightsKnown,
    bool HasRights,
    bool? IsSpectating,
    bool? IsRoomMuted,
    bool? CanMute,
    RoomModerationPermission? WhoCanMute,
    RoomModerationPermission? WhoCanKick,
    RoomModerationPermission? WhoCanBan);

/// <summary>Specifies what ended a room session.</summary>
public enum RoomExitSource
{
    /// <summary>A new room session that replaced the current one.</summary>
    RoomTransition = 0,
    /// <summary>A close of the room connection sent by the server.</summary>
    ConnectionClosed = 1,
    /// <summary>A quit room request sent by the client.</summary>
    ClientQuit = 3,
    /// <summary>A close of the hotel connection.</summary>
    Disconnected = 4,
    /// <summary>A failed attempt to access the room.</summary>
    AccessFailure = 5,
    /// <summary>A removal of the user's own avatar, which is no longer produced.</summary>
    /// <remarks>
    /// The client does not end a room session when the user's own avatar is removed:
    /// <c>onUserRemove</c> only disposes the avatar and <c>RoomUsersHandler.onUserRemove</c> only
    /// drops the user data, and the hotel follows such a removal with an explicit close or with a new
    /// room delivery. The member is kept so the numbering of the enum stays stable.
    /// </remarks>
    SelfRemoved = 6,
    /// <summary>A kick of the user by the room owner or staff.</summary>
    /// <remarks>
    /// Flash reports the kick through <c>GenericErrorEnum.KICKED_BY_OWNER</c> (4008), which
    /// <c>GenericErrorHandler</c> turns into <c>RSEME_KICKED</c>. The session itself still ends
    /// through a <c>CloseConnection</c> or a removal of the user's own avatar, so this value classifies
    /// an exit instead of replacing the source that carried it. It is reported by
    /// <see cref="RoomExitState.Cause"/>, never by <see cref="RoomExitState.Source"/>.
    /// </remarks>
    Kicked = 7
}

/// <summary>Represents how a room session ended.</summary>
/// <param name="RoomId">The room that was left.</param>
/// <param name="WasEntered">Whether the room had been fully entered.</param>
/// <param name="Source">The source that ended the room session.</param>
/// <param name="Reason">The reason code the server sent with the close, or <see langword="null"/> when it sent none.</param>
/// <param name="Kick">The kick that caused the exit, or <see langword="null"/> when the user was not kicked.</param>
public sealed record RoomExitState(
    Id RoomId,
    bool WasEntered,
    RoomExitSource Source,
    short? Reason,
    RoomKick? Kick = null)
{
    /// <summary>Gets whether the user was kicked out of the room.</summary>
    public bool WasKicked => Kick is not null;

    /// <summary>Gets why the room session ended.</summary>
    /// <remarks>
    /// The value is <see cref="RoomExitSource.Kicked"/> when a kick caused the exit, otherwise
    /// <see cref="Source"/>.
    /// </remarks>
    public RoomExitSource Cause => WasKicked ? RoomExitSource.Kicked : Source;
}
