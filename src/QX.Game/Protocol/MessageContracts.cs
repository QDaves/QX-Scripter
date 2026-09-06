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
        Room.Lifecycle.NativeExit,
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
        Room.Authority.SpectatingEnded,
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
        Users.Ignore.AddByNameRequest,
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
            Modern<GenericError>(MessageKeys.Errors.Generic);
    }

    public static class Session
    {
        public static readonly MessageContract<DisconnectReason> DisconnectReason =
            Modern<DisconnectReason>(MessageKeys.Session.DisconnectReason);
    }

    public static class Achievements
    {
        public static readonly MessageContract<AchievementsRequest> Request =
            Modern<AchievementsRequest>(MessageKeys.Achievements.Request);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Achievements> Snapshot =
            Modern<Qx.Model.Messages.Incoming.Achievements>(MessageKeys.Achievements.Snapshot);

        public static readonly MessageContract<AchievementUpdate> Updated =
            Modern<AchievementUpdate>(MessageKeys.Achievements.Updated);

        public static readonly MessageContract<AchievementScore> Score =
            Flash<AchievementScore>(MessageKeys.Achievements.Score);

        public static readonly MessageContract<BadgePointLimitsRequest> PointLimitsRequest =
            Modern<BadgePointLimitsRequest>(MessageKeys.Achievements.PointLimitsRequest);

        public static readonly MessageContract<BadgePointLimits> PointLimits =
            Modern<BadgePointLimits>(MessageKeys.Achievements.PointLimits);

        public static readonly MessageContract<AchievementNotification> Notification =
            Modern<AchievementNotification>(MessageKeys.Achievements.Notification);
    }

    public static class Badges
    {
        public static readonly MessageContract<BadgeInventoryRequest> Request =
            Modern<BadgeInventoryRequest>(MessageKeys.Badges.Request);

        public static readonly MessageContract<BadgeInventory> Snapshot =
            Modern<BadgeInventory>(MessageKeys.Badges.Snapshot);

        public static readonly MessageContract<SelectedBadgesRequest> SelectedRequest =
            Modern<SelectedBadgesRequest>(MessageKeys.Badges.SelectedRequest);

        public static readonly MessageContract<BadgeReceived> Received =
            Modern<BadgeReceived>(MessageKeys.Badges.Received);

        public static readonly MessageContract<UserBadges> Selected =
            Modern<UserBadges>(MessageKeys.Badges.Selected);
    }

    public static class Wallet
    {
        public static readonly MessageContract<WalletBalanceRequest> CreditsRequest =
            Modern<WalletBalanceRequest>(MessageKeys.Wallet.CreditsRequest);

        public static readonly MessageContract<CreditBalance> CreditsBalance =
            Modern<CreditBalance>(MessageKeys.Wallet.CreditsBalance);

        public static readonly MessageContract<ActivityPoints> ActivityPoints =
            Modern<ActivityPoints>(MessageKeys.Wallet.ActivityPoints);

        public static readonly MessageContract<ActivityPointNotification> ActivityPointUpdated =
            Modern<ActivityPointNotification>(MessageKeys.Wallet.ActivityPointUpdated);
    }

    public static class Earnings
    {
        public static readonly MessageContract<EarningStatusRequest> StatusRequest =
            Modern<EarningStatusRequest>(MessageKeys.Earnings.StatusRequest);

        public static readonly MessageContract<EarningStatus> StatusSnapshot =
            Modern<EarningStatus>(MessageKeys.Earnings.StatusSnapshot);

        public static readonly MessageContract<EarningClaimRequest> Claim =
            Modern<EarningClaimRequest>(MessageKeys.Earnings.Claim);

        public static readonly MessageContract<EarningClaimResult> Claimed =
            Modern<EarningClaimResult>(MessageKeys.Earnings.Claimed);

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
            Modern<GetQuests>(MessageKeys.Quests.Request);

        public static readonly MessageContract<global::Qx.Model.Messages.Incoming.Quests> Snapshot =
            Modern<global::Qx.Model.Messages.Incoming.Quests>(MessageKeys.Quests.Snapshot);

        public static readonly MessageContract<GetSeasonalQuests> SeasonalRequest =
            Modern<GetSeasonalQuests>(MessageKeys.Quests.SeasonalRequest);

        public static readonly MessageContract<QuestsSeasonal> SeasonalSnapshot =
            Modern<QuestsSeasonal>(MessageKeys.Quests.SeasonalSnapshot);

        public static readonly MessageContract<Quest> Updated =
            Modern<Quest>(MessageKeys.Quests.Updated);

        public static readonly MessageContract<QuestCompleted> Completed =
            Modern<QuestCompleted>(MessageKeys.Quests.Completed);

        public static readonly MessageContract<QuestCancelled> Cancelled =
            Modern<QuestCancelled>(MessageKeys.Quests.Cancelled);

        public static readonly MessageContract<GetDailyQuest> DailyRequest =
            Modern<GetDailyQuest>(MessageKeys.Quests.DailyRequest);

        public static readonly MessageContract<QuestDaily> Daily =
            Modern<QuestDaily>(MessageKeys.Quests.Daily);

        public static readonly MessageContract<AcceptQuest> Accept =
            Modern<AcceptQuest>(MessageKeys.Quests.Accept);

        public static readonly MessageContract<ActivateQuest> Activate =
            Modern<ActivateQuest>(MessageKeys.Quests.Activate);

        public static readonly MessageContract<RejectQuest> Reject =
            Modern<RejectQuest>(MessageKeys.Quests.Reject);

        public static readonly MessageContract<CancelQuest> Cancel =
            Modern<CancelQuest>(MessageKeys.Quests.Cancel);

        public static readonly MessageContract<OpenQuestTracker> TrackerOpen =
            Modern<OpenQuestTracker>(MessageKeys.Quests.TrackerOpen);

        public static readonly MessageContract<FriendRequestQuestComplete> FriendRequestCompleted =
            Modern<FriendRequestQuestComplete>(MessageKeys.Quests.FriendRequestCompleted);
    }

    public static class Habbicons
    {
        public static readonly MessageContract<HabbiconShopRequest> ShopRequest =
            Modern<HabbiconShopRequest>(MessageKeys.Habbicons.ShopRequest);

        public static readonly MessageContract<HabbiconShopData> ShopSnapshot =
            Modern<HabbiconShopData>(MessageKeys.Habbicons.ShopSnapshot);

        public static readonly MessageContract<UserHabbicons> InventorySnapshot =
            Modern<UserHabbicons>(MessageKeys.Habbicons.InventorySnapshot);

        public static readonly MessageContract<UserHabbiconStatusChanged> StatusUpdated =
            Modern<UserHabbiconStatusChanged>(MessageKeys.Habbicons.StatusUpdated);

        public static readonly MessageContract<HabbiconInfoRequest> InfoRequest =
            Modern<HabbiconInfoRequest>(MessageKeys.Habbicons.InfoRequest);

        public static readonly MessageContract<HabbiconInfo> InfoSnapshot =
            Modern<HabbiconInfo>(MessageKeys.Habbicons.InfoSnapshot);

        public static readonly MessageContract<RoomUseHabbicon> RoomUsed =
            Modern<RoomUseHabbicon>(MessageKeys.Habbicons.RoomUsed);

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
            Modern<GetForumStats>(MessageKeys.Forums.StatsRequest);

        public static readonly MessageContract<GetForumsList> ListRequest =
            Modern<GetForumsList>(MessageKeys.Forums.ListRequest);

        public static readonly MessageContract<GetForumThreads> ThreadsRequest =
            Modern<GetForumThreads>(MessageKeys.Forums.ThreadsRequest);

        public static readonly MessageContract<GetForumThreadMessages> MessagesRequest =
            Modern<GetForumThreadMessages>(MessageKeys.Forums.MessagesRequest);

        public static readonly MessageContract<GetForumThread> ThreadRequest =
            Modern<GetForumThread>(MessageKeys.Forums.ThreadRequest);

        public static readonly MessageContract<GetUnreadForumsCount> UnreadCountRequest =
            Modern<GetUnreadForumsCount>(MessageKeys.Forums.UnreadCountRequest);

        public static readonly MessageContract<PostMessage> Post =
            Modern<PostMessage>(MessageKeys.Forums.Post);

        public static readonly MessageContract<ModerateForumThread> ThreadModerate =
            Modern<ModerateForumThread>(MessageKeys.Forums.ThreadModerate);

        public static readonly MessageContract<ModerateForumMessage> MessageModerate =
            Modern<ModerateForumMessage>(MessageKeys.Forums.MessageModerate);

        public static readonly MessageContract<UpdateForumSettings> SettingsUpdate =
            Modern<UpdateForumSettings>(MessageKeys.Forums.SettingsUpdate);

        public static readonly MessageContract<UpdateForumReadMarkers> ReadMarkersUpdate =
            Modern<UpdateForumReadMarkers>(MessageKeys.Forums.ReadMarkersUpdate);

        public static readonly MessageContract<UpdateThread> ThreadUpdate =
            Modern<UpdateThread>(MessageKeys.Forums.ThreadUpdate);

        public static readonly MessageContract<CallForHelpFromForumThread> ThreadReport =
            ForumThreadReport();

        public static readonly MessageContract<CallForHelpFromForumMessage> MessageReport =
            ForumMessageReport();
    }

    public static class Catalog
    {
        public static readonly MessageContract<CatalogIndexRequest> IndexRequest =
            Modern<CatalogIndexRequest>(MessageKeys.Catalog.IndexRequest);

        public static readonly MessageContract<CatalogIndex> IndexSnapshot =
            Modern<CatalogIndex>(MessageKeys.Catalog.IndexSnapshot);

        public static readonly MessageContract<CatalogPageRequest> PageRequest =
            Modern<CatalogPageRequest>(MessageKeys.Catalog.PageRequest);

        public static readonly MessageContract<CatalogPage> PageSnapshot =
            Modern<CatalogPage>(MessageKeys.Catalog.PageSnapshot);

        public static readonly MessageContract<PurchaseFromCatalogRequest> Purchase =
            Modern<PurchaseFromCatalogRequest>(MessageKeys.Catalog.Purchase);

        public static readonly MessageContract<PurchaseOK> Accepted =
            Modern<PurchaseOK>(MessageKeys.Catalog.PurchaseAccepted);

        public static readonly MessageContract<PurchaseError> Failed =
            Modern<PurchaseError>(MessageKeys.Catalog.PurchaseFailed);

        public static readonly MessageContract<PurchaseNotAllowed> Forbidden =
            Modern<PurchaseNotAllowed>(MessageKeys.Catalog.PurchaseForbidden);

        public static readonly MessageContract<CatalogPublished> Published =
            Modern<CatalogPublished>(MessageKeys.Catalog.Published);

        public static readonly MessageContract<GetRoomAdPurchaseInfo> RoomAdInfoRequest =
            Flash<GetRoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfoRequest);

        public static readonly MessageContract<RoomAdPurchaseInfo> RoomAdInfo =
            Flash<RoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfo);
    }

    public static class Gifts
    {
        public static readonly MessageContract<GiftWrappingConfiguration> WrappingConfiguration =
            Modern<GiftWrappingConfiguration>(MessageKeys.Gifts.WrappingConfiguration);

        public static readonly MessageContract<PresentOpened> PresentOpened =
            Modern<PresentOpened>(MessageKeys.Gifts.PresentOpened);

        public static readonly MessageContract<ClubGiftInfo> ClubInfo =
            Modern<ClubGiftInfo>(MessageKeys.Gifts.ClubInfo);

        public static readonly MessageContract<ClubGiftSelected> ClubSelected =
            Modern<ClubGiftSelected>(MessageKeys.Gifts.ClubSelected);

        public static readonly MessageContract<GiftReceiverNotFound> ReceiverNotFound =
            Flash<GiftReceiverNotFound>(MessageKeys.Gifts.ReceiverNotFound);

        public static readonly MessageContract<ClubGiftNotification> ClubNotification =
            Flash<ClubGiftNotification>(MessageKeys.Gifts.ClubNotification);

        public static readonly MessageContract<IsOfferGiftable> OfferGiftability =
            Flash<IsOfferGiftable>(MessageKeys.Gifts.OfferGiftability);

        public static readonly MessageContract<NuxGiftOffer> NewUserOffer =
            Flash<NuxGiftOffer>(MessageKeys.Gifts.NewUserOffer);

        public static readonly MessageContract<NuxNotComplete> NewUserIncomplete =
            Modern<NuxNotComplete>(MessageKeys.Gifts.NewUserIncomplete);

        public static readonly MessageContract<GetGiftWrappingConfiguration>
            WrappingConfigurationRequest =
                Modern<GetGiftWrappingConfiguration>(
                    MessageKeys.Gifts.WrappingConfigurationRequest);

        public static readonly MessageContract<PresentOpen> PresentOpen =
            Modern<PresentOpen>(MessageKeys.Gifts.PresentOpen);

        public static readonly MessageContract<PurchaseFromCatalogAsGift> Purchase =
            Modern<PurchaseFromCatalogAsGift>(MessageKeys.Gifts.Purchase);

        public static readonly MessageContract<GetClubGift> ClubInfoRequest =
            Modern<GetClubGift>(MessageKeys.Gifts.ClubInfoRequest);

        public static readonly MessageContract<SelectClubGift> ClubSelect =
            Modern<SelectClubGift>(MessageKeys.Gifts.ClubSelect);

        public static readonly MessageContract<GetIsOfferGiftable> OfferGiftabilityRequest =
            Modern<GetIsOfferGiftable>(MessageKeys.Gifts.OfferGiftabilityRequest);

        public static readonly MessageContract<NuxGetGifts> NewUserSelect =
            Modern<NuxGetGifts>(MessageKeys.Gifts.NewUserSelect);

        public static readonly MessageContract<AdvanceNewUserFlowRequest> NewUserAdvance =
            Modern<AdvanceNewUserFlowRequest>(MessageKeys.Gifts.NewUserAdvance);
    }

    public static class Groups
    {
        public static class Details
        {
            public static readonly MessageContract<GroupDetailsRequest> Request =
                Modern<GroupDetailsRequest>(MessageKeys.Groups.Details.Request);

            public static readonly MessageContract<GroupData> Snapshot =
                Modern<GroupData>(MessageKeys.Groups.Details.Snapshot);
        }

        public static class Membership
        {
            public static readonly MessageContract<JoinGroupRequest> Join =
                Modern<JoinGroupRequest>(MessageKeys.Groups.Membership.Join);

            public static readonly MessageContract<KickGroupMemberRequest> Kick =
                Modern<KickGroupMemberRequest>(MessageKeys.Groups.Membership.Kick);

            public static readonly MessageContract<ApproveGroupMemberRequest> Approve =
                Modern<ApproveGroupMemberRequest>(MessageKeys.Groups.Membership.Approve);

            public static readonly MessageContract<RejectGroupMemberRequest> Reject =
                Modern<RejectGroupMemberRequest>(MessageKeys.Groups.Membership.Reject);
        }

        public static class Members
        {
            public static readonly MessageContract<GetGuildMembersRequest> Request =
                Modern<GetGuildMembersRequest>(MessageKeys.Groups.Members.Request);

            public static readonly MessageContract<GuildMembers> Snapshot =
                Modern<GuildMembers>(MessageKeys.Groups.Members.Snapshot);
        }

        public static class Memberships
        {
            public static readonly MessageContract<GuildMembershipsRequest> Request =
                Modern<GuildMembershipsRequest>(MessageKeys.Groups.Memberships.Request);

            public static readonly MessageContract<GuildMemberships> Snapshot =
                Modern<GuildMemberships>(MessageKeys.Groups.Memberships.Snapshot);
        }
    }

    public static class Navigator
    {
        public static class State
        {
            public static readonly MessageContract<NavigatorMetadataRequest> MetadataRequest =
                Modern<NavigatorMetadataRequest>(MessageKeys.Navigator.State.MetadataRequest);

            public static readonly MessageContract<NavigatorMetaData> Metadata =
                Modern<NavigatorMetaData>(MessageKeys.Navigator.State.Metadata);

            public static readonly MessageContract<FlatCategoriesRequest> FlatCategoriesRequest =
                Modern<FlatCategoriesRequest>(MessageKeys.Navigator.State.FlatCategoriesRequest);

            public static readonly MessageContract<UserFlatCats> FlatCategories =
                Modern<UserFlatCats>(MessageKeys.Navigator.State.FlatCategories);

            public static readonly MessageContract<NavigatorLiftedRooms> LiftedRooms =
                Modern<NavigatorLiftedRooms>(MessageKeys.Navigator.State.LiftedRooms);

            public static readonly MessageContract<NavigatorSettings> Settings =
                Modern<NavigatorSettings>(MessageKeys.Navigator.State.Settings);

            public static readonly MessageContract<NewNavigatorPreferences> Preferences =
                Modern<NewNavigatorPreferences>(MessageKeys.Navigator.State.Preferences);
        }

        public static class Search
        {
            public static readonly MessageContract<NavigatorSearchResult> Result =
                Modern<NavigatorSearchResult>(MessageKeys.Navigator.Search.Result);

            public static readonly MessageContract<NavigatorSearchResult> LegacyResult =
                LegacyNavigatorSearchResult();

            public static readonly MessageContract<NavigatorViewSearchRequest> View =
                Modern<NavigatorViewSearchRequest>(MessageKeys.Navigator.Search.View);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRooms =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFavouriteRooms =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFavouriteRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomRights =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomRights);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomHistory =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomHistory);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFrequentRoomHistory =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFrequentRoomHistory);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFriendsRooms =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFriendsRooms);

            public static readonly MessageContract<NavigatorEmptySearchRequest> RoomsWhereFriendsAre =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.RoomsWhereFriendsAre);

            public static readonly MessageContract<NavigatorEmptySearchRequest> MyGuildBases =
                Modern<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyGuildBases);

            public static readonly MessageContract<NavigatorTextSearchRequest> Text =
                Modern<NavigatorTextSearchRequest>(MessageKeys.Navigator.Search.Text);

            public static readonly MessageContract<NavigatorTagSearchRequest> Popular =
                Modern<NavigatorTagSearchRequest>(MessageKeys.Navigator.Search.Popular);

            public static readonly MessageContract<NavigatorAdSearchRequest> HighestScoring =
                Modern<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.HighestScoring);

            public static readonly MessageContract<NavigatorAdSearchRequest> GuildBases =
                Modern<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.GuildBases);
        }

        public static class Personalization
        {
            public static readonly MessageContract<NavigatorSavedSearches> SavedSearches =
                Modern<NavigatorSavedSearches>(MessageKeys.Navigator.Personalization.SavedSearches);

            public static readonly MessageContract<AddSavedSearchRequest> SavedSearchAdd =
                Modern<AddSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchAdd);

            public static readonly MessageContract<DeleteSavedSearchRequest> SavedSearchDelete =
                Modern<DeleteSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchDelete);

            public static readonly MessageContract<CollapsedCategories> CollapsedCategories =
                Modern<CollapsedCategories>(MessageKeys.Navigator.Personalization.CollapsedCategories);

            public static readonly MessageContract<AddCollapsedCategoryRequest> CollapsedCategoryAdd =
                Modern<AddCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryAdd);

            public static readonly MessageContract<RemoveCollapsedCategoryRequest> CollapsedCategoryRemove =
                Modern<RemoveCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryRemove);
        }

        public static readonly MessageContract<SetHomeRoomRequest> HomeRoomUpdate =
            Modern<SetHomeRoomRequest>(MessageKeys.Navigator.HomeRoomUpdate);

        public static readonly MessageContract<CreateRoomRequest> RoomCreate =
            Modern<CreateRoomRequest>(MessageKeys.Navigator.RoomCreate);

        public static readonly MessageContract<DeleteRoomRequest> RoomDelete =
            Modern<DeleteRoomRequest>(MessageKeys.Navigator.RoomDelete);
    }

    public static class Inventory
    {
        public static class AvatarEffects
        {
            public static readonly MessageContract<AvatarEffectActivationRequest> ActivationRequest =
                Modern<AvatarEffectActivationRequest>(MessageKeys.Inventory.AvatarEffects.ActivationRequest);
        }

        public static class Furni
        {
            public static readonly MessageContract<FurniInventoryRequest> Request =
                Modern<FurniInventoryRequest>(MessageKeys.Inventory.Furni.Request);

            public static readonly MessageContract<FurniList> Snapshot =
                InventoryFurni<FurniList>(MessageKeys.Inventory.Furni.Snapshot);

            public static readonly MessageContract<FurniListAddOrUpdate> AddedOrUpdated =
                InventoryFurni<FurniListAddOrUpdate>(MessageKeys.Inventory.Furni.AddedOrUpdated);

            public static readonly MessageContract<FurniListRemove> Removed =
                Modern<FurniListRemove>(MessageKeys.Inventory.Furni.Removed);

            public static readonly MessageContract<FurniListRemoveMultiple> RemovedMultiple =
                Flash<FurniListRemoveMultiple>(MessageKeys.Inventory.Furni.RemovedMultiple);

            public static readonly MessageContract<FurniListInvalidate> Invalidated =
                Modern<FurniListInvalidate>(MessageKeys.Inventory.Furni.Invalidated);

            public static readonly MessageContract<PostItPlaced> PostItPlaced =
                Modern<PostItPlaced>(MessageKeys.Inventory.Furni.PostItPlaced);
        }

        public static class Pets
        {
            public static readonly MessageContract<PetInventoryRequest> Request =
                Modern<PetInventoryRequest>(MessageKeys.Inventory.Pets.Request);

            public static readonly MessageContract<PetInventory> Snapshot =
                Modern<PetInventory>(MessageKeys.Inventory.Pets.Snapshot);

            public static readonly MessageContract<PetAddedToInventory> Added =
                Modern<PetAddedToInventory>(MessageKeys.Inventory.Pets.Added);

            public static readonly MessageContract<PetRemovedFromInventory> Removed =
                Modern<PetRemovedFromInventory>(MessageKeys.Inventory.Pets.Removed);
        }
    }

    public static class Wardrobe
    {
        public static readonly MessageContract<WardrobeRequest> Request =
            Modern<WardrobeRequest>(MessageKeys.Wardrobe.Request);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Wardrobe> Snapshot =
            Modern<Qx.Model.Messages.Incoming.Wardrobe>(MessageKeys.Wardrobe.Snapshot);

        public static readonly MessageContract<FigureUpdateRequest> FigureUpdate =
            Modern<FigureUpdateRequest>(MessageKeys.Wardrobe.FigureUpdate);

        public static readonly MessageContract<SaveWardrobeOutfitRequest> OutfitSave =
            Modern<SaveWardrobeOutfitRequest>(MessageKeys.Wardrobe.OutfitSave);
    }

    public static class Marketplace
    {
        public static class Configuration
        {
            public static readonly MessageContract<GetMarketplaceConfiguration> Request =
                ModernMarketplace<GetMarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Request);

            public static readonly MessageContract<MarketplaceConfiguration> Snapshot =
                Modern<MarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Snapshot);
        }

        public static class Eligibility
        {
            public static readonly MessageContract<GetMarketplaceCanMakeOffer> Request =
                ModernMarketplace<GetMarketplaceCanMakeOffer>(MessageKeys.Marketplace.Eligibility.Request);

            public static readonly MessageContract<MarketplaceCanMakeOfferResult> Result =
                Modern<MarketplaceCanMakeOfferResult>(MessageKeys.Marketplace.Eligibility.Result);
        }

        public static class Credits
        {
            public static readonly MessageContract<RedeemMarketplaceOfferCredits> Redeem =
                Modern<RedeemMarketplaceOfferCredits>(MessageKeys.Marketplace.Credits.Redeem);
        }

        public static class Tokens
        {
            public static readonly MessageContract<BuyMarketplaceTokens> Buy =
                ModernMarketplace<BuyMarketplaceTokens>(MessageKeys.Marketplace.Tokens.Buy);
        }

        public static class Offers
        {
            public static readonly MessageContract<SearchMarketplaceOffers> SearchRequest =
                MarketplaceLayout<SearchMarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchRequest);

            public static readonly MessageContract<MarketplaceOffers> SearchResult =
                Modern<MarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchResult);

            public static readonly MessageContract<GetMarketplaceOwnOffers> OwnRequest =
                MarketplaceLayout<GetMarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnRequest);

            public static readonly MessageContract<MarketplaceOwnOffers> OwnSnapshot =
                Modern<MarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnSnapshot);

            public static readonly MessageContract<MakeMarketplaceOffer> Make =
                MarketplaceLayout<MakeMarketplaceOffer>(MessageKeys.Marketplace.Offers.Make);

            public static readonly MessageContract<MarketplaceMakeOfferResult> MakeResult =
                Modern<MarketplaceMakeOfferResult>(MessageKeys.Marketplace.Offers.MakeResult);

            public static readonly MessageContract<MarketplaceBuyOfferRequest> Buy =
                new(
                    MessageKeys.Marketplace.Offers.Buy,
                    MessageDialectProjection<MarketplaceBuyOfferRequest>.FromModel(ClientType.Flash),
                    MessageDialectProjection<MarketplaceBuyOfferRequest>.FromModel(
                        ClientType.Unity,
                        UnityMarketplaceBuyCapability));

            public static readonly MessageContract<MarketplaceBuyResult> BuyResult =
                Modern<MarketplaceBuyResult>(MessageKeys.Marketplace.Offers.BuyResult);

            public static readonly MessageContract<CancelMarketplaceOffer> Cancel =
                Modern<CancelMarketplaceOffer>(MessageKeys.Marketplace.Offers.Cancel);

            public static readonly MessageContract<MarketplaceCancelOfferResult> CancelResult =
                Flash<MarketplaceCancelOfferResult>(MessageKeys.Marketplace.Offers.CancelResult);

            public static readonly MessageContract<CancelAllMarketplaceOffers> CancelAll =
                ModernMarketplace<CancelAllMarketplaceOffers>(MessageKeys.Marketplace.Offers.CancelAll);

            public static readonly MessageContract<MarketplaceCancelAllOffersResult> CancelAllResult =
                Modern<MarketplaceCancelAllOffersResult>(MessageKeys.Marketplace.Offers.CancelAllResult);

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
                Modern<MarketplaceItemStats>(MessageKeys.Marketplace.ItemStats.Snapshot);
        }
    }

    public static class Subscriptions
    {
        public static readonly MessageContract<ScrSendUserInfo> UserInfo =
            Modern<ScrSendUserInfo>(MessageKeys.Subscriptions.UserInfo);

        public static readonly MessageContract<SubscriptionGetUserInfo> UserInfoRequest =
            Modern<SubscriptionGetUserInfo>(MessageKeys.Subscriptions.UserInfoRequest);

        public static readonly MessageContract<ScrSendKickbackInfo> KickbackInfo =
            Modern<ScrSendKickbackInfo>(MessageKeys.Subscriptions.KickbackInfo);

        public static readonly MessageContract<SubscriptionGetKickbackInfo> KickbackInfoRequest =
            Modern<SubscriptionGetKickbackInfo>(MessageKeys.Subscriptions.KickbackInfoRequest);

        public static readonly MessageContract<HabboClubOffers> ClubOffersSnapshot =
            Modern<HabboClubOffers>(MessageKeys.Subscriptions.ClubOffersSnapshot);

        public static readonly MessageContract<GetClubOffers> ClubOffersRequest =
            Modern<GetClubOffers>(MessageKeys.Subscriptions.ClubOffersRequest);

        public static readonly MessageContract<BuildersClubFurniCount> BuildersClubFurniCount =
            Modern<BuildersClubFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCount);

        public static readonly MessageContract<BuildersClubQueryFurniCount> BuildersClubFurniCountRequest =
            Modern<BuildersClubQueryFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCountRequest);

        public static readonly MessageContract<BuildersClubMembershipStatus> BuildersClubMembershipStatus =
            Flash<BuildersClubMembershipStatus>(MessageKeys.Subscriptions.BuildersClubMembershipStatus);

        public static readonly MessageContract<BuildersClubPlacementWarning> BuildersClubPlacementWarning =
            Flash<BuildersClubPlacementWarning>(MessageKeys.Subscriptions.BuildersClubPlacementWarning);

        public static readonly MessageContract<BuildersClubPlaceRoomItem> BuildersClubFloorOfferPlace =
            Modern<BuildersClubPlaceRoomItem>(MessageKeys.Subscriptions.BuildersClubFloorOfferPlace);

        public static readonly MessageContract<BuildersClubPlaceWallItem> BuildersClubWallOfferPlace =
            Modern<BuildersClubPlaceWallItem>(
                MessageKeys.Subscriptions.BuildersClubWallOfferPlace,
                UnityBuildersClubWallOfferPlaceCapability,
                allows_schema_selected_header: true);
    }

    public static class Crafting
    {
        public static readonly MessageContract<GetCraftableProducts> ProductsRequest =
            Modern<GetCraftableProducts>(MessageKeys.Crafting.ProductsRequest);

        public static readonly MessageContract<CraftableProducts> ProductsSnapshot =
            Modern<CraftableProducts>(MessageKeys.Crafting.ProductsSnapshot);

        public static readonly MessageContract<GetCraftingRecipe> RecipeRequest =
            Modern<GetCraftingRecipe>(MessageKeys.Crafting.RecipeRequest);

        public static readonly MessageContract<CraftingRecipe> RecipeSnapshot =
            Modern<CraftingRecipe>(MessageKeys.Crafting.RecipeSnapshot);

        public static readonly MessageContract<Qx.Model.Messages.Incoming.Craft> Craft =
            Modern<Qx.Model.Messages.Incoming.Craft>(MessageKeys.Crafting.Craft);

        public static readonly MessageContract<CraftSecret> SecretCraft =
            Modern<CraftSecret>(MessageKeys.Crafting.SecretCraft);

        public static readonly MessageContract<GetCraftingRecipesAvailable> AvailabilityRequest =
            Modern<GetCraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilityRequest);

        public static readonly MessageContract<CraftingRecipesAvailable> AvailabilitySnapshot =
            Modern<CraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilitySnapshot);

        public static readonly MessageContract<CraftingResult> Result =
            Modern<CraftingResult>(MessageKeys.Crafting.Result);
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
                Modern<WiredEnvironment>(MessageKeys.Wired.State.Environment);

            public static readonly MessageContract<WiredClickSettings> ClickSettings =
                Modern<WiredClickSettings>(MessageKeys.Wired.State.ClickSettings);

            public static readonly MessageContract<WiredMenuError> MenuError =
                Flash<WiredMenuError>(MessageKeys.Wired.State.MenuError);

            public static readonly MessageContract<WiredRewardResult> RewardResult =
                Modern<WiredRewardResult>(MessageKeys.Wired.State.RewardResult);
        }

        public static class Configuration
        {
            public static readonly MessageContract<WiredOpen> Opened =
                Modern<WiredOpen>(MessageKeys.Wired.Configuration.Opened);

            public static readonly MessageContract<WiredOpen> OpenRequest =
                Modern<WiredOpen>(MessageKeys.Wired.Configuration.OpenRequest);

            public static readonly MessageContract<WiredApplySnapshot> ApplySnapshot =
                Modern<WiredApplySnapshot>(MessageKeys.Wired.Configuration.ApplySnapshot);

            public static readonly MessageContract<WiredFurniTrigger> Trigger =
                WiredConfiguration<WiredFurniTrigger>(MessageKeys.Wired.Configuration.Trigger);

            public static readonly MessageContract<WiredFurniAction> Action =
                WiredConfiguration<WiredFurniAction>(MessageKeys.Wired.Configuration.Action);

            public static readonly MessageContract<WiredFurniCondition> Condition =
                WiredConfiguration<WiredFurniCondition>(MessageKeys.Wired.Configuration.Condition);

            public static readonly MessageContract<WiredFurniSelector> Selector =
                WiredConfiguration<WiredFurniSelector>(MessageKeys.Wired.Configuration.Selector);

            public static readonly MessageContract<WiredFurniAddon> Addon =
                WiredConfiguration<WiredFurniAddon>(MessageKeys.Wired.Configuration.Addon);

            public static readonly MessageContract<WiredFurniVariable> Variable =
                Flash<WiredFurniVariable>(MessageKeys.Wired.Configuration.Variable);

            public static readonly MessageContract<UpdateTrigger> TriggerUpdate =
                Modern<UpdateTrigger>(MessageKeys.Wired.Configuration.TriggerUpdate);

            public static readonly MessageContract<UpdateAction> ActionUpdate =
                Modern<UpdateAction>(MessageKeys.Wired.Configuration.ActionUpdate);

            public static readonly MessageContract<UpdateCondition> ConditionUpdate =
                Modern<UpdateCondition>(MessageKeys.Wired.Configuration.ConditionUpdate);

            public static readonly MessageContract<UpdateSelector> SelectorUpdate =
                Modern<UpdateSelector>(MessageKeys.Wired.Configuration.SelectorUpdate);

            public static readonly MessageContract<UpdateAddon> AddonUpdate =
                Modern<UpdateAddon>(MessageKeys.Wired.Configuration.AddonUpdate);

            public static readonly MessageContract<UpdateVariable> VariableUpdate =
                Flash<UpdateVariable>(MessageKeys.Wired.Configuration.VariableUpdate);

            public static readonly MessageContract<WiredSaveSuccess> SaveSucceeded =
                Modern<WiredSaveSuccess>(MessageKeys.Wired.Configuration.SaveSucceeded);

            public static readonly MessageContract<WiredValidationError> ValidationFailed =
                Modern<WiredValidationError>(MessageKeys.Wired.Configuration.ValidationFailed);
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
                Modern<WiredClickUser>(MessageKeys.Wired.UserClick.Request);

            public static readonly MessageContract<WiredClickUserResponse> Result =
                Modern<WiredClickUserResponse>(MessageKeys.Wired.UserClick.Result);
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
                Modern<ItemsChestContentsChunk>(MessageKeys.Wired.Chests.ItemsChunk);

            public static readonly MessageContract<ItemsChestContentsUpdated> ItemsUpdated =
                Modern<ItemsChestContentsUpdated>(MessageKeys.Wired.Chests.ItemsUpdated);

            public static readonly MessageContract<UpgradeChestResult> UpgradeResult =
                Flash<UpgradeChestResult>(MessageKeys.Wired.Chests.UpgradeResult);

            public static readonly MessageContract<ChestPreferencesUpdateSuccess> PreferencesUpdated =
                Flash<ChestPreferencesUpdateSuccess>(MessageKeys.Wired.Chests.PreferencesUpdated);

            public static readonly MessageContract<OpenChestAndGetContents> OpenRequest =
                Modern<OpenChestAndGetContents>(MessageKeys.Wired.Chests.OpenRequest);

            public static readonly MessageContract<CloseChest> Close =
                Modern<CloseChest>(MessageKeys.Wired.Chests.Close);

            public static readonly MessageContract<LockAllChests> LockAll =
                Flash<LockAllChests>(MessageKeys.Wired.Chests.LockAll);

            public static readonly MessageContract<UpgradeChest> Upgrade =
                Flash<UpgradeChest>(MessageKeys.Wired.Chests.Upgrade);

            public static readonly MessageContract<WithdrawAllFromChest> WithdrawAll =
                Modern<WithdrawAllFromChest>(MessageKeys.Wired.Chests.WithdrawAll);

            public static readonly MessageContract<WithdrawCoinsFromChest> WithdrawCoins =
                Modern<WithdrawCoinsFromChest>(MessageKeys.Wired.Chests.WithdrawCoins);

            public static readonly MessageContract<WithdrawItemsFromChest> WithdrawItems =
                Modern<WithdrawItemsFromChest>(MessageKeys.Wired.Chests.WithdrawItems);

            public static readonly MessageContract<StartAddingToChest> StartAdding =
                Modern<StartAddingToChest>(MessageKeys.Wired.Chests.StartAdding);

            public static readonly MessageContract<SetChestOptions> OptionsUpdate =
                Modern<SetChestOptions>(MessageKeys.Wired.Chests.OptionsUpdate);

            public static readonly MessageContract<SetChestPreferences> PreferencesUpdate =
                Modern<SetChestPreferences>(MessageKeys.Wired.Chests.PreferencesUpdate);

            public static readonly MessageContract<SetChestNotificationPreferences> NotificationPreferencesUpdate =
                Flash<SetChestNotificationPreferences>(MessageKeys.Wired.Chests.NotificationPreferencesUpdate);
        }

        public static class Transaction
        {
            public static readonly MessageContract<WiredTransactionSuccess> Succeeded =
                Modern<WiredTransactionSuccess>(MessageKeys.Wired.Transaction.Succeeded);

            public static readonly MessageContract<WiredTransactionFail> Failed =
                Modern<WiredTransactionFail>(MessageKeys.Wired.Transaction.Failed);

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
                Modern<WiredTradeInitiate>(MessageKeys.Wired.Trade.Initiated);

            public static readonly MessageContract<WiredTradeItemsUpdate> ItemsUpdated =
                Modern<WiredTradeItemsUpdate>(MessageKeys.Wired.Trade.ItemsUpdated);

            public static readonly MessageContract<WiredTradeCancelled> Cancelled =
                Modern<WiredTradeCancelled>(MessageKeys.Wired.Trade.Cancelled);

            public static readonly MessageContract<WiredTradeCompleted> Completed =
                Modern<WiredTradeCompleted>(MessageKeys.Wired.Trade.Completed);

            public static readonly MessageContract<WiredTradeAddDeleteItems> ItemsUpdate =
                Modern<WiredTradeAddDeleteItems>(MessageKeys.Wired.Trade.ItemsUpdate);

            public static readonly MessageContract<WiredTradeConfirm> Confirm =
                Modern<WiredTradeConfirm>(MessageKeys.Wired.Trade.Confirm);

            public static readonly MessageContract<WiredTradeCancel> Cancel =
                Modern<WiredTradeCancel>(MessageKeys.Wired.Trade.Cancel);

            public static readonly MessageContract<WiredTradeTransactionNotification> Notification =
                Modern<WiredTradeTransactionNotification>(MessageKeys.Wired.Trade.Notification);
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
            Modern<PollContents>(MessageKeys.Polls.Contents);

        public static readonly MessageContract<PollError> Error =
            Modern<PollError>(MessageKeys.Polls.Error);

        public static readonly MessageContract<PollOffer> Offer =
            Modern<PollOffer>(MessageKeys.Polls.Offer);

        public static readonly MessageContract<PollAnswer> Answer =
            Modern<PollAnswer>(MessageKeys.Polls.Answer);

        public static readonly MessageContract<RejectPoll> Reject =
            Modern<RejectPoll>(MessageKeys.Polls.Reject);

        public static readonly MessageContract<StartPoll> Start =
            Modern<StartPoll>(MessageKeys.Polls.Start);
    }

    public static class Room
    {
        public static readonly MessageContract<GetGuestRoomRequest> SnapshotRequest =
            Modern<GetGuestRoomRequest>(MessageKeys.Room.SnapshotRequest);

        public static readonly MessageContract<GuestRoomResult> Snapshot =
            Modern<GuestRoomResult>(MessageKeys.Room.Snapshot);

        public static readonly MessageContract<ToggleRoomStaffPickRequest> StaffPickUpdateRequest =
            Modern<ToggleRoomStaffPickRequest>(MessageKeys.Room.StaffPickUpdateRequest);

        public static readonly MessageContract<RateRoomRequest> RatingRequest =
            Modern<RateRoomRequest>(MessageKeys.Room.RatingRequest);

        public static class Settings
        {
            public static readonly MessageContract<GetRoomSettingsRequest> Request =
                Modern<GetRoomSettingsRequest>(MessageKeys.Room.Settings.Request);

            public static readonly MessageContract<RoomSettings> Snapshot =
                new(
                    MessageKeys.Room.Settings.Snapshot,
                    MessageDialectProjection<RoomSettings>.FromModel(ClientType.Flash),
                    MessageDialectProjection<RoomSettings>.FromModel(
                        ClientType.Unity,
                        UnityRoomSettingsSnapshotCapability));

            public static readonly MessageContract<RoomSettingsError> RequestFailed =
                Modern<RoomSettingsError>(MessageKeys.Room.Settings.RequestFailed);

            public static readonly MessageContract<SaveRoomSettingsRequest> Save =
                new(
                    MessageKeys.Room.Settings.Save,
                    MessageDialectProjection<SaveRoomSettingsRequest>.FromModel(ClientType.Flash),
                    new MessageDialectProjection<SaveRoomSettingsRequest>(
                        ClientType.Unity,
                        ParseUnityRoomSettingsSave,
                        ComposeUnityRoomSettingsSave,
                        UnityRoomSettingsSaveCapability,
                        true));

            public static readonly MessageContract<RoomSettingsSaved> SaveSucceeded =
                Modern<RoomSettingsSaved>(MessageKeys.Room.Settings.SaveSucceeded);

            public static readonly MessageContract<RoomSettingsSaveError> SaveFailed =
                Modern<RoomSettingsSaveError>(MessageKeys.Room.Settings.SaveFailed);
        }

        public static class Access
        {
            public static readonly MessageContract<OpenFlatConnection> OpenRequest =
                Modern<OpenFlatConnection>(MessageKeys.Room.Access.OpenRequest);

            public static readonly MessageContract<OpenConnectionConfirmation> OpenConfirmed =
                Modern<OpenConnectionConfirmation>(MessageKeys.Room.Access.OpenConfirmed);

            public static readonly MessageContract<Doorbell> Doorbell =
                Modern<Doorbell>(MessageKeys.Room.Access.Doorbell);

            public static readonly MessageContract<AnswerDoorbellRequest> DoorbellAnswer =
                Modern<AnswerDoorbellRequest>(MessageKeys.Room.Access.DoorbellAnswer);

            public static readonly MessageContract<RoomQueueStatus> QueueStatus =
                Modern<RoomQueueStatus>(MessageKeys.Room.Access.QueueStatus);

            public static readonly MessageContract<FlatAccessible> Granted =
                Flash<FlatAccessible>(MessageKeys.Room.Access.Granted);

            public static readonly MessageContract<FlatAccessDenied> Denied =
                Flash<FlatAccessDenied>(MessageKeys.Room.Access.Denied);

            public static readonly MessageContract<NoSuchFlat> NotFound =
                Modern<NoSuchFlat>(MessageKeys.Room.Access.NotFound);

            public static readonly MessageContract<CanNotConnect> ConnectionFailed =
                Modern<CanNotConnect>(MessageKeys.Room.Access.ConnectionFailed);
        }

        public static class Lifecycle
        {
            public static readonly MessageContract<RoomReady> Ready =
                Modern<RoomReady>(MessageKeys.Room.Lifecycle.Ready);

            public static readonly MessageContract<RoomEntryInfo> Entry =
                Modern<RoomEntryInfo>(MessageKeys.Room.Lifecycle.Entry);

            public static readonly MessageContract<RoomForward> Forward =
                Modern<RoomForward>(MessageKeys.Room.Lifecycle.Forward);

            public static readonly MessageContract<CloseConnection> ConnectionClosed =
                Modern<CloseConnection>(MessageKeys.Room.Lifecycle.ConnectionClosed);

            public static readonly MessageContract<RoomExitReason> NativeExit =
                Unity<RoomExitReason>(MessageKeys.Room.Lifecycle.NativeExit);

            public static readonly MessageContract<QuitRoomRequest> Quit =
                Modern<QuitRoomRequest>(MessageKeys.Room.Lifecycle.Quit);
        }

        public static class Environment
        {
            public static readonly MessageContract<RoomEntryTile> EntryTile =
                Modern<RoomEntryTile>(MessageKeys.Room.Environment.EntryTile);

            public static readonly MessageContract<FlatProperty> Property =
                Modern<FlatProperty>(MessageKeys.Room.Environment.Property);

            public static readonly MessageContract<RoomVisualizationSettings> Visualization =
                Modern<RoomVisualizationSettings>(MessageKeys.Room.Environment.Visualization);

            public static readonly MessageContract<RoomChatSettings> ChatSettings =
                Modern<RoomChatSettings>(MessageKeys.Room.Environment.ChatSettings);

            public static readonly MessageContract<FloorPlan> FloorPlan =
                Modern<FloorPlan>(MessageKeys.Room.Environment.FloorPlan);
        }

        public static class Chat
        {
            public static readonly MessageContract<AvatarChat> Talk =
                Modern<AvatarChat>(MessageKeys.Room.Chat.Talk);

            public static readonly MessageContract<AvatarChat> Shout =
                Modern<AvatarChat>(MessageKeys.Room.Chat.Shout);

            public static readonly MessageContract<AvatarChat> Whisper =
                Modern<AvatarChat>(MessageKeys.Room.Chat.Whisper);

            public static readonly MessageContract<WhisperRequest> WhisperSend =
                Modern<WhisperRequest>(MessageKeys.Room.Chat.WhisperSend);

            public static readonly MessageContract<SpecialSystemChat> SpecialSystem =
                Flash<SpecialSystemChat>(MessageKeys.Room.Chat.SpecialSystem);

            public static readonly MessageContract<TalkRequest> TalkSend =
                Modern<TalkRequest>(MessageKeys.Room.Chat.TalkSend);

            public static readonly MessageContract<ShoutRequest> ShoutSend =
                Modern<ShoutRequest>(MessageKeys.Room.Chat.ShoutSend);
        }

        public static class Authority
        {
            public static readonly MessageContract<GetFlatControllersRequest> ControllersRequest =
                Modern<GetFlatControllersRequest>(MessageKeys.Room.Authority.ControllersRequest);

            public static readonly MessageContract<RightsList> ControllersSnapshot =
                Modern<RightsList>(MessageKeys.Room.Authority.ControllersSnapshot);

            public static readonly MessageContract<GiveRoomRightsRequest> ControllerGrantRequest =
                Modern<GiveRoomRightsRequest>(MessageKeys.Room.Authority.ControllerGrantRequest);

            public static readonly MessageContract<YouAreController> ControllerGranted =
                Modern<YouAreController>(MessageKeys.Room.Authority.ControllerGranted);

            public static readonly MessageContract<YouAreNotController> ControllerRevoked =
                Modern<YouAreNotController>(MessageKeys.Room.Authority.ControllerRevoked);

            public static readonly MessageContract<YouAreOwner> Owner =
                Modern<YouAreOwner>(MessageKeys.Room.Authority.Owner);

            public static readonly MessageContract<YouAreSpectator> SpectatorGranted =
                Modern<YouAreSpectator>(MessageKeys.Room.Authority.SpectatorGranted);

            public static readonly MessageContract<YouAreNotSpectator> SpectatorRevoked =
                Flash<YouAreNotSpectator>(MessageKeys.Room.Authority.SpectatorRevoked);

            public static readonly MessageContract<SpectatingEnded> SpectatingEnded =
                Unity<SpectatingEnded>(MessageKeys.Room.Authority.SpectatingEnded);
        }

        public static class Occupants
        {
            public static readonly MessageContract<RoomUsers> Snapshot =
                Modern<RoomUsers>(MessageKeys.Room.Occupants.Snapshot);

            public static readonly MessageContract<AvatarRemove> Removed =
                Modern<AvatarRemove>(MessageKeys.Room.Occupants.Removed);

            public static readonly MessageContract<UserUpdate> Status =
                Modern<UserUpdate>(MessageKeys.Room.Occupants.Status);

            public static readonly MessageContract<RespectNotification> Respect =
                Modern<RespectNotification>(MessageKeys.Room.Occupants.Respect);

            public static readonly MessageContract<RespectUserRequest> RespectRequest =
                Modern<RespectUserRequest>(MessageKeys.Room.Occupants.RespectRequest);

            public static class Action
            {
                public static readonly MessageContract<AvatarDanceUpdate> Dance =
                    Modern<AvatarDanceUpdate>(MessageKeys.Room.Occupants.Action.Dance);

                public static readonly MessageContract<AvatarDanceRequest> DanceRequest =
                    Modern<AvatarDanceRequest>(MessageKeys.Room.Occupants.Action.DanceRequest);

                public static readonly MessageContract<AvatarSignRequest> SignRequest =
                    Modern<AvatarSignRequest>(MessageKeys.Room.Occupants.Action.SignRequest);

                public static readonly MessageContract<AvatarEffectUpdate> Effect =
                    Modern<AvatarEffectUpdate>(MessageKeys.Room.Occupants.Action.Effect);

                public static readonly MessageContract<AvatarEffectSelectionRequest> EffectSelectionRequest =
                    Modern<AvatarEffectSelectionRequest>(MessageKeys.Room.Occupants.Action.EffectSelectionRequest);

                public static readonly MessageContract<AvatarPostureRequest> PostureRequest =
                    Modern<AvatarPostureRequest>(MessageKeys.Room.Occupants.Action.PostureRequest);

                public static readonly MessageContract<AvatarCarryUpdate> Carry =
                    Modern<AvatarCarryUpdate>(MessageKeys.Room.Occupants.Action.Carry);

                public static readonly MessageContract<AvatarSleepUpdate> Sleep =
                    Modern<AvatarSleepUpdate>(MessageKeys.Room.Occupants.Action.Sleep);

                public static readonly MessageContract<AvatarTypingUpdate> Typing =
                    Modern<AvatarTypingUpdate>(MessageKeys.Room.Occupants.Action.Typing);

                public static readonly MessageContract<AvatarAction> Expression =
                    Modern<AvatarAction>(MessageKeys.Room.Occupants.Action.Expression);

                public static readonly MessageContract<AvatarExpressionRequest> ExpressionRequest =
                    Modern<AvatarExpressionRequest>(MessageKeys.Room.Occupants.Action.ExpressionRequest);
            }

            public static class Identity
            {
                public static readonly MessageContract<UserChanged> Appearance =
                    Modern<UserChanged>(MessageKeys.Room.Occupants.Identity.Appearance);

                public static readonly MessageContract<UserNameChanged> Name =
                    Modern<UserNameChanged>(MessageKeys.Room.Occupants.Identity.Name);

                public static readonly MessageContract<FavoriteMembershipUpdate> FavoriteGroup =
                    Modern<FavoriteMembershipUpdate>(MessageKeys.Room.Occupants.Identity.FavoriteGroup);
            }

            public static class Pet
            {
                public static readonly MessageContract<GetPetInfoRequest> InfoRequest =
                    Modern<GetPetInfoRequest>(MessageKeys.Room.Occupants.Pet.InfoRequest);

                public static readonly MessageContract<PetInfo> Info =
                    Modern<PetInfo>(MessageKeys.Room.Occupants.Pet.Info);

                public static readonly MessageContract<PetFigureUpdate> Figure =
                    Modern<PetFigureUpdate>(MessageKeys.Room.Occupants.Pet.Figure);

                public static readonly MessageContract<PetStatusUpdate> Status =
                    Modern<PetStatusUpdate>(MessageKeys.Room.Occupants.Pet.Status);

                public static readonly MessageContract<PetLevelUpdate> Level =
                    Modern<PetLevelUpdate>(MessageKeys.Room.Occupants.Pet.Level);

                public static readonly MessageContract<RespectPetRequest> RespectRequest =
                    Modern<RespectPetRequest>(MessageKeys.Room.Occupants.Pet.RespectRequest);

                public static readonly MessageContract<MountPetRequest> MountRequest =
                    Modern<MountPetRequest>(MessageKeys.Room.Occupants.Pet.MountRequest);

                public static readonly MessageContract<RemovePetFromRoomRequest> RemoveRequest =
                    Modern<RemovePetFromRoomRequest>(MessageKeys.Room.Occupants.Pet.RemoveRequest);
            }

            public static class Bot
            {
                public static readonly MessageContract<RemoveBotFromFlat> RemoveRequest =
                    Modern<RemoveBotFromFlat>(MessageKeys.Room.Occupants.Bot.RemoveRequest);
            }
        }

        public static class HandItem
        {
            public static readonly MessageContract<HandItemReceived> Received =
                Modern<HandItemReceived>(MessageKeys.Room.HandItem.Received);

            public static readonly MessageContract<DropHandItemRequest> Drop =
                Modern<DropHandItemRequest>(MessageKeys.Room.HandItem.Drop);

            public static readonly MessageContract<PassHandItemRequest> Pass =
                Modern<PassHandItemRequest>(MessageKeys.Room.HandItem.Pass);
        }

        public static readonly MessageContract<UseFloorItemRequest> FloorItemUse =
            Modern<UseFloorItemRequest>(MessageKeys.Room.FloorItem.Use);

        public static readonly MessageContract<UseWallItemRequest> WallItemUse =
            Modern<UseWallItemRequest>(MessageKeys.Room.WallItem.Use);

        public static readonly MessageContract<RemoveWallItemRequest> WallItemRemove =
            Modern<RemoveWallItemRequest>(MessageKeys.Room.WallItem.Remove);

        public static readonly MessageContract<PlaceRoomItemRequest> ItemPlace =
            new(
                MessageKeys.Room.Item.Place,
                new MessageDialectProjection<PlaceRoomItemRequest>(
                    ClientType.Flash,
                    PlaceRoomItemRequest.ParseFlash,
                    PlaceRoomItemRequest.ComposeFlash),
                new MessageDialectProjection<PlaceRoomItemRequest>(
                    ClientType.Unity,
                    PlaceRoomItemRequest.ParseUnity,
                    PlaceRoomItemRequest.ComposeUnity,
                    UnityRoomItemPlaceCapability,
                    allows_schema_selected_header: true));

        public static readonly MessageContract<MoveFloorItemRequest> FloorItemMove =
            Modern<MoveFloorItemRequest>(MessageKeys.Room.FloorItem.Move);

        public static readonly MessageContract<MoveWallItemRequest> WallItemMove =
            new(
                MessageKeys.Room.WallItem.Move,
                MessageDialectProjection<MoveWallItemRequest>.FromModel(ClientType.Flash),
                MessageDialectProjection<MoveWallItemRequest>.FromModel(
                    ClientType.Unity,
                    UnityWallItemMoveCapability));

        public static readonly MessageContract<PickupRoomItemRequest> ItemPickup =
            Modern<PickupRoomItemRequest>(MessageKeys.Room.Item.Pickup);

        public static readonly MessageContract<PickupConfirmation> ItemPickupConfirmation =
            Flash<PickupConfirmation>(MessageKeys.Room.Item.PickupConfirmation);

        public static class FloorItem
        {
            public static readonly MessageContract<FloorItemAdd> Added =
                Modern<FloorItemAdd>(MessageKeys.Room.FloorItem.Added);

            public static readonly MessageContract<FloorItemRemove> Removed =
                Modern<FloorItemRemove>(MessageKeys.Room.FloorItem.Removed);

            public static readonly MessageContract<FloorItemUpdate> Updated =
                Modern<FloorItemUpdate>(MessageKeys.Room.FloorItem.Updated);

            public static readonly MessageContract<ThrowDiceRequest> ThrowDice =
                Modern<ThrowDiceRequest>(MessageKeys.Room.FloorItem.ThrowDice);

            public static readonly MessageContract<DiceOffRequest> DiceOff =
                Modern<DiceOffRequest>(MessageKeys.Room.FloorItem.DiceOff);

            public static readonly MessageContract<DiceValue> DiceValue =
                Modern<DiceValue>(MessageKeys.Room.FloorItem.DiceValue);

            public static readonly MessageContract<OneWayDoorStatus> OneWayDoorStatus =
                Modern<OneWayDoorStatus>(MessageKeys.Room.FloorItem.OneWayDoorStatus);

            public static readonly MessageContract<EnterOneWayDoorRequest> OneWayDoorEnter =
                Modern<EnterOneWayDoorRequest>(MessageKeys.Room.FloorItem.OneWayDoorEnter);
        }

        public static class WallItem
        {
            public static readonly MessageContract<WallItemAdd> Added =
                Modern<WallItemAdd>(MessageKeys.Room.WallItem.Added);

            public static readonly MessageContract<WallItemRemove> Removed =
                Modern<WallItemRemove>(MessageKeys.Room.WallItem.Removed);

            public static readonly MessageContract<WallItemUpdate> Updated =
                Modern<WallItemUpdate>(MessageKeys.Room.WallItem.Updated);

            public static readonly MessageContract<SetStickyDataRequest> StickyDataSet =
                Modern<SetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataSet);

            public static readonly MessageContract<GetStickyDataRequest> StickyDataRequest =
                Modern<GetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataRequest);

            public static readonly MessageContract<Sticky> StickyData =
                Modern<Sticky>(MessageKeys.Room.WallItem.StickyData);

            public static readonly MessageContract<PlacePostItRequest> PostItPlace =
                Modern<PlacePostItRequest>(MessageKeys.Room.WallItem.PostItPlace);

            public static readonly MessageContract<AddSpamWallPostItRequest> SpamPostItAdd =
                Modern<AddSpamWallPostItRequest>(MessageKeys.Room.WallItem.SpamPostItAdd);
        }

        public static class Movement
        {
            public static readonly MessageContract<WalkRequest> Walk =
                Modern<WalkRequest>(MessageKeys.Room.Movement.Walk);

            public static readonly MessageContract<LookToRequest> LookTo =
                Modern<LookToRequest>(MessageKeys.Room.Movement.LookTo);

            public static readonly MessageContract<SlideObjectBundle> Slide =
                Modern<SlideObjectBundle>(MessageKeys.Room.Movement.Slide);

            public static readonly MessageContract<WiredMovements> Wired =
                Flash<WiredMovements>(MessageKeys.Room.Movement.Wired);
        }

        public static class Typing
        {
            public static readonly MessageContract<StartTypingRequest> Start =
                Modern<StartTypingRequest>(MessageKeys.Room.Typing.Start);

            public static readonly MessageContract<CancelTypingRequest> Cancel =
                Modern<CancelTypingRequest>(MessageKeys.Room.Typing.Cancel);
        }

        public static class Moderation
        {
            public static readonly MessageContract<GetRoomBansRequest> BansRequest =
                Modern<GetRoomBansRequest>(MessageKeys.Room.Moderation.BansRequest);

            public static readonly MessageContract<BannedUsersFromRoom> BansSnapshot =
                Modern<BannedUsersFromRoom>(MessageKeys.Room.Moderation.BansSnapshot);

            public static readonly MessageContract<UserUnbannedFromRoom> UserUnbanned =
                Modern<UserUnbannedFromRoom>(MessageKeys.Room.Moderation.UserUnbanned);

            public static readonly MessageContract<MuteRoomUserRequest> UserMute =
                Modern<MuteRoomUserRequest>(MessageKeys.Room.Moderation.Mute);

            public static readonly MessageContract<KickRoomUserRequest> UserKick =
                Modern<KickRoomUserRequest>(MessageKeys.Room.Moderation.Kick);

            public static readonly MessageContract<BanRoomUserRequest> UserBan =
                Modern<BanRoomUserRequest>(MessageKeys.Room.Moderation.Ban);

            public static readonly MessageContract<UnbanRoomUserRequest> UserUnban =
                Modern<UnbanRoomUserRequest>(MessageKeys.Room.Moderation.Unban);
        }
    }

    public static class Friends
    {
        public static readonly MessageContract<FriendInitializationRequest> InitializeRequest =
            Modern<FriendInitializationRequest>(MessageKeys.Friends.InitializeRequest);

        public static readonly MessageContract<MessengerInit> Initialized =
            Modern<MessengerInit>(MessageKeys.Friends.Initialized);

        public static readonly MessageContract<FriendListFragment> ListFragment =
            Modern<FriendListFragment>(MessageKeys.Friends.ListFragment);

        public static readonly MessageContract<FriendListUpdate> ListUpdated =
            Modern<FriendListUpdate>(MessageKeys.Friends.ListUpdated);

        public static readonly MessageContract<SendPrivateMessage> PrivateMessageSend =
            new(
                MessageKeys.Friends.PrivateMessageSend,
                MessageDialectProjection<SendPrivateMessage>.FromModel(ClientType.Flash),
                MessageDialectProjection<SendPrivateMessage>.FromModel(
                    ClientType.Unity,
                    FriendPrivateMessageSchema.OutgoingCapability));

        public static readonly MessageContract<NewConsoleMessage> PrivateMessageReceived =
            new(
                MessageKeys.Friends.PrivateMessageReceived,
                MessageDialectProjection<NewConsoleMessage>.FromModel(ClientType.Flash),
                MessageDialectProjection<NewConsoleMessage>.FromModel(
                    ClientType.Unity,
                    FriendPrivateMessageSchema.IncomingCapability));

        public static readonly MessageContract<MessengerError> OperationFailed =
            Modern<MessengerError>(MessageKeys.Friends.OperationFailed);

        public static readonly MessageContract<InstantMessageError> PrivateMessageFailed =
            Modern<InstantMessageError>(MessageKeys.Friends.PrivateMessageFailed);

        public static readonly MessageContract<FriendRequest> FriendRequestSend =
            Modern<FriendRequest>(MessageKeys.Friends.FriendRequestSend);

        public static readonly MessageContract<NewFriendRequest> FriendRequestReceived =
            Modern<NewFriendRequest>(MessageKeys.Friends.FriendRequestReceived);

        public static readonly MessageContract<PendingFriendRequestsRequest> FriendRequestsRequest =
            Modern<PendingFriendRequestsRequest>(MessageKeys.Friends.FriendRequestsRequest);

        public static readonly MessageContract<PendingFriendRequests> FriendRequestsSnapshot =
            Modern<PendingFriendRequests>(MessageKeys.Friends.FriendRequestsSnapshot);

        public static readonly MessageContract<AcceptFriends> FriendRequestAccept =
            Modern<AcceptFriends>(MessageKeys.Friends.FriendRequestAccept);

        public static readonly MessageContract<DeclineFriends> FriendRequestDecline =
            Modern<DeclineFriends>(MessageKeys.Friends.FriendRequestDecline);

        public static readonly MessageContract<RemoveFriends> Remove =
            Modern<RemoveFriends>(MessageKeys.Friends.Remove);

        public static readonly MessageContract<FollowFriendRequest> Follow =
            Modern<FollowFriendRequest>(MessageKeys.Friends.Follow);

        public static readonly MessageContract<FriendSearchRequest> SearchRequest =
            Modern<FriendSearchRequest>(MessageKeys.Friends.SearchRequest);

        public static readonly MessageContract<UserSearchResults> SearchResult =
            Modern<UserSearchResults>(MessageKeys.Friends.SearchResult);

        public static readonly MessageContract<SetFriendRelationshipRequest> RelationshipSet =
            Modern<SetFriendRelationshipRequest>(MessageKeys.Friends.RelationshipSet);
    }

    public static class Trade
    {
        public static readonly MessageContract<TradeOpened> Opened =
            Modern<TradeOpened>(MessageKeys.Trade.Opened);

        public static readonly MessageContract<TradeOffers> Offers =
            Modern<TradeOffers>(MessageKeys.Trade.Offers);

        public static readonly MessageContract<TradeAccepted> AcceptanceUpdated =
            Modern<TradeAccepted>(MessageKeys.Trade.AcceptanceUpdated);

        public static readonly MessageContract<TradeConfirmation> Confirmation =
            Modern<TradeConfirmation>(MessageKeys.Trade.Confirmation);

        public static readonly MessageContract<TradeCompleted> Completed =
            Modern<TradeCompleted>(MessageKeys.Trade.Completed);

        public static readonly MessageContract<TradeClosed> Closed =
            Modern<TradeClosed>(MessageKeys.Trade.Closed);

        public static readonly MessageContract<TradeOpenFailed> OpenFailed =
            Modern<TradeOpenFailed>(MessageKeys.Trade.OpenFailed);

        public static readonly MessageContract<TradeNftAssets> NftOffers =
            Flash<TradeNftAssets>(MessageKeys.Trade.NftOffers);

        public static readonly MessageContract<TradeNftAssetInventory> NftInventory =
            Flash<TradeNftAssetInventory>(MessageKeys.Trade.NftInventory);

        public static readonly MessageContract<TradeSilverSet> SilverUpdated =
            Flash<TradeSilverSet>(MessageKeys.Trade.SilverUpdated);

        public static readonly MessageContract<TradeSilverFee> SilverFee =
            Flash<TradeSilverFee>(MessageKeys.Trade.SilverFee);

        public static readonly MessageContract<OpenTradeRequest> OpenRequest =
            Modern<OpenTradeRequest>(MessageKeys.Trade.OpenRequest);

        public static readonly MessageContract<AddTradeItemsRequest> ItemsAdd =
            Modern<AddTradeItemsRequest>(MessageKeys.Trade.ItemsAdd);

        public static readonly MessageContract<RemoveTradeItemRequest> ItemRemove =
            Modern<RemoveTradeItemRequest>(MessageKeys.Trade.ItemRemove);

        public static readonly MessageContract<AcceptTradeRequest> Accept =
            Modern<AcceptTradeRequest>(MessageKeys.Trade.Accept);

        public static readonly MessageContract<UnacceptTradeRequest> Unaccept =
            Modern<UnacceptTradeRequest>(MessageKeys.Trade.Unaccept);

        public static readonly MessageContract<ConfirmTradeRequest> Confirm =
            Modern<ConfirmTradeRequest>(MessageKeys.Trade.Confirm);

        public static readonly MessageContract<CloseTradeRequest> Close =
            Modern<CloseTradeRequest>(MessageKeys.Trade.Close);

        public static readonly MessageContract<GetNftTradeInventoryRequest> NftInventoryRequest =
            Flash<GetNftTradeInventoryRequest>(MessageKeys.Trade.NftInventoryRequest);
    }

    public static class Users
    {
        public static class Relationship
        {
            public static readonly MessageContract<RelationshipStatusRequest> Request =
                Modern<RelationshipStatusRequest>(MessageKeys.Users.Relationship.Request);

            public static readonly MessageContract<RelationshipStatus> Snapshot =
                Modern<RelationshipStatus>(MessageKeys.Users.Relationship.Snapshot);
        }

        public static class Block
        {
            public static readonly MessageContract<BlockListRequest> ListRequest =
                Modern<BlockListRequest>(MessageKeys.Users.Block.ListRequest);

            public static readonly MessageContract<BlockList> ListSnapshot =
                Modern<BlockList>(MessageKeys.Users.Block.ListSnapshot);

            public static readonly MessageContract<BlockUserUpdate> Updated =
                Modern<BlockUserUpdate>(MessageKeys.Users.Block.Updated);

            public static readonly MessageContract<BlockUserRequest> Add =
                Modern<BlockUserRequest>(MessageKeys.Users.Block.Add);

            public static readonly MessageContract<UnblockUserRequest> Remove =
                Modern<UnblockUserRequest>(MessageKeys.Users.Block.Remove);
        }

        public static class Ignore
        {
            public static readonly MessageContract<IgnoreListRequest> ListRequest =
                Modern<IgnoreListRequest>(MessageKeys.Users.Ignore.ListRequest);

            public static readonly MessageContract<RequestIgnoreList> ListSnapshot =
                Modern<RequestIgnoreList>(MessageKeys.Users.Ignore.ListSnapshot);

            public static readonly MessageContract<IgnoreUserResult> Updated =
                Modern<IgnoreUserResult>(MessageKeys.Users.Ignore.Updated);

            public static readonly MessageContract<IgnoreUserByIdRequest> AddByIdRequest =
                Modern<IgnoreUserByIdRequest>(MessageKeys.Users.Ignore.AddByIdRequest);

            public static readonly MessageContract<IgnoreUserByNameRequest> AddByNameRequest =
                Unity<IgnoreUserByNameRequest>(MessageKeys.Users.Ignore.AddByNameRequest);

            public static readonly MessageContract<UnignoreUserRequest> Remove =
                new(
                    MessageKeys.Users.Ignore.Remove,
                    MessageDialectProjection<UnignoreUserRequest>.FromModel(
                        ClientType.Flash,
                        static (_, _) => MessageDialectCapability.Ready("flashUnignoreIdSchema")),
                    new MessageDialectProjection<UnignoreUserRequest>(
                        ClientType.Unity,
                        ParseUnityUnignore,
                        ComposeUnityUnignore,
                        UnityUnignoreCapability,
                        true));
        }

        public static class FigureSets
        {
            public static readonly MessageContract<FigureSetIdAdded> Added =
                Flash<FigureSetIdAdded>(MessageKeys.Users.FigureSets.Added);

            public static readonly MessageContract<FigureSetIdRemoved> Removed =
                Flash<FigureSetIdRemoved>(MessageKeys.Users.FigureSets.Removed);

            public static readonly MessageContract<FigureSetIds> Snapshot =
                Modern<FigureSetIds>(MessageKeys.Users.FigureSets.Snapshot);
        }

        public static class Sanctions
        {
            public static readonly MessageContract<SanctionStatusRequest> Request =
                Modern<SanctionStatusRequest>(MessageKeys.Users.Sanctions.Request);

            public static readonly MessageContract<AccountSanctionStatus> Snapshot =
                Modern<AccountSanctionStatus>(MessageKeys.Users.Sanctions.Snapshot);
        }

        public static class FavoriteGroup
        {
            public static readonly MessageContract<SelectFavoriteGroupRequest> Select =
                Modern<SelectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Select);

            public static readonly MessageContract<DeselectFavoriteGroupRequest> Deselect =
                Modern<DeselectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Deselect);
        }

        public static readonly MessageContract<MottoUpdateRequest> MottoUpdate =
            Modern<MottoUpdateRequest>(MessageKeys.Users.MottoUpdate);

        public static readonly MessageContract<ProfileRequest> ProfileRequest =
            Modern<ProfileRequest>(MessageKeys.Users.ProfileRequest);

        public static readonly MessageContract<UserData> ProfileSnapshot =
            Modern<UserData>(MessageKeys.Users.ProfileSnapshot);

        public static readonly MessageContract<FigureUpdate> FigureUpdated =
            Modern<FigureUpdate>(MessageKeys.Users.FigureUpdated);

        public static readonly MessageContract<ChangeUserNameResult> NameChangeResult =
            Modern<ChangeUserNameResult>(MessageKeys.Users.NameChangeResult);

        public static readonly MessageContract<AccountSafetyLockStatusChange> SafetyLockChanged =
            Flash<AccountSafetyLockStatusChange>(MessageKeys.Users.SafetyLockChanged);

        public static readonly MessageContract<ExtendedProfileRequest> ExtendedProfileRequest =
            Modern<ExtendedProfileRequest>(MessageKeys.Users.ExtendedProfileRequest);

        public static readonly MessageContract<UserProfile> ExtendedProfileSnapshot =
            Modern<UserProfile>(MessageKeys.Users.ExtendedProfileSnapshot);
    }

    private static MessageDialectCapability UnityRoomItemPlaceCapability(
        MessageManager messages,
        Header header)
    {
        const string capability_name = "unityRoomItemPlaceSchema";
        if (!messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas) ||
            schemas.Count == 0)
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity room-item placement header has no verified wire schema.");
        }

        if (schemas.All(IsUnityFloorItemPlacementSchema))
            return MessageDialectCapability.Ready("unityFloorItemPlacementSchema");
        if (schemas.All(IsUnityWallItemPlacementSchema))
            return MessageDialectCapability.Ready("unityWallItemPlacementSchema");
        return MessageDialectCapability.Missing(
            capability_name,
            "The active Unity room-item placement header has mixed or unsupported wire schemas.");
    }

    private static MessageDialectCapability UnityWallItemMoveCapability(
        MessageManager messages,
        Header header)
    {
        const string capability_name = "unityWallItemMoveSchema";
        if (!messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas) ||
            schemas.Count == 0)
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity wall-item move header has no verified wire schema.");
        }
        return schemas.All(IsUnityWallItemPlacementSchema)
            ? MessageDialectCapability.Ready(capability_name)
            : MessageDialectCapability.Missing(
                capability_name,
                "The active Unity wall-item move header has mixed or unsupported wire schemas.");
    }

    private static MessageDialectCapability UnityBuildersClubWallOfferPlaceCapability(
        ClientType client,
        MessageManager messages,
        Header header)
    {
        const string capability_name = "unityBuildersClubWallLocationSchema";
        if (!messages.TryGetOutgoingSchemas(
                client,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas) ||
            schemas.Count == 0)
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity Builders Club wall-placement header has no verified wire schema.");
        }
        if (!messages.TryGetHeader(
                client,
                MessageKeys.Room.WallItem.Move,
                out Header wall_move_header) ||
            !messages.TryGetOutgoingSchemas(
                client,
                wall_move_header,
                out IReadOnlyList<OutgoingMessageSchema> wall_move_schemas) ||
            wall_move_schemas.Count == 0 ||
            !wall_move_schemas.All(IsUnityWallItemPlacementSchema))
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity build has no exact wall-location reference schema.");
        }
        if (wall_move_schemas.Any(schema =>
                string.IsNullOrWhiteSpace(schema.Parameters[1].SourceType)))
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity wall-location reference schema has no exact source type.");
        }

        string[] source_types =
        [
            .. wall_move_schemas
                .Select(schema => schema.Parameters[1].SourceType)
                .Distinct(StringComparer.Ordinal)
        ];
        if (source_types.Length != 1)
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity wall-location reference schema has an ambiguous source type.");
        }

        return schemas.All(schema =>
                IsUnityBuildersClubWallOfferPlaceSchema(schema, source_types[0]))
            ? MessageDialectCapability.Ready(capability_name)
            : MessageDialectCapability.Missing(
                capability_name,
                "The active Unity Builders Club wall-placement header has a mixed or unsupported wire schema.");
    }

    private static bool IsUnityFloorItemPlacementSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 4 &&
        IsScalar(schema.Parameters[0], 0, OutgoingWireType.Int64) &&
        IsScalar(schema.Parameters[1], 1, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[2], 2, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[3], 3, OutgoingWireType.Int32);

    private static bool IsUnityWallItemPlacementSchema(OutgoingMessageSchema schema) =>
        schema.Parameters.Count == 2 &&
        IsScalar(schema.Parameters[0], 0, OutgoingWireType.Int64) &&
        IsScalar(schema.Parameters[1], 1, OutgoingWireType.Unknown);

    private static bool IsUnityBuildersClubWallOfferPlaceSchema(
        OutgoingMessageSchema schema,
        string wall_location_source_type) =>
        schema.Parameters.Count == 5 &&
        IsScalar(schema.Parameters[0], 0, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[1], 1, OutgoingWireType.Int32) &&
        IsScalar(schema.Parameters[2], 2, OutgoingWireType.String) &&
        IsScalar(schema.Parameters[3], 3, OutgoingWireType.Unknown) &&
        string.Equals(
            schema.Parameters[3].SourceType,
            wall_location_source_type,
            StringComparison.Ordinal) &&
        IsScalar(schema.Parameters[4], 4, OutgoingWireType.Boolean);

    private static MessageContract<T> Modern<T>(
        MessageKey key,
        Func<ClientType, MessageManager, Header, MessageDialectCapability>? unity_capability = null,
        bool allows_schema_selected_header = false)
        where T : IParserComposer<T>
    {
        ClientType unity = ClientType.Unity;
        MessageDialectCapabilityProbe? capability = unity_capability is null
            ? null
            : (messages, header) => unity_capability(unity, messages, header);
        return new(
            key,
            MessageDialectProjection<T>.FromModel(ClientType.Flash),
            MessageDialectProjection<T>.FromModel(
                unity,
                capability,
                allows_schema_selected_header));
    }

    private static MessageContract<NavigatorSearchResult> LegacyNavigatorSearchResult() =>
        new(
            MessageKeys.Navigator.Search.LegacyResult,
            new MessageDialectProjection<NavigatorSearchResult>(
                ClientType.Flash,
                ParseLegacyNavigatorSearchResult,
                ComposeLegacyNavigatorSearchResult),
            new MessageDialectProjection<NavigatorSearchResult>(
                ClientType.Unity,
                ParseLegacyNavigatorSearchResult,
                ComposeLegacyNavigatorSearchResult));

    private static NavigatorSearchResult ParseLegacyNavigatorSearchResult(
        in PacketReader reader)
    {
        int search_type = reader.ReadInt();
        string filter = reader.ReadString();
        int count = reader.ReadLength();
        if (count > reader.Available / (ClientTypes.IsUnity(reader.Client) ? 48 : 40))
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
                    rooms,
                    [])
            ]);
    }

    private static void ParseLegacyNavigatorResultFlags(in PacketReader reader)
    {
        if (reader.Available == 0)
            return;
        if (!ClientTypes.IsUnity(reader.Client) || reader.Available != 2)
            throw new InvalidDataException("The legacy navigator result contains an unexpected trailing payload.");
        reader.ReadBool();
        reader.ReadBool();
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
        if (count > reader.Available / (ClientTypes.IsUnity(reader.Client) ? 48 : 40))
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
        if (ClientTypes.IsUnity(writer.Client))
        {
            writer.WriteBool(false);
            writer.WriteBool(false);
        }
    }

    private static MessageContract<CallForHelpFromForumThread> ForumThreadReport() =>
        new(
            MessageKeys.Forums.ThreadReport,
            MessageDialectProjection<CallForHelpFromForumThread>.FromModel(ClientType.Flash),
            new MessageDialectProjection<CallForHelpFromForumThread>(
                ClientType.Unity,
                ParseUnityForumThreadReport,
                ComposeUnityForumThreadReport));

    private static CallForHelpFromForumThread ParseUnityForumThreadReport(
        in PacketReader reader)
    {
        ReportForumThread report = ReportForumThread.Parse(in reader);
        return new CallForHelpFromForumThread(
            report.GroupId,
            report.ThreadId,
            report.CategoryId,
            report.Report,
            "",
            "");
    }

    private static void ComposeUnityForumThreadReport(
        CallForHelpFromForumThread report,
        in PacketWriter writer)
    {
        if (report.FirstContext.Length != 0 || report.SecondContext.Length != 0)
        {
            throw new NotSupportedException(
                "Unity forum reports cannot represent Flash context strings.");
        }
        new ReportForumThread(
            report.GroupId,
            report.ThreadId,
            report.CategoryId,
            report.Report).Compose(in writer);
    }

    private static MessageContract<CallForHelpFromForumMessage> ForumMessageReport() =>
        new(
            MessageKeys.Forums.MessageReport,
            MessageDialectProjection<CallForHelpFromForumMessage>.FromModel(ClientType.Flash),
            new MessageDialectProjection<CallForHelpFromForumMessage>(
                ClientType.Unity,
                ParseUnityForumMessageReport,
                ComposeUnityForumMessageReport));

    private static CallForHelpFromForumMessage ParseUnityForumMessageReport(
        in PacketReader reader)
    {
        ReportForumMessage report = ReportForumMessage.Parse(in reader);
        return new CallForHelpFromForumMessage(
            report.GroupId,
            report.ThreadId,
            report.MessageId,
            report.CategoryId,
            report.Report,
            "",
            "");
    }

    private static void ComposeUnityForumMessageReport(
        CallForHelpFromForumMessage report,
        in PacketWriter writer)
    {
        if (report.FirstContext.Length != 0 || report.SecondContext.Length != 0)
        {
            throw new NotSupportedException(
                "Unity forum reports cannot represent Flash context strings.");
        }
        new ReportForumMessage(
            report.GroupId,
            report.ThreadId,
            report.MessageId,
            report.CategoryId,
            report.Report).Compose(in writer);
    }

    private static MessageContract<T> InventoryFurni<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageDialectProjection<T>.FromModel(ClientType.Flash),
            MessageDialectProjection<T>.FromModel(
                ClientType.Unity,
                UnityInventoryFurniCapability));

    private static MessageDialectCapability UnityInventoryFurniCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                "unityInventoryItemLayout",
                "The Unity client catalog is still loading its inventory-item layout.");
        }
        return profile.UnityInventoryItemHasExtendedMetadata switch
        {
            true => MessageDialectCapability.Ready("unityInventoryExtendedMetadata"),
            false => MessageDialectCapability.Ready("unityInventoryLegacyMetadata"),
            null => MessageDialectCapability.Missing(
                "unityInventoryItemLayout",
                "The active Unity session has no compatible inventory-item wire layout.")
        };
    }

    private static MessageDialectCapability UnityRoomSettingsSnapshotCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                "unityRoomSettingsLayout",
                "The Unity client catalog is still loading its room settings layout.");
        }
        return profile.UnityRoomSettingsLayout switch
        {
            UnityRoomSettingsWireLayout.Legacy =>
                MessageDialectCapability.Ready("unityRoomSettingsLegacy"),
            UnityRoomSettingsWireLayout.Modern =>
                MessageDialectCapability.Ready("unityRoomSettingsModern"),
            _ => MessageDialectCapability.Missing(
                "unityRoomSettingsLayout",
                "The active Unity session has no compatible room settings wire layout.")
        };
    }

    private static SaveRoomSettingsRequest ParseUnityRoomSettingsSave(in PacketReader p) =>
        SaveRoomSettingsRequest.ParseUnity(
            in p,
            RequireUnityRoomSettingsSaveLayout(p.Context, p.Header));

    private static void ComposeUnityRoomSettingsSave(
        SaveRoomSettingsRequest value,
        in PacketWriter p) =>
        value.ComposeUnity(in p, RequireUnityRoomSettingsSaveLayout(p.Context, p.Header));

    private static UnityRoomSettingsSaveWireLayout RequireUnityRoomSettingsSaveLayout(
        IParserContext? context,
        Header header)
    {
        if (context?.Messages is not MessageManager messages)
            throw new NotSupportedException("Unity room settings saves require message-catalog context.");
        MessageDialectCapability capability = ClassifyUnityRoomSettingsSave(
            messages,
            header,
            out UnityRoomSettingsSaveWireLayout layout);
        if (!capability.Available)
            throw new NotSupportedException(capability.Reason);
        return layout;
    }

    private static MessageDialectCapability UnityRoomSettingsSaveCapability(
        MessageManager messages,
        Header header) => ClassifyUnityRoomSettingsSave(messages, header, out _);

    private static MessageDialectCapability ClassifyUnityRoomSettingsSave(
        MessageManager messages,
        Header header,
        out UnityRoomSettingsSaveWireLayout layout)
    {
        const string capability_name = "unityRoomSettingsSchema";
        layout = default;
        if (!messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas))
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity room settings save header has no verified wire schema.");
        }

        UnityRoomSettingsSaveWireLayout? resolved_layout = null;
        foreach (OutgoingMessageSchema schema in schemas)
        {
            UnityRoomSettingsSaveWireLayout schema_layout;
            if (IsUnityRoomSettingsSaveSchema(schema, false))
                schema_layout = UnityRoomSettingsSaveWireLayout.Legacy;
            else if (IsUnityRoomSettingsSaveSchema(schema, true))
                schema_layout = UnityRoomSettingsSaveWireLayout.Modern;
            else
            {
                return MessageDialectCapability.Missing(
                    capability_name,
                    "The active Unity room settings save header has an unsupported wire schema.");
            }

            if (resolved_layout is UnityRoomSettingsSaveWireLayout previous && previous != schema_layout)
            {
                return MessageDialectCapability.Missing(
                    capability_name,
                    "The active Unity room settings save header has ambiguous wire schemas.");
            }
            resolved_layout = schema_layout;
        }

        if (resolved_layout is not UnityRoomSettingsSaveWireLayout resolved)
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity room settings save header has no usable wire schema.");
        }

        layout = resolved;
        return MessageDialectCapability.Ready(
            resolved is UnityRoomSettingsSaveWireLayout.Legacy
                ? "unityRoomSettings12"
                : "unityRoomSettings15");
    }

    private static bool IsUnityRoomSettingsSaveSchema(
        OutgoingMessageSchema schema,
        bool modern)
    {
        IReadOnlyList<OutgoingParameterSchema> parameters = schema.Parameters;
        if (parameters.Count != (modern ? 15 : 12) ||
            !IsScalar(parameters[0], 0, OutgoingWireType.Int64) ||
            !IsScalar(parameters[1], 1, OutgoingWireType.String) ||
            !IsScalar(parameters[2], 2, OutgoingWireType.String) ||
            !IsScalar(parameters[3], 3, OutgoingWireType.Int32) ||
            !IsScalar(parameters[4], 4, OutgoingWireType.String) ||
            !IsScalar(parameters[5], 5, OutgoingWireType.Int32) ||
            !IsScalar(parameters[6], 6, OutgoingWireType.Boolean) ||
            !IsScalar(parameters[7], 7, OutgoingWireType.Int32) ||
            !IsScalar(parameters[8], 8, OutgoingWireType.Int32) ||
            !IsScalar(parameters[9], 9, OutgoingWireType.Int32) ||
            !IsScalar(parameters[10], 10, OutgoingWireType.Int32))
        {
            return false;
        }

        if (!modern)
            return IsIdArray(parameters[11], 11);
        return IsScalar(parameters[11], 11, OutgoingWireType.Int32) &&
            IsScalar(parameters[12], 12, OutgoingWireType.Int32) &&
            IsScalar(parameters[13], 13, OutgoingWireType.Int32) &&
            IsIdArray(parameters[14], 14);
    }

    private static bool IsScalar(
        OutgoingParameterSchema parameter,
        int position,
        OutgoingWireType wire_type) =>
        parameter.Position == position &&
        parameter.WireType == wire_type &&
        parameter.Collection is OutgoingCollectionKind.None &&
        parameter.ElementWireTypes is null;

    private static bool IsIdArray(OutgoingParameterSchema parameter, int position) =>
        parameter.Position == position &&
        parameter.SourceType.Equals("long[]", StringComparison.Ordinal) &&
        parameter.WireType is OutgoingWireType.Int64 &&
        parameter.Collection is OutgoingCollectionKind.Array &&
        parameter.ElementWireTypes is null;

    private static UnignoreUserRequest ParseUnityUnignore(in PacketReader p) =>
        UnignoreUserRequest.ParseUnity(in p, RequireUnityUnignoreKind(p.Context, p.Header));

    private static void ComposeUnityUnignore(UnignoreUserRequest value, in PacketWriter p) =>
        value.ComposeUnity(in p, RequireUnityUnignoreKind(p.Context, p.Header));

    private static UserIdentityKind RequireUnityUnignoreKind(
        IParserContext? context,
        Header header)
    {
        if (context?.Messages is not MessageManager messages)
            throw new NotSupportedException("Unity unignore requests require message-catalog context.");
        MessageDialectCapability capability = ClassifyUnityUnignore(messages, header, out UserIdentityKind kind);
        if (!capability.Available)
            throw new NotSupportedException(capability.Reason);
        return kind;
    }

    private static MessageDialectCapability UnityUnignoreCapability(
        MessageManager messages,
        Header header) => ClassifyUnityUnignore(messages, header, out _);

    private static MessageDialectCapability ClassifyUnityUnignore(
        MessageManager messages,
        Header header,
        out UserIdentityKind kind)
    {
        const string capability_name = "unityUnignoreSchema";
        kind = default;
        if (!messages.TryGetOutgoingSchemas(
                ClientType.Unity,
                header,
                out IReadOnlyList<OutgoingMessageSchema> schemas))
        {
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity unignore header has no verified wire schema.");
        }

        UserIdentityKind? resolved_kind = null;
        foreach (OutgoingMessageSchema schema in schemas)
        {
            UserIdentityKind schema_kind;
            if (IsUnignoreSchema(schema, OutgoingWireType.Int64))
                schema_kind = UserIdentityKind.Id;
            else if (IsUnignoreSchema(schema, OutgoingWireType.String))
                schema_kind = UserIdentityKind.Name;
            else
                return MessageDialectCapability.Missing(
                    capability_name,
                    "The active Unity unignore header has an unsupported wire schema.");

            if (resolved_kind is UserIdentityKind previous && previous != schema_kind)
            {
                return MessageDialectCapability.Missing(
                    capability_name,
                    "The active Unity unignore header has an ambiguous wire schema.");
            }
            resolved_kind = schema_kind;
        }

        if (resolved_kind is not UserIdentityKind resolved)
            return MessageDialectCapability.Missing(
                capability_name,
                "The active Unity unignore header has no usable wire schema.");

        kind = resolved;
        return MessageDialectCapability.Ready(
            resolved is UserIdentityKind.Id
                ? "unityUnignoreIdSchema"
                : "unityUnignoreNameSchema");
    }

    private static bool IsUnignoreSchema(
        OutgoingMessageSchema schema,
        OutgoingWireType wire_type) =>
        schema.Parameters.Count == 1 &&
        schema.Parameters[0].Position == 0 &&
        schema.Parameters[0].WireType == wire_type &&
        schema.Parameters[0].Collection is OutgoingCollectionKind.None;

    private static MessageContract<T> Flash<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(key, MessageDialectProjection<T>.FromModel(ClientType.Flash));

    private static MessageContract<T> Unity<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(key, MessageDialectProjection<T>.FromModel(ClientType.Unity));

    private static MessageContract<T> WiredConfiguration<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageDialectProjection<T>.FromModel(ClientType.Flash),
            MessageDialectProjection<T>.FromModel(
                ClientType.Unity,
                UnityWiredConfigurationCapability));

    private static MessageContract<T> MarketplaceLayout<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageDialectProjection<T>.FromModel(
                ClientType.Flash,
                FlashMarketplaceLayoutCapability),
            MessageDialectProjection<T>.FromModel(ClientType.Unity));

    private static MessageContract<T> ModernMarketplace<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageDialectProjection<T>.FromModel(
                ClientType.Flash,
                ModernFlashMarketplaceCapability),
            MessageDialectProjection<T>.FromModel(ClientType.Unity));

    private static MessageContract<T> ModernFlashMarketplace<T>(MessageKey key)
        where T : IParserComposer<T> =>
        new(
            key,
            MessageDialectProjection<T>.FromModel(
                ClientType.Flash,
                ModernFlashMarketplaceCapability));

    private static MessageDialectCapability FlashMarketplaceLayoutCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Flash);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                "flashMarketplaceLayout",
                "The Flash client catalog is still loading its marketplace layout.");
        }
        return profile.FlashMarketplaceLayout is FlashMarketplaceWireLayout.Unknown
            ? MessageDialectCapability.Missing(
                "flashMarketplaceLayout",
                "The active Flash build has no exact marketplace wire profile.")
            : MessageDialectCapability.Ready("flashMarketplaceLayout");
    }

    private static MessageDialectCapability UnityWiredConfigurationCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                "unityWiredConfigurationLayout",
                "The Unity client catalog is still loading its wired configuration layout.");
        }
        return profile.IsExact
            ? MessageDialectCapability.Ready("unityWiredConfigurationLayout")
            : MessageDialectCapability.Missing(
                "unityWiredConfigurationLayout",
                "The active Unity session has no compatible wired configuration layout.");
    }

    private static MessageDialectCapability ModernFlashMarketplaceCapability(
        MessageManager messages,
        Header header)
    {
        MessageDialectCapability layout =
            FlashMarketplaceLayoutCapability(messages, header);
        if (!layout.Available)
            return layout;
        return messages.GetWireProfile(ClientType.Flash).FlashMarketplaceLayout is
            FlashMarketplaceWireLayout.Modern
                ? MessageDialectCapability.Ready("flashMarketplaceModernLayout")
                : MessageDialectCapability.Missing(
                    "flashMarketplaceModernLayout",
                    "The active Flash build uses the legacy marketplace layout.");
    }

    private static MessageDialectCapability UnityMarketplaceBuyCapability(
        MessageManager messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile(ClientType.Unity);
        if (!profile.IsAnalyzed)
        {
            return MessageDialectCapability.Missing(
                "unityMarketplaceBuyLayout",
                "The Unity client catalog is still loading its marketplace purchase layout.");
        }
        if (profile.UnityMarketplaceBuyLayout is MarketplaceBuyWireLayout.Unknown ||
            profile.UnityMarketplaceBuyHeaderId is null)
        {
            return MessageDialectCapability.Missing(
                "unityMarketplaceBuyLayout",
                "The active Unity session has no compatible marketplace purchase wire layout.");
        }
        if (profile.UnityMarketplaceBuyHeaderId != header.Value)
        {
            return MessageDialectCapability.Missing(
                "unityMarketplaceBuyLayout",
                "The resolved Unity purchase header does not match the analyzed marketplace layout.");
        }
        return MessageDialectCapability.Ready("unityMarketplaceBuyLayout");
    }
}
