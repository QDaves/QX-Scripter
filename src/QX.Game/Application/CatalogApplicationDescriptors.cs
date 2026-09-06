using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class CatalogApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.CatalogState,
        "Catalog state",
        "Reads the bounded cache state for one catalog type.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CatalogStateRequest),
        typeof(CatalogStateView),
        [CatalogTypeParameter()],
        state_effects: [CacheEffect(ApplicationStateEffectKind.Reads)],
        messages: [PublishedMessage(false)],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor IndexGet { get; } = new(
        ApplicationMemberIds.CatalogIndexGet,
        "Get catalog index",
        "Loads the active catalog index and returns a bounded flat projection.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CatalogIndexGetRequest),
        typeof(CatalogIndexView),
        [CatalogTypeParameter(), MaxAgeParameter(), TimeoutParameter(), .. GenerationParameters()],
        [ApplicationStateKey.HotelConnected],
        [
            CacheEffect(ApplicationStateEffectKind.Changes),
            CacheEffect(ApplicationStateEffectKind.Invalidates)
        ],
        messages:
        [
            new(MessageKeys.Catalog.IndexRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Catalog.IndexSnapshot, Direction.In, ApplicationMessageRole.Observe),
            PublishedMessage(false)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor PageGet { get; } = new(
        ApplicationMemberIds.CatalogPageGet,
        "Get catalog page",
        "Loads one catalog page and returns a compact bounded offer projection.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CatalogPageGetRequest),
        typeof(CatalogPageView),
        [
            new("page_id", typeof(int), true, null, "Non-negative catalog page identifier.", new(Minimum: 0)),
            new("offer_id", typeof(int), false, -1, "Offer to preselect, or -1.", new(Minimum: -1)),
            CatalogTypeParameter(),
            MaxAgeParameter(),
            TimeoutParameter(),
            .. GenerationParameters()
        ],
        [ApplicationStateKey.HotelConnected],
        [CacheEffect(ApplicationStateEffectKind.Changes)],
        messages:
        [
            new(MessageKeys.Catalog.PageRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Catalog.PageSnapshot, Direction.In, ApplicationMessageRole.Observe),
            PublishedMessage(false)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor PagesLoad { get; } = new(
        ApplicationMemberIds.CatalogPagesLoad,
        "Load catalog pages",
        "Walks the current index and caches every eligible offer page without mixing catalog generations.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CatalogLoadRequest),
        typeof(CatalogLoadView),
        [
            CatalogTypeParameter(),
            new("only_visible", typeof(bool), false, true, "Skips hidden page subtrees."),
            new("delay_milliseconds", typeof(int), false, 0, "Pause between page requests.", new(Minimum: 0)),
            MaxAgeParameter(),
            TimeoutParameter(15000, "Maximum time for the index and for each individual page response."),
            .. GenerationParameters()
        ],
        [ApplicationStateKey.HotelConnected],
        [
            CacheEffect(ApplicationStateEffectKind.Changes),
            CacheEffect(ApplicationStateEffectKind.Invalidates)
        ],
        messages:
        [
            new(MessageKeys.Catalog.IndexRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Catalog.IndexSnapshot, Direction.In, ApplicationMessageRole.Observe),
            new(MessageKeys.Catalog.PageRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Catalog.PageSnapshot, Direction.In, ApplicationMessageRole.Observe),
            PublishedMessage(false)
        ],
        tool_hints: new(true, false, true, true));

    public static ApplicationDescriptor PagesList { get; } = new(
        ApplicationMemberIds.CatalogPagesList,
        "Cached catalog pages",
        "Reads one stable bounded page of compact cached-page summaries.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CatalogPagesRequest),
        typeof(CatalogPageListView),
        [CatalogTypeParameter(), .. PagingParameters(), .. GenerationParameters()],
        state_effects: [CacheEffect(ApplicationStateEffectKind.Reads)],
        messages: [PublishedMessage(false)],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor OffersSearch { get; } = new(
        ApplicationMemberIds.CatalogOffersSearch,
        "Search cached catalog offers",
        "Searches compact cached offers with a stable bounded snapshot lease; empty text lists all cached offers.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CatalogOfferSearchRequest),
        typeof(CatalogOfferSearchPage),
        [
            new("text", typeof(string), false, string.Empty, "Case-insensitive cached-offer text filter.", new(MaxUtf8Bytes: 1024)),
            CatalogTypeParameter(),
            .. PagingParameters(),
            .. GenerationParameters()
        ],
        state_effects: [CacheEffect(ApplicationStateEffectKind.Reads)],
        messages: [PublishedMessage(false)],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor CacheClear { get; } = new(
        ApplicationMemberIds.CatalogCacheClear,
        "Clear catalog cache",
        "Atomically invalidates one catalog type or the complete catalog cache.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CatalogCacheClearRequest),
        typeof(CatalogCacheClearView),
        [NullableCatalogTypeParameter(), .. GenerationParameters()],
        state_effects: [CacheEffect(ApplicationStateEffectKind.Invalidates)],
        messages: [PublishedMessage(false)],
        tool_hints: new(false, true, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor PurchaseState { get; } = new(
        ApplicationMemberIds.CatalogPurchaseState,
        "Catalog purchase state",
        "Reads the latest passive catalog purchase outcome for the active hotel session.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CatalogPurchaseStateRequest),
        typeof(CatalogPurchaseStateView),
        state_effects:
        [
            new(ApplicationStateKey.CatalogPurchase, ApplicationStateEffectKind.Reads)
        ],
        messages: PurchaseOutcomeMessages(false),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor PurchaseSend { get; } = new(
        ApplicationMemberIds.CatalogPurchaseSend,
        "Send catalog purchase",
        "Dispatches one catalog purchase without claiming that a later global outcome belongs to it.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CatalogPurchaseSendRequest),
        typeof(CatalogPurchaseDispatchReceipt),
        [
            new("page_id", typeof(int), true, null, "Non-negative catalog page identifier.", new(Minimum: 0)),
            new("offer_id", typeof(int), true, null, "Non-negative catalog offer identifier.", new(Minimum: 0)),
            new("extra_data", typeof(string), false, string.Empty, "Offer selection data.", new(MaxUtf8Bytes: ushort.MaxValue)),
            new("quantity", typeof(int), false, 1, "Positive purchase quantity.", new(Minimum: 1)),
            .. GenerationParameters()
        ],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new(MessageKeys.Catalog.Purchase, Direction.Out, ApplicationMessageRole.Send)
        ],
        tool_hints: new(false, true, false, true));

    public static ApplicationDescriptor PurchaseOutcome { get; } = new(
        ApplicationMemberIds.CatalogPurchaseOutcome,
        "Catalog purchase outcome",
        "Publishes each passive hotel purchase outcome without correlating it to a dispatch.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(CatalogPurchaseOutcomeEvent),
        state_effects:
        [
            new(ApplicationStateKey.CatalogPurchase, ApplicationStateEffectKind.Changes)
        ],
        messages: PurchaseOutcomeMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Published { get; } = new(
        ApplicationMemberIds.CatalogPublished,
        "Catalog published",
        "Publishes the committed invalidation epoch before consumers can observe stale cache state.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(CatalogPublishedEvent),
        state_effects: [CacheEffect(ApplicationStateEffectKind.Invalidates)],
        messages: [PublishedMessage()]);

    private static ApplicationParameterDescriptor CatalogTypeParameter() => new(
        "catalog_type",
        typeof(string),
        false,
        "NORMAL",
        "Catalog mode; NORMAL and BUILDERS_CLUB are accepted case-insensitively.");

    private static ApplicationParameterDescriptor NullableCatalogTypeParameter() => new(
        "catalog_type",
        typeof(string),
        false,
        null,
        "Catalog mode, or null for every mode; names are case-insensitive.");

    private static ApplicationParameterDescriptor MaxAgeParameter() => new(
        "max_age_milliseconds",
        typeof(long),
        false,
        300000L,
        "Cache age limit; zero forces a refresh and -1 accepts any age.",
        new(Minimum: -1, Maximum: TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond));

    private static ApplicationParameterDescriptor TimeoutParameter(
        int value = 10000,
        string description = "Maximum time to wait for the response.") => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        value,
        description,
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor[] PagingParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, 100, "Maximum rows returned.", new(Minimum: 1, Maximum: 500)),
        new("snapshot_revision", typeof(long?), false, null, "Opaque continuation lease from the first page.", new(Minimum: 1))
    ];

    private static ApplicationParameterDescriptor[] GenerationParameters() =>
    [
        new("expected_session_generation", typeof(long?), false, null, "Optional session compare-and-swap guard.", new(Minimum: 0)),
        new("expected_catalog_generation", typeof(long?), false, null, "Optional catalog compare-and-swap guard.", new(Minimum: 0))
    ];

    private static ApplicationMessageRequirement PublishedMessage(bool required = true) => new(
        MessageKeys.Catalog.Published,
        Direction.In,
        ApplicationMessageRole.Observe,
        required);

    private static IReadOnlyList<ApplicationMessageRequirement> PurchaseOutcomeMessages(
        bool required = true) =>
    [
        new(MessageKeys.Catalog.PurchaseAccepted, Direction.In, ApplicationMessageRole.Observe, required),
        new(MessageKeys.Catalog.PurchaseFailed, Direction.In, ApplicationMessageRole.Observe, required),
        new(MessageKeys.Catalog.PurchaseForbidden, Direction.In, ApplicationMessageRole.Observe, required)
    ];

    private static ApplicationStateEffect CacheEffect(ApplicationStateEffectKind kind) =>
        new(ApplicationStateKey.CatalogCache, kind);
}
