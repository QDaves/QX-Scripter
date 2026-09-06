using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record AchievementStateRequest(
    long? SnapshotRevision = null);

public sealed record AchievementListSummary(
    bool Loaded,
    string DefaultCategory,
    int Total,
    int Completed,
    int Progress,
    int MaxProgress,
    double Completion);

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

public sealed record AchievementStateView(
    bool Connected,
    ClientType? Client,
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

public sealed record AchievementPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record AchievementPage(
    bool Connected,
    ClientType? Client,
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

public sealed record AchievementPointLimitItem(
    string AchievementCode,
    int Level,
    int Limit,
    string BadgeCode);

public sealed record AchievementPointLimitPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record AchievementPointLimitPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long PointLimitsRevision,
    long SnapshotRevision,
    bool Loaded,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<AchievementPointLimitItem> Limits);

public sealed record AchievementRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record AchievementRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long ListRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    AchievementPage FirstPage);

public sealed record AchievementPointLimitsRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record AchievementPointLimitsRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long PointLimitsRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    AchievementPointLimitPage FirstPage);

public enum AchievementChangeKind
{
    Snapshot,
    Updated,
    Score,
    PointLimits,
    NewCodes,
    Reset
}

public sealed record AchievementChanged(
    AchievementChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
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

public sealed record BadgeStateRequest(
    long? SnapshotRevision = null);

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

public sealed record BadgeStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long InventoryRevision,
    long BaselineRevision,
    long SelectedRevision,
    long SnapshotRevision,
    BadgeInventorySummary Inventory);

public sealed record OwnedBadgePageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record OwnedBadgePage(
    bool Connected,
    ClientType? Client,
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

public sealed record BadgeSelectedSetSummary(
    Id UserId,
    int BadgeCount,
    long Revision);

public sealed record BadgeSelectedSetPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record BadgeSelectedSetPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long SelectedRevision,
    long SnapshotRevision,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<BadgeSelectedSetSummary> Sets);

public sealed record SelectedBadgeSnapshot(
    int Slot,
    string Code,
    int OwnerCount,
    int RarityId,
    bool HasRarityData);

public sealed record BadgeSelectedPageRequest(
    Id UserId,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record BadgeSelectedPage(
    bool Connected,
    ClientType? Client,
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

public sealed record BadgeRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record BadgeRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long InventoryRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    OwnedBadgePage FirstPage);

public enum BadgeChangeKind
{
    Loading,
    Loaded,
    Added,
    Updated,
    Removed,
    Selected,
    CorrelationFailed,
    Reset
}

public sealed record BadgeChanged(
    BadgeChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
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
