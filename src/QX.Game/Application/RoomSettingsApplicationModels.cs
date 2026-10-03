using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the cached settings of one room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomSettingsState"/>. No message is sent. Settings are cached
/// for up to 64 rooms per hotel session, and the cache is cleared when the hotel session changes.
/// </remarks>
/// <param name="RoomId">The id of the room. Must be positive.</param>
public sealed record RoomSettingsStateRequest(Id RoomId);

/// <summary>
/// Represents a request to load the settings of one room from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomSettingsGet"/>. Settings operations on the same room run
/// one at a time. The request is sent at most twice, the second time after a pause of up to 150
/// milliseconds when the first attempt got no answer. The hotel answers only for rooms the local user
/// may edit, and a rejection throws <see cref="RoomSettingsRejectedException"/>.
/// </remarks>
/// <param name="RoomId">The id of the room. Must be positive.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait in milliseconds, including the wait for earlier operations on the room and the
/// retry, from 1 to 120000.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the request must run in, or <see langword="null"/> to use the active session.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The generation the active room must have, or <see langword="null"/> to skip the check. When set, the
/// request fails unless <paramref name="RoomId"/> is the active room.
/// </param>
public sealed record RoomSettingsGetRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to save the settings of one room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.RoomSettingsSave"/>. The complete settings are sent once and
/// the call waits for the hotel to confirm or reject the save, and a rejection throws
/// <see cref="RoomSettingsRejectedException"/>. Settings operations on the same room run one at a time.
/// Sending the save clears the cached settings of the room.
/// </remarks>
/// <param name="Settings">The complete settings to save. Its <see cref="RoomSettingsValues.RoomId"/> selects the room.</param>
/// <param name="Password">
/// The door password. An empty value clears an existing password. Limited to 65535 UTF-8 bytes.
/// </param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait in milliseconds, including the wait for earlier operations on the room, from 1
/// to 120000.
/// </param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the save must run in, or <see langword="null"/> to use the active session.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The generation the active room must have, or <see langword="null"/> to skip the check. When set, the
/// save fails unless the room is the active room.
/// </param>
/// <param name="ExpectedOperationRevision">
/// The operation revision the room's cached settings must still have, or <see langword="null"/> to skip
/// the check. Must be positive.
/// </param>
/// <param name="ExpectedSnapshotRevision">
/// The snapshot revision the room's cached settings must still have, or <see langword="null"/> to skip
/// the check. Must be positive.
/// </param>
public sealed record RoomSettingsSaveRequest(
    RoomSettingsValues Settings,
    string Password = "",
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null,
    long? ExpectedOperationRevision = null,
    long? ExpectedSnapshotRevision = null);

/// <summary>
/// Represents the editable settings of a room.
/// </summary>
/// <remarks>
/// When saving, every enum value must be a defined member, every string is limited to 65535 UTF-8 bytes
/// and each list holds at most 500 entries.
/// </remarks>
/// <param name="RoomId">The id of the room. Must be positive and fit in a 32-bit integer.</param>
/// <param name="Name">The room name.</param>
/// <param name="Description">The room description.</param>
/// <param name="DoorMode">Who may enter the room.</param>
/// <param name="CategoryId">The id of the navigator category the room is listed in. Must not be negative.</param>
/// <param name="MaximumVisitors">The maximum number of visitors. Must not be negative.</param>
/// <param name="Tags">The room's search tags.</param>
/// <param name="TradeMode">Who may trade in the room.</param>
/// <param name="AllowPets">Whether pets are allowed in the room.</param>
/// <param name="AllowFoodConsume">Whether pets may eat food in the room.</param>
/// <param name="AllowWalkThrough">Whether avatars can walk through each other.</param>
/// <param name="HideWalls">Whether the room walls are hidden.</param>
/// <param name="WallThickness">The wall thickness.</param>
/// <param name="FloorThickness">The floor thickness.</param>
/// <param name="ChatFloodSensitivity">How strictly the room silences repeated or rapid chat.</param>
/// <param name="LeaveOnDoorTile">Whether avatars leave the room when they step on the door tile.</param>
/// <param name="IdleSleepEnabled">Whether idle avatars fall asleep.</param>
/// <param name="IdleSleepTimeoutSeconds">The idle time in seconds before an avatar falls asleep. Must not be negative.</param>
/// <param name="IdleAutokickEnabled">Whether idle avatars are kicked from the room.</param>
/// <param name="IdleAutokickTimeoutSeconds">The idle time in seconds before an avatar is kicked. Must not be negative.</param>
/// <param name="MuteAllPets">Whether every pet in the room is muted.</param>
/// <param name="WhoCanMute">Who may mute users in the room.</param>
/// <param name="WhoCanKick">Who may kick users from the room.</param>
/// <param name="WhoCanBan">Who may ban users from the room.</param>
/// <param name="NftGroupIds">
/// The NFT group ids of the room. The Flash settings messages do not carry them, so the list is empty
/// when read and is not sent when saved.
/// </param>
public sealed record RoomSettingsValues(
    Id RoomId,
    string Name,
    string Description,
    RoomDoorMode DoorMode,
    int CategoryId,
    int MaximumVisitors,
    IReadOnlyList<string> Tags,
    RoomTradeMode TradeMode,
    bool AllowPets,
    bool AllowFoodConsume,
    bool AllowWalkThrough,
    bool HideWalls,
    RoomThickness WallThickness,
    RoomThickness FloorThickness,
    RoomChatFloodSensitivity ChatFloodSensitivity,
    bool LeaveOnDoorTile,
    bool IdleSleepEnabled,
    int IdleSleepTimeoutSeconds,
    bool IdleAutokickEnabled,
    int IdleAutokickTimeoutSeconds,
    bool MuteAllPets,
    RoomModerationPermission WhoCanMute,
    RoomModerationPermission WhoCanKick,
    RoomModerationPermission WhoCanBan,
    IReadOnlyList<Id> NftGroupIds);

/// <summary>
/// Represents the read-only settings metadata of a room.
/// </summary>
/// <remarks>
/// The Flash settings message carries only <paramref name="MaximumVisitorsLimit"/> and
/// <paramref name="HiddenByBuildersClub"/>, so the other values are 0 or <see langword="false"/>.
/// </remarks>
/// <param name="MaximumVisitorsLimit">The highest visitor limit the hotel allows for the room.</param>
/// <param name="MaximumVisitorsLowerLimit">The lowest visitor limit the hotel allows for the room.</param>
/// <param name="HiddenByBuildersClub">Whether the hotel reports the room as hidden by Builders Club.</param>
/// <param name="IsGroupRoom">Whether the room belongs to a group.</param>
/// <param name="GroupRightsPolicy">The group rights policy code sent by the hotel.</param>
/// <param name="RequiresBuildersClub">Whether the room requires a Builders Club membership.</param>
/// <param name="IsHabboXDemoRoom">Whether the room is a HabboX demo room.</param>
public sealed record RoomSettingsMetadata(
    int MaximumVisitorsLimit,
    int MaximumVisitorsLowerLimit,
    bool HiddenByBuildersClub,
    bool IsGroupRoom,
    int GroupRightsPolicy,
    bool RequiresBuildersClub,
    bool IsHabboXDemoRoom);

/// <summary>
/// Represents the cached settings state of one room.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomSettingsState"/> and
/// <see cref="ApplicationMemberIds.RoomSettingsGet"/>. Sending a save clears the cached settings, so
/// <paramref name="Loaded"/> is <see langword="false"/> until the settings are loaded again.
/// </remarks>
/// <param name="Connected">Whether the settings state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session, increased each time the session changes.</param>
/// <param name="StateRevision">The room settings state revision, increased by every committed change.</param>
/// <param name="RoomId">The id of the room that was read.</param>
/// <param name="RoomGeneration">
/// The generation of the active room the cached settings were captured in, or <see langword="null"/> when
/// they were not captured while <paramref name="RoomId"/> was active or that room generation is no longer
/// active.
/// </param>
/// <param name="OperationRevision">
/// The state revision at which the room's cache entry last changed, or 0 when none is cached or
/// <paramref name="Connected"/> is <see langword="false"/>.
/// </param>
/// <param name="SnapshotRevision">
/// The revision of the room's cached settings, increased when settings are received, rejected or cleared,
/// or 0 when none is cached or <paramref name="Connected"/> is <see langword="false"/>.
/// </param>
/// <param name="Loaded">Whether settings are cached for the room in the active hotel session.</param>
/// <param name="Settings">The editable settings, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="Metadata">The read-only metadata, or <see langword="null"/> when <paramref name="Loaded"/> is <see langword="false"/>.</param>
public sealed record RoomSettingsStateView(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision,
    bool Loaded,
    RoomSettingsValues? Settings,
    RoomSettingsMetadata? Metadata);

/// <summary>
/// Represents the hotel's confirmation of a room settings save.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.RoomSettingsSave"/>.
/// </remarks>
/// <param name="SavedAtUtc">The time the receipt was created after the confirmation was received.</param>
/// <param name="SessionGeneration">The generation of the hotel session the save ran in.</param>
/// <param name="StateRevision">The room settings state revision after the confirmation was committed.</param>
/// <param name="RoomId">The id of the room.</param>
/// <param name="RoomGeneration">
/// The generation of the active room the save was bound to, or <see langword="null"/> when the room was
/// not the active room.
/// </param>
/// <param name="OperationRevision">The operation revision of the room's cache entry after the confirmation.</param>
/// <param name="SnapshotRevision">The snapshot revision of the room's cache entry after the confirmation.</param>
public sealed record RoomSettingsSaveReceipt(
    DateTimeOffset SavedAtUtc,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision);

/// <summary>
/// Specifies a room settings operation.
/// </summary>
public enum RoomSettingsOperationKind
{
    /// <summary>Loading the settings of a room.</summary>
    Get,
    /// <summary>Saving the settings of a room.</summary>
    Save
}

/// <summary>
/// Thrown when the hotel rejects loading or saving the settings of a room.
/// </summary>
public sealed class RoomSettingsRejectedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoomSettingsRejectedException"/> class.
    /// </summary>
    /// <param name="operation">The operation the hotel rejected.</param>
    /// <param name="roomId">The id of the room.</param>
    /// <param name="errorCode">The error code sent by the hotel.</param>
    /// <param name="info">The error text sent by the hotel, or <see langword="null"/> when none was sent.</param>
    public RoomSettingsRejectedException(
        RoomSettingsOperationKind operation,
        Id roomId,
        int errorCode,
        string? info = null)
        : base(info is { Length: > 0 }
            ? $"Room settings {operation.ToString().ToLowerInvariant()} failed for room {roomId} with error {errorCode}: {info}"
            : $"Room settings {operation.ToString().ToLowerInvariant()} failed for room {roomId} with error {errorCode}.")
    {
        Operation = operation;
        RoomId = roomId;
        ErrorCode = errorCode;
        Info = info;
    }

    /// <summary>Gets the operation the hotel rejected.</summary>
    public RoomSettingsOperationKind Operation { get; }
    /// <summary>Gets the id of the room.</summary>
    public Id RoomId { get; }
    /// <summary>Gets the error code sent by the hotel, or -1 when the rejection carried none.</summary>
    public int ErrorCode { get; }
    /// <summary>Gets the error text sent by the hotel, or <see langword="null"/> when none was sent.</summary>
    /// <remarks>Only save rejections carry text.</remarks>
    public string? Info { get; }
}

/// <summary>
/// Specifies the kind of change reported by <see cref="RoomSettingsChanged"/>.
/// </summary>
public enum RoomSettingsChangeKind
{
    /// <summary>Settings for a room were received and cached.</summary>
    Refreshed,
    /// <summary>The hotel rejected a settings request, or the received settings exceeded the 500 entry list limit.</summary>
    GetRejected,
    /// <summary>A settings save was sent for a room and its cached settings were cleared.</summary>
    Invalidated,
    /// <summary>The hotel confirmed a settings save.</summary>
    Saved,
    /// <summary>The hotel rejected a settings save.</summary>
    SaveRejected,
    /// <summary>The active room changed.</summary>
    RoomChanged,
    /// <summary>The cached settings were cleared because the state was reset or a hotel session connected.</summary>
    Reset
}

/// <summary>
/// Represents a change of the room settings state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.RoomSettingsChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session, increased each time the session changes.</param>
/// <param name="StateRevision">The room settings state revision after the change.</param>
/// <param name="RoomId">
/// The id of the room the change applies to. For <see cref="RoomSettingsChangeKind.RoomChanged"/> and
/// <see cref="RoomSettingsChangeKind.Reset"/> it is the active room, or 0 when no room is active.
/// </param>
/// <param name="RoomGeneration">
/// The active room generation recorded with the room's cache entry, or <see langword="null"/> when the
/// room was not active or the change has no cache entry.
/// </param>
/// <param name="OperationRevision">The operation revision of the room's cache entry, or 0 when the change has no cache entry.</param>
/// <param name="SnapshotRevision">The snapshot revision of the room's cache entry, or 0 when the change has no cache entry.</param>
/// <param name="Loaded">Whether the room's cache entry holds settings after the change.</param>
/// <param name="ErrorCode">The error code sent by the hotel for a rejection, or <see langword="null"/> when none applies.</param>
/// <param name="ErrorInfo">The error text of a rejection, or <see langword="null"/> when none applies.</param>
public sealed record RoomSettingsChanged(
    RoomSettingsChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision,
    bool Loaded,
    int? ErrorCode,
    string? ErrorInfo);
