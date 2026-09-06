using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class QuestApplication
{
    private readonly object operation_sync = new();
    private DailyQueueContext daily_queue = new();
    private QuestRouteOperation? available_operation;
    private QuestRouteOperation? seasonal_operation;

    public ValueTask<QuestAvailableRefreshResult> RefreshAvailable(
        QuestAvailableRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshAvailableCore(request, token));

    private async ValueTask<QuestAvailableRefreshResult> RefreshAvailableCore(
        QuestAvailableRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (QuestRouteOperation operation, QuestRouteWaiter waiter) = AcquireFixedOperation(
            QuestRequestRoute.Available,
            scope,
            false,
            request.TimeoutMilliseconds,
            cancellation_token);
        ObservedQuestRoute observed = await WaitRouteOperation(
            operation,
            waiter,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope, QuestRequestRoute.Available);
        QuestSnapshotLease lease = StoreLease(observed.State);
        try
        {
            QuestEntryPage first_page = EntryPageFor(
                lease,
                QuestCollection.Available,
                0,
                request.Limit);
            var result = new QuestAvailableRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.AvailableRevision,
                lease.Revision,
                waiter.DispatchCredit ? 1 : 0,
                first_page);
            RequireScope(scope, QuestRequestRoute.Available);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<QuestSeasonalRefreshResult> RefreshSeasonal(
        QuestSeasonalRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshSeasonalCore(request, token));

    private async ValueTask<QuestSeasonalRefreshResult> RefreshSeasonalCore(
        QuestSeasonalRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (QuestRouteOperation operation, QuestRouteWaiter waiter) = AcquireFixedOperation(
            QuestRequestRoute.Seasonal,
            scope,
            false,
            request.TimeoutMilliseconds,
            cancellation_token);
        ObservedQuestRoute observed = await WaitRouteOperation(
            operation,
            waiter,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope, QuestRequestRoute.Seasonal);
        QuestSnapshotLease lease = StoreLease(observed.State);
        try
        {
            QuestEntryPage first_page = EntryPageFor(
                lease,
                QuestCollection.Seasonal,
                0,
                request.Limit);
            var result = new QuestSeasonalRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.SeasonalRevision,
                lease.Revision,
                waiter.DispatchCredit ? 1 : 0,
                first_page);
            RequireScope(scope, QuestRequestRoute.Seasonal);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<QuestDailyRefreshResult> RefreshDaily(
        QuestDailyRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshDailyCore(request, token));

    private async ValueTask<QuestDailyRefreshResult> RefreshDailyCore(
        QuestDailyRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (QuestRouteOperation operation, QuestRouteWaiter waiter) = AcquireDailyOperation(
            scope,
            request.IsEasy,
            request.Index,
            request.TimeoutMilliseconds,
            cancellation_token);
        ObservedQuestRoute observed = await WaitRouteOperation(
            operation,
            waiter,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope, QuestRequestRoute.Daily);
        QuestDaily daily = observed.State.Daily ??
            throw new InvalidOperationException("The correlated daily quest response is missing.");
        QuestSnapshotLease lease = StoreLease(observed.State);
        try
        {
            var result = new QuestDailyRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.DailyRevision,
                lease.Revision,
                waiter.DispatchCredit ? 1 : 0,
                View(daily));
            RequireScope(scope, QuestRequestRoute.Daily);
            return result;
        }
        catch
        {
            RemoveLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<QuestSelectionDispatchReceipt> Accept(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchAccept(request, token)));

    public ValueTask<QuestSelectionDispatchReceipt> Activate(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchActivate(request, token)));

    public ValueTask<QuestSelectionDispatchReceipt> Reject(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchReject(request, token)));

    public ValueTask<QuestDispatchReceipt> Cancel(
        QuestDispatchRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchCancel(request, token)));

    public ValueTask<QuestDispatchReceipt> OpenTracker(
        QuestDispatchRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchTracker(request, token)));

    public ValueTask<QuestDispatchReceipt> CompleteFriendRequestQuest(
        QuestDispatchRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => ValueTask.FromResult(DispatchFriendRequest(request, token)));

    void IQuestOperations.RequestAvailable() => InvokeLegacy(
        cancellation_token => DispatchLegacyRequest(
            QuestRequestRoute.Available,
            cancellation_token));

    Task<IReadOnlyList<Qx.Model.Quests.QuestData>> IQuestOperations.EnsureAvailableLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsureAvailableLoadedCore(timeout_milliseconds, token)).AsTask();

    void IQuestOperations.RequestSeasonal() => InvokeLegacy(
        cancellation_token => DispatchLegacyRequest(
            QuestRequestRoute.Seasonal,
            cancellation_token));

    void IQuestOperations.RequestDaily(bool is_easy, int index) => InvokeLegacy(
        cancellation_token => DispatchLegacyDaily(is_easy, index, cancellation_token));

    void IQuestOperations.Accept(Id quest_id) => InvokeLegacy(
        cancellation_token => DispatchLegacyAccept(quest_id, cancellation_token));

    void IQuestOperations.Activate(Id quest_id) => InvokeLegacy(
        cancellation_token => DispatchLegacyActivate(quest_id, cancellation_token));

    void IQuestOperations.Reject(Id quest_id) => InvokeLegacy(
        cancellation_token => DispatchLegacyReject(quest_id, cancellation_token));

    void IQuestOperations.Cancel() => InvokeLegacy(DispatchLegacyCancel);

    void IQuestOperations.OpenTracker() => InvokeLegacy(DispatchLegacyTracker);

    void IQuestOperations.CompleteFriendRequestQuest() =>
        InvokeLegacy(DispatchLegacyFriendRequest);

    private async ValueTask<IReadOnlyList<Qx.Model.Quests.QuestData>> EnsureAvailableLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        QuestState current = quests.State;
        if (scope.Matches(current) && current.AvailableLoaded)
            return current.Available.ToArray();
        (QuestRouteOperation operation, QuestRouteWaiter waiter) = AcquireFixedOperation(
            QuestRequestRoute.Available,
            scope,
            true,
            timeout_milliseconds,
            cancellation_token);
        ObservedQuestRoute observed = await WaitRouteOperation(
            operation,
            waiter,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireScope(scope, QuestRequestRoute.Available);
        return observed.State.Available.ToArray();
    }

    private void DispatchLegacyRequest(
        QuestRequestRoute route,
        CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchRequest(
            route,
            false,
            0,
            scope,
            cancellation_token,
            () => quests.AdvanceLegacyRequest(
                route,
                scope.Session,
                scope.SessionGeneration));
    }

    private void DispatchLegacyDaily(
        bool is_easy,
        int index,
        CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchRequest(
            QuestRequestRoute.Daily,
            is_easy,
            index,
            scope,
            cancellation_token,
            () => quests.AdvanceLegacyRequest(
                QuestRequestRoute.Daily,
                scope.Session,
                scope.SessionGeneration));
    }

    private QuestSelectionDispatchReceipt DispatchAccept(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Accept,
            new AcceptQuest(request.QuestId),
            scope,
            cancellation_token);
        return SelectionReceipt(scope, request.QuestId);
    }

    private QuestSelectionDispatchReceipt DispatchActivate(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Activate,
            new ActivateQuest(request.QuestId),
            scope,
            cancellation_token);
        return SelectionReceipt(scope, request.QuestId);
    }

    private QuestSelectionDispatchReceipt DispatchReject(
        QuestSelectionActionRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Reject,
            new RejectQuest(request.QuestId),
            scope,
            cancellation_token);
        return SelectionReceipt(scope, request.QuestId);
    }

    private QuestDispatchReceipt DispatchCancel(
        QuestDispatchRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.Cancel,
            new CancelQuest(),
            scope,
            cancellation_token);
        return DispatchReceipt(scope);
    }

    private QuestDispatchReceipt DispatchTracker(
        QuestDispatchRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.TrackerOpen,
            new OpenQuestTracker(),
            scope,
            cancellation_token);
        return DispatchReceipt(scope);
    }

    private QuestDispatchReceipt DispatchFriendRequest(
        QuestDispatchRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        QuestSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.FriendRequestCompleted,
            new FriendRequestQuestComplete(),
            scope,
            cancellation_token);
        return DispatchReceipt(scope);
    }

    private void DispatchLegacyAccept(Id quest_id, CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Accept,
            new AcceptQuest(quest_id),
            scope,
            cancellation_token);
    }

    private void DispatchLegacyActivate(Id quest_id, CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Activate,
            new ActivateQuest(quest_id),
            scope,
            cancellation_token);
    }

    private void DispatchLegacyReject(Id quest_id, CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchSelection(
            MessageContracts.Quests.Reject,
            new RejectQuest(quest_id),
            scope,
            cancellation_token);
    }

    private void DispatchLegacyCancel(CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.Cancel,
            new CancelQuest(),
            scope,
            cancellation_token);
    }

    private void DispatchLegacyTracker(CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.TrackerOpen,
            new OpenQuestTracker(),
            scope,
            cancellation_token);
    }

    private void DispatchLegacyFriendRequest(CancellationToken cancellation_token)
    {
        QuestSessionScope scope = CaptureScope(null, cancellation_token);
        DispatchEmpty(
            MessageContracts.Quests.FriendRequestCompleted,
            new FriendRequestQuestComplete(),
            scope,
            cancellation_token);
    }

    private void DispatchSelection<T>(
        MessageContract<T> contract,
        T message,
        QuestSessionScope scope,
        CancellationToken cancellation_token)
        where T : IParserComposer<T> => message_dispatcher.Dispatch(
            contract,
            message,
            scope.Session,
            cancellation_token,
            () =>
            {
                cancellation_token.ThrowIfCancellationRequested();
                RequireScope(scope, QuestRequestRoute.Available);
            });

    private void DispatchEmpty<T>(
        MessageContract<T> contract,
        T message,
        QuestSessionScope scope,
        CancellationToken cancellation_token)
        where T : IParserComposer<T> => DispatchSelection(
            contract,
            message,
            scope,
            cancellation_token);

    private QuestSelectionDispatchReceipt SelectionReceipt(
        QuestSessionScope scope,
        long quest_id) => new(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            scope.SessionGeneration,
            quest_id,
            1);

    private QuestDispatchReceipt DispatchReceipt(QuestSessionScope scope) => new(
        scope.Session.Client,
        time_provider.GetUtcNow(),
        scope.SessionGeneration,
        1);

    private (
        QuestRouteOperation Operation,
        QuestRouteWaiter Waiter) AcquireFixedOperation(
        QuestRequestRoute route,
        QuestSessionScope scope,
        bool ensure_only,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        QuestRouteOperation operation;
        var waiter = new QuestRouteWaiter(
            ensure_only,
            time_provider.GetUtcNow().AddMilliseconds(timeout_milliseconds),
            cancellation_token);
        bool created = false;
        lock (operation_sync)
        {
            ThrowIfDisposed();
            operation = FixedOperation(route)!;
            if (operation is not null && !operation.Scope.Matches(scope))
            {
                CompleteFixedUnsafe(operation, Disconnect(route));
                operation = null!;
            }
            if (operation is not null)
            {
                bool promoted = !ensure_only &&
                    EnsureOnlyUnsafe(operation) &&
                    !operation.RequestCounted;
                waiter.DispatchCredit = promoted;
                operation.Waiters.Add(waiter);
                Pulse(operation.Signal);
                return (operation, waiter);
            }
            if (!ScopeMatches(scope, quests.State))
                throw Disconnect(route);
            operation = new QuestRouteOperation(
                route,
                scope,
                false,
                0);
            waiter.DispatchCredit = !ensure_only;
            operation.Waiters.Add(waiter);
            SetFixedOperation(route, operation);
            created = true;
        }
        if (created)
            _ = DriveFixedOperation(operation);
        return (operation, waiter);
    }

    private (
        QuestRouteOperation Operation,
        QuestRouteWaiter Waiter) AcquireDailyOperation(
        QuestSessionScope scope,
        bool is_easy,
        int index,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        QuestRouteOperation operation;
        var waiter = new QuestRouteWaiter(
            false,
            time_provider.GetUtcNow().AddMilliseconds(timeout_milliseconds),
            cancellation_token);
        bool start;
        DailyQueueContext queue;
        lock (operation_sync)
        {
            ThrowIfDisposed();
            if (!ScopeMatches(scope, quests.State))
                throw Disconnect(QuestRequestRoute.Daily);
            queue = daily_queue;
            operation = queue.Operations.FirstOrDefault(candidate =>
                candidate.Scope.Matches(scope) &&
                candidate.IsEasy == is_easy &&
                candidate.Index == index &&
                !candidate.Abandoned &&
                !candidate.Completion.Task.IsCompleted)!;
            if (operation is not null)
            {
                operation.Waiters.Add(waiter);
            }
            else
            {
                operation = new QuestRouteOperation(
                    QuestRequestRoute.Daily,
                    scope,
                    is_easy,
                    index)
                {
                    DailyQueue = queue
                };
                waiter.DispatchCredit = true;
                operation.Waiters.Add(waiter);
                operation.Node = queue.Operations.AddLast(operation);
            }
            start = !queue.DriverRunning;
            if (start)
                queue.DriverRunning = true;
            Pulse(queue.Signal);
        }
        if (start)
            _ = DriveDailyQueue(queue);
        return (operation, waiter);
    }

    private async Task DriveFixedOperation(QuestRouteOperation operation)
    {
        while (true)
        {
            QuestRequestCorrelation correlation;
            try
            {
                correlation = quests.CaptureRequestCorrelation(
                    operation.Route,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration);
            }
            catch (Exception error)
            {
                lock (operation_sync)
                {
                    if (FixedOperationCurrent(operation))
                        CompleteFixedUnsafe(operation, NormalizeFailure(error, operation));
                }
                return;
            }
            bool dispatch;
            lock (operation_sync)
            {
                if (!FixedOperationCurrent(operation))
                    return;
                if (EnsureOnlyUnsafe(operation) && Loaded(operation.Route, correlation.State))
                {
                    CompleteFixedUnsafe(
                        operation,
                        new ObservedQuestRoute(correlation.State, time_provider.GetUtcNow()));
                    return;
                }
                if (!HasLiveWaiterUnsafe(operation) &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted)
                {
                    RemoveFixedUnsafe(operation);
                    operation.Abandoned = true;
                    Pulse(operation.Signal);
                    return;
                }
                dispatch = correlation.OutstandingRequests == 0 && !operation.Dispatching;
                if (dispatch)
                {
                    operation.Dispatching = true;
                    operation.RequestBaseline = correlation.RequestEpoch;
                    operation.SourceBaseline = SourceRevision(operation.Route, correlation.State);
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
                DispatchRequest(
                    operation.Route,
                    operation.IsEasy,
                    operation.Index,
                    operation.Scope,
                    lifetime.Token,
                    () =>
                    {
                        guard_started = true;
                        ArmRouteDispatch(operation, correlation.RequestEpoch);
                    });
                MarkDispatched(operation);
                return;
            }
            catch (QuestAlreadyLoadedException loaded)
            {
                lock (operation_sync)
                {
                    if (!FixedOperationCurrent(operation))
                        return;
                    ResetDispatchUnsafe(operation);
                    if (!HasLiveWaiterUnsafe(operation))
                    {
                        RemoveFixedUnsafe(operation);
                        operation.Abandoned = true;
                        return;
                    }
                    if (EnsureOnlyUnsafe(operation))
                    {
                        CompleteFixedUnsafe(
                            operation,
                            new ObservedQuestRoute(loaded.State, time_provider.GetUtcNow()));
                        return;
                    }
                    Pulse(operation.Signal);
                }
            }
            catch (QuestDispatchAbandonedException)
            {
                return;
            }
            catch (InvalidOperationException) when (
                guard_started &&
                !RequestCounted(operation) &&
                ScopeCurrent(operation.Scope))
            {
                ResetDispatch(operation);
            }
            catch (Exception error)
            {
                lock (operation_sync)
                {
                    if (FixedOperationCurrent(operation))
                        CompleteFixedUnsafe(operation, NormalizeFailure(error, operation));
                }
                return;
            }
        }
    }

    private async Task DriveDailyQueue(DailyQueueContext queue)
    {
        while (true)
        {
            QuestRouteOperation? operation;
            lock (operation_sync)
            {
                if (queue.Retired)
                    return;
                PruneDailyUnsafe(queue);
                operation = queue.Operations.First?.Value;
                if (operation is null)
                {
                    queue.DriverRunning = false;
                    return;
                }
            }
            QuestRequestCorrelation correlation;
            try
            {
                correlation = quests.CaptureRequestCorrelation(
                    QuestRequestRoute.Daily,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration);
            }
            catch (Exception error)
            {
                lock (operation_sync)
                    DisconnectDailyUnsafe(queue, NormalizeFailure(error, operation));
                return;
            }
            bool dispatch;
            lock (operation_sync)
            {
                if (!DailyOperationCurrent(queue, operation))
                    continue;
                if (!HasLiveWaiterUnsafe(operation) &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted)
                {
                    operation.Abandoned = true;
                    RemoveDailyUnsafe(operation);
                    Pulse(queue.Signal);
                    continue;
                }
                dispatch = correlation.OutstandingRequests == 0 && !operation.Dispatching;
                if (dispatch)
                {
                    operation.Dispatching = true;
                    operation.RequestBaseline = correlation.RequestEpoch;
                    operation.SourceBaseline = correlation.State.DailyRevision;
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
                DispatchRequest(
                    QuestRequestRoute.Daily,
                    operation.IsEasy,
                    operation.Index,
                    operation.Scope,
                    lifetime.Token,
                    () =>
                    {
                        guard_started = true;
                        ArmRouteDispatch(operation, correlation.RequestEpoch);
                    });
                MarkDispatched(operation);
            }
            catch (QuestDispatchAbandonedException)
            {
                continue;
            }
            catch (InvalidOperationException) when (
                guard_started &&
                !RequestCounted(operation) &&
                ScopeCurrent(operation.Scope))
            {
                ResetDispatch(operation);
                continue;
            }
            catch (Exception error)
            {
                lock (operation_sync)
                {
                    if (DailyOperationCurrent(queue, operation))
                        CompleteDailyUnsafe(operation, NormalizeFailure(error, operation));
                }
                continue;
            }
            if (!await WaitSignal(queue.Signal).ConfigureAwait(false))
                return;
        }
    }

    private void DispatchRequest(
        QuestRequestRoute route,
        bool is_easy,
        int index,
        QuestSessionScope scope,
        CancellationToken cancellation_token,
        Action dispatch_guard)
    {
        switch (route)
        {
            case QuestRequestRoute.Available:
                message_dispatcher.Dispatch(
                    MessageContracts.Quests.Request,
                    new GetQuests(),
                    scope.Session,
                    cancellation_token,
                    dispatch_guard);
                break;
            case QuestRequestRoute.Seasonal:
                message_dispatcher.Dispatch(
                    MessageContracts.Quests.SeasonalRequest,
                    new GetSeasonalQuests(),
                    scope.Session,
                    cancellation_token,
                    dispatch_guard);
                break;
            case QuestRequestRoute.Daily:
                message_dispatcher.Dispatch(
                    MessageContracts.Quests.DailyRequest,
                    new GetDailyQuest(is_easy, index),
                    scope.Session,
                    cancellation_token,
                    dispatch_guard);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }
    }

    private void ArmRouteDispatch(QuestRouteOperation operation, long baseline)
    {
        bool ensure_only = BeginGuard(operation);
        long request_epoch;
        if (ensure_only)
        {
            bool advanced = quests.TryAdvanceTypedRequestIfUnloaded(
                operation.Route,
                baseline,
                operation.Scope.Session,
                operation.Scope.SessionGeneration,
                out request_epoch,
                out QuestState current);
            if (!advanced)
            {
                lock (operation_sync)
                {
                    if (!OperationCurrent(operation))
                        throw new QuestDispatchAbandonedException();
                    if (EnsureOnlyUnsafe(operation))
                        throw new QuestAlreadyLoadedException(current);
                }
                request_epoch = quests.AdvanceTypedRequest(
                    operation.Route,
                    baseline,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration);
            }
        }
        else
        {
            request_epoch = quests.AdvanceTypedRequest(
                operation.Route,
                baseline,
                operation.Scope.Session,
                operation.Scope.SessionGeneration);
        }
        lock (operation_sync)
        {
            if (!OperationCurrent(operation))
                return;
            operation.ExpectedRequestEpoch = request_epoch;
            operation.RequestCounted = true;
            operation.DispatchedAtUtc = time_provider.GetUtcNow();
        }
        QuestRequestCorrelation current_correlation = quests.CaptureRequestCorrelation(
            operation.Route,
            operation.Scope.Session,
            operation.Scope.SessionGeneration);
        lock (operation_sync)
        {
            if (!OperationCurrent(operation))
                return;
            if (current_correlation.RequestEpoch != request_epoch ||
                current_correlation.OutstandingRequests != 1)
            {
                CompleteUnsafe(
                    operation,
                    new InvalidOperationException(
                        "The quest request was invalidated by another dispatch."));
            }
        }
    }

    private bool BeginGuard(QuestRouteOperation operation)
    {
        lock (operation_sync)
        {
            if (!OperationCurrent(operation) || !HasLiveWaiterUnsafe(operation))
            {
                if (OperationCurrent(operation) && !operation.RequestCounted)
                {
                    RemoveOperationUnsafe(operation);
                    operation.Dispatching = false;
                    operation.Abandoned = true;
                    PulseOperation(operation);
                }
                throw new QuestDispatchAbandonedException();
            }
            AssignDispatchCreditUnsafe(operation);
            operation.GuardStarted = true;
            return EnsureOnlyUnsafe(operation);
        }
    }

    private void MarkDispatched(QuestRouteOperation operation)
    {
        lock (operation_sync)
        {
            operation.Dispatching = false;
            operation.Dispatched = true;
            PulseOperation(operation);
        }
    }

    private void ResetDispatch(QuestRouteOperation operation)
    {
        lock (operation_sync)
        {
            if (!OperationCurrent(operation))
                return;
            ResetDispatchUnsafe(operation);
            if (!HasLiveWaiterUnsafe(operation) && !operation.RequestCounted)
            {
                RemoveOperationUnsafe(operation);
                operation.Abandoned = true;
            }
            PulseOperation(operation);
        }
    }

    private static void ResetDispatchUnsafe(QuestRouteOperation operation)
    {
        operation.Dispatching = false;
        operation.ExpectedRequestEpoch = -1;
        operation.GuardStarted = false;
    }

    private bool RequestCounted(QuestRouteOperation operation)
    {
        lock (operation_sync)
            return operation.RequestCounted;
    }

    private async Task<ObservedQuestRoute> WaitRouteOperation(
        QuestRouteOperation operation,
        QuestRouteWaiter waiter,
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
                RequestKey(operation.Route).ToString(),
                ResponseKey(operation.Route).ToString(),
                timeout_milliseconds);
        }
        finally
        {
            lock (operation_sync)
            {
                waiter.Active = false;
                operation.Waiters.Remove(waiter);
                if (!HasLiveWaiterUnsafe(operation) &&
                    !operation.Completion.Task.IsCompleted &&
                    !operation.RequestCounted &&
                    !operation.GuardStarted &&
                    OperationCurrent(operation))
                {
                    RemoveOperationUnsafe(operation);
                    operation.Dispatching = false;
                    operation.Abandoned = true;
                }
                PulseOperation(operation);
            }
        }
    }

    private void ObserveCommit(QuestStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            ObserveCommitCore(update);
    }

    private void ObserveCommitCore(QuestStateUpdate update)
    {
        lock (operation_sync)
        {
            if (update.Kind is QuestStateChangeKind.Reset)
            {
                ClearLeases();
                if (available_operation is { } available)
                    CompleteFixedUnsafe(available, Disconnect(QuestRequestRoute.Available));
                if (seasonal_operation is { } seasonal)
                    CompleteFixedUnsafe(seasonal, Disconnect(QuestRequestRoute.Seasonal));
                DisconnectDailyUnsafe(Disconnect(QuestRequestRoute.Daily));
                return;
            }
            if (update.Kind is QuestStateChangeKind.Request &&
                update.Route is QuestRequestRoute request_route)
            {
                if (request_route is QuestRequestRoute.Daily)
                {
                    DailyQueueContext queue = daily_queue;
                    QuestRouteOperation? operation = queue.Operations.First?.Value;
                    Pulse(queue.Signal);
                    if (operation is not null &&
                        operation.ExpectedRequestEpoch > 0 &&
                        update.RequestEpoch != operation.ExpectedRequestEpoch)
                    {
                        CompleteDailyUnsafe(
                            operation,
                            new InvalidOperationException(
                                "The daily quest request was invalidated by another dispatch."));
                    }
                }
                else
                {
                    QuestRouteOperation? operation = FixedOperation(request_route);
                    if (operation is not null)
                    {
                        Pulse(operation.Signal);
                        if (operation.ExpectedRequestEpoch > 0 &&
                            update.RequestEpoch != operation.ExpectedRequestEpoch)
                        {
                            CompleteFixedUnsafe(
                                operation,
                                new InvalidOperationException(
                                    "The quest request was invalidated by another dispatch."));
                        }
                    }
                }
                return;
            }
            QuestRequestRoute? response_route = update.Kind switch
            {
                QuestStateChangeKind.Available => QuestRequestRoute.Available,
                QuestStateChangeKind.Seasonal => QuestRequestRoute.Seasonal,
                QuestStateChangeKind.Daily => QuestRequestRoute.Daily,
                _ => null
            };
            if (response_route is not QuestRequestRoute route)
                return;
            QuestRouteOperation? current = route is QuestRequestRoute.Daily
                ? daily_queue.Operations.First?.Value
                : FixedOperation(route);
            PulseRoute(route, current);
            if (current is null ||
                current.ExpectedRequestEpoch <= 0 ||
                update.RequestEpoch != current.ExpectedRequestEpoch ||
                !current.Scope.Matches(update.State) ||
                SourceRevision(route, update.State) <= current.SourceBaseline)
            {
                return;
            }
            var observed = new ObservedQuestRoute(update.State, time_provider.GetUtcNow());
            if (route is QuestRequestRoute.Daily)
                CompleteDailyUnsafe(current, observed);
            else
                CompleteFixedUnsafe(current, observed);
        }
    }

    private void CompleteFixedUnsafe(
        QuestRouteOperation operation,
        ObservedQuestRoute result)
    {
        RemoveFixedUnsafe(operation);
        operation.Completion.TrySetResult(result);
        Pulse(operation.Signal);
    }

    private void CompleteFixedUnsafe(QuestRouteOperation operation, Exception error)
    {
        RemoveFixedUnsafe(operation);
        operation.Completion.TrySetException(error);
        Pulse(operation.Signal);
    }

    private void CompleteDailyUnsafe(
        QuestRouteOperation operation,
        ObservedQuestRoute result)
    {
        RemoveDailyUnsafe(operation);
        operation.Completion.TrySetResult(result);
        Pulse(operation.DailyQueue?.Signal ?? daily_queue.Signal);
    }

    private void CompleteDailyUnsafe(QuestRouteOperation operation, Exception error)
    {
        RemoveDailyUnsafe(operation);
        operation.Completion.TrySetException(error);
        Pulse(operation.DailyQueue?.Signal ?? daily_queue.Signal);
    }

    private void CompleteUnsafe(QuestRouteOperation operation, Exception error)
    {
        if (operation.Route is QuestRequestRoute.Daily)
            CompleteDailyUnsafe(operation, error);
        else
            CompleteFixedUnsafe(operation, error);
    }

    private void DisconnectDailyUnsafe(Exception error)
    {
        DisconnectDailyUnsafe(daily_queue, error);
    }

    private void DisconnectDailyUnsafe(DailyQueueContext queue, Exception error)
    {
        if (queue.Retired)
            return;
        queue.Retired = true;
        if (ReferenceEquals(daily_queue, queue))
            daily_queue = new DailyQueueContext();
        foreach (QuestRouteOperation operation in queue.Operations.ToArray())
        {
            operation.Node = null;
            operation.Completion.TrySetException(error);
        }
        queue.Operations.Clear();
        queue.DriverRunning = false;
        Pulse(queue.Signal);
    }

    private void RemoveFixedUnsafe(QuestRouteOperation operation)
    {
        if (operation.Route is QuestRequestRoute.Available &&
            ReferenceEquals(available_operation, operation))
        {
            available_operation = null;
        }
        else if (operation.Route is QuestRequestRoute.Seasonal &&
            ReferenceEquals(seasonal_operation, operation))
        {
            seasonal_operation = null;
        }
    }

    private void RemoveDailyUnsafe(QuestRouteOperation operation)
    {
        if (operation.Node is { List: not null } node)
            node.List.Remove(node);
        operation.Node = null;
    }

    private void RemoveOperationUnsafe(QuestRouteOperation operation)
    {
        if (operation.Route is QuestRequestRoute.Daily)
            RemoveDailyUnsafe(operation);
        else
            RemoveFixedUnsafe(operation);
    }

    private static void PruneDailyUnsafe(DailyQueueContext queue)
    {
        LinkedListNode<QuestRouteOperation>? node = queue.Operations.First;
        while (node is not null)
        {
            LinkedListNode<QuestRouteOperation>? next = node.Next;
            QuestRouteOperation operation = node.Value;
            if (operation.Abandoned || operation.Completion.Task.IsCompleted)
            {
                queue.Operations.Remove(node);
                operation.Node = null;
            }
            node = next;
        }
    }

    private QuestRouteOperation? FixedOperation(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => available_operation,
        QuestRequestRoute.Seasonal => seasonal_operation,
        _ => null
    };

    private void SetFixedOperation(
        QuestRequestRoute route,
        QuestRouteOperation operation)
    {
        if (route is QuestRequestRoute.Available)
            available_operation = operation;
        else if (route is QuestRequestRoute.Seasonal)
            seasonal_operation = operation;
        else
            throw new ArgumentOutOfRangeException(nameof(route));
    }

    private bool FixedOperationCurrent(QuestRouteOperation operation) =>
        ReferenceEquals(FixedOperation(operation.Route), operation) &&
        !operation.Abandoned &&
        !operation.Completion.Task.IsCompleted;

    private bool DailyOperationCurrent(QuestRouteOperation operation) =>
        operation.DailyQueue is { } queue &&
        DailyOperationCurrent(queue, operation);

    private bool DailyOperationCurrent(
        DailyQueueContext queue,
        QuestRouteOperation operation) =>
        !queue.Retired &&
        ReferenceEquals(daily_queue, queue) &&
        ReferenceEquals(queue.Operations.First?.Value, operation) &&
        !operation.Abandoned &&
        !operation.Completion.Task.IsCompleted;

    private bool OperationCurrent(QuestRouteOperation operation) =>
        operation.Route is QuestRequestRoute.Daily
            ? DailyOperationCurrent(operation)
            : FixedOperationCurrent(operation);

    private bool HasLiveWaiterUnsafe(QuestRouteOperation operation) =>
        operation.Waiters.Any(WaiterLiveUnsafe);

    private bool EnsureOnlyUnsafe(QuestRouteOperation operation) =>
        operation.Waiters
            .Where(WaiterLiveUnsafe)
            .All(waiter => waiter.EnsureOnly);

    private void AssignDispatchCreditUnsafe(QuestRouteOperation operation)
    {
        QuestRouteWaiter[] refresh_waiters = operation.Waiters
            .Where(waiter => WaiterLiveUnsafe(waiter) && !waiter.EnsureOnly)
            .ToArray();
        if (refresh_waiters.Length == 0 ||
            refresh_waiters.Any(waiter => waiter.DispatchCredit))
        {
            return;
        }
        refresh_waiters[0].DispatchCredit = true;
    }

    private bool WaiterLiveUnsafe(QuestRouteWaiter waiter) =>
        waiter.Active &&
        !waiter.CancellationToken.IsCancellationRequested &&
        time_provider.GetUtcNow() < waiter.DeadlineUtc;

    private void ClearOperationState()
    {
        lock (operation_sync)
        {
            var disposed = new ObjectDisposedException(nameof(QuestApplication));
            if (available_operation is { } available)
                CompleteFixedUnsafe(available, disposed);
            if (seasonal_operation is { } seasonal)
                CompleteFixedUnsafe(seasonal, disposed);
            DisconnectDailyUnsafe(disposed);
        }
    }

    private QuestSessionScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedSessionGeneration(expected_session_generation);
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        QuestState state = quests.State;
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The quest state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active quest session generation does not match the expected generation.");
        }
        return new QuestSessionScope(session, state.SessionGeneration);
    }

    private void RequireScope(QuestSessionScope scope, QuestRequestRoute route)
    {
        ThrowIfDisposed();
        if (!ScopeCurrent(scope))
            throw Disconnect(route);
    }

    private bool ScopeCurrent(QuestSessionScope scope) =>
        !DisposalStarted() && ScopeMatches(scope, quests.State);

    private bool ScopeMatches(QuestSessionScope scope, QuestState state) =>
        scope.Matches(connection.Session, state);

    private Exception NormalizeFailure(Exception error, QuestRouteOperation operation)
    {
        if (DisposalStarted() || error is ObjectDisposedException or OperationCanceledException)
            return new ObjectDisposedException(nameof(QuestApplication), error);
        return ScopeCurrent(operation.Scope) ? error : Disconnect(operation.Route);
    }

    private static RequestDisconnectedException Disconnect(QuestRequestRoute route) => new(
        RequestKey(route).ToString(),
        ResponseKey(route).ToString());

    private static MessageKey RequestKey(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => MessageKeys.Quests.Request,
        QuestRequestRoute.Seasonal => MessageKeys.Quests.SeasonalRequest,
        QuestRequestRoute.Daily => MessageKeys.Quests.DailyRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private static MessageKey ResponseKey(QuestRequestRoute route) => route switch
    {
        QuestRequestRoute.Available => MessageKeys.Quests.Snapshot,
        QuestRequestRoute.Seasonal => MessageKeys.Quests.SeasonalSnapshot,
        QuestRequestRoute.Daily => MessageKeys.Quests.Daily,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private static bool Loaded(QuestRequestRoute route, QuestState state) => route switch
    {
        QuestRequestRoute.Available => state.AvailableLoaded,
        QuestRequestRoute.Seasonal => state.SeasonalLoaded,
        QuestRequestRoute.Daily => state.DailyLoaded,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private static long SourceRevision(QuestRequestRoute route, QuestState state) => route switch
    {
        QuestRequestRoute.Available => state.AvailableRevision,
        QuestRequestRoute.Seasonal => state.SeasonalRevision,
        QuestRequestRoute.Daily => state.DailyRevision,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

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

    private void PulseRoute(
        QuestRequestRoute route,
        QuestRouteOperation? operation)
    {
        if (route is QuestRequestRoute.Daily)
            Pulse(operation?.DailyQueue?.Signal ?? daily_queue.Signal);
        else if (operation is not null)
            Pulse(operation.Signal);
    }

    private void PulseOperation(QuestRouteOperation operation)
    {
        if (operation.Route is QuestRequestRoute.Daily)
            Pulse(operation.DailyQueue?.Signal ?? daily_queue.Signal);
        else
            Pulse(operation.Signal);
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

    private readonly record struct QuestSessionScope(
        Session Session,
        long SessionGeneration)
    {
        public bool Matches(QuestSessionScope other) =>
            ReferenceEquals(Session, other.Session) &&
            SessionGeneration == other.SessionGeneration;

        public bool Matches(QuestState state) =>
            ReferenceEquals(Session, state.Session) &&
            SessionGeneration == state.SessionGeneration;

        public bool Matches(Session? connection_session, QuestState state) =>
            ReferenceEquals(Session, connection_session) && Matches(state);
    }

    private sealed class QuestRouteOperation(
        QuestRequestRoute route,
        QuestSessionScope scope,
        bool is_easy,
        int index)
    {
        public QuestRequestRoute Route { get; } = route;
        public QuestSessionScope Scope { get; } = scope;
        public bool IsEasy { get; } = is_easy;
        public int Index { get; } = index;
        public List<QuestRouteWaiter> Waiters { get; } = [];
        public DailyQueueContext? DailyQueue { get; set; }
        public LinkedListNode<QuestRouteOperation>? Node { get; set; }
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
        public TaskCompletionSource<ObservedQuestRoute> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DailyQueueContext
    {
        public LinkedList<QuestRouteOperation> Operations { get; } = [];
        public SemaphoreSlim Signal { get; } = new(0, 1);
        public bool DriverRunning { get; set; }
        public bool Retired { get; set; }
    }

    private sealed class QuestRouteWaiter(
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

    private sealed record ObservedQuestRoute(
        QuestState State,
        DateTimeOffset ObservedAtUtc);

    private sealed class QuestDispatchAbandonedException : Exception
    {
    }

    private sealed class QuestAlreadyLoadedException(QuestState state) : Exception
    {
        public QuestState State { get; } = state;
    }
}
