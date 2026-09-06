using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomSettingsStateRequest(Id RoomId);

public sealed record RoomSettingsGetRequest(
    Id RoomId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomSettingsSaveRequest(
    RoomSettingsValues Settings,
    string Password = "",
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null,
    long? ExpectedOperationRevision = null,
    long? ExpectedSnapshotRevision = null);

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

public sealed record RoomSettingsMetadata(
    int MaximumVisitorsLimit,
    int MaximumVisitorsLowerLimit,
    bool HiddenByBuildersClub,
    bool IsGroupRoom,
    int GroupRightsPolicy,
    bool RequiresBuildersClub,
    bool IsHabboXDemoRoom);

public sealed record RoomSettingsStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision,
    bool Loaded,
    RoomSettingsValues? Settings,
    RoomSettingsMetadata? Metadata);

public sealed record RoomSettingsSaveReceipt(
    ClientType Client,
    DateTimeOffset SavedAtUtc,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision);

public enum RoomSettingsOperationKind
{
    Get,
    Save
}

public sealed class RoomSettingsRejectedException : InvalidOperationException
{
    public RoomSettingsRejectedException(
        RoomSettingsOperationKind operation,
        Id room_id,
        int error_code,
        string? info = null)
        : base(info is { Length: > 0 }
            ? $"Room settings {operation.ToString().ToLowerInvariant()} failed for room {room_id} with error {error_code}: {info}"
            : $"Room settings {operation.ToString().ToLowerInvariant()} failed for room {room_id} with error {error_code}.")
    {
        Operation = operation;
        RoomId = room_id;
        ErrorCode = error_code;
        Info = info;
    }

    public RoomSettingsOperationKind Operation { get; }
    public Id RoomId { get; }
    public int ErrorCode { get; }
    public string? Info { get; }
}

public enum RoomSettingsChangeKind
{
    Refreshed,
    GetRejected,
    Invalidated,
    Saved,
    SaveRejected,
    RoomChanged,
    Reset
}

public sealed record RoomSettingsChanged(
    RoomSettingsChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    long OperationRevision,
    long SnapshotRevision,
    bool Loaded,
    int? ErrorCode,
    string? ErrorInfo);
