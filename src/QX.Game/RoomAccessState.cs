using Qx.Model.Messages.Incoming;

namespace Qx.Game;

public enum RoomAccessState
{
    Idle,
    Connecting,
    RingingDoorbell,
    Queued,
    Accessible,
    Denied,
    NotFound,
    ConnectionError
}

public sealed record RoomConnectionFailure(
    RoomConnectionFailureKind Kind,
    int ReasonCode,
    string Parameter);

public sealed record RoomAccessTransition(
    RoomAccessState PreviousState,
    RoomAccessState CurrentState,
    Id? PreviousRoomId,
    Id? CurrentRoomId,
    RoomConnectionFailure? Failure);

public sealed record RoomAuthorityState(
    bool IsOwner,
    int? RightsLevel,
    bool RightsKnown,
    bool HasRights,
    bool? IsSpectating);

public enum RoomExitSource
{
    RoomTransition = 0,
    ConnectionClosed = 1,
    NativeReason = 2,
    ClientQuit = 3,
    Disconnected = 4,
    AccessFailure = 5,
    /// <summary>
    /// No longer produced. A removal naming the local avatar used to end the session, which the
    /// client does not do: <c>onUserRemove</c> only disposes the avatar and
    /// <c>RoomUsersHandler.onUserRemove</c> only drops the user data, and the hotel follows a self
    /// removal either with an explicit close or with a fresh room delivery. The member is kept so
    /// the numbering of this enum stays stable.
    /// </summary>
    SelfRemoved = 6,
    /// <summary>
    /// The local user was kicked out by the room owner or staff. Flash raises this through
    /// <c>GenericErrorEnum.KICKED_BY_OWNER</c> (4008), which <c>GenericErrorHandler</c>
    /// turns into <c>RSEME_KICKED</c>; the teardown itself still arrives as a
    /// <c>CloseConnection</c> or a self <c>UserRemove</c>, so this value classifies an exit
    /// rather than replacing the transport that carried it.
    /// </summary>
    Kicked = 7
}

/// <param name="RoomId">The room that was left.</param>
/// <param name="WasEntered">Whether the room had been fully entered.</param>
/// <param name="Source">The transport that ended the room session.</param>
/// <param name="Reason">The reason code carried by the transport, when it carries one.</param>
/// <param name="HasNativeReason">Whether a native room exit reason accompanied the exit.</param>
/// <param name="Kick">The kick this exit consumed, or <see langword="null"/> when none was staged.</param>
public sealed record RoomExitState(
    Id RoomId,
    bool WasEntered,
    RoomExitSource Source,
    short? Reason,
    bool HasNativeReason,
    RoomKick? Kick = null)
{
    /// <summary>Whether the local user was kicked out of the room.</summary>
    public bool WasKicked => Kick is not null;

    /// <summary>
    /// Why the room session ended: <see cref="RoomExitSource.Kicked"/> when a kick was
    /// staged, otherwise the transport <see cref="Source"/>.
    /// </summary>
    public RoomExitSource Cause => WasKicked ? RoomExitSource.Kicked : Source;
}
