using Qx.Messages;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class NavigatorApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.NavigatorState,
        "Navigator state",
        "Reads the active session's immutable navigator metadata and personalization snapshot.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(NavigatorStateRequest),
        typeof(NavigatorState),
        state_effects:
        [
            new(ApplicationStateKey.NavigatorMetadataLoaded, ApplicationStateEffectKind.Reads),
            new(ApplicationStateKey.NavigatorFlatCategoriesLoaded, ApplicationStateEffectKind.Reads)
        ],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor MetadataRefresh { get; } = new(
        ApplicationMemberIds.NavigatorMetadataRefresh,
        "Refresh navigator metadata",
        "Loads the navigator categories published for the active hotel session.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(NavigatorRefreshRequest),
        typeof(NavigatorState),
        [TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.NavigatorMetadataLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Navigator.State.MetadataRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Navigator.State.Metadata, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor FlatCategoriesRefresh { get; } = new(
        ApplicationMemberIds.NavigatorFlatCategoriesRefresh,
        "Refresh room categories",
        "Loads the room categories published for the active hotel session.",
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(NavigatorRefreshRequest),
        typeof(NavigatorState),
        [TimeoutParameter()],
        [ApplicationStateKey.HotelConnected],
        [new(ApplicationStateKey.NavigatorFlatCategoriesLoaded, ApplicationStateEffectKind.Changes)],
        [
            new(MessageKeys.Navigator.State.FlatCategoriesRequest, Direction.Out, ApplicationMessageRole.Send),
            new(MessageKeys.Navigator.State.FlatCategories, Direction.In, ApplicationMessageRole.Observe)
        ],
        new(true, false, true, true));

    public static ApplicationDescriptor SearchView { get; } = Search<NavigatorViewSearchInput>(
        ApplicationMemberIds.NavigatorSearchView,
        "Search navigator view",
        "Searches a hotel-published navigator view with an explicit filter.",
        MessageKeys.Navigator.Search.View,
        [RequiredText("search_code", "Hotel-published navigator view code."), OptionalText("filter", "Navigator filter text."), TimeoutParameter()]);

    public static ApplicationDescriptor SearchText { get; } = Search<NavigatorTextSearchInput>(
        ApplicationMemberIds.NavigatorSearchText,
        "Search rooms by text",
        "Searches rooms by the selected navigator text field.",
        MessageKeys.Navigator.Search.Text,
        [
            new("field", typeof(RoomSearchField), true, null, "Navigator text field."),
            OptionalText("text", "Text to search for."),
            TimeoutParameter()
        ],
        MessageKeys.Navigator.Search.LegacyResult);

    public static ApplicationDescriptor SearchMyRooms { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyRooms,
        "Search my rooms",
        "Loads rooms owned by the active account.",
        MessageKeys.Navigator.Search.MyRooms);

    public static ApplicationDescriptor SearchMyFavourites { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyFavourites,
        "Search favourite rooms",
        "Loads rooms favourited by the active account.",
        MessageKeys.Navigator.Search.MyFavouriteRooms);

    public static ApplicationDescriptor SearchMyRoomRights { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyRoomRights,
        "Search rooms with rights",
        "Loads rooms where the active account has room rights.",
        MessageKeys.Navigator.Search.MyRoomRights);

    public static ApplicationDescriptor SearchMyHistory { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyHistory,
        "Search room history",
        "Loads rooms recently visited by the active account.",
        MessageKeys.Navigator.Search.MyRoomHistory);

    public static ApplicationDescriptor SearchMyFrequentHistory { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyFrequentHistory,
        "Search frequent rooms",
        "Loads rooms most frequently visited by the active account.",
        MessageKeys.Navigator.Search.MyFrequentRoomHistory);

    public static ApplicationDescriptor SearchMyFriendsRooms { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyFriendsRooms,
        "Search friends' rooms",
        "Loads rooms owned by friends of the active account.",
        MessageKeys.Navigator.Search.MyFriendsRooms);

    public static ApplicationDescriptor SearchFriendsPresent { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchFriendsPresent,
        "Search rooms with friends",
        "Loads rooms where friends of the active account are currently present.",
        MessageKeys.Navigator.Search.RoomsWhereFriendsAre);

    public static ApplicationDescriptor SearchMyGuildBases { get; } = QuickSearch(
        ApplicationMemberIds.NavigatorSearchMyGuildBases,
        "Search my guild bases",
        "Loads bases of guilds joined by the active account.",
        MessageKeys.Navigator.Search.MyGuildBases);

    public static ApplicationDescriptor SearchPopular { get; } = Search<NavigatorPopularSearchInput>(
        ApplicationMemberIds.NavigatorSearchPopular,
        "Search popular rooms",
        "Loads popular rooms, optionally filtered by tag.",
        MessageKeys.Navigator.Search.Popular,
        [OptionalText("tag", "Optional room tag."), AdIndexParameter(), TimeoutParameter()]);

    public static ApplicationDescriptor SearchHighestScore { get; } = Search<NavigatorAdSearchInput>(
        ApplicationMemberIds.NavigatorSearchHighestScore,
        "Search highest-scoring rooms",
        "Loads the hotel's highest-scoring rooms.",
        MessageKeys.Navigator.Search.HighestScoring,
        [AdIndexParameter(), TimeoutParameter()]);

    public static ApplicationDescriptor SearchGuildBases { get; } = Search<NavigatorAdSearchInput>(
        ApplicationMemberIds.NavigatorSearchGuildBases,
        "Search guild bases",
        "Loads public guild-base rooms.",
        MessageKeys.Navigator.Search.GuildBases,
        [AdIndexParameter(), TimeoutParameter()]);

    public static ApplicationDescriptor SavedSearchAdd { get; } = Personalization<NavigatorSavedSearchAddInput>(
        ApplicationMemberIds.NavigatorSavedSearchAdd,
        "Add saved search",
        "Adds a navigator search to the active account's saved searches.",
        MessageKeys.Navigator.Personalization.SavedSearchAdd,
        [RequiredText("search_code", "Navigator view code."), OptionalText("filter", "Navigator filter text.")],
        new(false, false, false, true));

    public static ApplicationDescriptor SavedSearchDelete { get; } = Personalization<NavigatorSavedSearchDeleteInput>(
        ApplicationMemberIds.NavigatorSavedSearchDelete,
        "Delete saved search",
        "Deletes a saved navigator search from the active account.",
        MessageKeys.Navigator.Personalization.SavedSearchDelete,
        [new("saved_search_id", typeof(int), true, null, "Saved-search identifier.", new(Minimum: 0))],
        new(false, true, false, true));

    public static ApplicationDescriptor CategoryCollapse { get; } = Personalization<NavigatorCategoryInput>(
        ApplicationMemberIds.NavigatorCategoryCollapse,
        "Collapse navigator category",
        "Collapses a navigator category for the active account.",
        MessageKeys.Navigator.Personalization.CollapsedCategoryAdd,
        [RequiredText("search_code", "Navigator category code.")],
        new(false, false, true, true));

    public static ApplicationDescriptor CategoryExpand { get; } = Personalization<NavigatorCategoryInput>(
        ApplicationMemberIds.NavigatorCategoryExpand,
        "Expand navigator category",
        "Expands a collapsed navigator category for the active account.",
        MessageKeys.Navigator.Personalization.CollapsedCategoryRemove,
        [RequiredText("search_code", "Navigator category code.")],
        new(false, false, true, true));

    public static ApplicationDescriptor RoomCreate { get; } = RoomOperation<NavigatorRoomCreateInput>(
        ApplicationMemberIds.NavigatorRoomCreate,
        "Create room",
        "Creates a room owned by the active account.",
        MessageKeys.Navigator.RoomCreate,
        [
            RequiredText("name", "Room name."),
            OptionalText("description", "Room description."),
            RequiredText("model", "Floor-plan model name."),
            new("category", typeof(int), true, null, "Navigator category identifier."),
            new("max_visitors", typeof(int), true, null, "Maximum visitor count."),
            new("trade_mode", typeof(int), false, 0, "Room trading mode.")
        ]);

    public static ApplicationDescriptor RoomDelete { get; } = RoomOperation<NavigatorRoomDeleteInput>(
        ApplicationMemberIds.NavigatorRoomDelete,
        "Delete room",
        "Deletes a room owned by the active account.",
        MessageKeys.Navigator.RoomDelete,
        [new("room_id", typeof(Id), true, null, "Room identifier.")]);

    public static ApplicationDescriptor HomeRoomSet { get; } = RoomOperation<NavigatorHomeRoomSetInput>(
        ApplicationMemberIds.NavigatorHomeRoomSet,
        "Set home room",
        "Sets or clears the active account's home room.",
        MessageKeys.Navigator.HomeRoomUpdate,
        [new("room_id", typeof(Id), true, null, "Room identifier, or zero to clear it.")]);

    public static ApplicationDescriptor Changed { get; } = new(
        ApplicationMemberIds.NavigatorChanged,
        "Navigator state changed",
        "Publishes immutable navigator metadata and personalization state changes.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(NavigatorChanged),
        state_effects:
        [
            new(ApplicationStateKey.NavigatorMetadataLoaded, ApplicationStateEffectKind.Changes),
            new(ApplicationStateKey.NavigatorFlatCategoriesLoaded, ApplicationStateEffectKind.Changes)
        ],
        messages: StateMessages());

    public static ApplicationDescriptor SearchReceived { get; } = new(
        ApplicationMemberIds.NavigatorSearchReceived,
        "Navigator search received",
        "Publishes immutable navigator search results observed for the active session.",
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(NavigatorSearchReceived),
        messages:
        [
            new(MessageKeys.Navigator.Search.Result, Direction.In, ApplicationMessageRole.Observe),
            new(MessageKeys.Navigator.Search.LegacyResult, Direction.In, ApplicationMessageRole.Observe)
        ]);

    private static ApplicationDescriptor QuickSearch(
        string id,
        string title,
        string description,
        MessageKey request_key) => Search<NavigatorSearchRequest>(
            id,
            title,
            description,
            request_key,
            [TimeoutParameter()]);

    private static ApplicationDescriptor Search<TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        MessageKey? result_key = null) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(NavigatorSearchSnapshot),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages:
            [
                new(request_key, Direction.Out, ApplicationMessageRole.Send),
                new(
                    result_key ?? MessageKeys.Navigator.Search.Result,
                    Direction.In,
                    ApplicationMessageRole.Observe)
            ],
            tool_hints: new(true, false, true, true));

    private static ApplicationDescriptor Personalization<TRequest>(
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
            typeof(NavigatorOperationResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages: [new(request_key, Direction.Out, ApplicationMessageRole.Send)],
            tool_hints: hints);

    private static ApplicationDescriptor RoomOperation<TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        IReadOnlyList<ApplicationParameterDescriptor> parameters) => new(
            id,
            title,
            description,
            ApplicationMemberKind.Operation,
            ApplicationExposure.All,
            typeof(TRequest),
            typeof(NavigatorRoomOperationResult),
            parameters,
            [ApplicationStateKey.HotelConnected],
            messages: [new(request_key, Direction.Out, ApplicationMessageRole.Send)],
            tool_hints: new(false, true, false, true));

    private static ApplicationParameterDescriptor TimeoutParameter() => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        10000,
        "Maximum time to wait for the hotel response.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor AdIndexParameter() => new(
        "ad_index",
        typeof(int),
        false,
        -1,
        "Promoted-room slot requested alongside the search result.",
        new(Minimum: -1));

    private static ApplicationParameterDescriptor RequiredText(string name, string description) => new(
        name,
        typeof(string),
        true,
        null,
        description,
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue, Pattern: @".*\S.*"));

    private static ApplicationParameterDescriptor OptionalText(string name, string description) => new(
        name,
        typeof(string),
        false,
        string.Empty,
        description,
        new(MaxUtf8Bytes: ushort.MaxValue));

    private static ApplicationMessageRequirement[] StateMessages() =>
    [
        new(MessageKeys.Navigator.State.Metadata, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.State.FlatCategories, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.State.LiftedRooms, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.State.Settings, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.State.Preferences, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.Personalization.SavedSearches, Direction.In, ApplicationMessageRole.Observe),
        new(MessageKeys.Navigator.Personalization.CollapsedCategories, Direction.In, ApplicationMessageRole.Observe)
    ];
}
