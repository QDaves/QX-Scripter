using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using System.Text;

namespace Qx.Game.Application;

internal sealed class MarketplaceApplication : IApplicationFeature
{
    private const int event_page_size = 100;
    private const int maximum_page_size = 250;
    private readonly IInterceptor interceptor;
    private readonly GameState game;
    private readonly MarketplaceManager marketplace;
    private readonly ApplicationMessageDispatcher messages;
    private readonly TimeProvider time_provider;
    private readonly ApplicationEventSource<MarketplaceChanged> changed;
    private readonly ApplicationEventSource<MarketplaceConfigurationChanged> configuration_changed;
    private readonly ApplicationEventSource<MarketplaceEligibilityChanged> eligibility_changed;
    private readonly ApplicationEventSource<MarketplaceSearchReceived> search_received;
    private readonly ApplicationEventSource<MarketplaceOwnOffersReceived> own_offers_received;
    private readonly ApplicationEventSource<MarketplaceItemStatsReceived> item_stats_received;
    private readonly ApplicationEventSource<MarketplaceMakeOfferResultReceived> make_result_received;
    private readonly ApplicationEventSource<MarketplaceBuyResultReceived> buy_result_received;
    private readonly ApplicationEventSource<MarketplaceCancelResultReceived> cancel_result_received;
    private readonly ApplicationEventSource<MarketplaceCancelAllResultReceived> cancel_all_result_received;
    private readonly ApplicationEventSource<MarketplaceHistoryClearResultReceived> history_clear_result_received;
    private int disposed;

    public MarketplaceApplication(
        IInterceptor interceptor,
        GameState game,
        ApplicationMessageDispatcher messages,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.interceptor = interceptor;
        this.game = game;
        marketplace = game.Marketplace;
        this.messages = messages;
        this.time_provider = time_provider;
        changed = new ApplicationEventSource<MarketplaceChanged>(observer_error);
        configuration_changed = new ApplicationEventSource<MarketplaceConfigurationChanged>(observer_error);
        eligibility_changed = new ApplicationEventSource<MarketplaceEligibilityChanged>(observer_error);
        search_received = new ApplicationEventSource<MarketplaceSearchReceived>(observer_error);
        own_offers_received = new ApplicationEventSource<MarketplaceOwnOffersReceived>(observer_error);
        item_stats_received = new ApplicationEventSource<MarketplaceItemStatsReceived>(observer_error);
        make_result_received = new ApplicationEventSource<MarketplaceMakeOfferResultReceived>(observer_error);
        buy_result_received = new ApplicationEventSource<MarketplaceBuyResultReceived>(observer_error);
        cancel_result_received = new ApplicationEventSource<MarketplaceCancelResultReceived>(observer_error);
        cancel_all_result_received = new ApplicationEventSource<MarketplaceCancelAllResultReceived>(observer_error);
        history_clear_result_received = new ApplicationEventSource<MarketplaceHistoryClearResultReceived>(observer_error);

        try
        {
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<MarketplaceStateRequest, MarketplaceStateView>(
                    MarketplaceApplicationDescriptors.State,
                    (request, _) => ValueTask.FromResult(ReadState(request))),
                new ApplicationCallBinding<MarketplaceRefreshRequest, MarketplaceConfiguration>(
                    MarketplaceApplicationDescriptors.ConfigurationRefresh,
                    RefreshConfiguration),
                new ApplicationCallBinding<MarketplaceRefreshRequest, MarketplaceCanMakeOfferResult>(
                    MarketplaceApplicationDescriptors.EligibilityRefresh,
                    RefreshEligibility),
                new ApplicationCallBinding<MarketplaceItemStatsRequest, MarketplaceItemStatsSnapshot>(
                    MarketplaceApplicationDescriptors.ItemStatsGet,
                    GetItemStats),
                new ApplicationCallBinding<MarketplaceSearchRequest, MarketplaceOfferPage>(
                    MarketplaceApplicationDescriptors.Search,
                    Search),
                new ApplicationCallBinding<MarketplaceOwnOffersRequest, MarketplaceOwnOfferPage>(
                    MarketplaceApplicationDescriptors.OwnOffersGet,
                    GetOwnOffers),
                new ApplicationCallBinding<MarketplaceMakeOfferRequest, MarketplaceMakeOfferResult>(
                    MarketplaceApplicationDescriptors.OfferMake,
                    MakeOffer),
                new ApplicationCallBinding<MarketplaceBuyRequest, MarketplaceBuyResult>(
                    MarketplaceApplicationDescriptors.OfferBuy,
                    BuyOffer),
                new ApplicationCallBinding<MarketplaceBuySendRequest, MarketplaceDispatchResult>(
                    MarketplaceApplicationDescriptors.OfferBuySend,
                    SendBuyOffer),
                new ApplicationCallBinding<MarketplaceCancelRequest, MarketplaceCancelOfferResult>(
                    MarketplaceApplicationDescriptors.OfferCancel,
                    CancelOffer),
                new ApplicationCallBinding<MarketplaceCancelSendRequest, MarketplaceDispatchResult>(
                    MarketplaceApplicationDescriptors.OfferCancelSend,
                    SendCancelOffer),
                new ApplicationCallBinding<MarketplaceCancelAllRequest, MarketplaceCancelAllOffersSnapshot>(
                    MarketplaceApplicationDescriptors.OffersCancelAll,
                    CancelAllOffers),
                new ApplicationCallBinding<MarketplaceHistoryClearRequest, MarketplaceClearOwnHistoryResult>(
                    MarketplaceApplicationDescriptors.HistoryClear,
                    ClearHistory),
                new ApplicationCallBinding<MarketplaceCommandRequest, MarketplaceDispatchResult>(
                    MarketplaceApplicationDescriptors.CreditsRedeem,
                    RedeemCredits),
                new ApplicationCallBinding<MarketplaceCommandRequest, MarketplaceDispatchResult>(
                    MarketplaceApplicationDescriptors.TokensBuy,
                    BuyTokens),
                new ApplicationEventBinding<MarketplaceChanged>(
                    MarketplaceApplicationDescriptors.Changed,
                    changed.Subscribe),
                new ApplicationEventBinding<MarketplaceConfigurationChanged>(
                    MarketplaceApplicationDescriptors.ConfigurationChanged,
                    configuration_changed.Subscribe),
                new ApplicationEventBinding<MarketplaceEligibilityChanged>(
                    MarketplaceApplicationDescriptors.EligibilityChanged,
                    eligibility_changed.Subscribe),
                new ApplicationEventBinding<MarketplaceSearchReceived>(
                    MarketplaceApplicationDescriptors.SearchReceived,
                    search_received.Subscribe),
                new ApplicationEventBinding<MarketplaceOwnOffersReceived>(
                    MarketplaceApplicationDescriptors.OwnOffersReceived,
                    own_offers_received.Subscribe),
                new ApplicationEventBinding<MarketplaceItemStatsReceived>(
                    MarketplaceApplicationDescriptors.ItemStatsReceived,
                    item_stats_received.Subscribe),
                new ApplicationEventBinding<MarketplaceMakeOfferResultReceived>(
                    MarketplaceApplicationDescriptors.MakeResultReceived,
                    make_result_received.Subscribe),
                new ApplicationEventBinding<MarketplaceBuyResultReceived>(
                    MarketplaceApplicationDescriptors.BuyResultReceived,
                    buy_result_received.Subscribe),
                new ApplicationEventBinding<MarketplaceCancelResultReceived>(
                    MarketplaceApplicationDescriptors.CancelResultReceived,
                    cancel_result_received.Subscribe),
                new ApplicationEventBinding<MarketplaceCancelAllResultReceived>(
                    MarketplaceApplicationDescriptors.CancelAllResultReceived,
                    cancel_all_result_received.Subscribe),
                new ApplicationEventBinding<MarketplaceHistoryClearResultReceived>(
                    MarketplaceApplicationDescriptors.HistoryClearResultReceived,
                    history_clear_result_received.Subscribe)
            ]);
            marketplace.StateChanged += OnStateChanged;
        }
        catch
        {
            DisposeEvents();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    public MarketplaceStateView ReadState(MarketplaceStateRequest request)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaging(request.Page, request.PageSize);
        return StateView(marketplace.Snapshot, request.Page, request.PageSize);
    }

    public ValueTask<MarketplaceConfiguration> RefreshConfiguration(
        MarketplaceRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        Session session = RequireSession(cancellation_token);
        return Request<
            GetMarketplaceConfiguration,
            MarketplaceConfiguration,
            MarketplaceConfiguration>(
            MessageContracts.Marketplace.Configuration.Request,
            new GetMarketplaceConfiguration(),
            MessageContracts.Marketplace.Configuration.Snapshot,
            MarketplaceStateChangeKind.Configuration,
            null,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            2,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceCanMakeOfferResult> RefreshEligibility(
        MarketplaceRefreshRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ValidateRefresh(request);
        Session session = RequireSession(cancellation_token);
        return Request<
            GetMarketplaceCanMakeOffer,
            MarketplaceCanMakeOfferResult,
            MarketplaceCanMakeOfferResult>(
            MessageContracts.Marketplace.Eligibility.Request,
            new GetMarketplaceCanMakeOffer(),
            MessageContracts.Marketplace.Eligibility.Result,
            MarketplaceStateChangeKind.Eligibility,
            null,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            2,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceItemStatsSnapshot> GetItemStats(
        MarketplaceItemStatsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateCategory(request.FurniCategory, true);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.FurniTypeId, 1);
        ValidateText(request.ExtraData, nameof(request.ExtraData));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        if (session.Client is ClientType.Flash &&
            FlashLayout() is FlashMarketplaceWireLayout.Legacy &&
            request.ExtraData.Length != 0)
        {
            throw new NotSupportedException(
                "Legacy Flash marketplace statistics cannot carry extra data.");
        }
        var outgoing = new GetMarketplaceItemStats(
            request.FurniCategory,
            request.FurniTypeId,
            request.ExtraData);
        return Request<
            GetMarketplaceItemStats,
            MarketplaceItemStats,
            MarketplaceItemStatsSnapshot>(
            MessageContracts.Marketplace.ItemStats.Request,
            outgoing,
            MessageContracts.Marketplace.ItemStats.Snapshot,
            MarketplaceStateChangeKind.ItemStats,
            response =>
                response.FurniCategoryId == (int)request.FurniCategory &&
                response.FurniTypeId == request.FurniTypeId,
            MarketplaceManager.SnapshotOf,
            MarketplaceManager.Equivalent,
            request.TimeoutMilliseconds,
            2,
            session,
            cancellation_token);
    }

    public async ValueTask<MarketplaceOfferPage> Search(
        MarketplaceSearchRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.SearchQuery, nameof(request.SearchQuery));
        ValidatePriceRange(request.MinimumPrice, request.MaximumPrice);
        ValidateSortOrder(request.SortOrder);
        ValidatePaging(request.Page, request.PageSize);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        bool? combine_unique = SearchGrouping(session, request.CombineUniqueOffers);
        var outgoing = new SearchMarketplaceOffers(
            request.MinimumPrice,
            request.MaximumPrice,
            request.SearchQuery,
            request.SortOrder,
            combine_unique);
        MarketplaceCommitted<MarketplaceOffersSnapshot> result = await RequestCommitted<
            SearchMarketplaceOffers,
            MarketplaceOffers,
            MarketplaceOffersSnapshot>(
            MessageContracts.Marketplace.Offers.SearchRequest,
            outgoing,
            MessageContracts.Marketplace.Offers.SearchResult,
            MarketplaceStateChangeKind.Search,
            null,
            MarketplaceManager.SnapshotOf,
            MarketplaceManager.Equivalent,
            request.TimeoutMilliseconds,
            2,
            session,
            cancellation_token).ConfigureAwait(false);
        return OfferPage(
            result.Value,
            result.State.Generation,
            result.State.Revision,
            request.Page,
            request.PageSize);
    }

    public async ValueTask<MarketplaceOwnOfferPage> GetOwnOffers(
        MarketplaceOwnOffersRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOwnOffersCategory(request.Category);
        ValidatePaging(request.Page, request.PageSize);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        var outgoing = new GetMarketplaceOwnOffers(
            OwnOffersCategory(session, request.Category));
        MarketplaceCommitted<MarketplaceOwnOffersSnapshot> result = await RequestCommitted<
            GetMarketplaceOwnOffers,
            MarketplaceOwnOffers,
            MarketplaceOwnOffersSnapshot>(
            MessageContracts.Marketplace.Offers.OwnRequest,
            outgoing,
            MessageContracts.Marketplace.Offers.OwnSnapshot,
            MarketplaceStateChangeKind.OwnOffers,
            null,
            MarketplaceManager.SnapshotOf,
            MarketplaceManager.Equivalent,
            request.TimeoutMilliseconds,
            2,
            session,
            cancellation_token).ConfigureAwait(false);
        return OwnOfferPage(
            result.Value,
            result.State.Generation,
            result.State.Revision,
            request.Page,
            request.PageSize,
            request.Category);
    }

    public ValueTask<MarketplaceMakeOfferResult> MakeOffer(
        MarketplaceMakeOfferRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Price);
        MarketplaceFurniCategory furni_category =
            SellCategory(request.FurniCategory);
        ArgumentNullException.ThrowIfNull(request.ItemIds);
        Id[] item_ids = request.ItemIds.Distinct().ToArray();
        if (item_ids.Length == 0 || item_ids.Length > 1000)
            throw new ArgumentOutOfRangeException(nameof(request.ItemIds));
        if (item_ids.Any(item_id => (long)item_id <= 0))
            throw new ArgumentOutOfRangeException(nameof(request.ItemIds));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        if (session.Client is ClientType.Flash &&
            FlashLayout() is FlashMarketplaceWireLayout.Legacy &&
            item_ids.Length != 1)
        {
            throw new NotSupportedException(
                "Legacy Flash marketplace offers require exactly one inventory item.");
        }
        var outgoing = new MakeMarketplaceOffer(
            request.Price,
            furni_category,
            Array.AsReadOnly(item_ids));
        return Request<
            MakeMarketplaceOffer,
            MarketplaceMakeOfferResult,
            MarketplaceMakeOfferResult>(
            MessageContracts.Marketplace.Offers.Make,
            outgoing,
            MessageContracts.Marketplace.Offers.MakeResult,
            MarketplaceStateChangeKind.MakeResult,
            null,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            1,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceBuyResult> BuyOffer(
        MarketplaceBuyRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOfferId(request.OfferId);
        ValidateText(request.ExtraData, nameof(request.ExtraData));
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        MarketplaceBuyOfferRequest outgoing = BuyRequest(
            session,
            request.OfferId,
            request.ExtraData);
        Func<MarketplaceBuyResult, bool>? response_match =
            outgoing is BuyMarketplaceOffer
                ? response => response.RequestedOfferId == request.OfferId
                : null;
        return Request<
            MarketplaceBuyOfferRequest,
            MarketplaceBuyResult,
            MarketplaceBuyResult>(
            MessageContracts.Marketplace.Offers.Buy,
            outgoing,
            MessageContracts.Marketplace.Offers.BuyResult,
            MarketplaceStateChangeKind.BuyResult,
            response_match,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            1,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceDispatchResult> SendBuyOffer(
        MarketplaceBuySendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOfferId(request.OfferId);
        ValidateText(request.ExtraData, nameof(request.ExtraData));
        Session session = RequireSession(cancellation_token);
        MarketplaceBuyOfferRequest outgoing = BuyRequest(
            session,
            request.OfferId,
            request.ExtraData);
        messages.Dispatch(
            MessageContracts.Marketplace.Offers.Buy,
            outgoing,
            session,
            cancellation_token);
        return DispatchResult(session, request.OfferId);
    }

    public ValueTask<MarketplaceCancelOfferResult> CancelOffer(
        MarketplaceCancelRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOfferId(request.OfferId);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        if (session.Client is not ClientType.Flash)
        {
            throw new NotSupportedException(
                "The Unity marketplace cancel-offer result has no verified payload layout.");
        }
        return Request<
            CancelMarketplaceOffer,
            MarketplaceCancelOfferResult,
            MarketplaceCancelOfferResult>(
            MessageContracts.Marketplace.Offers.Cancel,
            new CancelMarketplaceOffer(request.OfferId),
            MessageContracts.Marketplace.Offers.CancelResult,
            MarketplaceStateChangeKind.CancelResult,
            response => response.OfferId == request.OfferId,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            1,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceDispatchResult> SendCancelOffer(
        MarketplaceCancelSendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateOfferId(request.OfferId);
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Marketplace.Offers.Cancel,
            new CancelMarketplaceOffer(request.OfferId),
            session,
            cancellation_token);
        return DispatchResult(session, request.OfferId);
    }

    public ValueTask<MarketplaceCancelAllOffersSnapshot> CancelAllOffers(
        MarketplaceCancelAllRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        return Request<
            CancelAllMarketplaceOffers,
            MarketplaceCancelAllOffersResult,
            MarketplaceCancelAllOffersSnapshot>(
            MessageContracts.Marketplace.Offers.CancelAll,
            new CancelAllMarketplaceOffers(),
            MessageContracts.Marketplace.Offers.CancelAllResult,
            MarketplaceStateChangeKind.CancelAllResult,
            null,
            MarketplaceManager.SnapshotOf,
            MarketplaceManager.Equivalent,
            request.TimeoutMilliseconds,
            1,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceClearOwnHistoryResult> ClearHistory(
        MarketplaceHistoryClearRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        MarketplaceOwnOffersCategory category =
            HistoryCategory(request.Category);
        ValidateTimeout(request.TimeoutMilliseconds);
        Session session = RequireSession(cancellation_token);
        if (session.Client is not ClientType.Flash ||
            FlashLayout() is not FlashMarketplaceWireLayout.Modern)
        {
            throw new NotSupportedException(
                "Marketplace history clearing requires the modern Flash marketplace layout.");
        }
        return Request<
            ClearMarketplaceOwnHistory,
            MarketplaceClearOwnHistoryResult,
            MarketplaceClearOwnHistoryResult>(
            MessageContracts.Marketplace.Offers.ClearOwnHistory,
            new ClearMarketplaceOwnHistory(category),
            MessageContracts.Marketplace.Offers.ClearOwnHistoryResult,
            MarketplaceStateChangeKind.ClearHistoryResult,
            null,
            static value => value,
            null,
            request.TimeoutMilliseconds,
            1,
            session,
            cancellation_token);
    }

    public ValueTask<MarketplaceDispatchResult> RedeemCredits(
        MarketplaceCommandRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Marketplace.Credits.Redeem,
            new RedeemMarketplaceOfferCredits(),
            session,
            cancellation_token);
        return DispatchResult(session);
    }

    public ValueTask<MarketplaceDispatchResult> BuyTokens(
        MarketplaceCommandRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        Session session = RequireSession(cancellation_token);
        messages.Dispatch(
            MessageContracts.Marketplace.Tokens.Buy,
            new BuyMarketplaceTokens(),
            session,
            cancellation_token);
        return DispatchResult(session);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        marketplace.StateChanged -= OnStateChanged;
        DisposeEvents();
    }

    private async ValueTask<TState> Request<TRequest, TResponse, TState>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<TResponse> response_contract,
        MarketplaceStateChangeKind kind,
        Func<TResponse, bool>? response_match,
        Func<TResponse, TState> snapshot,
        Func<TState, TState, bool>? equivalent,
        int timeout_milliseconds,
        int max_attempts,
        Session session,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
        where TState : class
    {
        MarketplaceCommitted<TState> committed = await RequestCommitted(
            request_contract,
            request,
            response_contract,
            kind,
            response_match,
            snapshot,
            equivalent,
            timeout_milliseconds,
            max_attempts,
            session,
            cancellation_token).ConfigureAwait(false);
        return committed.Value;
    }

    private async ValueTask<MarketplaceCommitted<TState>> RequestCommitted<TRequest, TResponse, TState>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<TResponse> response_contract,
        MarketplaceStateChangeKind kind,
        Func<TResponse, bool>? response_match,
        Func<TResponse, TState> snapshot,
        Func<TState, TState, bool>? equivalent,
        int timeout_milliseconds,
        int max_attempts,
        Session session,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
        where TState : class
    {
        long started = time_provider.GetTimestamp();
        using var commit = new MarketplaceStateCommit(
            marketplace,
            interceptor,
            session,
            kind);
        TResponse response = await game.Requests.RequestAsync(
            request_contract,
            request,
            response_contract,
            session,
            match: value =>
                ReferenceEquals(interceptor.Session, session) &&
                (response_match?.Invoke(value) ?? true),
            timeout_ms: timeout_milliseconds,
            block: false,
            cancellation_token: cancellation_token,
            max_attempts: max_attempts).ConfigureAwait(false);
        TState expected = snapshot(response);
        TimeSpan remaining = TimeSpan.FromMilliseconds(timeout_milliseconds) -
            time_provider.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
            remaining = TimeSpan.FromMilliseconds(1);
        MarketplaceStateUpdate update = await commit.WaitAsync(
            candidate =>
                candidate.Value is TState value &&
                (equivalent?.Invoke(value, expected) ??
                    EqualityComparer<TState>.Default.Equals(value, expected)),
            remaining,
            cancellation_token).ConfigureAwait(false);
        RequireCurrentState(session);
        return update.Value is TState result
            ? new MarketplaceCommitted<TState>(result, update.State)
            : throw new InvalidOperationException(
                "The committed marketplace response changed type.");
    }

    private MarketplaceBuyOfferRequest BuyRequest(
        Session session,
        Id offer_id,
        string extra_data)
    {
        if (session.Client is ClientType.Flash)
            return new BuyMarketplaceOffer(offer_id);
        MarketplaceBuyWireLayout layout = interceptor.Messages
            .GetWireProfile(ClientType.Unity)
            .RequireUnityMarketplaceBuyLayout();
        if (layout is MarketplaceBuyWireLayout.OfferId)
            return new BuyMarketplaceOffer(offer_id);
        MarketplaceOfferSnapshot offer = marketplace.Snapshot.FindOffer(offer_id) ??
            throw new InvalidOperationException(
                "The Unity furniture-details purchase layout requires the offer in marketplace state.");
        string wire_extra_data = extra_data.Length == 0 && offer.IsWall
            ? offer.WallData
            : extra_data;
        return new BuyMarketplaceOfferByDetails(
            offer.FurniCategory,
            offer.FurniTypeId,
            offer.Price,
            wire_extra_data);
    }

    private bool? SearchGrouping(Session session, bool combine_unique_offers)
    {
        if (session.Client is ClientType.Unity)
        {
            if (!combine_unique_offers)
            {
                throw new NotSupportedException(
                    "Unity marketplace searches do not expose unique-offer grouping.");
            }
            return null;
        }
        FlashMarketplaceWireLayout layout = FlashLayout();
        if (layout is FlashMarketplaceWireLayout.Legacy)
        {
            if (!combine_unique_offers)
            {
                throw new NotSupportedException(
                    "Legacy Flash marketplace searches do not expose unique-offer grouping.");
            }
            return null;
        }
        return combine_unique_offers;
    }

    private MarketplaceOwnOffersCategory? OwnOffersCategory(
        Session session,
        MarketplaceOwnOffersCategory category)
    {
        if (session.Client is ClientType.Unity)
        {
            if (category is not MarketplaceOwnOffersCategory.Open)
            {
                throw new NotSupportedException(
                    "Unity marketplace own offers expose only open offers.");
            }
            return null;
        }
        FlashMarketplaceWireLayout layout = FlashLayout();
        if (layout is FlashMarketplaceWireLayout.Legacy)
        {
            if (category is not MarketplaceOwnOffersCategory.Open)
            {
                throw new NotSupportedException(
                    "Legacy Flash marketplace own offers expose only open offers.");
            }
            return null;
        }
        return category;
    }

    private FlashMarketplaceWireLayout FlashLayout() => interceptor.Messages
        .GetWireProfile(ClientType.Flash)
        .RequireFlashMarketplaceLayout();

    private Session RequireSession(CancellationToken cancellation_token)
    {
        cancellation_token.ThrowIfCancellationRequested();
        return interceptor.Session ??
            throw new InvalidOperationException("No hotel session is connected.");
    }

    private MarketplaceSnapshot RequireCurrentState(Session session)
    {
        ThrowIfDisposed();
        if (!ReferenceEquals(interceptor.Session, session))
        {
            throw new InvalidOperationException(
                "The hotel session changed during the marketplace operation.");
        }
        return marketplace.Snapshot;
    }

    private ValueTask<MarketplaceDispatchResult> DispatchResult(
        Session session,
        Id? offer_id = null,
        MarketplaceOwnOffersCategory? category = null) =>
        ValueTask.FromResult(new MarketplaceDispatchResult(
            session.Client,
            time_provider.GetUtcNow(),
            offer_id,
            category));

    private void OnStateChanged(MarketplaceStateUpdate update)
    {
        DateTimeOffset received_at = time_provider.GetUtcNow();
        changed.Publish(new MarketplaceChanged(
            ChangeKind(update.Kind),
            received_at,
            StateSummary(update.State)));
        switch (update.Kind)
        {
            case MarketplaceStateChangeKind.Configuration
                when update.Value is MarketplaceConfiguration configuration:
                configuration_changed.Publish(new MarketplaceConfigurationChanged(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    configuration));
                break;
            case MarketplaceStateChangeKind.Eligibility
                when update.Value is MarketplaceCanMakeOfferResult eligibility:
                eligibility_changed.Publish(new MarketplaceEligibilityChanged(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    eligibility));
                break;
            case MarketplaceStateChangeKind.Search
                when update.Value is MarketplaceOffersSnapshot search:
                search_received.Publish(new MarketplaceSearchReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    OfferPage(
                        search,
                        update.State.Generation,
                        update.State.Revision,
                        0,
                        event_page_size)));
                break;
            case MarketplaceStateChangeKind.OwnOffers
                when update.Value is MarketplaceOwnOffersSnapshot own_offers:
                own_offers_received.Publish(new MarketplaceOwnOffersReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    OwnOfferPage(
                        own_offers,
                        update.State.Generation,
                        update.State.Revision,
                        0,
                        event_page_size,
                        null)));
                break;
            case MarketplaceStateChangeKind.ItemStats
                when update.Value is MarketplaceItemStatsSnapshot item_stats:
                item_stats_received.Publish(new MarketplaceItemStatsReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    item_stats));
                break;
            case MarketplaceStateChangeKind.MakeResult
                when update.Value is MarketplaceMakeOfferResult make_result:
                make_result_received.Publish(new MarketplaceMakeOfferResultReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    make_result));
                break;
            case MarketplaceStateChangeKind.BuyResult
                when update.Value is MarketplaceBuyResult buy_result:
                buy_result_received.Publish(new MarketplaceBuyResultReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    buy_result));
                break;
            case MarketplaceStateChangeKind.CancelResult
                when update.Value is MarketplaceCancelOfferResult cancel_result:
                cancel_result_received.Publish(new MarketplaceCancelResultReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    cancel_result));
                break;
            case MarketplaceStateChangeKind.CancelAllResult
                when update.Value is MarketplaceCancelAllOffersSnapshot cancel_all_result:
                cancel_all_result_received.Publish(new MarketplaceCancelAllResultReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    cancel_all_result));
                break;
            case MarketplaceStateChangeKind.ClearHistoryResult
                when update.Value is MarketplaceClearOwnHistoryResult clear_result:
                history_clear_result_received.Publish(new MarketplaceHistoryClearResultReceived(
                    update.State.Generation,
                    update.State.Revision,
                    received_at,
                    clear_result));
                break;
        }
    }

    private static MarketplaceStateView StateView(
        MarketplaceSnapshot state,
        int page,
        int page_size)
    {
        MarketplaceItemStatsSnapshot[] stats = state.ItemStats.Values
            .OrderBy(value => value.FurniCategory)
            .ThenBy(value => value.FurniTypeId)
            .ToArray();
        return new MarketplaceStateView(
            state.Generation,
            state.Revision,
            state.Configuration,
            state.Eligibility,
            state.SearchResult is null
                ? null
                : OfferPage(
                    state.SearchResult,
                    state.Generation,
                    state.Revision,
                    page,
                    page_size),
            state.OwnOffers is null
                ? null
                : OwnOfferPage(
                    state.OwnOffers,
                    state.Generation,
                    state.Revision,
                    page,
                    page_size,
                    null),
            new MarketplaceItemStatsPage(
                page,
                page_size,
                stats.Length,
                Slice(stats, page, page_size)),
            state.LastMakeOfferResult,
            state.LastBuyResult,
            state.LastCancelOfferResult,
            state.LastCancelAllOffersResult,
            state.LastClearHistoryResult);
    }

    private static MarketplaceStateSummary StateSummary(MarketplaceSnapshot state) => new(
        state.Generation,
        state.Revision,
        state.ConfigurationLoaded,
        state.EligibilityLoaded,
        state.SearchResult?.Offers.Count ?? 0,
        state.SearchResult?.TotalItemsFound ?? 0,
        state.OwnOffers?.Offers.Count ?? 0,
        state.ItemStats.Count);

    private static MarketplaceOfferPage OfferPage(
        MarketplaceOffersSnapshot result,
        long generation,
        long revision,
        int page,
        int page_size) => new(
        generation,
        revision,
        page,
        page_size,
        result.Offers.Count,
        result.TotalItemsFound,
        Slice(result.Offers, page, page_size));

    private static MarketplaceOwnOfferPage OwnOfferPage(
        MarketplaceOwnOffersSnapshot result,
        long generation,
        long revision,
        int page,
        int page_size,
        MarketplaceOwnOffersCategory? category) => new(
        generation,
        revision,
        page,
        page_size,
        result.Offers.Count,
        result.CreditsWaiting,
        category,
        Slice(result.Offers, page, page_size));

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int page,
        int page_size)
    {
        long offset = (long)page * page_size;
        if (offset >= values.Count)
            return Array.AsReadOnly(Array.Empty<T>());
        int count = Math.Min(page_size, values.Count - (int)offset);
        var result = new T[count];
        for (int index = 0; index < count; index++)
            result[index] = values[(int)offset + index];
        return Array.AsReadOnly(result);
    }

    private static MarketplaceChangeKind ChangeKind(
        MarketplaceStateChangeKind kind) => kind switch
    {
        MarketplaceStateChangeKind.Configuration => MarketplaceChangeKind.Configuration,
        MarketplaceStateChangeKind.Eligibility => MarketplaceChangeKind.Eligibility,
        MarketplaceStateChangeKind.Search => MarketplaceChangeKind.Search,
        MarketplaceStateChangeKind.OwnOffers => MarketplaceChangeKind.OwnOffers,
        MarketplaceStateChangeKind.ItemStats => MarketplaceChangeKind.ItemStats,
        MarketplaceStateChangeKind.MakeResult => MarketplaceChangeKind.MakeResult,
        MarketplaceStateChangeKind.BuyResult => MarketplaceChangeKind.BuyResult,
        MarketplaceStateChangeKind.CancelResult => MarketplaceChangeKind.CancelResult,
        MarketplaceStateChangeKind.CancelAllResult => MarketplaceChangeKind.CancelAllResult,
        MarketplaceStateChangeKind.ClearHistoryResult => MarketplaceChangeKind.ClearHistoryResult,
        MarketplaceStateChangeKind.Reset => MarketplaceChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static void ValidateRefresh(MarketplaceRefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        if (timeout_milliseconds is < 1 or > 120000)
            throw new ArgumentOutOfRangeException(nameof(timeout_milliseconds));
    }

    private static void ValidatePaging(int page, int page_size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        if (page_size is < 1 or > maximum_page_size)
            throw new ArgumentOutOfRangeException(nameof(page_size));
    }

    private static void ValidateText(string value, string argument_name)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidatePriceRange(int minimum_price, int maximum_price)
    {
        if (minimum_price < -1)
            throw new ArgumentOutOfRangeException(nameof(minimum_price));
        if (maximum_price < -1)
            throw new ArgumentOutOfRangeException(nameof(maximum_price));
        if (minimum_price >= 0 &&
            maximum_price >= 0 &&
            minimum_price > maximum_price)
        {
            throw new ArgumentException(
                "The minimum marketplace price cannot exceed the maximum price.");
        }
    }

    private static void ValidateSortOrder(MarketplaceSortOrder sort_order)
    {
        if (sort_order is < MarketplaceSortOrder.HighestPrice or
            > MarketplaceSortOrder.LeastOffers)
        {
            throw new ArgumentOutOfRangeException(nameof(sort_order));
        }
    }

    private static void ValidateCategory(
        MarketplaceFurniCategory category,
        bool allow_limited)
    {
        bool supported = category is
            MarketplaceFurniCategory.Floor or
            MarketplaceFurniCategory.Wall;
        if (allow_limited)
            supported |= category is MarketplaceFurniCategory.Limited;
        if (!supported)
            throw new ArgumentOutOfRangeException(nameof(category));
    }

    private static void ValidateOwnOffersCategory(
        MarketplaceOwnOffersCategory category)
    {
        if (category is < MarketplaceOwnOffersCategory.Open or
            > MarketplaceOwnOffersCategory.Expired)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }
    }

    private static MarketplaceFurniCategory SellCategory(
        MarketplaceSellCategory category) => category switch
    {
        MarketplaceSellCategory.Floor => MarketplaceFurniCategory.Floor,
        MarketplaceSellCategory.Wall => MarketplaceFurniCategory.Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private static MarketplaceOwnOffersCategory HistoryCategory(
        MarketplaceHistoryCategory category) => category switch
    {
        MarketplaceHistoryCategory.Sold => MarketplaceOwnOffersCategory.Sold,
        MarketplaceHistoryCategory.Expired => MarketplaceOwnOffersCategory.Expired,
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private static void ValidateOfferId(Id offer_id)
    {
        if ((long)offer_id <= 0)
            throw new ArgumentOutOfRangeException(nameof(offer_id));
    }

    private void DisposeEvents()
    {
        changed.Dispose();
        configuration_changed.Dispose();
        eligibility_changed.Dispose();
        search_received.Dispose();
        own_offers_received.Dispose();
        item_stats_received.Dispose();
        make_result_received.Dispose();
        buy_result_received.Dispose();
        cancel_result_received.Dispose();
        cancel_all_result_received.Dispose();
        history_clear_result_received.Dispose();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private readonly record struct MarketplaceCommitted<TState>(
        TState Value,
        MarketplaceSnapshot State)
        where TState : class;

    private sealed class MarketplaceStateCommit : IDisposable
    {
        private readonly object sync = new();
        private readonly MarketplaceManager marketplace;
        private readonly IConnection connection;
        private readonly Session session;
        private readonly MarketplaceStateChangeKind kind;
        private readonly List<MarketplaceStateUpdate> candidates = [];
        private readonly TaskCompletionSource<MarketplaceStateUpdate> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<MarketplaceStateUpdate, bool>? match;
        private long baseline_generation;
        private long baseline_revision;
        private bool initialized;
        private bool disposed;

        public MarketplaceStateCommit(
            MarketplaceManager marketplace,
            IConnection connection,
            Session session,
            MarketplaceStateChangeKind kind)
        {
            this.marketplace = marketplace;
            this.connection = connection;
            this.session = session;
            this.kind = kind;
            marketplace.StateChanged += OnStateChanged;
            MarketplaceSnapshot state = marketplace.Snapshot;
            lock (sync)
            {
                baseline_generation = state.Generation;
                baseline_revision = state.Revision;
                initialized = true;
                candidates.RemoveAll(candidate => !IsNewer(candidate.State));
            }
        }

        public Task<MarketplaceStateUpdate> WaitAsync(
            Func<MarketplaceStateUpdate, bool> predicate,
            TimeSpan timeout,
            CancellationToken cancellation_token)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                match = predicate;
                MarketplaceStateUpdate? candidate = candidates
                    .LastOrDefault(predicate);
                if (candidate is not null)
                    completion.TrySetResult(candidate);
                candidates.Clear();
            }
            return completion.Task.WaitAsync(
                timeout,
                cancellation_token);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;
                disposed = true;
                candidates.Clear();
            }
            marketplace.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(MarketplaceStateUpdate update)
        {
            if (update.Kind != kind ||
                !ReferenceEquals(connection.Session, session))
            {
                return;
            }
            lock (sync)
            {
                if (disposed || initialized && !IsNewer(update.State))
                    return;
                if (match is null)
                    candidates.Add(update);
                else if (match(update))
                    completion.TrySetResult(update);
            }
        }

        private bool IsNewer(MarketplaceSnapshot state) =>
            state.Generation > baseline_generation ||
            state.Generation == baseline_generation &&
            state.Revision > baseline_revision;
    }
}
