using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class LeaderboardApplication
{
    private const int commit_history_limit = 32;
    private readonly object refresh_sync = new();
    private readonly SemaphoreSlim refresh_serial = new(1, 1);
    private readonly SemaphoreSlim route_signal = new(0, 1);
    private readonly List<ObservedLeaderboardCommit> snapshot_commits = [];

    private ValueTask<LeaderboardRefreshResult> Refresh(
        LeaderboardRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshCore(request, token));

    private async ValueTask<LeaderboardRefreshResult> RefreshCore(
        LeaderboardRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRoute(request.Scope);
        ValidateDirection(request.Direction);
        ValidatePageLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        LeaderboardSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        var route = new LeaderboardRoute(request.Scope, request.Weekly);
        DateTimeOffset deadline = time_provider.GetUtcNow().AddMilliseconds(
            request.TimeoutMilliseconds);
        await refresh_serial.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            ObservedLeaderboardCommit observed = await RequestSnapshot(
                route,
                request.GameTypeId,
                request.StartRank,
                request.Direction,
                scope,
                deadline,
                request.TimeoutMilliseconds,
                cancellation_token).ConfigureAwait(false);
            RequireScope(scope, route);
            LeaderboardSnapshotLease lease = StoreLease(observed.Update.State);
            LeaderboardEntryPage first_page = PageFor(
                lease,
                route.Scope,
                route.Weekly,
                0,
                request.Limit);
            RequireLeaseActive(lease);
            return new LeaderboardRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.Update.State.Revision,
                observed.Update.State.BoardsRevision,
                lease.Revision,
                1,
                first_page);
        }
        finally
        {
            refresh_serial.Release();
        }
    }

    private ValueTask<LeaderboardWeekOffsetResult> SetWeekOffset(
        LeaderboardWeekOffsetRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token =>
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            leaderboards.SetWeekOffset(request.Offset);
            LeaderboardSnapshotLease lease = StoreCurrentLease();
            LeaderboardState state = lease.State;
            return ValueTask.FromResult(new LeaderboardWeekOffsetResult(
                request.Offset,
                state.WeekOffset,
                state.Revision,
                state.SettingsRevision,
                lease.Revision));
        });

    void ILeaderboardOperations.Request(
        int game_type_id,
        LeaderboardScope scope,
        bool weekly,
        int start_rank,
        int direction) =>
        InvokeLegacy(token => DispatchLegacy(
            game_type_id,
            scope,
            weekly,
            start_rank,
            direction,
            token));

    private void DispatchLegacy(
        int game_type_id,
        LeaderboardScope scope,
        bool weekly,
        int start_rank,
        int direction,
        CancellationToken cancellation_token)
    {
        ValidateRoute(scope);
        ValidateDirection(direction);
        LeaderboardSessionScope session_scope = CaptureScope(null, cancellation_token);
        int view_size = leaderboards.ViewSize;
        int window_size = leaderboards.WindowSize;
        if (weekly)
        {
            var request = new WeeklyLeaderboardRequest(
                game_type_id,
                leaderboards.WeekOffset,
                start_rank,
                direction,
                view_size,
                window_size);
            switch (scope)
            {
                case LeaderboardScope.Total:
                    message_dispatcher.Dispatch(
                        MessageContracts.Leaderboards.WeeklyTotalRequest,
                        request,
                        session_scope.Session,
                        cancellation_token,
                        () => leaderboards.AdvanceLegacyRequest(
                            new LeaderboardRoute(scope, true),
                            game_type_id,
                            session_scope.Session,
                            session_scope.SessionGeneration));
                    return;
                case LeaderboardScope.Friends:
                    message_dispatcher.Dispatch(
                        MessageContracts.Leaderboards.WeeklyFriendsRequest,
                        request,
                        session_scope.Session,
                        cancellation_token,
                        () => leaderboards.AdvanceLegacyRequest(
                            new LeaderboardRoute(scope, true),
                            game_type_id,
                            session_scope.Session,
                            session_scope.SessionGeneration));
                    return;
                case LeaderboardScope.Groups:
                    message_dispatcher.Dispatch(
                        MessageContracts.Leaderboards.WeeklyGroupsRequest,
                        request,
                        session_scope.Session,
                        cancellation_token,
                        () => leaderboards.AdvanceLegacyRequest(
                            new LeaderboardRoute(scope, true),
                            game_type_id,
                            session_scope.Session,
                            session_scope.SessionGeneration));
                    return;
            }
        }
        var ordinary = new LeaderboardRequest(
            game_type_id,
            start_rank,
            direction,
            view_size,
            window_size);
        switch (scope)
        {
            case LeaderboardScope.Total:
                message_dispatcher.Dispatch(
                    MessageContracts.Leaderboards.TotalRequest,
                    ordinary,
                    session_scope.Session,
                    cancellation_token,
                    () => leaderboards.AdvanceLegacyRequest(
                        new LeaderboardRoute(scope, false),
                        game_type_id,
                        session_scope.Session,
                        session_scope.SessionGeneration));
                break;
            case LeaderboardScope.Friends:
                message_dispatcher.Dispatch(
                    MessageContracts.Leaderboards.FriendsRequest,
                    ordinary,
                    session_scope.Session,
                    cancellation_token,
                    () => leaderboards.AdvanceLegacyRequest(
                        new LeaderboardRoute(scope, false),
                        game_type_id,
                        session_scope.Session,
                        session_scope.SessionGeneration));
                break;
            case LeaderboardScope.Groups:
                message_dispatcher.Dispatch(
                    MessageContracts.Leaderboards.GroupsRequest,
                    ordinary,
                    session_scope.Session,
                    cancellation_token,
                    () => leaderboards.AdvanceLegacyRequest(
                        new LeaderboardRoute(scope, false),
                        game_type_id,
                        session_scope.Session,
                        session_scope.SessionGeneration));
                break;
        }
    }

    private Task<ObservedLeaderboardCommit> RequestSnapshot(
        LeaderboardRoute route,
        int game_type_id,
        int start_rank,
        int direction,
        LeaderboardSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        int view_size = leaderboards.ViewSize;
        int window_size = leaderboards.WindowSize;
        return route switch
        {
            { Scope: LeaderboardScope.Total, Weekly: false } => RequestRoute(
                route,
                MessageContracts.Leaderboards.TotalRequest,
                new LeaderboardRequest(game_type_id, start_rank, direction, view_size, window_size),
                MessageContracts.Leaderboards.TotalSnapshot,
                game_type_id,
                null,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            { Scope: LeaderboardScope.Friends, Weekly: false } => RequestRoute(
                route,
                MessageContracts.Leaderboards.FriendsRequest,
                new LeaderboardRequest(game_type_id, start_rank, direction, view_size, window_size),
                MessageContracts.Leaderboards.FriendsSnapshot,
                game_type_id,
                null,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            { Scope: LeaderboardScope.Groups, Weekly: false } => RequestRoute(
                route,
                MessageContracts.Leaderboards.GroupsRequest,
                new LeaderboardRequest(game_type_id, start_rank, direction, view_size, window_size),
                MessageContracts.Leaderboards.GroupsSnapshot,
                game_type_id,
                null,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            { Scope: LeaderboardScope.Total, Weekly: true } => RequestWeeklyRoute(
                route,
                MessageContracts.Leaderboards.WeeklyTotalRequest,
                MessageContracts.Leaderboards.WeeklyTotalSnapshot,
                game_type_id,
                start_rank,
                direction,
                view_size,
                window_size,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            { Scope: LeaderboardScope.Friends, Weekly: true } => RequestWeeklyRoute(
                route,
                MessageContracts.Leaderboards.WeeklyFriendsRequest,
                MessageContracts.Leaderboards.WeeklyFriendsSnapshot,
                game_type_id,
                start_rank,
                direction,
                view_size,
                window_size,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            { Scope: LeaderboardScope.Groups, Weekly: true } => RequestWeeklyRoute(
                route,
                MessageContracts.Leaderboards.WeeklyGroupsRequest,
                MessageContracts.Leaderboards.WeeklyGroupsSnapshot,
                game_type_id,
                start_rank,
                direction,
                view_size,
                window_size,
                scope,
                deadline,
                timeout_milliseconds,
                cancellation_token),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
    }

    private Task<ObservedLeaderboardCommit> RequestWeeklyRoute<TResponse>(
        LeaderboardRoute route,
        MessageContract<WeeklyLeaderboardRequest> request_contract,
        MessageContract<TResponse> response_contract,
        int game_type_id,
        int start_rank,
        int direction,
        int view_size,
        int window_size,
        LeaderboardSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
        where TResponse : IParserComposer<TResponse>
    {
        int week_offset = leaderboards.WeekOffset;
        return RequestRoute(
            route,
            request_contract,
            new WeeklyLeaderboardRequest(
                game_type_id,
                week_offset,
                start_rank,
                direction,
                view_size,
                window_size),
            response_contract,
            game_type_id,
            week_offset,
            scope,
            deadline,
            timeout_milliseconds,
            cancellation_token);
    }

    private async Task<ObservedLeaderboardCommit> RequestRoute<TRequest, TResponse>(
        LeaderboardRoute route,
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<TResponse> response_contract,
        int game_type_id,
        int? week_offset,
        LeaderboardSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        while (true)
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireScope(scope, route);
            LeaderboardRequestCorrelation correlation = leaderboards.CaptureRequestCorrelation(
                route,
                scope.Session,
                scope.SessionGeneration);
            int remaining = RemainingMilliseconds(deadline, timeout_milliseconds, route);
            if (correlation.OutstandingRequests != 0)
            {
                await WaitForRouteChange(remaining, cancellation_token).ConfigureAwait(false);
                continue;
            }
            var await_state = new RouteAwaitState(route, game_type_id, week_offset)
            {
                RequestBaseline = correlation.RequestEpoch,
                SourceBaseline = correlation.State.BoardsRevision
            };
            await requests.RequestAsync(
                request_contract,
                request,
                response_contract,
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
                    cancellation_token)).ConfigureAwait(false);
            RequireScope(scope, route);
            return await_state.Accepted ??
                throw new InvalidOperationException("The leaderboard response was not committed.");
        }
    }

    private void Arm(
        RouteAwaitState await_state,
        LeaderboardSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        _ = RemainingMilliseconds(deadline, timeout_milliseconds, await_state.Route);
        long expected = leaderboards.AdvanceTypedRequest(
            await_state.Route,
            await_state.RequestBaseline,
            await_state.GameTypeId,
            scope.Session,
            scope.SessionGeneration);
        lock (refresh_sync)
        {
            await_state.ExpectedRequestEpoch = expected;
            await_state.Armed = true;
        }
    }

    private bool MatchSnapshot(RouteAwaitState await_state, LeaderboardSessionScope scope)
    {
        lock (refresh_sync)
        {
            if (!await_state.Armed || await_state.Accepted is not null || !ScopeCurrent(scope))
                return false;
            ObservedLeaderboardCommit? accepted = snapshot_commits.FirstOrDefault(commit =>
                commit.Update.Kind is LeaderboardStateChangeKind.Snapshot &&
                commit.Update.Route == await_state.Route &&
                commit.Update.RequestEpoch == await_state.ExpectedRequestEpoch &&
                scope.Matches(commit.Update.State) &&
                commit.Update.State.BoardsRevision > await_state.SourceBaseline &&
                commit.Update.Board?.GameTypeId == await_state.GameTypeId &&
                (!await_state.Route.Weekly ||
                    commit.Update.State.Period?.CurrentOffset == await_state.WeekOffset));
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private void ObserveCommit(LeaderboardStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
        {
            lock (refresh_sync)
            {
                if (update.Kind is LeaderboardStateChangeKind.Reset)
                {
                    snapshot_commits.Clear();
                    ClearLeases();
                }
                else if (update.Kind is LeaderboardStateChangeKind.Snapshot)
                {
                    snapshot_commits.Add(new ObservedLeaderboardCommit(update, time_provider.GetUtcNow()));
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
        await route_signal.WaitAsync(
            TimeSpan.FromMilliseconds(timeout_milliseconds),
            cancellation_token).ConfigureAwait(false);
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

    private LeaderboardSessionScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        if (expected_session_generation is <= 0)
            throw new ArgumentOutOfRangeException(nameof(expected_session_generation));
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        LeaderboardState state = leaderboards.State;
        if (!ReferenceEquals(state.Session, session))
            throw new InvalidOperationException("The leaderboard state is not bound to the active hotel session.");
        if (expected_session_generation is long expected && expected != state.SessionGeneration)
            throw new InvalidOperationException("The leaderboard session generation does not match.");
        return new LeaderboardSessionScope(session, state.SessionGeneration);
    }

    private void RequireScope(LeaderboardSessionScope scope, LeaderboardRoute route)
    {
        ThrowIfDisposed();
        if (!ScopeCurrent(scope))
        {
            throw new RequestDisconnectedException(
                RequestKey(route).ToString(),
                ResponseKey(route).ToString());
        }
    }

    private bool ScopeCurrent(LeaderboardSessionScope scope) =>
        !DisposalStarted() && scope.Matches(connection.Session, leaderboards.State);

    private int RemainingMilliseconds(
        DateTimeOffset deadline,
        int original,
        LeaderboardRoute route)
    {
        double remaining = (deadline - time_provider.GetUtcNow()).TotalMilliseconds;
        if (remaining <= 0)
        {
            throw new RequestTimeoutException(
                RequestKey(route).ToString(),
                ResponseKey(route).ToString(),
                original);
        }
        return Math.Max(1, (int)Math.Ceiling(remaining));
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

    private static void ValidateDirection(int direction)
    {
        if (direction is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));
    }

    private static MessageKey RequestKey(LeaderboardRoute route) => route switch
    {
        { Scope: LeaderboardScope.Total, Weekly: false } => MessageKeys.Leaderboards.Total.Request,
        { Scope: LeaderboardScope.Friends, Weekly: false } => MessageKeys.Leaderboards.Friends.Request,
        { Scope: LeaderboardScope.Groups, Weekly: false } => MessageKeys.Leaderboards.Groups.Request,
        { Scope: LeaderboardScope.Total, Weekly: true } => MessageKeys.Leaderboards.WeeklyTotal.Request,
        { Scope: LeaderboardScope.Friends, Weekly: true } => MessageKeys.Leaderboards.WeeklyFriends.Request,
        { Scope: LeaderboardScope.Groups, Weekly: true } => MessageKeys.Leaderboards.WeeklyGroups.Request,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private static MessageKey ResponseKey(LeaderboardRoute route) => route switch
    {
        { Scope: LeaderboardScope.Total, Weekly: false } => MessageKeys.Leaderboards.Total.Snapshot,
        { Scope: LeaderboardScope.Friends, Weekly: false } => MessageKeys.Leaderboards.Friends.Snapshot,
        { Scope: LeaderboardScope.Groups, Weekly: false } => MessageKeys.Leaderboards.Groups.Snapshot,
        { Scope: LeaderboardScope.Total, Weekly: true } => MessageKeys.Leaderboards.WeeklyTotal.Snapshot,
        { Scope: LeaderboardScope.Friends, Weekly: true } => MessageKeys.Leaderboards.WeeklyFriends.Snapshot,
        { Scope: LeaderboardScope.Groups, Weekly: true } => MessageKeys.Leaderboards.WeeklyGroups.Snapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(route))
    };

    private readonly record struct LeaderboardSessionScope(Session Session, long SessionGeneration)
    {
        public bool Matches(LeaderboardState state) =>
            ReferenceEquals(Session, state.Session) && SessionGeneration == state.SessionGeneration;

        public bool Matches(Session? active, LeaderboardState state) =>
            ReferenceEquals(Session, active) && Matches(state);
    }

    private sealed class RouteAwaitState(
        LeaderboardRoute route,
        int game_type_id,
        int? week_offset)
    {
        public LeaderboardRoute Route { get; } = route;
        public int GameTypeId { get; } = game_type_id;
        public int? WeekOffset { get; } = week_offset;
        public long RequestBaseline { get; init; }
        public long SourceBaseline { get; init; }
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool Armed { get; set; }
        public ObservedLeaderboardCommit? Accepted { get; set; }
    }

    private sealed record ObservedLeaderboardCommit(
        LeaderboardStateUpdate Update,
        DateTimeOffset ObservedAtUtc);
}
