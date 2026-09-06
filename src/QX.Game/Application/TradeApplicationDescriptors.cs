using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class TradeApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.TradeState,
        "Trade state",
        "Reads a bounded immutable projection of the active trade and NFT inventory summary.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(TradeStateRequest),
        typeof(TradeStateView),
        [OutputLimit("offer_item_limit", "Maximum items returned for each participant offer."), OutputLimit("nft_offer_limit", "Maximum NFT assets returned for each participant offer.")],
        state_effects:
        [
            new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.TradeActive, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.TradeNftInventoryLoaded, ApplicationStateEffectKind.Reads)
        ],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Open { get; } = Dispatch<TradeOpenRequest>(
        ApplicationMemberIds.TradeOpen,
        "Open trade",
        "Requests a trade with one user in the current ready room.",
        [
            new("user_index", typeof(int), true, null, "Target avatar room index.", new(Minimum: 0)),
            .. GuardParameters(),
            new("expected_room_generation", typeof(long?), false, null, "Optional ready-room generation guard for this transient avatar index.", new(Minimum: 0)),
            new("expected_user_id", typeof(Id?), false, null, "Optional target identity guard for this transient avatar index.", new(Pattern: "^[1-9][0-9]*$"))
        ],
        MessageKeys.Trade.OpenRequest,
        [ApplicationStateKey.TradeInactive]);

    public static ApplicationDescriptor ItemsAdd { get; } = Dispatch<TradeItemsAddRequest>(
        ApplicationMemberIds.TradeItemsAdd,
        "Add trade items",
        "Adds distinct inventory item identifiers to the active trade offer.",
        [new("item_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct nonzero inventory item identifiers valid for the active client dialect.", new(MinItems: 1, MaxItems: ushort.MaxValue)), .. GuardParameters()],
        MessageKeys.Trade.ItemsAdd,
        [ApplicationStateKey.TradeTrading]);

    public static ApplicationDescriptor ItemRemove { get; } = Dispatch<TradeItemRemoveRequest>(
        ApplicationMemberIds.TradeItemRemove,
        "Remove trade item",
        "Removes one inventory item identifier from the active trade offer.",
        [new("item_id", typeof(Id), true, null, "Nonzero inventory item identifier valid for the active client dialect.", new(Pattern: "^-?[1-9][0-9]*$")), .. GuardParameters()],
        MessageKeys.Trade.ItemRemove,
        [ApplicationStateKey.TradeTrading]);

    public static ApplicationDescriptor Accept { get; } = Command(
        ApplicationMemberIds.TradeAccept,
        "Accept trade",
        "Accepts the current offer state without assuming hotel confirmation.",
        MessageKeys.Trade.Accept,
        [
            ApplicationStateKey.TradeTrading,
            ApplicationStateKey.ProfileLoaded,
            ApplicationStateKey.TradeLocalCanTrade,
            ApplicationStateKey.TradeSilverFeeReached
        ]);

    public static ApplicationDescriptor Unaccept { get; } = Command(
        ApplicationMemberIds.TradeUnaccept,
        "Withdraw trade acceptance",
        "Withdraws acceptance from the active trade.",
        MessageKeys.Trade.Unaccept,
        [ApplicationStateKey.TradeActive]);

    public static ApplicationDescriptor Confirm { get; } = Command(
        ApplicationMemberIds.TradeConfirm,
        "Confirm trade",
        "Dispatches final confirmation for a trade awaiting confirmation.",
        MessageKeys.Trade.Confirm,
        [
            ApplicationStateKey.TradeAwaitingConfirmation,
            ApplicationStateKey.ProfileLoaded,
            ApplicationStateKey.TradeLocalCanTrade,
            ApplicationStateKey.TradeSilverFeeReached
        ]);

    public static ApplicationDescriptor Close { get; } = Command(
        ApplicationMemberIds.TradeClose,
        "Close trade",
        "Requests closure of the active trade without assuming the hotel accepted it.",
        MessageKeys.Trade.Close,
        [ApplicationStateKey.TradeActive]);

    public static ApplicationDescriptor NftInventoryList { get; } = new(
        ApplicationMemberIds.TradeNftInventoryList,
        "Trade NFT inventory page",
        "Reads one bounded page from an immutable session-bound NFT inventory snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(TradeNftInventoryPageRequest),
        typeof(TradeNftInventoryPage),
        PageParameters(),
        state_effects:
        [new(ApplicationStateKey.TradeNftInventoryLoaded, ApplicationStateEffectKind.Reads)],
        messages: [Observe(MessageKeys.Trade.NftInventory, false)],
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor NftInventoryRefresh { get; } = new(
        ApplicationMemberIds.TradeNftInventoryRefresh,
        "Refresh trade NFT inventory",
        "Loads the Flash trade NFT inventory and returns its first bounded snapshot page.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TradeNftInventoryRefreshRequest),
        typeof(TradeNftInventoryPage),
        [Limit(), Timeout()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.TradeNftInventoryLoaded, ApplicationStateEffectKind.Changes)],
        [
            Send(MessageKeys.Trade.NftInventoryRequest),
            Observe(MessageKeys.Trade.NftInventory)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.TradeChanged,
        "Trade changed",
        "Publishes bounded lifecycle summaries without embedding offer or NFT inventory lists.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(TradeChanged),
        state_effects:
        [
            new(ApplicationStateKey.TradeActive, ApplicationStateEffectKind.Changes),
            new(ApplicationStateKey.TradeNftInventoryLoaded, ApplicationStateEffectKind.Changes)
        ],
        messages: StateMessages());

    private static ApplicationDescriptor Command(
        string id,
        string title,
        string description,
        MessageKey key,
        IReadOnlyList<ApplicationStateKey> required_states) => Dispatch<TradeCommandRequest>(
            id,
            title,
            description,
            GuardParameters(),
            key,
            required_states);

    private static ApplicationDescriptor Dispatch<TRequest>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey key,
        IReadOnlyList<ApplicationStateKey> required_states) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(TradeDispatchResult),
            parameters,
            [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomReady, .. required_states],
            [new(ApplicationStateKey.TradeActive, ApplicationStateEffectKind.Changes)],
            [Send(key)],
            new(false, true, false, true));

    private static IReadOnlyList<ApplicationMessageRequirement> StateMessages() =>
    [
        Observe(MessageKeys.Trade.Opened),
        Observe(MessageKeys.Trade.Offers),
        Observe(MessageKeys.Trade.AcceptanceUpdated),
        Observe(MessageKeys.Trade.Confirmation),
        Observe(MessageKeys.Trade.Completed),
        Observe(MessageKeys.Trade.Closed),
        Observe(MessageKeys.Trade.OpenFailed),
        Observe(MessageKeys.Trade.NftOffers, false),
        Observe(MessageKeys.Trade.NftInventory, false),
        Observe(MessageKeys.Trade.SilverUpdated, false),
        Observe(MessageKeys.Trade.SilverFee, false)
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new("offset", typeof(int), false, 0, "Zero-based offset within the immutable snapshot.", new(Minimum: 0)),
        Limit(),
        new("snapshot_revision", typeof(long?), false, null, "Snapshot revision returned by the first page and required for continuation pages.", new(Minimum: 1))
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> GuardParameters() =>
    [
        new("expected_session_generation", typeof(long?), false, null, "Optional session generation guard from trade.state or trade.changed.", new(Minimum: 0)),
        new("expected_revision", typeof(long?), false, null, "Optional state revision guard from trade.state or trade.changed.", new(Minimum: 0)),
        new("expected_epoch", typeof(long?), false, null, "Optional trade epoch guard from trade.state or trade.changed.", new(Minimum: 0))
    ];

    private static ApplicationParameterDescriptor OutputLimit(string name, string description) =>
        new(name, typeof(int), false, 100, description, new(Minimum: 0, Maximum: 500));

    private static ApplicationParameterDescriptor Limit() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum NFT assets returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor Timeout() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the correlated hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);
}
