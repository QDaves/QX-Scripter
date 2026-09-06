using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed partial class HabbiconApplication
{
    private const int commit_history_limit = 32;
    private readonly object operations_sync = new();
    private readonly SemaphoreSlim request_serial = new(1, 1);
    private readonly SemaphoreSlim request_signal = new(0, 1);
    private readonly List<ObservedHabbiconCommit> commits = [];

    private ValueTask<HabbiconShopRefreshResult> RefreshShop(
        HabbiconShopRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshShopCore(request, false, token));

    private async ValueTask<HabbiconShopRefreshResult> RefreshShopCore(
        HabbiconShopRefreshRequest request,
        bool ensure_only,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageLimit(request.Limit);
        ValidateTimeout(request.TimeoutMilliseconds);
        HabbiconSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DateTimeOffset deadline = time_provider.GetUtcNow().AddMilliseconds(
            request.TimeoutMilliseconds);
        await request_serial.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            if (ensure_only && habbicons.State.ShopLoaded)
                return CurrentShopResult(scope, request.Limit, 0, time_provider.GetUtcNow());
            ObservedHabbiconCommit? observed = await RequestShop(
                scope,
                deadline,
                request.TimeoutMilliseconds,
                ensure_only,
                cancellation_token).ConfigureAwait(false);
            RequireScope(scope, ShopRequestKey());
            if (observed is null)
                return CurrentShopResult(scope, request.Limit, 0, time_provider.GetUtcNow());
            HabbiconSnapshotLease lease = StoreLease(observed.Update.State);
            HabbiconCollectionPage collections = CollectionPageFor(lease, 0, request.Limit);
            HabbiconEntryPage entries = EntryPageFor(lease, 0, request.Limit);
            RequireLeaseActive(lease);
            return new HabbiconShopRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.Update.State.Revision,
                observed.Update.State.ShopRevision,
                observed.Update.State.UserRevision,
                lease.Revision,
                1,
                collections,
                entries);
        }
        finally
        {
            request_serial.Release();
        }
    }

    private HabbiconShopRefreshResult CurrentShopResult(
        HabbiconSessionScope scope,
        int limit,
        int messages_dispatched,
        DateTimeOffset observed_at)
    {
        RequireScope(scope, ShopRequestKey());
        HabbiconSnapshotLease lease = StoreCurrentLease();
        HabbiconStateData state = lease.State;
        return new HabbiconShopRefreshResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            observed_at,
            scope.SessionGeneration,
            state.Revision,
            state.ShopRevision,
            state.UserRevision,
            lease.Revision,
            messages_dispatched,
            CollectionPageFor(lease, 0, limit),
            EntryPageFor(lease, 0, limit));
    }

    private ValueTask<HabbiconInfoRefreshResult> RefreshInfo(
        HabbiconInfoRefreshRequest request,
        CancellationToken cancellation_token) =>
        InvokeAsync(cancellation_token, token => RefreshInfoCore(request, token));

    private async ValueTask<HabbiconInfoRefreshResult> RefreshInfoCore(
        HabbiconInfoRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        HabbiconSessionScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            cancellation_token);
        DateTimeOffset deadline = time_provider.GetUtcNow().AddMilliseconds(
            request.TimeoutMilliseconds);
        HabbiconRequestKey key = InfoRequestKey(request.HabbiconId);
        await request_serial.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            ObservedHabbiconCommit observed = await RequestInfoSnapshot(
                key,
                request.HabbiconId,
                scope,
                deadline,
                request.TimeoutMilliseconds,
                cancellation_token).ConfigureAwait(false);
            RequireScope(scope, key);
            HabbiconSnapshotLease lease = StoreLease(observed.Update.State);
            HabbiconEntryView value = EntryView(
                observed.Update.Info ??
                    throw new InvalidOperationException("The habbicon info response was not committed."),
                0);
            RequireLeaseActive(lease);
            return new HabbiconInfoRefreshResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                observed.ObservedAtUtc,
                scope.SessionGeneration,
                observed.Update.State.Revision,
                observed.Update.State.InfoRevision,
                lease.Revision,
                1,
                value);
        }
        finally
        {
            request_serial.Release();
        }
    }

    private ValueTask<HabbiconDispatchResult> Dispatch<TApiRequest, TWireRequest>(
        TApiRequest api_request,
        MessageContract<TWireRequest> contract,
        TWireRequest wire_request,
        long? expected_session_generation,
        CancellationToken cancellation_token)
        where TApiRequest : class
        where TWireRequest : IParserComposer<TWireRequest> =>
        InvokeAsync(cancellation_token, token =>
        {
            ArgumentNullException.ThrowIfNull(api_request);
            HabbiconSessionScope scope = CaptureScope(expected_session_generation, token);
            message_dispatcher.Dispatch(contract, wire_request, scope.Session, token);
            return ValueTask.FromResult(new HabbiconDispatchResult(
                scope.Session.Client,
                time_provider.GetUtcNow(),
                scope.SessionGeneration,
                1));
        });

    void IHabbiconOperations.RequestShopData() =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.ShopRequest,
            new HabbiconShopRequest(),
            ShopRequestKey(),
            token));

    void IHabbiconOperations.RequestInfo(int habbicon_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.InfoRequest,
            new HabbiconInfoRequest(habbicon_id),
            InfoRequestKey(habbicon_id),
            token));

    void IHabbiconOperations.Buy(int habbicon_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.Buy,
            new HabbiconBuyRequest(habbicon_id),
            token));

    void IHabbiconOperations.BuyCollection(int collection_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.BuyCollection,
            new HabbiconCollectionBuyRequest(collection_id),
            token));

    void IHabbiconOperations.Claim(int habbicon_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.Claim,
            new HabbiconClaimRequest(habbicon_id),
            token));

    void IHabbiconOperations.Favorite(int habbicon_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.Favorite,
            new HabbiconFavoriteRequest(habbicon_id),
            token));

    void IHabbiconOperations.Unfavorite(int habbicon_id) =>
        InvokeLegacy(token => DispatchLegacy(
            MessageContracts.Habbicons.Unfavorite,
            new HabbiconUnfavoriteRequest(habbicon_id),
            token));

    Task<IReadOnlyList<HabbiconCollection>> IHabbiconOperations.EnsureShopLoadedAsync(
        int timeout_ms,
        CancellationToken cancellation_token) =>
        EnsureShopLoaded(timeout_ms, cancellation_token);

    private async Task<IReadOnlyList<HabbiconCollection>> EnsureShopLoaded(
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        HabbiconShopRefreshResult result = await InvokeAsync(
            cancellation_token,
            token => RefreshShopCore(
                new HabbiconShopRefreshRequest(
                    500,
                    timeout_ms,
                    habbicons.State.SessionGeneration),
                true,
                token)).ConfigureAwait(false);
        var values = new List<HabbiconCollectionView>(result.FirstCollections.Total);
        HabbiconCollectionPage page = result.FirstCollections;
        while (true)
        {
            values.AddRange(page.Collections);
            if (page.NextOffset is not int offset)
                break;
            page = ReadCollections(new HabbiconCollectionPageRequest(
                offset,
                500,
                result.SnapshotRevision));
        }
        return values.Select(CollectionModel).ToArray();
    }

    private static HabbiconCollection CollectionModel(HabbiconCollectionView value) =>
        new(
            value.CollectionId,
            value.Name,
            value.Completed,
            value.RewardHabbiconId,
            (HabbiconState)value.RewardState,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType,
            value.Habbicons.Select(EntryModel).ToArray());

    private static Habbicon EntryModel(HabbiconEntryView value) =>
        new(
            value.HabbiconId,
            value.Name,
            value.CollectionId,
            (HabbiconState)value.State,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType);

    private void DispatchLegacy<T>(
        MessageContract<T> contract,
        T request,
        HabbiconRequestKey correlation,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        HabbiconSessionScope scope = CaptureScope(null, cancellation_token);
        message_dispatcher.Dispatch(
            contract,
            request,
            scope.Session,
            cancellation_token,
            () => habbicons.AdvanceLegacyRequest(
                correlation,
                scope.Session,
                scope.SessionGeneration));
    }

    private void DispatchLegacy<T>(
        MessageContract<T> contract,
        T request,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        HabbiconSessionScope scope = CaptureScope(null, cancellation_token);
        message_dispatcher.Dispatch(contract, request, scope.Session, cancellation_token);
    }

    private async Task<ObservedHabbiconCommit?> RequestShop(
        HabbiconSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        bool ensure_only,
        CancellationToken cancellation_token)
    {
        HabbiconRequestKey key = ShopRequestKey();
        while (true)
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireScope(scope, key);
            HabbiconRequestCorrelation correlation = habbicons.CaptureRequestCorrelation(
                key,
                scope.Session,
                scope.SessionGeneration);
            if (ensure_only && correlation.State.ShopLoaded)
                return null;
            int remaining = RemainingMilliseconds(deadline, timeout_milliseconds, key);
            if (correlation.OutstandingRequests != 0)
            {
                await WaitForRequestChange(remaining, cancellation_token).ConfigureAwait(false);
                continue;
            }
            var await_state = new RequestAwaitState(key)
            {
                RequestBaseline = correlation.RequestEpoch,
                SourceBaseline = correlation.State.ShopRevision,
                EnsureOnly = ensure_only
            };
            try
            {
                await requests.RequestAsync(
                    MessageContracts.Habbicons.ShopRequest,
                    new HabbiconShopRequest(),
                    MessageContracts.Habbicons.ShopSnapshot,
                    scope.Session,
                    match: _ => Match(await_state, scope),
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
            }
            catch (ShopAlreadyLoadedException) when (ensure_only)
            {
                return null;
            }
            RequireScope(scope, key);
            return await_state.Accepted ??
                throw new InvalidOperationException("The habbicon shop response was not committed.");
        }
    }

    private async Task<ObservedHabbiconCommit> RequestInfoSnapshot(
        HabbiconRequestKey key,
        int habbicon_id,
        HabbiconSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        while (true)
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireScope(scope, key);
            HabbiconRequestCorrelation correlation = habbicons.CaptureRequestCorrelation(
                key,
                scope.Session,
                scope.SessionGeneration);
            int remaining = RemainingMilliseconds(deadline, timeout_milliseconds, key);
            if (correlation.OutstandingRequests != 0)
            {
                await WaitForRequestChange(remaining, cancellation_token).ConfigureAwait(false);
                continue;
            }
            var await_state = new RequestAwaitState(key)
            {
                RequestBaseline = correlation.RequestEpoch,
                SourceBaseline = correlation.State.InfoRevision
            };
            await requests.RequestAsync(
                MessageContracts.Habbicons.InfoRequest,
                new HabbiconInfoRequest(habbicon_id),
                MessageContracts.Habbicons.InfoSnapshot,
                scope.Session,
                match: response =>
                    response.Habbicon.HabbiconId == habbicon_id && Match(await_state, scope),
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
            RequireScope(scope, key);
            return await_state.Accepted ??
                throw new InvalidOperationException("The habbicon info response was not committed.");
        }
    }

    private void Arm(
        RequestAwaitState await_state,
        HabbiconSessionScope scope,
        DateTimeOffset deadline,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        _ = RemainingMilliseconds(deadline, timeout_milliseconds, await_state.Key);
        long? expected = await_state.EnsureOnly
            ? habbicons.AdvanceTypedShopRequestIfUnloaded(
                await_state.RequestBaseline,
                scope.Session,
                scope.SessionGeneration)
            : habbicons.AdvanceTypedRequest(
                await_state.Key,
                await_state.RequestBaseline,
                scope.Session,
                scope.SessionGeneration);
        if (expected is null)
            throw new ShopAlreadyLoadedException();
        lock (operations_sync)
        {
            await_state.ExpectedRequestEpoch = expected.Value;
            await_state.Armed = true;
        }
    }

    private bool Match(RequestAwaitState await_state, HabbiconSessionScope scope)
    {
        lock (operations_sync)
        {
            if (!await_state.Armed || await_state.Accepted is not null || !ScopeCurrent(scope))
                return false;
            ObservedHabbiconCommit? accepted = commits.FirstOrDefault(commit =>
                commit.Update.Request == await_state.Key &&
                commit.Update.RequestEpoch == await_state.ExpectedRequestEpoch &&
                scope.Matches(commit.Update.State) &&
                SourceRevision(commit.Update) > await_state.SourceBaseline);
            if (accepted is null)
                return false;
            await_state.Accepted = accepted;
            await_state.Armed = false;
            return true;
        }
    }

    private void ObserveCommit(HabbiconStateUpdate update)
    {
        if (!TryEnterInvocation(out Invocation? active))
            return;
        using (active)
        {
            lock (operations_sync)
            {
                if (update.Kind is HabbiconStateChangeKind.Reset)
                {
                    commits.Clear();
                    ClearLeases();
                }
                else if (update.Kind is HabbiconStateChangeKind.ShopSnapshot or
                    HabbiconStateChangeKind.Info)
                {
                    commits.Add(new ObservedHabbiconCommit(update, time_provider.GetUtcNow()));
                    while (commits.Count > commit_history_limit)
                        commits.RemoveAt(0);
                }
            }
            PulseRequest();
        }
    }

    private async Task WaitForRequestChange(
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        await request_signal.WaitAsync(
            TimeSpan.FromMilliseconds(timeout_milliseconds),
            cancellation_token).ConfigureAwait(false);
    }

    private void PulseRequest()
    {
        try
        {
            request_signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private void ClearOperationState()
    {
        lock (operations_sync)
            commits.Clear();
        PulseRequest();
    }

    private HabbiconSessionScope CaptureScope(
        long? expected_session_generation,
        CancellationToken cancellation_token)
    {
        if (expected_session_generation is <= 0)
            throw new ArgumentOutOfRangeException(nameof(expected_session_generation));
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        Session session = connection.Session ??
            throw new InvalidOperationException("An active hotel session is required.");
        HabbiconStateData state = habbicons.State;
        if (!ReferenceEquals(state.Session, session))
            throw new InvalidOperationException("The habbicon state is not bound to the active hotel session.");
        if (expected_session_generation is long expected && expected != state.SessionGeneration)
            throw new InvalidOperationException("The habbicon session generation does not match.");
        return new HabbiconSessionScope(session, state.SessionGeneration);
    }

    private void RequireScope(HabbiconSessionScope scope, HabbiconRequestKey key)
    {
        ThrowIfDisposed();
        if (!ScopeCurrent(scope))
        {
            throw new RequestDisconnectedException(
                RequestKey(key).ToString(),
                ResponseKey(key).ToString());
        }
    }

    private bool ScopeCurrent(HabbiconSessionScope scope) =>
        !DisposalStarted() && scope.Matches(connection.Session, habbicons.State);

    private int RemainingMilliseconds(
        DateTimeOffset deadline,
        int original,
        HabbiconRequestKey key)
    {
        double remaining = (deadline - time_provider.GetUtcNow()).TotalMilliseconds;
        if (remaining <= 0)
        {
            throw new RequestTimeoutException(
                RequestKey(key).ToString(),
                ResponseKey(key).ToString(),
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

    private static HabbiconRequestKey ShopRequestKey() =>
        new(HabbiconRequestRoute.Shop, 0);

    private static HabbiconRequestKey InfoRequestKey(int habbicon_id) =>
        new(HabbiconRequestRoute.Info, habbicon_id);

    private static MessageKey RequestKey(HabbiconRequestKey key) => key.Route switch
    {
        HabbiconRequestRoute.Shop => MessageKeys.Habbicons.ShopRequest,
        HabbiconRequestRoute.Info => MessageKeys.Habbicons.InfoRequest,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static MessageKey ResponseKey(HabbiconRequestKey key) => key.Route switch
    {
        HabbiconRequestRoute.Shop => MessageKeys.Habbicons.ShopSnapshot,
        HabbiconRequestRoute.Info => MessageKeys.Habbicons.InfoSnapshot,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private readonly record struct HabbiconSessionScope(Session Session, long SessionGeneration)
    {
        public bool Matches(HabbiconStateData state) =>
            ReferenceEquals(Session, state.Session) && SessionGeneration == state.SessionGeneration;

        public bool Matches(Session? active, HabbiconStateData state) =>
            ReferenceEquals(Session, active) && Matches(state);
    }

    private sealed class RequestAwaitState(HabbiconRequestKey key)
    {
        public HabbiconRequestKey Key { get; } = key;
        public long RequestBaseline { get; init; }
        public long SourceBaseline { get; init; }
        public bool EnsureOnly { get; init; }
        public long ExpectedRequestEpoch { get; set; } = -1;
        public bool Armed { get; set; }
        public ObservedHabbiconCommit? Accepted { get; set; }
    }

    private sealed record ObservedHabbiconCommit(
        HabbiconStateUpdate Update,
        DateTimeOffset ObservedAtUtc);

    private sealed class ShopAlreadyLoadedException : Exception;
}
