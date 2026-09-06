using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class SubscriptionApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.SubscriptionsState,
        "Subscription state",
        "Reads a bounded immutable page of subscription products and the latest passive club-offer, kickback and Builders Club summaries.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(SubscriptionStateRequest),
        typeof(SubscriptionStateView),
        StateParameters(),
        state_effects:
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Reads)],
        messages: ObservedMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ClubOffersList { get; } = new(
        ApplicationMemberIds.SubscriptionsClubOffersList,
        "Club offers page",
        "Reads a bounded page from one immutable club-offer snapshot without assigning it to an offer type.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(SubscriptionClubOffersPageRequest),
        typeof(SubscriptionClubOffersPage),
        ClubOffersPageParameters(),
        state_effects:
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Reads)],
        messages:
        [
            new(
                MessageKeys.Subscriptions.ClubOffersSnapshot,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ClubOffersRefresh { get; } = new(
        ApplicationMemberIds.SubscriptionsClubOffersRefresh,
        "Refresh club offers",
        "Requests club offers and returns the first bounded page of the first fresh route snapshot observed after dispatch without claiming offer-type identity.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionClubOffersRefreshRequest),
        typeof(SubscriptionClubOffersPage),
        [
            new(
                "offer_type",
                typeof(int),
                false,
                1,
                "Offer-set selector sent to the hotel without being treated as response identity."),
            ClubOffersLimitParameter(),
            ClubOffersTimeoutParameter(),
            ExpectedGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Changes)],
        [
            new(
                MessageKeys.Subscriptions.ClubOffersRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Subscriptions.ClubOffersSnapshot,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor UserInfoRefresh { get; } = new(
        ApplicationMemberIds.SubscriptionsUserInfoRefresh,
        "Refresh subscription user info",
        "Requests one product and returns the matching fresh response observed after dispatch without claiming request identity.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionUserInfoRefreshRequest),
        typeof(SubscriptionUserInfoRefreshResult),
        [ProductNameParameter(true), TimeoutParameter(), ExpectedGenerationParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Changes)],
        [
            new(
                MessageKeys.Subscriptions.UserInfoRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Subscriptions.UserInfo,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor KickbackRefresh { get; } = new(
        ApplicationMemberIds.SubscriptionsKickbackRefresh,
        "Refresh subscription kickback",
        "Requests the kickback summary and returns the fresh route response observed after dispatch without claiming request identity.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionKickbackRefreshRequest),
        typeof(SubscriptionKickbackRefreshResult),
        [TimeoutParameter(), ExpectedGenerationParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Changes)],
        [
            new(
                MessageKeys.Subscriptions.KickbackInfoRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Subscriptions.KickbackInfo,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor BuildersClubFurniCountRefresh { get; } = new(
        ApplicationMemberIds.SubscriptionsBuildersClubFurniCountRefresh,
        "Refresh Builders Club furni count",
        "Requests the Builders Club furni count and returns the fresh route response observed after dispatch without claiming request identity.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionBuildersClubFurniCountRefreshRequest),
        typeof(SubscriptionBuildersClubFurniCountRefreshResult),
        [TimeoutParameter(), ExpectedGenerationParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Changes)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubFurniCountRequest,
                Direction.Out,
                ApplicationMessageRole.Send),
            new(
                MessageKeys.Subscriptions.BuildersClubFurniCount,
                Direction.In,
                ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor BuildersClubFloorOfferPlace { get; } = new(
        ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace,
        "Place Builders Club floor offer",
        "Dispatches one Builders Club floor-offer placement in the current room without claiming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionBuildersClubFloorPlaceRequest),
        typeof(SubscriptionBuildersClubPlacementDispatchReceipt),
        [
            RequiredInt("page_id", "Catalog page identifier sent unchanged."),
            RequiredInt("offer_id", "Builders Club offer identifier sent unchanged."),
            RequiredInt("x", "Target tile column sent unchanged."),
            RequiredInt("y", "Target tile row sent unchanged."),
            new("direction", typeof(int), false, 0, "Target direction sent unchanged."),
            ExtraDataParameter(),
            new("is_retry", typeof(bool), false, false, "Explicit hotel placement-retry flag."),
            ExpectedGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubFloorOfferPlace,
                Direction.Out,
                ApplicationMessageRole.Send)
        ],
        new(false, true, false, true));

    public static ApplicationDescriptor BuildersClubWallOfferPlace { get; } = new(
        ApplicationMemberIds.SubscriptionsBuildersClubWallOfferPlace,
        "Place Builders Club wall offer",
        "Dispatches one Builders Club wall-offer placement in the current room without claiming hotel acceptance.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(SubscriptionBuildersClubWallPlaceRequest),
        typeof(SubscriptionBuildersClubPlacementDispatchReceipt),
        [
            RequiredInt("page_id", "Catalog page identifier sent unchanged."),
            RequiredInt("offer_id", "Builders Club offer identifier sent unchanged."),
            new(
                "wall_location",
                typeof(string),
                true,
                null,
                "Non-empty wall-location string represented natively for the active client.",
                new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*")),
            ExtraDataParameter(),
            new("is_retry", typeof(bool), false, false, "Explicit hotel placement-retry flag."),
            ExpectedGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady],
        [new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads)],
        [
            new(
                MessageKeys.Subscriptions.BuildersClubWallOfferPlace,
                Direction.Out,
                ApplicationMessageRole.Send,
                SchemaCapability: "unityBuildersClubWallLocationSchema")
        ],
        new(false, true, false, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.SubscriptionsChanged,
        "Subscription state changed",
        "Publishes ordered bounded subscription, kickback, Builders Club and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(SubscriptionChanged),
        state_effects:
        [new(ApplicationStateKey.Subscriptions, ApplicationStateEffectKind.Changes)],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor> StateParameters() =>
    [
        ProductNameParameter(false),
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable product snapshot.",
            new(Minimum: 0)),
        new(
            "limit",
            typeof(int),
            false,
            100,
            "Maximum products returned by this page.",
            new(Minimum: 1, Maximum: 500)),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> ClubOffersPageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the immutable club-offer snapshot.",
            new(Minimum: 0)),
        ClubOffersLimitParameter(),
        new(
            "snapshot_revision",
            typeof(long?),
            false,
            null,
            "Snapshot revision returned by the first page and required for continuation pages.",
            new(Minimum: 1))
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        new(
            MessageKeys.Subscriptions.UserInfo,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.KickbackInfo,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.ClubOffersSnapshot,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.BuildersClubFurniCount,
            Direction.In,
            ApplicationMessageRole.Observe),
        new(
            MessageKeys.Subscriptions.BuildersClubMembershipStatus,
            Direction.In,
            ApplicationMessageRole.Observe,
            false),
        new(
            MessageKeys.Subscriptions.BuildersClubPlacementWarning,
            Direction.In,
            ApplicationMessageRole.Observe,
            false)
    ];

    private static ApplicationParameterDescriptor ProductNameParameter(bool refresh) => new(
        "product_name",
        typeof(string),
        false,
        refresh ? "habbo_club" : null,
        refresh
            ? "Non-empty subscription product name sent to the hotel."
            : "Optional case-insensitive exact product filter.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time across both attempts.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor ClubOffersLimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum club offers returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor ClubOffersTimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time for the single request attempt.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor RequiredInt(
        string name,
        string description) => new(name, typeof(int), true, null, description);

    private static ApplicationParameterDescriptor ExtraDataParameter() => new(
        "extra_data",
        typeof(string),
        false,
        string.Empty,
        "Offer selection data.",
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApplicationParameterDescriptor ExpectedGenerationParameter() => new(
        "expected_session_generation",
        typeof(long?),
        false,
        null,
        "Optional active session generation precondition.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor ExpectedRoomGenerationParameter() => new(
        "expected_room_generation",
        typeof(long?),
        false,
        null,
        "Optional active room generation precondition.",
        new(Minimum: 1));
}
