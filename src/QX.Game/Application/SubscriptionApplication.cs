using System.Text;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Subscriptions;

namespace Qx.Game.Application;

internal sealed class SubscriptionApplication : IApplicationFeature, ISubscriptionOperations
{
    private const int commit_history_limit = 32;
    private const int club_offer_commit_history_limit = 2;
    private const int lease_limit = 16;
    private readonly IConnection connection;
    private readonly SubscriptionManager subscriptions;
    private readonly RoomManager room;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly GuardedEventSource<SubscriptionChanged> changed;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object commits_sync = new();
    private readonly List<ObservedCommit> user_info_commits = [];
    private readonly List<ObservedCommit> kickback_commits = [];
    private readonly List<ObservedCommit> club_offer_commits = [];
    private readonly List<ObservedCommit> furni_count_commits = [];
    private readonly object leases_sync = new();
    private readonly Dictionary<long, SubscriptionSnapshotLease> leases = [];
    private readonly Queue<long> lease_order = [];
    private readonly Dictionary<long, ClubOffersSnapshotLease> club_offer_leases = [];
    private readonly Queue<long> club_offer_lease_order = [];
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private long lease_revision;
    private long club_offer_lease_revision;
    private int active_invocations;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public SubscriptionApplication(
        IConnection connection,
        GameState game,
        ApplicationMessageDispatcher message_dispatcher,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(message_dispatcher);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.connection = connection;
        subscriptions = game.Subscriptions;
        room = game.Room;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        changed = new GuardedEventSource<SubscriptionChanged>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<SubscriptionStateRequest, SubscriptionStateView>(
                SubscriptionApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<
                SubscriptionClubOffersPageRequest,
                SubscriptionClubOffersPage>(
                SubscriptionApplicationDescriptors.ClubOffersList,
                (request, _) => ValueTask.FromResult(ReadClubOffers(request))),
            new ApplicationCallBinding<
                SubscriptionClubOffersRefreshRequest,
                SubscriptionClubOffersPage>(
                SubscriptionApplicationDescriptors.ClubOffersRefresh,
                RefreshClubOffers),
            new ApplicationCallBinding<
                SubscriptionUserInfoRefreshRequest,
                SubscriptionUserInfoRefreshResult>(
                SubscriptionApplicationDescriptors.UserInfoRefresh,
                RefreshUserInfo),
            new ApplicationCallBinding<
                SubscriptionKickbackRefreshRequest,
                SubscriptionKickbackRefreshResult>(
                SubscriptionApplicationDescriptors.KickbackRefresh,
                RefreshKickback),
            new ApplicationCallBinding<
                SubscriptionBuildersClubFurniCountRefreshRequest,
                SubscriptionBuildersClubFurniCountRefreshResult>(
                SubscriptionApplicationDescriptors.BuildersClubFurniCountRefresh,
                RefreshBuildersClubFurniCount),
            new ApplicationCallBinding<
                SubscriptionBuildersClubFloorPlaceRequest,
                SubscriptionBuildersClubPlacementDispatchReceipt>(
                SubscriptionApplicationDescriptors.BuildersClubFloorOfferPlace,
                (request, cancellation_token) => ValueTask.FromResult(
                    PlaceBuildersClubFloorOffer(request, cancellation_token))),
            new ApplicationCallBinding<
                SubscriptionBuildersClubWallPlaceRequest,
                SubscriptionBuildersClubPlacementDispatchReceipt>(
                SubscriptionApplicationDescriptors.BuildersClubWallOfferPlace,
                (request, cancellation_token) => ValueTask.FromResult(
                    PlaceBuildersClubWallOffer(request, cancellation_token))),
            new ApplicationEventBinding<SubscriptionChanged>(
                SubscriptionApplicationDescriptors.Changed,
                changed.Subscribe)
        ]);
        subscriptions.StateCommitted += OnStateCommitted;
        subscriptions.StateChanged += OnStateChanged;
        try
        {
            subscriptions.BindOperations(this);
        }
        catch
        {
            subscriptions.StateCommitted -= OnStateCommitted;
            subscriptions.StateChanged -= OnStateChanged;
            changed.Dispose();
            lifetime.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public SubscriptionStateView ReadState(SubscriptionStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ValidateStateRequest(request);
        SubscriptionSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision, request.ProductName)
            : StoreCurrentLease(request.ProductName);
        IReadOnlyList<SubscriptionProductView> products = Slice(
            lease.Products,
            request.Offset,
            request.Limit);
        SubscriptionState state = lease.State;
        bool connected = state.Session is not null &&
            ReferenceEquals(connection.Session, state.Session);
        var view = new SubscriptionStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.UserInfoRevision,
            state.KickbackRevision,
            state.BuildersClubFurniCountRevision,
            state.BuildersClubMembershipRevision,
            state.BuildersClubPlacementWarningRevision,
            lease.Revision,
            state.UserInfo.Count,
            lease.Products.Count,
            request.Offset,
            NextOffset(request.Offset, products.Count, lease.Products.Count),
            products,
            state.KickbackInfo is { } kickback ? KickbackView(kickback) : null,
            state.BuildersClubFurniCount?.FurniCount,
            state.BuildersClubStatus is { } membership ? MembershipView(membership) : null,
            state.LastPlacementWarning is { } warning ? PlacementView(warning) : null)
        {
            ClubOffersRevision = state.ClubOffersRevision,
            ClubOffers = lease.ClubOffers
        };
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the subscription state was being read.");
        }
        return view;
    }

    public SubscriptionClubOffersPage ReadClubOffers(
        SubscriptionClubOffersPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ValidateClubOffersPageRequest(request);
        ClubOffersSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadClubOffersLease(revision)
            : StoreCurrentClubOffersLease();
        return ClubOffersPage(lease, request.Offset, request.Limit);
    }

    public ValueTask<SubscriptionClubOffersPage> RefreshClubOffers(
        SubscriptionClubOffersRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeRefresh(
            cancellation_token,
            token => RefreshClubOffersCore(request, token));

    private async ValueTask<SubscriptionClubOffersPage> RefreshClubOffersCore(
        SubscriptionClubOffersRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateLimit(request.Limit);
        ValidatePositiveTimeout(request.TimeoutMilliseconds);
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        SubscriptionOperationScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        long baseline = -1;
        ObservedCommit? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Subscriptions.ClubOffersRequest,
            new GetClubOffers(request.OfferType),
            MessageContracts.Subscriptions.ClubOffersSnapshot,
            scope.Session,
            match: response =>
            {
                lock (commits_sync)
                {
                    if (baseline < 0 || accepted is not null)
                        return false;
                    ObservedCommit? commit = FindClubOffersCommitUnsafe(
                        scope,
                        baseline,
                        response);
                    if (commit is null)
                        return false;
                    accepted = commit;
                    return true;
                }
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 1,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (commits_sync)
                {
                    baseline = subscriptions.State.ClubOffersRevision;
                    accepted = null;
                }
            },
            attempt_start: () =>
            {
                lock (commits_sync)
                {
                    baseline = -1;
                    accepted = null;
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        ObservedCommit observed;
        lock (commits_sync)
        {
            observed = accepted ??
                throw new InvalidOperationException(
                    "The accepted club-offers response was not committed by the passive state owner.");
        }
        ClubOffersSnapshotLease lease = StoreClubOffersLease(observed.Update.State);
        return ClubOffersPage(lease, 0, request.Limit);
    }

    public ValueTask<SubscriptionUserInfoRefreshResult> RefreshUserInfo(
        SubscriptionUserInfoRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeRefresh(
            cancellation_token,
            token => RefreshUserInfoCore(request, token));

    private async ValueTask<SubscriptionUserInfoRefreshResult> RefreshUserInfoCore(
        SubscriptionUserInfoRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateProductName(request.ProductName, nameof(request.ProductName));
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        SubscriptionOperationScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        long baseline = -1;
        ObservedCommit? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Subscriptions.UserInfoRequest,
            new SubscriptionGetUserInfo(request.ProductName),
            MessageContracts.Subscriptions.UserInfo,
            scope.Session,
            match: response =>
            {
                lock (commits_sync)
                {
                    if (baseline < 0 || accepted is not null)
                        return false;
                    ObservedCommit? commit = FindUserInfoCommitUnsafe(
                        scope,
                        baseline,
                        request.ProductName,
                        response);
                    if (commit is null)
                        return false;
                    accepted = commit;
                    return true;
                }
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (commits_sync)
                {
                    baseline = subscriptions.State.UserInfoRevision;
                    accepted = null;
                }
            },
            attempt_start: () =>
            {
                lock (commits_sync)
                {
                    baseline = -1;
                    accepted = null;
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        ObservedCommit observed;
        lock (commits_sync)
        {
            observed = accepted ??
                throw new InvalidOperationException(
                    "The accepted subscription user-info response was not committed by the passive state owner.");
        }
        ScrSendUserInfo value = (ScrSendUserInfo)observed.Update.Value!;
        return new SubscriptionUserInfoRefreshResult(
            scope.Session.Client,
            scope.SessionGeneration,
            observed.Update.State.Revision,
            observed.Update.State.UserInfoRevision,
            observed.ObservedAtUtc,
            ProductView(value, observed.Update.State.UserInfoRevision));
    }

    public ValueTask<SubscriptionKickbackRefreshResult> RefreshKickback(
        SubscriptionKickbackRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeRefresh(
            cancellation_token,
            token => RefreshKickbackCore(request, token));

    private async ValueTask<SubscriptionKickbackRefreshResult> RefreshKickbackCore(
        SubscriptionKickbackRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        SubscriptionOperationScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        long baseline = -1;
        ObservedCommit? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Subscriptions.KickbackInfoRequest,
            new SubscriptionGetKickbackInfo(),
            MessageContracts.Subscriptions.KickbackInfo,
            scope.Session,
            match: response =>
            {
                lock (commits_sync)
                {
                    if (baseline < 0 || accepted is not null)
                        return false;
                    ObservedCommit? commit = FindCommitUnsafe(
                        kickback_commits,
                        scope,
                        baseline,
                        response,
                        static state => state.KickbackRevision);
                    if (commit is null)
                        return false;
                    accepted = commit;
                    return true;
                }
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (commits_sync)
                {
                    baseline = subscriptions.State.KickbackRevision;
                    accepted = null;
                }
            },
            attempt_start: () =>
            {
                lock (commits_sync)
                {
                    baseline = -1;
                    accepted = null;
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        ObservedCommit observed;
        lock (commits_sync)
        {
            observed = accepted ??
                throw new InvalidOperationException(
                    "The accepted subscription kickback response was not committed by the passive state owner.");
        }
        ScrSendKickbackInfo value = (ScrSendKickbackInfo)observed.Update.Value!;
        return new SubscriptionKickbackRefreshResult(
            scope.Session.Client,
            scope.SessionGeneration,
            observed.Update.State.Revision,
            observed.Update.State.KickbackRevision,
            observed.ObservedAtUtc,
            KickbackView(value));
    }

    public ValueTask<SubscriptionBuildersClubFurniCountRefreshResult>
        RefreshBuildersClubFurniCount(
            SubscriptionBuildersClubFurniCountRefreshRequest request,
            CancellationToken cancellation_token) =>
        InvokeRefresh(
            cancellation_token,
            token => RefreshBuildersClubFurniCountCore(request, token));

    private async ValueTask<SubscriptionBuildersClubFurniCountRefreshResult>
        RefreshBuildersClubFurniCountCore(
            SubscriptionBuildersClubFurniCountRefreshRequest request,
            CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        SubscriptionOperationScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        long baseline = -1;
        ObservedCommit? accepted = null;
        await requests.RequestAsync(
            MessageContracts.Subscriptions.BuildersClubFurniCountRequest,
            new BuildersClubQueryFurniCount(),
            MessageContracts.Subscriptions.BuildersClubFurniCount,
            scope.Session,
            match: response =>
            {
                lock (commits_sync)
                {
                    if (baseline < 0 || accepted is not null)
                        return false;
                    ObservedCommit? commit = FindCommitUnsafe(
                        furni_count_commits,
                        scope,
                        baseline,
                        response,
                        static state => state.BuildersClubFurniCountRevision);
                    if (commit is null)
                        return false;
                    accepted = commit;
                    return true;
                }
            },
            timeout_ms: request.TimeoutMilliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () =>
            {
                RequireScope(scope);
                lock (commits_sync)
                {
                    baseline = subscriptions.State.BuildersClubFurniCountRevision;
                    accepted = null;
                }
            },
            attempt_start: () =>
            {
                lock (commits_sync)
                {
                    baseline = -1;
                    accepted = null;
                }
            }).ConfigureAwait(false);
        RequireScope(scope);
        ObservedCommit observed;
        lock (commits_sync)
        {
            observed = accepted ??
                throw new InvalidOperationException(
                    "The accepted Builders Club furni-count response was not committed by the passive state owner.");
        }
        BuildersClubFurniCount value = (BuildersClubFurniCount)observed.Update.Value!;
        return new SubscriptionBuildersClubFurniCountRefreshResult(
            scope.Session.Client,
            scope.SessionGeneration,
            observed.Update.State.Revision,
            observed.Update.State.BuildersClubFurniCountRevision,
            observed.ObservedAtUtc,
            value.FurniCount);
    }

    public SubscriptionBuildersClubPlacementDispatchReceipt PlaceBuildersClubFloorOffer(
        SubscriptionBuildersClubFloorPlaceRequest request,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateWireString(request.ExtraData, nameof(request.ExtraData));
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        ValidateExpectedGeneration(request.ExpectedRoomGeneration);
        SubscriptionPlacementScope scope = CapturePlacementScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Subscriptions.BuildersClubFloorOfferPlace,
            new BuildersClubPlaceRoomItem(
                request.PageId,
                request.OfferId,
                request.ExtraData,
                request.X,
                request.Y,
                request.Direction,
                request.IsRetry),
            scope.Session,
            cancellation_token,
            () => room_revision = RequirePlacementScope(scope));
        return PlacementReceipt(
            SubscriptionPlacementKind.Floor,
            scope,
            room_revision,
            request.PageId,
            request.OfferId,
            request.IsRetry);
    }

    public SubscriptionBuildersClubPlacementDispatchReceipt PlaceBuildersClubWallOffer(
        SubscriptionBuildersClubWallPlaceRequest request,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateWallLocation(request.WallLocation, nameof(request.WallLocation));
        ValidateWireString(request.ExtraData, nameof(request.ExtraData));
        ValidateExpectedGeneration(request.ExpectedSessionGeneration);
        ValidateExpectedGeneration(request.ExpectedRoomGeneration);
        SubscriptionPlacementScope scope = CapturePlacementScope(
            request.ExpectedSessionGeneration,
            request.ExpectedRoomGeneration,
            cancellation_token);
        long room_revision = scope.RoomRevision;
        message_dispatcher.Dispatch(
            MessageContracts.Subscriptions.BuildersClubWallOfferPlace,
            new BuildersClubPlaceWallItem(
                request.PageId,
                request.OfferId,
                request.ExtraData,
                request.WallLocation,
                request.IsRetry),
            scope.Session,
            cancellation_token,
            () => room_revision = RequirePlacementScope(scope));
        return PlacementReceipt(
            SubscriptionPlacementKind.Wall,
            scope,
            room_revision,
            request.PageId,
            request.OfferId,
            request.IsRetry);
    }

    void ISubscriptionOperations.RequestUserInfo(string product_name)
    {
        InvokeLegacy(cancellation_token =>
        {
            ArgumentNullException.ThrowIfNull(product_name);
            SubscriptionOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Subscriptions.UserInfoRequest,
                new SubscriptionGetUserInfo(product_name),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    void ISubscriptionOperations.RequestKickbackInfo()
    {
        InvokeLegacy(cancellation_token =>
        {
            SubscriptionOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Subscriptions.KickbackInfoRequest,
                new SubscriptionGetKickbackInfo(),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    void ISubscriptionOperations.RequestBuildersClubFurniCount()
    {
        InvokeLegacy(cancellation_token =>
        {
            SubscriptionOperationScope scope = CaptureScope(null, cancellation_token);
            message_dispatcher.Dispatch(
                MessageContracts.Subscriptions.BuildersClubFurniCountRequest,
                new BuildersClubQueryFurniCount(),
                scope.Session,
                cancellation_token,
                () => RequireScope(scope));
        });
    }

    public void Dispose()
    {
        bool first;
        bool wait = invocation_depth.Value == 0;
        lock (lifecycle_sync)
        {
            first = !dispose_started;
            dispose_started = true;
        }
        if (first)
        {
            subscriptions.UnbindOperations(this);
            subscriptions.StateCommitted -= OnStateCommitted;
            subscriptions.StateChanged -= OnStateChanged;
            lifetime.Cancel();
            lock (commits_sync)
            {
                user_info_commits.Clear();
                kickback_commits.Clear();
                club_offer_commits.Clear();
                furni_count_commits.Clear();
            }
            ClearLeases();
            changed.Dispose();
            lock (lifecycle_sync)
                cleanup_finished = true;
            CompleteDisposalIfReady();
        }
        if (wait)
            disposal.Task.GetAwaiter().GetResult();
    }

    private void OnStateCommitted(SubscriptionStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            DateTimeOffset observed_at = time_provider.GetUtcNow();
            lock (commits_sync)
            {
                if (DisposalStarted())
                    return;
                var observed = new ObservedCommit(
                    update.Kind is SubscriptionStateChangeKind.ClubOffers
                        ? update
                        : WithoutClubOffers(update),
                    observed_at);
                switch (update.Kind)
                {
                    case SubscriptionStateChangeKind.UserInfo:
                        AddCommit(user_info_commits, observed);
                        break;
                    case SubscriptionStateChangeKind.KickbackInfo:
                        AddCommit(kickback_commits, observed);
                        break;
                    case SubscriptionStateChangeKind.ClubOffers:
                        AddCommit(
                            club_offer_commits,
                            observed,
                            club_offer_commit_history_limit);
                        break;
                    case SubscriptionStateChangeKind.BuildersClubFurniCount:
                        AddCommit(furni_count_commits, observed);
                        break;
                    case SubscriptionStateChangeKind.Reset:
                        user_info_commits.Clear();
                        kickback_commits.Clear();
                        club_offer_commits.Clear();
                        furni_count_commits.Clear();
                        break;
                }
            }
            if (update.Kind is SubscriptionStateChangeKind.Reset)
                ClearLeases();
        }
    }

    private void OnStateChanged(SubscriptionStateUpdate update)
    {
        Invocation invocation;
        try
        {
            invocation = EnterInvocation();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        using (invocation)
        {
            if (!PublicationCurrent(update))
                return;
            SubscriptionState state = update.State;
            SubscriptionProductView? product = update.Value is ScrSendUserInfo user_info
                ? ProductView(user_info, state.UserInfoRevision)
                : null;
            SubscriptionKickbackView? kickback = update.Value is ScrSendKickbackInfo kickback_info
                ? KickbackView(kickback_info)
                : null;
            int? furni_count = update.Value is BuildersClubFurniCount count
                ? count.FurniCount
                : null;
            SubscriptionBuildersClubMembershipView? membership =
                update.Value is BuildersClubMembershipStatus membership_status
                    ? MembershipView(membership_status)
                    : null;
            SubscriptionBuildersClubPlacementWarningView? warning =
                update.Value is BuildersClubPlacementWarning placement_warning
                    ? PlacementView(placement_warning)
                    : null;
            SubscriptionClubOffersSummaryView? club_offers =
                update.Value is HabboClubOffers offers
                    ? ClubOffersSummary(offers)
                    : null;
            changed.Publish(
                new SubscriptionChanged(
                    ChangeKind(update.Kind),
                    time_provider.GetUtcNow(),
                    state.Session?.Client,
                    state.SessionGeneration,
                    state.Revision,
                    SourceRevision(update),
                    product,
                    kickback,
                    furni_count,
                    membership,
                    warning)
                {
                    ClubOffers = club_offers
                },
                () => PublicationCurrent(update));
        }
    }

    private ObservedCommit? FindUserInfoCommitUnsafe(
        SubscriptionOperationScope scope,
        long baseline,
        string product_name,
        ScrSendUserInfo response)
    {
        for (int index = user_info_commits.Count - 1; index >= 0; index--)
        {
            ObservedCommit commit = user_info_commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    commit.Update.State.UserInfoRevision) &&
                commit.Update.Value is ScrSendUserInfo value &&
                value == response &&
                string.Equals(
                    value.ProductName,
                    product_name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return commit;
            }
        }
        return null;
    }

    private static ObservedCommit? FindCommitUnsafe<T>(
        List<ObservedCommit> commits,
        SubscriptionOperationScope scope,
        long baseline,
        T response,
        Func<SubscriptionState, long> revision)
    {
        for (int index = commits.Count - 1; index >= 0; index--)
        {
            ObservedCommit commit = commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    revision(commit.Update.State)) &&
                commit.Update.Value is T value &&
                EqualityComparer<T>.Default.Equals(value, response))
            {
                return commit;
            }
        }
        return null;
    }

    private ObservedCommit? FindClubOffersCommitUnsafe(
        SubscriptionOperationScope scope,
        long baseline,
        HabboClubOffers response)
    {
        for (int index = 0; index < club_offer_commits.Count; index++)
        {
            ObservedCommit commit = club_offer_commits[index];
            if (CommitMatches(
                    commit,
                    scope,
                    baseline,
                    commit.Update.State.ClubOffersRevision) &&
                commit.Update.Value is HabboClubOffers value &&
                ClubOffersEqual(value, response))
            {
                return commit;
            }
        }
        return null;
    }

    private static bool ClubOffersEqual(
        HabboClubOffers left,
        HabboClubOffers right)
    {
        if (left.DaysLeft != right.DaysLeft || left.Offers.Count != right.Offers.Count)
            return false;
        for (int index = 0; index < left.Offers.Count; index++)
        {
            if (!ClubOfferEqual(left.Offers[index], right.Offers[index]))
                return false;
        }
        return true;
    }

    private static bool ClubOfferEqual(HabboClubOffer left, HabboClubOffer right) =>
        left.OfferId == right.OfferId &&
        string.Equals(left.ProductCode, right.ProductCode, StringComparison.Ordinal) &&
        left.ReservedWireFlag == right.ReservedWireFlag &&
        left.PriceCredits == right.PriceCredits &&
        left.PriceActivityPoints == right.PriceActivityPoints &&
        left.PriceActivityPointType == right.PriceActivityPointType &&
        left.IsVip == right.IsVip &&
        left.Months == right.Months &&
        left.ExtraDays == right.ExtraDays &&
        left.IsGiftable == right.IsGiftable &&
        left.DaysLeftAfterPurchase == right.DaysLeftAfterPurchase &&
        left.Year == right.Year &&
        left.Month == right.Month &&
        left.Day == right.Day;

    private static bool CommitMatches(
        ObservedCommit commit,
        SubscriptionOperationScope scope,
        long baseline,
        long revision) =>
        baseline >= 0 &&
        ReferenceEquals(commit.Update.State.Session, scope.Session) &&
        commit.Update.State.SessionGeneration == scope.SessionGeneration &&
        revision > baseline;

    private SubscriptionSnapshotLease StoreCurrentLease(string? product_name)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SubscriptionState state = subscriptions.State;
            SubscriptionSnapshotLease lease = StoreLease(state, product_name);
            if (LeaseActive(lease))
                return lease;
            RemoveLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The subscription state changed while its snapshot was being captured.");
    }

    private SubscriptionSnapshotLease StoreLease(
        SubscriptionState state,
        string? product_name)
    {
        SubscriptionProductView[] products = state.UserInfo.Values
            .Where(entry =>
                product_name is null ||
                string.Equals(
                    entry.Value.ProductName,
                    product_name,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Value.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Value.ProductName, StringComparer.Ordinal)
            .Select(entry => ProductView(entry.Value, entry.Revision))
            .ToArray();
        long revision = Interlocked.Increment(ref lease_revision);
        var lease = new SubscriptionSnapshotLease(
            revision,
            state with { ClubOffers = null },
            product_name,
            Array.AsReadOnly(products),
            state.ClubOffers is { } club_offers
                ? ClubOffersSummary(club_offers)
                : null);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            leases.Add(revision, lease);
            lease_order.Enqueue(revision);
            while (leases.Count > lease_limit || lease_order.Count > lease_limit)
                leases.Remove(lease_order.Dequeue());
        }
        return lease;
    }

    private SubscriptionSnapshotLease ReadLease(long revision, string? product_name)
    {
        lock (leases_sync)
        {
            if (!leases.TryGetValue(revision, out SubscriptionSnapshotLease? lease) ||
                !string.Equals(
                    lease.ProductName,
                    product_name,
                    StringComparison.OrdinalIgnoreCase) ||
                !LeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The subscription snapshot is unavailable or does not match the requested filter.");
            }
            return lease;
        }
    }

    private bool LeaseActive(SubscriptionSnapshotLease lease)
    {
        SubscriptionState current = subscriptions.State;
        return ReferenceEquals(current.Session, lease.State.Session) &&
            current.SessionGeneration == lease.State.SessionGeneration &&
            ReferenceEquals(connection.Session, lease.State.Session);
    }

    private ClubOffersSnapshotLease StoreCurrentClubOffersLease()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            SubscriptionState state = subscriptions.State;
            ClubOffersSnapshotLease lease = StoreClubOffersLease(state);
            if (ClubOffersLeaseActive(lease))
                return lease;
            RemoveClubOffersLease(lease.Revision);
        }
        throw new InvalidOperationException(
            "The subscription state changed while the club-offer snapshot was being captured.");
    }

    private ClubOffersSnapshotLease StoreClubOffersLease(SubscriptionState state)
    {
        long revision = Interlocked.Increment(ref club_offer_lease_revision);
        var lease = new ClubOffersSnapshotLease(revision, state, state.ClubOffers);
        lock (leases_sync)
        {
            ThrowIfDisposed();
            club_offer_leases.Add(revision, lease);
            club_offer_lease_order.Enqueue(revision);
            while (club_offer_leases.Count > lease_limit ||
                club_offer_lease_order.Count > lease_limit)
            {
                club_offer_leases.Remove(club_offer_lease_order.Dequeue());
            }
        }
        return lease;
    }

    private ClubOffersSnapshotLease ReadClubOffersLease(long revision)
    {
        lock (leases_sync)
        {
            if (!club_offer_leases.TryGetValue(revision, out ClubOffersSnapshotLease? lease) ||
                !ClubOffersLeaseActive(lease))
            {
                throw new InvalidOperationException(
                    "The club-offer snapshot is unavailable for the active hotel session.");
            }
            return lease;
        }
    }

    private bool ClubOffersLeaseActive(ClubOffersSnapshotLease lease)
    {
        SubscriptionState current = subscriptions.State;
        return ReferenceEquals(current.Session, lease.State.Session) &&
            current.SessionGeneration == lease.State.SessionGeneration &&
            ReferenceEquals(connection.Session, lease.State.Session);
    }

    private SubscriptionClubOffersPage ClubOffersPage(
        ClubOffersSnapshotLease lease,
        int offset,
        int limit)
    {
        HabboClubOffers? snapshot = lease.Offers;
        int total = snapshot?.Offers.Count ?? 0;
        IReadOnlyList<SubscriptionClubOfferView> offers = snapshot is null
            ? Array.Empty<SubscriptionClubOfferView>()
            : SliceClubOffers(snapshot.Offers, offset, limit);
        SubscriptionState state = lease.State;
        bool connected = state.Session is not null &&
            ReferenceEquals(connection.Session, state.Session);
        var page = new SubscriptionClubOffersPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.ClubOffersRevision,
            lease.Revision,
            snapshot is not null,
            snapshot?.DaysLeft,
            total,
            offset,
            NextOffset(offset, offers.Count, total),
            offers);
        if (!ClubOffersLeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the club-offer snapshot was being read.");
        }
        return page;
    }

    private void RemoveLease(long revision)
    {
        lock (leases_sync)
            leases.Remove(revision);
    }

    private void RemoveClubOffersLease(long revision)
    {
        lock (leases_sync)
            club_offer_leases.Remove(revision);
    }

    private void ClearLeases()
    {
        lock (leases_sync)
        {
            leases.Clear();
            lease_order.Clear();
            club_offer_leases.Clear();
            club_offer_lease_order.Clear();
        }
    }

    private async ValueTask<TResult> InvokeRefresh<TResult>(
        CancellationToken cancellation_token,
        Func<CancellationToken, ValueTask<TResult>> invocation)
    {
        cancellation_token.ThrowIfCancellationRequested();
        Invocation active;
        try
        {
            active = EnterInvocation();
        }
        catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        using (active)
        using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            lifetime.Token))
        {
            try
            {
                TResult result = await invocation(linked.Token).ConfigureAwait(false);
                cancellation_token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                return result;
            }
            catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (ObjectDisposedException) when (cancellation_token.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellation_token);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(SubscriptionApplication));
            }
        }
    }

    private void InvokeLegacy(Action<CancellationToken> invocation)
    {
        using Invocation active = EnterInvocation();
        try
        {
            invocation(lifetime.Token);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(SubscriptionApplication));
        }
    }

    private Invocation EnterInvocation()
    {
        lock (lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(dispose_started, this);
            active_invocations++;
        }
        invocation_depth.Value++;
        return new Invocation(this);
    }

    private void LeaveInvocation()
    {
        invocation_depth.Value = Math.Max(0, invocation_depth.Value - 1);
        lock (lifecycle_sync)
            active_invocations--;
        CompleteDisposalIfReady();
    }

    private void CompleteDisposalIfReady()
    {
        bool complete = false;
        lock (lifecycle_sync)
        {
            if (dispose_started &&
                cleanup_finished &&
                active_invocations == 0 &&
                !disposal_finished)
            {
                disposal_finished = true;
                complete = true;
            }
        }
        if (complete)
        {
            lifetime.Dispose();
            disposal.TrySetResult();
        }
    }

    private bool DisposalStarted() => Volatile.Read(ref dispose_started);

    private bool PublicationCurrent(SubscriptionStateUpdate update) =>
        !DisposalStarted() && subscriptions.IsCurrentPublication(update);

    private SubscriptionOperationScope CaptureScope(
        long? expected_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        SubscriptionState state = subscriptions.State;
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        if (!ReferenceEquals(state.Session, session))
        {
            throw new InvalidOperationException(
                "The subscription state is not bound to the active hotel session.");
        }
        if (expected_generation is long expected && expected != state.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The active subscription session generation does not match the expected generation.");
        }
        return new SubscriptionOperationScope(session, state.SessionGeneration);
    }

    private void RequireScope(SubscriptionOperationScope scope)
    {
        ThrowIfDisposed();
        SubscriptionState state = subscriptions.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The hotel session changed during the subscription operation.");
        }
    }

    private SubscriptionPlacementScope CapturePlacementScope(
        long? expected_session_generation,
        long? expected_room_generation,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        return room.Capture(current_room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            SubscriptionState state = subscriptions.State;
            Session session = connection.Session ??
                throw new InvalidOperationException("An active hotel session is required.");
            if (!ReferenceEquals(state.Session, session))
            {
                throw new InvalidOperationException(
                    "The subscription state is not bound to the active hotel session.");
            }
            if (expected_session_generation is long session_generation &&
                session_generation != state.SessionGeneration)
            {
                throw new InvalidOperationException(
                    "The expected hotel-session generation is no longer active.");
            }
            RequireReadyRoom(current_room, expected_room_generation);
            return new SubscriptionPlacementScope(
                session,
                state.SessionGeneration,
                (Id)current_room.RoomId,
                current_room.Generation,
                current_room.Revision);
        });
    }

    private long RequirePlacementScope(SubscriptionPlacementScope scope)
    {
        ThrowIfDisposed();
        return room.Capture(current_room =>
        {
            SubscriptionState state = subscriptions.State;
            if (!ReferenceEquals(connection.Session, scope.Session) ||
                !ReferenceEquals(state.Session, scope.Session) ||
                state.SessionGeneration != scope.SessionGeneration)
            {
                throw new InvalidOperationException(
                    "The hotel session changed before Builders Club placement dispatch.");
            }
            if (!current_room.IsReady ||
                current_room.RoomId != scope.RoomId ||
                current_room.Generation != scope.RoomGeneration)
            {
                throw new InvalidOperationException(
                    "The ready room changed before Builders Club placement dispatch.");
            }
            return current_room.Revision;
        });
    }

    private static void RequireReadyRoom(
        RoomManager current_room,
        long? expected_room_generation)
    {
        if (!current_room.IsReady || current_room.RoomId == 0)
            throw new InvalidOperationException("A ready hotel room is required for placement.");
        if (expected_room_generation is long room_generation &&
            room_generation != current_room.Generation)
        {
            throw new InvalidOperationException(
                "The expected room generation is no longer active.");
        }
    }

    private SubscriptionBuildersClubPlacementDispatchReceipt PlacementReceipt(
        SubscriptionPlacementKind placement_kind,
        SubscriptionPlacementScope scope,
        long room_revision,
        int page_id,
        int offer_id,
        bool is_retry) => new(
        placement_kind,
        scope.Session.Client,
        time_provider.GetUtcNow(),
        scope.SessionGeneration,
        scope.RoomId,
        scope.RoomGeneration,
        room_revision,
        page_id,
        offer_id,
        is_retry,
        1);

    private static SubscriptionProductView ProductView(
        ScrSendUserInfo value,
        long revision) => new(
        revision,
        value.ProductName,
        value.DaysToPeriodEnd,
        value.MemberPeriods,
        value.PeriodsSubscribedAhead,
        value.ResponseType,
        value.HasEverBeenMember,
        value.IsVip,
        value.PastClubDays,
        value.PastVipDays,
        value.MinutesUntilExpiration,
        value.MinutesSinceLastModified);

    private static SubscriptionClubOfferView ClubOfferView(HabboClubOffer value) => new(
        value.OfferId,
        value.ProductCode,
        value.PriceCredits,
        value.PriceActivityPoints,
        value.PriceActivityPointType,
        value.IsVip,
        value.Months,
        value.ExtraDays,
        value.IsGiftable,
        value.DaysLeftAfterPurchase,
        value.Year,
        value.Month,
        value.Day)
    {
        ReservedWireFlag = value.ReservedWireFlag
    };

    private static SubscriptionClubOffersSummaryView ClubOffersSummary(
        HabboClubOffers value) => new(value.DaysLeft, value.Offers.Count);

    private static SubscriptionKickbackView KickbackView(
        ScrSendKickbackInfo value) => new(
        value.CurrentHcStreak,
        value.FirstSubscriptionDate,
        value.KickbackPercentage,
        value.TotalCreditsMissed,
        value.TotalCreditsRewarded,
        value.TotalCreditsSpent,
        value.CreditRewardForStreakBonus,
        value.CreditRewardForMonthlySpent,
        value.TimeUntilPayday);

    private static SubscriptionBuildersClubMembershipView MembershipView(
        BuildersClubMembershipStatus value) => new(
        value.SecondsLeft,
        value.FurniLimit,
        value.MaxFurniLimit,
        value.SecondsLeftWithGrace,
        value.EffectiveSecondsLeftWithGrace);

    private static SubscriptionBuildersClubPlacementWarningView PlacementView(
        BuildersClubPlacementWarning value) => value.Placement switch
        {
            BuildersClubFloorPlacement floor => new(
                value.PageId,
                value.OfferId,
                value.ExtraParam,
                SubscriptionPlacementKind.Floor,
                floor.X,
                floor.Y,
                floor.Direction,
                null),
            BuildersClubWallPlacement wall => new(
                value.PageId,
                value.OfferId,
                value.ExtraParam,
                SubscriptionPlacementKind.Wall,
                null,
                null,
                null,
                wall.WallLocation),
            _ => throw new InvalidDataException(
                $"Unsupported Builders Club placement model '{value.Placement?.GetType().Name ?? "null"}'.")
        };

    private static SubscriptionChangeKind ChangeKind(
        SubscriptionStateChangeKind kind) => kind switch
        {
            SubscriptionStateChangeKind.UserInfo => SubscriptionChangeKind.UserInfo,
            SubscriptionStateChangeKind.KickbackInfo => SubscriptionChangeKind.KickbackInfo,
            SubscriptionStateChangeKind.BuildersClubFurniCount =>
                SubscriptionChangeKind.BuildersClubFurniCount,
            SubscriptionStateChangeKind.BuildersClubMembershipStatus =>
                SubscriptionChangeKind.BuildersClubMembershipStatus,
            SubscriptionStateChangeKind.BuildersClubPlacementWarning =>
                SubscriptionChangeKind.BuildersClubPlacementWarning,
            SubscriptionStateChangeKind.Reset => SubscriptionChangeKind.Reset,
            SubscriptionStateChangeKind.ClubOffers => SubscriptionChangeKind.ClubOffers,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static long SourceRevision(SubscriptionStateUpdate update) => update.Kind switch
    {
        SubscriptionStateChangeKind.UserInfo => update.State.UserInfoRevision,
        SubscriptionStateChangeKind.KickbackInfo => update.State.KickbackRevision,
        SubscriptionStateChangeKind.BuildersClubFurniCount =>
            update.State.BuildersClubFurniCountRevision,
        SubscriptionStateChangeKind.BuildersClubMembershipStatus =>
            update.State.BuildersClubMembershipRevision,
        SubscriptionStateChangeKind.BuildersClubPlacementWarning =>
            update.State.BuildersClubPlacementWarningRevision,
        SubscriptionStateChangeKind.Reset => update.State.Revision,
        SubscriptionStateChangeKind.ClubOffers => update.State.ClubOffersRevision,
        _ => throw new ArgumentOutOfRangeException(nameof(update))
    };

    private static IReadOnlyList<SubscriptionProductView> Slice(
        IReadOnlyList<SubscriptionProductView> values,
        int offset,
        int limit)
    {
        if (offset >= values.Count)
            return Array.Empty<SubscriptionProductView>();
        int count = Math.Min(limit, values.Count - offset);
        var page = new SubscriptionProductView[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static IReadOnlyList<SubscriptionClubOfferView> SliceClubOffers(
        IReadOnlyList<HabboClubOffer> values,
        int offset,
        int limit)
    {
        if (offset >= values.Count)
            return Array.Empty<SubscriptionClubOfferView>();
        int count = Math.Min(limit, values.Count - offset);
        var page = new SubscriptionClubOfferView[count];
        for (int index = 0; index < count; index++)
            page[index] = ClubOfferView(values[offset + index]);
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int consumed = checked(offset + count);
        return consumed < total ? consumed : null;
    }

    private static void AddCommit(
        List<ObservedCommit> commits,
        ObservedCommit commit,
        int history_limit = commit_history_limit)
    {
        commits.Add(commit);
        if (commits.Count > history_limit)
            commits.RemoveAt(0);
    }

    private static SubscriptionStateUpdate WithoutClubOffers(
        SubscriptionStateUpdate update) =>
        update with { State = update.State with { ClubOffers = null } };

    private static void ValidateStateRequest(SubscriptionStateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProductName is not null)
            ValidateProductName(request.ProductName, nameof(request.ProductName));
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        if (request.Limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(request.Limit));
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.Offset != 0 && request.SnapshotRevision is null)
        {
            throw new ArgumentException(
                "Continuation pages require a snapshot revision.",
                nameof(request.SnapshotRevision));
        }
    }

    private static void ValidateClubOffersPageRequest(
        SubscriptionClubOffersPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ValidateLimit(request.Limit);
        if (request.SnapshotRevision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request.SnapshotRevision));
        if (request.Offset != 0 && request.SnapshotRevision is null)
        {
            throw new ArgumentException(
                "Continuation pages require a snapshot revision.",
                nameof(request.SnapshotRevision));
        }
    }

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
    }

    private static void ValidateProductName(string value, string argument_name)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (string.IsNullOrWhiteSpace(value) ||
            Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(argument_name);
        }
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidatePositiveTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidateWireString(string value, string argument_name)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidateWallLocation(string value, string argument_name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, argument_name);
        ValidateWireString(value, argument_name);
    }

    private static void ValidateExpectedGeneration(long? generation)
    {
        if (generation is <= 0)
            throw new ArgumentOutOfRangeException(nameof(generation));
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(DisposalStarted(), this);

    private readonly record struct SubscriptionOperationScope(
        Session Session,
        long SessionGeneration);

    private readonly record struct SubscriptionPlacementScope(
        Session Session,
        long SessionGeneration,
        Id RoomId,
        long RoomGeneration,
        long RoomRevision);

    private sealed record ObservedCommit(
        SubscriptionStateUpdate Update,
        DateTimeOffset ObservedAtUtc);

    private sealed record SubscriptionSnapshotLease(
        long Revision,
        SubscriptionState State,
        string? ProductName,
        IReadOnlyList<SubscriptionProductView> Products,
        SubscriptionClubOffersSummaryView? ClubOffers);

    private sealed record ClubOffersSnapshotLease(
        long Revision,
        SubscriptionState State,
        HabboClubOffers? Offers);

    private sealed class Invocation(SubscriptionApplication owner) : IDisposable
    {
        private SubscriptionApplication? current = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref current, null)?.LeaveInvocation();
        }
    }

    private sealed class GuardedEventSource<T>(Action<Exception>? observer_error) : IDisposable
    {
        private readonly object sync = new();
        private Action<T>? listeners;
        private bool disposed;

        public IDisposable Subscribe(Action<T> listener)
        {
            ArgumentNullException.ThrowIfNull(listener);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                listeners += listener;
            }
            return new Subscription(this, listener);
        }

        public void Publish(T value, Func<bool> current)
        {
            ArgumentNullException.ThrowIfNull(current);
            Action<T>? snapshot;
            lock (sync)
            {
                if (disposed)
                    return;
                snapshot = listeners;
            }
            if (snapshot is null)
                return;
            foreach (Action<T> listener in snapshot.GetInvocationList().Cast<Action<T>>())
            {
                lock (sync)
                {
                    if (disposed)
                        return;
                }
                if (!current())
                    return;
                try
                {
                    listener(value);
                }
                catch (Exception error)
                {
                    observer_error?.Invoke(error);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                listeners = null;
            }
        }

        private void Unsubscribe(Action<T> listener)
        {
            lock (sync)
                listeners -= listener;
        }

        private sealed class Subscription(
            GuardedEventSource<T> source,
            Action<T> listener) : IDisposable
        {
            private GuardedEventSource<T>? current_source = source;
            private Action<T>? current_listener = listener;

            public void Dispose()
            {
                GuardedEventSource<T>? source_value = Interlocked.Exchange(
                    ref current_source,
                    null);
                Action<T>? listener_value = Interlocked.Exchange(ref current_listener, null);
                if (source_value is not null && listener_value is not null)
                    source_value.Unsubscribe(listener_value);
            }
        }
    }
}
