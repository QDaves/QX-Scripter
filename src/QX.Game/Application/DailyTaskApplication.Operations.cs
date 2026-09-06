using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class DailyTaskApplication
{
    private const int commit_history_limit = 4;
    private readonly object refresh_sync = new();
    private readonly List<ObservedDailyTaskCommit> snapshot_commits = [];
    private readonly SemaphoreSlim route_signal = new(0, 1);

    public ValueTask<DailyTaskRefreshResult> Refresh(
        DailyTaskRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshCore(request, token));

    private async ValueTask<DailyTaskRefreshResult> RefreshCore(
        DailyTaskRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        DailyTaskSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        ObservedDailyTaskCommit observed = await RequestSnapshot(
            scope,
            false,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        DailyTaskSnapshotLease lease = StoreLease(observed.Update.State);
        try
        {
            DailyTaskPage first_page = PageFor(lease, 0, request.Limit);
            var result = new DailyTaskRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.Update.State.Revision,
                observed.Update.State.TasksRevision,
                observed.Update.State.BaselineRevision,
                lease.Revision,
                1,
                first_page);
            RequireScope(scope);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<DailyTaskClaimDispatchReceipt> Claim(
        DailyTaskClaimActionRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(ClaimCore(request, token)));

    private DailyTaskClaimDispatchReceipt ClaimCore(
        DailyTaskClaimActionRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        DailyTaskSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.DailyTasks.Claim,
            new DailyTaskClaimRequest(request.TaskId),
            scope.Session,
            cancellation_token,
            () =>
            {
                cancellation_token.ThrowIfCancellationRequested();
                RequireScope(scope);
            });
        return new DailyTaskClaimDispatchReceipt(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            request.TaskId,
            1);
    }

    bool IDailyTaskOperations.Request() => InvokeLegacy(DispatchLegacyRequest);

    void IDailyTaskOperations.Claim(long task_id) => InvokeLegacy(
        cancellation_token => DispatchLegacyClaim(task_id, cancellation_token));

    Task<IReadOnlyList<DailyTask>> IDailyTaskOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsureLoadedCore(timeout_milliseconds, token)).AsTask();

    private async ValueTask<IReadOnlyList<DailyTask>> EnsureLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        ValidateTimeout(timeout_milliseconds);
        DailyTaskSessionScope scope = CaptureScope(null, cancellation_token);
        DailyTaskState current = daily_tasks.State;
        if (scope.Matches(current) && current.Loaded)
            return current.Tasks.ToArray();
        ObservedDailyTaskCommit observed = await RequestSnapshot(
            scope,
            true,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        return observed.Update.State.Tasks.ToArray();
    }

    private bool DispatchLegacyRequest(CancellationToken cancellation_token)
    {
        DailyTaskSessionScope scope = CaptureScope(null, cancellation_token);
        bool sent = false;
        try
        {
            message_dispatcher.Dispatch(
                MessageContracts.DailyTasks.Request,
                new DailyTaskListRequest(),
                scope.Session,
                cancellation_token,
                () =>
                {
                    if (!daily_tasks.TryAdvanceLegacyRequest(
                            scope.Session,
                            scope.SessionGeneration,
                            out _))
                    {
                        throw new DailyTaskRequestThrottledException();
                    }
                    sent = true;
                });
        }
        catch (DailyTaskRequestThrottledException)
        {
            return false;
        }
        return sent;
    }

    private void DispatchLegacyClaim(long task_id, CancellationToken cancellation_token)
    {
        DailyTaskSessionScope scope = CaptureScope(null, cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.DailyTasks.Claim,
            new DailyTaskClaimRequest(task_id),
            scope.Session,
            cancellation_token,
            () => RequireScope(scope));
    }

    private async Task<ObservedDailyTaskCommit> RequestSnapshot(
        DailyTaskSessionScope scope,
        bool ensure_only,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        DateTimeOffset deadline = time_provider.GetUtcNow().AddMilliseconds(timeout_milliseconds);
        while (true)
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireScope(scope);
            DailyTaskRequestCorrelation correlation = daily_tasks.CaptureRequestCorrelation(
                scope.Session,
                scope.SessionGeneration);
            if (ensure_only && correlation.State.Loaded)
            {
                return new ObservedDailyTaskCommit(
                    new DailyTaskStateUpdate(
                        DailyTaskStateChangeKind.Snapshot,
                        correlation.State,
                        new DailyTasksActiveList(correlation.State.Tasks),
                        correlation.RequestEpoch,
                        0),
                    time_provider.GetUtcNow());
            }
            int remaining = RemainingMilliseconds(deadline, timeout_milliseconds);
            if (correlation.OutstandingRequests != 0)
            {
                await WaitForRouteChange(remaining, cancellation_token).ConfigureAwait(false);
                continue;
            }

            var await_state = new RouteAwaitState(ensure_only);
            try
            {
                await requests.RequestAsync(
                    MessageContracts.DailyTasks.Request,
                    new DailyTaskListRequest(),
                    MessageContracts.DailyTasks.Snapshot,
                    scope.Session,
                    match: _ => MatchSnapshot(await_state, scope),
                    timeout_ms: remaining,
                    block: false,
                    cancellation_token: cancellation_token,
                    max_attempts: 1,
                    dispatch_guard: () => Arm(
                        await_state,
                        scope,
                        deadline,
                        timeout_milliseconds,
                        cancellation_token),
                    attempt_start: () => Prepare(await_state, scope)).ConfigureAwait(false);
                RequireScope(scope);
                return Accepted(await_state);
            }
            catch (DailyTaskDispatchRetryException)
            {
                continue;
            }
            catch (DailyTaskAlreadyLoadedException loaded) when (ensure_only)
            {
                return new ObservedDailyTaskCommit(
                    new DailyTaskStateUpdate(
                        DailyTaskStateChangeKind.Snapshot,
                        loaded.State,
                        new DailyTasksActiveList(loaded.State.Tasks),
                        loaded.RequestEpoch,
                        0),
                    time_provider.GetUtcNow());
            }
        }
    }

    private void Prepare(RouteAwaitState await_state, DailyTaskSessionScope scope)
    {
        DailyTaskRequestCorrelation correlation = daily_tasks.CaptureRequestCorrelation(
            scope.Session,
            scope.SessionGeneration);
        if (correlation.OutstandingRequests != 0)
            throw new DailyTaskDispatchRetryException();
        if (await_state.EnsureOnly && correlation.State.Loaded)
        {
            throw new DailyTaskAlreadyLoadedException(
                correlation.State,
                correlation.RequestEpoch);
        }
        lock (refresh_sync)
        {
            await_state.RequestBaseline = correlation.RequestEpoch;
            await_state.SourceBaseline = correlation.State.BaselineRevision;
            await_state.ExpectedRequestEpoch = -1;
            await_state.Accepted = null;
            await_state.Armed = false;
        }
    }

    private void Arm(
        RouteAwaitState await_state,
        DailyTaskSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        _ = RemainingMilliseconds(deadline, timeout_milliseconds);
        long baseline;
        lock (refresh_sync)
            baseline = await_state.RequestBaseline;
        if (baseline < 0)
            throw new InvalidOperationException("The daily task request was not prepared.");
        long expected = daily_tasks.AdvanceTypedRequest(
            baseline,
            scope.Session,
            scope.SessionGeneration);
        lock (refresh_sync)
        {
            DailyTaskState current = daily_tasks.State;
            if (!scope.Matches(current))
            {
                throw new InvalidOperationException(
                    "The hotel session changed while the daily task response was armed.");
            }
            await_state.SourceBaseline = current.BaselineRevision;
            await_state.ExpectedRequestEpoch = expected;
            await_state.Armed = true;
        }
    }

    private bool MatchSnapshot(RouteAwaitState await_state, DailyTaskSessionScope scope)
    {
        lock (refresh_sync)
        {
            if (!await_state.Armed ||
                await_state.Accepted is not null ||
                !ScopeCurrent(scope) ||
                !daily_tasks.RequestEpochIsCurrent(
                    await_state.ExpectedRequestEpoch,
                    scope.Session,
                    scope.SessionGeneration))
            {
                return false;
            }
            ObservedDailyTaskCommit? accepted = snapshot_commits.FirstOrDefault(commit =>
                commit.Update.Kind is DailyTaskStateChangeKind.Snapshot &&
                commit.Update.RequestEpoch == await_state.ExpectedRequestEpoch &&
                scope.Matches(commit.Update.State) &&
                commit.Update.State.BaselineRevision > await_state.SourceBaseline);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private static ObservedDailyTaskCommit Accepted(RouteAwaitState await_state) =>
        await_state.Accepted ??
        throw new InvalidOperationException("The daily task response was not committed.");

    private void ObserveCommit(DailyTaskStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
        {
            lock (refresh_sync)
            {
                if (update.Kind is DailyTaskStateChangeKind.Reset)
                {
                    snapshot_commits.Clear();
                    ClearLeases();
                }
                else if (update.Kind is DailyTaskStateChangeKind.Snapshot)
                {
                    snapshot_commits.Add(new ObservedDailyTaskCommit(
                        update,
                        time_provider.GetUtcNow()));
                    while (snapshot_commits.Count > commit_history_limit)
                        snapshot_commits.RemoveAt(0);
                }
            }
            PulseRoute();
        }
    }

    private async Task WaitForRouteChange(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        try
        {
            await route_signal.WaitAsync(
                TimeSpan.FromMilliseconds(timeout_milliseconds),
                cancellation_token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
    }

    private void PulseRoute()
    {
        try
        {
            route_signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private void ClearOperationState()
    {
        lock (refresh_sync)
            snapshot_commits.Clear();
        PulseRoute();
    }

    private DailyTaskSessionScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedSessionGeneration(expected_session_generation);
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        DailyTaskState state = daily_tasks.State;
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The daily task state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active daily task session generation does not match the expected generation.");
        }
        return new DailyTaskSessionScope(session, state.SessionGeneration);
    }

    private void RequireScope(DailyTaskSessionScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeCurrent(scope))
        {
            throw new RequestDisconnectedException(
                MessageKeys.DailyTasks.Request.ToString(),
                MessageKeys.DailyTasks.Snapshot.ToString());
        }
    }

    private bool ScopeCurrent(DailyTaskSessionScope scope) =>
        !DisposalStarted() && scope.Matches(connection.Session, daily_tasks.State);

    private int RemainingMilliseconds(DateTimeOffset deadline, int original)
    {
        double remaining = (deadline - time_provider.GetUtcNow()).TotalMilliseconds;
        if (remaining <= 0)
        {
            throw new RequestTimeoutException(
                MessageKeys.DailyTasks.Request.ToString(),
                MessageKeys.DailyTasks.Snapshot.ToString(),
                original);
        }
        return Math.Max(1, (int)Math.Ceiling(remaining));
    }

    private static void ValidateExpectedSessionGeneration(long? generation)
    {
        if (generation is <= 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
    }

    private static void ValidatePageLimit(int limit)
    {
        if (limit is < 1 or > maximum_page_size)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private readonly record struct DailyTaskSessionScope(
        Session Session,
        long SessionGeneration)
    {
        public bool Matches(DailyTaskState state) =>
            ReferenceEquals(Session, state.Session) &&
            SessionGeneration == state.SessionGeneration;

        public bool Matches(Session? connection_session, DailyTaskState state) =>
            ReferenceEquals(Session, connection_session) && Matches(state);
    }

    private sealed class RouteAwaitState(bool ensure_only)
    {
        public bool EnsureOnly { get; } = ensure_only;
        public long RequestBaseline { get; set; } = -1;
        public long SourceBaseline { get; set; } = -1;
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool Armed { get; set; }
        public ObservedDailyTaskCommit? Accepted { get; set; }
    }

    private sealed record ObservedDailyTaskCommit(
        DailyTaskStateUpdate Update,
        DateTimeOffset ObservedAtUtc);

    private sealed class DailyTaskDispatchRetryException : Exception
    {
    }

    private sealed class DailyTaskAlreadyLoadedException(
        DailyTaskState state,
        long request_epoch) : Exception
    {
        public DailyTaskState State { get; } = state;
        public long RequestEpoch { get; } = request_epoch;
    }

    private sealed class DailyTaskRequestThrottledException : Exception
    {
    }
}
