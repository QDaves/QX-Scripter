using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class InventoryApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.InventoryState,
        "Inventory state",
        "Reads immutable furni and pet inventory lifecycle summaries for the active session.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(InventoryStateRequest),
        typeof(InventoryStateView),
        [],
        state_effects:
        [
            new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.InventoryPetsLoaded, ApplicationStateEffectKind.Reads)
        ],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor FurniList { get; } = new(
        ApplicationMemberIds.InventoryFurniList,
        "Furni inventory page",
        "Reads a bounded page from one immutable furni inventory snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(InventoryFurniPageRequest),
        typeof(InventoryFurniPage),
        FurniPageParameters(),
        state_effects:
        [new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Reads)],
        messages: FurniMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor FurniRefresh { get; } = new(
        ApplicationMemberIds.InventoryFurniRefresh,
        "Refresh furni inventory",
        "Loads every fragment for the active session and returns the first bounded snapshot page.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(InventoryFurniRefreshRequest),
        typeof(InventoryFurniPage),
        [OptionalId("item_id", "Optional exact inventory item identifier."), LimitParameter(), TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Inventory.Furni.Request, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Inventory.Furni.Snapshot, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor PetsList { get; } = new(
        ApplicationMemberIds.InventoryPetsList,
        "Pet inventory page",
        "Reads a bounded page from one immutable pet inventory snapshot with optional combined exact filters.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(InventoryPetPageRequest),
        typeof(InventoryPetPage),
        PetPageParameters(),
        state_effects:
        [new(ApplicationStateKey.InventoryPetsLoaded, ApplicationStateEffectKind.Reads)],
        messages: PetMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor PetsRefresh { get; } = new(
        ApplicationMemberIds.InventoryPetsRefresh,
        "Refresh pet inventory",
        "Loads every fragment for the active session and returns the first bounded snapshot page.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(InventoryPetRefreshRequest),
        typeof(InventoryPetPage),
        [
            OptionalId("pet_id", "Optional exact pet identifier; combined with name when both are present."),
            NameParameter(),
            LimitParameter(),
            TimeoutParameter()
        ],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.InventoryPetsLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Inventory.Pets.Request, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Inventory.Pets.Snapshot, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor AvatarEffectActivate { get; } = new(
        ApplicationMemberIds.InventoryAvatarEffectActivate,
        "Activate avatar effect",
        "Activates an owned avatar effect through the active client dialect.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(InventoryAvatarEffectRequest),
        typeof(InventoryDispatchResult),
        [new("effect_id", typeof(int), true, null, "Avatar effect identifier.")],
        [ApplicationStateKey.HotelConnected],
        messages:
        [
            new(
                MessageKeys.Inventory.AvatarEffects.ActivationRequest,
                Direction.Out,
                ApplicationMessageRole.Send)
        ],
        tool_hints: new(false, true, false, true));

    public static ApplicationDescriptor FurniChanged { get; } = new(
        ApplicationMemberIds.InventoryFurniChanged,
        "Furni inventory changed",
        "Publishes bounded immutable furni load, invalidation, add, update, remove and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(InventoryFurniChanged),
        state_effects:
        [new(ApplicationStateKey.InventoryFurniLoaded, ApplicationStateEffectKind.Changes)],
        messages: FurniMessages());

    public static ApplicationDescriptor PetsChanged { get; } = new(
        ApplicationMemberIds.InventoryPetsChanged,
        "Pet inventory changed",
        "Publishes bounded immutable pet load, add, update, remove and reset changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(InventoryPetChanged),
        state_effects:
        [new(ApplicationStateKey.InventoryPetsLoaded, ApplicationStateEffectKind.Changes)],
        messages: PetMessages());

    private static IReadOnlyList<ApplicationParameterDescriptor> FurniPageParameters() =>
    [
        OptionalId("item_id", "Optional exact inventory item identifier."),
        OffsetParameter(),
        LimitParameter(),
        SnapshotRevisionParameter()
    ];

    private static IReadOnlyList<ApplicationParameterDescriptor> PetPageParameters() =>
    [
        OptionalId("pet_id", "Optional exact pet identifier; combined with name when both are present."),
        NameParameter(),
        OffsetParameter(),
        LimitParameter(),
        SnapshotRevisionParameter()
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> StateMessages() =>
        [.. FurniMessages(), .. PetMessages()];

    private static IReadOnlyList<ApplicationMessageRequirement> FurniMessages() =>
    [
        Observe(MessageKeys.Inventory.Furni.Snapshot),
        Observe(MessageKeys.Inventory.Furni.AddedOrUpdated),
        Observe(MessageKeys.Inventory.Furni.Removed),
        Observe(MessageKeys.Inventory.Furni.RemovedMultiple, false),
        Observe(MessageKeys.Inventory.Furni.Invalidated),
        Observe(MessageKeys.Inventory.Furni.PostItPlaced)
    ];

    private static IReadOnlyList<ApplicationMessageRequirement> PetMessages() =>
    [
        Observe(MessageKeys.Inventory.Pets.Snapshot),
        Observe(MessageKeys.Inventory.Pets.Added),
        Observe(MessageKeys.Inventory.Pets.Removed)
    ];

    private static ApplicationMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationParameterDescriptor OptionalId(string name, string description) =>
        new(name, typeof(Id?), false, null, description, new(Pattern: "^[1-9][0-9]*$"));

    private static ApplicationParameterDescriptor NameParameter() => new(
        "name",
        typeof(string),
        false,
        null,
        "Optional case-insensitive exact pet name; combined with pet_id when both are present.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor OffsetParameter() => new(
        "offset",
        typeof(int),
        false,
        0,
        "Zero-based offset within the selected immutable snapshot.",
        new(Minimum: 0));

    private static ApplicationParameterDescriptor LimitParameter() => new(
        "limit",
        typeof(int),
        false,
        200,
        "Maximum entries returned by this page.",
        new(Minimum: 1, Maximum: 500));

    private static ApplicationParameterDescriptor SnapshotRevisionParameter() => new(
        "snapshot_revision",
        typeof(long?),
        false,
        null,
        "Snapshot revision returned by the first page and required for continuation pages.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum total time to wait for every fragment.",
        new(Minimum: 1, Maximum: 120000));
}
