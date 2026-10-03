using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>Represents a request for the daily task state view.</summary>
/// <remarks>
/// Used by the <c>daily_tasks.state</c> query. The application keeps the four most recent daily
/// task snapshots of the active hotel session, and a retained snapshot can no longer be read once it
/// is dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record DailyTaskStateRequest(long? SnapshotRevision = null);

/// <summary>Represents a summary of the daily task list.</summary>
/// <param name="Loaded">Whether the server has sent the task list in the current session.</param>
/// <param name="Total">The number of running tasks.</param>
/// <param name="Claimable">The number of tasks that are finished and whose reward has not been claimed.</param>
/// <param name="HasBonus">Whether the server has granted a bonus task.</param>
public sealed record DailyTaskSummary(
    bool Loaded,
    int Total,
    int Claimable,
    bool HasBonus);

/// <summary>Represents one item handed out for finishing a daily task.</summary>
/// <param name="ProductItemTypeId">The item type of the product.</param>
/// <param name="RewardTypeId">The reward category, which decides how the client draws it.</param>
/// <param name="ExtraParams">The reward specific detail, such as a badge code or a furniture class.</param>
/// <param name="Amount">The number of items given.</param>
public sealed record DailyTaskRewardView(
    short ProductItemTypeId,
    string RewardTypeId,
    string ExtraParams,
    int Amount);

/// <summary>Represents a daily task and the user's progress in it.</summary>
/// <param name="Ordinal">The zero-based position of the task in the snapshot.</param>
/// <param name="TaskId">The task id.</param>
/// <param name="TaskCode">The task code, which keys its localized name.</param>
/// <param name="QuestTypeCode">The underlying quest type, shared with the quest system.</param>
/// <param name="IsBonus">Whether the task is the bonus task.</param>
/// <param name="ImageVersion">The version suffix of the task artwork.</param>
/// <param name="CatalogName">The catalog page the task points to, or an empty string when it points nowhere.</param>
/// <param name="RequiredRepeats">The number of repeats that finish the task.</param>
/// <param name="Repeats">The number of repeats done.</param>
/// <param name="Status">The task status, 0 for in progress, 1 for completed and waiting to be claimed, and 2 for claimed.</param>
/// <param name="SecondsLeftAtArrival">The lifetime left when the server sent the task, in seconds. A negative value means the server considers it expired.</param>
/// <param name="ReceivedAt">The time the task was received.</param>
/// <param name="Rewards">The rewards for finishing the task.</param>
/// <param name="IsClaimable">Whether the task is finished and its reward has not been claimed.</param>
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

/// <summary>Represents the daily task state read from one snapshot.</summary>
/// <remarks>Returned by the <c>daily_tasks.state</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The daily task state revision, which increases with every change.</param>
/// <param name="TasksRevision">The revision of the task list, which increases whenever tasks are received, added, updated or cleared.</param>
/// <param name="BaselineRevision">The revision of the full task list, which increases each time the server sends the complete list.</param>
/// <param name="AddedRevision">The revision of task additions, which increases each time the server adds tasks or the list is cleared.</param>
/// <param name="UpdateRevision">The revision of task updates, which increases each time the progress or status of a task changes or the list is cleared.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Summary">The summary of the task list.</param>
public sealed record DailyTaskStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long TasksRevision,
    long BaselineRevision,
    long AddedRevision,
    long UpdateRevision,
    long SnapshotRevision,
    DailyTaskSummary Summary);

/// <summary>Represents a request for a page of daily tasks.</summary>
/// <remarks>Used by the <c>daily_tasks.entries.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first task to return.</param>
/// <param name="Limit">The maximum number of tasks to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record DailyTaskPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of daily tasks read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>daily_tasks.entries.list</c> query. The ordinary tasks come first and the
/// bonus task last.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The daily task state revision, which increases with every change.</param>
/// <param name="TasksRevision">The revision of the task list, which increases whenever tasks are received, added, updated or cleared.</param>
/// <param name="BaselineRevision">The revision of the full task list, which increases each time the server sends the complete list.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Summary">The summary of the task list.</param>
/// <param name="Total">The number of tasks in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first task in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more tasks.</param>
/// <param name="Tasks">The tasks in the page.</param>
public sealed record DailyTaskPage(
    bool Connected,
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

/// <summary>Represents a request to reload the daily task list from the server.</summary>
/// <remarks>
/// Used by the <c>daily_tasks.refresh</c> operation. The call waits until earlier list requests have
/// been answered, sends one request and returns the first full task list received after it. The
/// response carries no request id, so the timeout covers the wait for earlier requests too.
/// </remarks>
/// <param name="Limit">The maximum number of tasks in the first page of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record DailyTaskRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a daily task refresh.</summary>
/// <remarks>Returned by the <c>daily_tasks.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the task list was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The daily task state revision after the list was received.</param>
/// <param name="TasksRevision">The revision of the task list after it was received.</param>
/// <param name="BaselineRevision">The revision of the full task list that was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="FirstPage">The first page of tasks from the refreshed snapshot.</param>
public sealed record DailyTaskRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long TasksRevision,
    long BaselineRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    DailyTaskPage FirstPage);

/// <summary>Represents a request to claim the reward of a daily task.</summary>
/// <remarks>
/// Used by the <c>daily_tasks.claim</c> operation. The operation sends one claim and returns
/// without waiting, and the task update that follows is published as a <see cref="DailyTaskChanged"/>.
/// </remarks>
/// <param name="TaskId">The id of the task. The Flash client sends only its low 32 bits.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record DailyTaskClaimActionRequest(
    long TaskId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the receipt for a daily task claim that was sent.</summary>
/// <remarks>Returned by the <c>daily_tasks.claim</c> operation.</remarks>
/// <param name="DispatchedAtUtc">The UTC time the claim was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the claim was sent in.</param>
/// <param name="TaskId">The id of the task that was claimed.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record DailyTaskClaimDispatchReceipt(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long TaskId,
    int MessagesDispatched);

/// <summary>Specifies the kind of a daily task change.</summary>
public enum DailyTaskChangeKind
{
    /// <summary>The full task list was received.</summary>
    Snapshot,
    /// <summary>The server added tasks to the running set.</summary>
    Added,
    /// <summary>The progress or status of a task changed without completing or claiming it.</summary>
    Updated,
    /// <summary>A task changed to the completed status and its reward can be claimed.</summary>
    Completed,
    /// <summary>A task changed to the claimed status.</summary>
    Claimed,
    /// <summary>The daily task state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change to the daily task state.</summary>
/// <remarks>Published by the <c>daily_tasks.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The daily task state revision after the change.</param>
/// <param name="SourceRevision">The task list revision after the change, or <paramref name="Revision"/> for <see cref="DailyTaskChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the change, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="Summary">The summary of the task list, or <see langword="null"/> for <see cref="DailyTaskChangeKind.Reset"/>.</param>
/// <param name="TaskId">The id of the changed task for <see cref="DailyTaskChangeKind.Updated"/>, <see cref="DailyTaskChangeKind.Completed"/> and <see cref="DailyTaskChangeKind.Claimed"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Status">The new status of the changed task, 0 for in progress, 1 for completed and 2 for claimed, or <see langword="null"/> when no single task changed.</param>
/// <param name="Repeats">The number of repeats done on the changed task, or <see langword="null"/> when no single task changed.</param>
public sealed record DailyTaskChanged(
    DailyTaskChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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
