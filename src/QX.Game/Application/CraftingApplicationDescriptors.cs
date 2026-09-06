using Qx.Game.Protocol;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class CraftingApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui |
        ApplicationExposure.Cli |
        ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.CraftingState,
        "Crafting state",
        "Reads one bounded state view from a current or retained immutable crafting snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CraftingStateRequest),
        typeof(CraftingStateView),
        [SnapshotRevisionParameter(false)],
        state_effects: [CraftingRead()],
        messages: ObservedMessages(),
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ProductsList { get; } = new(
        ApplicationMemberIds.CraftingProductsList,
        "Crafting products page",
        "Reads one bounded products or usable-furniture-class page from an immutable crafting snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CraftingProductsPageRequest),
        typeof(CraftingProductsPage),
        ProductsPageParameters(),
        state_effects: [CraftingRead()],
        messages:
        [
            Observe(MessageKeys.Crafting.ProductsSnapshot)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor RecipeList { get; } = new(
        ApplicationMemberIds.CraftingRecipeList,
        "Crafting recipe page",
        "Reads one bounded ingredient page from an immutable crafting snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(CraftingRecipePageRequest),
        typeof(CraftingRecipePage),
        PageParameters(),
        state_effects: [CraftingRead()],
        messages:
        [
            Observe(MessageKeys.Crafting.RecipeSnapshot)
        ],
        tool_hints: QueryHints(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ProductsRefresh { get; } = new(
        ApplicationMemberIds.CraftingProductsRefresh,
        "Refresh crafting products",
        "Requests products in one pinned room and returns the first fresh post-dispatch route response only when no intervening crafting-products request invalidates its request epoch; the response does not echo the crafting-furniture id, so RequestedCraftingFurnitureId is request metadata only.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CraftingProductsRefreshRequest),
        typeof(CraftingProductsRefreshResult),
        [
            FurnitureIdParameter(),
            LimitParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.ProductsRequest),
            Observe(MessageKeys.Crafting.ProductsSnapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor RecipeRefresh { get; } = new(
        ApplicationMemberIds.CraftingRecipeRefresh,
        "Refresh crafting recipe",
        "Requests one recipe in a pinned room and returns the first fresh post-dispatch route response only when no intervening crafting-recipe request invalidates its request epoch; the response does not echo the recipe code, so RequestedRecipeCode is request metadata only.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CraftingRecipeRefreshRequest),
        typeof(CraftingRecipeRefreshResult),
        [
            RecipeCodeParameter(),
            LimitParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.RecipeRequest),
            Observe(MessageKeys.Crafting.RecipeSnapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor AvailabilityRefresh { get; } = new(
        ApplicationMemberIds.CraftingAvailabilityRefresh,
        "Refresh crafting availability",
        "Requests recipe availability in one pinned room and returns the first fresh post-dispatch route response only when no intervening availability request invalidates its request epoch; the response does not echo the request values, so the Requested fields are request metadata only.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CraftingAvailabilityRefreshRequest),
        typeof(CraftingAvailabilityRefreshResult),
        [
            FurnitureIdParameter(),
            IngredientIdsParameter(),
            TimeoutParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingChange(), RoomRead()],
        [
            Send(MessageKeys.Crafting.AvailabilityRequest),
            Observe(MessageKeys.Crafting.AvailabilitySnapshot)
        ],
        RefreshHints());

    public static ApplicationDescriptor Craft { get; } = new(
        ApplicationMemberIds.CraftingCraft,
        "Craft recipe",
        "Dispatches one known-recipe craft request in a pinned room without awaiting or assigning a crafting result.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CraftingCraftRequest),
        typeof(CraftingCraftDispatchReceipt),
        [
            FurnitureIdParameter(),
            RecipeCodeParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingRead(), RoomRead()],
        [
            Send(MessageKeys.Crafting.Craft),
            Observe(MessageKeys.Crafting.Result, false)
        ],
        DispatchHints());

    public static ApplicationDescriptor SecretCraft { get; } = new(
        ApplicationMemberIds.CraftingSecretCraft,
        "Craft secret recipe",
        "Dispatches one secret-craft request in a pinned room without awaiting or assigning a crafting result.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(CraftingSecretCraftRequest),
        typeof(CraftingSecretCraftDispatchReceipt),
        [
            FurnitureIdParameter(),
            IngredientIdsParameter(),
            ExpectedSessionGenerationParameter(),
            ExpectedRoomGenerationParameter()
        ],
        OperationStates(),
        [CraftingRead(), RoomRead()],
        [
            Send(MessageKeys.Crafting.SecretCraft),
            Observe(MessageKeys.Crafting.Result, false)
        ],
        DispatchHints());

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.CraftingChanged,
        "Crafting changed",
        "Publishes bounded passive crafting-state changes without assigning result messages to craft requests.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(CraftingChanged),
        state_effects: [CraftingChange()],
        messages: ObservedMessages(),
        invocation_scope: ApplicationInvocationScope.Persistent);

    private static IReadOnlyList<ApplicationParameterDescriptor>
        ProductsPageParameters() =>
    [
        new(
            "collection",
            typeof(CraftingProductsCollection),
            false,
            CraftingProductsCollection.Products,
            "Collection selected from the immutable products snapshot."),
        .. PageParameters()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> PageParameters() =>
    [
        new(
            "offset",
            typeof(int),
            false,
            0,
            "Zero-based offset within the selected collection.",
            new(Minimum: 0)),
        LimitParameter(),
        SnapshotRevisionParameter(true)
    ];

    private static IReadOnlyList<ApplicationStateKey> OperationStates() =>
    [
        ApplicationStateKey.HotelConnected,
        ApplicationStateKey.RoomReady
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> ObservedMessages() =>
    [
        Observe(MessageKeys.Crafting.ProductsSnapshot),
        Observe(MessageKeys.Crafting.RecipeSnapshot),
        Observe(MessageKeys.Crafting.Result),
        Observe(MessageKeys.Crafting.AvailabilitySnapshot)
    ];

    private static ApplicationParameterDescriptor FurnitureIdParameter() => new(
        "crafting_furniture_id",
        typeof(Id),
        true,
        null,
        "Positive crafting-furniture identifier valid for the active client dialect.",
        new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor RecipeCodeParameter() => new(
        "recipe_code",
        typeof(string),
        true,
        null,
        "Nonblank recipe code within the wire-string budget.",
        new(
            MinLength: 1,
            MaxUtf8Bytes: ushort.MaxValue,
            Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor IngredientIdsParameter() => new(
        "ingredient_item_ids",
        typeof(IReadOnlyList<Id>),
        true,
        null,
        "Ingredient item identifiers copied before dispatch.",
        new(MinItems: 0, MaxItems: ushort.MaxValue));

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        100,
        "Maximum rows returned from one collection.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total wait for the first fresh route response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter(
        bool continuation) => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        continuation
            ? "Snapshot revision returned by the first read and required for continuation pages."
            : "Optional retained snapshot revision; omitted to capture the current state.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor ExpectedSessionGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_session_generation",
            "Optional active hotel-session generation required through dispatch.");

    private static ApplicationParameterDescriptor ExpectedRoomGenerationParameter() =>
        ExpectedRevisionParameter(
            "expected_room_generation",
            "Optional room generation required through dispatch.");

    private static ApplicationParameterDescriptor ExpectedRevisionParameter(
        string name,
        string description) => new(
        name,
        typeof(long?),
        false,
        null,
        description,
        new(Minimum: 1));

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(
        MessageKey key,
        bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationStateEffect CraftingRead() =>
        new(ApplicationStateKey.Crafting, ApplicationStateEffectKind.Reads);

    private static ApplicationStateEffect CraftingChange() =>
        new(ApplicationStateKey.Crafting, ApplicationStateEffectKind.Changes);

    private static ApplicationStateEffect RoomRead() =>
        new(ApplicationStateKey.RoomActive, ApplicationStateEffectKind.Reads);

    private static ApplicationToolHints QueryHints() => new(true, false, true, false);
    private static ApplicationToolHints RefreshHints() => new(false, false, true, true);
    private static ApplicationToolHints DispatchHints() => new(false, true, false, true);
}
