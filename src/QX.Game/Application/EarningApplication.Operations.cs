using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class EarningApplication
{
    private readonly object operation_sync = new();
    private readonly Dictionary<int, EarningClaimQueue> claim_queues = [];
    private EarningStatusOperation? status_operation;

    public ValueTask<EarningRefreshResult> Refresh(
        EarningRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshCore(request, token));

    private async ValueTask<EarningRefreshResult> RefreshCore(
        EarningRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        EarningSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (
            EarningStatusOperation operation,
            EarningStatusWaiter waiter) = AcquireStatusOperation(
            scope,
            false,
            request.TimeoutMilliseconds,
            cancellation_token);
        ObservedEarningStatus observed = await WaitStatusOperation(
            operation,
            waiter,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        EarningSnapshotLease lease = StoreLease(observed.State);
        try
        {
            EarningEntryPage first_page = EntryPageFor(lease, 0, request.Limit);
            var result = new EarningRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.StatusRevision,
                observed.State.BaselineRevision,
                lease.Revision,
                waiter.DispatchCredit ? 1 : 0,
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

    public ValueTask<EarningClaimActionResult> Claim(
        EarningClaimActionRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => ClaimCore(request, token));

    private async ValueTask<EarningClaimActionResult> ClaimCore(
        EarningClaimActionRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCategory(request.Category);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        EarningSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        EarningClaimOperation operation = EnqueueClaim(
            request.Category,
            scope,
            request.TimeoutMilliseconds,
            cancellation_token);
        ObservedEarningClaim observed = await WaitClaimOperation(
            operation,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireClaimScope(scope);
        EarningSnapshotLease lease = StoreLease(observed.State);
        try
        {
            var result = new EarningClaimActionResult(
                scope.Session.Client,
                operation.DispatchedAtUtc,
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.StatusRevision,
                observed.State.BaselineRevision,
                observed.State.ClaimRevision,
                lease.Revision,
                request.Category,
                observed.Result.Success,
                1,
                VaultSummary(observed.State));
            RequireClaimScope(scope);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    void IEarningOperations.RequestStatus() => InvokeLegacy(DispatchLegacyStatus);

    void IEarningOperations.RequestStatusAfterNotification(
        Session expected_session,
        long expected_session_generation)
    {
        ArgumentNullException.ThrowIfNull(expected_session);
        try
        {
            InvokeLegacy(cancellation_token => DispatchNotificationStatus(
                expected_session,
                expected_session_generation,
                cancellation_token));
        }
        catch (EarningNotificationRequestSkippedException)
        {
        }
        catch (InvalidOperationException) when (!NotificationScopeCurrent(
            expected_session,
            expected_session_generation))
        {
        }
    }

    void IEarningOperations.Claim(EarningCategory category) => InvokeLegacy(
        cancellation_token => DispatchLegacyClaim(
            EarningsManager.NormalizeCategory(category),
            cancellation_token));

    Task<EarningStatus> IEarningOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsureLoadedCore(timeout_milliseconds, token)).AsTask();

    private async ValueTask<EarningStatus> EnsureLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        EarningSessionScope scope = CaptureScope(null, cancellation_token);
        EarningState current = earnings.State;
        if (ScopeMatches(scope, current) && current.Loaded)
            return current.Status;
        (EarningStatusOperation operation, EarningStatusWaiter waiter) =
            AcquireStatusOperation(
                scope,
                true,
                timeout_milliseconds,
                cancellation_token);
        ObservedEarningStatus observed = await WaitStatusOperation(
            operation,
            waiter,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope);
        return observed.State.Status;
    }

    private void DispatchLegacyStatus(CancellationToken cancellation_token)
    {
        EarningSessionScope scope = CaptureScope(null, cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Earnings.StatusRequest,
            new EarningStatusRequest(),
            scope.Session,
            cancellation_token,
            () => earnings.AdvanceLegacyStatusRequest(
                scope.Session,
                scope.SessionGeneration));
    }

    private void DispatchNotificationStatus(
        Session expected_session,
        long expected_session_generation,
        CancellationToken cancellation_token)
    {
        message_dispatcher.Dispatch(
            MessageContracts.Earnings.StatusRequest,
            new EarningStatusRequest(),
            expected_session,
            cancellation_token,
            () =>
            {
                if (!earnings.TryAdvanceNotificationStatusRequest(
                    expected_session,
                    expected_session_generation))
                {
                    throw new EarningNotificationRequestSkippedException();
                }
            });
    }

    private void DispatchLegacyClaim(int category, CancellationToken cancellation_token)
    {
        ValidateCategory(category);
        EarningSessionScope scope = CaptureScope(null, cancellation_token);
        message_dispatcher.Dispatch(
            MessageContracts.Earnings.Claim,
            new EarningClaimRequest((EarningCategory)category),
            scope.Session,
            cancellation_token,
            () => earnings.AdvanceLegacyClaimRequest(
                category,
                scope.Session,
                scope.SessionGeneration));
    }

    private (
        EarningStatusOperation Operation,
        EarningStatusWaiter Waiter) AcquireStatusOperation(
        EarningSessionScope scope,
        bool ensure_only,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        EarningStatusOperation operation;
        var waiter = new EarningStatusWaiter(
            ensure_only,
            time_provider.GetUtcNow().AddMilliseconds(timeout_milliseconds),
            cancellation_token);
        bool created = false;
        lock (operation_sync)
        {
            ThrowIfDisposed();
            operation = status_operation!;
            if (operation is not null && !operation.Scope.Matches(scope))
            {
                CompleteStatusUnsafe(operation, DisconnectStatus());
                operation = null!;
            }
            if (operation is not null)
            {
                bool promoted = !ensure_only &&
                    StatusEnsureOnlyUnsafe(operation) &&
                    !operation.RequestCounted;
                waiter.DispatchCredit = promoted;
                operation.Waiters.Add(waiter);
                Pulse(operation.Signal);
                return (operation, waiter);
            }
            EarningState state = earnings.State;
            if (!ScopeMatches(scope, state))
                throw DisconnectStatus();
            operation = new EarningStatusOperation(scope);
            waiter.DispatchCredit = !ensure_only;
            operation.Waiters.Add(waiter);
            status_operation = operation;
            created = true;
        }
        if (created)
            _ = DriveStatusOperation(operation);
        return (operation, waiter);
    }

    private EarningClaimOperation EnqueueClaim(
        int category,
        EarningSessionScope scope,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        EarningClaimQueue queue;
        EarningClaimOperation operation;
        bool start;
        lock (operation_sync)
        {
            ThrowIfDisposed();
            if (!ScopeMatches(scope, earnings.State))
                throw DisconnectClaim();
            if (!claim_queues.TryGetValue(category, out queue!))
            {
                queue = new EarningClaimQueue(category, scope);
                claim_queues.Add(category, queue);
            }
            else if (!queue.Scope.Matches(scope))
            {
                DisconnectClaimQueueUnsafe(queue);
                queue = new EarningClaimQueue(category, scope);
                claim_queues[category] = queue;
            }
            operation = new EarningClaimOperation(
                category,
                scope,
                time_provider.GetUtcNow().AddMilliseconds(timeout_milliseconds),
                cancellation_token);
            operation.Node = queue.Operations.AddLast(operation);
            start = !queue.DriverRunning;
            if (start)
                queue.DriverRunning = true;
            Pulse(queue.Signal);
        }
        if (start)
            _ = DriveClaimQueue(queue);
        return operation;
    }

    private async Task DriveStatusOperation(EarningStatusOperation operation)
    {
        while (true)
        {
            EarningStatusCorrelation correlation;
            try
            {
                correlation = earnings.CaptureStatusCorrelation(
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration);
            }
            catch (Exception error)
            {
                lock (operation_sync)
                {
                    if (ReferenceEquals(status_operation, operation))
                        CompleteStatusUnsafe(
                            operation,
                            NormalizeStatusFailure(error, operation.Scope));
                }
                return;
            }

            bool dispatch;
            lock (operation_sync)
            {
                if (!ReferenceEquals(status_operation, operation) ||
                    operation.Completion.Task.IsCompleted)
                {
                    return;
                }
                bool current_ensure_only = StatusEnsureOnlyUnsafe(operation);
                if (current_ensure_only && correlation.State.Loaded)
                {
                    CompleteStatusUnsafe(
                        operation,
                        new ObservedEarningStatus(correlation.State, time_provider.GetUtcNow()));
                    return;
                }
                if (!HasLiveStatusWaiterUnsafe(operation) &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted)
                {
                    status_operation = null;
                    operation.Abandoned = true;
                    Pulse(operation.Signal);
                    return;
                }
                dispatch = correlation.OutstandingRequests == 0 && !operation.Dispatching;
                if (dispatch)
                {
                    operation.Dispatching = true;
                    operation.RequestBaseline = correlation.RequestEpoch;
                    operation.SourceBaseline = correlation.State.BaselineRevision;
                    operation.ExpectedRequestEpoch = -1;
                }
            }
            if (!dispatch)
            {
                if (!await WaitSignal(operation.Signal).ConfigureAwait(false))
                    return;
                continue;
            }

            bool guard_started = false;
            try
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Earnings.StatusRequest,
                    new EarningStatusRequest(),
                    operation.Scope.Session,
                    lifetime.Token,
                    () =>
                    {
                        guard_started = true;
                        ArmStatusDispatch(operation, correlation.RequestEpoch);
                    });
                MarkStatusDispatched(operation);
                return;
            }
            catch (EarningStatusAlreadyLoadedException loaded)
            {
                lock (operation_sync)
                {
                    if (!ReferenceEquals(status_operation, operation) ||
                        operation.Completion.Task.IsCompleted)
                    {
                        return;
                    }
                    operation.Dispatching = false;
                    operation.GuardStarted = false;
                    operation.ExpectedRequestEpoch = -1;
                    if (!HasLiveStatusWaiterUnsafe(operation))
                    {
                        status_operation = null;
                        operation.Abandoned = true;
                        Pulse(operation.Signal);
                        return;
                    }
                    if (StatusEnsureOnlyUnsafe(operation))
                    {
                        CompleteStatusUnsafe(
                            operation,
                            new ObservedEarningStatus(
                                loaded.State,
                                time_provider.GetUtcNow()));
                        return;
                    }
                    Pulse(operation.Signal);
                }
                continue;
            }
            catch (EarningDispatchAbandonedException)
            {
                return;
            }
            catch (InvalidOperationException) when (
                guard_started &&
                !StatusRequestCounted(operation) &&
                StatusScopeCurrent(operation.Scope))
            {
                ResetStatusDispatch(operation);
            }
            catch (Exception error)
            {
                CompleteStatus(
                    operation,
                    NormalizeStatusFailure(error, operation.Scope));
                return;
            }
        }
    }

    private async Task DriveClaimQueue(EarningClaimQueue queue)
    {
        while (true)
        {
            EarningClaimOperation? operation;
            lock (operation_sync)
            {
                if (!claim_queues.TryGetValue(queue.Category, out EarningClaimQueue? current) ||
                    !ReferenceEquals(current, queue))
                {
                    return;
                }
                PruneClaimQueueUnsafe(queue);
                operation = queue.Operations.First?.Value;
                if (operation is null)
                {
                    queue.DriverRunning = false;
                    claim_queues.Remove(queue.Category);
                    return;
                }
            }

            EarningClaimCorrelation correlation;
            try
            {
                correlation = earnings.CaptureClaimCorrelation(
                    queue.Category,
                    queue.Scope.Session,
                    queue.Scope.SessionGeneration);
            }
            catch (Exception error)
            {
                lock (operation_sync)
                    DisconnectClaimQueueUnsafe(
                        queue,
                        NormalizeClaimFailure(error, queue.Scope));
                return;
            }

            bool dispatch;
            lock (operation_sync)
            {
                if (!ClaimOperationCurrent(queue, operation))
                    continue;
                dispatch = correlation.OutstandingRequests == 0 && !operation.Dispatching;
                if (dispatch)
                {
                    operation.Dispatching = true;
                    operation.RequestBaseline = correlation.RequestEpoch;
                    operation.SourceBaseline = correlation.State.ClaimRevision;
                    operation.ExpectedRequestEpoch = -1;
                }
            }
            if (!dispatch)
            {
                if (!await WaitSignal(queue.Signal).ConfigureAwait(false))
                    return;
                continue;
            }

            bool guard_started = false;
            try
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Earnings.Claim,
                    new EarningClaimRequest((EarningCategory)queue.Category),
                    queue.Scope.Session,
                    lifetime.Token,
                    () =>
                    {
                        guard_started = true;
                        ArmClaimGuard(queue, operation, correlation.RequestEpoch);
                    });
                MarkClaimDispatched(operation);
            }
            catch (EarningDispatchAbandonedException)
            {
                continue;
            }
            catch (InvalidOperationException) when (
                guard_started &&
                !operation.RequestCounted &&
                ClaimScopeCurrent(queue.Scope))
            {
                lock (operation_sync)
                {
                    if (ClaimOperationCurrent(queue, operation))
                    {
                        operation.Dispatching = false;
                        operation.ExpectedRequestEpoch = -1;
                        operation.GuardStarted = false;
                        if (!ClaimCallerLiveUnsafe(operation))
                        {
                            operation.Abandoned = true;
                            RemoveClaimUnsafe(queue, operation);
                            Pulse(queue.Signal);
                        }
                    }
                }
                continue;
            }
            catch (Exception error)
            {
                lock (operation_sync)
                {
                    if (ClaimOperationCurrent(queue, operation))
                        CompleteClaimUnsafe(
                            queue,
                            operation,
                            NormalizeClaimFailure(error, queue.Scope));
                }
                continue;
            }

            if (!await WaitSignal(queue.Signal).ConfigureAwait(false))
                return;
        }
    }

    private void ArmStatusAfterAdvance(EarningStatusOperation operation, long request_epoch)
    {
        lock (operation_sync)
        {
            if (!ReferenceEquals(status_operation, operation) ||
                operation.Completion.Task.IsCompleted)
            {
                return;
            }
            operation.ExpectedRequestEpoch = request_epoch;
            operation.RequestCounted = true;
            operation.DispatchedAtUtc = time_provider.GetUtcNow();
        }
        EarningStatusCorrelation current = earnings.CaptureStatusCorrelation(
            operation.Scope.Session,
            operation.Scope.SessionGeneration);
        lock (operation_sync)
        {
            if (!ReferenceEquals(status_operation, operation) ||
                operation.Completion.Task.IsCompleted)
            {
                return;
            }
            if (current.RequestEpoch != request_epoch || current.OutstandingRequests != 1)
            {
                CompleteStatusUnsafe(
                    operation,
                    new InvalidOperationException(
                        "The earning status request was invalidated by another dispatch."));
            }
        }
    }

    private void ArmStatusDispatch(EarningStatusOperation operation, long baseline)
    {
        bool ensure_only = BeginStatusGuard(operation);
        long request_epoch;
        if (ensure_only)
        {
            bool advanced = earnings.TryAdvanceTypedStatusRequestIfUnloaded(
                baseline,
                operation.Scope.Session,
                operation.Scope.SessionGeneration,
                out request_epoch,
                out EarningState current);
            if (!advanced)
            {
                lock (operation_sync)
                {
                    if (!ReferenceEquals(status_operation, operation) ||
                        operation.Completion.Task.IsCompleted)
                    {
                        throw new EarningDispatchAbandonedException();
                    }
                    if (StatusEnsureOnlyUnsafe(operation))
                        throw new EarningStatusAlreadyLoadedException(current);
                }
                request_epoch = earnings.AdvanceTypedStatusRequest(
                    baseline,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration);
            }
        }
        else
        {
            request_epoch = earnings.AdvanceTypedStatusRequest(
                baseline,
                operation.Scope.Session,
                operation.Scope.SessionGeneration);
        }
        ArmStatusAfterAdvance(operation, request_epoch);
    }

    private void ArmClaimGuard(
        EarningClaimQueue queue,
        EarningClaimOperation operation,
        long baseline)
    {
        BeginClaimGuard(queue, operation);
        long request_epoch = earnings.AdvanceTypedClaimRequest(
            queue.Category,
            baseline,
            queue.Scope.Session,
            queue.Scope.SessionGeneration);
        lock (operation_sync)
        {
            if (!ClaimOperationCurrent(queue, operation))
                return;
            operation.ExpectedRequestEpoch = request_epoch;
            operation.RequestCounted = true;
            operation.DispatchedAtUtc = time_provider.GetUtcNow();
        }
        EarningClaimCorrelation current = earnings.CaptureClaimCorrelation(
            queue.Category,
            queue.Scope.Session,
            queue.Scope.SessionGeneration);
        lock (operation_sync)
        {
            if (!ClaimOperationCurrent(queue, operation))
                return;
            if (current.RequestEpoch != request_epoch || current.OutstandingRequests != 1)
            {
                CompleteClaimUnsafe(
                    queue,
                    operation,
                    new InvalidOperationException(
                        "The earning claim request was invalidated by another dispatch."));
            }
        }
    }

    private bool BeginStatusGuard(EarningStatusOperation operation)
    {
        lock (operation_sync)
        {
            if (!ReferenceEquals(status_operation, operation) ||
                operation.Completion.Task.IsCompleted ||
                !HasLiveStatusWaiterUnsafe(operation))
            {
                if (ReferenceEquals(status_operation, operation) &&
                    !operation.RequestCounted)
                {
                    status_operation = null;
                    operation.Dispatching = false;
                    operation.Abandoned = true;
                    Pulse(operation.Signal);
                }
                throw new EarningDispatchAbandonedException();
            }
            AssignStatusDispatchCreditUnsafe(operation);
            operation.GuardStarted = true;
            return StatusEnsureOnlyUnsafe(operation);
        }
    }

    private void BeginClaimGuard(
        EarningClaimQueue queue,
        EarningClaimOperation operation)
    {
        lock (operation_sync)
        {
            if (!ClaimOperationCurrent(queue, operation) ||
                !ClaimCallerLiveUnsafe(operation))
            {
                if (ClaimOperationCurrent(queue, operation) &&
                    !operation.RequestCounted)
                {
                    operation.Dispatching = false;
                    operation.Abandoned = true;
                    RemoveClaimUnsafe(queue, operation);
                    Pulse(queue.Signal);
                }
                throw new EarningDispatchAbandonedException();
            }
            operation.GuardStarted = true;
        }
    }

    private void MarkStatusDispatched(EarningStatusOperation operation)
    {
        lock (operation_sync)
        {
            operation.Dispatching = false;
            operation.Dispatched = true;
            Pulse(operation.Signal);
        }
    }

    private void MarkClaimDispatched(EarningClaimOperation operation)
    {
        lock (operation_sync)
        {
            operation.Dispatching = false;
            operation.Dispatched = true;
        }
    }

    private void ResetStatusDispatch(EarningStatusOperation operation)
    {
        lock (operation_sync)
        {
            if (!ReferenceEquals(status_operation, operation) ||
                operation.Completion.Task.IsCompleted)
            {
                return;
            }
            operation.Dispatching = false;
            operation.ExpectedRequestEpoch = -1;
            operation.GuardStarted = false;
            if (!HasLiveStatusWaiterUnsafe(operation) && !operation.RequestCounted)
            {
                status_operation = null;
                operation.Abandoned = true;
            }
            Pulse(operation.Signal);
        }
    }

    private bool StatusRequestCounted(EarningStatusOperation operation)
    {
        lock (operation_sync)
            return operation.RequestCounted;
    }

    private async Task<ObservedEarningStatus> WaitStatusOperation(
        EarningStatusOperation operation,
        EarningStatusWaiter waiter,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        try
        {
            return await operation.Completion.Task.WaitAsync(
                TimeSpan.FromMilliseconds(timeout_milliseconds),
                time_provider,
                cancellation_token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            cancellation_token.ThrowIfCancellationRequested();
            throw new RequestTimeoutException(
                MessageKeys.Earnings.StatusRequest.ToString(),
                MessageKeys.Earnings.StatusSnapshot.ToString(),
                timeout_milliseconds);
        }
        finally
        {
            lock (operation_sync)
            {
                waiter.Active = false;
                operation.Waiters.Remove(waiter);
                if (!HasLiveStatusWaiterUnsafe(operation) &&
                    !operation.Completion.Task.IsCompleted &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted &&
                    ReferenceEquals(status_operation, operation))
                {
                    status_operation = null;
                    operation.Dispatching = false;
                    operation.Abandoned = true;
                }
                Pulse(operation.Signal);
            }
        }
    }

    private async Task<ObservedEarningClaim> WaitClaimOperation(
        EarningClaimOperation operation,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        try
        {
            return await operation.Completion.Task.WaitAsync(
                TimeSpan.FromMilliseconds(timeout_milliseconds),
                time_provider,
                cancellation_token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            cancellation_token.ThrowIfCancellationRequested();
            throw new RequestTimeoutException(
                MessageKeys.Earnings.Claim.ToString(),
                MessageKeys.Earnings.Claimed.ToString(),
                timeout_milliseconds);
        }
        finally
        {
            lock (operation_sync)
            {
                operation.CallerActive = false;
                if (!operation.Completion.Task.IsCompleted &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted &&
                    operation.Node?.List is not null &&
                    claim_queues.TryGetValue(
                        operation.Category,
                        out EarningClaimQueue? queue))
                {
                    operation.Abandoned = true;
                    operation.Dispatching = false;
                    queue.Operations.Remove(operation.Node);
                    operation.Node = null;
                    Pulse(queue.Signal);
                }
            }
        }
    }

    private void ObserveCommit(EarningStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            ObserveCommitCore(update);
    }

    private void ObserveCommitCore(EarningStateUpdate update)
    {
        lock (operation_sync)
        {
            if (update.Kind is EarningStateChangeKind.Reset)
            {
                ClearLeases();
                if (status_operation is { } status)
                    CompleteStatusUnsafe(status, DisconnectStatus());
                foreach (EarningClaimQueue queue in claim_queues.Values.ToArray())
                    DisconnectClaimQueueUnsafe(queue);
                claim_queues.Clear();
                return;
            }

            if (update.Kind is EarningStateChangeKind.Request)
            {
                if (update.Route is EarningRequestRoute.Status)
                {
                    EarningStatusOperation? operation = status_operation;
                    if (operation is not null)
                    {
                        Pulse(operation.Signal);
                        if (operation.ExpectedRequestEpoch > 0 &&
                            update.RequestEpoch != operation.ExpectedRequestEpoch)
                        {
                            CompleteStatusUnsafe(
                                operation,
                                new InvalidOperationException(
                                    "The earning status request was invalidated by another dispatch."));
                        }
                    }
                }
                else if (update.Route is EarningRequestRoute.Claim &&
                    update.Category is int category &&
                    claim_queues.TryGetValue(category, out EarningClaimQueue? queue))
                {
                    Pulse(queue.Signal);
                    EarningClaimOperation? operation = queue.Operations.First?.Value;
                    if (operation is not null &&
                        operation.ExpectedRequestEpoch > 0 &&
                        update.RequestEpoch != operation.ExpectedRequestEpoch)
                    {
                        CompleteClaimUnsafe(
                            queue,
                            operation,
                            new InvalidOperationException(
                                "The earning claim request was invalidated by another dispatch."));
                    }
                }
                return;
            }

            if (update.Kind is EarningStateChangeKind.Snapshot)
            {
                EarningStatusOperation? operation = status_operation;
                if (operation is null)
                    return;
                Pulse(operation.Signal);
                if (operation.ExpectedRequestEpoch > 0 &&
                    update.RequestEpoch == operation.ExpectedRequestEpoch &&
                    operation.Scope.Matches(update.State) &&
                    update.State.BaselineRevision > operation.SourceBaseline)
                {
                    CompleteStatusUnsafe(
                        operation,
                        new ObservedEarningStatus(update.State, time_provider.GetUtcNow()));
                }
                return;
            }

            if (update.Kind is EarningStateChangeKind.Claimed &&
                update.Category is int claimed_category &&
                claim_queues.TryGetValue(claimed_category, out EarningClaimQueue? claimed_queue))
            {
                Pulse(claimed_queue.Signal);
                EarningClaimOperation? operation = claimed_queue.Operations.First?.Value;
                if (operation is null ||
                    operation.ExpectedRequestEpoch <= 0 ||
                    update.RequestEpoch != operation.ExpectedRequestEpoch ||
                    !operation.Scope.Matches(update.State) ||
                    update.State.ClaimRevision <= operation.SourceBaseline)
                {
                    return;
                }
                var commit = (EarningClaimCommit)update.Value!;
                CompleteClaimUnsafe(
                    claimed_queue,
                    operation,
                    new ObservedEarningClaim(
                        update.State,
                        commit.Result,
                        time_provider.GetUtcNow()));
            }
        }
    }

    private void CompleteStatus(
        EarningStatusOperation operation,
        Exception error)
    {
        lock (operation_sync)
        {
            if (ReferenceEquals(status_operation, operation))
                CompleteStatusUnsafe(operation, error);
        }
    }

    private void CompleteStatusUnsafe(
        EarningStatusOperation operation,
        ObservedEarningStatus result)
    {
        if (ReferenceEquals(status_operation, operation))
            status_operation = null;
        operation.Completion.TrySetResult(result);
        Pulse(operation.Signal);
    }

    private void CompleteStatusUnsafe(EarningStatusOperation operation, Exception error)
    {
        if (ReferenceEquals(status_operation, operation))
            status_operation = null;
        operation.Completion.TrySetException(error);
        Pulse(operation.Signal);
    }

    private void CompleteClaimUnsafe(
        EarningClaimQueue queue,
        EarningClaimOperation operation,
        ObservedEarningClaim result)
    {
        RemoveClaimUnsafe(queue, operation);
        operation.Completion.TrySetResult(result);
        Pulse(queue.Signal);
    }

    private void CompleteClaimUnsafe(
        EarningClaimQueue queue,
        EarningClaimOperation operation,
        Exception error)
    {
        RemoveClaimUnsafe(queue, operation);
        operation.Completion.TrySetException(error);
        Pulse(queue.Signal);
    }

    private static void RemoveClaimUnsafe(
        EarningClaimQueue queue,
        EarningClaimOperation operation)
    {
        if (operation.Node?.List is not null)
            queue.Operations.Remove(operation.Node);
        operation.Node = null;
    }

    private void DisconnectClaimQueueUnsafe(
        EarningClaimQueue queue,
        Exception? failure = null)
    {
        Exception error = failure ?? DisconnectClaim();
        foreach (EarningClaimOperation operation in queue.Operations.ToArray())
        {
            operation.Node = null;
            operation.Completion.TrySetException(error);
        }
        queue.Operations.Clear();
        queue.DriverRunning = false;
        Pulse(queue.Signal);
        if (claim_queues.TryGetValue(queue.Category, out EarningClaimQueue? current) &&
            ReferenceEquals(current, queue))
        {
            claim_queues.Remove(queue.Category);
        }
    }

    private static void PruneClaimQueueUnsafe(EarningClaimQueue queue)
    {
        LinkedListNode<EarningClaimOperation>? node = queue.Operations.First;
        while (node is not null)
        {
            LinkedListNode<EarningClaimOperation>? next = node.Next;
            EarningClaimOperation operation = node.Value;
            if (operation.Abandoned || operation.Completion.Task.IsCompleted)
            {
                queue.Operations.Remove(node);
                operation.Node = null;
            }
            node = next;
        }
    }

    private static bool ClaimOperationCurrent(
        EarningClaimQueue queue,
        EarningClaimOperation operation) =>
        ReferenceEquals(queue.Operations.First?.Value, operation) &&
        !operation.Abandoned &&
        !operation.Completion.Task.IsCompleted;

    private bool HasLiveStatusWaiterUnsafe(EarningStatusOperation operation) =>
        operation.Waiters.Any(StatusWaiterLiveUnsafe);

    private bool StatusEnsureOnlyUnsafe(EarningStatusOperation operation) =>
        operation.Waiters
            .Where(StatusWaiterLiveUnsafe)
            .All(waiter => waiter.EnsureOnly);

    private void AssignStatusDispatchCreditUnsafe(EarningStatusOperation operation)
    {
        EarningStatusWaiter[] refresh_waiters = operation.Waiters
            .Where(waiter => StatusWaiterLiveUnsafe(waiter) && !waiter.EnsureOnly)
            .ToArray();
        if (refresh_waiters.Length == 0 ||
            refresh_waiters.Any(waiter => waiter.DispatchCredit))
        {
            return;
        }
        refresh_waiters[0].DispatchCredit = true;
    }

    private bool StatusWaiterLiveUnsafe(EarningStatusWaiter waiter) =>
        waiter.Active &&
        !waiter.CancellationToken.IsCancellationRequested &&
        time_provider.GetUtcNow() < waiter.DeadlineUtc;

    private bool ClaimCallerLiveUnsafe(EarningClaimOperation operation) =>
        operation.CallerActive &&
        !operation.CancellationToken.IsCancellationRequested &&
        time_provider.GetUtcNow() < operation.DeadlineUtc;

    private void ClearOperationState()
    {
        lock (operation_sync)
        {
            var disposed = new ObjectDisposedException(nameof(EarningApplication));
            if (status_operation is { } status)
                CompleteStatusUnsafe(status, disposed);
            foreach (EarningClaimQueue queue in claim_queues.Values.ToArray())
                DisconnectClaimQueueUnsafe(queue, disposed);
            claim_queues.Clear();
        }
    }

    private EarningSessionScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedSessionGeneration(expected_session_generation);
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        EarningState state = earnings.State;
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The earning state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active earning session generation does not match the expected generation.");
        }
        return new EarningSessionScope(session, state.SessionGeneration);
    }

    private void RequireScope(EarningSessionScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeMatches(scope, earnings.State))
            throw DisconnectStatus();
    }

    private void RequireClaimScope(EarningSessionScope scope)
    {
        ThrowIfDisposed();
        if (!ScopeMatches(scope, earnings.State))
            throw DisconnectClaim();
    }

    private bool ScopeMatches(EarningSessionScope scope, EarningState state) =>
        scope.Matches(connection.Session, state.Session, state.SessionGeneration);

    private bool StatusScopeCurrent(EarningSessionScope scope) =>
        ScopeMatches(scope, earnings.State) && !DisposalStarted();

    private bool ClaimScopeCurrent(EarningSessionScope scope) =>
        ScopeMatches(scope, earnings.State) && !DisposalStarted();

    private bool NotificationScopeCurrent(
        Session expected_session,
        long expected_session_generation)
    {
        EarningState current = earnings.State;
        return !DisposalStarted() &&
            ReferenceEquals(connection.Session, expected_session) &&
            ReferenceEquals(current.Session, expected_session) &&
            current.SessionGeneration == expected_session_generation &&
            current.Loaded &&
            earnings.RefreshOnNotification;
    }

    private async Task<bool> WaitSignal(SemaphoreSlim signal)
    {
        try
        {
            await signal.WaitAsync(lifetime.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            return false;
        }
    }

    private static void Pulse(SemaphoreSlim signal)
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private Exception NormalizeStatusFailure(
        Exception error,
        EarningSessionScope scope)
    {
        if (DisposalStarted() || error is ObjectDisposedException or OperationCanceledException)
            return new ObjectDisposedException(nameof(EarningApplication), error);
        return StatusScopeCurrent(scope) ? error : DisconnectStatus();
    }

    private Exception NormalizeClaimFailure(
        Exception error,
        EarningSessionScope scope)
    {
        if (DisposalStarted() || error is ObjectDisposedException or OperationCanceledException)
            return new ObjectDisposedException(nameof(EarningApplication), error);
        return ClaimScopeCurrent(scope) ? error : DisconnectClaim();
    }

    private static RequestDisconnectedException DisconnectStatus() => new(
        MessageKeys.Earnings.StatusRequest.ToString(),
        MessageKeys.Earnings.StatusSnapshot.ToString());

    private static RequestDisconnectedException DisconnectClaim() => new(
        MessageKeys.Earnings.Claim.ToString(),
        MessageKeys.Earnings.Claimed.ToString());

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

    private static void ValidateOperationTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateCategory(int category)
    {
        if (category is < sbyte.MinValue or > sbyte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(category));
    }

    private readonly record struct EarningSessionScope(
        Session Session,
        long SessionGeneration)
    {
        public bool Matches(EarningSessionScope other) =>
            ReferenceEquals(Session, other.Session) &&
            SessionGeneration == other.SessionGeneration;

        public bool Matches(Session? connection_session, Session? state_session, long generation) =>
            ReferenceEquals(Session, connection_session) &&
            ReferenceEquals(Session, state_session) &&
            SessionGeneration == generation;

        public bool Matches(EarningState state) =>
            ReferenceEquals(Session, state.Session) && SessionGeneration == state.SessionGeneration;
    }

    private sealed class EarningStatusOperation(EarningSessionScope scope)
    {
        public EarningSessionScope Scope { get; } = scope;
        public List<EarningStatusWaiter> Waiters { get; } = [];
        public long RequestBaseline { get; set; }
        public long SourceBaseline { get; set; }
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool Dispatching { get; set; }
        public bool GuardStarted { get; set; }
        public bool RequestCounted { get; set; }
        public bool Dispatched { get; set; }
        public bool Abandoned { get; set; }
        public DateTimeOffset DispatchedAtUtc { get; set; }
        public SemaphoreSlim Signal { get; } = new(0, 1);
        public TaskCompletionSource<ObservedEarningStatus> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class EarningStatusWaiter(
        bool ensure_only,
        DateTimeOffset deadline_utc,
        CancellationToken cancellation_token)
    {
        public bool EnsureOnly { get; } = ensure_only;
        public DateTimeOffset DeadlineUtc { get; } = deadline_utc;
        public CancellationToken CancellationToken { get; } = cancellation_token;
        public bool Active { get; set; } = true;
        public bool DispatchCredit { get; set; }
    }

    private sealed class EarningClaimQueue(
        int category,
        EarningSessionScope scope)
    {
        public int Category { get; } = category;
        public EarningSessionScope Scope { get; } = scope;
        public LinkedList<EarningClaimOperation> Operations { get; } = [];
        public SemaphoreSlim Signal { get; } = new(0, 1);
        public bool DriverRunning { get; set; }
    }

    private sealed class EarningClaimOperation(
        int category,
        EarningSessionScope scope,
        DateTimeOffset deadline_utc,
        CancellationToken cancellation_token)
    {
        public int Category { get; } = category;
        public EarningSessionScope Scope { get; } = scope;
        public DateTimeOffset DeadlineUtc { get; } = deadline_utc;
        public CancellationToken CancellationToken { get; } = cancellation_token;
        public LinkedListNode<EarningClaimOperation>? Node { get; set; }
        public long RequestBaseline { get; set; }
        public long SourceBaseline { get; set; }
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool Dispatching { get; set; }
        public bool GuardStarted { get; set; }
        public bool RequestCounted { get; set; }
        public bool Dispatched { get; set; }
        public bool Abandoned { get; set; }
        public bool CallerActive { get; set; } = true;
        public DateTimeOffset DispatchedAtUtc { get; set; }
        public TaskCompletionSource<ObservedEarningClaim> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record ObservedEarningStatus(
        EarningState State,
        DateTimeOffset ObservedAtUtc);

    private sealed record ObservedEarningClaim(
        EarningState State,
        EarningClaimResult Result,
        DateTimeOffset ObservedAtUtc);

    private sealed class EarningDispatchAbandonedException : Exception
    {
    }

    private sealed class EarningStatusAlreadyLoadedException(EarningState state) : Exception
    {
        public EarningState State { get; } = state;
    }

    private sealed class EarningNotificationRequestSkippedException : Exception
    {
    }
}
