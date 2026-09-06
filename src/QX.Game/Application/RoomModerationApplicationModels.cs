using Qx.Model;

namespace Qx.Game.Application;

public sealed record RoomModerationStateRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record RoomModerationRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null);

public sealed record RoomModerationTargetRequest(
    Id UserId,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

public sealed record RoomModerationMuteRequest(
    Id UserId,
    int Minutes,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

public sealed record RoomModerationBanRequest(
    Id UserId,
    BanLength Length,
    long? ExpectedSessionGeneration = null,
    Id? ExpectedRoomId = null,
    long? ExpectedRoomGeneration = null,
    int? ExpectedUserIndex = null);

public sealed record RoomModerationUnbanRequest(
    Id UserId,
    Id RoomId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null,
    long? ExpectedSnapshotRevision = null);

public sealed record RoomBanView(Id UserId, string Name);

public sealed record RoomBanPage(
    long SnapshotRevision,
    int TotalBans,
    int Offset,
    int? NextOffset,
    IReadOnlyList<RoomBanView> Bans);

public sealed record RoomModerationStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long RoomGeneration,
    Id RoomId,
    bool RoomReady,
    bool Loaded,
    RoomBanPage BanList);

public sealed record RoomModerationDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long StateRevision,
    Id RoomId,
    long? RoomGeneration,
    Id UserId,
    int? UserIndex,
    int MessagesDispatched);

public enum RoomModerationChangeKind
{
    Refreshed,
    UserUnbanned,
    Invalidated,
    RoomChanged,
    Reset
}

public sealed record RoomModerationStateSummary(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long RoomGeneration,
    Id RoomId,
    bool Loaded,
    int TotalBans);

public sealed record RoomModerationChanged(
    RoomModerationChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    RoomModerationStateSummary State,
    Id? UserId);
