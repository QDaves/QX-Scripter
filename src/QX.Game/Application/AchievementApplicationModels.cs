using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>Represents a request for the achievement state view.</summary>
/// <remarks>
/// Used by the <c>achievements.state</c> query. The application keeps the four most recent
/// achievement snapshots of the active hotel session, and a retained snapshot can no longer be read
/// once it is dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record AchievementStateRequest(
    long? SnapshotRevision = null);

/// <summary>Represents a summary of the achievement list.</summary>
/// <param name="Loaded">Whether the achievement list has been received in the current session.</param>
/// <param name="DefaultCategory">The category the client opens first, as sent with the achievement list.</param>
/// <param name="Total">The number of known achievements, including those the client does not list.</param>
/// <param name="Completed">The number of achievements at their final level.</param>
/// <param name="Progress">The number of levels achieved across the listed achievements.</param>
/// <param name="MaxProgress">The total number of levels across the listed achievements.</param>
/// <param name="Completion">The fraction of levels achieved, from 0 to 1, or 0 when <paramref name="MaxProgress"/> is 0.</param>
public sealed record AchievementListSummary(
    bool Loaded,
    string DefaultCategory,
    int Total,
    int Completed,
    int Progress,
    int MaxProgress,
    double Completion);

/// <summary>Represents an achievement and the user's progress in it.</summary>
/// <param name="Id">The achievement id.</param>
/// <param name="Level">The level in progress, or the final level once <paramref name="IsComplete"/> is <see langword="true"/>.</param>
/// <param name="BadgeCode">The badge code of <paramref name="Level"/>, such as <c>ACH_RoomEntry5</c>.</param>
/// <param name="BaseProgress">The progress at which <paramref name="Level"/> starts.</param>
/// <param name="MaxProgress">The progress <paramref name="Level"/> requires.</param>
/// <param name="LevelRewardPoints">The number of activity points <paramref name="Level"/> rewards.</param>
/// <param name="LevelRewardPointType">The activity point type of the level reward.</param>
/// <param name="CurrentProgress">The user's current progress.</param>
/// <param name="IsComplete">Whether the achievement is at its final level.</param>
/// <param name="Category">The category the achievement is filed under.</param>
/// <param name="Subcategory">The subcategory the achievement is filed under.</param>
/// <param name="MaxLevel">The total number of levels.</param>
/// <param name="DisplayMethod">How the client draws the progress, 0 for a progress bar and 1 for none.</param>
/// <param name="State">The listing state sent by the hotel, 0 for normal, 2 for archived and 4 for hidden.</param>
/// <param name="IsNew">Whether the achievement's code is marked as new.</param>
public sealed record AchievementApplicationItem(
    int Id,
    int Level,
    string BadgeCode,
    int BaseProgress,
    int MaxProgress,
    int LevelRewardPoints,
    int LevelRewardPointType,
    int CurrentProgress,
    bool IsComplete,
    string Category,
    string Subcategory,
    int MaxLevel,
    int DisplayMethod,
    short State,
    bool IsNew);

/// <summary>Represents the achievement state read from one snapshot.</summary>
/// <remarks>Returned by the <c>achievements.state</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The achievement state revision, which increases with every change.</param>
/// <param name="ListRevision">The revision of the achievement list, which increases when the list is received, updated or cleared.</param>
/// <param name="BaselineRevision">The revision of the full achievement list, which increases each time the server sends the complete list.</param>
/// <param name="ScoreRevision">The revision of the achievement score, which increases when the score is received or cleared.</param>
/// <param name="PointLimitsRevision">The revision of the badge point limits, which increases when they are received or cleared.</param>
/// <param name="NewCodesRevision">The revision of the new achievement codes, which increases when they are set or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="List">The summary of the achievement list.</param>
/// <param name="ScoreLoaded">Whether the achievement score has been received in the current session.</param>
/// <param name="Score">The achievement score, or <see langword="null"/> when it has not been received.</param>
/// <param name="PointLimitsLoaded">Whether the badge point limits have been received in the current session.</param>
/// <param name="PointLimitCount">The number of badge point limits in the snapshot.</param>
/// <param name="NewCodeCount">The number of achievement codes marked as new.</param>
public sealed record AchievementStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long ListRevision,
    long BaselineRevision,
    long ScoreRevision,
    long PointLimitsRevision,
    long NewCodesRevision,
    long SnapshotRevision,
    AchievementListSummary List,
    bool ScoreLoaded,
    int? Score,
    bool PointLimitsLoaded,
    int PointLimitCount,
    int NewCodeCount);

/// <summary>Represents a request for a page of achievements.</summary>
/// <remarks>Used by the <c>achievements.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first achievement to return.</param>
/// <param name="Limit">The maximum number of achievements to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record AchievementPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of achievements read from one snapshot.</summary>
/// <remarks>Returned by the <c>achievements.list</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The achievement state revision, which increases with every change.</param>
/// <param name="ListRevision">The revision of the achievement list, which increases when the list is received, updated or cleared.</param>
/// <param name="BaselineRevision">The revision of the full achievement list, which increases each time the server sends the complete list.</param>
/// <param name="NewCodesRevision">The revision of the new achievement codes, which increases when they are set or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the achievement list has been received in the current session.</param>
/// <param name="DefaultCategory">The category the client opens first, as sent with the achievement list.</param>
/// <param name="Total">The number of achievements in the snapshot.</param>
/// <param name="Completed">The number of achievements in the snapshot at their final level.</param>
/// <param name="Offset">The zero-based index of the first achievement in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more achievements.</param>
/// <param name="Achievements">The achievements in the page, in the order the server sent them.</param>
public sealed record AchievementPage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long ListRevision,
    long BaselineRevision,
    long NewCodesRevision,
    long SnapshotRevision,
    bool Loaded,
    string DefaultCategory,
    int Total,
    int Completed,
    int Offset,
    int? NextOffset,
    IReadOnlyList<AchievementApplicationItem> Achievements);

/// <summary>Represents the points one level of an achievement requires.</summary>
/// <param name="AchievementCode">The achievement code, without the badge prefix or level.</param>
/// <param name="Level">The achievement level.</param>
/// <param name="Limit">The point total the level requires.</param>
/// <param name="BadgeCode">The badge code of the level, such as <c>ACH_RoomEntry5</c>.</param>
public sealed record AchievementPointLimitItem(
    string AchievementCode,
    int Level,
    int Limit,
    string BadgeCode);

/// <summary>Represents a request for a page of badge point limits.</summary>
/// <remarks>Used by the <c>achievements.point_limits.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first point limit to return.</param>
/// <param name="Limit">The maximum number of point limits to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record AchievementPointLimitPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of badge point limits read from one achievement snapshot.</summary>
/// <remarks>
/// Returned by the <c>achievements.point_limits.list</c> query. The limits are ordered by
/// achievement code, then level, then badge code.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The achievement state revision, which increases with every change.</param>
/// <param name="PointLimitsRevision">The revision of the badge point limits, which increases when they are received or cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Loaded">Whether the badge point limits have been received in the current session.</param>
/// <param name="Total">The number of point limits in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first point limit in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more point limits.</param>
/// <param name="Limits">The point limits in the page.</param>
public sealed record AchievementPointLimitPage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long PointLimitsRevision,
    long SnapshotRevision,
    bool Loaded,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<AchievementPointLimitItem> Limits);

/// <summary>Represents a request to reload the achievement list from the server.</summary>
/// <remarks>
/// Used by the <c>achievements.refresh</c> operation. Concurrent callers in the same hotel session
/// share one request and wait for a new full achievement list. Timing out or canceling ends only
/// the caller's own wait.
/// </remarks>
/// <param name="Limit">The maximum number of achievements in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record AchievementRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of an achievement list refresh.</summary>
/// <remarks>Returned by the <c>achievements.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the new achievement list was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The achievement state revision after the list was received.</param>
/// <param name="ListRevision">The revision of the achievement list after it was received.</param>
/// <param name="BaselineRevision">The revision of the full achievement list that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent, 0 when it joined a request already in progress.</param>
/// <param name="FirstPage">The first page of achievements from the refreshed snapshot.</param>
public sealed record AchievementRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long ListRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    AchievementPage FirstPage);

/// <summary>Represents a request to reload the badge point limits from the server.</summary>
/// <remarks>
/// Used by the <c>achievements.point_limits.refresh</c> operation. Concurrent callers in the same
/// hotel session share one request and wait for a new point limit response. Timing out or
/// canceling ends only the caller's own wait.
/// </remarks>
/// <param name="Limit">The maximum number of point limits in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record AchievementPointLimitsRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a badge point limit refresh.</summary>
/// <remarks>Returned by the <c>achievements.point_limits.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the new point limits were observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The achievement state revision after the point limits were received.</param>
/// <param name="PointLimitsRevision">The revision of the badge point limits that were received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent, 0 when it joined a request already in progress.</param>
/// <param name="FirstPage">The first page of point limits from the refreshed snapshot.</param>
public sealed record AchievementPointLimitsRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long PointLimitsRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    AchievementPointLimitPage FirstPage);

/// <summary>Specifies the kind of an achievement change.</summary>
public enum AchievementChangeKind
{
    /// <summary>The full achievement list was received.</summary>
    Snapshot,
    /// <summary>A single achievement was added or updated.</summary>
    Updated,
    /// <summary>The achievement score was received.</summary>
    Score,
    /// <summary>The badge point limits were received.</summary>
    PointLimits,
    /// <summary>The achievement codes marked as new were set.</summary>
    NewCodes,
    /// <summary>The achievement state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the achievement state.</summary>
/// <remarks>Published by the <c>achievements.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The achievement state revision after the change.</param>
/// <param name="SourceRevision">The revision of the part that changed: the list revision for <see cref="AchievementChangeKind.Snapshot"/> and <see cref="AchievementChangeKind.Updated"/>, the score, point limit or new code revision for those kinds, and <paramref name="Revision"/> for <see cref="AchievementChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the change, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="List">The summary of the achievement list for <see cref="AchievementChangeKind.Snapshot"/>, <see cref="AchievementChangeKind.Updated"/> and <see cref="AchievementChangeKind.NewCodes"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Achievement">The achievement after an <see cref="AchievementChangeKind.Updated"/> change; otherwise, <see langword="null"/>.</param>
/// <param name="PreviousAchievement">The achievement before an <see cref="AchievementChangeKind.Updated"/> change, or <see langword="null"/> when it was not known before.</param>
/// <param name="ScoreLoaded">Whether the achievement score has been received in the current session.</param>
/// <param name="Score">The achievement score, or <see langword="null"/> when it has not been received.</param>
/// <param name="PointLimitsLoaded">Whether the badge point limits have been received in the current session.</param>
/// <param name="PointLimitCount">The number of badge point limits for a <see cref="AchievementChangeKind.PointLimits"/> change; otherwise, <see langword="null"/>.</param>
/// <param name="NewCodeCount">The number of achievement codes marked as new for a <see cref="AchievementChangeKind.NewCodes"/> change; otherwise, <see langword="null"/>.</param>
public sealed record AchievementChanged(
    AchievementChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    AchievementListSummary? List,
    AchievementApplicationItem? Achievement,
    AchievementApplicationItem? PreviousAchievement,
    bool ScoreLoaded,
    int? Score,
    bool PointLimitsLoaded,
    int? PointLimitCount,
    int? NewCodeCount);

/// <summary>Represents a request for the badge state view.</summary>
/// <remarks>
/// Used by the <c>badges.state</c> query. The application keeps the four most recent badge
/// snapshots of the active hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record BadgeStateRequest(
    long? SnapshotRevision = null);

/// <summary>Represents a summary of the badge inventory and its load state.</summary>
/// <remarks>
/// The badge inventory arrives in fragments. The owned badges are replaced only once every fragment
/// of a load has been received.
/// </remarks>
/// <param name="Loaded">Whether the complete badge inventory has been received in the current session.</param>
/// <param name="Loading">Whether a badge inventory request is pending or its fragments are still arriving.</param>
/// <param name="Stale">Whether the owned badges include badges that a completed load has not confirmed yet.</param>
/// <param name="RecoveryPending">Whether a completed load was discarded because it could not be matched to the active request, and the inventory waits for a later complete load.</param>
/// <param name="LoadGeneration">The load generation, which increases each time a new inventory load begins or the state is cleared.</param>
/// <param name="ExpectedFragments">The number of fragments the current load expects, or -1 when it is not known yet.</param>
/// <param name="ReceivedFragments">The number of fragments received in the current load.</param>
/// <param name="OwnedCount">The number of badges the user owns.</param>
/// <param name="SelectedSetCount">The number of users whose selected badges are retained.</param>
/// <param name="SelectedBadgeCount">The total number of selected badges across the retained users.</param>
/// <param name="RetiredRequestEpoch">The epoch of the expired request whose fragments completed the discarded load, or <see langword="null"/> when <paramref name="RecoveryPending"/> is <see langword="false"/>.</param>
/// <param name="ActiveRequestEpoch">The epoch of the request that was active when the load was discarded, or <see langword="null"/> when there is none.</param>
public sealed record BadgeInventorySummary(
    bool Loaded,
    bool Loading,
    bool Stale,
    bool RecoveryPending,
    long LoadGeneration,
    int ExpectedFragments,
    int ReceivedFragments,
    int OwnedCount,
    int SelectedSetCount,
    int SelectedBadgeCount,
    long? RetiredRequestEpoch,
    long? ActiveRequestEpoch);

/// <summary>Represents the badge state read from one snapshot.</summary>
/// <remarks>Returned by the <c>badges.state</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The badge state revision, which increases with every change.</param>
/// <param name="InventoryRevision">The revision of the owned badges and their load state.</param>
/// <param name="BaselineRevision">The revision of the complete badge inventory, which increases each time a load completes.</param>
/// <param name="SelectedRevision">The revision of the selected badge sets, which increases each time a user's selected badges are received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Inventory">The summary of the badge inventory.</param>
public sealed record BadgeStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long InventoryRevision,
    long BaselineRevision,
    long SelectedRevision,
    long SnapshotRevision,
    BadgeInventorySummary Inventory);

/// <summary>Represents a request for a page of the badges the user owns.</summary>
/// <remarks>Used by the <c>badges.owned.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first badge to return.</param>
/// <param name="Limit">The maximum number of badges to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record OwnedBadgePageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the badges the user owns, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>badges.owned.list</c> query. The badges keep the order of the inventory
/// fragments, followed by badges received during the session.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The badge state revision, which increases with every change.</param>
/// <param name="InventoryRevision">The revision of the owned badges and their load state.</param>
/// <param name="BaselineRevision">The revision of the complete badge inventory, which increases each time a load completes.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Inventory">The summary of the badge inventory.</param>
/// <param name="Total">The number of owned badges in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first badge in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more badges.</param>
/// <param name="Badges">The owned badges in the page.</param>
public sealed record OwnedBadgePage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long InventoryRevision,
    long BaselineRevision,
    long SnapshotRevision,
    BadgeInventorySummary Inventory,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<OwnedBadgeSnapshot> Badges);

/// <summary>Represents a summary of the selected badges of one user.</summary>
/// <param name="UserId">The id of the user.</param>
/// <param name="BadgeCount">The number of badges the user has selected.</param>
/// <param name="Revision">The selected badge set revision at which the user's selection was last received.</param>
public sealed record BadgeSelectedSetSummary(
    Id UserId,
    int BadgeCount,
    long Revision);

/// <summary>Represents a request for a page of the retained selected badge sets.</summary>
/// <remarks>Used by the <c>badges.selected_sets.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first set to return.</param>
/// <param name="Limit">The maximum number of sets to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record BadgeSelectedSetPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the selected badge sets received in the session, read from one snapshot.</summary>
/// <remarks>Returned by the <c>badges.selected_sets.list</c> query. The sets are ordered by user id.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The badge state revision, which increases with every change.</param>
/// <param name="SelectedRevision">The revision of the selected badge sets, which increases each time a user's selected badges are received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Total">The number of selected badge sets in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first set in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more sets.</param>
/// <param name="Sets">The selected badge sets in the page.</param>
public sealed record BadgeSelectedSetPage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long SelectedRevision,
    long SnapshotRevision,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<BadgeSelectedSetSummary> Sets);

/// <summary>Represents a badge a user has selected to wear.</summary>
/// <param name="Slot">The one-based slot the badge is shown in.</param>
/// <param name="Code">The badge code.</param>
/// <param name="OwnerCount">The number of users who own the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="RarityId">The rarity of the badge, or 0 when <paramref name="HasRarityData"/> is <see langword="false"/>.</param>
/// <param name="HasRarityData">Whether the hotel sent the owner count and rarity for the badge.</param>
public sealed record SelectedBadgeSnapshot(
    int Slot,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);

/// <summary>Represents a request for a page of the selected badges of one user.</summary>
/// <remarks>Used by the <c>badges.selected.list</c> query.</remarks>
/// <param name="UserId">The id of the user whose selected badges to read.</param>
/// <param name="Offset">The zero-based index of the first badge to return.</param>
/// <param name="Limit">The maximum number of badges to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record BadgeSelectedPageRequest(
    Id UserId,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of the selected badges of one user, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>badges.selected.list</c> query. The badges keep the order in which the
/// server sent them.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The badge state revision, which increases with every change.</param>
/// <param name="SelectedRevision">The revision of the selected badge sets, which increases each time a user's selected badges are received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="UserId">The id of the user.</param>
/// <param name="Found">Whether the snapshot holds a selected badge set for the user.</param>
/// <param name="Total">The number of badges the user has selected, or 0 when <paramref name="Found"/> is <see langword="false"/>.</param>
/// <param name="Offset">The zero-based index of the first badge in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more badges.</param>
/// <param name="Badges">The selected badges in the page.</param>
public sealed record BadgeSelectedPage(
    bool Connected,
    long SessionGeneration,
    long StateRevision,
    long SelectedRevision,
    long SnapshotRevision,
    Id UserId,
    bool Found,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<SelectedBadgeSnapshot> Badges);

/// <summary>Represents a request to reload the badge inventory from the server.</summary>
/// <remarks>
/// Used by the <c>badges.refresh</c> operation. Concurrent callers in the same hotel session share
/// one request and wait until every fragment of the new inventory has arrived. Timing out or
/// canceling ends only the caller's own wait. A shared request that has had no waiters and no
/// response for 30 seconds is retired before a new one is sent.
/// </remarks>
/// <param name="Limit">The maximum number of badges in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record BadgeRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a badge inventory refresh.</summary>
/// <remarks>Returned by the <c>badges.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the complete badge inventory was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The badge state revision after the inventory was received.</param>
/// <param name="InventoryRevision">The revision of the owned badges after the inventory was received.</param>
/// <param name="BaselineRevision">The revision of the complete badge inventory that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent, 0 when it joined a request already in progress.</param>
/// <param name="FirstPage">The first page of owned badges from the refreshed snapshot.</param>
public sealed record BadgeRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long InventoryRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    OwnedBadgePage FirstPage);

/// <summary>Specifies the kind of a badge change.</summary>
public enum BadgeChangeKind
{
    /// <summary>A badge inventory request was sent or an inventory fragment was received.</summary>
    Loading,
    /// <summary>Every fragment of the badge inventory was received and the owned badges were replaced.</summary>
    Loaded,
    /// <summary>The user received a badge they did not own.</summary>
    Added,
    /// <summary>The server sent a badge the user already owns again.</summary>
    Updated,
    /// <summary>A badge was removed from the user's badges, usually because an achievement level replaced it.</summary>
    Removed,
    /// <summary>The selected badges of a user were received.</summary>
    Selected,
    /// <summary>A completed inventory load could not be matched to the active request and was discarded.</summary>
    CorrelationFailed,
    /// <summary>The badge state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the badge state.</summary>
/// <remarks>
/// Published by the <c>badges.changed</c> event. A message that adds, updates or removes several
/// badges publishes one change for each badge.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The badge state revision after the change.</param>
/// <param name="SourceRevision">The selected badge set revision for <see cref="BadgeChangeKind.Selected"/>, otherwise the inventory revision.</param>
/// <param name="LoadGeneration">The badge inventory load generation.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the change, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="Inventory">The summary of the badge inventory, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="Badge">The badge for <see cref="BadgeChangeKind.Added"/>, <see cref="BadgeChangeKind.Updated"/> and <see cref="BadgeChangeKind.Removed"/>; otherwise, <see langword="null"/>.</param>
/// <param name="SelectedSet">The user's selected badge set for <see cref="BadgeChangeKind.Selected"/>; otherwise, <see langword="null"/>.</param>
/// <param name="RetiredRequestEpoch">The epoch of the expired request whose fragments completed a discarded load, or <see langword="null"/> when no load is waiting to recover.</param>
/// <param name="ActiveRequestEpoch">The epoch of the request that was active when the load was discarded, or <see langword="null"/> when there is none.</param>
public sealed record BadgeChanged(
    BadgeChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long LoadGeneration,
    long? SnapshotRevision,
    BadgeInventorySummary? Inventory,
    OwnedBadgeSnapshot? Badge,
    BadgeSelectedSetSummary? SelectedSet,
    long? RetiredRequestEpoch,
    long? ActiveRequestEpoch);

internal interface IAchievementOperations
{
    void RequestAchievements();
    void RequestPointLimits();
    Task<IReadOnlyList<Achievement>> EnsureAchievementsLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
    Task<BadgePointLimits> EnsurePointLimitsLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
}

internal interface IBadgeInventoryOperations
{
    Task<IReadOnlyCollection<OwnedBadge>> EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
}
