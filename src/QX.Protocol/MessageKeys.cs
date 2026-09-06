namespace Qx.Protocol;

public static class MessageKeys
{
    public static class Errors
    {
        public static readonly MessageKey Generic = new("errors.generic");
    }

    public static class Session
    {
        public static readonly MessageKey DisconnectReason = new("session.disconnect.reason");
    }

    public static class Achievements
    {
        public static readonly MessageKey Request = new("achievements.request");
        public static readonly MessageKey Snapshot = new("achievements.snapshot");
        public static readonly MessageKey Updated = new("achievement.updated");
        public static readonly MessageKey Score = new("achievement.score");
        public static readonly MessageKey PointLimitsRequest = new("achievement.point_limits.request");
        public static readonly MessageKey PointLimits = new("achievement.point_limits");
        public static readonly MessageKey Notification = new("achievement.notification");
    }

    public static class Badges
    {
        public static readonly MessageKey Request = new("badges.request");
        public static readonly MessageKey Snapshot = new("badges.snapshot");
        public static readonly MessageKey SelectedRequest = new("badges.selected.request");
        public static readonly MessageKey Received = new("badge.received");
        public static readonly MessageKey Selected = new("badge.selected");
    }

    public static class Wallet
    {
        public static readonly MessageKey CreditsRequest = new("wallet.credits.request");
        public static readonly MessageKey CreditsBalance = new("wallet.credits.balance");
        public static readonly MessageKey ActivityPoints = new("wallet.activity_points");
        public static readonly MessageKey ActivityPointUpdated = new("wallet.activity_point.updated");
    }

    public static class Earnings
    {
        public static readonly MessageKey StatusRequest = new("earnings.status.request");
        public static readonly MessageKey StatusSnapshot = new("earnings.status.snapshot");
        public static readonly MessageKey Claim = new("earnings.claim");
        public static readonly MessageKey Claimed = new("earnings.claimed");
        public static readonly MessageKey Notification = new("earnings.notification");
    }

    public static class Subscriptions
    {
        public static readonly MessageKey UserInfo = new("subscriptions.user_info");
        public static readonly MessageKey UserInfoRequest = new("subscriptions.user_info.request");
        public static readonly MessageKey KickbackInfo = new("subscriptions.kickback_info");
        public static readonly MessageKey KickbackInfoRequest = new("subscriptions.kickback_info.request");
        public static readonly MessageKey ClubOffersSnapshot = new("subscriptions.club_offers.snapshot");
        public static readonly MessageKey ClubOffersRequest = new("subscriptions.club_offers.request");
        public static readonly MessageKey BuildersClubFurniCount = new("subscriptions.builders_club.furni_count");
        public static readonly MessageKey BuildersClubFurniCountRequest = new("subscriptions.builders_club.furni_count.request");
        public static readonly MessageKey BuildersClubMembershipStatus = new("subscriptions.builders_club.membership_status");
        public static readonly MessageKey BuildersClubPlacementWarning = new("subscriptions.builders_club.placement_warning");
        public static readonly MessageKey BuildersClubFloorOfferPlace = new("subscriptions.builders_club.floor_offer.place");
        public static readonly MessageKey BuildersClubWallOfferPlace = new("subscriptions.builders_club.wall_offer.place");
    }

    public static class Crafting
    {
        public static readonly MessageKey ProductsRequest = new("crafting.products.request");
        public static readonly MessageKey ProductsSnapshot = new("crafting.products.snapshot");
        public static readonly MessageKey RecipeRequest = new("crafting.recipe.request");
        public static readonly MessageKey RecipeSnapshot = new("crafting.recipe.snapshot");
        public static readonly MessageKey Craft = new("crafting.craft");
        public static readonly MessageKey SecretCraft = new("crafting.secret_craft");
        public static readonly MessageKey AvailabilityRequest = new("crafting.availability.request");
        public static readonly MessageKey AvailabilitySnapshot = new("crafting.availability.snapshot");
        public static readonly MessageKey Result = new("crafting.result");
    }

    public static class Recycler
    {
        public static readonly MessageKey Status = new("recycler.status");
        public static readonly MessageKey Finished = new("recycler.finished");
    }

    public static class Wired
    {
        public static class State
        {
            public static readonly MessageKey Permissions = new("wired.permissions");
            public static readonly MessageKey Environment = new("wired.environment");
            public static readonly MessageKey ClickSettings = new("wired.click_settings");
            public static readonly MessageKey MenuError = new("wired.menu.error");
            public static readonly MessageKey RewardResult = new("wired.reward.result");
        }

        public static class Configuration
        {
            public static readonly MessageKey Opened = new("wired.configuration.opened");
            public static readonly MessageKey OpenRequest = new("wired.configuration.open.request");
            public static readonly MessageKey ApplySnapshot = new("wired.configuration.snapshot.apply");
            public static readonly MessageKey Trigger = new("wired.configuration.trigger");
            public static readonly MessageKey Action = new("wired.configuration.action");
            public static readonly MessageKey Condition = new("wired.configuration.condition");
            public static readonly MessageKey Selector = new("wired.configuration.selector");
            public static readonly MessageKey Addon = new("wired.configuration.addon");
            public static readonly MessageKey Variable = new("wired.configuration.variable");
            public static readonly MessageKey TriggerUpdate = new("wired.configuration.trigger.update");
            public static readonly MessageKey ActionUpdate = new("wired.configuration.action.update");
            public static readonly MessageKey ConditionUpdate = new("wired.configuration.condition.update");
            public static readonly MessageKey SelectorUpdate = new("wired.configuration.selector.update");
            public static readonly MessageKey AddonUpdate = new("wired.configuration.addon.update");
            public static readonly MessageKey VariableUpdate = new("wired.configuration.variable.update");
            public static readonly MessageKey SaveSucceeded = new("wired.configuration.save.succeeded");
            public static readonly MessageKey ValidationFailed = new("wired.configuration.validation.failed");
        }

        public static class Room
        {
            public static readonly MessageKey SettingsRequest = new("wired.room.settings.request");
            public static readonly MessageKey Settings = new("wired.room.settings");
            public static readonly MessageKey SettingsUpdate = new("wired.room.settings.update");
            public static readonly MessageKey StatsRequest = new("wired.room.stats.request");
            public static readonly MessageKey Stats = new("wired.room.stats");
            public static readonly MessageKey LogsRequest = new("wired.room.logs.request");
            public static readonly MessageKey Logs = new("wired.room.logs");
            public static readonly MessageKey Update = new("wired.room.update");
            public static readonly MessageKey PreferencesUpdate = new("wired.preferences.update");
        }

        public static class ErrorLogs
        {
            public static readonly MessageKey Request = new("wired.error_logs.request");
            public static readonly MessageKey Snapshot = new("wired.error_logs");
            public static readonly MessageKey Clear = new("wired.error_logs.clear");
        }

        public static class UserClick
        {
            public static readonly MessageKey Request = new("wired.user_click.request");
            public static readonly MessageKey Result = new("wired.user_click.result");
        }

        public static class Variables
        {
            public static readonly MessageKey HashRequest = new("wired.variables.hash.request");
            public static readonly MessageKey Hash = new("wired.variables.hash");
            public static readonly MessageKey DifferencesRequest = new("wired.variables.differences.request");
            public static readonly MessageKey Differences = new("wired.variables.differences");
            public static readonly MessageKey ObjectRequest = new("wired.variables.object.request");
            public static readonly MessageKey Object = new("wired.variables.object");
            public static readonly MessageKey HoldersRequest = new("wired.variables.holders.request");
            public static readonly MessageKey Holders = new("wired.variables.holders");
            public static readonly MessageKey PermanentRequest = new("wired.variables.permanent.request");
            public static readonly MessageKey Permanent = new("wired.variables.permanent");
            public static readonly MessageKey OwnersRequest = new("wired.variables.owners.request");
            public static readonly MessageKey Owners = new("wired.variables.owners");
            public static readonly MessageKey ObjectValueSet = new("wired.variables.object_value.set");
            public static readonly MessageKey PermanentValueSet = new("wired.variables.permanent_value.set");
            public static readonly MessageKey PermanentValueSetResult = new("wired.variables.permanent_value.set.result");
        }

        public static class Chests
        {
            public static readonly MessageKey Opened = new("wired.chest.opened");
            public static readonly MessageKey Coins = new("wired.chest.coins");
            public static readonly MessageKey ItemsChunk = new("wired.chest.items.chunk");
            public static readonly MessageKey ItemsUpdated = new("wired.chest.items.updated");
            public static readonly MessageKey UpgradeResult = new("wired.chest.upgrade.result");
            public static readonly MessageKey PreferencesUpdated = new("wired.chest.preferences.updated");
            public static readonly MessageKey OpenRequest = new("wired.chest.open.request");
            public static readonly MessageKey Close = new("wired.chest.close");
            public static readonly MessageKey LockAll = new("wired.chests.lock");
            public static readonly MessageKey Upgrade = new("wired.chest.upgrade");
            public static readonly MessageKey WithdrawAll = new("wired.chest.withdraw.all");
            public static readonly MessageKey WithdrawCoins = new("wired.chest.withdraw.coins");
            public static readonly MessageKey WithdrawItems = new("wired.chest.withdraw.items");
            public static readonly MessageKey StartAdding = new("wired.chest.add.start");
            public static readonly MessageKey OptionsUpdate = new("wired.chest.options.update");
            public static readonly MessageKey PreferencesUpdate = new("wired.chest.preferences.update");
            public static readonly MessageKey NotificationPreferencesUpdate = new("wired.chest.notification_preferences.update");
        }

        public static class Transaction
        {
            public static readonly MessageKey Succeeded = new("wired.transaction.succeeded");
            public static readonly MessageKey Failed = new("wired.transaction.failed");
            public static readonly MessageKey ChestLogsRequest = new("wired.transaction.chest_logs.request");
            public static readonly MessageKey RoomLogsRequest = new("wired.transaction.room_logs.request");
            public static readonly MessageKey Logs = new("wired.transaction.logs");
            public static readonly MessageKey LogDetailsRequest = new("wired.transaction.log_details.request");
            public static readonly MessageKey LogDetails = new("wired.transaction.log_details");
        }

        public static class Contracts
        {
            public static readonly MessageKey Contents = new("wired.contract.contents");
            public static readonly MessageKey Opened = new("wired.contract.opened");
            public static readonly MessageKey OpenRequest = new("wired.contract.open.request");
            public static readonly MessageKey Update = new("wired.contract.update");
            public static readonly MessageKey UpdateResult = new("wired.contract.update.result");
        }

        public static class Trade
        {
            public static readonly MessageKey Initiated = new("wired.trade.initiated");
            public static readonly MessageKey ItemsUpdated = new("wired.trade.items.updated");
            public static readonly MessageKey Cancelled = new("wired.trade.cancelled");
            public static readonly MessageKey Completed = new("wired.trade.completed");
            public static readonly MessageKey ItemsUpdate = new("wired.trade.items.update");
            public static readonly MessageKey Confirm = new("wired.trade.confirm");
            public static readonly MessageKey Cancel = new("wired.trade.cancel");
            public static readonly MessageKey Notification = new("wired.trade.notification");
        }
    }

    public static class Quests
    {
        public static readonly MessageKey Request = new("quests.request");
        public static readonly MessageKey Snapshot = new("quests.snapshot");
        public static readonly MessageKey SeasonalRequest = new("quests.seasonal.request");
        public static readonly MessageKey SeasonalSnapshot = new("quests.seasonal.snapshot");
        public static readonly MessageKey Updated = new("quest.updated");
        public static readonly MessageKey Completed = new("quest.completed");
        public static readonly MessageKey Cancelled = new("quest.cancelled");
        public static readonly MessageKey DailyRequest = new("quest.daily.request");
        public static readonly MessageKey Daily = new("quest.daily");
        public static readonly MessageKey Accept = new("quest.accept");
        public static readonly MessageKey Activate = new("quest.activate");
        public static readonly MessageKey Reject = new("quest.reject");
        public static readonly MessageKey Cancel = new("quest.cancel");
        public static readonly MessageKey TrackerOpen = new("quest.tracker.open");
        public static readonly MessageKey FriendRequestCompleted = new("quest.friend_request.completed");
    }

    public static class DailyTasks
    {
        public static readonly MessageKey Request = new("daily_tasks.request");
        public static readonly MessageKey Snapshot = new("daily_tasks.snapshot");
        public static readonly MessageKey Added = new("daily_tasks.added");
        public static readonly MessageKey Updated = new("daily_task.updated");
        public static readonly MessageKey Claim = new("daily_task.claim");
    }

    public static class Habbicons
    {
        public static readonly MessageKey ShopRequest = new("habbicons.shop.request");
        public static readonly MessageKey ShopSnapshot = new("habbicons.shop.snapshot");
        public static readonly MessageKey InventorySnapshot = new("habbicons.inventory.snapshot");
        public static readonly MessageKey StatusUpdated = new("habbicon.status.updated");
        public static readonly MessageKey InfoRequest = new("habbicon.info.request");
        public static readonly MessageKey InfoSnapshot = new("habbicon.info.snapshot");
        public static readonly MessageKey RoomUsed = new("habbicon.room.used");
        public static readonly MessageKey Buy = new("habbicon.buy");
        public static readonly MessageKey BuyCollection = new("habbicon.collection.buy");
        public static readonly MessageKey Claim = new("habbicon.claim");
        public static readonly MessageKey Favorite = new("habbicon.favorite");
        public static readonly MessageKey Unfavorite = new("habbicon.unfavorite");
    }

    public static class Leaderboards
    {
        public static class Total
        {
            public static readonly MessageKey Request = new("leaderboards.total.request");
            public static readonly MessageKey Snapshot = new("leaderboards.total.snapshot");
        }

        public static class Friends
        {
            public static readonly MessageKey Request = new("leaderboards.friends.request");
            public static readonly MessageKey Snapshot = new("leaderboards.friends.snapshot");
        }

        public static class Groups
        {
            public static readonly MessageKey Request = new("leaderboards.groups.request");
            public static readonly MessageKey Snapshot = new("leaderboards.groups.snapshot");
        }

        public static class WeeklyTotal
        {
            public static readonly MessageKey Request = new("leaderboards.weekly.total.request");
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.total.snapshot");
        }

        public static class WeeklyFriends
        {
            public static readonly MessageKey Request = new("leaderboards.weekly.friends.request");
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.friends.snapshot");
        }

        public static class WeeklyGroups
        {
            public static readonly MessageKey Request = new("leaderboards.weekly.groups.request");
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.groups.snapshot");
        }
    }

    public static class Inventory
    {
        public static class AvatarEffects
        {
            public static readonly MessageKey ActivationRequest =
                new("inventory.avatar_effect.activation.request");
        }

        public static class Furni
        {
            public static readonly MessageKey Request = new("inventory.furni.request");
            public static readonly MessageKey Snapshot = new("inventory.furni.snapshot");
            public static readonly MessageKey AddedOrUpdated = new("inventory.furni.added_or_updated");
            public static readonly MessageKey Removed = new("inventory.furni.removed");
            public static readonly MessageKey RemovedMultiple = new("inventory.furni.removed_multiple");
            public static readonly MessageKey Invalidated = new("inventory.furni.invalidated");
            public static readonly MessageKey PostItPlaced = new("inventory.furni.post_it_placed");
        }

        public static class Pets
        {
            public static readonly MessageKey Request = new("inventory.pets.request");
            public static readonly MessageKey Snapshot = new("inventory.pets.snapshot");
            public static readonly MessageKey Added = new("inventory.pets.added");
            public static readonly MessageKey Removed = new("inventory.pets.removed");
        }
    }

    public static class Wardrobe
    {
        public static readonly MessageKey Request = new("wardrobe.request");
        public static readonly MessageKey Snapshot = new("wardrobe.snapshot");
        public static readonly MessageKey FigureUpdate = new("wardrobe.figure.update");
        public static readonly MessageKey OutfitSave = new("wardrobe.outfit.save");
    }

    public static class Catalog
    {
        public static readonly MessageKey IndexRequest = new("catalog.index.request");
        public static readonly MessageKey IndexSnapshot = new("catalog.index.snapshot");
        public static readonly MessageKey PageRequest = new("catalog.page.request");
        public static readonly MessageKey PageSnapshot = new("catalog.page.snapshot");
        public static readonly MessageKey Purchase = new("catalog.purchase");
        public static readonly MessageKey PurchaseAccepted = new("catalog.purchase.accepted");
        public static readonly MessageKey PurchaseFailed = new("catalog.purchase.failed");
        public static readonly MessageKey PurchaseForbidden = new("catalog.purchase.forbidden");
        public static readonly MessageKey Published = new("catalog.published");
        public static readonly MessageKey RoomAdInfoRequest = new("catalog.room_ad.info.request");
        public static readonly MessageKey RoomAdInfo = new("catalog.room_ad.info");
    }

    public static class Marketplace
    {
        public static class Configuration
        {
            public static readonly MessageKey Request = new("marketplace.configuration.request");
            public static readonly MessageKey Snapshot = new("marketplace.configuration");
        }

        public static class Eligibility
        {
            public static readonly MessageKey Request = new("marketplace.eligibility.request");
            public static readonly MessageKey Result = new("marketplace.eligibility.result");
        }

        public static class Credits
        {
            public static readonly MessageKey Redeem = new("marketplace.credits.redeem");
        }

        public static class Tokens
        {
            public static readonly MessageKey Buy = new("marketplace.tokens.buy");
        }

        public static class Offers
        {
            public static readonly MessageKey SearchRequest = new("marketplace.offers.search.request");
            public static readonly MessageKey SearchResult = new("marketplace.offers.search.result");
            public static readonly MessageKey OwnRequest = new("marketplace.offers.own.request");
            public static readonly MessageKey OwnSnapshot = new("marketplace.offers.own.snapshot");
            public static readonly MessageKey Make = new("marketplace.offer.make");
            public static readonly MessageKey MakeResult = new("marketplace.offer.make.result");
            public static readonly MessageKey Buy = new("marketplace.offer.buy");
            public static readonly MessageKey BuyResult = new("marketplace.offer.buy.result");
            public static readonly MessageKey Cancel = new("marketplace.offer.cancel");
            public static readonly MessageKey CancelResult = new("marketplace.offer.cancel.result");
            public static readonly MessageKey CancelAll = new("marketplace.offers.cancel_all");
            public static readonly MessageKey CancelAllResult = new("marketplace.offers.cancel_all.result");
            public static readonly MessageKey ClearOwnHistory = new("marketplace.offers.own_history.clear");
            public static readonly MessageKey ClearOwnHistoryResult = new("marketplace.offers.own_history.clear.result");
        }

        public static class ItemStats
        {
            public static readonly MessageKey Request = new("marketplace.item_stats.request");
            public static readonly MessageKey Snapshot = new("marketplace.item_stats");
        }
    }

    public static class Friends
    {
        public static readonly MessageKey InitializeRequest = new("friends.initialize.request");
        public static readonly MessageKey Initialized = new("friends.initialized");
        public static readonly MessageKey ListFragment = new("friends.list.fragment");
        public static readonly MessageKey ListUpdated = new("friends.list.updated");
        public static readonly MessageKey PrivateMessageSend = new("friends.private_message.send");
        public static readonly MessageKey PrivateMessageReceived = new("friends.private_message.received");
        public static readonly MessageKey OperationFailed = new("friends.operation.failed");
        public static readonly MessageKey PrivateMessageFailed = new("friends.private_message.failed");
        public static readonly MessageKey FriendRequestSend = new("friends.request.send");
        public static readonly MessageKey FriendRequestReceived = new("friends.request.received");
        public static readonly MessageKey FriendRequestsRequest = new("friends.requests.request");
        public static readonly MessageKey FriendRequestsSnapshot = new("friends.requests.snapshot");
        public static readonly MessageKey FriendRequestAccept = new("friends.request.accept");
        public static readonly MessageKey FriendRequestDecline = new("friends.request.decline");
        public static readonly MessageKey Remove = new("friends.remove");
        public static readonly MessageKey Follow = new("friends.follow");
        public static readonly MessageKey SearchRequest = new("friends.search.request");
        public static readonly MessageKey SearchResult = new("friends.search.result");
        public static readonly MessageKey RelationshipSet = new("friends.relationship.set");
        public static readonly MessageKey RoomInvite = new("friends.room_invite.received");
    }

    public static class Groups
    {
        public static class Details
        {
            public static readonly MessageKey Request = new("groups.details.request");
            public static readonly MessageKey Snapshot = new("groups.details.snapshot");
        }

        public static class Membership
        {
            public static readonly MessageKey Join = new("groups.membership.join");
            public static readonly MessageKey Kick = new("groups.membership.kick");
            public static readonly MessageKey Approve = new("groups.membership.approve");
            public static readonly MessageKey Reject = new("groups.membership.reject");
        }

        public static class Members
        {
            public static readonly MessageKey Request = new("groups.members.request");
            public static readonly MessageKey Snapshot = new("groups.members.snapshot");
        }

        public static class Memberships
        {
            public static readonly MessageKey Request = new("groups.memberships.request");
            public static readonly MessageKey Snapshot = new("groups.memberships.snapshot");
        }
    }

    public static class Navigator
    {
        public static class State
        {
            public static readonly MessageKey MetadataRequest = new("navigator.metadata.request");
            public static readonly MessageKey Metadata = new("navigator.metadata");
            public static readonly MessageKey FlatCategoriesRequest = new("navigator.flat_categories.request");
            public static readonly MessageKey FlatCategories = new("navigator.flat_categories");
            public static readonly MessageKey LiftedRooms = new("navigator.lifted_rooms");
            public static readonly MessageKey Settings = new("navigator.settings");
            public static readonly MessageKey Preferences = new("navigator.preferences");
        }

        public static class Search
        {
            public static readonly MessageKey Result = new("navigator.search.result");
            public static readonly MessageKey LegacyResult = new("navigator.search.legacy_result");
            public static readonly MessageKey View = new("navigator.search.view.request");
            public static readonly MessageKey MyRooms = new("navigator.search.my_rooms.request");
            public static readonly MessageKey MyFavouriteRooms = new("navigator.search.my_favourite_rooms.request");
            public static readonly MessageKey MyRoomRights = new("navigator.search.my_room_rights.request");
            public static readonly MessageKey MyRoomHistory = new("navigator.search.my_room_history.request");
            public static readonly MessageKey MyFrequentRoomHistory = new("navigator.search.my_frequent_room_history.request");
            public static readonly MessageKey MyFriendsRooms = new("navigator.search.my_friends_rooms.request");
            public static readonly MessageKey RoomsWhereFriendsAre = new("navigator.search.rooms_where_friends_are.request");
            public static readonly MessageKey MyGuildBases = new("navigator.search.my_guild_bases.request");
            public static readonly MessageKey Text = new("navigator.search.text.request");
            public static readonly MessageKey Popular = new("navigator.search.popular.request");
            public static readonly MessageKey HighestScoring = new("navigator.search.highest_scoring.request");
            public static readonly MessageKey GuildBases = new("navigator.search.guild_bases.request");
        }

        public static class Personalization
        {
            public static readonly MessageKey SavedSearches = new("navigator.saved_searches");
            public static readonly MessageKey SavedSearchAdd = new("navigator.saved_search.add");
            public static readonly MessageKey SavedSearchDelete = new("navigator.saved_search.delete");
            public static readonly MessageKey CollapsedCategories = new("navigator.collapsed_categories");
            public static readonly MessageKey CollapsedCategoryAdd = new("navigator.collapsed_category.add");
            public static readonly MessageKey CollapsedCategoryRemove = new("navigator.collapsed_category.remove");
        }

        public static readonly MessageKey HomeRoomUpdate = new("navigator.home_room.update");
        public static readonly MessageKey RoomCreate = new("navigator.room.create");
        public static readonly MessageKey RoomDelete = new("navigator.room.delete");
    }

    public static class Users
    {
        public static class Relationship
        {
            public static readonly MessageKey Request = new("users.relationship.request");
            public static readonly MessageKey Snapshot = new("users.relationship.snapshot");
        }

        public static class Block
        {
            public static readonly MessageKey ListRequest = new("users.block.list.request");
            public static readonly MessageKey ListSnapshot = new("users.block.list.snapshot");
            public static readonly MessageKey Updated = new("users.block.updated");
            public static readonly MessageKey Add = new("users.block.add");
            public static readonly MessageKey Remove = new("users.block.remove");
        }

        public static class Ignore
        {
            public static readonly MessageKey ListRequest = new("users.ignore.list.request");
            public static readonly MessageKey ListSnapshot = new("users.ignore.list.snapshot");
            public static readonly MessageKey Updated = new("users.ignore.updated");
            public static readonly MessageKey AddByIdRequest = new("users.ignore.add_by_id.request");
            public static readonly MessageKey AddByNameRequest = new("users.ignore.add_by_name.request");
            public static readonly MessageKey Remove = new("users.ignore.remove");
        }

        public static class FigureSets
        {
            public static readonly MessageKey Added = new("users.figure_sets.added");
            public static readonly MessageKey Removed = new("users.figure_sets.removed");
            public static readonly MessageKey Snapshot = new("users.figure_sets.snapshot");
        }

        public static class Sanctions
        {
            public static readonly MessageKey Request = new("users.sanctions.request");
            public static readonly MessageKey Snapshot = new("users.sanctions.snapshot");
        }

        public static class FavoriteGroup
        {
            public static readonly MessageKey Select = new("users.favorite_group.select");
            public static readonly MessageKey Deselect = new("users.favorite_group.deselect");
        }

        public static readonly MessageKey MottoUpdate = new("users.motto.update");
        public static readonly MessageKey ProfileRequest = new("users.profile.request");
        public static readonly MessageKey ProfileSnapshot = new("users.profile.snapshot");
        public static readonly MessageKey FigureUpdated = new("users.figure.updated");
        public static readonly MessageKey NameChangeResult = new("users.name_change.result");
        public static readonly MessageKey SafetyLockChanged = new("users.safety_lock.changed");
        public static readonly MessageKey ExtendedProfileRequest = new("users.profile.extended.request");
        public static readonly MessageKey ExtendedProfileSnapshot = new("users.profile.extended.snapshot");
    }

    public static class Notifications
    {
        public static readonly MessageKey Dialog = new("notifications.dialog");
        public static readonly MessageKey MessageOfTheDay = new("notifications.message_of_the_day");
    }

    public static class Forums
    {
        public static readonly MessageKey Stats = new("forums.stats");
        public static readonly MessageKey List = new("forums.list");
        public static readonly MessageKey Threads = new("forums.threads");
        public static readonly MessageKey Messages = new("forums.messages");
        public static readonly MessageKey ThreadCreated = new("forums.thread.created");
        public static readonly MessageKey MessageCreated = new("forums.message.created");
        public static readonly MessageKey ThreadUpdated = new("forums.thread.updated");
        public static readonly MessageKey MessageUpdated = new("forums.message.updated");
        public static readonly MessageKey UnreadCount = new("forums.unread_count");
        public static readonly MessageKey StatsRequest = new("forums.stats.request");
        public static readonly MessageKey ListRequest = new("forums.list.request");
        public static readonly MessageKey ThreadsRequest = new("forums.threads.request");
        public static readonly MessageKey MessagesRequest = new("forums.messages.request");
        public static readonly MessageKey ThreadRequest = new("forums.thread.request");
        public static readonly MessageKey UnreadCountRequest = new("forums.unread_count.request");
        public static readonly MessageKey Post = new("forums.post");
        public static readonly MessageKey ThreadModerate = new("forums.thread.moderate");
        public static readonly MessageKey MessageModerate = new("forums.message.moderate");
        public static readonly MessageKey SettingsUpdate = new("forums.settings.update");
        public static readonly MessageKey ReadMarkersUpdate = new("forums.read_markers.update");
        public static readonly MessageKey ThreadUpdate = new("forums.thread.update");
        public static readonly MessageKey ThreadReport = new("forums.thread.report");
        public static readonly MessageKey MessageReport = new("forums.message.report");
    }

    public static class Polls
    {
        public static readonly MessageKey Contents = new("polls.contents");
        public static readonly MessageKey Error = new("polls.error");
        public static readonly MessageKey Offer = new("polls.offer");
        public static readonly MessageKey Answer = new("polls.answer");
        public static readonly MessageKey Reject = new("polls.reject");
        public static readonly MessageKey Start = new("polls.start");
    }

    public static class Gifts
    {
        public static readonly MessageKey WrappingConfiguration = new("gifts.wrapping.configuration");
        public static readonly MessageKey PresentOpened = new("gifts.present.opened");
        public static readonly MessageKey ClubInfo = new("gifts.club.info");
        public static readonly MessageKey ClubSelected = new("gifts.club.selected");
        public static readonly MessageKey ReceiverNotFound = new("gifts.receiver.not_found");
        public static readonly MessageKey ClubNotification = new("gifts.club.notification");
        public static readonly MessageKey OfferGiftability = new("gifts.offer.giftability");
        public static readonly MessageKey NewUserOffer = new("gifts.new_user.offer");
        public static readonly MessageKey NewUserIncomplete = new("gifts.new_user.incomplete");
        public static readonly MessageKey WrappingConfigurationRequest = new("gifts.wrapping.configuration.request");
        public static readonly MessageKey PresentOpen = new("gifts.present.open");
        public static readonly MessageKey Purchase = new("gifts.purchase");
        public static readonly MessageKey ClubInfoRequest = new("gifts.club.info.request");
        public static readonly MessageKey ClubSelect = new("gifts.club.select");
        public static readonly MessageKey OfferGiftabilityRequest = new("gifts.offer.giftability.request");
        public static readonly MessageKey NewUserSelect = new("gifts.new_user.select");
        public static readonly MessageKey NewUserAdvance = new("gifts.new_user.advance");
    }

    public static class Trade
    {
        public static readonly MessageKey Opened = new("trade.opened");
        public static readonly MessageKey Offers = new("trade.offers");
        public static readonly MessageKey AcceptanceUpdated = new("trade.acceptance.updated");
        public static readonly MessageKey Confirmation = new("trade.confirmation");
        public static readonly MessageKey Completed = new("trade.completed");
        public static readonly MessageKey Closed = new("trade.closed");
        public static readonly MessageKey OpenFailed = new("trade.open.failed");
        public static readonly MessageKey NftOffers = new("trade.nft.offers");
        public static readonly MessageKey NftInventory = new("trade.nft.inventory");
        public static readonly MessageKey SilverUpdated = new("trade.silver.updated");
        public static readonly MessageKey SilverFee = new("trade.silver.fee");
        public static readonly MessageKey OpenRequest = new("trade.open.request");
        public static readonly MessageKey ItemsAdd = new("trade.items.add");
        public static readonly MessageKey ItemRemove = new("trade.item.remove");
        public static readonly MessageKey Accept = new("trade.accept");
        public static readonly MessageKey Unaccept = new("trade.unaccept");
        public static readonly MessageKey Confirm = new("trade.confirm");
        public static readonly MessageKey Close = new("trade.close");
        public static readonly MessageKey NftInventoryRequest = new("trade.nft.inventory.request");
    }

    public static class Room
    {
        public static readonly MessageKey Objects = new("room.objects");
        public static readonly MessageKey WallItems = new("room.wall_items");
        public static readonly MessageKey SnapshotRequest = new("room.snapshot.request");
        public static readonly MessageKey Snapshot = new("room.snapshot");
        public static readonly MessageKey Advertisement = new("room.advertisement");
        public static readonly MessageKey StaffPickUpdateRequest =
            new("room.staff_pick.update.request");
        public static readonly MessageKey RatingRequest = new("room.rating.request");

        public static class Settings
        {
            public static readonly MessageKey Request = new("room.settings.request");
            public static readonly MessageKey Snapshot = new("room.settings.snapshot");
            public static readonly MessageKey RequestFailed = new("room.settings.request.failed");
            public static readonly MessageKey Save = new("room.settings.save");
            public static readonly MessageKey SaveSucceeded = new("room.settings.save.succeeded");
            public static readonly MessageKey SaveFailed = new("room.settings.save.failed");
        }

        public static class Access
        {
            public static readonly MessageKey OpenRequest = new("room.access.open.request");
            public static readonly MessageKey OpenConfirmed = new("room.access.open.confirmed");
            public static readonly MessageKey Doorbell = new("room.access.doorbell");
            public static readonly MessageKey DoorbellAnswer = new("room.access.doorbell.answer");
            public static readonly MessageKey QueueStatus = new("room.access.queue.status");
            public static readonly MessageKey Granted = new("room.access.granted");
            public static readonly MessageKey Denied = new("room.access.denied");
            public static readonly MessageKey NotFound = new("room.access.not_found");
            public static readonly MessageKey ConnectionFailed = new("room.access.connection_failed");
        }

        public static class Lifecycle
        {
            public static readonly MessageKey Ready = new("room.lifecycle.ready");
            public static readonly MessageKey Entry = new("room.lifecycle.entry");
            public static readonly MessageKey Forward = new("room.lifecycle.forward");
            public static readonly MessageKey ConnectionClosed = new("room.lifecycle.connection_closed");
            public static readonly MessageKey NativeExit = new("room.lifecycle.native_exit");
            public static readonly MessageKey Quit = new("room.lifecycle.quit");
        }

        public static class Environment
        {
            public static readonly MessageKey EntryTile = new("room.environment.entry_tile");
            public static readonly MessageKey Property = new("room.environment.property");
            public static readonly MessageKey Visualization = new("room.environment.visualization");
            public static readonly MessageKey ChatSettings = new("room.environment.chat_settings");
            public static readonly MessageKey FloorPlan = new("room.environment.floor_plan");
        }

        public static class Chat
        {
            public static readonly MessageKey Talk = new("room.chat.talk");
            public static readonly MessageKey Shout = new("room.chat.shout");
            public static readonly MessageKey Whisper = new("room.chat.whisper");
            public static readonly MessageKey WhisperSend = new("room.chat.whisper.send");
            public static readonly MessageKey SpecialSystem = new("room.chat.special_system");
            public static readonly MessageKey TalkSend = new("room.chat.talk.send");
            public static readonly MessageKey ShoutSend = new("room.chat.shout.send");
        }

        public static class Authority
        {
            public static readonly MessageKey ControllersRequest = new("room.authority.controllers.request");
            public static readonly MessageKey ControllersSnapshot = new("room.authority.controllers.snapshot");
            public static readonly MessageKey ControllerGrantRequest = new("room.authority.controller.grant.request");
            public static readonly MessageKey ControllerGranted = new("room.authority.controller.granted");
            public static readonly MessageKey ControllerRevoked = new("room.authority.controller.revoked");
            public static readonly MessageKey Owner = new("room.authority.owner");
            public static readonly MessageKey SpectatorGranted = new("room.authority.spectator.granted");
            public static readonly MessageKey SpectatorRevoked = new("room.authority.spectator.revoked");
            public static readonly MessageKey SpectatingEnded = new("room.authority.spectating.ended");
        }

        public static class Occupants
        {
            public static readonly MessageKey Snapshot = new("room.occupants.snapshot");
            public static readonly MessageKey Removed = new("room.occupants.removed");
            public static readonly MessageKey Status = new("room.occupants.status");
            public static readonly MessageKey Respect = new("room.occupants.respect");
            public static readonly MessageKey RespectRequest = new("room.occupants.respect.request");

            public static class Action
            {
                public static readonly MessageKey Dance = new("room.occupants.action.dance");
                public static readonly MessageKey DanceRequest = new("room.occupants.action.dance.request");
                public static readonly MessageKey SignRequest = new("room.occupants.action.sign.request");
                public static readonly MessageKey Effect = new("room.occupants.action.effect");
                public static readonly MessageKey EffectSelectionRequest = new("room.occupants.action.effect.selection.request");
                public static readonly MessageKey PostureRequest = new("room.occupants.action.posture.request");
                public static readonly MessageKey Carry = new("room.occupants.action.carry");
                public static readonly MessageKey Sleep = new("room.occupants.action.sleep");
                public static readonly MessageKey Typing = new("room.occupants.action.typing");
                public static readonly MessageKey Expression = new("room.occupants.action.expression");
                public static readonly MessageKey ExpressionRequest = new("room.occupants.action.expression.request");
            }

            public static class Identity
            {
                public static readonly MessageKey Appearance = new("room.occupants.identity.appearance");
                public static readonly MessageKey Name = new("room.occupants.identity.name");
                public static readonly MessageKey FavoriteGroup = new("room.occupants.identity.favorite_group");
            }

            public static class Pet
            {
                public static readonly MessageKey InfoRequest = new("room.occupants.pet.info.request");
                public static readonly MessageKey Info = new("room.occupants.pet.info");
                public static readonly MessageKey Figure = new("room.occupants.pet.figure");
                public static readonly MessageKey Status = new("room.occupants.pet.status");
                public static readonly MessageKey Level = new("room.occupants.pet.level");
                public static readonly MessageKey Respect = new("room.occupants.pet.respect");
                public static readonly MessageKey RespectRequest = new("room.occupants.pet.respect.request");
                public static readonly MessageKey MountRequest = new("room.occupants.pet.mount.request");
                public static readonly MessageKey RemoveRequest = new("room.occupants.pet.remove.request");
            }

            public static class Bot
            {
                public static readonly MessageKey RemoveRequest = new("room.occupants.bot.remove.request");
            }
        }

        public static class HandItem
        {
            public static readonly MessageKey Received = new("room.hand_item.received");
            public static readonly MessageKey Drop = new("room.hand_item.drop");
            public static readonly MessageKey Pass = new("room.hand_item.pass");
        }

        public static class FloorItem
        {
            public static readonly MessageKey Use = new("room.floor_item.use");
            public static readonly MessageKey Move = new("room.floor_item.move");
            public static readonly MessageKey ThrowDice = new("room.floor_item.dice.throw");
            public static readonly MessageKey DiceOff = new("room.floor_item.dice.off");
            public static readonly MessageKey DiceValue = new("room.floor_item.dice_value");
            public static readonly MessageKey OneWayDoorStatus = new("room.floor_item.one_way_door_status");
            public static readonly MessageKey OneWayDoorEnter = new("room.floor_item.one_way_door.enter");
            public static readonly MessageKey Added = new("room.floor_item.added");
            public static readonly MessageKey Removed = new("room.floor_item.removed");
            public static readonly MessageKey RemovedMultiple = new("room.floor_item.removed_multiple");
            public static readonly MessageKey Updated = new("room.floor_item.updated");
            public static readonly MessageKey DataUpdated = new("room.floor_item.data_updated");
            public static readonly MessageKey DataBatchUpdated = new("room.floor_item.data_batch_updated");
        }

        public static class WallItem
        {
            public static readonly MessageKey Use = new("room.wall_item.use");
            public static readonly MessageKey Move = new("room.wall_item.move");
            public static readonly MessageKey Remove = new("room.wall_item.remove");
            public static readonly MessageKey StickyDataSet = new("room.wall_item.sticky_data.set");
            public static readonly MessageKey StickyDataRequest = new("room.wall_item.sticky_data.request");
            public static readonly MessageKey StickyData = new("room.wall_item.sticky_data");
            public static readonly MessageKey PostItPlace = new("room.wall_item.post_it.place");
            public static readonly MessageKey SpamPostItAdd = new("room.wall_item.spam_post_it.add");
            public static readonly MessageKey Added = new("room.wall_item.added");
            public static readonly MessageKey Removed = new("room.wall_item.removed");
            public static readonly MessageKey RemovedMultiple = new("room.wall_item.removed_multiple");
            public static readonly MessageKey Updated = new("room.wall_item.updated");
            public static readonly MessageKey DataUpdated = new("room.wall_item.data_updated");
            public static readonly MessageKey DataBatchUpdated = new("room.wall_item.data_batch_updated");
        }

        public static class Heightmap
        {
            public static readonly MessageKey Snapshot = new("room.heightmap.snapshot");
            public static readonly MessageKey Diff = new("room.heightmap.diff");
        }

        public static class Movement
        {
            public static readonly MessageKey Walk = new("room.movement.walk");
            public static readonly MessageKey LookTo = new("room.movement.look_to");
            public static readonly MessageKey Slide = new("room.movement.slide");
            public static readonly MessageKey Wired = new("room.movement.wired");
        }

        public static class Typing
        {
            public static readonly MessageKey Start = new("room.typing.start");
            public static readonly MessageKey Cancel = new("room.typing.cancel");
        }

        public static class Item
        {
            public static readonly MessageKey Place = new("room.item.place");
            public static readonly MessageKey Pickup = new("room.item.pickup");
            public static readonly MessageKey PickupConfirmation = new("room.item.pickup.confirmation");
        }

        public static class Moderation
        {
            public static readonly MessageKey BansRequest = new("room.moderation.bans.request");
            public static readonly MessageKey BansSnapshot = new("room.moderation.bans.snapshot");
            public static readonly MessageKey UserUnbanned = new("room.moderation.user.unbanned");
            public static readonly MessageKey Mute = new("room.moderation.mute");
            public static readonly MessageKey Kick = new("room.moderation.kick");
            public static readonly MessageKey Ban = new("room.moderation.ban");
            public static readonly MessageKey Unban = new("room.moderation.unban");
        }
    }
}
