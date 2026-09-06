using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class MarketplaceApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.MarketplaceState,
        "Marketplace state",
        "Reads one bounded page of the active session's immutable marketplace state.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(MarketplaceStateRequest),
        typeof(MarketplaceStateView),
        PagingParameters(),
        state_effects:
        [
            new(ApplicationStateKey.MarketplaceConfigurationLoaded, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.MarketplaceEligibilityLoaded, ApplicationStateEffectKind.Reads)
        ],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ConfigurationRefresh { get; } = RequestResponse<
        MarketplaceRefreshRequest,
        MarketplaceConfiguration>(
        ApplicationMemberIds.MarketplaceConfigurationRefresh,
        "Refresh marketplace configuration",
        "Loads marketplace availability, pricing, fee and tax limits for the active hotel.",
        MessageKeys.Marketplace.Configuration.Request,
        MessageKeys.Marketplace.Configuration.Snapshot,
        [TimeoutParameter()],
        new(true, false, true, true),
        [new(ApplicationStateKey.MarketplaceConfigurationLoaded, ApplicationStateEffectKind.Changes)]);

    public static ApplicationDescriptor EligibilityRefresh { get; } = RequestResponse<
        MarketplaceRefreshRequest,
        MarketplaceCanMakeOfferResult>(
        ApplicationMemberIds.MarketplaceEligibilityRefresh,
        "Refresh marketplace eligibility",
        "Loads whether the active account may create an offer and why it may be refused.",
        MessageKeys.Marketplace.Eligibility.Request,
        MessageKeys.Marketplace.Eligibility.Result,
        [TimeoutParameter()],
        new(true, false, true, true),
        [new(ApplicationStateKey.MarketplaceEligibilityLoaded, ApplicationStateEffectKind.Changes)]);

    public static ApplicationDescriptor ItemStatsGet { get; } = RequestResponse<
        MarketplaceItemStatsRequest,
        MarketplaceItemStatsSnapshot>(
        ApplicationMemberIds.MarketplaceItemStatsGet,
        "Get marketplace item statistics",
        "Loads current offers and bounded historical sale statistics for one furniture type.",
        MessageKeys.Marketplace.ItemStats.Request,
        MessageKeys.Marketplace.ItemStats.Snapshot,
        [
            new("furni_category", typeof(MarketplaceFurniCategory), true, null, "Furniture market category."),
            new("furni_type_id", typeof(int), true, null, "Furniture type or limited-edition lookup identifier.", new(Minimum: 1)),
            TextParameter("extra_data", "Optional variant data used by modern marketplace layouts."),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Search { get; } = RequestResponse<
        MarketplaceSearchRequest,
        MarketplaceOfferPage>(
        ApplicationMemberIds.MarketplaceSearch,
        "Search marketplace",
        "Searches public marketplace offers and returns one bounded page of the hotel result.",
        MessageKeys.Marketplace.Offers.SearchRequest,
        MessageKeys.Marketplace.Offers.SearchResult,
        [
            TextParameter("search_query", "Furniture name or marketplace search text."),
            new("minimum_price", typeof(int), false, -1, "Minimum price, or -1 for no lower bound.", new(Minimum: -1)),
            new("maximum_price", typeof(int), false, -1, "Maximum price, or -1 for no upper bound.", new(Minimum: -1)),
            new("sort_order", typeof(MarketplaceSortOrder), false, MarketplaceSortOrder.HighestPrice, "Hotel marketplace sort order."),
            new("combine_unique_offers", typeof(bool), false, true, "Groups equivalent offers when the active Flash layout supports it."),
            .. PagingParameters(),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor OwnOffersGet { get; } = RequestResponse<
        MarketplaceOwnOffersRequest,
        MarketplaceOwnOfferPage>(
        ApplicationMemberIds.MarketplaceOwnOffersGet,
        "Get own marketplace offers",
        "Loads one bounded page of the active account's open, sold or expired offers.",
        MessageKeys.Marketplace.Offers.OwnRequest,
        MessageKeys.Marketplace.Offers.OwnSnapshot,
        [
            new("category", typeof(MarketplaceOwnOffersCategory), false, MarketplaceOwnOffersCategory.Open, "Own-offer category; legacy Flash and Unity expose only open offers."),
            .. PagingParameters(),
            TimeoutParameter()
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor OfferMake { get; } = RequestResponse<
        MarketplaceMakeOfferRequest,
        MarketplaceMakeOfferResult>(
        ApplicationMemberIds.MarketplaceOfferMake,
        "Create marketplace offer",
        "Lists one or more inventory items for sale using the active client's verified layout.",
        MessageKeys.Marketplace.Offers.Make,
        MessageKeys.Marketplace.Offers.MakeResult,
        [
            new("price", typeof(int), true, null, "Listing price per offer.", new(Minimum: 1)),
            new("furni_category", typeof(MarketplaceSellCategory), true, null, "Floor or wall furniture category."),
            new("item_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct inventory item identifiers.", new(MinItems: 1, MaxItems: 1000)),
            TimeoutParameter()
        ],
        new(false, true, false, true));

    public static ApplicationDescriptor OfferBuy { get; } = RequestResponse<
        MarketplaceBuyRequest,
        MarketplaceBuyResult>(
        ApplicationMemberIds.MarketplaceOfferBuy,
        "Buy marketplace offer",
        "Purchases a cached offer and waits for the hotel result using the verified ID or furniture-details layout.",
        MessageKeys.Marketplace.Offers.Buy,
        MessageKeys.Marketplace.Offers.BuyResult,
        [OfferIdParameter(), TextParameter("extra_data", "Optional furniture variant data for Unity details purchases."), TimeoutParameter()],
        new(false, true, false, true));

    public static ApplicationDescriptor OfferBuySend { get; } = Send<MarketplaceBuySendRequest>(
        ApplicationMemberIds.MarketplaceOfferBuySend,
        "Send marketplace purchase",
        "Sends a purchase for a cached offer without waiting for the hotel result.",
        MessageKeys.Marketplace.Offers.Buy,
        [OfferIdParameter(), TextParameter("extra_data", "Optional furniture variant data for Unity details purchases.")],
        new(false, true, false, true));

    public static ApplicationDescriptor OfferCancel { get; } = RequestResponse<
        MarketplaceCancelRequest,
        MarketplaceCancelOfferResult>(
        ApplicationMemberIds.MarketplaceOfferCancel,
        "Cancel marketplace offer",
        "Cancels one own offer and waits for the verified Flash result.",
        MessageKeys.Marketplace.Offers.Cancel,
        MessageKeys.Marketplace.Offers.CancelResult,
        [OfferIdParameter(), TimeoutParameter()],
        new(false, true, false, true));

    public static ApplicationDescriptor OfferCancelSend { get; } = Send<MarketplaceCancelSendRequest>(
        ApplicationMemberIds.MarketplaceOfferCancelSend,
        "Send marketplace cancellation",
        "Sends one offer cancellation without waiting for an unverified client result.",
        MessageKeys.Marketplace.Offers.Cancel,
        [OfferIdParameter()],
        new(false, true, false, true));

    public static ApplicationDescriptor OffersCancelAll { get; } = RequestResponse<
        MarketplaceCancelAllRequest,
        MarketplaceCancelAllOffersSnapshot>(
        ApplicationMemberIds.MarketplaceOffersCancelAll,
        "Cancel all marketplace offers",
        "Cancels every open offer and returns the exact identifiers confirmed by the hotel.",
        MessageKeys.Marketplace.Offers.CancelAll,
        MessageKeys.Marketplace.Offers.CancelAllResult,
        [TimeoutParameter()],
        new(false, true, false, true));

    public static ApplicationDescriptor HistoryClear { get; } = RequestResponse<
        MarketplaceHistoryClearRequest,
        MarketplaceClearOwnHistoryResult>(
        ApplicationMemberIds.MarketplaceHistoryClear,
        "Clear marketplace history",
        "Clears the sold or expired own-offer history on supported Flash layouts.",
        MessageKeys.Marketplace.Offers.ClearOwnHistory,
        MessageKeys.Marketplace.Offers.ClearOwnHistoryResult,
        [
            new("category", typeof(MarketplaceHistoryCategory), true, null, "Sold or expired own-offer category."),
            TimeoutParameter()
        ],
        new(false, true, false, true));

    public static ApplicationDescriptor CreditsRedeem { get; } = Send<MarketplaceCommandRequest>(
        ApplicationMemberIds.MarketplaceCreditsRedeem,
        "Redeem marketplace credits",
        "Collects credits waiting on sold marketplace offers.",
        MessageKeys.Marketplace.Credits.Redeem,
        [],
        new(false, true, false, true));

    public static ApplicationDescriptor TokensBuy { get; } = Send<MarketplaceCommandRequest>(
        ApplicationMemberIds.MarketplaceTokensBuy,
        "Buy marketplace tokens",
        "Buys the configured marketplace listing-token batch for the active account.",
        MessageKeys.Marketplace.Tokens.Buy,
        [],
        new(false, true, false, true));

    public static ApplicationDescriptor Changed { get; } = Event<MarketplaceChanged>(
        ApplicationMemberIds.MarketplaceChanged,
        "Marketplace state changed",
        "Publishes ordered immutable marketplace state revisions, including reset.",
        StateMessages(),
        [
            new(ApplicationStateKey.MarketplaceConfigurationLoaded, ApplicationStateEffectKind.Changes),
            new(ApplicationStateKey.MarketplaceEligibilityLoaded, ApplicationStateEffectKind.Changes)
        ]);

    public static ApplicationDescriptor ConfigurationChanged { get; } = Event<MarketplaceConfigurationChanged>(
        ApplicationMemberIds.MarketplaceConfigurationChanged,
        "Marketplace configuration changed",
        "Publishes marketplace configuration received for the active session.",
        [new(MessageKeys.Marketplace.Configuration.Snapshot, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor EligibilityChanged { get; } = Event<MarketplaceEligibilityChanged>(
        ApplicationMemberIds.MarketplaceEligibilityChanged,
        "Marketplace eligibility changed",
        "Publishes the active account's latest marketplace eligibility result.",
        [new(MessageKeys.Marketplace.Eligibility.Result, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor SearchReceived { get; } = Event<MarketplaceSearchReceived>(
        ApplicationMemberIds.MarketplaceSearchReceived,
        "Marketplace search received",
        "Publishes a bounded first page of each public marketplace search result.",
        [new(MessageKeys.Marketplace.Offers.SearchResult, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor OwnOffersReceived { get; } = Event<MarketplaceOwnOffersReceived>(
        ApplicationMemberIds.MarketplaceOwnOffersReceived,
        "Own marketplace offers received",
        "Publishes a bounded first page of each own-offer snapshot.",
        [new(MessageKeys.Marketplace.Offers.OwnSnapshot, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor ItemStatsReceived { get; } = Event<MarketplaceItemStatsReceived>(
        ApplicationMemberIds.MarketplaceItemStatsReceived,
        "Marketplace item statistics received",
        "Publishes immutable sale statistics received for one furniture type.",
        [new(MessageKeys.Marketplace.ItemStats.Snapshot, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor MakeResultReceived { get; } = Event<MarketplaceMakeOfferResultReceived>(
        ApplicationMemberIds.MarketplaceOfferMakeResult,
        "Marketplace offer result",
        "Publishes each hotel result for a marketplace listing request.",
        [new(MessageKeys.Marketplace.Offers.MakeResult, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor BuyResultReceived { get; } = Event<MarketplaceBuyResultReceived>(
        ApplicationMemberIds.MarketplaceOfferBuyResult,
        "Marketplace purchase result",
        "Publishes each hotel result for a marketplace purchase.",
        [new(MessageKeys.Marketplace.Offers.BuyResult, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor CancelResultReceived { get; } = Event<MarketplaceCancelResultReceived>(
        ApplicationMemberIds.MarketplaceOfferCancelResult,
        "Marketplace cancellation result",
        "Publishes verified Flash cancellation results.",
        [new(MessageKeys.Marketplace.Offers.CancelResult, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor CancelAllResultReceived { get; } = Event<MarketplaceCancelAllResultReceived>(
        ApplicationMemberIds.MarketplaceOffersCancelAllResult,
        "Marketplace cancel-all result",
        "Publishes immutable cancel-all results and confirmed offer identifiers.",
        [new(MessageKeys.Marketplace.Offers.CancelAllResult, Direction.In, ApplicationMessageRole.Observe)]);

    public static ApplicationDescriptor HistoryClearResultReceived { get; } = Event<MarketplaceHistoryClearResultReceived>(
        ApplicationMemberIds.MarketplaceHistoryClearResult,
        "Marketplace history-clear result",
        "Publishes verified Flash history-clear results.",
        [new(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult, Direction.In, ApplicationMessageRole.Observe)]);

    private static ApplicationDescriptor RequestResponse<TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey response_key,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        ApplicationToolHints hints,
        IReadOnlyList<ApplicationStateEffect>? state_effects = null) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TRequest),
        typeof(TResult),
        parameters,
        [ApplicationStateKey.HotelConnected],
        state_effects,
        [
            new(request_key, Direction.Out, ApplicationMessageRole.Send),
            new(response_key, Direction.In, ApplicationMessageRole.Observe)
        ],
        hints);

    private static ApplicationDescriptor Send<TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        ApplicationToolHints hints) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TRequest),
        typeof(MarketplaceDispatchResult),
        parameters,
        [ApplicationStateKey.HotelConnected],
        messages: [new(request_key, Direction.Out, ApplicationMessageRole.Send)],
        tool_hints: hints);

    private static ApplicationDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationMessageRequirement> messages,
        IReadOnlyList<ApplicationStateEffect>? state_effects = null) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(TEvent),
        state_effects: state_effects,
        messages: messages);

    private static ApplicationParameterDescriptor[] PagingParameters() =>
    [
        new("page", typeof(int), false, 0, "Zero-based result page.", new(Minimum: 0)),
        new("page_size", typeof(int), false, 100, "Maximum records returned in this page.", new(Minimum: 1, Maximum: 250))
    ];

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor TextParameter(
        string name,
        string description) => new(
        name,
        typeof(string),
        false,
        string.Empty,
        description,
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApplicationParameterDescriptor OfferIdParameter() => new(
        "offer_id",
        typeof(Id),
        true,
        null,
        "Positive marketplace offer identifier.",
        new(Pattern: @"^[1-9][0-9]*$"));

    private static ApplicationMessageRequirement[] StateMessages() =>
    [
        new(MessageKeys.Marketplace.Configuration.Snapshot, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Eligibility.Result, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.SearchResult, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.OwnSnapshot, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.ItemStats.Snapshot, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.MakeResult, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.BuyResult, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.CancelResult, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.CancelAllResult, Direction.In, ApplicationMessageRole.Observe, false),
        new(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult, Direction.In, ApplicationMessageRole.Observe, false)
    ];
}
