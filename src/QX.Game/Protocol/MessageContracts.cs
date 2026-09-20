using Qx.Messages;
using Qx.Model;
using Qx.Model.Forums;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Protocol;

public static class MessageContracts
{
    public static IReadOnlyList<IMessageContract> All { get; } =
    [
        Errors.Generic,
        Session.DisconnectReason,
        Achievements.Request,
        Achievements.Snapshot,
        Achievements.Updated,
        Achievements.Score,
        Achievements.PointLimitsRequest,
        Achievements.PointLimits,
        Achievements.Notification,
        Badges.Request,
        Badges.Snapshot,
        Badges.SelectedRequest,
        Badges.Received,
        Badges.Selected,
        Wallet.CreditsRequest,
        Wallet.CreditsBalance,
        Wallet.ActivityPoints,
        Wallet.ActivityPointUpdated,
        Earnings.StatusRequest,
        Earnings.StatusSnapshot,
        Earnings.Claim,
        Earnings.Claimed,
        Earnings.Notification,
        DailyTasks.Request,
        DailyTasks.Snapshot,
        DailyTasks.Added,
        DailyTasks.Updated,
        DailyTasks.Claim,
        Quests.Request,
        Quests.Snapshot,
        Quests.SeasonalRequest,
        Quests.SeasonalSnapshot,
        Quests.Updated,
        Quests.Completed,
        Quests.Cancelled,
        Quests.DailyRequest,
        Quests.Daily,
        Quests.Accept,
        Quests.Activate,
        Quests.Reject,
        Quests.Cancel,
        Quests.TrackerOpen,
        Quests.FriendRequestCompleted,
        Habbicons.ShopRequest,
        Habbicons.ShopSnapshot,
        Habbicons.InventorySnapshot,
        Habbicons.StatusUpdated,
        Habbicons.InfoRequest,
        Habbicons.InfoSnapshot,
        Habbicons.RoomUsed,
        Habbicons.Buy,
        Habbicons.BuyCollection,
        Habbicons.Claim,
        Habbicons.Favorite,
        Habbicons.Unfavorite,
        Leaderboards.TotalRequest,
        Leaderboards.TotalSnapshot,
        Leaderboards.FriendsRequest,
        Leaderboards.FriendsSnapshot,
        Leaderboards.GroupsRequest,
        Leaderboards.GroupsSnapshot,
        Leaderboards.WeeklyTotalRequest,
        Leaderboards.WeeklyTotalSnapshot,
        Leaderboards.WeeklyFriendsRequest,
        Leaderboards.WeeklyFriendsSnapshot,
        Leaderboards.WeeklyGroupsRequest,
        Leaderboards.WeeklyGroupsSnapshot,
        Forums.Stats,
        Forums.List,
        Forums.Threads,
        Forums.Messages,
        Forums.ThreadCreated,
        Forums.MessageCreated,
        Forums.ThreadUpdated,
        Forums.MessageUpdated,
        Forums.UnreadCount,
        Forums.StatsRequest,
        Forums.ListRequest,
        Forums.ThreadsRequest,
        Forums.MessagesRequest,
        Forums.ThreadRequest,
        Forums.UnreadCountRequest,
        Forums.Post,
        Forums.ThreadModerate,
        Forums.MessageModerate,
        Forums.SettingsUpdate,
        Forums.ReadMarkersUpdate,
        Forums.ThreadUpdate,
        Forums.ThreadReport,
        Forums.MessageReport,
        Catalog.IndexRequest,
        Catalog.IndexSnapshot,
        Catalog.PageRequest,
        Catalog.PageSnapshot,
        Catalog.Purchase,
        Catalog.Accepted,
        Catalog.Failed,
        Catalog.Forbidden,
        Catalog.Published,
        Catalog.RoomAdInfoRequest,
        Catalog.RoomAdInfo,
        Gifts.WrappingConfiguration,
        Gifts.PresentOpened,
        Gifts.ClubInfo,
        Gifts.ClubSelected,
        Gifts.ReceiverNotFound,
        Gifts.ClubNotification,
        Gifts.OfferGiftability,
        Gifts.NewUserOffer,
        Gifts.NewUserIncomplete,
        Gifts.WrappingConfigurationRequest,
        Gifts.PresentOpen,
        Gifts.Purchase,
        Gifts.ClubInfoRequest,
        Gifts.ClubSelect,
        Gifts.OfferGiftabilityRequest,
        Gifts.NewUserSelect,
        Gifts.NewUserAdvance,
        Subscriptions.UserInfo,
        Subscriptions.UserInfoRequest,
        Subscriptions.KickbackInfo,
        Subscriptions.KickbackInfoRequest,
        Subscriptions.ClubOffersSnapshot,
        Subscriptions.ClubOffersRequest,
        Subscriptions.BuildersClubFurniCount,
        Subscriptions.BuildersClubFurniCountRequest,
        Subscriptions.BuildersClubMembershipStatus,
        Subscriptions.BuildersClubPlacementWarning,
        Subscriptions.BuildersClubFloorOfferPlace,
        Subscriptions.BuildersClubWallOfferPlace,
        Crafting.ProductsRequest,
        Crafting.ProductsSnapshot,
        Crafting.RecipeRequest,
        Crafting.RecipeSnapshot,
        Crafting.Craft,
        Crafting.SecretCraft,
        Crafting.AvailabilityRequest,
        Crafting.AvailabilitySnapshot,
        Crafting.Result,
        Recycler.Status,
        Recycler.Finished,
        Wired.State.Permissions,
        Wired.State.Environment,
        Wired.State.ClickSettings,
        Wired.State.MenuError,
        Wired.State.RewardResult,
        Wired.Configuration.Opened,
        Wired.Configuration.OpenRequest,
        Wired.Configuration.ApplySnapshot,
        Wired.Configuration.Trigger,
        Wired.Configuration.Action,
        Wired.Configuration.Condition,
        Wired.Configuration.Selector,
        Wired.Configuration.Addon,
        Wired.Configuration.Variable,
        Wired.Configuration.TriggerUpdate,
        Wired.Configuration.ActionUpdate,
        Wired.Configuration.ConditionUpdate,
        Wired.Configuration.SelectorUpdate,
        Wired.Configuration.AddonUpdate,
        Wired.Configuration.VariableUpdate,
        Wired.Configuration.SaveSucceeded,
        Wired.Configuration.ValidationFailed,
        Wired.Room.SettingsRequest,
        Wired.Room.Settings,
        Wired.Room.SettingsUpdate,
        Wired.Room.StatsRequest,
        Wired.Room.Stats,
        Wired.Room.LogsRequest,
        Wired.Room.Logs,
        Wired.Room.Update,
        Wired.Room.PreferencesUpdate,
        Wired.ErrorLogs.Request,
        Wired.ErrorLogs.Snapshot,
        Wired.ErrorLogs.Clear,
        Wired.UserClick.Request,
        Wired.UserClick.Result,
        Wired.Variables.HashRequest,
        Wired.Variables.Hash,
        Wired.Variables.DifferencesRequest,
        Wired.Variables.Differences,
        Wired.Variables.ObjectRequest,
        Wired.Variables.Object,
        Wired.Variables.HoldersRequest,
        Wired.Variables.Holders,
        Wired.Variables.PermanentRequest,
        Wired.Variables.Permanent,
        Wired.Variables.OwnersRequest,
        Wired.Variables.Owners,
        Wired.Variables.ObjectValueSet,
        Wired.Variables.PermanentValueSet,
        Wired.Variables.PermanentValueSetResult,
        Wired.Chests.Opened,
        Wired.Chests.Coins,
        Wired.Chests.ItemsChunk,
        Wired.Chests.ItemsUpdated,
        Wired.Chests.UpgradeResult,
        Wired.Chests.PreferencesUpdated,
        Wired.Chests.OpenRequest,
        Wired.Chests.Close,
        Wired.Chests.LockAll,
        Wired.Chests.Upgrade,
        Wired.Chests.WithdrawAll,
        Wired.Chests.WithdrawCoins,
        Wired.Chests.WithdrawItems,
        Wired.Chests.StartAdding,
        Wired.Chests.OptionsUpdate,
        Wired.Chests.PreferencesUpdate,
        Wired.Chests.NotificationPreferencesUpdate,
        Wired.Transaction.Succeeded,
        Wired.Transaction.Failed,
        Wired.Transaction.ChestLogsRequest,
        Wired.Transaction.RoomLogsRequest,
        Wired.Transaction.Logs,
        Wired.Transaction.LogDetailsRequest,
        Wired.Transaction.LogDetails,
        Wired.Contracts.Contents,
        Wired.Contracts.Opened,
        Wired.Contracts.OpenRequest,
        Wired.Contracts.Update,
        Wired.Contracts.UpdateResult,
        Wired.Trade.Initiated,
        Wired.Trade.ItemsUpdated,
        Wired.Trade.Cancelled,
        Wired.Trade.Completed,
        Wired.Trade.ItemsUpdate,
        Wired.Trade.Confirm,
        Wired.Trade.Cancel,
        Wired.Trade.Notification,
        Notifications.MessageOfTheDay,
        Polls.Contents,
        Polls.Error,
        Polls.Offer,
        Polls.Answer,
        Polls.Reject,
        Polls.Start,
        Room.RatingRequest,
        Room.Settings.Request,
        Room.Settings.Snapshot,
        Room.Settings.RequestFailed,
        Room.Settings.Save,
        Room.Settings.SaveSucceeded,
        Room.Settings.SaveFailed,
        Room.Access.OpenRequest,
        Room.Access.OpenConfirmed,
        Room.Access.Doorbell,
        Room.Access.DoorbellAnswer,
        Room.Access.QueueStatus,
        Room.Access.Granted,
        Room.Access.Denied,
        Room.Access.NotFound,
        Room.Access.ConnectionFailed,
        Room.Lifecycle.Ready,
        Room.Lifecycle.Entry,
        Room.Lifecycle.Forward,
        Room.Lifecycle.ConnectionClosed,
        Room.Lifecycle.Quit,
        Room.Environment.EntryTile,
        Room.Environment.Property,
        Room.Environment.Visualization,
        Room.Environment.ChatSettings,
        Room.Environment.FloorPlan,
        Room.Chat.Talk,
        Room.Chat.Shout,
        Room.Chat.Whisper,
        Room.Chat.WhisperSend,
        Room.Chat.SpecialSystem,
        Room.Chat.TalkSend,
        Room.Chat.ShoutSend,
        Room.Authority.ControllersRequest,
        Room.Authority.ControllersSnapshot,
        Room.Authority.ControllerGrantRequest,
        Room.Authority.ControllerGranted,
        Room.Authority.ControllerRevoked,
        Room.Authority.Owner,
        Room.Authority.SpectatorGranted,
        Room.Authority.SpectatorRevoked,
        Room.Occupants.Snapshot,
        Room.Occupants.Removed,
        Room.Occupants.Status,
        Room.Occupants.Respect,
        Room.Occupants.RespectRequest,
        Room.Occupants.Action.Dance,
        Room.Occupants.Action.DanceRequest,
        Room.Occupants.Action.SignRequest,
        Room.Occupants.Action.Effect,
        Room.Occupants.Action.EffectSelectionRequest,
        Room.Occupants.Action.PostureRequest,
        Room.Occupants.Action.Carry,
        Room.Occupants.Action.Sleep,
        Room.Occupants.Action.Typing,
        Room.Occupants.Action.Expression,
        Room.Occupants.Action.ExpressionRequest,
        Room.Occupants.Identity.Appearance,
        Room.Occupants.Identity.Name,
        Room.Occupants.Identity.FavoriteGroup,
        Room.Occupants.Pet.Figure,
        Room.Occupants.Pet.InfoRequest,
        Room.Occupants.Pet.Info,
        Room.Occupants.Pet.Status,
        Room.Occupants.Pet.Level,
        Room.Occupants.Pet.RespectRequest,
        Room.Occupants.Pet.MountRequest,
        Room.Occupants.Pet.RemoveRequest,
        Room.Occupants.Bot.RemoveRequest,
        Room.HandItem.Received,
        Room.HandItem.Drop,
        Room.HandItem.Pass,
        Room.SnapshotRequest,
        Room.Snapshot,
        Room.StaffPickUpdateRequest,
        Room.FloorItemUse,
        Room.WallItemUse,
        Room.WallItemRemove,
        Room.ItemPlace,
        Room.WallItem.StickyDataSet,
        Room.WallItem.StickyDataRequest,
        Room.WallItem.StickyData,
        Room.WallItem.PostItPlace,
        Room.WallItem.SpamPostItAdd,
        Room.FloorItemMove,
        Room.WallItemMove,
        Room.FloorItem.Added,
        Room.FloorItem.Removed,
        Room.FloorItem.Updated,
        Room.FloorItem.ThrowDice,
        Room.FloorItem.DiceOff,
        Room.FloorItem.DiceValue,
        Room.FloorItem.OneWayDoorStatus,
        Room.FloorItem.OneWayDoorEnter,
        Room.Movement.Walk,
        Room.Movement.LookTo,
        Room.Movement.Slide,
        Room.Movement.Wired,
        Room.Typing.Start,
        Room.Typing.Cancel,
        Room.ItemPickup,
        Room.ItemPickupConfirmation,
        Room.WallItem.Added,
        Room.WallItem.Removed,
        Room.WallItem.Updated,
        Room.Moderation.BansRequest,
        Room.Moderation.BansSnapshot,
        Room.Moderation.UserUnbanned,
        Room.Moderation.UserMute,
        Room.Moderation.UserKick,
        Room.Moderation.UserBan,
        Room.Moderation.UserUnban,
        Friends.InitializeRequest,
        Friends.Initialized,
        Friends.ListFragment,
        Friends.ListUpdated,
        Friends.PrivateMessageSend,
        Friends.PrivateMessageReceived,
        Friends.OperationFailed,
        Friends.PrivateMessageFailed,
        Friends.FriendRequestSend,
        Friends.FriendRequestReceived,
        Friends.FriendRequestsRequest,
        Friends.FriendRequestsSnapshot,
        Friends.FriendRequestAccept,
        Friends.FriendRequestDecline,
        Friends.Remove,
        Friends.Follow,
        Friends.SearchRequest,
        Friends.SearchResult,
        Friends.RelationshipSet,
        Groups.Membership.Join,
        Groups.Membership.Kick,
        Groups.Membership.Approve,
        Groups.Membership.Reject,
        Groups.Details.Request,
        Groups.Details.Snapshot,
        Groups.Members.Request,
        Groups.Members.Snapshot,
        Groups.Memberships.Request,
        Groups.Memberships.Snapshot,
        Navigator.State.MetadataRequest,
        Navigator.State.Metadata,
        Navigator.State.FlatCategoriesRequest,
        Navigator.State.FlatCategories,
        Navigator.State.LiftedRooms,
        Navigator.State.Settings,
        Navigator.State.Preferences,
        Navigator.Search.Result,
        Navigator.Search.LegacyResult,
        Navigator.Search.View,
        Navigator.Search.MyRooms,
        Navigator.Search.MyFavouriteRooms,
        Navigator.Search.MyRoomRights,
        Navigator.Search.MyRoomHistory,
        Navigator.Search.MyFrequentRoomHistory,
        Navigator.Search.MyFriendsRooms,
        Navigator.Search.RoomsWhereFriendsAre,
        Navigator.Search.MyGuildBases,
        Navigator.Search.Text,
        Navigator.Search.Popular,
        Navigator.Search.HighestScoring,
        Navigator.Search.GuildBases,
        Navigator.Personalization.SavedSearches,
        Navigator.Personalization.SavedSearchAdd,
        Navigator.Personalization.SavedSearchDelete,
        Navigator.Personalization.CollapsedCategories,
        Navigator.Personalization.CollapsedCategoryAdd,
        Navigator.Personalization.CollapsedCategoryRemove,
        Navigator.HomeRoomUpdate,
        Navigator.RoomCreate,
        Navigator.RoomDelete,
        Inventory.AvatarEffects.ActivationRequest,
        Inventory.Furni.Request,
        Inventory.Furni.Snapshot,
        Inventory.Furni.AddedOrUpdated,
        Inventory.Furni.Removed,
        Inventory.Furni.RemovedMultiple,
        Inventory.Furni.Invalidated,
        Inventory.Furni.PostItPlaced,
        Inventory.Pets.Request,
        Inventory.Pets.Snapshot,
        Inventory.Pets.Added,
        Inventory.Pets.Removed,
        Marketplace.Configuration.Request,
        Marketplace.Configuration.Snapshot,
        Marketplace.Eligibility.Request,
        Marketplace.Eligibility.Result,
        Marketplace.Credits.Redeem,
        Marketplace.Tokens.Buy,
        Marketplace.Offers.SearchRequest,
        Marketplace.Offers.SearchResult,
        Marketplace.Offers.OwnRequest,
        Marketplace.Offers.OwnSnapshot,
        Marketplace.Offers.Make,
        Marketplace.Offers.MakeResult,
        Marketplace.Offers.Buy,
        Marketplace.Offers.BuyResult,
        Marketplace.Offers.Cancel,
        Marketplace.Offers.CancelResult,
        Marketplace.Offers.CancelAll,
        Marketplace.Offers.CancelAllResult,
        Marketplace.Offers.ClearOwnHistory,
        Marketplace.Offers.ClearOwnHistoryResult,
        Marketplace.ItemStats.Request,
        Marketplace.ItemStats.Snapshot,
        Wardrobe.Request,
        Wardrobe.Snapshot,
        Wardrobe.FigureUpdate,
        Wardrobe.OutfitSave,
        Trade.Opened,
        Trade.Offers,
        Trade.AcceptanceUpdated,
        Trade.Confirmation,
        Trade.Completed,
        Trade.Closed,
        Trade.OpenFailed,
        Trade.NftOffers,
        Trade.NftInventory,
        Trade.SilverUpdated,
        Trade.SilverFee,
        Trade.OpenRequest,
        Trade.ItemsAdd,
        Trade.ItemRemove,
        Trade.Accept,
        Trade.Unaccept,
        Trade.Confirm,
        Trade.Close,
        Trade.NftInventoryRequest,
        Users.Block.ListRequest,
        Users.Block.ListSnapshot,
        Users.Block.Updated,
        Users.Block.Add,
        Users.Block.Remove,
        Users.FavoriteGroup.Select,
        Users.FavoriteGroup.Deselect,
        Users.Ignore.ListRequest,
        Users.Ignore.ListSnapshot,
        Users.Ignore.Updated,
        Users.Ignore.AddByIdRequest,
        Users.Ignore.Remove,
        Users.FigureSets.Added,
        Users.FigureSets.Removed,
        Users.FigureSets.Snapshot,
        Users.Sanctions.Request,
        Users.Sanctions.Snapshot,
        Users.MottoUpdate,
        Users.ProfileRequest,
        Users.ProfileSnapshot,
        Users.FigureUpdated,
        Users.NameChangeResult,
        Users.SafetyLockChanged,
        Users.ExtendedProfileRequest,
        Users.ExtendedProfileSnapshot,
        Users.Relationship.Request,
        Users.Relationship.Snapshot
    ];

    public static class Errors
    {
        public static readonly MessageContract<GenericError> Generic =
            Flash<GenericError>(MessageKeys.Errors.Generic);
    }

    public static class Session
    {
        public static readonly MessageContract<DisconnectReason> DisconnectReason =
            Flash<DisconnectReason>(MessageKeys.Session.DisconnectReason);
    }

    public static class Achievements
    {
        public static readonly MessageContract<AchievementsRequest> Request =
            Flash<AchievementsRequest>(MessageKeys.Achievements.Request);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Achievements> Snapshot =
            Flash<Qx.Model.Messages.Incoming.Achievements>(MessageKeys.Achievements.Snapshot);

        public static readonly MessageContract<AchievementUpdate> Updated =
            Flash<AchievementUpdate>(MessageKeys.Achievements.Updated);

        public static readonly MessageContract<AchievementScore> Score =
            Flash<AchievementScore>(MessageKeys.Achievements.Score);

        public static readonly MessageContract<BadgePointLimitsRequest> PointLimitsRequest =
            Flash<BadgePointLimitsRequest>(MessageKeys.Achievements.PointLimitsRequest);

        public static readonly MessageContract<BadgePointLimits> PointLimits =
            Flash<BadgePointLimits>(MessageKeys.Achievements.PointLimits);

        public static readonly MessageContract<AchievementNotification> Notification =
            Flash<AchievementNotification>(MessageKeys.Achievements.Notification);
    }

    public static class Badges
    {
        public static readonly MessageContract<BadgeInventoryRequest> Request =
            Flash<BadgeInventoryRequest>(MessageKeys.Badges.Request);

        public static readonly MessageContract<BadgeInventory> Snapshot =
            Flash<BadgeInventory>(MessageKeys.Badges.Snapshot);

        public static readonly MessageContract<SelectedBadgesRequest> SelectedRequest =
            Flash<SelectedBadgesRequest>(MessageKeys.Badges.SelectedRequest);

        public static readonly MessageContract<BadgeReceived> Received =
            Flash<BadgeReceived>(MessageKeys.Badges.Received);

        public static readonly MessageContract<UserBadges> Selected =
            Flash<UserBadges>(MessageKeys.Badges.Selected);
    }

    public static class Wallet
    {
        public static readonly MessageContract<WalletBalanceRequest> CreditsRequest =
            Flash<WalletBalanceRequest>(MessageKeys.Wallet.CreditsRequest);

        public static readonly MessageContract<CreditBalance> CreditsBalance =
            Flash<CreditBalance>(MessageKeys.Wallet.CreditsBalance);

        public static readonly MessageContract<ActivityPoints> ActivityPoints =
            Flash<ActivityPoints>(MessageKeys.Wallet.ActivityPoints);

        public static readonly MessageContract<ActivityPointNotification> ActivityPointUpdated =
            Flash<ActivityPointNotification>(MessageKeys.Wallet.ActivityPointUpdated);
    }

    public static class Earnings
    {
        public static readonly MessageContract<EarningStatusRequest> StatusRequest =
            Flash<EarningStatusRequest>(MessageKeys.Earnings.StatusRequest);

        public static readonly MessageContract<EarningStatus> StatusSnapshot =
            Flash<EarningStatus>(MessageKeys.Earnings.StatusSnapshot);

        public static readonly MessageContract<EarningClaimRequest> Claim =
            Flash<EarningClaimRequest>(MessageKeys.Earnings.Claim);

        public static readonly MessageContract<EarningClaimResult> Claimed =
            Flash<EarningClaimResult>(MessageKeys.Earnings.Claimed);

        public static readonly MessageContract<EarningNotification> Notification =
            Flash<EarningNotification>(MessageKeys.Earnings.Notification);
    }

    public static class DailyTasks
    {
        public static readonly MessageContract<DailyTaskListRequest> Request =
            Flash<DailyTaskListRequest>(MessageKeys.DailyTasks.Request);

        public static readonly MessageContract<DailyTasksActiveList> Snapshot =
            Flash<DailyTasksActiveList>(MessageKeys.DailyTasks.Snapshot);

        public static readonly MessageContract<DailyTasksTasksAdded> Added =
            Flash<DailyTasksTasksAdded>(MessageKeys.DailyTasks.Added);

        public static readonly MessageContract<DailyTasksTaskUpdate> Updated =
            Flash<DailyTasksTaskUpdate>(MessageKeys.DailyTasks.Updated);

        public static readonly MessageContract<DailyTaskClaimRequest> Claim =
            Flash<DailyTaskClaimRequest>(MessageKeys.DailyTasks.Claim);
    }

    public static class Quests
    {
        public static readonly MessageContract<GetQuests> Request =
            Flash<GetQuests>(MessageKeys.Quests.Request);

        public static readonly MessageContract<global::Qx.Model.Messages.Incoming.Quests> Snapshot =
            Flash<global::Qx.Model.Messages.Incoming.Quests>(MessageKeys.Quests.Snapshot);

        public static readonly MessageContract<GetSeasonalQuests> SeasonalRequest =
            Flash<GetSeasonalQuests>(MessageKeys.Quests.SeasonalRequest);

        public static readonly MessageContract<QuestsSeasonal> SeasonalSnapshot =
            Flash<QuestsSeasonal>(MessageKeys.Quests.SeasonalSnapshot);

        public static readonly MessageContract<Quest> Updated =
            Flash<Quest>(MessageKeys.Quests.Updated);

        public static readonly MessageContract<QuestCompleted> Completed =
            Flash<QuestCompleted>(MessageKeys.Quests.Completed);

        public static readonly MessageContract<QuestCancelled> Cancelled =
            Flash<QuestCancelled>(MessageKeys.Quests.Cancelled);

        public static readonly MessageContract<GetDailyQuest> DailyRequest =
            Flash<GetDailyQuest>(MessageKeys.Quests.DailyRequest);

        public static readonly MessageContract<QuestDaily> Daily =
            Flash<QuestDaily>(MessageKeys.Quests.Daily);

        public static readonly MessageContract<AcceptQuest> Accept =
            Flash<AcceptQuest>(MessageKeys.Quests.Accept);

        public static readonly MessageContract<ActivateQuest> Activate =
            Flash<ActivateQuest>(MessageKeys.Quests.Activate);

        public static readonly MessageContract<RejectQuest> Reject =
            Flash<RejectQuest>(MessageKeys.Quests.Reject);

        public static readonly MessageContract<CancelQuest> Cancel =
            Flash<CancelQuest>(MessageKeys.Quests.Cancel);

        public static readonly MessageContract<OpenQuestTracker> TrackerOpen =
            Flash<OpenQuestTracker>(MessageKeys.Quests.TrackerOpen);

        public static readonly MessageContract<FriendRequestQuestComplete> FriendRequestCompleted =
            Flash<FriendRequestQuestComplete>(MessageKeys.Quests.FriendRequestCompleted);
    }

    public static class Habbicons
    {
        public static readonly MessageContract<HabbiconShopRequest> ShopRequest =
            Flash<HabbiconShopRequest>(MessageKeys.Habbicons.ShopRequest);

        public static readonly MessageContract<HabbiconShopData> ShopSnapshot =
            Flash<HabbiconShopData>(MessageKeys.Habbicons.ShopSnapshot);

        public static readonly MessageContract<UserHabbicons> InventorySnapshot =
            Flash<UserHabbicons>(MessageKeys.Habbicons.InventorySnapshot);

        public static readonly MessageContract<UserHabbiconStatusChanged> StatusUpdated =
            Flash<UserHabbiconStatusChanged>(MessageKeys.Habbicons.StatusUpdated);

        public static readonly MessageContract<HabbiconInfoRequest> InfoRequest =
            Flash<HabbiconInfoRequest>(MessageKeys.Habbicons.InfoRequest);

        public static readonly MessageContract<HabbiconInfo> InfoSnapshot =
            Flash<HabbiconInfo>(MessageKeys.Habbicons.InfoSnapshot);

        public static readonly MessageContract<RoomUseHabbicon> RoomUsed =
            Flash<RoomUseHabbicon>(MessageKeys.Habbicons.RoomUsed);

        public static readonly MessageContract<HabbiconBuyRequest> Buy =
            Flash<HabbiconBuyRequest>(MessageKeys.Habbicons.Buy);

        public static readonly MessageContract<HabbiconCollectionBuyRequest> BuyCollection =
            Flash<HabbiconCollectionBuyRequest>(MessageKeys.Habbicons.BuyCollection);

        public static readonly MessageContract<HabbiconClaimRequest> Claim =
            Flash<HabbiconClaimRequest>(MessageKeys.Habbicons.Claim);

        public static readonly MessageContract<HabbiconFavoriteRequest> Favorite =
            Flash<HabbiconFavoriteRequest>(MessageKeys.Habbicons.Favorite);

        public static readonly MessageContract<HabbiconUnfavoriteRequest> Unfavorite =
            Flash<HabbiconUnfavoriteRequest>(MessageKeys.Habbicons.Unfavorite);
    }

    public static class Leaderboards
    {
        public static readonly MessageContract<LeaderboardRequest> TotalRequest =
            Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Total.Request);

        public static readonly MessageContract<TotalLeaderboard> TotalSnapshot =
            Flash<TotalLeaderboard>(MessageKeys.Leaderboards.Total.Snapshot);

        public static readonly MessageContract<LeaderboardRequest> FriendsRequest =
            Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Friends.Request);

        public static readonly MessageContract<FriendsLeaderboard> FriendsSnapshot =
            Flash<FriendsLeaderboard>(MessageKeys.Leaderboards.Friends.Snapshot);

        public static readonly MessageContract<LeaderboardRequest> GroupsRequest =
            Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Groups.Request);

        public static readonly MessageContract<TotalGroupLeaderboard> GroupsSnapshot =
            Flash<TotalGroupLeaderboard>(MessageKeys.Leaderboards.Groups.Snapshot);

        public static readonly MessageContract<WeeklyLeaderboardRequest> WeeklyTotalRequest =
            Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyTotal.Request);

        public static readonly MessageContract<WeeklyLeaderboard> WeeklyTotalSnapshot =
            Flash<WeeklyLeaderboard>(MessageKeys.Leaderboards.WeeklyTotal.Snapshot);

        public static readonly MessageContract<WeeklyLeaderboardRequest> WeeklyFriendsRequest =
            Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyFriends.Request);

        public static readonly MessageContract<WeeklyFriendsLeaderboard> WeeklyFriendsSnapshot =
            Flash<WeeklyFriendsLeaderboard>(MessageKeys.Leaderboards.WeeklyFriends.Snapshot);

        public static readonly MessageContract<WeeklyLeaderboardRequest> WeeklyGroupsRequest =
            Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyGroups.Request);

        public static readonly MessageContract<WeeklyGroupLeaderboard> WeeklyGroupsSnapshot =
            Flash<WeeklyGroupLeaderboard>(MessageKeys.Leaderboards.WeeklyGroups.Snapshot);
    }

    public static class Forums
    {
        public static readonly MessageContract<ForumData> Stats =
            Flash<ForumData>(MessageKeys.Forums.Stats);

        public static readonly MessageContract<ForumsList> List =
            Flash<ForumsList>(MessageKeys.Forums.List);

        public static readonly MessageContract<ForumThreads> Threads =
            Flash<ForumThreads>(MessageKeys.Forums.Threads);

        public static readonly MessageContract<ThreadMessages> Messages =
            Flash<ThreadMessages>(MessageKeys.Forums.Messages);

        public static readonly MessageContract<PostThread> ThreadCreated =
            Flash<PostThread>(MessageKeys.Forums.ThreadCreated);

        public static readonly MessageContract<PostMessage> MessageCreated =
            Flash<PostMessage>(MessageKeys.Forums.MessageCreated);

        public static readonly MessageContract<UpdateThread> ThreadUpdated =
            Flash<UpdateThread>(MessageKeys.Forums.ThreadUpdated);

        public static readonly MessageContract<UpdateMessage> MessageUpdated =
            Flash<UpdateMessage>(MessageKeys.Forums.MessageUpdated);

        public static readonly MessageContract<UnreadForumsCount> UnreadCount =
            Flash<UnreadForumsCount>(MessageKeys.Forums.UnreadCount);

        public static readonly MessageContract<GetForumStats> StatsRequest =
            Flash<GetForumStats>(MessageKeys.Forums.StatsRequest);

        public static readonly MessageContract<GetForumsList> ListRequest =
            Flash<GetForumsList>(MessageKeys.Forums.ListRequest);

        public static readonly MessageContract<GetForumThreads> ThreadsRequest =
            Flash<GetForumThreads>(MessageKeys.Forums.ThreadsRequest);

        public static readonly MessageContract<GetForumThreadMessages> MessagesRequest =
            Flash<GetForumThreadMessages>(MessageKeys.Forums.MessagesRequest);

        public static readonly MessageContract<GetForumThread> ThreadRequest =
            Flash<GetForumThread>(MessageKeys.Forums.ThreadRequest);

        public static readonly MessageContract<GetUnreadForumsCount> UnreadCountRequest =
            Flash<GetUnreadForumsCount>(MessageKeys.Forums.UnreadCountRequest);

        public static readonly MessageContract<PostMessage> Post =
            Flash<PostMessage>(MessageKeys.Forums.Post);

        public static readonly MessageContract<ModerateForumThread> ThreadModerate =
            Flash<ModerateForumThread>(MessageKeys.Forums.ThreadModerate);

        public static readonly MessageContract<ModerateForumMessage> MessageModerate =
            Flash<ModerateForumMessage>(MessageKeys.Forums.MessageModerate);

        public static readonly MessageContract<UpdateForumSettings> SettingsUpdate =
            Flash<UpdateForumSettings>(MessageKeys.Forums.SettingsUpdate);

        public static readonly MessageContract<UpdateForumReadMarkers> ReadMarkersUpdate =
            Flash<UpdateForumReadMarkers>(MessageKeys.Forums.ReadMarkersUpdate);

        public static readonly MessageContract<UpdateThread> ThreadUpdate =
            Flash<UpdateThread>(MessageKeys.Forums.ThreadUpdate);

        public static readonly MessageContract<CallForHelpFromForumThread> ThreadReport =
            ForumThreadReport();

        public static readonly MessageContract<CallForHelpFromForumMessage> MessageReport =
            ForumMessageReport();
    }

    public static class Catalog
    {
        public static readonly MessageContract<CatalogIndexRequest> IndexRequest =
            Flash<CatalogIndexRequest>(MessageKeys.Catalog.IndexRequest);

        public static readonly MessageContract<CatalogIndex> IndexSnapshot =
            Flash<CatalogIndex>(MessageKeys.Catalog.IndexSnapshot);

        public static readonly MessageContract<CatalogPageRequest> PageRequest =
            Flash<CatalogPageRequest>(MessageKeys.Catalog.PageRequest);

        public static readonly MessageContract<CatalogPage> PageSnapshot =
            Flash<CatalogPage>(MessageKeys.Catalog.PageSnapshot);

        public static readonly MessageContract<PurchaseFromCatalogRequest> Purchase =
            Flash<PurchaseFromCatalogRequest>(MessageKeys.Catalog.Purchase);

        public static readonly MessageContract<PurchaseOK> Accepted =
            Flash<PurchaseOK>(MessageKeys.Catalog.PurchaseAccepted);

        public static readonly MessageContract<PurchaseError> Failed =
            Flash<PurchaseError>(MessageKeys.Catalog.PurchaseFailed);

        public static readonly MessageContract<PurchaseNotAllowed> Forbidden =
            Flash<PurchaseNotAllowed>(MessageKeys.Catalog.PurchaseForbidden);

        public static readonly MessageContract<CatalogPublished> Published =
            Flash<CatalogPublished>(MessageKeys.Catalog.Published);

        public static readonly MessageContract<GetRoomAdPurchaseInfo> RoomAdInfoRequest =
            Flash<GetRoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfoRequest);

        public static readonly MessageContract<RoomAdPurchaseInfo> RoomAdInfo =
            Flash<RoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfo);
    }

    public static class Gifts
    {
        public static readonly MessageContract<GiftWrappingConfiguration> WrappingConfiguration =
            Flash<GiftWrappingConfiguration>(MessageKeys.Gifts.WrappingConfiguration);

        public static readonly MessageContract<PresentOpened> PresentOpened =
            Flash<PresentOpened>(MessageKeys.Gifts.PresentOpened);

        public static readonly MessageContract<ClubGiftInfo> ClubInfo =
            Flash<ClubGiftInfo>(MessageKeys.Gifts.ClubInfo);

        public static readonly MessageContract<ClubGiftSelected> ClubSelected =
            Flash<ClubGiftSelected>(MessageKeys.Gifts.ClubSelected);

        public static readonly MessageContract<GiftReceiverNotFound> ReceiverNotFound =
            Flash<GiftReceiverNotFound>(MessageKeys.Gifts.ReceiverNotFound);

        public static readonly MessageContract<ClubGiftNotification> ClubNotification =
            Flash<ClubGiftNotification>(MessageKeys.Gifts.ClubNotification);

        public static readonly MessageContract<IsOfferGiftable> OfferGiftability =
            Flash<IsOfferGiftable>(MessageKeys.Gifts.OfferGiftability);

        public static readonly MessageContract<NuxGiftOffer> NewUserOffer =
            Flash<NuxGiftOffer>(MessageKeys.Gifts.NewUserOffer);

        public static readonly MessageContract<NuxNotComplete> NewUserIncomplete =
            Flash<NuxNotComplete>(MessageKeys.Gifts.NewUserIncomplete);

        public static readonly MessageContract<GetGiftWrappingConfiguration>
            WrappingConfigurationRequest =
                Flash<GetGiftWrappingConfiguration>(
                    MessageKeys.Gifts.WrappingConfigurationRequest);

        public static readonly MessageContract<PresentOpen> PresentOpen =
            Flash<PresentOpen>(MessageKeys.Gifts.PresentOpen);

        public static readonly MessageContract<PurchaseFromCatalogAsGift> Purchase =
            Flash<PurchaseFromCatalogAsGift>(MessageKeys.Gifts.Purchase);

        public static readonly MessageContract<GetClubGift> ClubInfoRequest =
            Flash<GetClubGift>(MessageKeys.Gifts.ClubInfoRequest);

        public static readonly MessageContract<SelectClubGift> ClubSelect =
            Flash<SelectClubGift>(MessageKeys.Gifts.ClubSelect);

        public static readonly MessageContract<GetIsOfferGiftable> OfferGiftabilityRequest =
            Flash<GetIsOfferGiftable>(MessageKeys.Gifts.OfferGiftabilityRequest);

        public static readonly MessageContract<NuxGetGifts> NewUserSelect =
            Flash<NuxGetGifts>(MessageKeys.Gifts.NewUserSelect);

        public static readonly MessageContract<AdvanceNewUserFlowRequest> NewUserAdvance =
            Flash<AdvanceNewUserFlowRequest>(MessageKeys.Gifts.NewUserAdvance);
    }

    public static class Groups
    {
        public static class Details
        {
            public static readonly MessageContract<GroupDetailsRequest> Request =
                Flash<GroupDetailsRequest>(MessageKeys.Groups.Details.Request);

            public static readonly MessageContract<GroupData> Snapshot =
                Flash<GroupData>(MessageKeys.Groups.Details.Snapshot);
        }

        public static class Membership
        {
            public static readonly MessageContract<JoinGroupRequest> Join =
                Flash<JoinGroupRequest>(MessageKeys.Groups.Membership.Join);

            public static readonly MessageContract<KickGroupMemberRequest> Kick =
                Flash<KickGroupMemberRequest>(MessageKeys.Groups.Membership.Kick);

            public static readonly MessageContract<ApproveGroupMemberRequest> Approve =
                Flash<ApproveGroupMemberRequest>(MessageKeys.Groups.Membership.Approve);

            public static readonly MessageContract<RejectGroupMemberRequest> Reject =
                Flash<RejectGroupMemberRequest>(MessageKeys.Groups.Membership.Reject);
        }

        public static class Members
        {
            public static readonly MessageContract<GetGuildMembersRequest> Request =
                Flash<GetGuildMembersRequest>(MessageKeys.Groups.Members.Request);

            public static readonly MessageContract<GuildMembers> Snapshot =
                Flash<GuildMembers>(MessageKeys.Groups.Members.Snapshot);
        }

        public static class Memberships
        {
            public static readonly MessageContract<GuildMembershipsRequest> Request =
                Flash<GuildMembershipsRequest>(MessageKeys.Groups.Memberships.Request);

            public static readonly MessageContract<GuildMemberships> Snapshot =
                Flash<GuildMemberships>(MessageKeys.Groups.Memberships.Snapshot);
        }
    }

    public static class Navigator
    {
        public static class State
        {
            public static readonly MessageContract<NavigatorMetadataRequest> MetadataRequest =
                Flash<NavigatorMetadataRequest>(MessageKeys.Navigator.State.MetadataRequest);

            public static readonly MessageContract<NavigatorMetaData> Metadata =
                Flash<NavigatorMetaData>(MessageKeys.Navigator.State.Metadata);

            public static readonly MessageContract<FlatCategoriesRequest> FlatCategoriesRequest =
                Flash<FlatCategoriesRequest>(MessageKeys.Navigator.State.FlatCategoriesRequest);

            public static readonly MessageContract<UserFlatCats> FlatCategories =
                Flash<UserFlatCats>(MessageKeys.Navigator.State.FlatCategories);

            public static readonly MessageContract<NavigatorLiftedRooms> LiftedRooms =
                Flash<NavigatorLiftedRooms>(MessageKeys.Navigator.State.LiftedRooms);

            public static readonly MessageContract<NavigatorSettings> Settings =
                Flash<NavigatorSettings>(MessageKeys.Navigator.State.Settings);

            public static readonly MessageContract<NewNavigatorPreferences> Preferences =
                Flash<NewNavigatorPreferences>(MessageKeys.Navigator.State.Preferences);
        }

        public static class Search
        {
            public static readonly MessageContract<NavigatorSearchResult> Result =
                Flash<NavigatorSearchResult>(MessageKeys.Navigator.Search.Result);

            public static readonly MessageContract<NavigatorSearchResult> LegacyResult =
                LegacyNavigatorSearchResult();

            public static readonly MessageContract<NavigatorViewSearchRequest> View =
                Flash<NavigatorViewSearchRequest>(MessageKeys.Navigator.Search.View);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFavouriteRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFavouriteRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomRights =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomRights);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomHistory =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomHistory);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFrequentRoomHistory =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFrequentRoomHistory);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFriendsRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFriendsRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> RoomsWhereFriendsAre =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.RoomsWhereFriendsAre);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyGuildBases =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyGuildBases);

            public static readonly MessageContract<NavigatorTextSearchRequest> Text =
                Flash<NavigatorTextSearchRequest>(MessageKeys.Navigator.Search.Text);

            public static readonly MessageContract<NavigatorTagSearchRequest> Popular =
                Flash<NavigatorTagSearchRequest>(MessageKeys.Navigator.Search.Popular);

            public static readonly MessageContract<NavigatorAdSearchRequest> HighestScoring =
                Flash<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.HighestScoring);

            public static readonly MessageContract<NavigatorAdSearchRequest> GuildBases =
                Flash<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.GuildBases);
        }

        public static class Personalization
        {
            public static readonly MessageContract<NavigatorSavedSearches> SavedSearches =
                Flash<NavigatorSavedSearches>(MessageKeys.Navigator.Personalization.SavedSearches);

            public static readonly MessageContract<AddSavedSearchRequest> SavedSearchAdd =
                Flash<AddSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchAdd);

            public static readonly MessageContract<DeleteSavedSearchRequest> SavedSearchDelete =
                Flash<DeleteSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchDelete);

            public static readonly MessageContract<CollapsedCategories> CollapsedCategories =
                Flash<CollapsedCategories>(MessageKeys.Navigator.Personalization.CollapsedCategories);

            public static readonly MessageContract<AddCollapsedCategoryRequest> CollapsedCategoryAdd =
                Flash<AddCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryAdd);

            public static readonly MessageContract<RemoveCollapsedCategoryRequest> CollapsedCategoryRemove =
                Flash<RemoveCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryRemove);
        }

        public static readonly MessageContract<SetHomeRoomRequest> HomeRoomUpdate =
            Flash<SetHomeRoomRequest>(MessageKeys.Navigator.HomeRoomUpdate);

        public static readonly MessageContract<CreateRoomRequest> RoomCreate =
            Flash<CreateRoomRequest>(MessageKeys.Navigator.RoomCreate);

        public static readonly MessageContract<DeleteRoomRequest> RoomDelete =
            Flash<DeleteRoomRequest>(MessageKeys.Navigator.RoomDelete);
    }

    public static class Inventory
    {
        public static class AvatarEffects
        {
            public static readonly MessageContract<AvatarEffectActivationRequest> ActivationRequest =
                Flash<AvatarEffectActivationRequest>(MessageKeys.Inventory.AvatarEffects.ActivationRequest);
        }

        public static class Furni
        {
            public static readonly MessageContract<FurniInventoryRequest> Request =
                Flash<FurniInventoryRequest>(MessageKeys.Inventory.Furni.Request);

            public static readonly MessageContract<FurniList> Snapshot =
                Flash<FurniList>(MessageKeys.Inventory.Furni.Snapshot);

            public static readonly MessageContract<FurniListAddOrUpdate> AddedOrUpdated =
                Flash<FurniListAddOrUpdate>(MessageKeys.Inventory.Furni.AddedOrUpdated);

            public static readonly MessageContract<FurniListRemove> Removed =
                Flash<FurniListRemove>(MessageKeys.Inventory.Furni.Removed);

            public static readonly MessageContract<FurniListRemoveMultiple> RemovedMultiple =
                Flash<FurniListRemoveMultiple>(MessageKeys.Inventory.Furni.RemovedMultiple);

            public static readonly MessageContract<FurniListInvalidate> Invalidated =
                Flash<FurniListInvalidate>(MessageKeys.Inventory.Furni.Invalidated);

            public static readonly MessageContract<PostItPlaced> PostItPlaced =
                Flash<PostItPlaced>(MessageKeys.Inventory.Furni.PostItPlaced);
        }

        public static class Pets
        {
            public static readonly MessageContract<PetInventoryRequest> Request =
                Flash<PetInventoryRequest>(MessageKeys.Inventory.Pets.Request);

            public static readonly MessageContract<PetInventory> Snapshot =
                Flash<PetInventory>(MessageKeys.Inventory.Pets.Snapshot);

            public static readonly MessageContract<PetAddedToInventory> Added =
                Flash<PetAddedToInventory>(MessageKeys.Inventory.Pets.Added);

            public static readonly MessageContract<PetRemovedFromInventory> Removed =
                Flash<PetRemovedFromInventory>(MessageKeys.Inventory.Pets.Removed);
        }
    }

    public static class Wardrobe
    {
        public static readonly MessageContract<WardrobeRequest> Request =
            Flash<WardrobeRequest>(MessageKeys.Wardrobe.Request);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Wardrobe> Snapshot =
            Flash<Qx.Model.Messages.Incoming.Wardrobe>(MessageKeys.Wardrobe.Snapshot);

        public static readonly MessageContract<FigureUpdateRequest> FigureUpdate =
            Flash<FigureUpdateRequest>(MessageKeys.Wardrobe.FigureUpdate);

        public static readonly MessageContract<SaveWardrobeOutfitRequest> OutfitSave =
            Flash<SaveWardrobeOutfitRequest>(MessageKeys.Wardrobe.OutfitSave);
    }

    public static class Marketplace
    {
        public static class Configuration
        {
            public static readonly MessageContract<GetMarketplaceConfiguration> Request =
                ModernFlashMarketplace<GetMarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Request);

            public static readonly MessageContract<MarketplaceConfiguration> Snapshot =
                Flash<MarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Snapshot);
        }

        public static class Eligibility
        {
            public static readonly MessageContract<GetMarketplaceCanMakeOffer> Request =
                ModernFlashMarketplace<GetMarketplaceCanMakeOffer>(MessageKeys.Marketplace.Eligibility.Request);

            public static readonly MessageContract<MarketplaceCanMakeOfferResult> Result =
                Flash<MarketplaceCanMakeOfferResult>(MessageKeys.Marketplace.Eligibility.Result);
        }

        public static class Credits
        {
            public static readonly MessageContract<RedeemMarketplaceOfferCredits> Redeem =
                Flash<RedeemMarketplaceOfferCredits>(MessageKeys.Marketplace.Credits.Redeem);
        }

        public static class Tokens
        {
            public static readonly MessageContract<BuyMarketplaceTokens> Buy =
                ModernFlashMarketplace<BuyMarketplaceTokens>(MessageKeys.Marketplace.Tokens.Buy);
        }

        public static class Offers
        {
            public static readonly MessageContract<SearchMarketplaceOffers> SearchRequest =
                MarketplaceLayout<SearchMarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchRequest);

            public static readonly MessageContract<MarketplaceOffers> SearchResult =
                Flash<MarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchResult);

            public static readonly MessageContract<GetMarketplaceOwnOffers> OwnRequest =
                MarketplaceLayout<GetMarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnRequest);

            public static readonly MessageContract<MarketplaceOwnOffers> OwnSnapshot =
                Flash<MarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnSnapshot);

            public static readonly MessageContract<MakeMarketplaceOffer> Make =
                MarketplaceLayout<MakeMarketplaceOffer>(MessageKeys.Marketplace.Offers.Make);

            public static readonly MessageContract<MarketplaceMakeOfferResult> MakeResult =
                Flash<MarketplaceMakeOfferResult>(MessageKeys.Marketplace.Offers.MakeResult);

            public static readonly MessageContract<MarketplaceBuyOfferRequest> Buy =
                new(
                    MessageKeys.Marketplace.Offers.Buy,
                    MessageCodec<MarketplaceBuyOfferRequest>.FromModel());

            public static readonly MessageContract<MarketplaceBuyResult> BuyResult =
                Flash<MarketplaceBuyResult>(MessageKeys.Marketplace.Offers.BuyResult);

            public static readonly MessageContract<CancelMarketplaceOffer> Cancel =
                Flash<CancelMarketplaceOffer>(MessageKeys.Marketplace.Offers.Cancel);

            public static readonly MessageContract<MarketplaceCancelOfferResult> CancelResult =
                Flash<MarketplaceCancelOfferResult>(MessageKeys.Marketplace.Offers.CancelResult);

            public static readonly MessageContract<CancelAllMarketplaceOffers> CancelAll =
                ModernFlashMarketplace<CancelAllMarketplaceOffers>(MessageKeys.Marketplace.Offers.CancelAll);

            public static readonly MessageContract<MarketplaceCancelAllOffersResult> CancelAllResult =
                Flash<MarketplaceCancelAllOffersResult>(MessageKeys.Marketplace.Offers.CancelAllResult);

            public static readonly MessageContract<ClearMarketplaceOwnHistory> ClearOwnHistory =
                ModernFlashMarketplace<ClearMarketplaceOwnHistory>(MessageKeys.Marketplace.Offers.ClearOwnHistory);

            public static readonly MessageContract<MarketplaceClearOwnHistoryResult> ClearOwnHistoryResult =
                ModernFlashMarketplace<MarketplaceClearOwnHistoryResult>(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult);
        }

        public static class ItemStats
        {
            public static readonly MessageContract<GetMarketplaceItemStats> Request =
                MarketplaceLayout<GetMarketplaceItemStats>(MessageKeys.Marketplace.ItemStats.Request);

            public static readonly MessageContract<MarketplaceItemStats> Snapshot =
                Flash<MarketplaceItemStats>(MessageKeys.Marketplace.ItemStats.Snapshot);
        }
    }

    public static class Subscriptions
    {
        public static readonly MessageContract<ScrSendUserInfo> UserInfo =
            Flash<ScrSendUserInfo>(MessageKeys.Subscriptions.UserInfo);

        public static readonly MessageContract<SubscriptionGetUserInfo> UserInfoRequest =
            Flash<SubscriptionGetUserInfo>(MessageKeys.Subscriptions.UserInfoRequest);

        public static readonly MessageContract<ScrSendKickbackInfo> KickbackInfo =
            Flash<ScrSendKickbackInfo>(MessageKeys.Subscriptions.KickbackInfo);

        public static readonly MessageContract<SubscriptionGetKickbackInfo> KickbackInfoRequest =
            Flash<SubscriptionGetKickbackInfo>(MessageKeys.Subscriptions.KickbackInfoRequest);

        public static readonly MessageContract<HabboClubOffers> ClubOffersSnapshot =
            Flash<HabboClubOffers>(MessageKeys.Subscriptions.ClubOffersSnapshot);

        public static readonly MessageContract<GetClubOffers> ClubOffersRequest =
            Flash<GetClubOffers>(MessageKeys.Subscriptions.ClubOffersRequest);

        public static readonly MessageContract<BuildersClubFurniCount> BuildersClubFurniCount =
            Flash<BuildersClubFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCount);

        public static readonly MessageContract<BuildersClubQueryFurniCount> BuildersClubFurniCountRequest =
            Flash<BuildersClubQueryFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCountRequest);

        public static readonly MessageContract<BuildersClubMembershipStatus> BuildersClubMembershipStatus =
            Flash<BuildersClubMembershipStatus>(MessageKeys.Subscriptions.BuildersClubMembershipStatus);

        public static readonly MessageContract<BuildersClubPlacementWarning> BuildersClubPlacementWarning =
            Flash<BuildersClubPlacementWarning>(MessageKeys.Subscriptions.BuildersClubPlacementWarning);

        public static readonly MessageContract<BuildersClubPlaceRoomItem> BuildersClubFloorOfferPlace =
            Flash<BuildersClubPlaceRoomItem>(MessageKeys.Subscriptions.BuildersClubFloorOfferPlace);

        public static readonly MessageContract<BuildersClubPlaceWallItem> BuildersClubWallOfferPlace =
            Flash<BuildersClubPlaceWallItem>(
                MessageKeys.Subscriptions.BuildersClubWallOfferPlace);
    }

    public static class Crafting
    {
        public static readonly MessageContract<GetCraftableProducts> ProductsRequest =
            Flash<GetCraftableProducts>(MessageKeys.Crafting.ProductsRequest);

        public static readonly MessageContract<CraftableProducts> ProductsSnapshot =
            Flash<CraftableProducts>(MessageKeys.Crafting.ProductsSnapshot);

        public static readonly MessageContract<GetCraftingRecipe> RecipeRequest =
            Flash<GetCraftingRecipe>(MessageKeys.Crafting.RecipeRequest);

        public static readonly MessageContract<CraftingRecipe> RecipeSnapshot =
            Flash<CraftingRecipe>(MessageKeys.Crafting.RecipeSnapshot);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Craft> Craft =
            Flash<Qx.Model.Messages.Incoming.Craft>(MessageKeys.Crafting.Craft);

        public static readonly MessageContract<CraftSecret> SecretCraft =
            Flash<CraftSecret>(MessageKeys.Crafting.SecretCraft);

        public static readonly MessageContract<GetCraftingRecipesAvailable> AvailabilityRequest =
            Flash<GetCraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilityRequest);

        public static readonly MessageContract<CraftingRecipesAvailable> AvailabilitySnapshot =
            Flash<CraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilitySnapshot);

        public static readonly MessageContract<CraftingResult> Result =
            Flash<CraftingResult>(MessageKeys.Crafting.Result);
    }

    public static class Recycler
    {
        public static readonly MessageContract<RecyclerStatus> Status =
            Flash<RecyclerStatus>(MessageKeys.Recycler.Status);

        public static readonly MessageContract<RecyclerFinished> Finished =
            Flash<RecyclerFinished>(MessageKeys.Recycler.Finished);
    }

    public static class Wired
    {
        public static class State
        {
            public static readonly MessageContract<WiredPermissions> Permissions =
                Flash<WiredPermissions>(MessageKeys.Wired.State.Permissions);

            public static readonly MessageContract<WiredEnvironment> Environment =
                Flash<WiredEnvironment>(MessageKeys.Wired.State.Environment);

            public static readonly MessageContract<WiredClickSettings> ClickSettings =
                Flash<WiredClickSettings>(MessageKeys.Wired.State.ClickSettings);

            public static readonly MessageContract<WiredMenuError> MenuError =
                Flash<WiredMenuError>(MessageKeys.Wired.State.MenuError);

            public static readonly MessageContract<WiredRewardResult> RewardResult =
                Flash<WiredRewardResult>(MessageKeys.Wired.State.RewardResult);
        }

        public static class Configuration
        {
            public static readonly MessageContract<WiredOpen> Opened =
                Flash<WiredOpen>(MessageKeys.Wired.Configuration.Opened);

            public static readonly MessageContract<WiredOpen> OpenRequest =
                Flash<WiredOpen>(MessageKeys.Wired.Configuration.OpenRequest);

            public static readonly MessageContract<WiredApplySnapshot> ApplySnapshot =
                Flash<WiredApplySnapshot>(MessageKeys.Wired.Configuration.ApplySnapshot);

            public static readonly MessageContract<WiredFurniTrigger> Trigger =
                Flash<WiredFurniTrigger>(MessageKeys.Wired.Configuration.Trigger);

            public static readonly MessageContract<WiredFurniAction> Action =
                Flash<WiredFurniAction>(MessageKeys.Wired.Configuration.Action);

            public static readonly MessageContract<WiredFurniCondition> Condition =
                Flash<WiredFurniCondition>(MessageKeys.Wired.Configuration.Condition);

            public static readonly MessageContract<WiredFurniSelector> Selector =
                Flash<WiredFurniSelector>(MessageKeys.Wired.Configuration.Selector);

            public static readonly MessageContract<WiredFurniAddon> Addon =
                Flash<WiredFurniAddon>(MessageKeys.Wired.Configuration.Addon);

            public static readonly MessageContract<WiredFurniVariable> Variable =
                Flash<WiredFurniVariable>(MessageKeys.Wired.Configuration.Variable);

            public static readonly MessageContract<UpdateTrigger> TriggerUpdate =
                Flash<UpdateTrigger>(MessageKeys.Wired.Configuration.TriggerUpdate);

            public static readonly MessageContract<UpdateAction> ActionUpdate =
                Flash<UpdateAction>(MessageKeys.Wired.Configuration.ActionUpdate);

            public static readonly MessageContract<UpdateCondition> ConditionUpdate =
                Flash<UpdateCondition>(MessageKeys.Wired.Configuration.ConditionUpdate);

            public static readonly MessageContract<UpdateSelector> SelectorUpdate =
                Flash<UpdateSelector>(MessageKeys.Wired.Configuration.SelectorUpdate);

            public static readonly MessageContract<UpdateAddon> AddonUpdate =
                Flash<UpdateAddon>(MessageKeys.Wired.Configuration.AddonUpdate);

            public static readonly MessageContract<UpdateVariable> VariableUpdate =
                Flash<UpdateVariable>(MessageKeys.Wired.Configuration.VariableUpdate);

            public static readonly MessageContract<WiredSaveSuccess> SaveSucceeded =
                Flash<WiredSaveSuccess>(MessageKeys.Wired.Configuration.SaveSucceeded);

            public static readonly MessageContract<WiredValidationError> ValidationFailed =
                Flash<WiredValidationError>(MessageKeys.Wired.Configuration.ValidationFailed);
        }

        public static class Room
        {
            public static readonly MessageContract<WiredGetRoomSettings> SettingsRequest =
                Flash<WiredGetRoomSettings>(MessageKeys.Wired.Room.SettingsRequest);

            public static readonly MessageContract<WiredRoomSettings> Settings =
                Flash<WiredRoomSettings>(MessageKeys.Wired.Room.Settings);

            public static readonly MessageContract<WiredSetRoomSettings> SettingsUpdate =
                Flash<WiredSetRoomSettings>(MessageKeys.Wired.Room.SettingsUpdate);

            public static readonly MessageContract<WiredGetRoomStats> StatsRequest =
                Flash<WiredGetRoomStats>(MessageKeys.Wired.Room.StatsRequest);

            public static readonly MessageContract<WiredRoomStats> Stats =
                Flash<WiredRoomStats>(MessageKeys.Wired.Room.Stats);

            public static readonly MessageContract<WiredGetRoomLogs> LogsRequest =
                Flash<WiredGetRoomLogs>(MessageKeys.Wired.Room.LogsRequest);

            public static readonly MessageContract<WiredRoomLogs> Logs =
                Flash<WiredRoomLogs>(MessageKeys.Wired.Room.Logs);

            public static readonly MessageContract<WiredUpdateRoom> Update =
                Flash<WiredUpdateRoom>(MessageKeys.Wired.Room.Update);

            public static readonly MessageContract<WiredSetPreferences> PreferencesUpdate =
                Flash<WiredSetPreferences>(MessageKeys.Wired.Room.PreferencesUpdate);
        }

        public static class ErrorLogs
        {
            public static readonly MessageContract<WiredGetErrorLogs> Request =
                Flash<WiredGetErrorLogs>(MessageKeys.Wired.ErrorLogs.Request);

            public static readonly MessageContract<WiredErrorLogs> Snapshot =
                Flash<WiredErrorLogs>(MessageKeys.Wired.ErrorLogs.Snapshot);

            public static readonly MessageContract<WiredClearErrorLogs> Clear =
                Flash<WiredClearErrorLogs>(MessageKeys.Wired.ErrorLogs.Clear);
        }

        public static class UserClick
        {
            public static readonly MessageContract<WiredClickUser> Request =
                Flash<WiredClickUser>(MessageKeys.Wired.UserClick.Request);

            public static readonly MessageContract<WiredClickUserResponse> Result =
                Flash<WiredClickUserResponse>(MessageKeys.Wired.UserClick.Result);
        }

        public static class Variables
        {
            public static readonly MessageContract<WiredGetAllVariablesHash> HashRequest =
                Flash<WiredGetAllVariablesHash>(MessageKeys.Wired.Variables.HashRequest);

            public static readonly MessageContract<WiredAllVariablesHash> Hash =
                Flash<WiredAllVariablesHash>(MessageKeys.Wired.Variables.Hash);

            public static readonly MessageContract<WiredGetAllVariablesDiffs> DifferencesRequest =
                Flash<WiredGetAllVariablesDiffs>(MessageKeys.Wired.Variables.DifferencesRequest);

            public static readonly MessageContract<WiredAllVariablesDiffs> Differences =
                Flash<WiredAllVariablesDiffs>(MessageKeys.Wired.Variables.Differences);

            public static readonly MessageContract<WiredGetVariablesForObject> ObjectRequest =
                Flash<WiredGetVariablesForObject>(MessageKeys.Wired.Variables.ObjectRequest);

            public static readonly MessageContract<WiredVariablesForObject> Object =
                Flash<WiredVariablesForObject>(MessageKeys.Wired.Variables.Object);

            public static readonly MessageContract<WiredGetAllVariableHolders> HoldersRequest =
                Flash<WiredGetAllVariableHolders>(MessageKeys.Wired.Variables.HoldersRequest);

            public static readonly MessageContract<WiredAllVariableHolders> Holders =
                Flash<WiredAllVariableHolders>(MessageKeys.Wired.Variables.Holders);

            public static readonly MessageContract<WiredGetUserPermanentVariables> PermanentRequest =
                Flash<WiredGetUserPermanentVariables>(MessageKeys.Wired.Variables.PermanentRequest);

            public static readonly MessageContract<WiredUserPermanentVariables> Permanent =
                Flash<WiredUserPermanentVariables>(MessageKeys.Wired.Variables.Permanent);

            public static readonly MessageContract<WiredGetVariableOwnersPage> OwnersRequest =
                Flash<WiredGetVariableOwnersPage>(MessageKeys.Wired.Variables.OwnersRequest);

            public static readonly MessageContract<WiredUserVariablesList> Owners =
                Flash<WiredUserVariablesList>(MessageKeys.Wired.Variables.Owners);

            public static readonly MessageContract<WiredSetObjectVariableValue> ObjectValueSet =
                Flash<WiredSetObjectVariableValue>(MessageKeys.Wired.Variables.ObjectValueSet);

            public static readonly MessageContract<WiredSetUserPermanentVariable> PermanentValueSet =
                Flash<WiredSetUserPermanentVariable>(MessageKeys.Wired.Variables.PermanentValueSet);

            public static readonly MessageContract<WiredSetUserPermanentVariableResult> PermanentValueSetResult =
                Flash<WiredSetUserPermanentVariableResult>(MessageKeys.Wired.Variables.PermanentValueSetResult);
        }

        public static class Chests
        {
            public static readonly MessageContract<OpenChest> Opened =
                Flash<OpenChest>(MessageKeys.Wired.Chests.Opened);

            public static readonly MessageContract<CoinsChestContents> Coins =
                Flash<CoinsChestContents>(MessageKeys.Wired.Chests.Coins);

            public static readonly MessageContract<ItemsChestContentsChunk> ItemsChunk =
                Flash<ItemsChestContentsChunk>(MessageKeys.Wired.Chests.ItemsChunk);

            public static readonly MessageContract<ItemsChestContentsUpdated> ItemsUpdated =
                Flash<ItemsChestContentsUpdated>(MessageKeys.Wired.Chests.ItemsUpdated);

            public static readonly MessageContract<UpgradeChestResult> UpgradeResult =
                Flash<UpgradeChestResult>(MessageKeys.Wired.Chests.UpgradeResult);

            public static readonly MessageContract<ChestPreferencesUpdateSuccess> PreferencesUpdated =
                Flash<ChestPreferencesUpdateSuccess>(MessageKeys.Wired.Chests.PreferencesUpdated);

            public static readonly MessageContract<OpenChestAndGetContents> OpenRequest =
                Flash<OpenChestAndGetContents>(MessageKeys.Wired.Chests.OpenRequest);

            public static readonly MessageContract<CloseChest> Close =
                Flash<CloseChest>(MessageKeys.Wired.Chests.Close);

            public static readonly MessageContract<LockAllChests> LockAll =
                Flash<LockAllChests>(MessageKeys.Wired.Chests.LockAll);

            public static readonly MessageContract<UpgradeChest> Upgrade =
                Flash<UpgradeChest>(MessageKeys.Wired.Chests.Upgrade);

            public static readonly MessageContract<WithdrawAllFromChest> WithdrawAll =
                Flash<WithdrawAllFromChest>(MessageKeys.Wired.Chests.WithdrawAll);

            public static readonly MessageContract<WithdrawCoinsFromChest> WithdrawCoins =
                Flash<WithdrawCoinsFromChest>(MessageKeys.Wired.Chests.WithdrawCoins);

            public static readonly MessageContract<WithdrawItemsFromChest> WithdrawItems =
                Flash<WithdrawItemsFromChest>(MessageKeys.Wired.Chests.WithdrawItems);

            public static readonly MessageContract<StartAddingToChest> StartAdding =
                Flash<StartAddingToChest>(MessageKeys.Wired.Chests.StartAdding);

            public static readonly MessageContract<SetChestOptions> OptionsUpdate =
                Flash<SetChestOptions>(MessageKeys.Wired.Chests.OptionsUpdate);

            public static readonly MessageContract<SetChestPreferences> PreferencesUpdate =
                Flash<SetChestPreferences>(MessageKeys.Wired.Chests.PreferencesUpdate);

            public static readonly MessageContract<SetChestNotificationPreferences> NotificationPreferencesUpdate =
                Flash<SetChestNotificationPreferences>(MessageKeys.Wired.Chests.NotificationPreferencesUpdate);
        }

        public static class Transaction
        {
            public static readonly MessageContract<WiredTransactionSuccess> Succeeded =
                Flash<WiredTransactionSuccess>(MessageKeys.Wired.Transaction.Succeeded);

            public static readonly MessageContract<WiredTransactionFail> Failed =
                Flash<WiredTransactionFail>(MessageKeys.Wired.Transaction.Failed);

            public static readonly MessageContract<WiredTransactionGetChestLogs> ChestLogsRequest =
                Flash<WiredTransactionGetChestLogs>(MessageKeys.Wired.Transaction.ChestLogsRequest);

            public static readonly MessageContract<WiredTransactionGetRoomLogs> RoomLogsRequest =
                Flash<WiredTransactionGetRoomLogs>(MessageKeys.Wired.Transaction.RoomLogsRequest);

            public static readonly MessageContract<WiredTransactionLogList> Logs =
                Flash<WiredTransactionLogList>(MessageKeys.Wired.Transaction.Logs);

            public static readonly MessageContract<WiredTransactionGetLogDetails> LogDetailsRequest =
                Flash<WiredTransactionGetLogDetails>(MessageKeys.Wired.Transaction.LogDetailsRequest);

            public static readonly MessageContract<WiredTransactionLogDetails> LogDetails =
                Flash<WiredTransactionLogDetails>(MessageKeys.Wired.Transaction.LogDetails);
        }

        public static class Contracts
        {
            public static readonly MessageContract<WiredContractContents> Contents =
                Flash<WiredContractContents>(MessageKeys.Wired.Contracts.Contents);

            public static readonly MessageContract<WiredOpenContract> Opened =
                Flash<WiredOpenContract>(MessageKeys.Wired.Contracts.Opened);

            public static readonly MessageContract<WiredOpenContract> OpenRequest =
                Flash<WiredOpenContract>(MessageKeys.Wired.Contracts.OpenRequest);

            public static readonly MessageContract<WiredUpdateContract> Update =
                Flash<WiredUpdateContract>(MessageKeys.Wired.Contracts.Update);

            public static readonly MessageContract<WiredContractUpdateResult> UpdateResult =
                Flash<WiredContractUpdateResult>(MessageKeys.Wired.Contracts.UpdateResult);
        }

        public static class Trade
        {
            public static readonly MessageContract<WiredTradeInitiate> Initiated =
                Flash<WiredTradeInitiate>(MessageKeys.Wired.Trade.Initiated);

            public static readonly MessageContract<WiredTradeItemsUpdate> ItemsUpdated =
                Flash<WiredTradeItemsUpdate>(MessageKeys.Wired.Trade.ItemsUpdated);

            public static readonly MessageContract<WiredTradeCancelled> Cancelled =
                Flash<WiredTradeCancelled>(MessageKeys.Wired.Trade.Cancelled);

            public static readonly MessageContract<WiredTradeCompleted> Completed =
                Flash<WiredTradeCompleted>(MessageKeys.Wired.Trade.Completed);

            public static readonly MessageContract<WiredTradeAddDeleteItems> ItemsUpdate =
                Flash<WiredTradeAddDeleteItems>(MessageKeys.Wired.Trade.ItemsUpdate);

            public static readonly MessageContract<WiredTradeConfirm> Confirm =
                Flash<WiredTradeConfirm>(MessageKeys.Wired.Trade.Confirm);

            public static readonly MessageContract<WiredTradeCancel> Cancel =
                Flash<WiredTradeCancel>(MessageKeys.Wired.Trade.Cancel);

            public static readonly MessageContract<WiredTradeTransactionNotification> Notification =
                Flash<WiredTradeTransactionNotification>(MessageKeys.Wired.Trade.Notification);
        }
    }

    public static class Notifications
    {
        public static readonly MessageContract<MOTDNotification> MessageOfTheDay =
            Flash<MOTDNotification>(MessageKeys.Notifications.MessageOfTheDay);
    }

    public static class Polls
    {
        public static readonly MessageContract<PollContents> Contents =
            Flash<PollContents>(MessageKeys.Polls.Contents);

        public static readonly MessageContract<PollError> Error =
            Flash<PollError>(MessageKeys.Polls.Error);

        public static readonly MessageContract<PollOffer> Offer =
            Flash<PollOffer>(MessageKeys.Polls.Offer);

        public static readonly MessageContract<PollAnswer> Answer =
            Flash<PollAnswer>(MessageKeys.Polls.Answer);

        public static readonly MessageContract<RejectPoll> Reject =
            Flash<RejectPoll>(MessageKeys.Polls.Reject);

        public static readonly MessageContract<StartPoll> Start =
            Flash<StartPoll>(MessageKeys.Polls.Start);
    }

    public static class Room
    {
        public static readonly MessageContract<GetGuestRoomRequest> SnapshotRequest =
            Flash<GetGuestRoomRequest>(MessageKeys.Room.SnapshotRequest);

        public static readonly MessageContract<GuestRoomResult> Snapshot =
            Flash<GuestRoomResult>(MessageKeys.Room.Snapshot);

        public static readonly MessageContract<ToggleRoomStaffPickRequest> StaffPickUpdateRequest =
            Flash<ToggleRoomStaffPickRequest>(MessageKeys.Room.StaffPickUpdateRequest);

        public static readonly MessageContract<RateRoomRequest> RatingRequest =
            Flash<RateRoomRequest>(MessageKeys.Room.RatingRequest);

        public static class Settings
        {
            public static readonly MessageContract<GetRoomSettingsRequest> Request =
                Flash<GetRoomSettingsRequest>(MessageKeys.Room.Settings.Request);

            public static readonly MessageContract<RoomSettings> Snapshot =
                new(
                    MessageKeys.Room.Settings.Snapshot,
                    MessageCodec<RoomSettings>.FromModel());

            public static readonly MessageContract<RoomSettingsError> RequestFailed =
                Flash<RoomSettingsError>(MessageKeys.Room.Settings.RequestFailed);

            public static readonly MessageContract<SaveRoomSettingsRequest> Save =
                new(
                    MessageKeys.Room.Settings.Save,
                    MessageCodec<SaveRoomSettingsRequest>.FromModel());

            public static readonly MessageContract<RoomSettingsSaved> SaveSucceeded =
                Flash<RoomSettingsSaved>(MessageKeys.Room.Settings.SaveSucceeded);

            public static readonly MessageContract<RoomSettingsSaveError> SaveFailed =
                Flash<RoomSettingsSaveError>(MessageKeys.Room.Settings.SaveFailed);
        }

        public static class Access
        {
            public static readonly MessageContract<OpenFlatConnection> OpenRequest =
                Flash<OpenFlatConnection>(MessageKeys.Room.Access.OpenRequest);

            public static readonly MessageContract<OpenConnectionConfirmation> OpenConfirmed =
                Flash<OpenConnectionConfirmation>(MessageKeys.Room.Access.OpenConfirmed);

            public static readonly MessageContract<Doorbell> Doorbell =
                Flash<Doorbell>(MessageKeys.Room.Access.Doorbell);

            public static readonly MessageContract<AnswerDoorbellRequest> DoorbellAnswer =
                Flash<AnswerDoorbellRequest>(MessageKeys.Room.Access.DoorbellAnswer);

            public static readonly MessageContract<RoomQueueStatus> QueueStatus =
                Flash<RoomQueueStatus>(MessageKeys.Room.Access.QueueStatus);

            public static readonly MessageContract<FlatAccessible> Granted =
                Flash<FlatAccessible>(MessageKeys.Room.Access.Granted);

            public static readonly MessageContract<FlatAccessDenied> Denied =
                Flash<FlatAccessDenied>(MessageKeys.Room.Access.Denied);

            public static readonly MessageContract<NoSuchFlat> NotFound =
                Flash<NoSuchFlat>(MessageKeys.Room.Access.NotFound);

            public static readonly MessageContract<CanNotConnect> ConnectionFailed =
                Flash<CanNotConnect>(MessageKeys.Room.Access.ConnectionFailed);
        }

        public static class Lifecycle
        {
            public static readonly MessageContract<RoomReady> Ready =
                Flash<RoomReady>(MessageKeys.Room.Lifecycle.Ready);

            public static readonly MessageContract<RoomEntryInfo> Entry =
                Flash<RoomEntryInfo>(MessageKeys.Room.Lifecycle.Entry);

            public static readonly MessageContract<RoomForward> Forward =
                Flash<RoomForward>(MessageKeys.Room.Lifecycle.Forward);

            public static readonly MessageContract<CloseConnection> ConnectionClosed =
                Flash<CloseConnection>(MessageKeys.Room.Lifecycle.ConnectionClosed);

            public static readonly MessageContract<QuitRoomRequest> Quit =
                Flash<QuitRoomRequest>(MessageKeys.Room.Lifecycle.Quit);
        }

        public static class Environment
        {
            public static readonly MessageContract<RoomEntryTile> EntryTile =
                Flash<RoomEntryTile>(MessageKeys.Room.Environment.EntryTile);

            public static readonly MessageContract<FlatProperty> Property =
                Flash<FlatProperty>(MessageKeys.Room.Environment.Property);

            public static readonly MessageContract<RoomVisualizationSettings> Visualization =
                Flash<RoomVisualizationSettings>(MessageKeys.Room.Environment.Visualization);

            public static readonly MessageContract<RoomChatSettings> ChatSettings =
                Flash<RoomChatSettings>(MessageKeys.Room.Environment.ChatSettings);

            public static readonly MessageContract<FloorPlan> FloorPlan =
                Flash<FloorPlan>(MessageKeys.Room.Environment.FloorPlan);
        }

        public static class Chat
        {
            public static readonly MessageContract<AvatarChat> Talk =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Talk);

            public static readonly MessageContract<AvatarChat> Shout =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Shout);

            public static readonly MessageContract<AvatarChat> Whisper =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Whisper);

            public static readonly MessageContract<WhisperRequest> WhisperSend =
                Flash<WhisperRequest>(MessageKeys.Room.Chat.WhisperSend);

            public static readonly MessageContract<SpecialSystemChat> SpecialSystem =
                Flash<SpecialSystemChat>(MessageKeys.Room.Chat.SpecialSystem);

            public static readonly MessageContract<TalkRequest> TalkSend =
                Flash<TalkRequest>(MessageKeys.Room.Chat.TalkSend);

            public static readonly MessageContract<ShoutRequest> ShoutSend =
                Flash<ShoutRequest>(MessageKeys.Room.Chat.ShoutSend);
        }

        public static class Authority
        {
            public static readonly MessageContract<GetFlatControllersRequest> ControllersRequest =
                Flash<GetFlatControllersRequest>(MessageKeys.Room.Authority.ControllersRequest);

            public static readonly MessageContract<RightsList> ControllersSnapshot =
                Flash<RightsList>(MessageKeys.Room.Authority.ControllersSnapshot);

            public static readonly MessageContract<GiveRoomRightsRequest> ControllerGrantRequest =
                Flash<GiveRoomRightsRequest>(MessageKeys.Room.Authority.ControllerGrantRequest);

            public static readonly MessageContract<YouAreController> ControllerGranted =
                Flash<YouAreController>(MessageKeys.Room.Authority.ControllerGranted);

            public static readonly MessageContract<YouAreNotController> ControllerRevoked =
                Flash<YouAreNotController>(MessageKeys.Room.Authority.ControllerRevoked);

            public static readonly MessageContract<YouAreOwner> Owner =
                Flash<YouAreOwner>(MessageKeys.Room.Authority.Owner);

            public static readonly MessageContract<YouAreSpectator> SpectatorGranted =
                Flash<YouAreSpectator>(MessageKeys.Room.Authority.SpectatorGranted);

            public static readonly MessageContract<YouAreNotSpectator> SpectatorRevoked =
                Flash<YouAreNotSpectator>(MessageKeys.Room.Authority.SpectatorRevoked);
        }

        public static class Occupants
        {
            public static readonly MessageContract<RoomUsers> Snapshot =
                Flash<RoomUsers>(MessageKeys.Room.Occupants.Snapshot);

            public static readonly MessageContract<AvatarRemove> Removed =
                Flash<AvatarRemove>(MessageKeys.Room.Occupants.Removed);

            public static readonly MessageContract<UserUpdate> Status =
                Flash<UserUpdate>(MessageKeys.Room.Occupants.Status);

            public static readonly MessageContract<RespectNotification> Respect =
                Flash<RespectNotification>(MessageKeys.Room.Occupants.Respect);

            public static readonly MessageContract<RespectUserRequest> RespectRequest =
                Flash<RespectUserRequest>(MessageKeys.Room.Occupants.RespectRequest);

            public static class Action
            {
                public static readonly MessageContract<AvatarDanceUpdate> Dance =
                    Flash<AvatarDanceUpdate>(MessageKeys.Room.Occupants.Action.Dance);

                public static readonly MessageContract<AvatarDanceRequest> DanceRequest =
                    Flash<AvatarDanceRequest>(MessageKeys.Room.Occupants.Action.DanceRequest);

                public static readonly MessageContract<AvatarSignRequest> SignRequest =
                    Flash<AvatarSignRequest>(MessageKeys.Room.Occupants.Action.SignRequest);

                public static readonly MessageContract<AvatarEffectUpdate> Effect =
                    Flash<AvatarEffectUpdate>(MessageKeys.Room.Occupants.Action.Effect);

                public static readonly MessageContract<AvatarEffectSelectionRequest> EffectSelectionRequest =
                    Flash<AvatarEffectSelectionRequest>(MessageKeys.Room.Occupants.Action.EffectSelectionRequest);

                public static readonly MessageContract<AvatarPostureRequest> PostureRequest =
                    Flash<AvatarPostureRequest>(MessageKeys.Room.Occupants.Action.PostureRequest);

                public static readonly MessageContract<AvatarCarryUpdate> Carry =
                    Flash<AvatarCarryUpdate>(MessageKeys.Room.Occupants.Action.Carry);

                public static readonly MessageContract<AvatarSleepUpdate> Sleep =
                    Flash<AvatarSleepUpdate>(MessageKeys.Room.Occupants.Action.Sleep);

                public static readonly MessageContract<AvatarTypingUpdate> Typing =
                    Flash<AvatarTypingUpdate>(MessageKeys.Room.Occupants.Action.Typing);

                public static readonly MessageContract<AvatarAction> Expression =
                    Flash<AvatarAction>(MessageKeys.Room.Occupants.Action.Expression);

                public static readonly MessageContract<AvatarExpressionRequest> ExpressionRequest =
                    Flash<AvatarExpressionRequest>(MessageKeys.Room.Occupants.Action.ExpressionRequest);
            }

            public static class Identity
            {
                public static readonly MessageContract<UserChanged> Appearance =
                    Flash<UserChanged>(MessageKeys.Room.Occupants.Identity.Appearance);

                public static readonly MessageContract<UserNameChanged> Name =
                    Flash<UserNameChanged>(MessageKeys.Room.Occupants.Identity.Name);

                public static readonly MessageContract<FavoriteMembershipUpdate> FavoriteGroup =
                    Flash<FavoriteMembershipUpdate>(MessageKeys.Room.Occupants.Identity.FavoriteGroup);
            }

            public static class Pet
            {
                public static readonly MessageContract<GetPetInfoRequest> InfoRequest =
                    Flash<GetPetInfoRequest>(MessageKeys.Room.Occupants.Pet.InfoRequest);

                public static readonly MessageContract<PetInfo> Info =
                    Flash<PetInfo>(MessageKeys.Room.Occupants.Pet.Info);

                public static readonly MessageContract<PetFigureUpdate> Figure =
                    Flash<PetFigureUpdate>(MessageKeys.Room.Occupants.Pet.Figure);

                public static readonly MessageContract<PetStatusUpdate> Status =
                    Flash<PetStatusUpdate>(MessageKeys.Room.Occupants.Pet.Status);

                public static readonly MessageContract<PetLevelUpdate> Level =
                    Flash<PetLevelUpdate>(MessageKeys.Room.Occupants.Pet.Level);

                public static readonly MessageContract<RespectPetRequest> RespectRequest =
                    Flash<RespectPetRequest>(MessageKeys.Room.Occupants.Pet.RespectRequest);

                public static readonly MessageContract<MountPetRequest> MountRequest =
                    Flash<MountPetRequest>(MessageKeys.Room.Occupants.Pet.MountRequest);

                public static readonly MessageContract<RemovePetFromRoomRequest> RemoveRequest =
                    Flash<RemovePetFromRoomRequest>(MessageKeys.Room.Occupants.Pet.RemoveRequest);
            }

            public static class Bot
            {
                public static readonly MessageContract<RemoveBotFromFlat> RemoveRequest =
                    Flash<RemoveBotFromFlat>(MessageKeys.Room.Occupants.Bot.RemoveRequest);
            }
        }

        public static class HandItem
        {
            public static readonly MessageContract<HandItemReceived> Received =
                Flash<HandItemReceived>(MessageKeys.Room.HandItem.Received);

            public static readonly MessageContract<DropHandItemRequest> Drop =
                Flash<DropHandItemRequest>(MessageKeys.Room.HandItem.Drop);

            public static readonly MessageContract<PassHandItemRequest> Pass =
                Flash<PassHandItemRequest>(MessageKeys.Room.HandItem.Pass);
        }

        public static readonly MessageContract<UseFloorItemRequest> FloorItemUse =
            Flash<UseFloorItemRequest>(MessageKeys.Room.FloorItem.Use);

        public static readonly MessageContract<UseWallItemRequest> WallItemUse =
            Flash<UseWallItemRequest>(MessageKeys.Room.WallItem.Use);

        public static readonly MessageContract<RemoveWallItemRequest> WallItemRemove =
            Flash<RemoveWallItemRequest>(MessageKeys.Room.WallItem.Remove);

        public static readonly MessageContract<PlaceRoomItemRequest> ItemPlace =
            new(
                MessageKeys.Room.Item.Place,
                new MessageCodec<PlaceRoomItemRequest>(PlaceRoomItemRequest.ParseFlash,
                    PlaceRoomItemRequest.ComposeFlash));

        public static readonly MessageContract<MoveFloorItemRequest> FloorItemMove =
            Flash<MoveFloorItemRequest>(MessageKeys.Room.FloorItem.Move);

        public static readonly MessageContract<MoveWallItemRequest> WallItemMove =
            new(
                MessageKeys.Room.WallItem.Move,
                MessageCodec<MoveWallItemRequest>.FromModel());

        public static readonly MessageContract<PickupRoomItemRequest> ItemPickup =
            Flash<PickupRoomItemRequest>(MessageKeys.Room.Item.Pickup);

        public static readonly MessageContract<PickupConfirmation> ItemPickupConfirmation =
            Flash<PickupConfirmation>(MessageKeys.Room.Item.PickupConfirmation);

        public static class FloorItem
        {
            public static readonly MessageContract<FloorItemAdd> Added =
                Flash<FloorItemAdd>(MessageKeys.Room.FloorItem.Added);

            public static readonly MessageContract<FloorItemRemove> Removed =
                Flash<FloorItemRemove>(MessageKeys.Room.FloorItem.Removed);

            public static readonly MessageContract<FloorItemUpdate> Updated =
                Flash<FloorItemUpdate>(MessageKeys.Room.FloorItem.Updated);

            public static readonly MessageContract<ThrowDiceRequest> ThrowDice =
                Flash<ThrowDiceRequest>(MessageKeys.Room.FloorItem.ThrowDice);

            public static readonly MessageContract<DiceOffRequest> DiceOff =
                Flash<DiceOffRequest>(MessageKeys.Room.FloorItem.DiceOff);

            public static readonly MessageContract<DiceValue> DiceValue =
                Flash<DiceValue>(MessageKeys.Room.FloorItem.DiceValue);

            public static readonly MessageContract<OneWayDoorStatus> OneWayDoorStatus =
                Flash<OneWayDoorStatus>(MessageKeys.Room.FloorItem.OneWayDoorStatus);

            public static readonly MessageContract<EnterOneWayDoorRequest> OneWayDoorEnter =
                Flash<EnterOneWayDoorRequest>(MessageKeys.Room.FloorItem.OneWayDoorEnter);
        }

        public static class WallItem
        {
            public static readonly MessageContract<WallItemAdd> Added =
                Flash<WallItemAdd>(MessageKeys.Room.WallItem.Added);

            public static readonly MessageContract<WallItemRemove> Removed =
                Flash<WallItemRemove>(MessageKeys.Room.WallItem.Removed);

            public static readonly MessageContract<WallItemUpdate> Updated =
                Flash<WallItemUpdate>(MessageKeys.Room.WallItem.Updated);

            public static readonly MessageContract<SetStickyDataRequest> StickyDataSet =
                Flash<SetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataSet);

            public static readonly MessageContract<GetStickyDataRequest> StickyDataRequest =
                Flash<GetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataRequest);

            public static readonly MessageContract<Sticky> StickyData =
                Flash<Sticky>(MessageKeys.Room.WallItem.StickyData);

            public static readonly MessageContract<PlacePostItRequest> PostItPlace =
                Flash<PlacePostItRequest>(MessageKeys.Room.WallItem.PostItPlace);

            public static readonly MessageContract<AddSpamWallPostItRequest> SpamPostItAdd =
                Flash<AddSpamWallPostItRequest>(MessageKeys.Room.WallItem.SpamPostItAdd);
        }

        public static class Movement
        {
            public static readonly MessageContract<WalkRequest> Walk =
                Flash<WalkRequest>(MessageKeys.Room.Movement.Walk);

            public static readonly MessageContract<LookToRequest> LookTo =
                Flash<LookToRequest>(MessageKeys.Room.Movement.LookTo);

            public static readonly MessageContract<SlideObjectBundle> Slide =
                Flash<SlideObjectBundle>(MessageKeys.Room.Movement.Slide);

            public static readonly MessageContract<WiredMovements> Wired =
                Flash<WiredMovements>(MessageKeys.Room.Movement.Wired);
        }

        public static class Typing
        {
            public static readonly MessageContract<StartTypingRequest> Start =
                Flash<StartTypingRequest>(MessageKeys.Room.Typing.Start);

            public static readonly MessageContract<CancelTypingRequest> Cancel =
                Flash<CancelTypingRequest>(MessageKeys.Room.Typing.Cancel);
        }

        public static class Moderation
        {
            public static readonly MessageContract<GetRoomBansRequest> BansRequest =
                Flash<GetRoomBansRequest>(MessageKeys.Room.Moderation.BansRequest);

            public static readonly MessageContract<BannedUsersFromRoom> BansSnapshot =
                Flash<BannedUsersFromRoom>(MessageKeys.Room.Moderation.BansSnapshot);

            public static readonly MessageContract<UserUnbannedFromRoom> UserUnbanned =
                Flash<UserUnbannedFromRoom>(MessageKeys.Room.Moderation.UserUnbanned);

            public static readonly MessageContract<MuteRoomUserRequest> UserMute =
                Flash<MuteRoomUserRequest>(MessageKeys.Room.Moderation.Mute);

            public static readonly MessageContract<KickRoomUserRequest> UserKick =
                Flash<KickRoomUserRequest>(MessageKeys.Room.Moderation.Kick);

            public static readonly MessageContract<BanRoomUserRequest> UserBan =
                Flash<BanRoomUserRequest>(MessageKeys.Room.Moderation.Ban);

            public static readonly MessageContract<UnbanRoomUserRequest> UserUnban =
                Flash<UnbanRoomUserRequest>(MessageKeys.Room.Moderation.Unban);
        }
    }

    public static class Friends
    {
        public static readonly MessageContract<FriendInitializationRequest> InitializeRequest =
            Flash<FriendInitializationRequest>(MessageKeys.Friends.InitializeRequest);

        public static readonly MessageContract<MessengerInit> Initialized =
            Flash<MessengerInit>(MessageKeys.Friends.Initialized);

        public static readonly MessageContract<FriendListFragment> ListFragment =
            Flash<FriendListFragment>(MessageKeys.Friends.ListFragment);

        public static readonly MessageContract<FriendListUpdate> ListUpdated =
            Flash<FriendListUpdate>(MessageKeys.Friends.ListUpdated);

        public static readonly MessageContract<SendPrivateMessage> PrivateMessageSend =
            new(
                MessageKeys.Friends.PrivateMessageSend,
                MessageCodec<SendPrivateMessage>.FromModel());

        public static readonly MessageContract<NewConsoleMessage> PrivateMessageReceived =
            new(
                MessageKeys.Friends.PrivateMessageReceived,
                MessageCodec<NewConsoleMessage>.FromModel());

        public static readonly MessageContract<MessengerError> OperationFailed =
            Flash<MessengerError>(MessageKeys.Friends.OperationFailed);

        public static readonly MessageContract<InstantMessageError> PrivateMessageFailed =
            Flash<InstantMessageError>(MessageKeys.Friends.PrivateMessageFailed);

        public static readonly MessageContract<FriendRequest> FriendRequestSend =
            Flash<FriendRequest>(MessageKeys.Friends.FriendRequestSend);

        public static readonly MessageContract<NewFriendRequest> FriendRequestReceived =
            Flash<NewFriendRequest>(MessageKeys.Friends.FriendRequestReceived);

        public static readonly MessageContract<PendingFriendRequestsRequest> FriendRequestsRequest =
            Flash<PendingFriendRequestsRequest>(MessageKeys.Friends.FriendRequestsRequest);

        public static readonly MessageContract<PendingFriendRequests> FriendRequestsSnapshot =
            Flash<PendingFriendRequests>(MessageKeys.Friends.FriendRequestsSnapshot);

        public static readonly MessageContract<AcceptFriends> FriendRequestAccept =
            Flash<AcceptFriends>(MessageKeys.Friends.FriendRequestAccept);

        public static readonly MessageContract<DeclineFriends> FriendRequestDecline =
            Flash<DeclineFriends>(MessageKeys.Friends.FriendRequestDecline);

        public static readonly MessageContract<RemoveFriends> Remove =
            Flash<RemoveFriends>(MessageKeys.Friends.Remove);

        public static readonly MessageContract<FollowFriendRequest> Follow =
            Flash<FollowFriendRequest>(MessageKeys.Friends.Follow);

        public static readonly MessageContract<FriendSearchRequest> SearchRequest =
            Flash<FriendSearchRequest>(MessageKeys.Friends.SearchRequest);

        public static readonly MessageContract<UserSearchResults> SearchResult =
            Flash<UserSearchResults>(MessageKeys.Friends.SearchResult);

        public static readonly MessageContract<SetFriendRelationshipRequest> RelationshipSet =
            Flash<SetFriendRelationshipRequest>(MessageKeys.Friends.RelationshipSet);
    }

    public static class Trade
    {
        public static readonly MessageContract<TradeOpened> Opened =
            Flash<TradeOpened>(MessageKeys.Trade.Opened);

        public static readonly MessageContract<TradeOffers> Offers =
            Flash<TradeOffers>(MessageKeys.Trade.Offers);

        public static readonly MessageContract<TradeAccepted> AcceptanceUpdated =
            Flash<TradeAccepted>(MessageKeys.Trade.AcceptanceUpdated);

        public static readonly MessageContract<TradeConfirmation> Confirmation =
            Flash<TradeConfirmation>(MessageKeys.Trade.Confirmation);

        public static readonly MessageContract<TradeCompleted> Completed =
            Flash<TradeCompleted>(MessageKeys.Trade.Completed);

        public static readonly MessageContract<TradeClosed> Closed =
            Flash<TradeClosed>(MessageKeys.Trade.Closed);

        public static readonly MessageContract<TradeOpenFailed> OpenFailed =
            Flash<TradeOpenFailed>(MessageKeys.Trade.OpenFailed);

        public static readonly MessageContract<TradeNftAssets> NftOffers =
            Flash<TradeNftAssets>(MessageKeys.Trade.NftOffers);

        public static readonly MessageContract<TradeNftAssetInventory> NftInventory =
            Flash<TradeNftAssetInventory>(MessageKeys.Trade.NftInventory);

        public static readonly MessageContract<TradeSilverSet> SilverUpdated =
            Flash<TradeSilverSet>(MessageKeys.Trade.SilverUpdated);

        public static readonly MessageContract<TradeSilverFee> SilverFee =
            Flash<TradeSilverFee>(MessageKeys.Trade.SilverFee);

        public static readonly MessageContract<OpenTradeRequest> OpenRequest =
            Flash<OpenTradeRequest>(MessageKeys.Trade.OpenRequest);

        public static readonly MessageContract<AddTradeItemsRequest> ItemsAdd =
            Flash<AddTradeItemsRequest>(MessageKeys.Trade.ItemsAdd);

        public static readonly MessageContract<RemoveTradeItemRequest> ItemRemove =
            Flash<RemoveTradeItemRequest>(MessageKeys.Trade.ItemRemove);

        public static readonly MessageContract<AcceptTradeRequest> Accept =
            Flash<AcceptTradeRequest>(MessageKeys.Trade.Accept);

        public static readonly MessageContract<UnacceptTradeRequest> Unaccept =
            Flash<UnacceptTradeRequest>(MessageKeys.Trade.Unaccept);

        public static readonly MessageContract<ConfirmTradeRequest> Confirm =
            Flash<ConfirmTradeRequest>(MessageKeys.Trade.Confirm);

        public static readonly MessageContract<CloseTradeRequest> Close =
            Flash<CloseTradeRequest>(MessageKeys.Trade.Close);

        public static readonly MessageContract<GetNftTradeInventoryRequest> NftInventoryRequest =
            Flash<GetNftTradeInventoryRequest>(MessageKeys.Trade.NftInventoryRequest);
    }

    public static class Users
    {
        public static class Relationship
        {
            public static readonly MessageContract<RelationshipStatusRequest> Request =
                Flash<RelationshipStatusRequest>(MessageKeys.Users.Relationship.Request);

            public static readonly MessageContract<RelationshipStatus> Snapshot =
                Flash<RelationshipStatus>(MessageKeys.Users.Relationship.Snapshot);
        }

        public static class Block
        {
            public static readonly MessageContract<BlockListRequest> ListRequest =
                Flash<BlockListRequest>(MessageKeys.Users.Block.ListRequest);

            public static readonly MessageContract<BlockList> ListSnapshot =
                Flash<BlockList>(MessageKeys.Users.Block.ListSnapshot);

            public static readonly MessageContract<BlockUserUpdate> Updated =
                Flash<BlockUserUpdate>(MessageKeys.Users.Block.Updated);

            public static readonly MessageContract<BlockUserRequest> Add =
                Flash<BlockUserRequest>(MessageKeys.Users.Block.Add);

            public static readonly MessageContract<UnblockUserRequest> Remove =
                Flash<UnblockUserRequest>(MessageKeys.Users.Block.Remove);
        }

        public static class Ignore
        {
            public static readonly MessageContract<IgnoreListRequest> ListRequest =
                Flash<IgnoreListRequest>(MessageKeys.Users.Ignore.ListRequest);

            public static readonly MessageContract<RequestIgnoreList> ListSnapshot =
                Flash<RequestIgnoreList>(MessageKeys.Users.Ignore.ListSnapshot);

            public static readonly MessageContract<IgnoreUserResult> Updated =
                Flash<IgnoreUserResult>(MessageKeys.Users.Ignore.Updated);

            public static readonly MessageContract<IgnoreUserByIdRequest> AddByIdRequest =
                Flash<IgnoreUserByIdRequest>(MessageKeys.Users.Ignore.AddByIdRequest);

            public static readonly MessageContract<UnignoreUserRequest> Remove =
                new(
                    MessageKeys.Users.Ignore.Remove,
                    MessageCodec<UnignoreUserRequest>.FromModel(static (_, _) => MessageCapability.Ready("flashUnignoreIdSchema")));
        }

        public static class FigureSets
        {
            public static readonly MessageContract<FigureSetIdAdded> Added =
                Flash<FigureSetIdAdded>(MessageKeys.Users.FigureSets.Added);

            public static readonly MessageContract<FigureSetIdRemoved> Removed =
                Flash<FigureSetIdRemoved>(MessageKeys.Users.FigureSets.Removed);

            public static readonly MessageContract<FigureSetIds> Snapshot =
                Flash<FigureSetIds>(MessageKeys.Users.FigureSets.Snapshot);
        }

        public static class Sanctions
        {
            public static readonly MessageContract<SanctionStatusRequest> Request =
                Flash<SanctionStatusRequest>(MessageKeys.Users.Sanctions.Request);

            public static readonly MessageContract<AccountSanctionStatus> Snapshot =
                Flash<AccountSanctionStatus>(MessageKeys.Users.Sanctions.Snapshot);
        }

        public static class FavoriteGroup
        {
            public static readonly MessageContract<SelectFavoriteGroupRequest> Select =
                Flash<SelectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Select);

            public static readonly MessageContract<DeselectFavoriteGroupRequest> Deselect =
                Flash<DeselectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Deselect);
        }

        public static readonly MessageContract<MottoUpdateRequest> MottoUpdate =
            Flash<MottoUpdateRequest>(MessageKeys.Users.MottoUpdate);

        public static readonly MessageContract<ProfileRequest> ProfileRequest =
            Flash<ProfileRequest>(MessageKeys.Users.ProfileRequest);

        public static readonly MessageContract<UserData> ProfileSnapshot =
            Flash<UserData>(MessageKeys.Users.ProfileSnapshot);

        public static readonly MessageContract<FigureUpdate> FigureUpdated =
            Flash<FigureUpdate>(MessageKeys.Users.FigureUpdated);

        public static readonly MessageContract<ChangeUserNameResult> NameChangeResult =
            Flash<ChangeUserNameResult>(MessageKeys.Users.NameChangeResult);

        public static readonly MessageContract<AccountSafetyLockStatusChange> SafetyLockChanged =
            Flash<AccountSafetyLockStatusChange>(MessageKeys.Users.SafetyLockChanged);

        public static readonly MessageContract<ExtendedProfileRequest> ExtendedProfileRequest =
            Flash<ExtendedProfileRequest>(MessageKeys.Users.ExtendedProfileRequest);

        public static readonly MessageContract<UserProfile> ExtendedProfileSnapshot =
            Flash<UserProfile>(MessageKeys.Users.ExtendedProfileSnapshot);
    }

    private static MessageContract<NavigatorSearchResult> LegacyNavigatorSearchResult() =>
        new(
            MessageKeys.Navigator.Search.LegacyResult,
            new MessageCodec<NavigatorSearchResult>(ParseLegacyNavigatorSearchResult,
                ComposeLegacyNavigatorSearchResult));

    private static NavigatorSearchResult ParseLegacyNavigatorSearchResult(
        in PacketReader reader)
    {
        int search_type = reader.ReadInt();
        string filter = reader.ReadString();
        int count = reader.ReadLength();
        if (count > reader.Available / (40))
            throw new InvalidDataException("The legacy navigator room count exceeds the packet capacity.");

        var rooms = new RoomData[count];
        for (int i = 0; i < rooms.Length; i++)
            rooms[i] = reader.Parse<RoomData>();

        bool has_promotion = reader.ReadBool();
        if (has_promotion)
            ParseLegacyNavigatorPromotion(in reader);
        ParseLegacyNavigatorResultFlags(in reader);

        string search_code = search_type == 8 ? "query" : $"legacy:{search_type}";
        return new NavigatorSearchResult(
            search_code,
            filter,
            [
                new NavigatorSearchBlock(
                    search_code,
                    "",
                    0,
                    false,
                    0,
                    rooms)
            ]);
    }

    private static void ParseLegacyNavigatorResultFlags(in PacketReader reader)
    {
        if (reader.Available == 0)
            return;
        throw new InvalidDataException("The legacy navigator result contains an unexpected trailing payload.");
    }

    private static void ParseLegacyNavigatorPromotion(in PacketReader reader)
    {
        reader.ReadId();
        reader.ReadString();
        reader.ReadString();
        reader.ReadBool();
        reader.ReadString();
        reader.ReadString();
        reader.ReadInt();
        reader.ReadInt();
        reader.ReadInt();
        int count = reader.ReadLength();
        if (count > reader.Available / (40))
            throw new InvalidDataException("The promoted navigator room count exceeds the packet capacity.");
        for (int i = 0; i < count; i++)
            reader.Parse<RoomData>();
        if (reader.Available != 0)
            throw new InvalidDataException("The promoted navigator result contains an unexpected trailing payload.");
    }

    private static void ComposeLegacyNavigatorSearchResult(
        NavigatorSearchResult result,
        in PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(result);
        RoomData[] rooms = result.Blocks
            .SelectMany(block => block.Rooms)
            .ToArray();
        if (rooms.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(result));

        writer.WriteInt(8);
        writer.WriteString(result.Filter);
        writer.WriteLength((Length)rooms.Length);
        foreach (RoomData room in rooms)
        {
            ArgumentNullException.ThrowIfNull(room);
            writer.Compose(room);
        }
        writer.WriteBool(false);
    }

    private static MessageContract<CallForHelpFromForumThread> ForumThreadReport() =>
        new(
            MessageKeys.Forums.ThreadReport,
            MessageCodec<CallForHelpFromForumThread>.FromModel());

    private static MessageContract<CallForHelpFromForumMessage> ForumMessageReport() =>
        new(
            MessageKeys.Forums.MessageReport,
            MessageCodec<CallForHelpFromForumMessage>.FromModel());

    private static MessageContract<T> Flash<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(key, MessageCodec<T>.FromModel());

    private static MessageContract<T> MarketplaceLayout<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageCodec<T>.FromModel(FlashMarketplaceLayoutCapability));

    private static MessageContract<T> ModernFlashMarketplace<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageCodec<T>.FromModel(ModernFlashMarketplaceCapability));

    private static MessageCapability FlashMarketplaceLayoutCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Flash);
        if (!profile.IsAnalyzed)
        {
            return MessageCapability.Missing(
                "flashMarketplaceLayout",
                "The Flash client catalog is still loading its marketplace layout.");
        }
        return profile.FlashMarketplaceLayout is FlashMarketplaceWireLayout.Unknown
            ? MessageCapability.Missing(
                "flashMarketplaceLayout",
                "The active Flash build has no exact marketplace wire profile.")
            : MessageCapability.Ready("flashMarketplaceLayout");
    }

    private static MessageCapability ModernFlashMarketplaceCapability(
        MessageManager messages,
        Header header)
    {
        MessageCapability layout =
            FlashMarketplaceLayoutCapability(messages, header);
        if (!layout.Available)
            return layout;
        return messages.GetWireProfile(ClientType.Flash).FlashMarketplaceLayout is
            FlashMarketplaceWireLayout.Modern
                ? MessageCapability.Ready("flashMarketplaceModernLayout")
                : MessageCapability.Missing(
                    "flashMarketplaceModernLayout",
                    "The active Flash build uses the legacy marketplace layout.");
    }
}
