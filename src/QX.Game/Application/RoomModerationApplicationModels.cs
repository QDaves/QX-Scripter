using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read a page of the current room's ban list.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationState"/>. Only the current ban list state can be
/// read, so a continuation page fails once the state has changed since the first page.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first ban to return.</param>
/// <param name="Limit">The maximum number of bans to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The ban list revision the page must be read from, or <see langword="null"/> to read the current state.
/// Required when <paramref name="Offset"/> is greater than 0.
/// </param>
public sealed record RoomModerationStateRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a request to fetch the ban list of the current room from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationRefresh"/>. The room must be ready, and the
/// refresh fails when the hotel session or the room changes before the ban list is received.
/// </remarks>
/// <param name="Limit">The maximum number of bans in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the ban list in milliseconds, from 1 to 120000. The request is sent at most twice
/// within this time.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation of the ban list state the refresh must run in, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomId">The id the current room must have, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomModerationRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to moderate one user in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationKick"/> and
/// <see cref="ApplicationMemberIds.RoomModerationBounce"/>. The room must be ready, the local profile must be
/// loaded, and the target must be a user in the room other than the local user.
/// </remarks>
/// <param name="UserId">The id of the user to moderate.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation of the ban list state the action must run in, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomId">The id the current room must have, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedUserIndex">
/// The room index the target must hold, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomModerationTargetRequest(
    Id UserId,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

/// <summary>
/// Represents a request to mute one user in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationMute"/>. The room must be ready, the local profile
/// must be loaded, and the target must be a user in the room other than the local user.
/// </remarks>
/// <param name="UserId">The id of the user to mute.</param>
/// <param name="Minutes">The mute duration in minutes, from 0 to 1440, where 0 removes the mute.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation of the ban list state the action must run in, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomId">The id the current room must have, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedUserIndex">
/// The room index the target must hold, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomModerationMuteRequest(
    Id UserId,
    int Minutes,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

/// <summary>
/// Represents a request to ban one user from the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationBan"/>. The room must be ready, the local profile
/// must be loaded, and the target must be a user in the room other than the local user. The loaded ban list
/// is cleared after the ban is sent.
/// </remarks>
/// <param name="UserId">The id of the user to ban.</param>
/// <param name="Length">The duration of the ban.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation of the ban list state the action must run in, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomId">The id the current room must have, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the current room must have, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedUserIndex">
/// The room index the target must hold, or <see langword="null"/> to skip the check.
/// </param>
public sealed record RoomModerationBanRequest(
    Id UserId,
    BanLength Length,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

/// <summary>
/// Represents a request to lift a user's ban from a room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomModerationUnban"/>. The room does not have to be the current
/// room. When it is the current ready room, the loaded ban list is cleared after the unban is sent.
/// </remarks>
/// <param name="UserId">The id of the banned user.</param>
/// <param name="RoomId">The id of the room to lift the ban from.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation of the ban list state the action must run in, or <see langword="null"/> to skip the check.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the room must have, or <see langword="null"/> to skip the check. Requires
/// <paramref name="RoomId"/> to be the current ready room.
/// </param>
/// <param name="ExpectedSnapshotRevision">
/// The ban list revision that must still be current, or <see langword="null"/> to skip the check. Requires
/// <paramref name="RoomId"/> to be the current ready room.
/// </param>
public sealed record RoomModerationUnbanRequest(
    Id UserId,
    Id RoomId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null,
    long? ExpectedSnapshotRevision = null);

/// <summary>
/// Represents one user on a room's ban list.
/// </summary>
/// <param name="UserId">The id of the banned user.</param>
/// <param name="Name">The name of the banned user.</param>
public sealed record RoomBanView(Id UserId, string Name);

/// <summary>
/// Represents a page of a room's ban list.
/// </summary>
/// <param name="SnapshotRevision">The ban list revision the page was read from, passed back to read the next page.</param>
/// <param name="TotalBans">The number of bans in the whole list.</param>
/// <param name="Offset">The zero-based offset of the first ban in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Bans">The bans in the page.</param>
public sealed record RoomBanPage(
    long SnapshotRevision,
    int TotalBans,
    int Offset,
    int? NextOffset,
    IReadOnlyList<RoomBanView> Bans);

/// <summary>
/// Represents the room moderation state with one page of the ban list.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomModerationState"/> and
/// <see cref="ApplicationMemberIds.RoomModerationRefresh"/>.
/// </remarks>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The session generation of the ban list state, increased when the hotel session changes.</param>
/// <param name="Revision">The ban list state revision, increased by every committed change.</param>
/// <param name="RoomGeneration">The generation of the room the ban list is bound to.</param>
/// <param name="RoomId">The id of the room the ban list is bound to, or 0 when no room is active.</param>
/// <param name="RoomReady">Whether the room the ban list is bound to is the current ready room.</param>
/// <param name="Loaded">
/// Whether a ban list was received for the room and has not been cleared since by a ban or unban.
/// </param>
/// <param name="BanList">The requested page of the ban list.</param>
public sealed record RoomModerationStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long RoomGeneration,
    Id RoomId,
    bool RoomReady,
    bool Loaded,
    RoomBanPage BanList);

/// <summary>
/// Represents the result of sending a room moderation action to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomModerationMute"/>, <see cref="ApplicationMemberIds.RoomModerationKick"/>,
/// <see cref="ApplicationMemberIds.RoomModerationBan"/>, <see cref="ApplicationMemberIds.RoomModerationUnban"/> and
/// <see cref="ApplicationMemberIds.RoomModerationBounce"/>. The hotel's reaction is not awaited.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the result was created after the messages were sent.</param>
/// <param name="SessionGeneration">The session generation of the ban list state the action ran in.</param>
/// <param name="StateRevision">The ban list state revision checked when the message was sent.</param>
/// <param name="RoomId">The id of the room the action targeted.</param>
/// <param name="RoomGeneration">
/// The generation of the room, or <see langword="null"/> for an unban in a room that is not the current ready room.
/// </param>
/// <param name="UserId">The id of the target user.</param>
/// <param name="UserIndex">The room index of the target user, or <see langword="null"/> for an unban.</param>
/// <param name="MessagesDispatched">The number of messages sent, which is 2 for a bounce and 1 otherwise.</param>
public sealed record RoomModerationDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    Id UserId,
    int? UserIndex,
    int MessagesDispatched);

/// <summary>
/// Specifies the kind of change reported by <see cref="RoomModerationChanged"/>.
/// </summary>
public enum RoomModerationChangeKind
{
    /// <summary>A ban list was received for the current room.</summary>
    Refreshed,
    /// <summary>The hotel reported that a user was unbanned, and the user was removed from the loaded list.</summary>
    UserUnbanned,
    /// <summary>The loaded ban list was cleared after a ban or unban was sent.</summary>
    Invalidated,
    /// <summary>The ban list was cleared because a room was entered or left.</summary>
    RoomChanged,
    /// <summary>The state was cleared because the manager was attached, reset or bound to a new hotel session.</summary>
    Reset
}

/// <summary>
/// Represents a summary of the room moderation state.
/// </summary>
/// <param name="Connected">Whether the ban list state is bound to a hotel session.</param>
/// <param name="SessionGeneration">The session generation of the ban list state, increased when the hotel session changes.</param>
/// <param name="Revision">The ban list state revision, increased by every committed change.</param>
/// <param name="RoomGeneration">The generation of the room the ban list is bound to.</param>
/// <param name="RoomId">The id of the room the ban list is bound to, or 0 when no room is active.</param>
/// <param name="Loaded">
/// Whether a ban list was received for the room and has not been cleared since by a ban or unban.
/// </param>
/// <param name="TotalBans">The number of bans in the loaded list.</param>
public sealed record RoomModerationStateSummary(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long RoomGeneration,
    Id RoomId,
    bool Loaded,
    int TotalBans);

/// <summary>
/// Represents a change of the room moderation state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.RoomModerationChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="State">The summary of the state after the change.</param>
/// <param name="UserId">
/// The id of the unbanned user for a <see cref="RoomModerationChangeKind.UserUnbanned"/> change, otherwise
/// <see langword="null"/>.
/// </param>
public sealed record RoomModerationChanged(
    RoomModerationChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    RoomModerationStateSummary State,
    Id? UserId);
