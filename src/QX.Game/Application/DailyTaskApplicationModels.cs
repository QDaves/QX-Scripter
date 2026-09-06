using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record DailyTaskStateRequest(long? SnapshotRevision = null);

public sealed record DailyTaskSummary(
    bool Loaded,
    int Total,
    int Claimable,
    bool HasBonus);

public sealed record DailyTaskRewardView(
    short ProductItemTypeId,
    string RewardTypeId,
    string ExtraParams,
    int Amount);

public sealed record DailyTaskView(
    int Ordinal,
    long TaskId,
    string TaskCode,
    string QuestTypeCode,
    bool IsBonus,
    string ImageVersion,
    string CatalogName,
    int RequiredRepeats,
    int Repeats,
    int Status,
    int SecondsLeftAtArrival,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<DailyTaskRewardView> Rewards,
    bool IsClaimable);

public sealed record DailyTaskStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long TasksRevision,
    long BaselineRevision,
    long AddedRevision,
    long UpdateRevision,
    long SnapshotRevision,
    DailyTaskSummary Summary);

public sealed record DailyTaskPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record DailyTaskPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long TasksRevision,
    long BaselineRevision,
    long SnapshotRevision,
    DailyTaskSummary Summary,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<DailyTaskView> Tasks);

public sealed record DailyTaskRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record DailyTaskRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long TasksRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    DailyTaskPage FirstPage);

public sealed record DailyTaskClaimActionRequest(
    long TaskId,
    long? ExpectedSessionGeneration = null);

public sealed record DailyTaskClaimDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long TaskId,
    int MessagesDispatched);

public enum DailyTaskChangeKind
{
    Snapshot,
    Added,
    Updated,
    Completed,
    Claimed,
    Reset
}

public sealed record DailyTaskChanged(
    DailyTaskChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    DailyTaskSummary? Summary,
    long? TaskId,
    int? Status,
    int? Repeats);

internal interface IDailyTaskOperations
{
    bool Request();
    void Claim(long task_id);
    Task<IReadOnlyList<DailyTask>> EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token = default);
}
