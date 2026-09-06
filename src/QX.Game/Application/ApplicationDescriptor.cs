using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Application;

public enum ApplicationMemberKind
{
    Query,
    Operation,
    Event
}

public enum ApplicationInvocationScope
{
    Transient,
    Persistent
}

[Flags]
public enum ApplicationExposure
{
    None = 0,
    Ui = 1,
    Cli = 2,
    Scripting = 4,
    Mcp = 8,
    All = Ui | Cli | Scripting | Mcp
}

public enum ApplicationStateKey
{
    HotelConnected,
    CatalogCache,
    RoomActive,
    RoomReady,
    ProfileLoaded,
    ProfileBlockListLoaded,
    ProfileIgnoreListLoaded,
    ProfileFigureSetsLoaded,
    ProfileSanctionsLoaded,
    FriendsLoaded,
    NavigatorMetadataLoaded,
    NavigatorFlatCategoriesLoaded,
    MarketplaceConfigurationLoaded,
    MarketplaceEligibilityLoaded,
    InventoryFurniLoaded,
    InventoryPetsLoaded,
    TradeInactive,
    TradeActive,
    TradeTrading,
    TradeAwaitingConfirmation,
    TradeLocalCanTrade,
    TradeSilverFeeReached,
    TradeNftInventoryLoaded,
    RoomBansLoaded,
    WalletLoaded,
    RoomSettingsLoaded,
    CatalogPurchase,
    Subscriptions,
    Gifts,
    Crafting,
    Achievements,
    BadgeInventory,
    Earnings,
    DailyTasks,
    Quests,
    Forums,
    Leaderboards,
    Habbicons
}

public enum ApplicationStateEffectKind
{
    Reads,
    Changes,
    Invalidates
}

public enum ApplicationMessageRole
{
    Observe,
    Send
}

public sealed record ApplicationParameterDescriptor(
    string Name,
    Type Type,
    bool Required,
    object? DefaultValue,
    string Description,
    ApplicationParameterConstraints? Constraints = null);

public sealed record ApplicationParameterConstraints(
    long? Minimum = null,
    long? Maximum = null,
    int? MinLength = null,
    int? MaxLength = null,
    int? MinItems = null,
    int? MaxItems = null,
    int? MaxUtf8Bytes = null,
    string? Pattern = null);

public sealed record ApplicationStateEffect(
    ApplicationStateKey State,
    ApplicationStateEffectKind Kind);

public sealed record ApplicationMessageRequirement(
    MessageKey Key,
    Direction Direction,
    ApplicationMessageRole Role,
    bool Required = true,
    string? SchemaCapability = null);

public sealed record ApplicationToolHints(
    bool ReadOnly,
    bool Destructive,
    bool Idempotent,
    bool OpenWorld);

public sealed class ApplicationDescriptor
{
    public ApplicationDescriptor(
        string id,
        string title,
        string description,
        ApplicationMemberKind kind,
        ApplicationExposure exposure,
        Type? request_type,
        Type result_type,
        IEnumerable<ApplicationParameterDescriptor>? parameters = null,
        IEnumerable<ApplicationStateKey>? required_states = null,
        IEnumerable<ApplicationStateEffect>? state_effects = null,
        IEnumerable<ApplicationMessageRequirement>? messages = null,
        ApplicationToolHints? tool_hints = null,
        ApplicationInvocationScope invocation_scope = ApplicationInvocationScope.Transient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(result_type);
        if (kind is ApplicationMemberKind.Event && request_type is not null)
            throw new ArgumentException("An event cannot declare a request type.", nameof(request_type));
        if (kind is not ApplicationMemberKind.Event && request_type is null)
            throw new ArgumentException("A call requires a request type.", nameof(request_type));
        if (exposure is ApplicationExposure.None || (exposure & ~ApplicationExposure.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(exposure));
        if (!Enum.IsDefined(invocation_scope))
            throw new ArgumentOutOfRangeException(nameof(invocation_scope));
        if (exposure.HasFlag(ApplicationExposure.Mcp) && kind is ApplicationMemberKind.Event)
            throw new ArgumentException("MCP event exposure requires a streaming binding.", nameof(exposure));
        if (exposure.HasFlag(ApplicationExposure.Mcp) && tool_hints is null)
            throw new ArgumentException("MCP call exposure requires explicit tool hints.", nameof(tool_hints));
        if (tool_hints is { ReadOnly: true, Destructive: true })
            throw new ArgumentException("A read-only application member cannot be destructive.", nameof(tool_hints));

        ApplicationParameterDescriptor[] parameter_values = [.. parameters ?? []];
        string[] duplicate_parameters = parameter_values
            .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicate_parameters.Length != 0)
            throw new ArgumentException("Application parameter names must be unique.", nameof(parameters));
        foreach (ApplicationParameterDescriptor parameter in parameter_values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Name);
            ArgumentNullException.ThrowIfNull(parameter.Type);
            ArgumentException.ThrowIfNullOrWhiteSpace(parameter.Description);
            if (parameter.Required && parameter.DefaultValue is not null)
                throw new ArgumentException($"Required parameter '{parameter.Name}' cannot declare a default value.", nameof(parameters));
            if (parameter.DefaultValue is not null && !parameter.Type.IsInstanceOfType(parameter.DefaultValue))
                throw new ArgumentException($"Default value for '{parameter.Name}' does not match '{parameter.Type.FullName}'.", nameof(parameters));
            ValidateConstraints(parameter, nameof(parameters));
        }

        ApplicationStateKey[] state_values = [.. (required_states ?? []).Distinct()];
        ApplicationStateEffect[] effect_values = [.. state_effects ?? []];
        ApplicationMessageRequirement[] message_values = [.. messages ?? []];
        if (message_values.Any(message => message.Key.IsEmpty || message.Direction is Direction.None))
            throw new ArgumentException("Application message requirements need a semantic key and direction.", nameof(messages));
        if (message_values.Any(message =>
                message.SchemaCapability is not null &&
                string.IsNullOrWhiteSpace(message.SchemaCapability)))
        {
            throw new ArgumentException("Application message schema capabilities cannot be empty.", nameof(messages));
        }

        Id = id;
        Title = title;
        Description = description;
        Kind = kind;
        Exposure = exposure;
        RequestType = request_type;
        ResultType = result_type;
        Parameters = Array.AsReadOnly(parameter_values);
        RequiredStates = Array.AsReadOnly(state_values);
        StateEffects = Array.AsReadOnly(effect_values);
        Messages = Array.AsReadOnly(message_values);
        ToolHints = tool_hints;
        InvocationScope = invocation_scope;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public ApplicationMemberKind Kind { get; }
    public ApplicationExposure Exposure { get; }
    public Type? RequestType { get; }
    public Type ResultType { get; }
    public IReadOnlyList<ApplicationParameterDescriptor> Parameters { get; }
    public IReadOnlyList<ApplicationStateKey> RequiredStates { get; }
    public IReadOnlyList<ApplicationStateEffect> StateEffects { get; }
    public IReadOnlyList<ApplicationMessageRequirement> Messages { get; }
    public ApplicationToolHints? ToolHints { get; }
    public ApplicationInvocationScope InvocationScope { get; }

    private static void ValidateConstraints(
        ApplicationParameterDescriptor parameter,
        string argument_name)
    {
        if (parameter.Constraints is not { } constraints)
            return;
        Type type = Nullable.GetUnderlyingType(parameter.Type) ?? parameter.Type;
        bool numeric = type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(int) ||
            type == typeof(long);
        if ((constraints.Minimum is not null || constraints.Maximum is not null) && !numeric)
            throw new ArgumentException($"Parameter '{parameter.Name}' has numeric constraints for a non-numeric type.", argument_name);
        if (constraints.Minimum > constraints.Maximum)
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid numeric range.", argument_name);
        if ((constraints.MinLength is not null ||
             constraints.MaxLength is not null ||
             constraints.MaxUtf8Bytes is not null) && type != typeof(string))
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has string constraints for a non-string type.", argument_name);
        }
        if (constraints.MinLength < 0 ||
            constraints.MaxLength < 0 ||
            constraints.MaxUtf8Bytes < 1 ||
            constraints.MinLength > constraints.MaxLength)
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid string range.", argument_name);
        }
        bool collection = type != typeof(string) &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        if ((constraints.MinItems is not null || constraints.MaxItems is not null) && !collection)
            throw new ArgumentException($"Parameter '{parameter.Name}' has collection constraints for a non-collection type.", argument_name);
        if (constraints.MinItems < 0 ||
            constraints.MaxItems < 0 ||
            constraints.MinItems > constraints.MaxItems)
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' has an invalid collection range.", argument_name);
        }
        if (constraints.Pattern is { Length: 0 })
            throw new ArgumentException($"Parameter '{parameter.Name}' has an empty pattern.", argument_name);
    }
}

public static class ApplicationMemberIds
{
    public const string RoomChatHistory = "room.chat.history";
    public const string RoomChatTalk = "room.chat.talk";
    public const string RoomChatShout = "room.chat.shout";
    public const string RoomChatWhisper = "room.chat.whisper";
    public const string RoomChatReceived = "room.chat.received";
    public const string RoomAvatarWalk = "room.avatar.walk";
    public const string RoomAvatarLook = "room.avatar.look";
    public const string RoomAvatarDance = "room.avatar.dance";
    public const string RoomAvatarExpression = "room.avatar.expression";
    public const string RoomAvatarPosture = "room.avatar.posture";
    public const string RoomAvatarSign = "room.avatar.sign";
    public const string RoomAvatarEffect = "room.avatar.effect";
    public const string RoomAvatarTyping = "room.avatar.typing.set";
    public const string RoomItemFloorUse = "room.item.floor.use";
    public const string RoomItemWallUse = "room.item.wall.use";
    public const string RoomItemOneWayDoorEnter = "room.item.one_way_door.enter";
    public const string RoomItemDiceThrow = "room.item.dice.throw";
    public const string RoomItemDiceClear = "room.item.dice.clear";
    public const string RoomItemWallRemove = "room.item.wall.remove";
    public const string RoomItemStickySet = "room.item.sticky.set";
    public const string RoomItemPostItPlace = "room.item.post_it.place";
    public const string RoomItemPostItAdd = "room.item.post_it.add";
    public const string RoomEnter = "room.enter";
    public const string RoomLeave = "room.leave";
    public const string RoomDoorbellAnswer = "room.doorbell.answer";
    public const string RoomHandItemDrop = "room.hand_item.drop";
    public const string RoomHandItemPass = "room.hand_item.pass";
    public const string RoomRatingSubmit = "room.rating.submit";
    public const string RoomStaffPickSet = "room.staff_pick.set";
    public const string RoomPeopleRespect = "room.people.respect";
    public const string RoomPeopleRightsGrant = "room.people.rights.grant";
    public const string RoomPetRespect = "room.pet.respect";
    public const string RoomPetMountSet = "room.pet.mount.set";
    public const string RoomPetRemove = "room.pet.remove";
    public const string RoomBotRemove = "room.bot.remove";
    public const string RoomPlacementFloorPlace = "room.placement.floor.place";
    public const string RoomPlacementWallPlace = "room.placement.wall.place";
    public const string RoomPlacementFloorMove = "room.placement.floor.move";
    public const string RoomPlacementWallMove = "room.placement.wall.move";
    public const string RoomPlacementPickup = "room.placement.pickup";
    public const string RoomPlacementChanged = "room.placement.changed";
    public const string RoomPlacementPickupConfirmation = "room.placement.pickup_confirmation";
    public const string RoomModerationState = "room.moderation.state";
    public const string RoomModerationRefresh = "room.moderation.refresh";
    public const string RoomModerationMute = "room.moderation.mute";
    public const string RoomModerationKick = "room.moderation.kick";
    public const string RoomModerationBan = "room.moderation.ban";
    public const string RoomModerationUnban = "room.moderation.unban";
    public const string RoomModerationBounce = "room.moderation.bounce";
    public const string RoomModerationChanged = "room.moderation.changed";
    public const string RoomSettingsState = "room.settings.state";
    public const string RoomSettingsGet = "room.settings.get";
    public const string RoomSettingsSave = "room.settings.save";
    public const string RoomSettingsChanged = "room.settings.changed";
    public const string RoomDataGet = "room.data.get";
    public const string RoomRightsList = "room.rights.list";
    public const string RoomStickyGet = "room.sticky.get";
    public const string PetsInfoGet = "pets.info.get";
    public const string CatalogRoomAdInfoGet = "catalog.room_ad.info.get";
    public const string ProfileState = "profile.state";
    public const string ProfileRefresh = "profile.refresh";
    public const string ProfileBlocksList = "profile.blocks.list";
    public const string ProfileBlocksRefresh = "profile.blocks.refresh";
    public const string ProfileBlockAdd = "profile.block.add";
    public const string ProfileBlockRemove = "profile.block.remove";
    public const string ProfileIgnoresList = "profile.ignores.list";
    public const string ProfileIgnoresRefresh = "profile.ignores.refresh";
    public const string ProfileIgnoreAddById = "profile.ignore.add_by_id";
    public const string ProfileIgnoreAddByName = "profile.ignore.add_by_name";
    public const string ProfileIgnoreRemove = "profile.ignore.remove";
    public const string ProfileFigureSetsList = "profile.figure_sets.list";
    public const string ProfileSanctionsList = "profile.sanctions.list";
    public const string ProfileSanctionsRefresh = "profile.sanctions.refresh";
    public const string ProfileWardrobeGet = "profile.wardrobe.get";
    public const string ProfileMottoSet = "profile.motto.set";
    public const string ProfileFigureSet = "profile.figure.set";
    public const string ProfileWardrobeOutfitSave = "profile.wardrobe.outfit.save";
    public const string ProfileFavoriteGroupSelect = "profile.favorite_group.select";
    public const string ProfileFavoriteGroupDeselect = "profile.favorite_group.deselect";
    public const string ProfileChanged = "profile.changed";
    public const string ProfileBlockUpdated = "profile.block.updated";
    public const string ProfileIgnoreUpdated = "profile.ignore.updated";
    public const string PeopleProfileGet = "people.profile.get";
    public const string PeopleRelationshipGet = "people.relationship.get";
    public const string PeopleBadgesGet = "people.badges.get";
    public const string PeopleProfileOpen = "people.profile.open";
    public const string GroupsDetailsGet = "groups.details.get";
    public const string GroupsMembersPage = "groups.members.page";
    public const string GroupsMembershipsGet = "groups.memberships.get";
    public const string CatalogState = "catalog.state";
    public const string CatalogIndexGet = "catalog.index.get";
    public const string CatalogPageGet = "catalog.page.get";
    public const string CatalogPagesLoad = "catalog.pages.load";
    public const string CatalogPagesList = "catalog.pages.list";
    public const string CatalogOffersSearch = "catalog.offers.search";
    public const string CatalogCacheClear = "catalog.cache.clear";
    public const string CatalogPurchaseState = "catalog.purchase.state";
    public const string CatalogPurchaseSend = "catalog.purchase.send";
    public const string CatalogPurchaseOutcome = "catalog.purchase.outcome";
    public const string CatalogPublished = "catalog.published";
    public const string SubscriptionsState = "subscriptions.state";
    public const string SubscriptionsClubOffersList = "subscriptions.club_offers.list";
    public const string SubscriptionsClubOffersRefresh = "subscriptions.club_offers.refresh";
    public const string SubscriptionsUserInfoRefresh = "subscriptions.user_info.refresh";
    public const string SubscriptionsKickbackRefresh = "subscriptions.kickback.refresh";
    public const string SubscriptionsBuildersClubFurniCountRefresh =
        "subscriptions.builders_club.furni_count.refresh";
    public const string SubscriptionsBuildersClubFloorOfferPlace =
        "subscriptions.builders_club.floor_offer.place";
    public const string SubscriptionsBuildersClubWallOfferPlace =
        "subscriptions.builders_club.wall_offer.place";
    public const string SubscriptionsChanged = "subscriptions.changed";
    public const string GiftsState = "gifts.state";
    public const string GiftsWrappingList = "gifts.wrapping.list";
    public const string GiftsClubInfoList = "gifts.club_info.list";
    public const string GiftsClubSelectedList = "gifts.club_selected.list";
    public const string GiftsNewUserOfferList = "gifts.new_user_offer.list";
    public const string GiftsRefresh = "gifts.refresh";
    public const string GiftsPresentOpen = "gifts.present.open";
    public const string GiftsPurchase = "gifts.purchase";
    public const string GiftsClubSelect = "gifts.club.select";
    public const string GiftsOfferGiftabilityRefresh = "gifts.offer_giftability.refresh";
    public const string GiftsNewUserSelect = "gifts.new_user.select";
    public const string GiftsNewUserAdvance = "gifts.new_user.advance";
    public const string GiftsChanged = "gifts.changed";
    public const string CraftingState = "crafting.state";
    public const string CraftingProductsList = "crafting.products.list";
    public const string CraftingRecipeList = "crafting.recipe.list";
    public const string CraftingProductsRefresh = "crafting.products.refresh";
    public const string CraftingRecipeRefresh = "crafting.recipe.refresh";
    public const string CraftingAvailabilityRefresh =
        "crafting.availability.refresh";
    public const string CraftingCraft = "crafting.craft";
    public const string CraftingSecretCraft = "crafting.secret_craft";
    public const string CraftingChanged = "crafting.changed";
    public const string AchievementsState = "achievements.state";
    public const string AchievementsList = "achievements.list";
    public const string AchievementPointLimitsList = "achievements.point_limits.list";
    public const string AchievementsRefresh = "achievements.refresh";
    public const string AchievementPointLimitsRefresh =
        "achievements.point_limits.refresh";
    public const string AchievementsChanged = "achievements.changed";
    public const string BadgesState = "badges.state";
    public const string BadgesOwnedList = "badges.owned.list";
    public const string BadgesSelectedSetsList = "badges.selected_sets.list";
    public const string BadgesSelectedList = "badges.selected.list";
    public const string BadgesRefresh = "badges.refresh";
    public const string BadgesChanged = "badges.changed";
    public const string EarningsState = "earnings.state";
    public const string EarningsEntriesList = "earnings.entries.list";
    public const string EarningsRefresh = "earnings.refresh";
    public const string EarningsClaim = "earnings.claim";
    public const string EarningsChanged = "earnings.changed";
    public const string DailyTasksState = "daily_tasks.state";
    public const string DailyTasksEntriesList = "daily_tasks.entries.list";
    public const string DailyTasksRefresh = "daily_tasks.refresh";
    public const string DailyTasksClaim = "daily_tasks.claim";
    public const string DailyTasksChanged = "daily_tasks.changed";
    public const string QuestsState = "quests.state";
    public const string QuestsEntriesList = "quests.entries.list";
    public const string QuestsAvailableRefresh = "quests.available.refresh";
    public const string QuestsSeasonalRefresh = "quests.seasonal.refresh";
    public const string QuestsDailyRefresh = "quests.daily.refresh";
    public const string QuestsAccept = "quests.accept";
    public const string QuestsActivate = "quests.activate";
    public const string QuestsReject = "quests.reject";
    public const string QuestsCancel = "quests.cancel";
    public const string QuestsTrackerOpen = "quests.tracker.open";
    public const string QuestsFriendRequestComplete =
        "quests.friend_request.complete";
    public const string QuestsChanged = "quests.changed";
    public const string ForumsState = "forums.state";
    public const string ForumDetailsRequest = "forums.details.request";
    public const string ForumsListRequest = "forums.list.request";
    public const string ForumThreadsRequest = "forums.threads.request";
    public const string ForumMessagesRequest = "forums.messages.request";
    public const string ForumThreadRequest = "forums.thread.request";
    public const string ForumsUnreadRequest = "forums.unread.request";
    public const string ForumsListRefresh = "forums.list.refresh";
    public const string ForumThreadsRefresh = "forums.threads.refresh";
    public const string ForumMessagesRefresh = "forums.messages.refresh";
    public const string ForumDetailsRefresh = "forums.details.refresh";
    public const string ForumThreadRefresh = "forums.thread.refresh";
    public const string ForumsUnreadRefresh = "forums.unread.refresh";
    public const string ForumsPost = "forums.post";
    public const string ForumThreadModerate = "forums.thread.moderate";
    public const string ForumMessageModerate = "forums.message.moderate";
    public const string ForumSettingsUpdate = "forums.settings.update";
    public const string ForumReadMarkersUpdate = "forums.read_markers.update";
    public const string ForumThreadUpdate = "forums.thread.update";
    public const string ForumThreadReport = "forums.thread.report";
    public const string ForumMessageReport = "forums.message.report";
    public const string ForumsChanged = "forums.changed";
    public const string LeaderboardsState = "leaderboards.state";
    public const string LeaderboardsEntriesList = "leaderboards.entries.list";
    public const string LeaderboardsRefresh = "leaderboards.refresh";
    public const string LeaderboardsWeekOffsetSet = "leaderboards.week_offset.set";
    public const string LeaderboardsChanged = "leaderboards.changed";
    public const string HabbiconsState = "habbicons.state";
    public const string HabbiconCollectionsList = "habbicons.collections.list";
    public const string HabbiconEntriesList = "habbicons.entries.list";
    public const string HabbiconShopRefresh = "habbicons.shop.refresh";
    public const string HabbiconInfoRefresh = "habbicons.info.refresh";
    public const string HabbiconBuy = "habbicons.buy";
    public const string HabbiconCollectionBuy = "habbicons.collection.buy";
    public const string HabbiconClaim = "habbicons.claim";
    public const string HabbiconFavorite = "habbicons.favorite";
    public const string HabbiconUnfavorite = "habbicons.unfavorite";
    public const string HabbiconsChanged = "habbicons.changed";
    public const string InventoryState = "inventory.state";
    public const string InventoryFurniList = "inventory.furni.list";
    public const string InventoryFurniRefresh = "inventory.furni.refresh";
    public const string InventoryPetsList = "inventory.pets.list";
    public const string InventoryPetsRefresh = "inventory.pets.refresh";
    public const string InventoryAvatarEffectActivate = "inventory.avatar_effect.activate";
    public const string InventoryFurniChanged = "inventory.furni.changed";
    public const string InventoryPetsChanged = "inventory.pets.changed";
    public const string WalletState = "wallet.state";
    public const string WalletRefresh = "wallet.refresh";
    public const string WalletChanged = "wallet.changed";
    public const string PollsState = "polls.state";
    public const string PollsStart = "polls.start";
    public const string PollsContentsGet = "polls.contents.get";
    public const string PollsReject = "polls.reject";
    public const string PollsAnswer = "polls.answer";
    public const string PollsChanged = "polls.changed";
    public const string TradeState = "trade.state";
    public const string TradeOpen = "trade.open";
    public const string TradeItemsAdd = "trade.items.add";
    public const string TradeItemRemove = "trade.item.remove";
    public const string TradeAccept = "trade.accept";
    public const string TradeUnaccept = "trade.unaccept";
    public const string TradeConfirm = "trade.confirm";
    public const string TradeClose = "trade.close";
    public const string TradeNftInventoryList = "trade.nft.inventory.list";
    public const string TradeNftInventoryRefresh = "trade.nft.inventory.refresh";
    public const string TradeChanged = "trade.changed";
    public const string GroupMembershipJoin = "groups.membership.join";
    public const string GroupMembershipKick = "groups.membership.kick";
    public const string GroupMembershipApprove = "groups.membership.approve";
    public const string GroupMembershipReject = "groups.membership.reject";
    public const string FriendsList = "friends.list";
    public const string FriendsRefresh = "friends.refresh";
    public const string FriendsSearch = "friends.search";
    public const string FriendMessageHistory = "friends.message.history";
    public const string FriendMessageSend = "friends.message.send";
    public const string FriendRequestSend = "friends.request.send";
    public const string FriendRequestAccept = "friends.request.accept";
    public const string FriendRequestDecline = "friends.request.decline";
    public const string FriendRequestsDeclineAll = "friends.requests.decline_all";
    public const string FriendRequestsList = "friends.requests.list";
    public const string FriendsRemove = "friends.remove";
    public const string FriendFollow = "friends.follow";
    public const string FriendRelationshipSet = "friends.relationship.set";
    public const string FriendsChanged = "friends.changed";
    public const string FriendMessageReceived = "friends.message.received";
    public const string FriendMessageFailed = "friends.message.failed";
    public const string FriendOperationFailed = "friends.operation.failed";
    public const string FriendRequestReceived = "friends.request.received";
    public const string NavigatorState = "navigator.state";
    public const string NavigatorMetadataRefresh = "navigator.metadata.refresh";
    public const string NavigatorFlatCategoriesRefresh = "navigator.flat_categories.refresh";
    public const string NavigatorSearchView = "navigator.search.view";
    public const string NavigatorSearchText = "navigator.search.text";
    public const string NavigatorSearchMyRooms = "navigator.search.my_rooms";
    public const string NavigatorSearchMyFavourites = "navigator.search.my_favourites";
    public const string NavigatorSearchMyRoomRights = "navigator.search.my_room_rights";
    public const string NavigatorSearchMyHistory = "navigator.search.my_history";
    public const string NavigatorSearchMyFrequentHistory = "navigator.search.my_frequent_history";
    public const string NavigatorSearchMyFriendsRooms = "navigator.search.my_friends_rooms";
    public const string NavigatorSearchFriendsPresent = "navigator.search.friends_present";
    public const string NavigatorSearchMyGuildBases = "navigator.search.my_guild_bases";
    public const string NavigatorSearchPopular = "navigator.search.popular";
    public const string NavigatorSearchHighestScore = "navigator.search.highest_score";
    public const string NavigatorSearchGuildBases = "navigator.search.guild_bases";
    public const string NavigatorSavedSearchAdd = "navigator.saved_search.add";
    public const string NavigatorSavedSearchDelete = "navigator.saved_search.delete";
    public const string NavigatorCategoryCollapse = "navigator.category.collapse";
    public const string NavigatorCategoryExpand = "navigator.category.expand";
    public const string NavigatorRoomCreate = "navigator.room.create";
    public const string NavigatorRoomDelete = "navigator.room.delete";
    public const string NavigatorHomeRoomSet = "navigator.home_room.set";
    public const string NavigatorChanged = "navigator.changed";
    public const string NavigatorSearchReceived = "navigator.search.received";
    public const string MarketplaceState = "marketplace.state";
    public const string MarketplaceConfigurationRefresh = "marketplace.configuration.refresh";
    public const string MarketplaceEligibilityRefresh = "marketplace.eligibility.refresh";
    public const string MarketplaceItemStatsGet = "marketplace.item_stats.get";
    public const string MarketplaceSearch = "marketplace.search";
    public const string MarketplaceOwnOffersGet = "marketplace.own_offers.get";
    public const string MarketplaceOfferMake = "marketplace.offer.make";
    public const string MarketplaceOfferBuy = "marketplace.offer.buy";
    public const string MarketplaceOfferBuySend = "marketplace.offer.buy.send";
    public const string MarketplaceOfferCancel = "marketplace.offer.cancel";
    public const string MarketplaceOfferCancelSend = "marketplace.offer.cancel.send";
    public const string MarketplaceOffersCancelAll = "marketplace.offers.cancel_all";
    public const string MarketplaceHistoryClear = "marketplace.history.clear";
    public const string MarketplaceCreditsRedeem = "marketplace.credits.redeem";
    public const string MarketplaceTokensBuy = "marketplace.tokens.buy";
    public const string MarketplaceChanged = "marketplace.changed";
    public const string MarketplaceConfigurationChanged = "marketplace.configuration.changed";
    public const string MarketplaceEligibilityChanged = "marketplace.eligibility.changed";
    public const string MarketplaceSearchReceived = "marketplace.search.received";
    public const string MarketplaceOwnOffersReceived = "marketplace.own_offers.received";
    public const string MarketplaceItemStatsReceived = "marketplace.item_stats.received";
    public const string MarketplaceOfferMakeResult = "marketplace.offer.make_result";
    public const string MarketplaceOfferBuyResult = "marketplace.offer.buy_result";
    public const string MarketplaceOfferCancelResult = "marketplace.offer.cancel_result";
    public const string MarketplaceOffersCancelAllResult = "marketplace.offers.cancel_all_result";
    public const string MarketplaceHistoryClearResult = "marketplace.history.clear_result";
    public const string WiredState = "wired.state";
    public const string WiredConfigurationOpen = "wired.configuration.open";
    public const string WiredConfigurationGet = "wired.configuration.get";
    public const string WiredConfigurationSnapshotApply = "wired.configuration.snapshot.apply";
    public const string WiredConfigurationTriggerSave = "wired.configuration.trigger.save";
    public const string WiredConfigurationActionSave = "wired.configuration.action.save";
    public const string WiredConfigurationConditionSave = "wired.configuration.condition.save";
    public const string WiredConfigurationSelectorSave = "wired.configuration.selector.save";
    public const string WiredConfigurationAddonSave = "wired.configuration.addon.save";
    public const string WiredConfigurationVariableSave = "wired.configuration.variable.save";
    public const string WiredVariablesHashGet = "wired.variables.hash.get";
    public const string WiredVariablesDifferencesGet = "wired.variables.differences.get";
    public const string WiredVariablesList = "wired.variables.list";
    public const string WiredVariablesObjectGet = "wired.variables.object.get";
    public const string WiredVariablesHoldersGet = "wired.variables.holders.get";
    public const string WiredVariablesPermanentGet = "wired.variables.permanent.get";
    public const string WiredVariablesOwnersGet = "wired.variables.owners.get";
    public const string WiredVariablesObjectSet = "wired.variables.object.set";
    public const string WiredVariablesPermanentSet = "wired.variables.permanent.set";
    public const string WiredVariablesPermanentSetSend = "wired.variables.permanent.set.send";
    public const string WiredRoomSettingsGet = "wired.room.settings.get";
    public const string WiredRoomSettingsSet = "wired.room.settings.set";
    public const string WiredRoomStatsGet = "wired.room.stats.get";
    public const string WiredRoomLogsGet = "wired.room.logs.get";
    public const string WiredRoomErrorLogsGet = "wired.room.error_logs.get";
    public const string WiredRoomErrorLogsClear = "wired.room.error_logs.clear";
    public const string WiredRoomUserClick = "wired.room.user.click";
    public const string WiredRoomReload = "wired.room.reload";
    public const string WiredRoomRollback = "wired.room.rollback";
    public const string WiredPreferencesSet = "wired.preferences.set";
    public const string WiredChestOpen = "wired.chest.open";
    public const string WiredChestClose = "wired.chest.close";
    public const string WiredChestsLock = "wired.chests.lock";
    public const string WiredChestUpgrade = "wired.chest.upgrade";
    public const string WiredChestWithdrawAll = "wired.chest.withdraw_all";
    public const string WiredChestWithdrawCoins = "wired.chest.withdraw_coins";
    public const string WiredChestWithdrawItems = "wired.chest.withdraw_items";
    public const string WiredChestAddStart = "wired.chest.add.start";
    public const string WiredChestOptionsSet = "wired.chest.options.set";
    public const string WiredChestPreferencesSet = "wired.chest.preferences.set";
    public const string WiredChestNotificationPreferencesSet = "wired.chest.notification_preferences.set";
    public const string WiredChestDeposit = "wired.chest.deposit";
    public const string WiredTransactionChestLogsGet = "wired.transaction.chest_logs.get";
    public const string WiredTransactionRoomLogsGet = "wired.transaction.room_logs.get";
    public const string WiredTransactionDetailsGet = "wired.transaction.details.get";
    public const string WiredContractOpen = "wired.contract.open";
    public const string WiredContractOpenSend = "wired.contract.open.send";
    public const string WiredContractUpdate = "wired.contract.update";
    public const string WiredContractUpdateSend = "wired.contract.update.send";
    public const string WiredTradeItemsAdd = "wired.trade.items.add";
    public const string WiredTradeItemsRemove = "wired.trade.items.remove";
    public const string WiredTradeConfirm = "wired.trade.confirm";
    public const string WiredTradeCancel = "wired.trade.cancel";
    public const string WiredChanged = "wired.changed";
    public const string WiredPermissionsChanged = "wired.permissions.changed";
    public const string WiredEnvironmentChanged = "wired.environment.changed";
    public const string WiredClickSettingsChanged = "wired.click_settings.changed";
    public const string WiredRoomSettingsChanged = "wired.room.settings.changed";
    public const string WiredConfigurationOpened = "wired.configuration.opened";
    public const string WiredConfigurationReceived = "wired.configuration.received";
    public const string WiredConfigurationSaveResult = "wired.configuration.save_result";
    public const string WiredMenuError = "wired.menu.error";
    public const string WiredRewardResult = "wired.reward.result";
    public const string WiredChestOpened = "wired.chest.opened";
    public const string WiredChestCoinsReceived = "wired.chest.coins.received";
    public const string WiredChestItemsChunkReceived = "wired.chest.items.chunk_received";
    public const string WiredChestItemsUpdated = "wired.chest.items.updated";
    public const string WiredChestUpgradeResult = "wired.chest.upgrade_result";
    public const string WiredChestPreferencesUpdated = "wired.chest.preferences.updated";
    public const string WiredTransactionSucceeded = "wired.transaction.succeeded";
    public const string WiredTransactionFailed = "wired.transaction.failed";
    public const string WiredContractContentsReceived = "wired.contract.contents.received";
    public const string WiredContractOpened = "wired.contract.opened";
    public const string WiredContractUpdateResult = "wired.contract.update_result";
    public const string WiredTradeInitiated = "wired.trade.initiated";
    public const string WiredTradeItemsUpdated = "wired.trade.items.updated";
    public const string WiredTradeCancelled = "wired.trade.cancelled";
    public const string WiredTradeCompleted = "wired.trade.completed";
    public const string WiredTradeNotification = "wired.trade.notification";
}
