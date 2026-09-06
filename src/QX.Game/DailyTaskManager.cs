using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Qx.Game.Application;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game;

internal enum DailyTaskStateChangeKind
{
    Snapshot,
    Added,
    Updated,
    Request,
    Reset
}

internal sealed record DailyTaskState(
    Session? Session,
    long SessionGeneration,
    long Revision,
    long TasksRevision,
    long BaselineRevision,
    long AddedRevision,
    long UpdateRevision,
    bool Loaded,
    IReadOnlyList<DailyTask> Tasks);

internal sealed record DailyTaskUpdateCommit(
    DailyTask Task,
    DailyTaskStatus PreviousStatus);

internal sealed record DailyTaskStateUpdate(
    DailyTaskStateChangeKind Kind,
    DailyTaskState State,
    object? Value,
    long RequestEpoch,
    long PublicationEpoch);

internal readonly record struct DailyTaskRequestCorrelation(
    DailyTaskState State,
    long RequestEpoch,
    int OutstandingRequests);

/// <summary>
/// Mirrors the daily tasks: the short repeatable goals the hotel hands out each day, their progress
/// and the rewards waiting to be claimed.
/// </summary>
/// <remarks>
/// <para>
/// The whole feature is Flash only. It has no Unity counterpart in <c>messages.ini</c>, so every
/// binding here is made for Flash alone rather than cross-mapped.
/// </para>
/// <para>
/// Ordering is preserved the way the client builds its list: the active-list message is a full
/// snapshot, and the client rebuilds from it with the ordinary tasks first and the bonus task last,
/// regardless of the order the hotel sent them in.
/// </para>
/// </remarks>
public sealed class DailyTaskManager : GameStateManager
{
    private const int RequestIntervalMs = 10000;

    private readonly object operations_sync = new();
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Queue<DailyTaskStateUpdate> publications = [];
    private readonly Stopwatch since_request = Stopwatch.StartNew();
    private DailyTaskState state = InitialState();
    private IDailyTaskOperations? operations;
    private long request_epoch;
    private int outstanding_requests;
    private long clean_request_epoch;
    private long last_request_ms = long.MinValue;
    private long committed_generation;
    private long reset_generation = -1;
    private long publication_epoch;
    private bool publishing;
    private bool delivering;
    private int delivery_thread_id;

    /// <summary>The running tasks, ordinary ones first and the bonus task last.</summary>
    public IReadOnlyList<DailyTask> Tasks => State.Tasks.ToArray();

    /// <summary>Whether the hotel has sent the task list this session.</summary>
    public bool IsLoaded => State.Loaded;

    /// <summary>The tasks that are finished and still owe a reward.</summary>
    public IReadOnlyList<DailyTask> Claimable =>
        State.Tasks.Where(task => task.IsClaimable).ToArray();

    /// <summary>The bonus task, or <see langword="null"/> when the hotel has not granted one.</summary>
    public DailyTask? Bonus => State.Tasks.FirstOrDefault(task => task.IsBonus);

    /// <summary>Raised when the full task list arrived and replaced what was held.</summary>
    public event Action<IReadOnlyList<DailyTask>>? ListChanged;

    /// <summary>Raised when the hotel added tasks to the running set.</summary>
    public event Action<IReadOnlyList<DailyTask>>? TasksAdded;

    /// <summary>Raised when a task's progress or status changed, with the task as it now stands.</summary>
    public event Action<DailyTask>? TaskUpdated;

    /// <summary>Raised when a task became claimable.</summary>
    public event Action<DailyTask>? TaskCompleted;

    /// <summary>Raised when a task's reward was taken.</summary>
    public event Action<DailyTask>? TaskClaimed;
    internal event Action<DailyTaskStateUpdate>? StateCommitted;
    internal event Action<DailyTaskStateUpdate>? StateChanged;

    internal DailyTaskState State => Volatile.Read(ref state);

    protected override void OnAttach()
    {
        CommitReset(CurrentSession);
        OnConnected(BindSession);
        OnOutgoing(
            MessageContracts.DailyTasks.Request,
            (_, generation) => ObserveRequest(generation));
        OnIncoming(MessageContracts.DailyTasks.Snapshot, ApplySnapshot);
        OnIncoming(MessageContracts.DailyTasks.Added, ApplyAdded);
        OnIncoming(MessageContracts.DailyTasks.Updated, ApplyUpdate);
    }

    /// <summary>Asks the hotel for the task list, unless one was asked for in the last ten seconds.</summary>
    /// <returns>Whether a request was actually sent.</returns>
    public bool Request() => Operations().Request();

    /// <summary>Claims the reward for a finished task.</summary>
    /// <param name="taskId">The task to claim.</param>
    public void Claim(long taskId) => Operations().Claim(taskId);

    /// <summary>Whether the connected client supports daily tasks at all.</summary>
    public bool IsSupported =>
        (Interceptor.Session?.Client ?? Interceptor.Messages.ActiveClient) is ClientType.Flash;

    /// <summary>Returns the task list, asking the hotel for it when it has not been seen.</summary>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <exception cref="TimeoutException">The hotel did not answer in time.</exception>
    public Task<IReadOnlyList<DailyTask>> EnsureLoadedAsync(
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        return Operations().EnsureLoadedAsync(timeoutMs, cancellationToken);
    }

    internal void BindOperations(IDailyTaskOperations value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (operations_sync)
        {
            if (operations is not null)
                throw new InvalidOperationException("Daily task operations are already bound.");
            Volatile.Write(ref operations, value);
        }
    }

    internal void UnbindOperations(IDailyTaskOperations value)
    {
        lock (operations_sync)
        {
            if (ReferenceEquals(operations, value))
                Volatile.Write(ref operations, null);
        }
    }

    internal DailyTaskRequestCorrelation CaptureRequestCorrelation(
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        lock (state_sync)
        {
            RequireRequestScope(expected_session, expected_session_generation, "captured");
            return new DailyTaskRequestCorrelation(state, request_epoch, outstanding_requests);
        }
    }

    internal bool TryAdvanceLegacyRequest(
        Session expected_session,
        long expected_session_generation,
        out long next_epoch)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        DailyTaskStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                long now = since_request.ElapsedMilliseconds;
                if (last_request_ms != long.MinValue &&
                    now <= last_request_ms + RequestIntervalMs)
                {
                    next_epoch = request_epoch;
                    return false;
                }
                last_request_ms = now;
                update = BeginRequestUnsafe(expected_session, expected_session_generation);
                next_epoch = update.RequestEpoch;
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return true;
    }

    internal long AdvanceTypedRequest(
        long baseline,
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(baseline);
        ArgumentNullException.ThrowIfNull(expected_session);
        DailyTaskStateUpdate update;
        Exception? failure;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                RequireRequestScope(expected_session, expected_session_generation, "advanced");
                if (request_epoch != baseline || outstanding_requests != 0)
                {
                    throw new InvalidOperationException(
                        "The daily task request is no longer safe to dispatch.");
                }
                update = BeginRequestUnsafe(expected_session, expected_session_generation);
            }
            failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
        return update.RequestEpoch;
    }

    internal bool RequestEpochIsCurrent(
        long expected_epoch,
        Session expected_session,
        long expected_session_generation)
    {
        lock (state_sync)
        {
            DailyTaskState current = state;
            if (!ReferenceEquals(current.Session, expected_session) ||
                current.SessionGeneration != expected_session_generation ||
                request_epoch != expected_epoch)
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == expected_session_generation &&
            after == expected_session_generation &&
            ReferenceEquals(active_session, expected_session);
    }

    internal bool IsCurrentPublication(DailyTaskStateUpdate update) => UpdateCurrent(update);

    protected override void Reset() => CommitReset(CurrentSession);

    private void BindSession(Session session) => CommitReset(session);

    private void ApplySnapshot(DailyTasksActiveList message, long state_generation)
    {
        DailyTask[] tasks = Order(message.Tasks.Select(Clone));
        Store(
            state_generation,
            DailyTaskStateChangeKind.Snapshot,
            new DailyTasksActiveList(tasks),
            current => current with
            {
                Revision = checked(current.Revision + 1),
                TasksRevision = checked(current.TasksRevision + 1),
                BaselineRevision = checked(current.BaselineRevision + 1),
                Loaded = true,
                Tasks = Array.AsReadOnly(tasks)
            },
            true);
    }

    private void ApplyAdded(DailyTasksTasksAdded message, long state_generation)
    {
        if (message.Tasks.Count == 0)
            return;
        DailyTask[] added = message.Tasks.Select(Clone).ToArray();
        Store(
            state_generation,
            DailyTaskStateChangeKind.Added,
            new DailyTasksTasksAdded(added),
            current =>
            {
                DailyTask[] tasks = Order(
                    current.Tasks
                        .Where(existing => !added.Any(task => task.TaskId == existing.TaskId))
                        .Concat(added));
                return current with
                {
                    Revision = checked(current.Revision + 1),
                    TasksRevision = checked(current.TasksRevision + 1),
                    AddedRevision = checked(current.AddedRevision + 1),
                    Loaded = true,
                    Tasks = Array.AsReadOnly(tasks)
                };
            },
            false);
    }

    private void ApplyUpdate(DailyTasksTaskUpdate message, long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        DailyTaskStateUpdate? update = null;
        bool unknown = false;
        bool drain = false;
        Exception? committed_failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                DailyTaskState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return;
                int index = current.Tasks
                    .Select((task, position) => (task, position))
                    .FirstOrDefault(value => value.task.TaskId == message.TaskId, (null!, -1))
                    .position;
                if (index < 0)
                {
                    unknown = true;
                }
                else
                {
                    DailyTaskStatus previous = current.Tasks[index].Status;
                    DailyTask task = current.Tasks[index] with
                    {
                        Repeats = message.Repeats,
                        Status = message.Status,
                        SecondsLeftAtArrival = message.SecondsLeftAtArrival,
                        ReceivedAt = DateTimeOffset.UtcNow
                    };
                    DailyTask[] tasks = current.Tasks.ToArray();
                    tasks[index] = task;
                    var updated = current with
                    {
                        Revision = checked(current.Revision + 1),
                        TasksRevision = checked(current.TasksRevision + 1),
                        UpdateRevision = checked(current.UpdateRevision + 1),
                        Tasks = Array.AsReadOnly(tasks)
                    };
                    var commit = new DailyTaskUpdateCommit(task, previous);
                    if (!ApplyIfCurrent(state_generation, active_session, () =>
                        {
                            Volatile.Write(ref state, updated);
                            committed_generation = state_generation;
                            reset_generation = -1;
                            update = new DailyTaskStateUpdate(
                                DailyTaskStateChangeKind.Updated,
                                updated,
                                commit,
                                request_epoch,
                                publication_epoch);
                        }))
                    {
                        return;
                    }
                }
            }
            if (update is not null)
            {
                publications.Enqueue(update);
                drain = !publishing;
                publishing = true;
                committed_failure = NotifyCommitted(update);
            }
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
        if (unknown)
            Request();
    }

    private void ObserveRequest(long state_generation)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        DailyTaskStateUpdate? update = null;
        Exception? failure = null;
        lock (publication_sync)
        {
            lock (state_sync)
            {
                if (RequestScopeCurrent(active_session, state_generation))
                    update = BeginRequestUnsafe(active_session, state_generation);
            }
            if (update is not null)
                failure = NotifyCommitted(update);
        }
        ThrowFailure(failure);
    }

    private DailyTaskStateUpdate BeginRequestUnsafe(
        Session expected_session,
        long expected_session_generation)
    {
        long next = checked(request_epoch + 1);
        int previous = outstanding_requests;
        int outstanding = checked(previous + 1);
        if (!ApplyIfCurrent(
                expected_session_generation,
                expected_session,
                () =>
                {
                    request_epoch = next;
                    outstanding_requests = outstanding;
                    clean_request_epoch = previous == 0 ? next : 0;
                }))
        {
            throw new InvalidOperationException(
                "The hotel session changed before the daily task request could be dispatched.");
        }
        return new DailyTaskStateUpdate(
            DailyTaskStateChangeKind.Request,
            state,
            null,
            next,
            publication_epoch);
    }

    private void Store(
        long state_generation,
        DailyTaskStateChangeKind kind,
        object value,
        Func<DailyTaskState, DailyTaskState> mutation,
        bool response)
    {
        Session? active_session = CurrentSession;
        if (active_session is null)
            return;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            DailyTaskStateUpdate update;
            lock (state_sync)
            {
                DailyTaskState current = state;
                if (!StateCurrent(current, active_session, state_generation))
                    return;
                int previous = outstanding_requests;
                int outstanding = response && previous > 0 ? previous - 1 : previous;
                long response_epoch = response && previous == 1 ? clean_request_epoch : 0;
                DailyTaskState updated = mutation(current);
                update = null!;
                if (!ApplyIfCurrent(state_generation, active_session, () =>
                    {
                        if (response)
                        {
                            outstanding_requests = outstanding;
                            if (previous <= 1)
                                clean_request_epoch = 0;
                        }
                        Volatile.Write(ref state, updated);
                        committed_generation = state_generation;
                        reset_generation = -1;
                        update = new DailyTaskStateUpdate(
                            kind,
                            updated,
                            value,
                            response_epoch,
                            publication_epoch);
                    }))
                {
                    return;
                }
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private void CommitReset(Session? active_session)
    {
        long state_generation = CurrentStateGeneration;
        int thread_id = Environment.CurrentManagedThreadId;
        bool drain;
        Exception? committed_failure;
        lock (publication_sync)
        {
            while (delivering && delivery_thread_id != thread_id)
                Monitor.Wait(publication_sync);
            DailyTaskStateUpdate update;
            lock (state_sync)
            {
                DailyTaskState current = state;
                if (state_generation < committed_generation ||
                    state_generation == reset_generation &&
                    ReferenceEquals(current.Session, active_session))
                {
                    return;
                }
                var updated = new DailyTaskState(
                    active_session,
                    state_generation,
                    checked(current.Revision + 1),
                    checked(current.TasksRevision + 1),
                    current.BaselineRevision,
                    checked(current.AddedRevision + 1),
                    checked(current.UpdateRevision + 1),
                    false,
                    Array.AsReadOnly(Array.Empty<DailyTask>()));
                Volatile.Write(ref state, updated);
                request_epoch = 0;
                outstanding_requests = 0;
                clean_request_epoch = 0;
                last_request_ms = long.MinValue;
                committed_generation = state_generation;
                reset_generation = state_generation;
                publication_epoch = checked(publication_epoch + 1);
                update = new DailyTaskStateUpdate(
                    DailyTaskStateChangeKind.Reset,
                    updated,
                    null,
                    0,
                    publication_epoch);
            }
            publications.Enqueue(update);
            drain = !publishing;
            publishing = true;
            committed_failure = NotifyCommitted(update);
        }
        Exception? publication_failure = DrainIfNeeded(drain);
        ThrowFailures(committed_failure, publication_failure);
    }

    private Exception? DrainIfNeeded(bool drain)
    {
        if (!drain)
            return null;
        try
        {
            DrainPublications();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private void DrainPublications()
    {
        Exception? failure = null;
        while (true)
        {
            DailyTaskStateUpdate update;
            lock (publication_sync)
            {
                if (!publications.TryDequeue(out update!))
                {
                    publishing = false;
                    break;
                }
                delivering = true;
                delivery_thread_id = Environment.CurrentManagedThreadId;
            }
            try
            {
                if (!UpdateCurrent(update))
                    continue;
                failure = Notify(StateChanged, update, update, failure);
                if (!UpdateCurrent(update))
                    continue;
                failure = NotifyLegacy(update, failure);
            }
            finally
            {
                lock (publication_sync)
                {
                    delivering = false;
                    delivery_thread_id = 0;
                    Monitor.PulseAll(publication_sync);
                }
            }
        }
        ThrowFailure(failure);
    }

    private Exception? NotifyLegacy(DailyTaskStateUpdate update, Exception? failure)
    {
        switch (update.Kind)
        {
            case DailyTaskStateChangeKind.Snapshot:
                return Notify(
                    ListChanged,
                    ((DailyTasksActiveList)update.Value!).Tasks,
                    update,
                    failure);
            case DailyTaskStateChangeKind.Added:
                return Notify(
                    TasksAdded,
                    ((DailyTasksTasksAdded)update.Value!).Tasks,
                    update,
                    failure);
            case DailyTaskStateChangeKind.Updated:
                {
                    var commit = (DailyTaskUpdateCommit)update.Value!;
                    failure = Notify(TaskUpdated, commit.Task, update, failure);
                    if (commit.PreviousStatus == commit.Task.Status)
                        return failure;
                    if (commit.Task.Status is DailyTaskStatus.Completed)
                        return Notify(TaskCompleted, commit.Task, update, failure);
                    return commit.Task.Status is DailyTaskStatus.Claimed
                        ? Notify(TaskClaimed, commit.Task, update, failure)
                        : failure;
                }
            case DailyTaskStateChangeKind.Request:
            case DailyTaskStateChangeKind.Reset:
                return failure;
            default:
                throw new ArgumentOutOfRangeException(nameof(update));
        }
    }

    private bool UpdateCurrent(DailyTaskStateUpdate update)
    {
        lock (publication_sync)
        {
            if (publication_epoch != update.PublicationEpoch)
                return false;
            DailyTaskState current = State;
            if (current.SessionGeneration != update.State.SessionGeneration ||
                !ReferenceEquals(current.Session, update.State.Session))
            {
                return false;
            }
        }
        long before = CurrentStateGeneration;
        Session? active_session = CurrentSession;
        long after = CurrentStateGeneration;
        return before == update.State.SessionGeneration &&
            after == update.State.SessionGeneration &&
            ReferenceEquals(active_session, update.State.Session);
    }

    private Exception? NotifyCommitted(DailyTaskStateUpdate update) =>
        Notify(StateCommitted, update, update, null, false);

    private Exception? Notify<T>(
        Action<T>? listeners,
        T value,
        DailyTaskStateUpdate update,
        Exception? failure,
        bool require_current = true)
    {
        if (listeners is null)
            return failure;
        foreach (Action<T> listener in listeners.GetInvocationList().Cast<Action<T>>())
        {
            if (require_current && !UpdateCurrent(update))
                break;
            try
            {
                listener(value);
            }
            catch (Exception error)
            {
                failure ??= error;
            }
        }
        return failure;
    }

    private bool StateCurrent(
        DailyTaskState current,
        Session active_session,
        long state_generation) =>
        state_generation == committed_generation &&
        current.SessionGeneration == state_generation &&
        ReferenceEquals(current.Session, active_session);

    private bool RequestScopeCurrent(Session active_session, long state_generation) =>
        StateCurrent(state, active_session, state_generation);

    private void RequireRequestScope(
        Session expected_session,
        long expected_session_generation,
        string operation)
    {
        DailyTaskState current = state;
        if (!ReferenceEquals(current.Session, expected_session) ||
            current.SessionGeneration != expected_session_generation ||
            committed_generation != expected_session_generation)
        {
            throw new InvalidOperationException(
                $"The daily task request correlation cannot be {operation} for a stale hotel session.");
        }
    }

    private IDailyTaskOperations Operations() =>
        Volatile.Read(ref operations) ??
        throw new InvalidOperationException(
            "Daily task operations are unavailable until the application runtime is active.");

    private static DailyTaskState InitialState() => new(
        null,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        Array.AsReadOnly(Array.Empty<DailyTask>()));

    private static DailyTask[] Order(IEnumerable<DailyTask> tasks) =>
        [.. tasks.Where(task => !task.IsBonus), .. tasks.Where(task => task.IsBonus)];

    private static DailyTask Clone(DailyTask task) => task with
    {
        Rewards = task.Rewards.Select(reward => reward with { }).ToArray()
    };

    private static void ThrowFailures(Exception? first, Exception? second)
    {
        if (first is not null && second is not null)
            throw new AggregateException(first, second);
        if (first is not null)
            ExceptionDispatchInfo.Capture(first).Throw();
        if (second is not null)
            ExceptionDispatchInfo.Capture(second).Throw();
    }

    private static void ThrowFailure(Exception? failure)
    {
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
