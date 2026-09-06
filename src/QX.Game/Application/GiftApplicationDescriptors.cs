using Qx.Messages;
using Qx.Game.Protocol;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class GiftApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.GiftsState,
        "Gift state",
        "Reads one atomic bounded summary of gift state without retaining nested gift graphs.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(GiftStateRequest),
        typeof(GiftStateView),
        state_effects: [GiftRead()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor WrappingList { get; } = new(
        ApplicationMemberIds.GiftsWrappingList,
        "Gift wrapping page",
        "Reads one bounded collection from an immutable gift snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(GiftWrappingPageRequest),
        typeof(GiftWrappingPage),
        PageParameters(typeof(GiftWrappingCollection), GiftWrappingCollection.StuffTypes),
        state_effects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.WrappingConfiguration,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ClubInfoList { get; } = new(
        ApplicationMemberIds.GiftsClubInfoList,
        "Club gift page",
        "Reads one flat bounded collection from an immutable club-gift snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(GiftClubInfoPageRequest),
        typeof(GiftClubInfoPage),
        PageParameters(typeof(GiftClubInfoCollection), GiftClubInfoCollection.Offers),
        state_effects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.ClubInfo,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ClubSelectedList { get; } = new(
        ApplicationMemberIds.GiftsClubSelectedList,
        "Selected club gift page",
        "Reads one bounded collection from the last immutable selected club-gift snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(GiftClubSelectedPageRequest),
        typeof(GiftClubSelectedPage),
        PageParameters(
            typeof(GiftClubSelectedCollection),
            GiftClubSelectedCollection.Products),
        state_effects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.ClubSelected,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor NewUserOfferList { get; } = new(
        ApplicationMemberIds.GiftsNewUserOfferList,
        "New-user gift offer page",
        "Reads one flat bounded collection from the last immutable new-user gift offer.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(GiftNewUserOfferPageRequest),
        typeof(GiftNewUserOfferPage),
        PageParameters(typeof(GiftNewUserOfferCollection), GiftNewUserOfferCollection.Steps),
        state_effects: [GiftRead()],
        messages:
        [
            new(
                MessageKeys.Gifts.NewUserOffer,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Refresh { get; } = new(
        ApplicationMemberIds.GiftsRefresh,
        "Refresh gift configuration",
        "Requests wrapping and club-gift state on one pinned session and returns the first fresh committed response from each route without claiming a request identifier.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftRefreshRequest),
        typeof(GiftRefreshResult),
        [LimitParameter(), TimeoutParameter(), ExpectedSessionGenerationParameter()],
        [ApplicationStateKey.HotelConnected],
        [GiftChange()],
        [
            new(
                MessageKeys.Gifts.WrappingConfigurationRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.WrappingConfiguration,
                Direction.In,
                ApplicationMessageRole.Observe),
            new(
                MessageKeys.Gifts.ClubInfoRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.ClubInfo,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        RefreshHints());

    public static ApplicationDescriptor PresentOpen { get; } = new(
        ApplicationMemberIds.GiftsPresentOpen,
        "Open present",
        "Dispatches one room-local present-open request without claiming an acknowledgement.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftPresentOpenRequest),
        typeof(GiftPresentOpenDispatchReceipt),
        [
            new(
                "furni_id",
                typeof(Id),
                true,
                null,
                "Positive room-furniture identifier.",
                new(Pattern: "^[1-9][0-9]*$")),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [GiftRead(), new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [
            new(
                MessageKeys.Gifts.PresentOpen,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.PresentOpened,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApplicationDescriptor Purchase { get; } = new(
        ApplicationMemberIds.GiftsPurchase,
        "Purchase gift",
        "Dispatches one gift purchase on a pinned hotel session and catalog generation without claiming a purchase outcome.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftPurchaseRequest),
        typeof(GiftPurchaseDispatchReceipt),
        PurchaseParameters(),
        [ApplicationStateKey.HotelConnected],
        [
            GiftRead(),
            new(ApplicationStateKey.CatalogCache, ApplicationStateEffectKind.Reads)
        ],
        [
            new(
                MessageKeys.Gifts.Purchase,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.ReceiverNotFound,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApplicationDescriptor ClubSelect { get; } = new(
        ApplicationMemberIds.GiftsClubSelect,
        "Select club gift",
        "Dispatches one club-gift selection without claiming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftClubSelectRequest),
        typeof(GiftClubSelectDispatchReceipt),
        [
            RequiredString("product_code", "Non-empty club-gift product code."),
            ExpectedSessionGenerationParameter(),
            ExpectedRevisionParameter(
                "expected_club_info_revision",
                "Optional club-info revision pinned through dispatch.")
        ],
        [ApplicationStateKey.HotelConnected],
        [GiftRead()],
        [
            new(
                MessageKeys.Gifts.ClubSelect,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.ClubSelected,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApplicationDescriptor OfferGiftabilityRefresh { get; } = new(
        ApplicationMemberIds.GiftsOfferGiftabilityRefresh,
        "Refresh offer giftability",
        "Requests one offer and returns the exact matching fresh Flash response.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftOfferGiftabilityRefreshRequest),
        typeof(GiftOfferGiftabilityRefreshResult),
        [
            Required("offer_id", typeof(int), "Offer identifier matched exactly."),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected],
        [GiftChange()],
        [
            new(
                MessageKeys.Gifts.OfferGiftabilityRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.OfferGiftability,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        RefreshHints());

    public static ApplicationDescriptor NewUserSelect { get; } = new(
        ApplicationMemberIds.GiftsNewUserSelect,
        "Select new-user gifts",
        "Dispatches one bounded new-user gift selection without claiming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftNewUserSelectRequest),
        typeof(GiftNewUserSelectDispatchReceipt),
        [
            new(
                "selections",
                typeof(IReadOnlyList<NuxGiftSelection>),
                true,
                null,
                "Ordered new-user gift selections.",
                new(MinItems: 0, MaxItems: 21845)),
            ExpectedSessionGenerationParameter(),
            ExpectedRevisionParameter(
                "expected_new_user_offer_revision",
                "Optional new-user offer revision pinned through dispatch.")
        ],
        [ApplicationStateKey.HotelConnected],
        [GiftRead()],
        [
            new(
                MessageKeys.Gifts.NewUserSelect,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.NewUserIncomplete,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApplicationDescriptor NewUserAdvance { get; } = new(
        ApplicationMemberIds.GiftsNewUserAdvance,
        "Advance new-user gift flow",
        "Dispatches one room-local new-user flow advance without claiming an acknowledgement.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(GiftNewUserAdvanceRequest),
        typeof(GiftNewUserAdvanceDispatchReceipt),
        [ExpectedSessionGenerationParameter(), ExpectedRoomGenerationParameter()],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [GiftRead(), new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [
            new(
                MessageKeys.Gifts.NewUserAdvance,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Gifts.NewUserIncomplete,
                Direction.In,
                ApplicationMessageRole.Observe,
                false)
        ],
        DispatchHints());

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.GiftsChanged,
        "Gift state changed",
        "Publishes ordered bounded gift summaries and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(GiftChanged),
        state_effects: [GiftChange()],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters(
        Type collection_type,
        object default_collection) =>
    [
        new(
            "collection",
            collection_type,
            false,
            default_collection,
            "Collection selected from the immutable gift snapshot."),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected collection.",
            new(Minimum: 0)),
        LimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> PurchaseParameters() =>
    [
        Required("page_id", typeof(int), "Catalog page identifier sent unchanged."),
        Required("offer_id", typeof(int), "Catalog offer identifier sent unchanged."),
        WireString("extra_data", false, "Catalog extra data sent unchanged."),
        WireString("receiver_name", false, "Gift receiver name sent unchanged."),
        WireString("gift_message", false, "Gift message sent unchanged."),
        Required("sprite_id", typeof(int), "Gift sprite identifier sent in the first wrapping slot."),
        Required("box_type", typeof(int), "Gift box type sent in the second wrapping slot."),
        Required("ribbon_type", typeof(int), "Gift ribbon type sent in the third wrapping slot."),
        Required(
            "show_purchaser_name",
            typeof(bool),
            "Wire flag controlling whether the purchaser name is shown."),
        new(
            "quantity",
            typeof(int),
            false,
            1,
            "Positive requested quantity; Unity sends it and Flash uses one.",
            new(Minimum: 1)),
        ExpectedSessionGenerationParameter(),
        ExpectedRevisionParameter(
            "expected_catalog_generation",
            "Optional catalog generation pinned through dispatch.")
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        new(
            MessageKeys.Gifts.WrappingConfiguration,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Gifts.PresentOpened,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Gifts.ClubInfo,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Gifts.ClubSelected,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Gifts.ReceiverNotFound,
            Direction.In,
            ApplicationMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.ClubNotification,
            Direction.In,
            ApplicationMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.OfferGiftability,
            Direction.In,
            ApplicationMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.NewUserOffer,
            Direction.In,
            ApplicationMessageRole.Observe,
            false),
        new(
            MessageKeys.Gifts.NewUserIncomplete,
            Direction.In,
            ApplicationMessageRole.Observe)
    ];

    private static ApplicationParameterDescriptor Required(
        string name,
        Type type,
        string description) => new(name, type, true, null, description);

    private static ApplicationParameterDescriptor RequiredString(
        string name,
        string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor WireString(
        string name,
        bool non_empty,
        string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(
            MinLength: non_empty ? 1 : null,
            MaxUtf8Bytes: ushort.MaxValue,
            Pattern: non_empty ? @".*\S.*" : null));

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one flat collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait for the operation.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor ExpectedSessionGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_session_generation",
            "Optional active hotel-session generation required through dispatch.");

    private static ApplicationParameterDescriptor ExpectedRoomGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_room_generation",
            "Optional ready-room generation required through dispatch.");

    private static ApplicationParameterDescriptor ExpectedRevisionParameter(
        string name,
        string description) => new(
        name,
        typeof(long?),
        false,
        null,
        description,
        new(Minimum: 1));

    private static ApplicationStateEffect GiftRead() =>
        new(ApplicationStateKey.Gifts, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect GiftChange() =>
        new(ApplicationStateKey.Gifts, ApplicationStateEffectKind.Changes);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
    private static ApplicationToolHints RefreshHints() => new(false, false, true, true);
    private static ApplicationToolHints DispatchHints() => new(false, true, false, true);
}
