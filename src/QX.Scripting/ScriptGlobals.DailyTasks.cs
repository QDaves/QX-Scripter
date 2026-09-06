using Qx.Game;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The daily tasks: the hotel's short repeatable goals, their progress and their rewards.
    /// </summary>
    /// <remarks>Flash only. <see cref="DailyTaskManager.IsSupported"/> reports whether it applies.</remarks>
    public DailyTaskManager DailyTasks => Game.DailyTasks;

    /// <summary>
    /// The running daily tasks, fetching them from the hotel on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<DailyTask>> GetDailyTasks(int timeoutMs = 10000) =>
        (await ReadDailyTaskSnapshot(timeoutMs).ConfigureAwait(false)).Tasks;

    /// <summary>
    /// The daily tasks that are finished and still owe a reward.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<DailyTask>> GetClaimableDailyTasks(int timeoutMs = 10000)
    {
        IReadOnlyList<DailyTask> tasks = await GetDailyTasks(timeoutMs);
        return tasks.Where(task => task.IsClaimable).ToArray();
    }

    /// <summary>Claims the reward for one finished daily task.</summary>
    /// <param name="taskId">The task to claim.</param>
    public void ClaimDailyTask(long taskId) => Game.DailyTasks.Claim(taskId);

    /// <summary>
    /// Claims every daily task that is finished and unclaimed.
    /// </summary>
    /// <remarks>
    /// The hotel answers each claim with its own update, so this returns as soon as the requests
    /// are away rather than waiting for the confirmations. Subscribe with
    /// <see cref="OnDailyTaskClaimed"/> to see them land.
    /// </remarks>
    /// <param name="timeoutMs">How long to wait for the task list.</param>
    /// <returns>How many claims were sent.</returns>
    public async Task<int> ClaimAllDailyTasks(int timeoutMs = 10000)
    {
        DailyTaskReadSnapshot snapshot = await ReadDailyTaskSnapshot(timeoutMs)
            .ConfigureAwait(false);
        DailyTask[] claimable = snapshot.Tasks.Where(task => task.IsClaimable).ToArray();
        foreach (DailyTask task in claimable)
        {
            DailyTaskClaimDispatchReceipt receipt = await Application
                .InvokeAsync<DailyTaskClaimActionRequest, DailyTaskClaimDispatchReceipt>(
                    ApplicationMemberIds.DailyTasksClaim,
                    new DailyTaskClaimActionRequest(
                        task.TaskId,
                        snapshot.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            if (receipt.SessionGeneration != snapshot.SessionGeneration ||
                receipt.TaskId != task.TaskId ||
                receipt.MessagesDispatched != 1)
            {
                throw new InvalidOperationException(
                    "The daily task application returned an invalid claim receipt.");
            }
        }
        return claimable.Length;
    }

    /// <summary>
    /// Asks the hotel to resend the daily task list.
    /// </summary>
    /// <returns>
    /// Whether a request went out. The client allows one per ten seconds and silently drops the
    /// rest, so this reports false when called again inside that window.
    /// </returns>
    public bool RefreshDailyTasks() => Game.DailyTasks.Request();

    /// <summary>Runs a callback whenever a daily task's progress or status changes.</summary>
    /// <param name="handler">Receives the task as it now stands.</param>
    public void OnDailyTaskUpdated(Action<DailyTask> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.DailyTasks.TaskUpdated += value,
            value => Game.DailyTasks.TaskUpdated -= value);
    }

    /// <summary>Runs a callback whenever a daily task becomes claimable.</summary>
    /// <param name="handler">Receives the finished task.</param>
    public void OnDailyTaskCompleted(Action<DailyTask> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.DailyTasks.TaskCompleted += value,
            value => Game.DailyTasks.TaskCompleted -= value);
    }

    /// <summary>Runs a callback whenever a daily task's reward is taken.</summary>
    /// <param name="handler">Receives the claimed task.</param>
    public void OnDailyTaskClaimed(Action<DailyTask> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.DailyTasks.TaskClaimed += value,
            value => Game.DailyTasks.TaskClaimed -= value);
    }

    private async Task<DailyTaskReadSnapshot> ReadDailyTaskSnapshot(int timeout_ms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout_ms, "timeoutMs");
        DailyTaskStateView state = await Application
            .InvokeAsync<DailyTaskStateRequest, DailyTaskStateView>(
                ApplicationMemberIds.DailyTasksState,
                new DailyTaskStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateDailyTaskState(state);

        DailyTaskPage first_page;
        if (state.Summary.Loaded)
        {
            first_page = await Application
                .InvokeAsync<DailyTaskPageRequest, DailyTaskPage>(
                    ApplicationMemberIds.DailyTasksEntriesList,
                    new DailyTaskPageRequest(
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateDailyTaskStatePage(state, first_page);
        }
        else
        {
            DailyTaskRefreshResult refreshed = await Application
                .InvokeAsync<DailyTaskRefreshRequest, DailyTaskRefreshResult>(
                    ApplicationMemberIds.DailyTasksRefresh,
                    new DailyTaskRefreshRequest(
                        Limit: 500,
                        TimeoutMilliseconds: timeout_ms,
                        ExpectedSessionGeneration: state.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            ValidateDailyTaskRefresh(refreshed, state.SessionGeneration);
            first_page = refreshed.FirstPage;
        }

        DailyTaskPage page = first_page;
        ValidateDailyTaskPage(first_page, page, 0);
        var tasks = new List<DailyTask>(page.Total);
        AddDailyTasks(page, tasks);
        while (page.NextOffset is int offset)
        {
            page = await Application
                .InvokeAsync<DailyTaskPageRequest, DailyTaskPage>(
                    ApplicationMemberIds.DailyTasksEntriesList,
                    new DailyTaskPageRequest(offset, 500, first_page.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateDailyTaskPage(first_page, page, offset);
            AddDailyTasks(page, tasks);
        }

        if (tasks.Count != first_page.Total)
        {
            throw new InvalidOperationException(
                "The daily task application returned an incomplete task list.");
        }
        DailyTask[] values = tasks.ToArray();
        ValidateDailyTaskSummary(first_page.Summary, values);
        return new DailyTaskReadSnapshot(
            first_page.SessionGeneration,
            Array.AsReadOnly(values));
    }

    private static void AddDailyTasks(DailyTaskPage page, List<DailyTask> tasks)
    {
        for (int index = 0; index < page.Tasks.Count; index++)
        {
            DailyTaskView task = page.Tasks[index];
            if (task.Ordinal != checked(page.Offset + index) ||
                task.TaskCode is null ||
                task.QuestTypeCode is null ||
                task.ImageVersion is null ||
                task.CatalogName is null)
            {
                throw new InvalidOperationException(
                    "The daily task application returned an invalid task entry.");
            }
            var rewards = new DailyTaskReward[task.Rewards.Count];
            for (int reward_index = 0; reward_index < rewards.Length; reward_index++)
            {
                DailyTaskRewardView reward = task.Rewards[reward_index];
                if (reward is null ||
                    reward.RewardTypeId is null ||
                    reward.ExtraParams is null)
                {
                    throw new InvalidOperationException(
                        "The daily task application returned an invalid reward entry.");
                }
                rewards[reward_index] = new DailyTaskReward(
                    reward.ProductItemTypeId,
                    reward.RewardTypeId,
                    reward.ExtraParams,
                    reward.Amount);
            }
            var value = new DailyTask(
                task.TaskId,
                task.TaskCode,
                task.QuestTypeCode,
                task.IsBonus,
                task.ImageVersion,
                task.CatalogName,
                task.RequiredRepeats,
                task.Repeats,
                (DailyTaskStatus)task.Status,
                task.SecondsLeftAtArrival,
                task.ReceivedAt,
                rewards);
            if (value.IsClaimable != task.IsClaimable)
            {
                throw new InvalidOperationException(
                    "The daily task application returned inconsistent task state.");
            }
            tasks.Add(value);
        }
    }

    private static void ValidateDailyTaskState(DailyTaskStateView state)
    {
        if (!state.Connected ||
            state.Client is not ClientType.Flash ||
            state.SessionGeneration <= 0 ||
            state.SnapshotRevision <= 0 ||
            state.Summary.Total < 0 ||
            state.Summary.Claimable < 0 ||
            state.Summary.Claimable > state.Summary.Total)
        {
            throw new InvalidOperationException(
                "The daily task application returned an invalid state snapshot.");
        }
    }

    private static void ValidateDailyTaskStatePage(
        DailyTaskStateView state,
        DailyTaskPage page)
    {
        if (page.Connected != state.Connected ||
            page.Client != state.Client ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.TasksRevision != state.TasksRevision ||
            page.BaselineRevision != state.BaselineRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Summary != state.Summary)
        {
            throw new InvalidOperationException(
                "The daily task application returned a page from another state snapshot.");
        }
    }

    private static void ValidateDailyTaskRefresh(
        DailyTaskRefreshResult refreshed,
        long expected_session_generation)
    {
        DailyTaskPage page = refreshed.FirstPage;
        if (refreshed.SnapshotRevision <= 0 ||
            refreshed.MessagesDispatched != 1 ||
            refreshed.SessionGeneration != expected_session_generation ||
            !page.Connected ||
            page.Client != refreshed.Client ||
            page.SessionGeneration != refreshed.SessionGeneration ||
            page.StateRevision != refreshed.StateRevision ||
            page.TasksRevision != refreshed.TasksRevision ||
            page.BaselineRevision != refreshed.BaselineRevision ||
            page.SnapshotRevision != refreshed.SnapshotRevision)
        {
            throw new InvalidOperationException(
                "The daily task application returned an invalid refresh result.");
        }
    }

    private static void ValidateDailyTaskPage(
        DailyTaskPage first_page,
        DailyTaskPage page,
        int offset)
    {
        int consumed = checked(offset + page.Tasks.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (first_page.SnapshotRevision <= 0 ||
            !first_page.Connected ||
            first_page.Client is not ClientType.Flash ||
            !first_page.Summary.Loaded ||
            page.Connected != first_page.Connected ||
            page.Client != first_page.Client ||
            page.SessionGeneration != first_page.SessionGeneration ||
            page.StateRevision != first_page.StateRevision ||
            page.TasksRevision != first_page.TasksRevision ||
            page.BaselineRevision != first_page.BaselineRevision ||
            page.SnapshotRevision != first_page.SnapshotRevision ||
            page.Summary != first_page.Summary ||
            page.Total < 0 ||
            page.Total != first_page.Total ||
            page.Summary.Total != page.Total ||
            page.Offset != offset ||
            page.Tasks.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Tasks.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException(
                "The daily task application returned an invalid snapshot page.");
        }
    }

    private static void ValidateDailyTaskSummary(
        DailyTaskSummary summary,
        IReadOnlyList<DailyTask> tasks)
    {
        if (!summary.Loaded ||
            summary.Total != tasks.Count ||
            summary.Claimable != tasks.Count(task => task.IsClaimable) ||
            summary.HasBonus != tasks.Any(task => task.IsBonus))
        {
            throw new InvalidOperationException(
                "The daily task application returned inconsistent task totals.");
        }
    }

    private sealed record DailyTaskReadSnapshot(
        long SessionGeneration,
        IReadOnlyList<DailyTask> Tasks);
}
