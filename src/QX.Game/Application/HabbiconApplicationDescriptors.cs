using Qx.Protocol;

namespace Qx.Game.Application;

internal static class HabbiconApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = Query(
        ApplicationMemberIds.HabbiconsState,
        "Habbicon state",
        "Reads the immutable habbicon vault state.",
        typeof(HabbiconStateRequest),
        typeof(HabbiconStateView),
        [SnapshotRevisionParameter(false)]);

    public static ApplicationDescriptor Collections { get; } = Query(
        ApplicationMemberIds.HabbiconCollectionsList,
        "Habbicon collections",
        "Reads one hotel-ordered collection page from an immutable snapshot.",
        typeof(HabbiconCollectionPageRequest),
        typeof(HabbiconCollectionPage),
        PageParameters());

    public static ApplicationDescriptor Entries { get; } = Query(
        ApplicationMemberIds.HabbiconEntriesList,
        "Habbicon entries",
        "Reads one hotel-ordered icon page from an immutable snapshot.",
        typeof(HabbiconEntryPageRequest),
        typeof(HabbiconEntryPage),
        PageParameters());

    public static ApplicationDescriptor ShopRefresh { get; } = new(
        ApplicationMemberIds.HabbiconShopRefresh,
        "Refresh habbicon shop",
        "Returns the first fresh shop snapshot after one uniquely correlated request.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(HabbiconShopRefreshRequest),
        typeof(HabbiconShopRefreshResult),
        RefreshParameters(),
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.Habbicons.ShopRequest), Observe(MessageKeys.Habbicons.ShopSnapshot)],
        new ApplicationToolHints(false, false, true, true));

    public static ApplicationDescriptor InfoRefresh { get; } = new(
        ApplicationMemberIds.HabbiconInfoRefresh,
        "Refresh habbicon info",
        "Returns fresh detail for the requested icon; the response echoes the icon identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(HabbiconInfoRefreshRequest),
        typeof(HabbiconInfoRefreshResult),
        [
            IdParameter("habbicon_id", "Habbicon identifier."),
            TimeoutParameter(),
            ExpectedSessionParameter()
        ],
        [ApplicationStateKey.HotelConnected],
        [ChangeEffect()],
        [Send(MessageKeys.Habbicons.InfoRequest), Observe(MessageKeys.Habbicons.InfoSnapshot)],
        new ApplicationToolHints(false, false, true, true));

    public static ApplicationDescriptor Buy { get; } = Action<HabbiconBuyActionRequest>(
        ApplicationMemberIds.HabbiconBuy,
        "Buy habbicon",
        "Buys one habbicon.",
        MessageKeys.Habbicons.Buy,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApplicationDescriptor BuyCollection { get; } =
        Action<HabbiconCollectionBuyActionRequest>(
            ApplicationMemberIds.HabbiconCollectionBuy,
            "Buy habbicon collection",
            "Buys one habbicon collection.",
            MessageKeys.Habbicons.BuyCollection,
            IdParameter("collection_id", "Collection identifier."));

    public static ApplicationDescriptor Claim { get; } = Action<HabbiconClaimActionRequest>(
        ApplicationMemberIds.HabbiconClaim,
        "Claim habbicon",
        "Claims one earned habbicon.",
        MessageKeys.Habbicons.Claim,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApplicationDescriptor Favorite { get; } = Action<HabbiconFavoriteActionRequest>(
        ApplicationMemberIds.HabbiconFavorite,
        "Favorite habbicon",
        "Marks one habbicon as favorite.",
        MessageKeys.Habbicons.Favorite,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApplicationDescriptor Unfavorite { get; } = Action<HabbiconUnfavoriteActionRequest>(
        ApplicationMemberIds.HabbiconUnfavorite,
        "Unfavorite habbicon",
        "Removes one habbicon from favorites.",
        MessageKeys.Habbicons.Unfavorite,
        IdParameter("habbicon_id", "Habbicon identifier."));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.HabbiconsChanged,
        "Habbicons changed",
        "Publishes bounded shop, inventory, status, info, room-use, setting, and reset transitions.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(HabbiconChanged),
        state_effects: [ChangeEffect()],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor Query(
        string id,
        string title,
        string description,
        Type request,
        Type result,
        IReadOnlyList<ApplicationParameterDescriptor> parameters) =>
        new(
            id,
            title,
            description,
            ApplicationMemberKind.Query,
            ApplicationExposure.All,
            request,
            result,
            parameters,
            state_effects: [ReadEffect()],
            messages: ObservedMessages(),
            tool_hints: new ApplicationToolHints(true, false, true, false),
            invocation_scope: ApplicationInvocationScope.Persistent);

    private static ApplicationDescriptor Action<T>(
        string id,
        string title,
        string description,
        MessageKey key,
        ApplicationParameterDescriptor parameter) =>
        new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(T),
            typeof(HabbiconDispatchResult),
            [parameter, ExpectedSessionParameter()],
            [ApplicationStateKey.HotelConnected],
            [ChangeEffect()],
            [Send(key)],
            new ApplicationToolHints(false, true, false, true));

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based snapshot offset.", new(Minimum: 0)),
        new("limit", typeof(int), false, 100, "Maximum values returned.", new(Minimum: 1, Maximum: 500)),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> RefreshParameters() =>
    [
        new("limit", typeof(int), false, 100, "Maximum first-page values returned.", new(Minimum: 1, Maximum: 500)),
        TimeoutParameter(),
        ExpectedSessionParameter()
    ];

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(bool continuation) =>
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            continuation
                ? "Snapshot revision returned by the first read and required for continuation pages."
                : "Optional retained snapshot revision; omitted to capture current state.",
            new(Minimum: 1));

    private static ApplicationParameterDescriptor TimeoutParameter() =>
        new(
            "timeout_milliseconds",
            typeof(int),
            false,
            10000,
            "Total response budget.",
            new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor ExpectedSessionParameter() =>
        new(
            "expected_session_generation",
            typeof(long?),
            false,
            null,
            "Optional active session generation required through dispatch.",
            new(Minimum: 1));

    private static ApplicationParameterDescriptor IdParameter(string name, string description) =>
        new(name, typeof(int), true, null, description);

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Habbicons.ShopSnapshot),
        Observe(MessageKeys.Habbicons.InventorySnapshot),
        Observe(MessageKeys.Habbicons.StatusUpdated),
        Observe(MessageKeys.Habbicons.InfoSnapshot),
        Observe(MessageKeys.Habbicons.RoomUsed)
    ];

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key) =>
        new(key, Direction.In, ApplicationMessageRole.Observe);

    private static ApplicationStateEffect ReadEffect() =>
        new(ApplicationStateKey.Habbicons, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect ChangeEffect() =>
        new(ApplicationStateKey.Habbicons, ApplicationStateEffectKind.Changes);
}
