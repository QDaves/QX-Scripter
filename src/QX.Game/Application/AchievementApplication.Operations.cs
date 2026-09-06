using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class AchievementApplication
{
    private static readonly TimeSpan badge_response_free_lease = TimeSpan.FromSeconds(30);
    private readonly object operation_sync = new();
    private AchievementRouteOperation? achievement_list_operation;
    private AchievementRouteOperation? achievement_point_limits_operation;
    private BadgeLoadOperation? badge_load_operation;

    public ValueTask<AchievementRefreshResult> RefreshAchievements(
        AchievementRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshAchievementsCore(request, token));

    private async ValueTask<AchievementRefreshResult> RefreshAchievementsCore(
        AchievementRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        DomainSessionScope scope = CaptureAchievementScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (AchievementRouteOperation operation, bool dispatch) =
            AcquireAchievementOperation(AchievementRequestRoute.List, scope);
        if (dispatch)
            DispatchAchievementOperation(operation, lifetime.Token);
        ObservedAchievementCommit observed = await WaitAchievementOperation(
            operation,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireAchievementScope(scope);
        AchievementSnapshotLease lease = StoreAchievementLease(observed.State);
        try
        {
            AchievementPage first_page = AchievementPageFor(lease, 0, request.Limit);
            var result = new AchievementRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.ListRevision,
                observed.State.BaselineRevision,
                lease.Revision,
                dispatch ? 1 : 0,
                first_page);
            RequireAchievementScope(scope);
            return result;
        }
        catch
        {
            RemoveAchievementLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<AchievementPointLimitsRefreshResult> RefreshAchievementPointLimits(
        AchievementPointLimitsRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshAchievementPointLimitsCore(request, token));

    private async ValueTask<AchievementPointLimitsRefreshResult>
        RefreshAchievementPointLimitsCore(
            AchievementPointLimitsRefreshRequest request,
            CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        DomainSessionScope scope = CaptureAchievementScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (AchievementRouteOperation operation, bool dispatch) =
            AcquireAchievementOperation(AchievementRequestRoute.PointLimits, scope);
        if (dispatch)
            DispatchAchievementOperation(operation, lifetime.Token);
        ObservedAchievementCommit observed = await WaitAchievementOperation(
            operation,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireAchievementScope(scope);
        AchievementSnapshotLease lease = StoreAchievementLease(observed.State);
        try
        {
            AchievementPointLimitPage first_page = AchievementPointLimitPageFor(
                lease,
                0,
                request.Limit);
            var result = new AchievementPointLimitsRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.PointLimitsRevision,
                lease.Revision,
                dispatch ? 1 : 0,
                first_page);
            RequireAchievementScope(scope);
            return result;
        }
        catch
        {
            RemoveAchievementLease(lease.Revision);
            throw;
        }
    }

    public ValueTask<BadgeRefreshResult> RefreshBadges(
        BadgeRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(
            cancellation_token,
            token => RefreshBadgesCore(request, token));

    private async ValueTask<BadgeRefreshResult> RefreshBadgesCore(
        BadgeRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateOperationTimeout(request.TimeoutMilliseconds);
        DomainSessionScope scope = CaptureBadgeScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        (BadgeLoadOperation operation, bool dispatch) = AcquireBadgeOperation(scope);
        if (dispatch)
            DispatchBadgeOperation(operation, lifetime.Token);
        ObservedBadgeCommit observed = await WaitBadgeOperation(
            operation,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireBadgeScope(scope);
        BadgeSnapshotLease lease = StoreBadgeLease(observed.State);
        try
        {
            OwnedBadgePage first_page = OwnedBadgePageFor(lease, 0, request.Limit);
            var result = new BadgeRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.State.Revision,
                observed.State.InventoryRevision,
                observed.State.BaselineRevision,
                lease.Revision,
                dispatch ? 1 : 0,
                first_page);
            RequireBadgeScope(scope);
            return result;
        }
        catch
        {
            RemoveBadgeLease(lease.Revision);
            throw;
        }
    }

    void IAchievementOperations.RequestAchievements() => InvokeLegacy(
        cancellation_token => DispatchLegacyAchievementRequest(
            AchievementRequestRoute.List,
            cancellation_token));

    void IAchievementOperations.RequestPointLimits() => InvokeLegacy(
        cancellation_token => DispatchLegacyAchievementRequest(
            AchievementRequestRoute.PointLimits,
            cancellation_token));

    Task<IReadOnlyList<Achievement>> IAchievementOperations.EnsureAchievementsLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsureAchievementsLoadedCore(timeout_milliseconds, token)).AsTask();

    private async ValueTask<IReadOnlyList<Achievement>> EnsureAchievementsLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        DomainSessionScope scope = CaptureAchievementScope(null, cancellation_token);
        (AchievementRouteOperation? operation, bool dispatch, AchievementState? loaded) =
            AcquireAchievementEnsure(AchievementRequestRoute.List, scope);
        if (loaded is { } current)
        {
            RequireAchievementScope(scope);
            return Array.AsReadOnly(current.Achievements
                .Select(AchievementManager.Clone)
                .ToArray());
        }
        if (dispatch)
            DispatchAchievementOperation(operation!, lifetime.Token);
        ObservedAchievementCommit observed = await WaitAchievementOperation(
            operation!,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireAchievementScope(scope);
        return Array.AsReadOnly(observed.State.Achievements
            .Select(AchievementManager.Clone)
            .ToArray());
    }

    Task<BadgePointLimits> IAchievementOperations.EnsurePointLimitsLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsurePointLimitsLoadedCore(timeout_milliseconds, token)).AsTask();

    private async ValueTask<BadgePointLimits> EnsurePointLimitsLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        DomainSessionScope scope = CaptureAchievementScope(null, cancellation_token);
        (AchievementRouteOperation? operation, bool dispatch, AchievementState? loaded) =
            AcquireAchievementEnsure(AchievementRequestRoute.PointLimits, scope);
        if (loaded is { } current)
        {
            RequireAchievementScope(scope);
            return AchievementManager.Clone(current.PointLimits);
        }
        if (dispatch)
            DispatchAchievementOperation(operation!, lifetime.Token);
        ObservedAchievementCommit observed = await WaitAchievementOperation(
            operation!,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireAchievementScope(scope);
        return AchievementManager.Clone(observed.State.PointLimits);
    }

    Task<IReadOnlyCollection<OwnedBadge>> IBadgeInventoryOperations.EnsureLoadedAsync(
        int timeout_milliseconds,
        CancellationToken cancellation_token) => InvokeAsync(
            cancellation_token,
            token => EnsureBadgesLoadedCore(timeout_milliseconds, token)).AsTask();

    private async ValueTask<IReadOnlyCollection<OwnedBadge>> EnsureBadgesLoadedCore(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        DomainSessionScope scope = CaptureBadgeScope(null, cancellation_token);
        (BadgeLoadOperation? operation, bool dispatch, BadgeInventoryState? loaded) =
            AcquireBadgeEnsure(scope);
        if (loaded is { } current)
        {
            RequireBadgeScope(scope);
            return Array.AsReadOnly(current.OwnedBadges.ToArray());
        }
        if (dispatch)
            DispatchBadgeOperation(operation!, lifetime.Token);
        ObservedBadgeCommit observed = await WaitBadgeOperation(
            operation!,
            timeout_milliseconds,
            cancellation_token).ConfigureAwait(false);
        RequireBadgeScope(scope);
        return Array.AsReadOnly(observed.State.OwnedBadges.ToArray());
    }

    private void DispatchLegacyAchievementRequest(
        AchievementRequestRoute route,
        CancellationToken cancellation_token)
    {
        DomainSessionScope scope = CaptureAchievementScope(null, cancellation_token);
        long baseline = achievements.CaptureRequestEpoch(
            route,
            scope.Session,
            scope.SessionGeneration);
        if (route is AchievementRequestRoute.List)
        {
            message_dispatcher.Dispatch(
                MessageContracts.Achievements.Request,
                new AchievementsRequest(),
                scope.Session,
                cancellation_token,
                () => achievements.AdvanceRequestEpoch(
                    route,
                    baseline,
                    scope.Session,
                    scope.SessionGeneration));
        }
        else
        {
            message_dispatcher.Dispatch(
                MessageContracts.Achievements.PointLimitsRequest,
                new BadgePointLimitsRequest(),
                scope.Session,
                cancellation_token,
                () => achievements.AdvanceRequestEpoch(
                    route,
                    baseline,
                    scope.Session,
                    scope.SessionGeneration));
        }
    }

    private (AchievementRouteOperation Operation, bool Dispatch)
        AcquireAchievementOperation(
            AchievementRequestRoute route,
            DomainSessionScope scope)
    {
        lock (operation_sync)
        {
            ThrowIfDisposed();
            return AcquireAchievementOperationUnsafe(route, scope, false);
        }
    }

    private (
        AchievementRouteOperation? Operation,
        bool Dispatch,
        AchievementState? Loaded) AcquireAchievementEnsure(
            AchievementRequestRoute route,
            DomainSessionScope scope)
    {
        lock (operation_sync)
        {
            ThrowIfDisposed();
            AchievementState state = achievements.State;
            if (!scope.Matches(state.Session, state.SessionGeneration) ||
                !ReferenceEquals(connection.Session, scope.Session))
            {
                throw new RequestDisconnectedException(
                    AchievementOutgoingName(route),
                    AchievementIncomingName(route));
            }
            bool loaded = route is AchievementRequestRoute.List
                ? state.Loaded
                : state.PointLimitsLoaded;
            if (loaded)
                return (null, false, state);
            (AchievementRouteOperation operation, bool dispatch) =
                AcquireAchievementOperationUnsafe(route, scope, true);
            return (operation, dispatch, null);
        }
    }

    private (AchievementRouteOperation Operation, bool Dispatch)
        AcquireAchievementOperationUnsafe(
            AchievementRequestRoute route,
            DomainSessionScope scope,
            bool ensure_only)
    {
        AchievementRouteOperation? operation = AchievementOperation(route);
        if (operation is not null &&
            !operation.Scope.Matches(scope.Session, scope.SessionGeneration))
        {
            CompleteAchievementUnsafe(
                operation,
                new RequestDisconnectedException(
                    AchievementOutgoingName(route),
                    AchievementIncomingName(route)));
            operation = null;
        }
        if (operation is not null)
        {
            operation.EnsureOnly &= ensure_only;
            operation.Waiters++;
            operation.ZeroWaiterSinceUtc = null;
            return (operation, false);
        }
        long request_baseline = achievements.CaptureRequestEpoch(
            route,
            scope.Session,
            scope.SessionGeneration);
        AchievementState state = achievements.State;
        long source_baseline = route is AchievementRequestRoute.List
            ? state.BaselineRevision
            : state.PointLimitsRevision;
        operation = new AchievementRouteOperation(
            route,
            scope,
            request_baseline,
            source_baseline,
            ensure_only)
        {
            Waiters = 1
        };
        SetAchievementOperation(route, operation);
        return (operation, true);
    }

    private (BadgeLoadOperation Operation, bool Dispatch) AcquireBadgeOperation(
        DomainSessionScope scope)
    {
        lock (operation_sync)
        {
            ThrowIfDisposed();
            return AcquireBadgeOperationUnsafe(scope, false);
        }
    }

    private (BadgeLoadOperation? Operation, bool Dispatch, BadgeInventoryState? Loaded)
        AcquireBadgeEnsure(DomainSessionScope scope)
    {
        lock (operation_sync)
        {
            ThrowIfDisposed();
            BadgeInventoryState state = badges.State;
            if (!scope.Matches(state.Session, state.SessionGeneration) ||
                !ReferenceEquals(connection.Session, scope.Session))
            {
                throw new RequestDisconnectedException(
                    MessageKeys.Badges.Request.ToString(),
                    MessageKeys.Badges.Snapshot.ToString());
            }
            if (state.Loaded)
                return (null, false, state);
            (BadgeLoadOperation operation, bool dispatch) =
                AcquireBadgeOperationUnsafe(scope, true);
            return (operation, dispatch, null);
        }
    }

    private (BadgeLoadOperation Operation, bool Dispatch) AcquireBadgeOperationUnsafe(
        DomainSessionScope scope,
        bool ensure_only)
    {
            BadgeLoadOperation? operation = badge_load_operation;
            if (operation is not null &&
                !operation.Scope.Matches(scope.Session, scope.SessionGeneration))
            {
                CompleteBadgeUnsafe(
                    operation,
                    new RequestDisconnectedException(
                        MessageKeys.Badges.Request.ToString(),
                        MessageKeys.Badges.Snapshot.ToString()));
                operation = null;
            }
            bool retire_response_free = false;
            if (operation is not null &&
                operation.Waiters == 0 &&
                !operation.ResponseObserved &&
                operation.ZeroWaiterSinceUtc is DateTimeOffset idle_since &&
                time_provider.GetUtcNow() - idle_since >= badge_response_free_lease &&
                badges.RequestEpochIsCurrent(
                    operation.ExpectedRequestEpoch,
                    scope.Session,
                    scope.SessionGeneration))
            {
                CompleteBadgeUnsafe(
                    operation,
                    new RequestTimeoutException(
                        MessageKeys.Badges.Request.ToString(),
                        MessageKeys.Badges.Snapshot.ToString(),
                        checked((int)badge_response_free_lease.TotalMilliseconds)));
                operation = null;
                retire_response_free = true;
            }
            if (operation is not null)
            {
                operation.EnsureOnly &= ensure_only;
                operation.Waiters++;
                operation.ZeroWaiterSinceUtc = null;
                return (operation, false);
            }
            long request_baseline = badges.CaptureRequestEpoch(
                scope.Session,
                scope.SessionGeneration);
            operation = new BadgeLoadOperation(
                scope,
                request_baseline,
                badges.State.BaselineRevision,
                retire_response_free,
                ensure_only)
            {
                Waiters = 1
            };
            badge_load_operation = operation;
            return (operation, true);
    }

    private void DispatchAchievementOperation(
        AchievementRouteOperation operation,
        CancellationToken cancellation_token)
    {
        try
        {
            bool ensure_gate;
            lock (operation_sync)
            {
                if (!ReferenceEquals(AchievementOperation(operation.Route), operation) ||
                    operation.Completion.Task.IsCompleted)
                {
                    return;
                }
                operation.ExpectedRequestEpoch = checked(operation.RequestBaseline + 1);
                operation.Dispatching = true;
                ensure_gate = operation.EnsureOnly;
            }
            bool request_advanced = false;
            if (ensure_gate)
            {
                request_advanced = achievements.TryAdvanceRequestEpochIfUnloaded(
                    operation.Route,
                    operation.RequestBaseline,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration,
                    out long advanced_epoch,
                    out AchievementState current);
                if (request_advanced &&
                    advanced_epoch != operation.ExpectedRequestEpoch)
                {
                    throw new InvalidOperationException(
                        "The achievement request epoch advanced unexpectedly.");
                }
                if (!request_advanced)
                {
                    lock (operation_sync)
                    {
                        if (ReferenceEquals(
                                AchievementOperation(operation.Route),
                                operation) &&
                            operation.EnsureOnly)
                        {
                            operation.Dispatching = false;
                            CompleteAchievementUnsafe(
                                operation,
                                new ObservedAchievementCommit(
                                    current,
                                    time_provider.GetUtcNow()));
                            return;
                        }
                    }
                }
            }
            if (operation.Route is AchievementRequestRoute.List)
            {
                if (request_advanced)
                {
                    message_dispatcher.Dispatch(
                        MessageContracts.Achievements.Request,
                        new AchievementsRequest(),
                        operation.Scope.Session,
                        cancellation_token);
                }
                else
                {
                    message_dispatcher.Dispatch(
                        MessageContracts.Achievements.Request,
                        new AchievementsRequest(),
                        operation.Scope.Session,
                        cancellation_token,
                        () => achievements.AdvanceRequestEpoch(
                            operation.Route,
                            operation.RequestBaseline,
                            operation.Scope.Session,
                            operation.Scope.SessionGeneration));
                }
            }
            else
            {
                if (request_advanced)
                {
                    message_dispatcher.Dispatch(
                        MessageContracts.Achievements.PointLimitsRequest,
                        new BadgePointLimitsRequest(),
                        operation.Scope.Session,
                        cancellation_token);
                }
                else
                {
                    message_dispatcher.Dispatch(
                        MessageContracts.Achievements.PointLimitsRequest,
                        new BadgePointLimitsRequest(),
                        operation.Scope.Session,
                        cancellation_token,
                        () => achievements.AdvanceRequestEpoch(
                            operation.Route,
                            operation.RequestBaseline,
                            operation.Scope.Session,
                            operation.Scope.SessionGeneration));
                }
            }
            lock (operation_sync)
            {
                operation.Dispatching = false;
                operation.DispatchedAtUtc = time_provider.GetUtcNow();
            }
        }
        catch (Exception error)
        {
            lock (operation_sync)
            {
                operation.Dispatching = false;
                CompleteAchievementUnsafe(operation, error);
            }
        }
    }

    private void DispatchBadgeOperation(
        BadgeLoadOperation operation,
        CancellationToken cancellation_token)
    {
        try
        {
            bool ensure_gate;
            lock (operation_sync)
            {
                if (!ReferenceEquals(badge_load_operation, operation) ||
                    operation.Completion.Task.IsCompleted)
                {
                    return;
                }
                operation.ExpectedRequestEpoch = checked(operation.RequestBaseline + 1);
                operation.Dispatching = true;
                ensure_gate = operation.EnsureOnly;
            }
            bool request_advanced = false;
            if (ensure_gate)
            {
                request_advanced = badges.TryAdvanceRequestEpochIfUnloaded(
                    operation.RequestBaseline,
                    operation.Scope.Session,
                    operation.Scope.SessionGeneration,
                    operation.RetireResponseFree,
                    out long advanced_epoch,
                    out BadgeInventoryState current);
                if (request_advanced &&
                    advanced_epoch != operation.ExpectedRequestEpoch)
                {
                    throw new InvalidOperationException(
                        "The badge inventory request epoch advanced unexpectedly.");
                }
                if (!request_advanced)
                {
                    lock (operation_sync)
                    {
                        if (ReferenceEquals(badge_load_operation, operation) &&
                            operation.EnsureOnly)
                        {
                            operation.Dispatching = false;
                            CompleteBadgeUnsafe(
                                operation,
                                new ObservedBadgeCommit(
                                    current,
                                    time_provider.GetUtcNow()));
                            return;
                        }
                    }
                }
            }
            if (request_advanced)
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Badges.Request,
                    new BadgeInventoryRequest(),
                    operation.Scope.Session,
                    cancellation_token);
            }
            else
            {
                message_dispatcher.Dispatch(
                    MessageContracts.Badges.Request,
                    new BadgeInventoryRequest(),
                    operation.Scope.Session,
                    cancellation_token,
                    () => badges.AdvanceRequestEpoch(
                        operation.RequestBaseline,
                        operation.Scope.Session,
                        operation.Scope.SessionGeneration,
                        operation.RetireResponseFree));
            }
            lock (operation_sync)
            {
                operation.Dispatching = false;
                operation.DispatchedAtUtc = time_provider.GetUtcNow();
            }
        }
        catch (Exception error)
        {
            lock (operation_sync)
            {
                operation.Dispatching = false;
                CompleteBadgeUnsafe(operation, error);
            }
        }
    }

    private async Task<ObservedAchievementCommit> WaitAchievementOperation(
        AchievementRouteOperation operation,
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
                AchievementOutgoingName(operation.Route),
                AchievementIncomingName(operation.Route),
                timeout_milliseconds);
        }
        finally
        {
            lock (operation_sync)
            {
                operation.Waiters = Math.Max(0, operation.Waiters - 1);
                if (operation.Waiters == 0 && !operation.Completion.Task.IsCompleted)
                    operation.ZeroWaiterSinceUtc = time_provider.GetUtcNow();
            }
        }
    }

    private async Task<ObservedBadgeCommit> WaitBadgeOperation(
        BadgeLoadOperation operation,
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
                MessageKeys.Badges.Request.ToString(),
                MessageKeys.Badges.Snapshot.ToString(),
                timeout_milliseconds);
        }
        finally
        {
            lock (operation_sync)
            {
                operation.Waiters = Math.Max(0, operation.Waiters - 1);
                if (operation.Waiters == 0 && !operation.Completion.Task.IsCompleted)
                    operation.ZeroWaiterSinceUtc = time_provider.GetUtcNow();
            }
        }
    }

    private void ObserveAchievementCommit(AchievementStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            ObserveAchievementCommitCore(update);
    }

    private void ObserveAchievementCommitCore(AchievementStateUpdate update)
    {
        lock (operation_sync)
        {
            if (update.Kind is AchievementStateChangeKind.Reset)
            {
                ClearAchievementLeases();
                DisconnectAchievementUnsafe(achievement_list_operation);
                DisconnectAchievementUnsafe(achievement_point_limits_operation);
                return;
            }
            if (update.Kind is AchievementStateChangeKind.Request && update.Route is { } route)
            {
                AchievementRouteOperation? requested = AchievementOperation(route);
                if (requested is not null &&
                    update.RequestEpoch != requested.ExpectedRequestEpoch)
                {
                    CompleteAchievementUnsafe(
                        requested,
                        new InvalidOperationException(
                            "The achievement request was invalidated by another dispatch."));
                }
                return;
            }
            AchievementRequestRoute? response_route = update.Kind switch
            {
                AchievementStateChangeKind.Snapshot => AchievementRequestRoute.List,
                AchievementStateChangeKind.PointLimits =>
                    AchievementRequestRoute.PointLimits,
                _ => null
            };
            if (response_route is not { } matched_route)
                return;
            AchievementRouteOperation? operation = AchievementOperation(matched_route);
            if (operation is null ||
                operation.ExpectedRequestEpoch <= 0 ||
                update.RequestEpoch != operation.ExpectedRequestEpoch ||
                !operation.Scope.Matches(update.State.Session, update.State.SessionGeneration))
            {
                return;
            }
            long source_revision = matched_route is AchievementRequestRoute.List
                ? update.State.BaselineRevision
                : update.State.PointLimitsRevision;
            if (source_revision <= operation.SourceBaseline)
                return;
            CompleteAchievementUnsafe(
                operation,
                new ObservedAchievementCommit(update.State, time_provider.GetUtcNow()));
        }
    }

    private void ObserveBadgeCommit(BadgeInventoryStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
            ObserveBadgeCommitCore(update);
    }

    private void ObserveBadgeCommitCore(BadgeInventoryStateUpdate update)
    {
        lock (operation_sync)
        {
            BadgeLoadOperation? operation = badge_load_operation;
            if (update.Kind is BadgeInventoryStateChangeKind.Reset)
            {
                ClearBadgeLeases();
                if (operation is not null)
                {
                    CompleteBadgeUnsafe(
                        operation,
                        new RequestDisconnectedException(
                            MessageKeys.Badges.Request.ToString(),
                            MessageKeys.Badges.Snapshot.ToString()));
                }
                return;
            }
            if (operation is null)
                return;
            if (update.Kind is BadgeInventoryStateChangeKind.Request)
            {
                if (update.RequestEpoch != operation.ExpectedRequestEpoch)
                {
                    CompleteBadgeUnsafe(
                        operation,
                        new InvalidOperationException(
                            "The badge inventory request was invalidated by another dispatch."));
                }
                return;
            }
            if (!operation.Scope.Matches(update.State.Session, update.State.SessionGeneration))
                return;
            if (update.Kind is BadgeInventoryStateChangeKind.Fragment &&
                operation.ExpectedRequestEpoch > 0 &&
                update.RequestEpoch == operation.ExpectedRequestEpoch)
            {
                operation.ResponseObserved = true;
                operation.LastResponseAtUtc = time_provider.GetUtcNow();
                return;
            }
            if (update.Kind is BadgeInventoryStateChangeKind.CorrelationFailed &&
                operation.ExpectedRequestEpoch > 0 &&
                update.RequestEpoch == operation.ExpectedRequestEpoch)
            {
                CompleteBadgeUnsafe(
                    operation,
                    update.Value as FragmentedLoadCorrelationException ??
                        new FragmentedLoadCorrelationException(
                            "badge inventory",
                            update.State.RecoveryRetiredRequestEpoch,
                            update.State.RecoveryActiveRequestEpoch));
                return;
            }
            if (update.Kind is BadgeInventoryStateChangeKind.Loaded &&
                operation.ExpectedRequestEpoch > 0 &&
                update.RequestEpoch == operation.ExpectedRequestEpoch &&
                update.State.BaselineRevision > operation.SourceBaseline)
            {
                CompleteBadgeUnsafe(
                    operation,
                    new ObservedBadgeCommit(update.State, time_provider.GetUtcNow()));
            }
        }
    }

    private void CompleteAchievementUnsafe(
        AchievementRouteOperation operation,
        ObservedAchievementCommit result)
    {
        ClearAchievementOperationUnsafe(operation);
        operation.Completion.TrySetResult(result);
    }

    private void CompleteAchievementUnsafe(
        AchievementRouteOperation operation,
        Exception error)
    {
        ClearAchievementOperationUnsafe(operation);
        operation.Completion.TrySetException(error);
    }

    private void CompleteBadgeUnsafe(BadgeLoadOperation operation, ObservedBadgeCommit result)
    {
        if (ReferenceEquals(badge_load_operation, operation))
            badge_load_operation = null;
        operation.Completion.TrySetResult(result);
    }

    private void CompleteBadgeUnsafe(BadgeLoadOperation operation, Exception error)
    {
        if (ReferenceEquals(badge_load_operation, operation))
            badge_load_operation = null;
        operation.Completion.TrySetException(error);
    }

    private void DisconnectAchievementUnsafe(AchievementRouteOperation? operation)
    {
        if (operation is null)
            return;
        CompleteAchievementUnsafe(
            operation,
            new RequestDisconnectedException(
                AchievementOutgoingName(operation.Route),
                AchievementIncomingName(operation.Route)));
    }

    private void ClearAchievementOperationUnsafe(AchievementRouteOperation operation)
    {
        if (ReferenceEquals(AchievementOperation(operation.Route), operation))
            SetAchievementOperation(operation.Route, null);
    }

    private AchievementRouteOperation? AchievementOperation(
        AchievementRequestRoute route) => route switch
    {
        AchievementRequestRoute.List => achievement_list_operation,
        AchievementRequestRoute.PointLimits => achievement_point_limits_operation,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private void SetAchievementOperation(
        AchievementRequestRoute route,
        AchievementRouteOperation? operation)
    {
        if (route is AchievementRequestRoute.List)
            achievement_list_operation = operation;
        else if (route is AchievementRequestRoute.PointLimits)
            achievement_point_limits_operation = operation;
        else
            throw new ArgumentOutOfRangeException(nameof(route));
    }

    private void ClearOperationState()
    {
        lock (operation_sync)
        {
            var disposed = new ObjectDisposedException(nameof(AchievementApplication));
            if (achievement_list_operation is { } list)
                CompleteAchievementUnsafe(list, disposed);
            if (achievement_point_limits_operation is { } point_limits)
                CompleteAchievementUnsafe(point_limits, disposed);
            if (badge_load_operation is { } inventory)
                CompleteBadgeUnsafe(inventory, disposed);
        }
    }

    private DomainSessionScope CaptureAchievementScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedSessionGeneration(expected_session_generation);
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        AchievementState state = achievements.State;
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The achievement state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active achievement session generation does not match the expected generation.");
        }
        return new DomainSessionScope(session, state.SessionGeneration);
    }

    private DomainSessionScope CaptureBadgeScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        ValidateExpectedSessionGeneration(expected_session_generation);
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        BadgeInventoryState state = badges.State;
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The badge state is not bound to the active hotel session.");
        }
        if (expected_session_generation is long expected &&
            expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active badge session generation does not match the expected generation.");
        }
        return new DomainSessionScope(session, state.SessionGeneration);
    }

    private void RequireAchievementScope(DomainSessionScope scope)
    {
        ThrowIfDisposed();
        AchievementState state = achievements.State;
        if (!scope.Matches(connection.Session, state.SessionGeneration) ||
            !ReferenceEquals(state.Session, scope.Session))
        {
            throw new RequestDisconnectedException(
                MessageKeys.Achievements.Request.ToString(),
                MessageKeys.Achievements.Snapshot.ToString());
        }
    }

    private void RequireBadgeScope(DomainSessionScope scope)
    {
        ThrowIfDisposed();
        BadgeInventoryState state = badges.State;
        if (!scope.Matches(connection.Session, state.SessionGeneration) ||
            !ReferenceEquals(state.Session, scope.Session))
        {
            throw new RequestDisconnectedException(
                MessageKeys.Badges.Request.ToString(),
                MessageKeys.Badges.Snapshot.ToString());
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

    private static string AchievementOutgoingName(AchievementRequestRoute route) =>
        route is AchievementRequestRoute.List
            ? MessageKeys.Achievements.Request.ToString()
            : MessageKeys.Achievements.PointLimitsRequest.ToString();

    private static string AchievementIncomingName(AchievementRequestRoute route) =>
        route is AchievementRequestRoute.List
            ? MessageKeys.Achievements.Snapshot.ToString()
            : MessageKeys.Achievements.PointLimits.ToString();

    private readonly record struct DomainSessionScope(
        Session Session,
        long SessionGeneration)
    {
        public bool Matches(Session? session, long session_generation) =>
            ReferenceEquals(Session, session) && SessionGeneration == session_generation;
    }

    private sealed class AchievementRouteOperation(
        AchievementRequestRoute route,
        DomainSessionScope scope,
        long request_baseline,
        long source_baseline,
        bool ensure_only)
    {
        public AchievementRequestRoute Route { get; } = route;
        public DomainSessionScope Scope { get; } = scope;
        public long RequestBaseline { get; } = request_baseline;
        public long SourceBaseline { get; } = source_baseline;
        public TaskCompletionSource<ObservedAchievementCommit> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool EnsureOnly { get; set; } = ensure_only;
        public int Waiters { get; set; }
        public bool Dispatching { get; set; }
        public DateTimeOffset DispatchedAtUtc { get; set; }
        public DateTimeOffset? ZeroWaiterSinceUtc { get; set; }
    }

    private sealed class BadgeLoadOperation(
        DomainSessionScope scope,
        long request_baseline,
        long source_baseline,
        bool retire_response_free,
        bool ensure_only)
    {
        public DomainSessionScope Scope { get; } = scope;
        public long RequestBaseline { get; } = request_baseline;
        public long SourceBaseline { get; } = source_baseline;
        public bool RetireResponseFree { get; } = retire_response_free;
        public TaskCompletionSource<ObservedBadgeCommit> Completion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool EnsureOnly { get; set; } = ensure_only;
        public int Waiters { get; set; }
        public bool Dispatching { get; set; }
        public bool ResponseObserved { get; set; }
        public DateTimeOffset DispatchedAtUtc { get; set; }
        public DateTimeOffset LastResponseAtUtc { get; set; }
        public DateTimeOffset? ZeroWaiterSinceUtc { get; set; }
    }

    private sealed record ObservedAchievementCommit(
        AchievementState State,
        DateTimeOffset ObservedAtUtc);

    private sealed record ObservedBadgeCommit(
        BadgeInventoryState State,
        DateTimeOffset ObservedAtUtc);
}
