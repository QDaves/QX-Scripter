using System.Globalization;
using System.Text;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Protocol;

namespace Qx.Game.Application;

internal sealed class CatalogApplication : IApplicationFeature, ICatalogBrowseOperations, ICatalogPurchaseOperations
{
    private const int lane_limit = 32;
    private const int walk_lane_limit = 4;
    private const int lease_limit = 16;
    private const int output_limit = 500;
    private const int purchase_product_limit = 256;
    private const int purchase_item_limit = (output_limit - purchase_product_limit) / 2;
    private const int worker_timeout_milliseconds = 120000;
    private static readonly long maximum_age_milliseconds =
        TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond;

    private readonly IConnection connection;
    private readonly CatalogManager catalog;
    private readonly RequestBroker requests;
    private readonly ApplicationMessageDispatcher message_dispatcher;
    private readonly TimeProvider time_provider;
    private readonly Action<Exception>? observer_error;
    private readonly ApplicationEventSource<CatalogPublishedEvent> published;
    private readonly ApplicationEventSource<CatalogPurchaseOutcomeEvent> purchase_outcomes;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object lane_sync = new();
    private readonly Dictionary<IndexLaneKey, SharedLane<IndexFetch>> index_lanes = [];
    private readonly Dictionary<PageLaneKey, SharedLane<PageFetch>> page_lanes = [];
    private readonly Dictionary<WalkLaneKey, SharedLane<LoadFetch>> walk_lanes = [];
    private readonly object lease_sync = new();
    private readonly Dictionary<long, SnapshotLease> leases = [];
    private readonly Queue<long> lease_order = [];
    private readonly object lifecycle_sync = new();
    private readonly TaskCompletionSource disposal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly AsyncLocal<int> invocation_depth = new();
    private readonly AsyncLocal<object?> lane_context = new();
    private long lease_revision;
    private int active_lanes;
    private int active_walk_lanes;
    private int active_invocations;
    private int active_workers;
    private bool dispose_started;
    private bool cleanup_finished;
    private bool disposal_finished;

    public CatalogApplication(
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
        catalog = game.Catalog;
        requests = game.Requests;
        this.message_dispatcher = message_dispatcher;
        this.time_provider = time_provider;
        this.observer_error = observer_error;
        published = new ApplicationEventSource<CatalogPublishedEvent>(observer_error);
        purchase_outcomes = new ApplicationEventSource<CatalogPurchaseOutcomeEvent>(observer_error);
        Bindings = Array.AsReadOnly<IApplicationBinding>(
        [
            new ApplicationCallBinding<CatalogStateRequest, CatalogStateView>(
                CatalogApplicationDescriptors.State,
                (request, _) => ValueTask.FromResult(ReadState(request))),
            new ApplicationCallBinding<CatalogIndexGetRequest, CatalogIndexView>(
                CatalogApplicationDescriptors.IndexGet,
                GetIndexView),
            new ApplicationCallBinding<CatalogPageGetRequest, CatalogPageView>(
                CatalogApplicationDescriptors.PageGet,
                GetPageView),
            new ApplicationCallBinding<CatalogLoadRequest, CatalogLoadView>(
                CatalogApplicationDescriptors.PagesLoad,
                LoadPagesView),
            new ApplicationCallBinding<CatalogPagesRequest, CatalogPageListView>(
                CatalogApplicationDescriptors.PagesList,
                (request, _) => ValueTask.FromResult(ListPages(request))),
            new ApplicationCallBinding<CatalogOfferSearchRequest, CatalogOfferSearchPage>(
                CatalogApplicationDescriptors.OffersSearch,
                (request, _) => ValueTask.FromResult(SearchOffers(request))),
            new ApplicationCallBinding<CatalogCacheClearRequest, CatalogCacheClearView>(
                CatalogApplicationDescriptors.CacheClear,
                (request, _) => ValueTask.FromResult(Clear(request))),
            new ApplicationCallBinding<CatalogPurchaseStateRequest, CatalogPurchaseStateView>(
                CatalogApplicationDescriptors.PurchaseState,
                (request, _) => ValueTask.FromResult(ReadPurchaseState(request))),
            new ApplicationCallBinding<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
                CatalogApplicationDescriptors.PurchaseSend,
                (request, cancellation_token) =>
                    ValueTask.FromResult(SendPurchase(request, cancellation_token))),
            new ApplicationEventBinding<CatalogPurchaseOutcomeEvent>(
                CatalogApplicationDescriptors.PurchaseOutcome,
                purchase_outcomes.Subscribe),
            new ApplicationEventBinding<CatalogPublishedEvent>(
                CatalogApplicationDescriptors.Published,
                published.Subscribe)
        ]);
        try
        {
            catalog.CacheInvalidated += OnCacheInvalidated;
            catalog.InvalidationPublished += OnInvalidationPublished;
            catalog.PurchaseOutcomePublished += OnPurchaseOutcomePublished;
            catalog.BindBrowseOperations(this);
            catalog.BindPurchaseOperations(this);
        }
        catch
        {
            catalog.CacheInvalidated -= OnCacheInvalidated;
            catalog.InvalidationPublished -= OnInvalidationPublished;
            catalog.PurchaseOutcomePublished -= OnPurchaseOutcomePublished;
            catalog.UnbindPurchaseOperations(this);
            catalog.UnbindBrowseOperations(this);
            purchase_outcomes.Dispose();
            published.Dispose();
            lifetime.Dispose();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public CatalogStateView ReadState(CatalogStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        CatalogCacheSnapshot snapshot = catalog.Snapshot(catalog_type);
        bool connected = Connected(snapshot.State);
        return new CatalogStateView(
            connected,
            connected ? snapshot.State.Session!.Client : null,
            snapshot.State.SessionGeneration,
            snapshot.State.CatalogGeneration,
            snapshot.State.Revision,
            catalog_type,
            snapshot.Index is not null,
            snapshot.Index?.ReceivedAtUtc,
            snapshot.Index is { } index ? AgeMilliseconds(index.Age) : null,
            snapshot.Pages.Count,
            snapshot.Pages.Sum(page => page.Offers.Count),
            snapshot.State.LastPublication,
            snapshot.State.LastPublishedAtUtc);
    }

    public async ValueTask<CatalogIndexView> GetIndexView(
        CatalogIndexGetRequest request,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        TimeSpan max_age = MaxAge(request.MaxAgeMilliseconds);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration,
            cancellation_token);
        IndexFetch fetched = await GetIndexCore(
            scope,
            catalog_type,
            max_age,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(fetched.Scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        return IndexView(fetched);
    }

    public async ValueTask<CatalogPageView> GetPageView(
        CatalogPageGetRequest request,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageId(request.PageId);
        ValidateOfferId(request.OfferId);
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        TimeSpan max_age = MaxAge(request.MaxAgeMilliseconds);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration,
            cancellation_token);
        PageFetch fetched = await GetPageCore(
            scope,
            request.PageId,
            request.OfferId,
            catalog_type,
            max_age,
            request.TimeoutMilliseconds,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(fetched.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        return PageView(fetched, request.OfferId);
    }

    public async ValueTask<CatalogLoadView> LoadPagesView(
        CatalogLoadRequest request,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        ArgumentOutOfRangeException.ThrowIfNegative(request.DelayMilliseconds);
        TimeSpan max_age = MaxAge(request.MaxAgeMilliseconds);
        ValidateTimeout(request.TimeoutMilliseconds);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerScope scope = CaptureScope(
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration,
            cancellation_token);
        LoadFetch loaded = await LoadCore(
            scope,
            catalog_type,
            request.OnlyVisible,
            request.DelayMilliseconds,
            max_age,
            request.TimeoutMilliseconds,
            null,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(loaded.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        return new CatalogLoadView(
            loaded.Scope.Session.Client,
            loaded.Scope.SessionGeneration,
            loaded.Scope.CatalogGeneration,
            loaded.StateRevision,
            loaded.CompletedAtUtc,
            catalog_type,
            request.OnlyVisible,
            loaded.Report.Loaded,
            loaded.Report.AlreadyCached,
            loaded.Report.Refused,
            loaded.Report.Total,
            loaded.Report.Available);
    }

    public CatalogPageListView ListPages(CatalogPagesRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        ValidatePaging(request.Offset, request.Limit, request.SnapshotRevision);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerState current = catalog.State;
        RequireExpected(current, request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        SnapshotLease lease = request.SnapshotRevision is { } revision
            ? ReadLease(revision, LeaseKind.Pages, catalog_type, string.Empty, current)
            : StorePageLease(catalog_type, current);
        IReadOnlyList<CatalogPageSummaryView> page = Slice(lease.Pages!, request.Offset, request.Limit);
        return new CatalogPageListView(
            Connected(lease.State),
            Connected(lease.State) ? lease.State.Session!.Client : null,
            lease.State.SessionGeneration,
            lease.State.CatalogGeneration,
            lease.State.Revision,
            lease.Revision,
            catalog_type,
            lease.Pages!.Count,
            request.Offset,
            NextOffset(request.Offset, page.Count, lease.Pages.Count),
            page);
    }

    public CatalogOfferSearchPage SearchOffers(CatalogOfferSearchRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Text);
        if (Encoding.UTF8.GetByteCount(request.Text) > 1024)
            throw new ArgumentOutOfRangeException(nameof(request.Text));
        string catalog_type = NormalizeCatalogType(request.CatalogType);
        ValidatePaging(request.Offset, request.Limit, request.SnapshotRevision);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerState current = catalog.State;
        RequireExpected(current, request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        SnapshotLease lease = request.SnapshotRevision is { } revision
            ? ReadLease(revision, LeaseKind.Offers, catalog_type, request.Text, current)
            : StoreOfferLease(catalog_type, request.Text, current);
        IReadOnlyList<CatalogOfferSearchMatchView> page = Slice(
            lease.Offers!,
            request.Offset,
            request.Limit);
        return new CatalogOfferSearchPage(
            Connected(lease.State),
            Connected(lease.State) ? lease.State.Session!.Client : null,
            lease.State.SessionGeneration,
            lease.State.CatalogGeneration,
            lease.State.Revision,
            lease.Revision,
            request.Text,
            catalog_type,
            lease.Offers!.Count,
            request.Offset,
            NextOffset(request.Offset, page.Count, lease.Offers.Count),
            page);
    }

    public CatalogCacheClearView Clear(CatalogCacheClearRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        string? catalog_type = request.CatalogType is null
            ? null
            : NormalizeCatalogType(request.CatalogType);
        CatalogInvalidationUpdate update = catalog.Clear(
            catalog_type,
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration);
        return new CatalogCacheClearView(
            update.State.Session?.Client,
            update.State.SessionGeneration,
            update.State.CatalogGeneration,
            update.State.Revision,
            update.ChangedAtUtc,
            catalog_type);
    }

    public CatalogPurchaseStateView ReadPurchaseState(CatalogPurchaseStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        return CapturePurchaseStateView();
    }

    public CatalogPurchaseDispatchReceipt SendPurchase(
        CatalogPurchaseSendRequest request,
        CancellationToken cancellation_token = default)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePageId(request.PageId);
        ArgumentOutOfRangeException.ThrowIfNegative(request.OfferId);
        ArgumentNullException.ThrowIfNull(request.ExtraData);
        if (Encoding.UTF8.GetByteCount(request.ExtraData) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.ExtraData));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Quantity);
        ValidateExpected(request.ExpectedSessionGeneration, request.ExpectedCatalogGeneration);
        CatalogManagerScope scope = CapturePurchaseScope(
            request.ExpectedSessionGeneration,
            request.ExpectedCatalogGeneration,
            cancellation_token);
        CatalogPurchaseState baseline = catalog.PurchaseState;
        if (!ReferenceEquals(baseline.Session, scope.Session) ||
            baseline.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The catalog purchase state is not bound to the active hotel session.");
        }
        message_dispatcher.Dispatch(
            MessageContracts.Catalog.Purchase,
            new PurchaseFromCatalogRequest(
                request.PageId,
                request.OfferId,
                request.ExtraData,
                request.Quantity),
            scope.Session,
            cancellation_token,
            () => RequirePurchaseCurrent(scope));
        return new CatalogPurchaseDispatchReceipt(
            scope.Session.Client,
            scope.SessionGeneration,
            scope.CatalogGeneration,
            request.PageId,
            request.OfferId,
            request.Quantity,
            1,
            time_provider.GetUtcNow());
    }

    async Task<CatalogIndex> ICatalogBrowseOperations.GetIndexAsync(
        string catalog_type,
        TimeSpan? max_age,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        string normalized = NormalizeCatalogType(catalog_type);
        TimeSpan age = LegacyMaxAge(max_age);
        ValidateTimeout(timeout_ms);
        CatalogManagerScope scope = CaptureScope(null, null, cancellation_token);
        IndexFetch fetched = await GetIndexCore(
            scope,
            normalized,
            age,
            timeout_ms,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(fetched.Scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        return fetched.Value;
    }

    async Task<CatalogPage> ICatalogBrowseOperations.GetPageAsync(
        int page_id,
        string catalog_type,
        TimeSpan? max_age,
        int offer_id,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ValidatePageId(page_id);
        ValidateOfferId(offer_id);
        string normalized = NormalizeCatalogType(catalog_type);
        TimeSpan age = LegacyMaxAge(max_age);
        ValidateTimeout(timeout_ms);
        CatalogManagerScope scope = CaptureScope(null, null, cancellation_token);
        PageFetch fetched = await GetPageCore(
            scope,
            page_id,
            offer_id,
            normalized,
            age,
            timeout_ms,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(fetched.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        return fetched.Value;
    }

    async Task<CatalogLoadReport> ICatalogBrowseOperations.LoadAllPagesAsync(
        string catalog_type,
        bool only_visible,
        int delay_ms,
        TimeSpan? max_age,
        int timeout_ms,
        IProgress<(int Loaded, int Total)>? progress,
        CancellationToken cancellation_token)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentOutOfRangeException.ThrowIfNegative(delay_ms);
        string normalized = NormalizeCatalogType(catalog_type);
        TimeSpan age = LegacyMaxAge(max_age);
        ValidateTimeout(timeout_ms);
        CatalogManagerScope scope = CaptureScope(null, null, cancellation_token);
        LoadFetch loaded = await LoadCore(
            scope,
            normalized,
            only_visible,
            delay_ms,
            age,
            timeout_ms,
            progress,
            cancellation_token).ConfigureAwait(false);
        cancellation_token.ThrowIfCancellationRequested();
        RequireCurrent(loaded.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        return loaded.Report;
    }

    IReadOnlyList<CatalogPage> ICatalogBrowseOperations.CachedPages(string catalog_type)
    {
        using Invocation invocation = EnterInvocation();
        return catalog.Snapshot(NormalizeCatalogType(catalog_type)).Pages;
    }

    IReadOnlyList<CatalogOfferMatch> ICatalogBrowseOperations.CachedOffers(string catalog_type)
    {
        using Invocation invocation = EnterInvocation();
        return Offers(catalog.Snapshot(NormalizeCatalogType(catalog_type)), null, null);
    }

    CatalogCacheState ICatalogBrowseOperations.CacheState(string catalog_type)
    {
        using Invocation invocation = EnterInvocation();
        return catalog.ReadCacheState(NormalizeCatalogType(catalog_type));
    }

    IReadOnlyList<CatalogOfferMatch> ICatalogBrowseOperations.FindOffers(
        string text,
        string catalog_type,
        Func<CatalogProduct, string?>? describe)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentException.ThrowIfNullOrEmpty(text);
        return Offers(catalog.Snapshot(NormalizeCatalogType(catalog_type)), text, describe);
    }

    void ICatalogBrowseOperations.ClearCache(string? catalog_type)
    {
        using Invocation invocation = EnterInvocation();
        catalog.Clear(
            catalog_type is null ? null : NormalizeCatalogType(catalog_type),
            null,
            null);
    }

    Task<CatalogPurchaseOutcome> ICatalogPurchaseOperations.PurchaseAsync(
        PurchaseFromCatalogRequest request,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            _ = SendPurchase(
                new CatalogPurchaseSendRequest(
                    request.PageId,
                    request.OfferId,
                    request.ExtraData,
                    request.Quantity),
                cancellation_token);
            return Task.FromResult(DispatchedPurchase());
        }
        catch (Exception error)
        {
            return Task.FromException<CatalogPurchaseOutcome>(error);
        }
    }

    Task<CatalogPurchaseOutcome> ICatalogPurchaseOperations.DispatchCompatibility(
        Action send,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        try
        {
            cancellation_token.ThrowIfCancellationRequested();
            using Invocation invocation = EnterInvocation();
            ArgumentNullException.ThrowIfNull(send);
            cancellation_token.ThrowIfCancellationRequested();
            send();
            return Task.FromResult(DispatchedPurchase());
        }
        catch (Exception error)
        {
            return Task.FromException<CatalogPurchaseOutcome>(error);
        }
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
            catalog.UnbindBrowseOperations(this);
            catalog.UnbindPurchaseOperations(this);
            catalog.CacheInvalidated -= OnCacheInvalidated;
            catalog.InvalidationPublished -= OnInvalidationPublished;
            catalog.PurchaseOutcomePublished -= OnPurchaseOutcomePublished;
            lifetime.Cancel();
            CancelAllLanes();
            ClearLeases();
            purchase_outcomes.Dispose();
            published.Dispose();
            lock (lifecycle_sync)
                cleanup_finished = true;
            CompleteDisposalIfReady();
        }
        if (wait)
            disposal.Task.GetAwaiter().GetResult();
    }

    private async Task<IndexFetch> GetIndexCore(
        CatalogManagerScope scope,
        string catalog_type,
        TimeSpan max_age,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        long started = time_provider.GetTimestamp();
        CatalogCommitStatus status = catalog.ReadIndex(
            scope,
            catalog_type,
            max_age,
            out CatalogCachedIndex? cached,
            out CatalogManagerState state);
        ThrowStatus(status, scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        if (cached is { } value)
        {
            return new IndexFetch(
                value.Value,
                scope,
                state.Revision,
                value.ReceivedAtUtc,
                true);
        }
        var key = new IndexLaneKey(
            scope.SessionGeneration,
            scope.CatalogGeneration,
            catalog_type);
        SharedLane<IndexFetch> lane = AcquireLane(
            index_lanes,
            key,
            scope,
            catalog_type,
            token => FetchIndex(scope, catalog_type, token),
            false);
        int remaining = Remaining(started, timeout_milliseconds);
        return await AwaitLane(
            lane,
            remaining,
            timeout_milliseconds,
            MessageKeys.Catalog.IndexRequest,
            MessageKeys.Catalog.IndexSnapshot,
            cancellation_token).ConfigureAwait(false);
    }

    private async Task<PageFetch> GetPageCore(
        CatalogManagerScope scope,
        int page_id,
        int offer_id,
        string catalog_type,
        TimeSpan max_age,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        long started = time_provider.GetTimestamp();
        CatalogCommitStatus status = catalog.ReadPage(
            scope,
            catalog_type,
            page_id,
            max_age,
            out CatalogCachedPage? cached,
            out long version,
            out CatalogManagerState state);
        ThrowStatus(status, scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        if (cached is { } value)
        {
            return new PageFetch(
                value.Value,
                scope,
                state.Revision,
                value.ReceivedAtUtc,
                true);
        }
        var key = new PageLaneKey(
            scope.SessionGeneration,
            scope.CatalogGeneration,
            catalog_type,
            page_id,
            offer_id,
            version);
        SharedLane<PageFetch> lane = AcquireLane(
            page_lanes,
            key,
            scope,
            catalog_type,
            token => FetchPage(scope, page_id, offer_id, catalog_type, version, token),
            false);
        int remaining = Remaining(started, timeout_milliseconds);
        return await AwaitLane(
            lane,
            remaining,
            timeout_milliseconds,
            MessageKeys.Catalog.PageRequest,
            MessageKeys.Catalog.PageSnapshot,
            cancellation_token).ConfigureAwait(false);
    }

    private async Task<IndexFetch> FetchIndex(
        CatalogManagerScope scope,
        string catalog_type,
        CancellationToken cancellation_token)
    {
        RequireCurrent(scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        CatalogIndex response = await requests.RequestAsync(
            MessageContracts.Catalog.IndexRequest,
            new CatalogIndexRequest(catalog_type),
            MessageContracts.Catalog.IndexSnapshot,
            scope.Session,
            match: value =>
                string.Equals(value.CatalogType, catalog_type, StringComparison.OrdinalIgnoreCase) &&
                ScopeActive(scope),
            timeout_ms: worker_timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireCurrent(
                scope,
                MessageKeys.Catalog.IndexRequest,
                MessageKeys.Catalog.IndexSnapshot)).ConfigureAwait(false);
        RequireCurrent(scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        CatalogCommitStatus status = catalog.TryCommitIndex(
            scope,
            catalog_type,
            response,
            out CatalogCachedIndex committed,
            out CatalogManagerState state);
        ThrowStatus(status, scope, MessageKeys.Catalog.IndexRequest, MessageKeys.Catalog.IndexSnapshot);
        var committed_scope = new CatalogManagerScope(
            scope.Session,
            state.SessionGeneration,
            state.CatalogGeneration);
        return new IndexFetch(
            committed.Value,
            committed_scope,
            state.Revision,
            committed.ReceivedAtUtc,
            false);
    }

    private async Task<PageFetch> FetchPage(
        CatalogManagerScope scope,
        int page_id,
        int offer_id,
        string catalog_type,
        long expected_version,
        CancellationToken cancellation_token)
    {
        RequireCurrent(scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        CatalogPage response = await requests.RequestAsync(
            MessageContracts.Catalog.PageRequest,
            new CatalogPageRequest(page_id, offer_id, catalog_type),
            MessageContracts.Catalog.PageSnapshot,
            scope.Session,
            match: value =>
                value.PageId == page_id &&
                string.Equals(value.CatalogType, catalog_type, StringComparison.OrdinalIgnoreCase) &&
                ScopeActive(scope),
            timeout_ms: worker_timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: 2,
            dispatch_guard: () => RequireCurrent(
                scope,
                MessageKeys.Catalog.PageRequest,
                MessageKeys.Catalog.PageSnapshot)).ConfigureAwait(false);
        RequireCurrent(scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        CatalogCommitStatus status = catalog.TryCommitPage(
            scope,
            catalog_type,
            expected_version,
            response,
            out CatalogCachedPage committed,
            out CatalogManagerState state);
        if (status is CatalogCommitStatus.Superseded)
        {
            CatalogCommitStatus current_status = catalog.ReadPage(
                scope,
                catalog_type,
                page_id,
                Timeout.InfiniteTimeSpan,
                out CatalogCachedPage? current,
                out _,
                out CatalogManagerState current_state);
            if (current_status is CatalogCommitStatus.Committed && current is { } winner)
            {
                return new PageFetch(
                    winner.Value,
                    scope,
                    current_state.Revision,
                    winner.ReceivedAtUtc,
                    true);
            }
            ThrowStatus(
                current_status,
                scope,
                MessageKeys.Catalog.PageRequest,
                MessageKeys.Catalog.PageSnapshot);
        }
        ThrowStatus(status, scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        return new PageFetch(
            committed.Value,
            scope,
            state.Revision,
            committed.ReceivedAtUtc,
            false);
    }

    private async Task<LoadFetch> LoadCore(
        CatalogManagerScope scope,
        string catalog_type,
        bool only_visible,
        int delay_milliseconds,
        TimeSpan max_age,
        int timeout_milliseconds,
        IProgress<(int Loaded, int Total)>? progress,
        CancellationToken cancellation_token)
    {
        var key = new WalkLaneKey(
            catalog_type,
            only_visible,
            delay_milliseconds,
            max_age.Ticks,
            timeout_milliseconds);
        SharedLane<LoadFetch> lane = AcquireWalkLane(
            key,
            scope,
            catalog_type,
            only_visible,
            delay_milliseconds,
            max_age,
            timeout_milliseconds);
        if (progress is not null)
        {
            lock (lane_sync)
                lane.Progress.Add(progress);
        }
        try
        {
            return await AwaitLane(
                lane,
                null,
                timeout_milliseconds,
                MessageKeys.Catalog.PageRequest,
                MessageKeys.Catalog.PageSnapshot,
                cancellation_token).ConfigureAwait(false);
        }
        finally
        {
            if (progress is not null)
            {
                lock (lane_sync)
                    lane.Progress.Remove(progress);
            }
        }
    }

    private async Task<LoadFetch> LoadWalk(
        SharedLane<LoadFetch> lane,
        string catalog_type,
        bool only_visible,
        int delay_milliseconds,
        TimeSpan max_age,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
    {
        Interlocked.Exchange(ref lane.AcceptIndexRefresh, 1);
        IndexFetch index;
        try
        {
            index = await GetIndexCore(
                lane.Scope,
                catalog_type,
                max_age,
                timeout_milliseconds,
                cancellation_token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref lane.AcceptIndexRefresh, 0);
        }
        lane.Scope = index.Scope;
        IReadOnlyList<CatalogNode> nodes = PageNodes(index.Value.Root, only_visible);
        int loaded = 0;
        int already_cached = 0;
        int refused = 0;
        int done = 0;
        foreach (CatalogNode node in nodes)
        {
            cancellation_token.ThrowIfCancellationRequested();
            RequireCurrent(lane.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
            CatalogCommitStatus read = catalog.ReadPage(
                lane.Scope,
                catalog_type,
                node.PageId,
                max_age,
                out CatalogCachedPage? cached,
                out _,
                out _);
            ThrowStatus(read, lane.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
            if (cached is not null)
            {
                already_cached++;
            }
            else
            {
                bool pause_after_request = false;
                try
                {
                    PageFetch page = await GetPageCore(
                        lane.Scope,
                        node.PageId,
                        -1,
                        catalog_type,
                        max_age,
                        timeout_milliseconds,
                        cancellation_token).ConfigureAwait(false);
                    if (page.FromCache)
                        already_cached++;
                    else
                    {
                        loaded++;
                        pause_after_request = true;
                    }
                }
                catch (RequestTimeoutException)
                {
                    refused++;
                    pause_after_request = true;
                }
                if (pause_after_request && delay_milliseconds > 0)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(delay_milliseconds),
                        time_provider,
                        cancellation_token).ConfigureAwait(false);
                }
            }
            ReportProgress(lane, ++done, nodes.Count);
        }
        RequireCurrent(lane.Scope, MessageKeys.Catalog.PageRequest, MessageKeys.Catalog.PageSnapshot);
        CatalogManagerState state = catalog.State;
        return new LoadFetch(
            new CatalogLoadReport(loaded, already_cached, refused, nodes.Count),
            lane.Scope,
            state.Revision,
            time_provider.GetUtcNow());
    }

    private SharedLane<TValue> AcquireLane<TKey, TValue>(
        Dictionary<TKey, SharedLane<TValue>> lanes,
        TKey key,
        CatalogManagerScope scope,
        string catalog_type,
        Func<CancellationToken, Task<TValue>> work,
        bool walk)
        where TKey : notnull
    {
        ThrowIfDisposed();
        SharedLane<TValue> lane;
        bool created = false;
        lock (lane_sync)
        {
            if (lanes.TryGetValue(key, out SharedLane<TValue>? existing) &&
                existing.Accepting &&
                SameScope(existing.Scope, scope))
            {
                lane = existing;
            }
            else
            {
                if (active_lanes >= lane_limit || walk && active_walk_lanes >= walk_lane_limit)
                    throw new InvalidOperationException("The catalog request lane limit has been reached.");
                lane = new SharedLane<TValue>(scope, catalog_type, work);
                lanes[key] = lane;
                active_lanes++;
                if (walk)
                    active_walk_lanes++;
                created = true;
            }
            lane.Waiters++;
        }
        if (created)
            _ = RunLane(lane, () => RemoveLane(lanes, key, lane, walk));
        return lane;
    }

    private SharedLane<LoadFetch> AcquireWalkLane(
        WalkLaneKey key,
        CatalogManagerScope scope,
        string catalog_type,
        bool only_visible,
        int delay_milliseconds,
        TimeSpan max_age,
        int timeout_milliseconds)
    {
        ThrowIfDisposed();
        SharedLane<LoadFetch> lane;
        bool created = false;
        lock (lane_sync)
        {
            if (walk_lanes.TryGetValue(key, out SharedLane<LoadFetch>? existing) &&
                existing.Accepting &&
                SameScope(existing.Scope, scope))
            {
                lane = existing;
            }
            else
            {
                if (active_lanes >= lane_limit || active_walk_lanes >= walk_lane_limit)
                    throw new InvalidOperationException("The catalog request lane limit has been reached.");
                lane = new SharedLane<LoadFetch>(scope, catalog_type, _ => Task.FromException<LoadFetch>(
                    new InvalidOperationException("The catalog walk was not initialized.")));
                lane.Work = token => LoadWalk(
                    lane,
                    catalog_type,
                    only_visible,
                    delay_milliseconds,
                    max_age,
                    timeout_milliseconds,
                    token);
                walk_lanes[key] = lane;
                active_lanes++;
                active_walk_lanes++;
                created = true;
            }
            lane.Waiters++;
        }
        if (created)
            _ = RunLane(lane, () => RemoveLane(walk_lanes, key, lane, true));
        return lane;
    }

    private async Task RunLane<TValue>(SharedLane<TValue> lane, Action remove)
    {
        bool entered = false;
        object? previous_lane = lane_context.Value;
        try
        {
            EnterWorker();
            entered = true;
            lane_context.Value = lane;
            using CancellationTokenSource cancellation =
                CancellationTokenSource.CreateLinkedTokenSource(lane.Cancellation.Token, lifetime.Token);
            TValue value = await lane.Work(cancellation.Token).ConfigureAwait(false);
            lane.Completion.TrySetResult(value);
        }
        catch (OperationCanceledException) when (lane.Poison is { } poison)
        {
            lane.Completion.TrySetException(poison);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            lane.Completion.TrySetException(new ObjectDisposedException(nameof(CatalogApplication)));
        }
        catch (OperationCanceledException)
        {
            lane.Completion.TrySetCanceled(lane.Cancellation.Token);
        }
        catch (Exception error)
        {
            lane.Completion.TrySetException(error);
        }
        finally
        {
            lane_context.Value = previous_lane;
            remove();
            lane.Cancellation.Dispose();
            if (entered)
                LeaveWorker();
        }
    }

    private async Task<TValue> AwaitLane<TValue>(
        SharedLane<TValue> lane,
        int? timeout_milliseconds,
        int original_timeout_milliseconds,
        MessageKey outgoing,
        MessageKey incoming,
        CancellationToken cancellation_token)
    {
        try
        {
            if (timeout_milliseconds is { } timeout)
            {
                if (timeout <= 0)
                    throw new TimeoutException();
                using CancellationTokenSource timed_cancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellation_token, lifetime.Token);
                return await lane.Completion.Task.WaitAsync(
                    TimeSpan.FromMilliseconds(timeout),
                    time_provider,
                    timed_cancellation.Token)
                    .ConfigureAwait(false);
            }
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellation_token, lifetime.Token);
            return await lane.Completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (Exception) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (lane.Poison is { } poison)
        {
            throw poison;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(CatalogApplication));
        }
        catch (TimeoutException) when (lane.Poison is { } poison)
        {
            throw poison;
        }
        catch (TimeoutException)
        {
            throw new RequestTimeoutException(
                outgoing.Value,
                incoming.Value,
                original_timeout_milliseconds);
        }
        finally
        {
            ReleaseLane(lane);
        }
    }

    private void ReleaseLane<TValue>(SharedLane<TValue> lane)
    {
        bool cancel = false;
        lock (lane_sync)
        {
            if (lane.Waiters > 0)
                lane.Waiters--;
            if (lane.Waiters == 0 && !lane.Completion.Task.IsCompleted)
            {
                lane.Accepting = false;
                cancel = true;
            }
        }
        if (cancel)
            Cancel(lane.Cancellation);
    }

    private void RemoveLane<TKey, TValue>(
        Dictionary<TKey, SharedLane<TValue>> lanes,
        TKey key,
        SharedLane<TValue> lane,
        bool walk)
        where TKey : notnull
    {
        lock (lane_sync)
        {
            if (lanes.TryGetValue(key, out SharedLane<TValue>? current) &&
                ReferenceEquals(current, lane))
            {
                lanes.Remove(key);
            }
            active_lanes--;
            if (walk)
                active_walk_lanes--;
        }
    }

    private void OnCacheInvalidated(CatalogInvalidationUpdate update)
    {
        ClearLeases();
        List<CancellationTokenSource> cancellations = [];
        object? committing_lane = lane_context.Value;
        lock (lane_sync)
        {
            foreach (SharedLane<IndexFetch> lane in index_lanes.Values.Distinct())
            {
                if (update.Kind is CatalogInvalidationKind.IndexRefreshed &&
                    ReferenceEquals(lane, committing_lane))
                {
                    continue;
                }
                PoisonIfOld(lane, update, cancellations);
            }
            foreach (SharedLane<PageFetch> lane in page_lanes.Values.Distinct())
                PoisonIfOld(lane, update, cancellations);
            foreach (SharedLane<LoadFetch> lane in walk_lanes.Values.Distinct())
            {
                if (update.Kind is CatalogInvalidationKind.IndexRefreshed &&
                    string.Equals(update.CatalogType, lane.CatalogType, StringComparison.Ordinal) &&
                    ReferenceEquals(update.State.Session, lane.Scope.Session) &&
                    update.State.SessionGeneration == lane.Scope.SessionGeneration &&
                    Interlocked.Exchange(ref lane.AcceptIndexRefresh, 0) == 1)
                {
                    lane.Scope = new CatalogManagerScope(
                        lane.Scope.Session,
                        update.State.SessionGeneration,
                        update.State.CatalogGeneration);
                    continue;
                }
                PoisonIfOld(lane, update, cancellations);
            }
        }
        foreach (CancellationTokenSource cancellation in cancellations)
            Cancel(cancellation);
    }

    private static void PoisonIfOld<TValue>(
        SharedLane<TValue> lane,
        CatalogInvalidationUpdate update,
        List<CancellationTokenSource> cancellations)
    {
        if (ReferenceEquals(update.State.Session, lane.Scope.Session) &&
            update.State.SessionGeneration == lane.Scope.SessionGeneration &&
            update.State.CatalogGeneration == lane.Scope.CatalogGeneration)
        {
            return;
        }
        lane.Accepting = false;
        lane.Poison ??= update.Kind is CatalogInvalidationKind.Reset or CatalogInvalidationKind.SessionChanged
            ? new RequestDisconnectedException(
                MessageKeys.Catalog.IndexRequest.Value,
                MessageKeys.Catalog.IndexSnapshot.Value)
            : new CatalogInvalidatedException(
                lane.Scope.SessionGeneration,
                lane.Scope.CatalogGeneration,
                update.State.SessionGeneration,
                update.State.CatalogGeneration);
        cancellations.Add(lane.Cancellation);
    }

    private void OnInvalidationPublished(CatalogInvalidationUpdate update)
    {
        if (update.Publication is not { } publication || update.State.Session is not { } session)
            return;
        published.Publish(new CatalogPublishedEvent(
            session.Client,
            update.State.SessionGeneration,
            update.State.CatalogGeneration,
            update.State.Revision,
            update.ChangedAtUtc,
            publication));
    }

    private void OnPurchaseOutcomePublished(CatalogPurchaseUpdate update)
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
            if (update.State.Session is not { } session ||
                update.State.LastOutcome is not { } outcome ||
                update.State.LastOutcomeAtUtc is not { } received_at)
            {
                return;
            }
            purchase_outcomes.Publish(new CatalogPurchaseOutcomeEvent(
                session.Client,
                update.State.SessionGeneration,
                update.State.Revision,
                received_at,
                PurchaseOutcomeView(outcome)));
        }
    }

    private void CancelAllLanes()
    {
        CancellationTokenSource[] cancellations;
        lock (lane_sync)
        {
            cancellations =
            [
                .. index_lanes.Values.Select(value => value.Cancellation),
                .. page_lanes.Values.Select(value => value.Cancellation),
                .. walk_lanes.Values.Select(value => value.Cancellation)
            ];
            foreach (SharedLane<IndexFetch> lane in index_lanes.Values)
                lane.Accepting = false;
            foreach (SharedLane<PageFetch> lane in page_lanes.Values)
                lane.Accepting = false;
            foreach (SharedLane<LoadFetch> lane in walk_lanes.Values)
                lane.Accepting = false;
        }
        foreach (CancellationTokenSource cancellation in cancellations.Distinct())
            Cancel(cancellation);
    }

    private void ReportProgress(SharedLane<LoadFetch> lane, int loaded, int total)
    {
        IProgress<(int Loaded, int Total)>[] observers;
        lock (lane_sync)
            observers = lane.Progress.ToArray();
        foreach (IProgress<(int Loaded, int Total)> observer in observers)
        {
            try
            {
                observer.Report((loaded, total));
            }
            catch (Exception error)
            {
                observer_error?.Invoke(error);
            }
        }
    }

    private SnapshotLease StorePageLease(string catalog_type, CatalogManagerState expected)
    {
        CatalogCacheSnapshot snapshot = catalog.Snapshot(catalog_type);
        RequireSameState(expected, snapshot.State);
        CatalogPageSummaryView[] pages = snapshot.Pages.Select(PageSummary).ToArray();
        return StoreLease(new SnapshotLease(
            NextLeaseRevision(),
            LeaseKind.Pages,
            snapshot.State,
            catalog_type,
            string.Empty,
            Array.AsReadOnly(pages),
            null));
    }

    private SnapshotLease StoreOfferLease(
        string catalog_type,
        string text,
        CatalogManagerState expected)
    {
        CatalogCacheSnapshot snapshot = catalog.Snapshot(catalog_type);
        RequireSameState(expected, snapshot.State);
        Dictionary<int, CatalogNode> nodes = IndexNodes(snapshot.Index?.Value.Root);
        CatalogOfferSearchMatchView[] offers = snapshot.Pages
            .SelectMany(page => page.Offers.Select(offer => new
            {
                Offer = offer,
                View = OfferSearchView(
                    page,
                    offer,
                    nodes.GetValueOrDefault(page.PageId))
            }))
            .Where(value =>
                text.Length == 0 ||
                SearchMatch(value.View, text) ||
                ProductMatch(value.Offer, text, null))
            .Select(value => value.View)
            .OrderBy(value => value.PageId)
            .ThenBy(value => value.OfferId)
            .ThenBy(value => value.LocalizationId, StringComparer.Ordinal)
            .ToArray();
        return StoreLease(new SnapshotLease(
            NextLeaseRevision(),
            LeaseKind.Offers,
            snapshot.State,
            catalog_type,
            text,
            null,
            Array.AsReadOnly(offers)));
    }

    private SnapshotLease StoreLease(SnapshotLease lease)
    {
        lock (lease_sync)
        {
            ThrowIfDisposed();
            RequireSameState(lease.State, catalog.State);
            leases.Add(lease.Revision, lease);
            lease_order.Enqueue(lease.Revision);
            while (leases.Count > lease_limit && lease_order.TryDequeue(out long revision))
                leases.Remove(revision);
        }
        return lease;
    }

    private SnapshotLease ReadLease(
        long revision,
        LeaseKind kind,
        string catalog_type,
        string text,
        CatalogManagerState current)
    {
        lock (lease_sync)
        {
            if (!leases.TryGetValue(revision, out SnapshotLease? lease) ||
                lease.Kind != kind ||
                !string.Equals(lease.CatalogType, catalog_type, StringComparison.Ordinal) ||
                !string.Equals(lease.Text, text, StringComparison.Ordinal) ||
                !SameStateEpoch(lease.State, current))
            {
                throw new InvalidOperationException(
                    "The catalog snapshot lease is unavailable or does not match the requested query.");
            }
            return lease;
        }
    }

    private void ClearLeases()
    {
        lock (lease_sync)
        {
            leases.Clear();
            lease_order.Clear();
        }
    }

    private long NextLeaseRevision()
    {
        long revision = Interlocked.Increment(ref lease_revision);
        if (revision <= 0)
            throw new InvalidOperationException("The catalog snapshot revision space is exhausted.");
        return revision;
    }

    private CatalogIndexView IndexView(IndexFetch fetched)
    {
        var views = new List<CatalogNodeView>(output_limit);
        int total = 0;
        var stack = new Stack<(CatalogNode Node, int? Parent, int Depth)>();
        stack.Push((fetched.Value.Root, null, 0));
        while (stack.TryPop(out var entry))
        {
            total++;
            if (views.Count < output_limit)
            {
                views.Add(new CatalogNodeView(
                    entry.Node.PageId,
                    entry.Parent,
                    entry.Depth,
                    entry.Node.Visible,
                    entry.Node.Icon,
                    entry.Node.PageName,
                    entry.Node.Localization,
                    entry.Node.OfferIds.Count,
                    entry.Node.Children.Count));
            }
            for (int index = entry.Node.Children.Count - 1; index >= 0; index--)
            {
                stack.Push((
                    entry.Node.Children[index],
                    entry.Node.PageId >= 0 ? entry.Node.PageId : null,
                    entry.Depth + 1));
            }
        }
        return new CatalogIndexView(
            fetched.Scope.Session.Client,
            fetched.Scope.SessionGeneration,
            fetched.Scope.CatalogGeneration,
            fetched.StateRevision,
            fetched.ReceivedAtUtc,
            fetched.FromCache,
            fetched.Value.CatalogType,
            fetched.Value.NewAdditionsAvailable,
            total,
            total > views.Count,
            Array.AsReadOnly(views.ToArray()));
    }

    private CatalogPageView PageView(PageFetch fetched, int requested_offer_id)
    {
        CatalogPage page = fetched.Value;
        int[] selected_indices = Enumerable.Range(0, Math.Min(page.Offers.Count, output_limit)).ToArray();
        int target_index = requested_offer_id >= 0
            ? page.Offers.ToList().FindIndex(offer => offer.OfferId == requested_offer_id)
            : -1;
        if (target_index >= output_limit && selected_indices.Length == output_limit)
            selected_indices[^1] = target_index;
        Array.Sort(selected_indices);
        int target_products = target_index >= 0 ? page.Offers[target_index].Products.Count : 0;
        int remaining_products = output_limit - target_products;
        var offers = new List<CatalogOfferView>(selected_indices.Length);
        foreach (int index in selected_indices)
        {
            CatalogPageOffer offer = page.Offers[index];
            int take = index == target_index
                ? offer.Products.Count
                : Math.Min(offer.Products.Count, Math.Max(0, remaining_products));
            if (index != target_index)
                remaining_products -= take;
            CatalogProductView[] products = offer.Products
                .Take(take)
                .Select(ProductView)
                .ToArray();
            offers.Add(new CatalogOfferView(
                offer.OfferId,
                offer.LocalizationId,
                offer.IsRent,
                offer.PriceInCredits,
                offer.PriceInActivityPoints,
                offer.ActivityPointType,
                offer.PriceInSilver,
                offer.Giftable,
                offer.ClubLevel,
                offer.BundlePurchaseAllowed,
                offer.IsPet,
                offer.PreviewImage,
                offer.Products.Count,
                products.Length != offer.Products.Count,
                Array.AsReadOnly(products)));
        }
        string[] images = page.Localization.Images.Take(output_limit).ToArray();
        string[] texts = page.Localization.Texts.Take(output_limit).ToArray();
        CatalogFrontPageItemView[] front = (page.FrontPageItems ?? [])
            .Take(output_limit)
            .Select(FrontPageItemView)
            .ToArray();
        return new CatalogPageView(
            fetched.Scope.Session.Client,
            fetched.Scope.SessionGeneration,
            fetched.Scope.CatalogGeneration,
            fetched.StateRevision,
            fetched.ReceivedAtUtc,
            fetched.FromCache,
            page.PageId,
            page.CatalogType,
            page.LayoutCode,
            page.OfferId,
            page.AcceptSeasonCurrencyAsCredits,
            page.Localization.Images.Count,
            images.Length != page.Localization.Images.Count,
            Array.AsReadOnly(images),
            page.Localization.Texts.Count,
            texts.Length != page.Localization.Texts.Count,
            Array.AsReadOnly(texts),
            page.Offers.Count,
            offers.Count != page.Offers.Count,
            Array.AsReadOnly(offers.ToArray()),
            page.FrontPageItems?.Count ?? 0,
            front.Length != (page.FrontPageItems?.Count ?? 0),
            Array.AsReadOnly(front));
    }

    private static CatalogProductView ProductView(CatalogProduct value) => new(
        value.ProductType,
        value.FurniClassId,
        value.ExtraParam,
        value.ProductCount,
        value.UniqueLimitedItem,
        value.UniqueLimitedItemSeriesSize,
        value.UniqueLimitedItemsLeft,
        value.UnityProductType);

    private CatalogPurchaseStateView CapturePurchaseStateView()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Session? before = connection.Session;
            CatalogPurchaseState state = catalog.PurchaseState;
            Session? after = connection.Session;
            if (ReferenceEquals(before, after))
                return PurchaseStateView(state, before);
        }
        throw new InvalidOperationException(
            "The hotel session changed while the catalog purchase state was being read.");
    }

    private static CatalogPurchaseStateView PurchaseStateView(
        CatalogPurchaseState state,
        Session? active_session)
    {
        bool connected = state.Session is not null &&
            ReferenceEquals(state.Session, active_session);
        return new CatalogPurchaseStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            connected && state.LastOutcome is { } outcome
                ? PurchaseOutcomeView(outcome)
                : null,
            connected ? state.LastOutcomeAtUtc : null);
    }

    private static CatalogPurchaseOutcomeView PurchaseOutcomeView(
        CatalogPurchaseOutcome outcome) => outcome.Status switch
    {
        CatalogPurchaseStatus.Completed when outcome.Offer is { } offer => new(
            CatalogPurchaseOutcomeKind.Accepted,
            PurchaseOfferView(offer),
            0),
        CatalogPurchaseStatus.Failed => new(
            CatalogPurchaseOutcomeKind.Failed,
            null,
            outcome.ErrorCode),
        CatalogPurchaseStatus.NotAllowed => new(
            CatalogPurchaseOutcomeKind.Forbidden,
            null,
            outcome.ErrorCode),
        _ => throw new InvalidDataException("The passive catalog purchase outcome is invalid.")
    };

    private static CatalogPurchaseOfferView PurchaseOfferView(PurchaseOffer offer)
    {
        CatalogProductView[] products = offer.Products
            .Take(purchase_product_limit)
            .Select(ProductView)
            .ToArray();
        Id[] room_items = (offer.RoomItems ?? [])
            .Take(purchase_item_limit)
            .ToArray();
        Id[] wall_items = (offer.WallItems ?? [])
            .Take(purchase_item_limit)
            .ToArray();
        return new CatalogPurchaseOfferView(
            offer.OfferId,
            offer.LocalizationId,
            offer.IsRent,
            offer.PriceInCredits,
            offer.PriceInActivityPoints,
            offer.ActivityPointType,
            offer.Giftable,
            offer.ClubLevel,
            offer.BundlePurchaseAllowed,
            offer.Products.Count,
            products.Length != offer.Products.Count,
            Array.AsReadOnly(products),
            offer.GiftTo,
            offer.RoomItems?.Count ?? 0,
            room_items.Length != (offer.RoomItems?.Count ?? 0),
            Array.AsReadOnly(room_items),
            offer.WallItems?.Count ?? 0,
            wall_items.Length != (offer.WallItems?.Count ?? 0),
            Array.AsReadOnly(wall_items));
    }

    private static CatalogPurchaseOutcome DispatchedPurchase() => new(
        CatalogPurchaseStatus.Dispatched,
        null,
        0);

    private static CatalogFrontPageItemView FrontPageItemView(CatalogFrontPageItem value) => new(
        value.Position,
        value.ItemName,
        value.ItemPromoImage,
        value.Type,
        value.CataloguePageLocation,
        value.ProductOfferId,
        value.ProductCode,
        value.ExpirationSeconds);

    private static CatalogPageSummaryView PageSummary(CatalogPage page) => new(
        page.PageId,
        page.CatalogType,
        page.LayoutCode,
        page.OfferId,
        page.Offers.Count,
        page.Offers.Sum(offer => offer.Products.Count),
        page.AcceptSeasonCurrencyAsCredits);

    private static CatalogOfferSearchMatchView OfferSearchView(
        CatalogPage page,
        CatalogPageOffer offer,
        CatalogNode? node)
    {
        CatalogProduct? first = offer.Products.FirstOrDefault();
        return new CatalogOfferSearchMatchView(
            page.PageId,
            node?.PageName ?? string.Empty,
            node?.Localization ?? string.Empty,
            node?.Visible ?? false,
            offer.OfferId,
            offer.LocalizationId,
            offer.IsRent,
            offer.PriceInCredits,
            offer.PriceInActivityPoints,
            offer.ActivityPointType,
            offer.PriceInSilver,
            offer.Giftable,
            offer.ClubLevel,
            offer.BundlePurchaseAllowed,
            offer.IsPet,
            offer.Products.Count,
            first?.ProductType,
            first?.FurniClassId,
            first?.ExtraParam);
    }

    private static bool SearchMatch(CatalogOfferSearchMatchView value, string text) =>
        value.LocalizationId.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        value.PageName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        value.PageLocalization.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        value.FirstProductType?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
        value.FirstExtraParam?.Contains(text, StringComparison.OrdinalIgnoreCase) == true ||
        string.Equals(
            value.FirstFurniClassId?.ToString(CultureInfo.InvariantCulture),
            text,
            StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<CatalogOfferMatch> Offers(
        CatalogCacheSnapshot snapshot,
        string? text,
        Func<CatalogProduct, string?>? describe)
    {
        Dictionary<int, CatalogNode> nodes = IndexNodes(snapshot.Index?.Value.Root);
        var values = new List<CatalogOfferMatch>();
        foreach (CatalogPage page in snapshot.Pages)
        {
            foreach (CatalogPageOffer offer in page.Offers)
            {
                if (text is not null && !LegacyMatch(offer, text, describe))
                    continue;
                values.Add(new CatalogOfferMatch(
                    offer,
                    page,
                    nodes.GetValueOrDefault(page.PageId)));
            }
        }
        return Array.AsReadOnly(values.ToArray());
    }

    private static bool LegacyMatch(
        CatalogPageOffer offer,
        string text,
        Func<CatalogProduct, string?>? describe)
    {
        if (offer.LocalizationId.Contains(text, StringComparison.OrdinalIgnoreCase))
            return true;
        return ProductMatch(offer, text, describe);
    }

    private static bool ProductMatch(
        CatalogPageOffer offer,
        string text,
        Func<CatalogProduct, string?>? describe)
    {
        foreach (CatalogProduct product in offer.Products)
        {
            if (product.ProductType.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;
            if (product.ExtraParam.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;
            if (product.FurniClassId.ToString(CultureInfo.InvariantCulture).Equals(
                text,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (describe?.Invoke(product) is { Length: > 0 } value &&
                value.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static Dictionary<int, CatalogNode> IndexNodes(CatalogNode? root)
    {
        var values = new Dictionary<int, CatalogNode>();
        if (root is null)
            return values;
        var stack = new Stack<CatalogNode>();
        stack.Push(root);
        while (stack.TryPop(out CatalogNode? node))
        {
            if (node.PageId >= 0)
                values.TryAdd(node.PageId, node);
            for (int index = node.Children.Count - 1; index >= 0; index--)
                stack.Push(node.Children[index]);
        }
        return values;
    }

    private static IReadOnlyList<CatalogNode> PageNodes(CatalogNode root, bool only_visible)
    {
        var values = new List<CatalogNode>();
        var stack = new Stack<CatalogNode>();
        stack.Push(root);
        while (stack.TryPop(out CatalogNode? node))
        {
            if (only_visible && !node.Visible)
                continue;
            if (node.PageId >= 0 && node.OfferIds.Count > 0)
                values.Add(node);
            for (int index = node.Children.Count - 1; index >= 0; index--)
                stack.Push(node.Children[index]);
        }
        return Array.AsReadOnly(values.ToArray());
    }

    private CatalogManagerScope CaptureScope(
        long? expected_session_generation,
        long? expected_catalog_generation,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        CatalogManagerScope scope = catalog.CaptureScope(
            expected_session_generation,
            expected_catalog_generation);
        if (!ReferenceEquals(connection.Session, scope.Session))
            throw new RequestDisconnectedException(
                MessageKeys.Catalog.IndexRequest.Value,
                MessageKeys.Catalog.IndexSnapshot.Value);
        return scope;
    }

    private CatalogManagerScope CapturePurchaseScope(
        long? expected_session_generation,
        long? expected_catalog_generation,
        CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        CatalogManagerScope scope = catalog.CaptureScope(
            expected_session_generation,
            expected_catalog_generation);
        if (!ReferenceEquals(connection.Session, scope.Session))
        {
            throw new InvalidOperationException(
                "The hotel session changed before the catalog purchase dispatch.");
        }
        return scope;
    }

    private bool ScopeActive(CatalogManagerScope scope)
    {
        CatalogManagerState state = catalog.State;
        return ReferenceEquals(connection.Session, scope.Session) &&
            ReferenceEquals(state.Session, scope.Session) &&
            state.SessionGeneration == scope.SessionGeneration &&
            state.CatalogGeneration == scope.CatalogGeneration;
    }

    private void RequireCurrent(
        CatalogManagerScope scope,
        MessageKey outgoing,
        MessageKey incoming)
    {
        ThrowIfDisposed();
        CatalogManagerState state = catalog.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new RequestDisconnectedException(outgoing.Value, incoming.Value);
        }
        if (state.CatalogGeneration != scope.CatalogGeneration)
        {
            throw new CatalogInvalidatedException(
                scope.SessionGeneration,
                scope.CatalogGeneration,
                state.SessionGeneration,
                state.CatalogGeneration);
        }
    }

    private void RequirePurchaseCurrent(CatalogManagerScope scope)
    {
        ThrowIfDisposed();
        CatalogManagerState state = catalog.State;
        if (!ReferenceEquals(connection.Session, scope.Session) ||
            !ReferenceEquals(state.Session, scope.Session) ||
            state.SessionGeneration != scope.SessionGeneration)
        {
            throw new InvalidOperationException(
                "The hotel session changed before the catalog purchase dispatch.");
        }
        if (state.CatalogGeneration != scope.CatalogGeneration)
        {
            throw new CatalogInvalidatedException(
                scope.SessionGeneration,
                scope.CatalogGeneration,
                state.SessionGeneration,
                state.CatalogGeneration);
        }
    }

    private void ThrowStatus(
        CatalogCommitStatus status,
        CatalogManagerScope scope,
        MessageKey outgoing,
        MessageKey incoming)
    {
        if (status is CatalogCommitStatus.Committed)
            return;
        if (status is CatalogCommitStatus.SessionChanged)
            throw new RequestDisconnectedException(outgoing.Value, incoming.Value);
        CatalogManagerState state = catalog.State;
        throw new CatalogInvalidatedException(
            scope.SessionGeneration,
            scope.CatalogGeneration,
            state.SessionGeneration,
            state.CatalogGeneration);
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

    private void EnterWorker()
    {
        lock (lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(dispose_started, this);
            active_workers++;
        }
    }

    private void LeaveWorker()
    {
        lock (lifecycle_sync)
            active_workers--;
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
                active_workers == 0 &&
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

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref dispose_started), this);

    private static string NormalizeCatalogType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (string.Equals(value, "NORMAL", StringComparison.OrdinalIgnoreCase))
            return "NORMAL";
        if (string.Equals(value, "BUILDERS_CLUB", StringComparison.OrdinalIgnoreCase))
            return "BUILDERS_CLUB";
        throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static TimeSpan MaxAge(long milliseconds)
    {
        if (milliseconds == -1)
            return Timeout.InfiniteTimeSpan;
        if (milliseconds < 0 || milliseconds > maximum_age_milliseconds)
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        return TimeSpan.FromTicks(checked(milliseconds * TimeSpan.TicksPerMillisecond));
    }

    private static TimeSpan LegacyMaxAge(TimeSpan? value)
    {
        TimeSpan age = value ?? CatalogManager.DefaultMaxAge;
        if (age == Timeout.InfiniteTimeSpan)
            return age;
        if (age < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(value));
        return age;
    }

    private static void ValidateTimeout(int value)
    {
        if (value is < 1 or > worker_timeout_milliseconds)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ValidatePageId(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
    }

    private static void ValidateOfferId(int value)
    {
        if (value < -1)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ValidateExpected(long? session_generation, long? catalog_generation)
    {
        if (session_generation < 0)
            throw new ArgumentOutOfRangeException(nameof(session_generation));
        if (catalog_generation < 0)
            throw new ArgumentOutOfRangeException(nameof(catalog_generation));
    }

    private static void ValidatePaging(int offset, int limit, long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > output_limit)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
        if (offset != 0 && snapshot_revision is null)
            throw new ArgumentException("Continuation pages require a snapshot revision.", nameof(snapshot_revision));
    }

    private static void RequireExpected(
        CatalogManagerState state,
        long? session_generation,
        long? catalog_generation)
    {
        if (session_generation is { } expected_session && expected_session != state.SessionGeneration)
            throw new InvalidOperationException("The catalog session generation does not match the expected value.");
        if (catalog_generation is { } expected_catalog && expected_catalog != state.CatalogGeneration)
            throw new InvalidOperationException("The catalog generation does not match the expected value.");
    }

    private static void RequireSameState(CatalogManagerState expected, CatalogManagerState current)
    {
        if (!SameStateEpoch(expected, current) || expected.Revision != current.Revision)
            throw new InvalidOperationException("The catalog state changed while creating the snapshot lease.");
    }

    private static bool SameStateEpoch(CatalogManagerState left, CatalogManagerState right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.CatalogGeneration == right.CatalogGeneration;

    private bool Connected(CatalogManagerState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private static bool SameScope(CatalogManagerScope left, CatalogManagerScope right) =>
        ReferenceEquals(left.Session, right.Session) &&
        left.SessionGeneration == right.SessionGeneration &&
        left.CatalogGeneration == right.CatalogGeneration;

    private int Remaining(long started, int timeout_milliseconds)
    {
        double remaining = timeout_milliseconds - time_provider.GetElapsedTime(started).TotalMilliseconds;
        return remaining <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(remaining));
    }

    private static long AgeMilliseconds(TimeSpan age) =>
        age <= TimeSpan.Zero ? 0 : (long)Math.Ceiling(age.TotalMilliseconds);

    private static IReadOnlyList<T> Slice<T>(IReadOnlyList<T> values, int offset, int limit)
    {
        if (offset > values.Count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (offset == values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(limit, values.Count - offset);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int? NextOffset(int offset, int count, int total)
    {
        int next = checked(offset + count);
        return next < total ? next : null;
    }

    private static void Cancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private readonly record struct IndexLaneKey(
        long SessionGeneration,
        long CatalogGeneration,
        string CatalogType);

    private readonly record struct PageLaneKey(
        long SessionGeneration,
        long CatalogGeneration,
        string CatalogType,
        int PageId,
        int OfferId,
        long ExpectedVersion);

    private readonly record struct WalkLaneKey(
        string CatalogType,
        bool OnlyVisible,
        int DelayMilliseconds,
        long MaxAgeTicks,
        int TimeoutMilliseconds);

    private sealed class SharedLane<TValue>(
        CatalogManagerScope scope,
        string catalog_type,
        Func<CancellationToken, Task<TValue>> work)
    {
        public CatalogManagerScope Scope = scope;
        public string CatalogType { get; } = catalog_type;
        public Func<CancellationToken, Task<TValue>> Work = work;
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource<TValue> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<IProgress<(int Loaded, int Total)>> Progress { get; } = [];
        public Exception? Poison;
        public int Waiters;
        public int AcceptIndexRefresh;
        public bool Accepting = true;
    }

    private sealed record IndexFetch(
        CatalogIndex Value,
        CatalogManagerScope Scope,
        long StateRevision,
        DateTimeOffset ReceivedAtUtc,
        bool FromCache);

    private sealed record PageFetch(
        CatalogPage Value,
        CatalogManagerScope Scope,
        long StateRevision,
        DateTimeOffset ReceivedAtUtc,
        bool FromCache);

    private sealed record LoadFetch(
        CatalogLoadReport Report,
        CatalogManagerScope Scope,
        long StateRevision,
        DateTimeOffset CompletedAtUtc);

    private enum LeaseKind
    {
        Pages,
        Offers
    }

    private sealed record SnapshotLease(
        long Revision,
        LeaseKind Kind,
        CatalogManagerState State,
        string CatalogType,
        string Text,
        IReadOnlyList<CatalogPageSummaryView>? Pages,
        IReadOnlyList<CatalogOfferSearchMatchView>? Offers);

    private sealed class Invocation(CatalogApplication owner) : IDisposable
    {
        private CatalogApplication? current = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref current, null)?.LeaveInvocation();
        }
    }
}

public sealed class CatalogInvalidatedException(
    long expected_session_generation,
    long expected_catalog_generation,
    long current_session_generation,
    long current_catalog_generation) : InvalidOperationException(
        "The catalog generation changed during the operation.")
{
    public long ExpectedSessionGeneration { get; } = expected_session_generation;
    public long ExpectedCatalogGeneration { get; } = expected_catalog_generation;
    public long CurrentSessionGeneration { get; } = current_session_generation;
    public long CurrentCatalogGeneration { get; } = current_catalog_generation;
}
