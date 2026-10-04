using Qx.Interception;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the state of one leaderboard route.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.LeaderboardsState"/>. Up to four snapshots are retained,
/// and a retained snapshot becomes unavailable when the hotel session changes.
/// </remarks>
/// <param name="Scope">The players the board ranks.</param>
/// <param name="Weekly">Whether to read the weekly board instead of the all-time board.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.
/// </param>
public sealed record LeaderboardStateRequest(
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a summary of the board stored for one leaderboard route.
/// </summary>
/// <param name="Loaded">Whether a board was received for the route.</param>
/// <param name="EntryCount">The number of entries in the stored window.</param>
/// <param name="TotalListSize">The number of entries on the whole board as reported by the hotel.</param>
/// <param name="GameTypeId">The id of the game type the board belongs to, or 0 when no board was received.</param>
/// <param name="HasMoreAbove">Whether the board has ranks above the stored window.</param>
/// <param name="HasMoreBelow">Whether the board has ranks below the stored window.</param>
public sealed record LeaderboardSummary(
    bool Loaded,
    int EntryCount,
    int TotalListSize,
    int GameTypeId,
    bool HasMoreAbove,
    bool HasMoreBelow);

/// <summary>
/// Represents one entry of a stored leaderboard window.
/// </summary>
/// <param name="Ordinal">The zero-based position of the entry in the stored window.</param>
/// <param name="UserId">The id of the player.</param>
/// <param name="Score">The player's score.</param>
/// <param name="Rank">The player's rank, counted from one.</param>
/// <param name="Name">The player's name.</param>
/// <param name="Figure">The player's figure string.</param>
/// <param name="Gender">The player's gender.</param>
public sealed record LeaderboardEntryView(
    int Ordinal,
    int UserId,
    int Score,
    int Rank,
    string Name,
    string Figure,
    string Gender);

/// <summary>
/// Represents the week covered by the last weekly leaderboard received.
/// </summary>
/// <param name="Year">The year the week belongs to.</param>
/// <param name="Week">The week number.</param>
/// <param name="MaxOffset">The oldest week offset the hotel keeps, counted back from the current week.</param>
/// <param name="CurrentOffset">The number of weeks back from the current week the board covers.</param>
/// <param name="MinutesUntilReset">The number of minutes until the current week ends.</param>
/// <param name="IsCurrentWeek">Whether the board covers the current week.</param>
public sealed record LeaderboardPeriodView(
    int Year,
    int Week,
    int MaxOffset,
    int CurrentOffset,
    int MinutesUntilReset,
    bool IsCurrentWeek);

/// <summary>
/// Represents the state of one leaderboard route read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.LeaderboardsState"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="Revision">
/// The leaderboard state revision, increased when a board is stored, the week offset is set or the state resets.
/// </param>
/// <param name="BoardsRevision">The revision increased when a board is stored or the state resets.</param>
/// <param name="SettingsRevision">The revision increased when the week offset is set or the state resets.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, used to read the same snapshot again.</param>
/// <param name="Scope">The players the board ranks.</param>
/// <param name="Weekly">Whether the weekly route was read.</param>
/// <param name="Board">The summary of the board stored for the route.</param>
/// <param name="Period">
/// The week covered by the last weekly board received on any route, or <see langword="null"/> when none was received.
/// </param>
/// <param name="FavouriteGroupId">
/// The id of the local user's favorite group reported by the last group board, or 0 when none was received.
/// </param>
/// <param name="WeekOffset">
/// The number of weeks back weekly requests use, set locally and updated from each weekly board received.
/// </param>
/// <param name="ViewSize">The view size sent with each leaderboard request.</param>
/// <param name="WindowSize">The window size sent with each leaderboard request.</param>
public sealed record LeaderboardStateView(
    bool Connected,
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

/// <summary>
/// Represents a request to read a page of entries of one leaderboard route from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.LeaderboardsEntriesList"/>. Entries are returned in the
/// order the hotel sent them.
/// </remarks>
/// <param name="Scope">The players the board ranks.</param>
/// <param name="Weekly">Whether to read the weekly board instead of the all-time board.</param>
/// <param name="Offset">The zero-based offset of the first entry to return.</param>
/// <param name="Limit">The maximum number of entries to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.
/// Required when <paramref name="Offset"/> is greater than 0.
/// </param>
public sealed record LeaderboardEntryPageRequest(
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a page of entries of one leaderboard route read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.LeaderboardsEntriesList"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="StateRevision">The leaderboard state revision of the snapshot.</param>
/// <param name="BoardsRevision">The revision increased when a board is stored or the state resets.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="Scope">The players the board ranks.</param>
/// <param name="Weekly">Whether the weekly route was read.</param>
/// <param name="Board">The summary of the board stored for the route.</param>
/// <param name="Total">The number of entries in the stored window.</param>
/// <param name="Offset">The zero-based offset of the first entry in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Entries">The entries in the page.</param>
public sealed record LeaderboardEntryPage(
    bool Connected,
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

/// <summary>
/// Represents a request to fetch a leaderboard route from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.LeaderboardsRefresh"/>. The refresh waits until earlier
/// requests for the route are answered, sends one request and completes with the first new board for
/// the route that matches the game type and, for weekly boards, the week offset. The response does not
/// echo the start rank or direction. Weekly requests use the stored week offset.
/// </remarks>
/// <param name="GameTypeId">The id of the game type.</param>
/// <param name="Scope">The players the board ranks.</param>
/// <param name="Weekly">Whether to request the weekly board instead of the all-time board.</param>
/// <param name="StartRank">The rank the requested window starts from, or -1 for the window around the local user.</param>
/// <param name="Direction">The paging direction, 0 to walk down the board or 1 to walk up.</param>
/// <param name="Limit">The maximum number of entries in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The total time to wait for the response in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record LeaderboardRefreshRequest(
    int GameTypeId,
    LeaderboardScope Scope = LeaderboardScope.Total,
    bool Weekly = false,
    int StartRank = -1,
    int Direction = 0,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of a leaderboard refresh.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.LeaderboardsRefresh"/>.
/// </remarks>
/// <param name="RefreshedAtUtc">The time the result was created.</param>
/// <param name="ObservedAtUtc">The time the matching board was received.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The leaderboard state revision after the board was stored.</param>
/// <param name="BoardsRevision">The boards revision after the board was stored.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the first page was read from.</param>
/// <param name="MessagesDispatched">The number of request messages sent.</param>
/// <param name="FirstPage">The first page of entries of the received board.</param>
public sealed record LeaderboardRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long BoardsRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    LeaderboardEntryPage FirstPage);

/// <summary>
/// Represents a request to set how many weeks back weekly leaderboard requests look.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.LeaderboardsWeekOffsetSet"/>. The value is capped at the
/// maximum offset of the last weekly board received, and no request is sent to the hotel.
/// </remarks>
/// <param name="Offset">The number of weeks back from the current week. Must not be negative.</param>
public sealed record LeaderboardWeekOffsetRequest(int Offset);

/// <summary>
/// Represents the result of setting the leaderboard week offset.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.LeaderboardsWeekOffsetSet"/>.
/// </remarks>
/// <param name="RequestedOffset">The week offset that was requested.</param>
/// <param name="EffectiveOffset">The week offset in effect after the change was applied.</param>
/// <param name="StateRevision">The leaderboard state revision after the change.</param>
/// <param name="SettingsRevision">The settings revision after the change.</param>
/// <param name="SnapshotRevision">The revision of the snapshot retained after the change.</param>
public sealed record LeaderboardWeekOffsetResult(
    int RequestedOffset,
    int EffectiveOffset,
    long StateRevision,
    long SettingsRevision,
    long SnapshotRevision);

/// <summary>
/// Specifies the kind of change reported by <see cref="LeaderboardChanged"/>.
/// </summary>
public enum LeaderboardChangeKind
{
    /// <summary>A board was received and stored.</summary>
    Snapshot,
    /// <summary>The week offset was set.</summary>
    Settings,
    /// <summary>The state was cleared because the hotel session changed.</summary>
    Reset
}

/// <summary>
/// Represents a change of the leaderboard state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.LeaderboardsChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The state generation of the hotel session.</param>
/// <param name="Revision">The leaderboard state revision after the change.</param>
/// <param name="SourceRevision">
/// The settings revision for a <see cref="LeaderboardChangeKind.Settings"/> change, otherwise the boards revision.
/// </param>
/// <param name="SnapshotRevision">
/// The revision of the snapshot retained for the change, or <see langword="null"/> when none could be stored.
/// </param>
/// <param name="Scope">
/// The players the stored board ranks, or <see langword="null"/> when the change is not a
/// <see cref="LeaderboardChangeKind.Snapshot"/>.
/// </param>
/// <param name="Weekly">
/// Whether the stored board is weekly, or <see langword="null"/> when the change is not a
/// <see cref="LeaderboardChangeKind.Snapshot"/>.
/// </param>
/// <param name="Board">
/// The summary of the stored board, or <see langword="null"/> when the change is not a
/// <see cref="LeaderboardChangeKind.Snapshot"/>.
/// </param>
/// <param name="Period">The week covered by the last weekly board received, or <see langword="null"/> when none was received.</param>
/// <param name="WeekOffset">The number of weeks back weekly requests use.</param>
public sealed record LeaderboardChanged(
    LeaderboardChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    LeaderboardScope? Scope,
    bool? Weekly,
    LeaderboardSummary? Board,
    LeaderboardPeriodView? Period,
    int WeekOffset);
