using Qx.Game;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the daily task manager, which tracks the hotel's short repeatable goals, their progress
    /// and their rewards.
    /// </summary>
    public DailyTaskManager DailyTasks => Game.DailyTasks;

    /// <summary>
    /// Gets the running daily tasks, requesting them from the hotel when they have not been
    /// loaded yet.
    /// </summary>
    /// <remarks>
    /// Once the task list is loaded, the cached copy is read without a request.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the task list request.</param>
    /// <returns>The running daily tasks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the task list changed while it was read.</exception>
    public async Task<IReadOnlyList<DailyTask>> GetDailyTasks(int timeoutMs = 10000) =>
        (await ReadDailyTaskSnapshot(timeoutMs).ConfigureAwait(false)).Tasks;

    /// <summary>
    /// Gets the daily tasks that are finished and still owe a reward.
    /// </summary>
    /// <remarks>
    /// The task list is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the task list request.</param>
    /// <returns>The claimable daily tasks.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the task list changed while it was read.</exception>
    public async Task<IReadOnlyList<DailyTask>> GetClaimableDailyTasks(int timeoutMs = 10000)
    {
        IReadOnlyList<DailyTask> tasks = await GetDailyTasks(timeoutMs);
        return tasks.Where(task => task.IsClaimable).ToArray();
    }

    /// <summary>Sends a claim for the reward of one finished daily task.</summary>
    /// <remarks>
    /// It returns without waiting for the answer; <see cref="OnDailyTaskClaimed"/> reports the
    /// claim once the hotel confirms it.
    /// </remarks>
    /// <param name="taskId">The id of the task to claim.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void ClaimDailyTask(long taskId) => Game.DailyTasks.Claim(taskId);

    /// <summary>
    /// Claims every daily task that is finished and unclaimed.
    /// </summary>
    /// <remarks>
    /// The hotel answers each claim with its own update, so this returns as soon as the requests
    /// are sent, one per claimable task, rather than waiting for the confirmations. Subscribe with
    /// <see cref="OnDailyTaskClaimed"/> to see them land. The task list is requested from the
    /// hotel first when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the task list request.</param>
    /// <returns>The number of claims that were sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, the session changed, or the task list changed while it was read.</exception>
    public async Task<int> ClaimAllDailyTasks(int timeoutMs = 10000)
    {
        DailyTaskReadSnapshot snapshot = await ReadDailyTaskSnapshot(timeoutMs)
            .ConfigureAwait(false);
        DailyTask[] claimable = snapshot.Tasks.Where(task => task.IsClaimable).ToArray();
        foreach (DailyTask task in claimable)
        {
            DailyTaskClaimDispatchReceipt receipt = await _application
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
    /// Sends a request for the hotel to resend the daily task list.
    /// </summary>
    /// <remarks>
    /// It returns without waiting for the answer. Only one request is sent per ten seconds; a
    /// call inside that window sends nothing.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> when a request was sent; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active.</exception>
    public bool RefreshDailyTasks() => Game.DailyTasks.Request();

    /// <summary>Registers a handler that runs whenever a daily task's progress or status changes.</summary>
    /// <param name="handler">The handler to call with the task as it now stands.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnDailyTaskUpdated(Action<DailyTask> handler)
        => Subscribe(
            handler,
            value => Game.DailyTasks.TaskUpdated += value,
            value => Game.DailyTasks.TaskUpdated -= value);

    /// <summary>Registers a handler that runs whenever a daily task becomes completed and claimable.</summary>
    /// <remarks>
    /// It runs after the <see cref="OnDailyTaskUpdated"/> handlers for the same update.
    /// </remarks>
    /// <param name="handler">The handler to call with the finished task.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnDailyTaskCompleted(Action<DailyTask> handler)
        => Subscribe(
            handler,
            value => Game.DailyTasks.TaskCompleted += value,
            value => Game.DailyTasks.TaskCompleted -= value);

    /// <summary>Registers a handler that runs whenever a daily task's reward is claimed.</summary>
    /// <remarks>
    /// It runs after the <see cref="OnDailyTaskUpdated"/> handlers for the same update.
    /// </remarks>
    /// <param name="handler">The handler to call with the claimed task.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnDailyTaskClaimed(Action<DailyTask> handler)
        => Subscribe(
            handler,
            value => Game.DailyTasks.TaskClaimed += value,
            value => Game.DailyTasks.TaskClaimed -= value);

    private async Task<DailyTaskReadSnapshot> ReadDailyTaskSnapshot(int timeout_ms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout_ms, "timeoutMs");
        DailyTaskStateView state = await _application
            .InvokeAsync<DailyTaskStateRequest, DailyTaskStateView>(
                ApplicationMemberIds.DailyTasksState,
                new DailyTaskStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateDailyTaskState(state);

        DailyTaskPage first_page;
        if (state.Summary.Loaded)
        {
            first_page = await _application
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
            DailyTaskRefreshResult refreshed = await _application
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
            page = await _application
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
            !first_page.Summary.Loaded ||
            page.Connected != first_page.Connected ||
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
