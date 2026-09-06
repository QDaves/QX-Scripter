using Qx.Interception;

namespace Qx.Game.Application;

public sealed record LeaderboardStateRequest(
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    long? SnapshotRevision = null);

public sealed record LeaderboardSummary(
    bool Loaded,
    int EntryCount,
    int TotalListSize,
    int GameTypeId,
    bool HasMoreAbove,
    bool HasMoreBelow);

public sealed record LeaderboardEntryView(
    int Ordinal,
    int UserId,
    int Score,
    int Rank,
    string Name,
    string Figure,
    string Gender);

public sealed record LeaderboardPeriodView(
    int Year,
    int Week,
    int MaxOffset,
    int CurrentOffset,
    int MinutesUntilReset,
    bool IsCurrentWeek);

public sealed record LeaderboardStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long BoardsRevision,
    long SettingsRevision,
    long SnapshotRevision,
    LeaderboardScope Scope,
    bool Weekly,
    LeaderboardSummary Board,
    LeaderboardPeriodView? Period,
    int FavouriteGroupId,
    int WeekOffset,
    int ViewSize,
    int WindowSize);

public sealed record LeaderboardEntryPageRequest(
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record LeaderboardEntryPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long BoardsRevision,
    long SnapshotRevision,
    LeaderboardScope Scope,
    bool Weekly,
    LeaderboardSummary Board,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<LeaderboardEntryView> Entries);

public sealed record LeaderboardRefreshRequest(
    int GameTypeId,
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    int StartRank = -1,
    int Direction = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record LeaderboardRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long BoardsRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    LeaderboardEntryPage FirstPage);

public sealed record LeaderboardWeekOffsetRequest(int Offset);

public sealed record LeaderboardWeekOffsetResult(
    int RequestedOffset,
    int EffectiveOffset,
    long StateRevision,
    long SettingsRevision,
    long SnapshotRevision);

public enum LeaderboardChangeKind
{
    Snapshot,
    Settings,
    Reset
}

public sealed record LeaderboardChanged(
    LeaderboardChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    LeaderboardScope? Scope,
    bool? Weekly,
    LeaderboardSummary? Board,
    LeaderboardPeriodView? Period,
    int WeekOffset);
