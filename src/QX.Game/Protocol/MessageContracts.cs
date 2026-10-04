using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Protocol;

/// <summary>Provides the message contracts, grouped by feature.</summary>
public static class MessageContracts
{
    /// <summary>Gets every contract declared in <see cref="MessageContracts"/>.</summary>
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
        Leaderboards.Total.Request,
        Leaderboards.Total.Snapshot,
        Leaderboards.Friends.Request,
        Leaderboards.Friends.Snapshot,
        Leaderboards.Groups.Request,
        Leaderboards.Groups.Snapshot,
        Leaderboards.WeeklyTotal.Request,
        Leaderboards.WeeklyTotal.Snapshot,
        Leaderboards.WeeklyFriends.Request,
        Leaderboards.WeeklyFriends.Snapshot,
        Leaderboards.WeeklyGroups.Request,
        Leaderboards.WeeklyGroups.Snapshot,
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
        Catalog.PurchaseAccepted,
        Catalog.PurchaseFailed,
        Catalog.PurchaseForbidden,
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
        Wired.Account.Preferences,
        Wired.WebApi.KeyGenerate,
        Wired.WebApi.KeyResult,
        Wired.State.Permissions,
        Wired.State.Environment,
        Wired.State.ClickSettings,
        Wired.State.MenuError,
        Wired.State.RewardResult,
        Wired.VariableFx.Configs,
        Wired.VariableFx.ConfigsRemoved,
        Wired.VariableFx.Statuses,
        Wired.VariableFx.StatusesRemoved,
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
        Wired.Variables.HoldersDelete,
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
        Room.Environment.AreaHide,
        Room.Environment.AreaHideSet,
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
        Room.Objects,
        Room.WallItems,
        Room.SnapshotRequest,
        Room.Snapshot,
        Room.StaffPickUpdateRequest,
        Room.FloorItem.Use,
        Room.WallItem.Use,
        Room.WallItem.Remove,
        Room.Item.Place,
        Room.WallItem.StickyDataSet,
        Room.WallItem.StickyDataRequest,
        Room.WallItem.StickyData,
        Room.WallItem.PostItPlace,
        Room.WallItem.SpamPostItAdd,
        Room.FloorItem.Move,
        Room.WallItem.Move,
        Room.FloorItem.Added,
        Room.FloorItem.Removed,
        Room.FloorItem.RemovedMultiple,
        Room.FloorItem.Updated,
        Room.FloorItem.DataUpdated,
        Room.FloorItem.DataBatchUpdated,
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
        Room.Item.Pickup,
        Room.Item.Click,
        Room.Item.PickupConfirmation,
        Room.WallItem.Added,
        Room.WallItem.Removed,
        Room.WallItem.RemovedMultiple,
        Room.WallItem.Updated,
        Room.WallItem.DataUpdated,
        Room.WallItem.DataBatchUpdated,
        Room.Heightmap.Snapshot,
        Room.Heightmap.Diff,
        Room.Moderation.BansRequest,
        Room.Moderation.BansSnapshot,
        Room.Moderation.UserUnbanned,
        Room.Moderation.Mute,
        Room.Moderation.Kick,
        Room.Moderation.Ban,
        Room.Moderation.Unban,
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

    /// <summary>Contains the error message contracts.</summary>
    public static class Errors
    {
        /// <summary>The contract for the incoming <c>GenericError</c> message, which reports a generic error code.</summary>
        public static readonly MessageContract<GenericError> Generic =
            Flash<GenericError>(MessageKeys.Errors.Generic);
    }

    /// <summary>Contains the session message contracts.</summary>
    public static class Session
    {
        /// <summary>The contract for the incoming <c>DisconnectReason</c> message, which carries the reason the server is closing the connection.</summary>
        public static readonly MessageContract<DisconnectReason> DisconnectReason =
            Flash<DisconnectReason>(MessageKeys.Session.DisconnectReason);
    }

    /// <summary>Contains the achievement message contracts.</summary>
    public static class Achievements
    {
        /// <summary>The contract for the outgoing <c>GetAchievements</c> message, which requests the local user's achievements.</summary>
        public static readonly MessageContract<AchievementsRequest> Request =
            Flash<AchievementsRequest>(MessageKeys.Achievements.Request);

        /// <summary>The contract for the incoming <c>Achievements</c> message, which carries the local user's achievements.</summary>
        public static readonly MessageContract<Qx.Model.Messages.Incoming.Achievements> Snapshot =
            Flash<Qx.Model.Messages.Incoming.Achievements>(MessageKeys.Achievements.Snapshot);

        /// <summary>The contract for the incoming <c>Achievement</c> message, which carries an updated achievement.</summary>
        public static readonly MessageContract<AchievementUpdate> Updated =
            Flash<AchievementUpdate>(MessageKeys.Achievements.Updated);

        /// <summary>The contract for the incoming <c>AchievementsScore</c> message, which carries the local user's achievement score.</summary>
        public static readonly MessageContract<AchievementScore> Score =
            Flash<AchievementScore>(MessageKeys.Achievements.Score);

        /// <summary>The contract for the outgoing <c>GetBadgePointLimits</c> message, which requests the points each badge level requires.</summary>
        public static readonly MessageContract<BadgePointLimitsRequest> PointLimitsRequest =
            Flash<BadgePointLimitsRequest>(MessageKeys.Achievements.PointLimitsRequest);

        /// <summary>The contract for the incoming <c>BadgePointLimits</c> message, which carries the points each badge level requires.</summary>
        public static readonly MessageContract<BadgePointLimits> PointLimits =
            Flash<BadgePointLimits>(MessageKeys.Achievements.PointLimits);

        /// <summary>The contract for the incoming <c>HabboAchievementNotification</c> message, which announces an achievement level the local user reached.</summary>
        public static readonly MessageContract<AchievementNotification> Notification =
            Flash<AchievementNotification>(MessageKeys.Achievements.Notification);
    }

    /// <summary>Contains the badge message contracts.</summary>
    public static class Badges
    {
        /// <summary>The contract for the outgoing <c>GetBadges</c> message, which requests the local user's owned badges.</summary>
        public static readonly MessageContract<BadgeInventoryRequest> Request =
            Flash<BadgeInventoryRequest>(MessageKeys.Badges.Request);

        /// <summary>The contract for the incoming <c>Badges</c> message, which carries one page of the local user's owned badges.</summary>
        public static readonly MessageContract<BadgeInventory> Snapshot =
            Flash<BadgeInventory>(MessageKeys.Badges.Snapshot);

        /// <summary>The contract for the outgoing <c>GetSelectedBadges</c> message, which requests the badges a user has selected to wear.</summary>
        public static readonly MessageContract<SelectedBadgesRequest> SelectedRequest =
            Flash<SelectedBadgesRequest>(MessageKeys.Badges.SelectedRequest);

        /// <summary>The contract for the incoming <c>BadgeReceived</c> message, which announces a badge the local user received.</summary>
        public static readonly MessageContract<BadgeReceived> Received =
            Flash<BadgeReceived>(MessageKeys.Badges.Received);

        /// <summary>The contract for the incoming <c>HabboUserBadges</c> message, which carries the badges a user has selected to wear.</summary>
        public static readonly MessageContract<UserBadges> Selected =
            Flash<UserBadges>(MessageKeys.Badges.Selected);
    }

    /// <summary>Contains the credit and activity point message contracts.</summary>
    public static class Wallet
    {
        /// <summary>The contract for the outgoing <c>GetCreditsInfo</c> message, which requests the local user's credit balance.</summary>
        public static readonly MessageContract<WalletBalanceRequest> CreditsRequest =
            Flash<WalletBalanceRequest>(MessageKeys.Wallet.CreditsRequest);

        /// <summary>The contract for the incoming <c>CreditBalance</c> message, which carries the local user's credit balance.</summary>
        public static readonly MessageContract<CreditBalance> CreditsBalance =
            Flash<CreditBalance>(MessageKeys.Wallet.CreditsBalance);

        /// <summary>The contract for the incoming <c>ActivityPoints</c> message, which carries the local user's activity point balances.</summary>
        public static readonly MessageContract<ActivityPoints> ActivityPoints =
            Flash<ActivityPoints>(MessageKeys.Wallet.ActivityPoints);

        /// <summary>The contract for the incoming <c>HabboActivityPointNotification</c> message, which reports a change to one activity point balance.</summary>
        public static readonly MessageContract<ActivityPointNotification> ActivityPointUpdated =
            Flash<ActivityPointNotification>(MessageKeys.Wallet.ActivityPointUpdated);
    }

    /// <summary>Contains the earning message contracts.</summary>
    public static class Earnings
    {
        /// <summary>The contract for the outgoing <c>IncomeRewardStatus</c> message, which requests the local user's earning status.</summary>
        public static readonly MessageContract<EarningStatusRequest> StatusRequest =
            Flash<EarningStatusRequest>(MessageKeys.Earnings.StatusRequest);

        /// <summary>The contract for the incoming <c>IncomeRewardStatus</c> message, which carries the local user's earning status.</summary>
        public static readonly MessageContract<EarningStatus> StatusSnapshot =
            Flash<EarningStatus>(MessageKeys.Earnings.StatusSnapshot);

        /// <summary>The contract for the outgoing <c>IncomeRewardClaim</c> message, which claims the earnings in a category.</summary>
        public static readonly MessageContract<EarningClaimRequest> Claim =
            Flash<EarningClaimRequest>(MessageKeys.Earnings.Claim);

        /// <summary>The contract for the incoming <c>IncomeRewardClaimResponse</c> message, which carries the result of an earning claim.</summary>
        public static readonly MessageContract<EarningClaimResult> Claimed =
            Flash<EarningClaimResult>(MessageKeys.Earnings.Claimed);

        /// <summary>The contract for the incoming <c>IncomeRewardNotification</c> message, which notifies the local user of earnings in a category.</summary>
        public static readonly MessageContract<EarningNotification> Notification =
            Flash<EarningNotification>(MessageKeys.Earnings.Notification);
    }

    /// <summary>Contains the daily task message contracts.</summary>
    public static class DailyTasks
    {
        /// <summary>The contract for the outgoing <c>GetDailyTasks</c> message, which requests the running daily tasks.</summary>
        public static readonly MessageContract<DailyTaskListRequest> Request =
            Flash<DailyTaskListRequest>(MessageKeys.DailyTasks.Request);

        /// <summary>The contract for the incoming <c>DailyTasksActiveList</c> message, which carries the running daily tasks.</summary>
        public static readonly MessageContract<DailyTasksActiveList> Snapshot =
            Flash<DailyTasksActiveList>(MessageKeys.DailyTasks.Snapshot);

        /// <summary>The contract for the incoming <c>DailyTasksTasksAdded</c> message, which carries daily tasks the hotel added to the running set.</summary>
        public static readonly MessageContract<DailyTasksTasksAdded> Added =
            Flash<DailyTasksTasksAdded>(MessageKeys.DailyTasks.Added);

        /// <summary>The contract for the incoming <c>DailyTasksTaskUpdate</c> message, which carries the progress of one running daily task.</summary>
        public static readonly MessageContract<DailyTasksTaskUpdate> Updated =
            Flash<DailyTasksTaskUpdate>(MessageKeys.DailyTasks.Updated);

        /// <summary>The contract for the outgoing <c>ClaimDailyTask</c> message, which claims a daily task.</summary>
        public static readonly MessageContract<DailyTaskClaimRequest> Claim =
            Flash<DailyTaskClaimRequest>(MessageKeys.DailyTasks.Claim);
    }

    /// <summary>Contains the quest message contracts.</summary>
    public static class Quests
    {
        /// <summary>The contract for the outgoing <c>GetQuests</c> message, which requests the available quests.</summary>
        public static readonly MessageContract<GetQuests> Request =
            Flash<GetQuests>(MessageKeys.Quests.Request);

        /// <summary>The contract for the incoming <c>Quests</c> message, which carries the available quests.</summary>
        public static readonly MessageContract<global::Qx.Model.Messages.Incoming.Quests> Snapshot =
            Flash<global::Qx.Model.Messages.Incoming.Quests>(MessageKeys.Quests.Snapshot);

        /// <summary>The contract for the outgoing <c>GetSeasonalQuestsOnly</c> message, which requests the seasonal quests.</summary>
        public static readonly MessageContract<GetSeasonalQuests> SeasonalRequest =
            Flash<GetSeasonalQuests>(MessageKeys.Quests.SeasonalRequest);

        /// <summary>The contract for the incoming <c>SeasonalQuests</c> message, which carries the seasonal quests.</summary>
        public static readonly MessageContract<QuestsSeasonal> SeasonalSnapshot =
            Flash<QuestsSeasonal>(MessageKeys.Quests.SeasonalSnapshot);

        /// <summary>The contract for the incoming <c>Quest</c> message, which carries an updated quest.</summary>
        public static readonly MessageContract<Quest> Updated =
            Flash<Quest>(MessageKeys.Quests.Updated);

        /// <summary>The contract for the incoming <c>QuestCompleted</c> message, which announces a completed quest.</summary>
        public static readonly MessageContract<QuestCompleted> Completed =
            Flash<QuestCompleted>(MessageKeys.Quests.Completed);

        /// <summary>The contract for the incoming <c>QuestCancelled</c> message, which announces a canceled quest.</summary>
        public static readonly MessageContract<QuestCancelled> Cancelled =
            Flash<QuestCancelled>(MessageKeys.Quests.Cancelled);

        /// <summary>The contract for the outgoing <c>GetDailyQuest</c> message, which requests a daily quest by difficulty and index.</summary>
        public static readonly MessageContract<GetDailyQuest> DailyRequest =
            Flash<GetDailyQuest>(MessageKeys.Quests.DailyRequest);

        /// <summary>The contract for the incoming <c>QuestDaily</c> message, which carries a daily quest and the number of easy and hard daily quests.</summary>
        public static readonly MessageContract<QuestDaily> Daily =
            Flash<QuestDaily>(MessageKeys.Quests.Daily);

        /// <summary>The contract for the outgoing <c>AcceptQuest</c> message, which accepts a quest.</summary>
        public static readonly MessageContract<AcceptQuest> Accept =
            Flash<AcceptQuest>(MessageKeys.Quests.Accept);

        /// <summary>The contract for the outgoing <c>ActivateQuest</c> message, which activates a quest.</summary>
        public static readonly MessageContract<ActivateQuest> Activate =
            Flash<ActivateQuest>(MessageKeys.Quests.Activate);

        /// <summary>The contract for the outgoing <c>RejectQuest</c> message, which rejects a quest.</summary>
        public static readonly MessageContract<RejectQuest> Reject =
            Flash<RejectQuest>(MessageKeys.Quests.Reject);

        /// <summary>The contract for the outgoing <c>CancelQuest</c> message, which cancels the active quest.</summary>
        public static readonly MessageContract<CancelQuest> Cancel =
            Flash<CancelQuest>(MessageKeys.Quests.Cancel);

        /// <summary>The contract for the outgoing <c>OpenQuestTracker</c> message, which tells the hotel that the quest tracker was opened.</summary>
        public static readonly MessageContract<OpenQuestTracker> TrackerOpen =
            Flash<OpenQuestTracker>(MessageKeys.Quests.TrackerOpen);

        /// <summary>The contract for the outgoing <c>FriendRequestQuestComplete</c> message, which reports progress on the friend request quest step.</summary>
        public static readonly MessageContract<FriendRequestQuestComplete> FriendRequestCompleted =
            Flash<FriendRequestQuestComplete>(MessageKeys.Quests.FriendRequestCompleted);
    }

    /// <summary>Contains the habbicon message contracts.</summary>
    public static class Habbicons
    {
        /// <summary>The contract for the outgoing <c>GetHabbiconShopData</c> message, which requests the habbicon shop.</summary>
        public static readonly MessageContract<HabbiconShopRequest> ShopRequest =
            Flash<HabbiconShopRequest>(MessageKeys.Habbicons.ShopRequest);

        /// <summary>The contract for the incoming <c>HabbiconShopData</c> message, which carries every habbicon collection in the shop with its icons.</summary>
        public static readonly MessageContract<HabbiconShopData> ShopSnapshot =
            Flash<HabbiconShopData>(MessageKeys.Habbicons.ShopSnapshot);

        /// <summary>The contract for the incoming <c>UserHabbicons</c> message, which carries the local user's habbicon states and recently used icons.</summary>
        public static readonly MessageContract<UserHabbicons> InventorySnapshot =
            Flash<UserHabbicons>(MessageKeys.Habbicons.InventorySnapshot);

        /// <summary>The contract for the incoming <c>UserHabbiconStatusChanged</c> message, which reports a change to the state of one habbicon.</summary>
        public static readonly MessageContract<UserHabbiconStatusChanged> StatusUpdated =
            Flash<UserHabbiconStatusChanged>(MessageKeys.Habbicons.StatusUpdated);

        /// <summary>The contract for the outgoing <c>GetHabbiconInfo</c> message, which requests the details of one habbicon.</summary>
        public static readonly MessageContract<HabbiconInfoRequest> InfoRequest =
            Flash<HabbiconInfoRequest>(MessageKeys.Habbicons.InfoRequest);

        /// <summary>The contract for the incoming <c>HabbiconInfo</c> message, which carries the details of one habbicon.</summary>
        public static readonly MessageContract<HabbiconInfo> InfoSnapshot =
            Flash<HabbiconInfo>(MessageKeys.Habbicons.InfoSnapshot);

        /// <summary>The contract for the incoming <c>RoomUseHabbicon</c> message, which reports that an avatar in the room used a habbicon.</summary>
        public static readonly MessageContract<RoomUseHabbicon> RoomUsed =
            Flash<RoomUseHabbicon>(MessageKeys.Habbicons.RoomUsed);

        /// <summary>The contract for the outgoing <c>BuyHabbicon</c> message, which buys a habbicon.</summary>
        public static readonly MessageContract<HabbiconBuyRequest> Buy =
            Flash<HabbiconBuyRequest>(MessageKeys.Habbicons.Buy);

        /// <summary>The contract for the outgoing <c>BuyHabbiconCollection</c> message, which buys a habbicon collection.</summary>
        public static readonly MessageContract<HabbiconCollectionBuyRequest> BuyCollection =
            Flash<HabbiconCollectionBuyRequest>(MessageKeys.Habbicons.BuyCollection);

        /// <summary>The contract for the outgoing <c>ClaimHabbicon</c> message, which claims an earned habbicon.</summary>
        public static readonly MessageContract<HabbiconClaimRequest> Claim =
            Flash<HabbiconClaimRequest>(MessageKeys.Habbicons.Claim);

        /// <summary>The contract for the outgoing <c>FavoriteHabbicon</c> message, which marks a habbicon as a favorite.</summary>
        public static readonly MessageContract<HabbiconFavoriteRequest> Favorite =
            Flash<HabbiconFavoriteRequest>(MessageKeys.Habbicons.Favorite);

        /// <summary>The contract for the outgoing <c>UnfavoriteHabbicon</c> message, which removes a habbicon from the favorites.</summary>
        public static readonly MessageContract<HabbiconUnfavoriteRequest> Unfavorite =
            Flash<HabbiconUnfavoriteRequest>(MessageKeys.Habbicons.Unfavorite);
    }

    /// <summary>Contains the leaderboard message contracts.</summary>
    public static class Leaderboards
    {
        /// <summary>Contains the total leaderboard message contracts.</summary>
        public static class Total
        {
            /// <summary>The contract for the outgoing <c>Game2GetTotalLeaderboard</c> message, which requests the all-time leaderboard covering everyone.</summary>
            public static readonly MessageContract<LeaderboardRequest> Request =
                Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Total.Request);

            /// <summary>The contract for the incoming <c>Game2TotalLeaderboard</c> message, which carries the all-time leaderboard covering everyone.</summary>
            public static readonly MessageContract<TotalLeaderboard> Snapshot =
                Flash<TotalLeaderboard>(MessageKeys.Leaderboards.Total.Snapshot);
        }

        /// <summary>Contains the friends leaderboard message contracts.</summary>
        public static class Friends
        {
            /// <summary>The contract for the outgoing <c>Game2GetFriendsLeaderboard</c> message, which requests the all-time leaderboard covering the local user's friends.</summary>
            public static readonly MessageContract<LeaderboardRequest> Request =
                Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Friends.Request);

            /// <summary>The contract for the incoming <c>Game2FriendsLeaderboard</c> message, which carries the all-time leaderboard covering the local user's friends.</summary>
            public static readonly MessageContract<FriendsLeaderboard> Snapshot =
                Flash<FriendsLeaderboard>(MessageKeys.Leaderboards.Friends.Snapshot);
        }

        /// <summary>Contains the group leaderboard message contracts.</summary>
        public static class Groups
        {
            /// <summary>The contract for the outgoing <c>Game2GetTotalGroupLeaderboard</c> message, which requests the all-time leaderboard covering groups.</summary>
            public static readonly MessageContract<LeaderboardRequest> Request =
                Flash<LeaderboardRequest>(MessageKeys.Leaderboards.Groups.Request);

            /// <summary>The contract for the incoming <c>Game2TotalGroupLeaderboard</c> message, which carries the all-time leaderboard covering groups.</summary>
            public static readonly MessageContract<TotalGroupLeaderboard> Snapshot =
                Flash<TotalGroupLeaderboard>(MessageKeys.Leaderboards.Groups.Snapshot);
        }

        /// <summary>Contains the weekly total leaderboard message contracts.</summary>
        public static class WeeklyTotal
        {
            /// <summary>The contract for the outgoing <c>Game2GetWeeklyLeaderboard</c> message, which requests the weekly leaderboard covering everyone.</summary>
            public static readonly MessageContract<WeeklyLeaderboardRequest> Request =
                Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyTotal.Request);

            /// <summary>The contract for the incoming <c>Game2WeeklyLeaderboard</c> message, which carries the weekly leaderboard covering everyone.</summary>
            public static readonly MessageContract<WeeklyLeaderboard> Snapshot =
                Flash<WeeklyLeaderboard>(MessageKeys.Leaderboards.WeeklyTotal.Snapshot);
        }

        /// <summary>Contains the weekly friends leaderboard message contracts.</summary>
        public static class WeeklyFriends
        {
            /// <summary>The contract for the outgoing <c>Game2GetWeeklyFriendsLeaderboard</c> message, which requests the weekly leaderboard covering the local user's friends.</summary>
            public static readonly MessageContract<WeeklyLeaderboardRequest> Request =
                Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyFriends.Request);

            /// <summary>The contract for the incoming <c>Game2WeeklyFriendsLeaderboard</c> message, which carries the weekly leaderboard covering the local user's friends.</summary>
            public static readonly MessageContract<WeeklyFriendsLeaderboard> Snapshot =
                Flash<WeeklyFriendsLeaderboard>(MessageKeys.Leaderboards.WeeklyFriends.Snapshot);
        }

        /// <summary>Contains the weekly group leaderboard message contracts.</summary>
        public static class WeeklyGroups
        {
            /// <summary>The contract for the outgoing <c>Game2GetWeeklyGroupLeaderboard</c> message, which requests the weekly leaderboard covering groups.</summary>
            public static readonly MessageContract<WeeklyLeaderboardRequest> Request =
                Flash<WeeklyLeaderboardRequest>(MessageKeys.Leaderboards.WeeklyGroups.Request);

            /// <summary>The contract for the incoming <c>Game2WeeklyGroupLeaderboard</c> message, which carries the weekly leaderboard covering groups.</summary>
            public static readonly MessageContract<WeeklyGroupLeaderboard> Snapshot =
                Flash<WeeklyGroupLeaderboard>(MessageKeys.Leaderboards.WeeklyGroups.Snapshot);
        }
    }

    /// <summary>Contains the group forum message contracts.</summary>
    public static class Forums
    {
        /// <summary>The contract for the incoming <c>ForumData</c> message, which carries the details of a group forum.</summary>
        public static readonly MessageContract<ForumData> Stats =
            Flash<ForumData>(MessageKeys.Forums.Stats);

        /// <summary>The contract for the incoming <c>ForumsList</c> message, which carries a page of the forum directory.</summary>
        public static readonly MessageContract<ForumsList> List =
            Flash<ForumsList>(MessageKeys.Forums.List);

        /// <summary>The contract for the incoming <c>ForumThreads</c> message, which carries a page of threads in a group forum.</summary>
        public static readonly MessageContract<ForumThreads> Threads =
            Flash<ForumThreads>(MessageKeys.Forums.Threads);

        /// <summary>The contract for the incoming <c>ThreadMessages</c> message, which carries a page of messages in a forum thread.</summary>
        public static readonly MessageContract<ThreadMessages> Messages =
            Flash<ThreadMessages>(MessageKeys.Forums.Messages);

        /// <summary>The contract for the incoming <c>PostThread</c> message, which announces a thread created in a group forum.</summary>
        public static readonly MessageContract<PostThread> ThreadCreated =
            Flash<PostThread>(MessageKeys.Forums.ThreadCreated);

        /// <summary>The contract for the incoming <c>PostMessage</c> message, which announces a message posted in a forum thread.</summary>
        public static readonly MessageContract<PostMessage> MessageCreated =
            Flash<PostMessage>(MessageKeys.Forums.MessageCreated);

        /// <summary>The contract for the incoming <c>UpdateThread</c> message, which carries an updated forum thread.</summary>
        public static readonly MessageContract<UpdateThread> ThreadUpdated =
            Flash<UpdateThread>(MessageKeys.Forums.ThreadUpdated);

        /// <summary>The contract for the incoming <c>UpdateMessage</c> message, which carries a forum message that changed, such as after moderation.</summary>
        public static readonly MessageContract<UpdateMessage> MessageUpdated =
            Flash<UpdateMessage>(MessageKeys.Forums.MessageUpdated);

        /// <summary>The contract for the incoming <c>UnreadForumsCount</c> message, which carries the number of forums with unread messages.</summary>
        public static readonly MessageContract<UnreadForumsCount> UnreadCount =
            Flash<UnreadForumsCount>(MessageKeys.Forums.UnreadCount);

        /// <summary>The contract for the outgoing <c>GetForumStats</c> message, which requests the details of a group forum.</summary>
        public static readonly MessageContract<GetForumStats> StatsRequest =
            Flash<GetForumStats>(MessageKeys.Forums.StatsRequest);

        /// <summary>The contract for the outgoing <c>GetForumsList</c> message, which requests a page of the forum directory.</summary>
        public static readonly MessageContract<GetForumsList> ListRequest =
            Flash<GetForumsList>(MessageKeys.Forums.ListRequest);

        /// <summary>The contract for the outgoing <c>GetThreads</c> message, which requests a page of threads in a group forum.</summary>
        public static readonly MessageContract<GetForumThreads> ThreadsRequest =
            Flash<GetForumThreads>(MessageKeys.Forums.ThreadsRequest);

        /// <summary>The contract for the outgoing <c>GetMessages</c> message, which requests a page of messages in a forum thread.</summary>
        public static readonly MessageContract<GetForumThreadMessages> MessagesRequest =
            Flash<GetForumThreadMessages>(MessageKeys.Forums.MessagesRequest);

        /// <summary>The contract for the outgoing <c>GetThread</c> message, which requests a single forum thread without its messages.</summary>
        public static readonly MessageContract<GetForumThread> ThreadRequest =
            Flash<GetForumThread>(MessageKeys.Forums.ThreadRequest);

        /// <summary>The contract for the outgoing <c>GetUnreadForumsCount</c> message, which requests the number of forums with unread messages.</summary>
        public static readonly MessageContract<GetUnreadForumsCount> UnreadCountRequest =
            Flash<GetUnreadForumsCount>(MessageKeys.Forums.UnreadCountRequest);

        /// <summary>The contract for the outgoing <c>PostMessage</c> message, which posts a message to a forum thread or starts a new thread.</summary>
        public static readonly MessageContract<PostMessage> Post =
            Flash<PostMessage>(MessageKeys.Forums.Post);

        /// <summary>The contract for the outgoing <c>ModerateThread</c> message, which hides or restores a forum thread.</summary>
        public static readonly MessageContract<ModerateForumThread> ThreadModerate =
            Flash<ModerateForumThread>(MessageKeys.Forums.ThreadModerate);

        /// <summary>The contract for the outgoing <c>ModerateMessage</c> message, which hides or restores a forum message.</summary>
        public static readonly MessageContract<ModerateForumMessage> MessageModerate =
            Flash<ModerateForumMessage>(MessageKeys.Forums.MessageModerate);

        /// <summary>The contract for the outgoing <c>UpdateForumSettings</c> message, which changes the permission levels of a group forum.</summary>
        public static readonly MessageContract<UpdateForumSettings> SettingsUpdate =
            Flash<UpdateForumSettings>(MessageKeys.Forums.SettingsUpdate);

        /// <summary>The contract for the outgoing <c>UpdateForumReadMarker</c> message, which marks group forums as read up to a given message.</summary>
        public static readonly MessageContract<UpdateForumReadMarkers> ReadMarkersUpdate =
            Flash<UpdateForumReadMarkers>(MessageKeys.Forums.ReadMarkersUpdate);

        /// <summary>The contract for the outgoing <c>UpdateThread</c> message, which changes the sticky and locked flags of a forum thread.</summary>
        public static readonly MessageContract<UpdateThread> ThreadUpdate =
            Flash<UpdateThread>(MessageKeys.Forums.ThreadUpdate);

        /// <summary>The contract for the outgoing <c>CallForHelpFromForumThread</c> message, which reports a forum thread to the moderators.</summary>
        public static readonly MessageContract<CallForHelpFromForumThread> ThreadReport =
            ForumThreadReport();

        /// <summary>The contract for the outgoing <c>CallForHelpFromForumMessage</c> message, which reports a forum message to the moderators.</summary>
        public static readonly MessageContract<CallForHelpFromForumMessage> MessageReport =
            ForumMessageReport();
    }

    /// <summary>Contains the catalog message contracts.</summary>
    public static class Catalog
    {
        /// <summary>The contract for the outgoing <c>GetCatalogIndex</c> message, which requests the catalog index for a catalog type.</summary>
        public static readonly MessageContract<CatalogIndexRequest> IndexRequest =
            Flash<CatalogIndexRequest>(MessageKeys.Catalog.IndexRequest);

        /// <summary>The contract for the incoming <c>CatalogIndex</c> message, which carries the catalog index.</summary>
        public static readonly MessageContract<CatalogIndex> IndexSnapshot =
            Flash<CatalogIndex>(MessageKeys.Catalog.IndexSnapshot);

        /// <summary>The contract for the outgoing <c>GetCatalogPage</c> message, which requests a catalog page.</summary>
        public static readonly MessageContract<CatalogPageRequest> PageRequest =
            Flash<CatalogPageRequest>(MessageKeys.Catalog.PageRequest);

        /// <summary>The contract for the incoming <c>CatalogPage</c> message, which carries a catalog page and its offers.</summary>
        public static readonly MessageContract<CatalogPage> PageSnapshot =
            Flash<CatalogPage>(MessageKeys.Catalog.PageSnapshot);

        /// <summary>The contract for the outgoing <c>PurchaseFromCatalog</c> message, which buys an offer from a catalog page.</summary>
        public static readonly MessageContract<PurchaseFromCatalogRequest> Purchase =
            Flash<PurchaseFromCatalogRequest>(MessageKeys.Catalog.Purchase);

        /// <summary>The contract for the incoming <c>PurchaseOk</c> message, which confirms a catalog purchase and carries the purchased offer.</summary>
        public static readonly MessageContract<PurchaseOK> PurchaseAccepted =
            Flash<PurchaseOK>(MessageKeys.Catalog.PurchaseAccepted);

        /// <summary>The contract for the incoming <c>PurchaseError</c> message, which reports that a catalog purchase failed, with an error code.</summary>
        public static readonly MessageContract<PurchaseError> PurchaseFailed =
            Flash<PurchaseError>(MessageKeys.Catalog.PurchaseFailed);

        /// <summary>The contract for the incoming <c>PurchaseNotAllowed</c> message, which reports that a catalog purchase is not allowed, with an error code.</summary>
        public static readonly MessageContract<PurchaseNotAllowed> PurchaseForbidden =
            Flash<PurchaseNotAllowed>(MessageKeys.Catalog.PurchaseForbidden);

        /// <summary>The contract for the incoming <c>CatalogPublished</c> message, which announces that the catalog was updated.</summary>
        public static readonly MessageContract<CatalogPublished> Published =
            Flash<CatalogPublished>(MessageKeys.Catalog.Published);

        /// <summary>The contract for the outgoing <c>GetRoomAdPurchaseInfo</c> message, which requests the rooms that may be advertised.</summary>
        public static readonly MessageContract<GetRoomAdPurchaseInfo> RoomAdInfoRequest =
            Flash<GetRoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfoRequest);

        /// <summary>The contract for the incoming <c>RoomAdPurchaseInfo</c> message, which carries the rooms that may be advertised.</summary>
        public static readonly MessageContract<RoomAdPurchaseInfo> RoomAdInfo =
            Flash<RoomAdPurchaseInfo>(MessageKeys.Catalog.RoomAdInfo);
    }

    /// <summary>Contains the gift message contracts.</summary>
    public static class Gifts
    {
        /// <summary>The contract for the incoming <c>GiftWrappingConfiguration</c> message, which carries the gift wrapping options.</summary>
        public static readonly MessageContract<GiftWrappingConfiguration> WrappingConfiguration =
            Flash<GiftWrappingConfiguration>(MessageKeys.Gifts.WrappingConfiguration);

        /// <summary>The contract for the incoming <c>PresentOpened</c> message, which carries the contents of an opened present.</summary>
        public static readonly MessageContract<PresentOpened> PresentOpened =
            Flash<PresentOpened>(MessageKeys.Gifts.PresentOpened);

        /// <summary>The contract for the incoming <c>ClubGiftInfo</c> message, which carries the club gifts and how many the local user can select.</summary>
        public static readonly MessageContract<ClubGiftInfo> ClubInfo =
            Flash<ClubGiftInfo>(MessageKeys.Gifts.ClubInfo);

        /// <summary>The contract for the incoming <c>ClubGiftSelected</c> message, which confirms a selected club gift.</summary>
        public static readonly MessageContract<ClubGiftSelected> ClubSelected =
            Flash<ClubGiftSelected>(MessageKeys.Gifts.ClubSelected);

        /// <summary>The contract for the incoming <c>GiftReceiverNotFound</c> message, which reports that the recipient of a gift purchase was not found.</summary>
        public static readonly MessageContract<GiftReceiverNotFound> ReceiverNotFound =
            Flash<GiftReceiverNotFound>(MessageKeys.Gifts.ReceiverNotFound);

        /// <summary>The contract for the incoming <c>ClubGiftNotification</c> message, which reports the number of club gifts available to select.</summary>
        public static readonly MessageContract<ClubGiftNotification> ClubNotification =
            Flash<ClubGiftNotification>(MessageKeys.Gifts.ClubNotification);

        /// <summary>The contract for the incoming <c>IsOfferGiftable</c> message, which reports whether a catalog offer can be bought as a gift.</summary>
        public static readonly MessageContract<IsOfferGiftable> OfferGiftability =
            Flash<IsOfferGiftable>(MessageKeys.Gifts.OfferGiftability);

        /// <summary>The contract for the incoming <c>NewUserExperienceGiftOffer</c> message, which carries the gift choices offered to a new user.</summary>
        public static readonly MessageContract<NuxGiftOffer> NewUserOffer =
            Flash<NuxGiftOffer>(MessageKeys.Gifts.NewUserOffer);

        /// <summary>The contract for the incoming <c>NewUserExperienceNotComplete</c> message, which reports that the new user flow is not complete.</summary>
        public static readonly MessageContract<NuxNotComplete> NewUserIncomplete =
            Flash<NuxNotComplete>(MessageKeys.Gifts.NewUserIncomplete);

        /// <summary>The contract for the outgoing <c>GetGiftWrappingConfiguration</c> message, which requests the gift wrapping options.</summary>
        public static readonly MessageContract<GetGiftWrappingConfiguration>
            WrappingConfigurationRequest =
                Flash<GetGiftWrappingConfiguration>(
                    MessageKeys.Gifts.WrappingConfigurationRequest);

        /// <summary>The contract for the outgoing <c>PresentOpen</c> message, which opens a present in the room.</summary>
        public static readonly MessageContract<PresentOpen> PresentOpen =
            Flash<PresentOpen>(MessageKeys.Gifts.PresentOpen);

        /// <summary>The contract for the outgoing <c>PurchaseFromCatalogAsGift</c> message, which buys a catalog offer as a gift for another user.</summary>
        public static readonly MessageContract<PurchaseFromCatalogAsGift> Purchase =
            Flash<PurchaseFromCatalogAsGift>(MessageKeys.Gifts.Purchase);

        /// <summary>The contract for the outgoing <c>GetClubGift</c> message, which requests the club gift information.</summary>
        public static readonly MessageContract<GetClubGift> ClubInfoRequest =
            Flash<GetClubGift>(MessageKeys.Gifts.ClubInfoRequest);

        /// <summary>The contract for the outgoing <c>SelectClubGift</c> message, which selects a club gift by product code.</summary>
        public static readonly MessageContract<SelectClubGift> ClubSelect =
            Flash<SelectClubGift>(MessageKeys.Gifts.ClubSelect);

        /// <summary>The contract for the outgoing <c>GetIsOfferGiftable</c> message, which asks whether a catalog offer can be bought as a gift.</summary>
        public static readonly MessageContract<GetIsOfferGiftable> OfferGiftabilityRequest =
            Flash<GetIsOfferGiftable>(MessageKeys.Gifts.OfferGiftabilityRequest);

        /// <summary>The contract for the outgoing <c>NewUserExperienceGetGifts</c> message, which selects the new user gifts.</summary>
        public static readonly MessageContract<NuxGetGifts> NewUserSelect =
            Flash<NuxGetGifts>(MessageKeys.Gifts.NewUserSelect);

        /// <summary>The contract for the outgoing <c>NewUserExperienceScriptProceed</c> message, which advances the new user flow.</summary>
        public static readonly MessageContract<AdvanceNewUserFlowRequest> NewUserAdvance =
            Flash<AdvanceNewUserFlowRequest>(MessageKeys.Gifts.NewUserAdvance);
    }

    /// <summary>Contains the group message contracts.</summary>
    public static class Groups
    {
        /// <summary>Contains the group details message contracts.</summary>
        public static class Details
        {
            /// <summary>The contract for the outgoing <c>GetHabboGroupDetails</c> message, which requests the details of a group.</summary>
            public static readonly MessageContract<GroupDetailsRequest> Request =
                Flash<GroupDetailsRequest>(MessageKeys.Groups.Details.Request);

            /// <summary>The contract for the incoming <c>HabboGroupDetails</c> message, which carries the details of a group.</summary>
            public static readonly MessageContract<GroupData> Snapshot =
                Flash<GroupData>(MessageKeys.Groups.Details.Snapshot);
        }

        /// <summary>Contains the message contracts that manage group membership.</summary>
        public static class Membership
        {
            /// <summary>The contract for the outgoing <c>JoinHabboGroup</c> message, which requests membership in a group.</summary>
            public static readonly MessageContract<JoinGroupRequest> Join =
                Flash<JoinGroupRequest>(MessageKeys.Groups.Membership.Join);

            /// <summary>The contract for the outgoing <c>KickMember</c> message, which removes a member from a group.</summary>
            public static readonly MessageContract<KickGroupMemberRequest> Kick =
                Flash<KickGroupMemberRequest>(MessageKeys.Groups.Membership.Kick);

            /// <summary>The contract for the outgoing <c>ApproveMembershipRequest</c> message, which approves a pending group membership request.</summary>
            public static readonly MessageContract<ApproveGroupMemberRequest> Approve =
                Flash<ApproveGroupMemberRequest>(MessageKeys.Groups.Membership.Approve);

            /// <summary>The contract for the outgoing <c>RejectMembershipRequest</c> message, which rejects a pending group membership request.</summary>
            public static readonly MessageContract<RejectGroupMemberRequest> Reject =
                Flash<RejectGroupMemberRequest>(MessageKeys.Groups.Membership.Reject);
        }

        /// <summary>Contains the group member list message contracts.</summary>
        public static class Members
        {
            /// <summary>The contract for the outgoing <c>GetGuildMembers</c> message, which requests a page of a group's members.</summary>
            public static readonly MessageContract<GetGuildMembersRequest> Request =
                Flash<GetGuildMembersRequest>(MessageKeys.Groups.Members.Request);

            /// <summary>The contract for the incoming <c>GuildMembers</c> message, which carries a page of a group's members.</summary>
            public static readonly MessageContract<GuildMembers> Snapshot =
                Flash<GuildMembers>(MessageKeys.Groups.Members.Snapshot);
        }

        /// <summary>Contains the message contracts for the groups the local user belongs to.</summary>
        public static class Memberships
        {
            /// <summary>The contract for the outgoing <c>GetGuildMemberships</c> message, which requests the groups the local user belongs to.</summary>
            public static readonly MessageContract<GuildMembershipsRequest> Request =
                Flash<GuildMembershipsRequest>(MessageKeys.Groups.Memberships.Request);

            /// <summary>The contract for the incoming <c>GuildMemberships</c> message, which carries the groups the local user belongs to.</summary>
            public static readonly MessageContract<GuildMemberships> Snapshot =
                Flash<GuildMemberships>(MessageKeys.Groups.Memberships.Snapshot);
        }
    }

    /// <summary>Contains the navigator message contracts.</summary>
    public static class Navigator
    {
        /// <summary>Contains the navigator state message contracts.</summary>
        public static class State
        {
            /// <summary>The contract for the outgoing <c>NewNavigatorInit</c> message, which initializes the navigator and requests its categories.</summary>
            public static readonly MessageContract<NavigatorMetadataRequest> MetadataRequest =
                Flash<NavigatorMetadataRequest>(MessageKeys.Navigator.State.MetadataRequest);

            /// <summary>The contract for the incoming <c>NavigatorMetaData</c> message, which carries every navigator category the hotel publishes.</summary>
            public static readonly MessageContract<NavigatorMetaData> Metadata =
                Flash<NavigatorMetaData>(MessageKeys.Navigator.State.Metadata);

            /// <summary>The contract for the outgoing <c>GetUserFlatCats</c> message, which requests the room categories.</summary>
            public static readonly MessageContract<FlatCategoriesRequest> FlatCategoriesRequest =
                Flash<FlatCategoriesRequest>(MessageKeys.Navigator.State.FlatCategoriesRequest);

            /// <summary>The contract for the incoming <c>UserFlatCats</c> message, which carries the room categories the hotel publishes.</summary>
            public static readonly MessageContract<UserFlatCats> FlatCategories =
                Flash<UserFlatCats>(MessageKeys.Navigator.State.FlatCategories);

            /// <summary>The contract for the incoming <c>NavigatorLiftedRooms</c> message, which carries the rooms the hotel is promoting.</summary>
            public static readonly MessageContract<NavigatorLiftedRooms> LiftedRooms =
                Flash<NavigatorLiftedRooms>(MessageKeys.Navigator.State.LiftedRooms);

            /// <summary>The contract for the incoming <c>NavigatorSettings</c> message, which carries the local user's home room and the room to enter.</summary>
            public static readonly MessageContract<NavigatorSettings> Settings =
                Flash<NavigatorSettings>(MessageKeys.Navigator.State.Settings);

            /// <summary>The contract for the incoming <c>NewNavigatorPreferences</c> message, which carries how the local user has arranged the navigator window.</summary>
            public static readonly MessageContract<NewNavigatorPreferences> Preferences =
                Flash<NewNavigatorPreferences>(MessageKeys.Navigator.State.Preferences);
        }

        /// <summary>Contains the navigator search message contracts.</summary>
        public static class Search
        {
            /// <summary>The contract for the incoming <c>NavigatorSearchResultBlocks</c> message, which carries the result blocks of a navigator search.</summary>
            public static readonly MessageContract<NavigatorSearchResult> Result =
                Flash<NavigatorSearchResult>(MessageKeys.Navigator.Search.Result);

            /// <summary>The contract for the incoming <c>GuestRoomSearchResult</c> message, which carries the rooms of a legacy room search.</summary>
            /// <remarks>
            /// The rooms are parsed into a single block whose search code is <c>query</c> for search type 8,
            /// or <c>legacy:</c> followed by the search type otherwise. Promoted rooms are read and discarded.
            /// Composing writes search type 8 with the rooms of every block and no promotion.
            /// </remarks>
            public static readonly MessageContract<NavigatorSearchResult> LegacyResult =
                LegacyNavigatorSearchResult();

            /// <summary>The contract for the outgoing <c>NewNavigatorSearch</c> message, which searches a navigator view with a filter.</summary>
            public static readonly MessageContract<NavigatorViewSearchRequest> View =
                Flash<NavigatorViewSearchRequest>(MessageKeys.Navigator.Search.View);

            /// <summary>The contract for the outgoing <c>MyRoomsSearch</c> message, which searches the rooms the local user owns.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRooms);

            /// <summary>The contract for the outgoing <c>MyFavouriteRoomsSearch</c> message, which searches the local user's favorite rooms.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFavouriteRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFavouriteRooms);

            /// <summary>The contract for the outgoing <c>MyRoomRightsSearch</c> message, which searches the rooms where the local user has rights.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomRights =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomRights);

            /// <summary>The contract for the outgoing <c>MyRoomHistorySearch</c> message, which searches the rooms the local user visited recently.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyRoomHistory =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyRoomHistory);

            /// <summary>The contract for the outgoing <c>MyFrequentRoomHistorySearch</c> message, which searches the rooms the local user visits most often.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFrequentRoomHistory =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFrequentRoomHistory);

            /// <summary>The contract for the outgoing <c>MyFriendsRoomsSearch</c> message, which searches the rooms owned by the local user's friends.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyFriendsRooms =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyFriendsRooms);

            /// <summary>The contract for the outgoing <c>RoomsWhereMyFriendsAreSearch</c> message, which searches the rooms where the local user's friends are.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> RoomsWhereFriendsAre =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.RoomsWhereFriendsAre);

            /// <summary>The contract for the outgoing <c>MyGuildBasesSearch</c> message, which searches the base rooms of the groups the local user belongs to.</summary>
            public static readonly MessageContract<NavigatorEmptySearchRequest> MyGuildBases =
                Flash<NavigatorEmptySearchRequest>(MessageKeys.Navigator.Search.MyGuildBases);

            /// <summary>The contract for the outgoing <c>RoomTextSearch</c> message, which searches rooms by text.</summary>
            public static readonly MessageContract<NavigatorTextSearchRequest> Text =
                Flash<NavigatorTextSearchRequest>(MessageKeys.Navigator.Search.Text);

            /// <summary>The contract for the outgoing <c>PopularRoomsSearch</c> message, which searches popular rooms, optionally by tag.</summary>
            public static readonly MessageContract<NavigatorTagSearchRequest> Popular =
                Flash<NavigatorTagSearchRequest>(MessageKeys.Navigator.Search.Popular);

            /// <summary>The contract for the outgoing <c>RoomsWithHighestScoreSearch</c> message, which searches the highest scoring rooms.</summary>
            public static readonly MessageContract<NavigatorAdSearchRequest> HighestScoring =
                Flash<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.HighestScoring);

            /// <summary>The contract for the outgoing <c>GuildBaseSearch</c> message, which searches the base rooms of public groups.</summary>
            public static readonly MessageContract<NavigatorAdSearchRequest> GuildBases =
                Flash<NavigatorAdSearchRequest>(MessageKeys.Navigator.Search.GuildBases);
        }

        /// <summary>Contains the message contracts for saved searches and collapsed navigator categories.</summary>
        public static class Personalization
        {
            /// <summary>The contract for the incoming <c>NavigatorSavedSearches</c> message, which carries the local user's saved searches.</summary>
            public static readonly MessageContract<NavigatorSavedSearches> SavedSearches =
                Flash<NavigatorSavedSearches>(MessageKeys.Navigator.Personalization.SavedSearches);

            /// <summary>The contract for the outgoing <c>NavigatorAddSavedSearch</c> message, which saves a navigator search.</summary>
            public static readonly MessageContract<AddSavedSearchRequest> SavedSearchAdd =
                Flash<AddSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchAdd);

            /// <summary>The contract for the outgoing <c>NavigatorDeleteSavedSearch</c> message, which deletes a saved navigator search.</summary>
            public static readonly MessageContract<DeleteSavedSearchRequest> SavedSearchDelete =
                Flash<DeleteSavedSearchRequest>(MessageKeys.Navigator.Personalization.SavedSearchDelete);

            /// <summary>The contract for the incoming <c>CollapsedCategories</c> message, which carries the navigator categories the local user has collapsed.</summary>
            public static readonly MessageContract<CollapsedCategories> CollapsedCategories =
                Flash<CollapsedCategories>(MessageKeys.Navigator.Personalization.CollapsedCategories);

            /// <summary>The contract for the outgoing <c>NavigatorAddCollapsedCategory</c> message, which collapses a navigator category.</summary>
            public static readonly MessageContract<AddCollapsedCategoryRequest> CollapsedCategoryAdd =
                Flash<AddCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryAdd);

            /// <summary>The contract for the outgoing <c>NavigatorRemoveCollapsedCategory</c> message, which expands a collapsed navigator category.</summary>
            public static readonly MessageContract<RemoveCollapsedCategoryRequest> CollapsedCategoryRemove =
                Flash<RemoveCollapsedCategoryRequest>(MessageKeys.Navigator.Personalization.CollapsedCategoryRemove);
        }

        /// <summary>The contract for the outgoing <c>UpdateHomeRoom</c> message, which sets the local user's home room.</summary>
        public static readonly MessageContract<SetHomeRoomRequest> HomeRoomUpdate =
            Flash<SetHomeRoomRequest>(MessageKeys.Navigator.HomeRoomUpdate);

        /// <summary>The contract for the outgoing <c>CreateFlat</c> message, which creates a room owned by the local user.</summary>
        public static readonly MessageContract<CreateRoomRequest> RoomCreate =
            Flash<CreateRoomRequest>(MessageKeys.Navigator.RoomCreate);

        /// <summary>The contract for the outgoing <c>DeleteRoom</c> message, which deletes a room.</summary>
        public static readonly MessageContract<DeleteRoomRequest> RoomDelete =
            Flash<DeleteRoomRequest>(MessageKeys.Navigator.RoomDelete);
    }

    /// <summary>Contains the inventory message contracts.</summary>
    public static class Inventory
    {
        /// <summary>Contains the avatar effect inventory message contracts.</summary>
        public static class AvatarEffects
        {
            /// <summary>The contract for the outgoing <c>AvatarEffectActivated</c> message, which activates an owned avatar effect.</summary>
            public static readonly MessageContract<AvatarEffectActivationRequest> ActivationRequest =
                Flash<AvatarEffectActivationRequest>(MessageKeys.Inventory.AvatarEffects.ActivationRequest);
        }

        /// <summary>Contains the furni inventory message contracts.</summary>
        public static class Furni
        {
            /// <summary>The contract for the outgoing <c>RequestFurniInventory</c> message, which requests the local user's furni inventory.</summary>
            public static readonly MessageContract<FurniInventoryRequest> Request =
                Flash<FurniInventoryRequest>(MessageKeys.Inventory.Furni.Request);

            /// <summary>The contract for the incoming <c>FurniList</c> message, which carries one fragment of the local user's furni inventory.</summary>
            public static readonly MessageContract<FurniList> Snapshot =
                Flash<FurniList>(MessageKeys.Inventory.Furni.Snapshot);

            /// <summary>The contract for the incoming <c>FurniListAddOrUpdate</c> message, which carries items added to or updated in the furni inventory.</summary>
            public static readonly MessageContract<FurniListAddOrUpdate> AddedOrUpdated =
                Flash<FurniListAddOrUpdate>(MessageKeys.Inventory.Furni.AddedOrUpdated);

            /// <summary>The contract for the incoming <c>FurniListRemove</c> message, which announces an item removed from the furni inventory.</summary>
            public static readonly MessageContract<FurniListRemove> Removed =
                Flash<FurniListRemove>(MessageKeys.Inventory.Furni.Removed);

            /// <summary>The contract for the incoming <c>FurniListRemoveMultiple</c> message, which announces several items removed from the furni inventory.</summary>
            public static readonly MessageContract<FurniListRemoveMultiple> RemovedMultiple =
                Flash<FurniListRemoveMultiple>(MessageKeys.Inventory.Furni.RemovedMultiple);

            /// <summary>The contract for the incoming <c>FurniListInvalidate</c> message, which tells the client that the furni inventory is out of date.</summary>
            public static readonly MessageContract<FurniListInvalidate> Invalidated =
                Flash<FurniListInvalidate>(MessageKeys.Inventory.Furni.Invalidated);

            /// <summary>The contract for the incoming <c>PostItPlaced</c> message, which reports a placed post-it note and how many are left in its stack.</summary>
            public static readonly MessageContract<PostItPlaced> PostItPlaced =
                Flash<PostItPlaced>(MessageKeys.Inventory.Furni.PostItPlaced);
        }

        /// <summary>Contains the pet inventory message contracts.</summary>
        public static class Pets
        {
            /// <summary>The contract for the outgoing <c>GetPetInventory</c> message, which requests the local user's pet inventory.</summary>
            public static readonly MessageContract<PetInventoryRequest> Request =
                Flash<PetInventoryRequest>(MessageKeys.Inventory.Pets.Request);

            /// <summary>The contract for the incoming <c>PetInventory</c> message, which carries one fragment of the local user's pet inventory.</summary>
            public static readonly MessageContract<PetInventory> Snapshot =
                Flash<PetInventory>(MessageKeys.Inventory.Pets.Snapshot);

            /// <summary>The contract for the incoming <c>PetAddedToInventory</c> message, which announces a pet added to the pet inventory.</summary>
            public static readonly MessageContract<PetAddedToInventory> Added =
                Flash<PetAddedToInventory>(MessageKeys.Inventory.Pets.Added);

            /// <summary>The contract for the incoming <c>PetRemovedFromInventory</c> message, which announces a pet removed from the pet inventory.</summary>
            public static readonly MessageContract<PetRemovedFromInventory> Removed =
                Flash<PetRemovedFromInventory>(MessageKeys.Inventory.Pets.Removed);
        }
    }

    /// <summary>Contains the wardrobe and figure message contracts.</summary>
    public static class Wardrobe
    {
        /// <summary>The contract for the outgoing <c>GetWardrobe</c> message, which requests the local user's saved wardrobe outfits.</summary>
        public static readonly MessageContract<WardrobeRequest> Request =
            Flash<WardrobeRequest>(MessageKeys.Wardrobe.Request);

        /// <summary>The contract for the incoming <c>Wardrobe</c> message, which carries the local user's saved wardrobe outfits.</summary>
        public static readonly MessageContract<Qx.Model.Messages.Incoming.Wardrobe> Snapshot =
            Flash<Qx.Model.Messages.Incoming.Wardrobe>(MessageKeys.Wardrobe.Snapshot);

        /// <summary>The contract for the outgoing <c>UpdateFigureData</c> message, which changes the local user's figure and gender.</summary>
        public static readonly MessageContract<FigureUpdateRequest> FigureUpdate =
            Flash<FigureUpdateRequest>(MessageKeys.Wardrobe.FigureUpdate);

        /// <summary>The contract for the outgoing <c>SaveWardrobeOutfit</c> message, which saves a figure and gender into a wardrobe slot.</summary>
        public static readonly MessageContract<SaveWardrobeOutfitRequest> OutfitSave =
            Flash<SaveWardrobeOutfitRequest>(MessageKeys.Wardrobe.OutfitSave);
    }

    /// <summary>Contains the marketplace message contracts.</summary>
    public static class Marketplace
    {
        /// <summary>Contains the marketplace configuration message contracts.</summary>
        public static class Configuration
        {
            /// <summary>The contract for the outgoing <c>GetMarketplaceConfiguration</c> message, which requests the marketplace configuration.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<GetMarketplaceConfiguration> Request =
                ModernFlashMarketplace<GetMarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Request);

            /// <summary>The contract for the incoming <c>MarketplaceConfiguration</c> message, which carries the marketplace configuration, such as availability and commission.</summary>
            public static readonly MessageContract<MarketplaceConfiguration> Snapshot =
                Flash<MarketplaceConfiguration>(MessageKeys.Marketplace.Configuration.Snapshot);
        }

        /// <summary>Contains the message contracts that check whether the local user may create a marketplace offer.</summary>
        public static class Eligibility
        {
            /// <summary>The contract for the outgoing <c>GetMarketplaceCanMakeOffer</c> message, which asks whether the local user may create a marketplace offer.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<GetMarketplaceCanMakeOffer> Request =
                ModernFlashMarketplace<GetMarketplaceCanMakeOffer>(MessageKeys.Marketplace.Eligibility.Request);

            /// <summary>The contract for the incoming <c>MarketplaceCanMakeOfferResult</c> message, which reports whether the local user may create a marketplace offer.</summary>
            public static readonly MessageContract<MarketplaceCanMakeOfferResult> Result =
                Flash<MarketplaceCanMakeOfferResult>(MessageKeys.Marketplace.Eligibility.Result);
        }

        /// <summary>Contains the marketplace credit message contracts.</summary>
        public static class Credits
        {
            /// <summary>The contract for the outgoing <c>RedeemMarketplaceOfferCredits</c> message, which collects the credits from sold marketplace offers.</summary>
            public static readonly MessageContract<RedeemMarketplaceOfferCredits> Redeem =
                Flash<RedeemMarketplaceOfferCredits>(MessageKeys.Marketplace.Credits.Redeem);
        }

        /// <summary>Contains the marketplace token message contracts.</summary>
        public static class Tokens
        {
            /// <summary>The contract for the outgoing <c>BuyMarketplaceTokens</c> message, which buys a batch of marketplace listing tokens.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<BuyMarketplaceTokens> Buy =
                ModernFlashMarketplace<BuyMarketplaceTokens>(MessageKeys.Marketplace.Tokens.Buy);
        }

        /// <summary>Contains the marketplace offer message contracts.</summary>
        public static class Offers
        {
            /// <summary>The contract for the outgoing <c>GetMarketplaceOffers</c> message, which searches public marketplace offers.</summary>
            /// <remarks>The <c>flashMarketplaceLayout</c> capability is available only when the Flash wire profile has been analyzed and the active build has a known marketplace wire layout.</remarks>
            public static readonly MessageContract<SearchMarketplaceOffers> SearchRequest =
                MarketplaceLayout<SearchMarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchRequest);

            /// <summary>The contract for the incoming <c>MarketPlaceOffers</c> message, which carries the marketplace offers that match a search.</summary>
            public static readonly MessageContract<MarketplaceOffers> SearchResult =
                Flash<MarketplaceOffers>(MessageKeys.Marketplace.Offers.SearchResult);

            /// <summary>The contract for the outgoing <c>GetMarketplaceOwnOffers</c> message, which requests the local user's own marketplace offers.</summary>
            /// <remarks>The <c>flashMarketplaceLayout</c> capability is available only when the Flash wire profile has been analyzed and the active build has a known marketplace wire layout.</remarks>
            public static readonly MessageContract<GetMarketplaceOwnOffers> OwnRequest =
                MarketplaceLayout<GetMarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnRequest);

            /// <summary>The contract for the incoming <c>MarketPlaceOwnOffers</c> message, which carries the local user's own marketplace offers.</summary>
            public static readonly MessageContract<MarketplaceOwnOffers> OwnSnapshot =
                Flash<MarketplaceOwnOffers>(MessageKeys.Marketplace.Offers.OwnSnapshot);

            /// <summary>The contract for the outgoing <c>MakeOffer</c> message, which lists inventory items for sale on the marketplace.</summary>
            /// <remarks>The <c>flashMarketplaceLayout</c> capability is available only when the Flash wire profile has been analyzed and the active build has a known marketplace wire layout.</remarks>
            public static readonly MessageContract<MakeMarketplaceOffer> Make =
                MarketplaceLayout<MakeMarketplaceOffer>(MessageKeys.Marketplace.Offers.Make);

            /// <summary>The contract for the incoming <c>MarketplaceMakeOfferResult</c> message, which carries the result of a marketplace listing.</summary>
            public static readonly MessageContract<MarketplaceMakeOfferResult> MakeResult =
                Flash<MarketplaceMakeOfferResult>(MessageKeys.Marketplace.Offers.MakeResult);

            /// <summary>The contract for the outgoing <c>BuyMarketplaceOffer</c> message, which buys a marketplace offer by id.</summary>
            public static readonly MessageContract<MarketplaceBuyOfferRequest> Buy =
                new(
                    MessageKeys.Marketplace.Offers.Buy,
                    MessageCodec<MarketplaceBuyOfferRequest>.FromModel());

            /// <summary>The contract for the incoming <c>MarketplaceBuyOfferResult</c> message, which carries the result of a marketplace purchase.</summary>
            public static readonly MessageContract<MarketplaceBuyResult> BuyResult =
                Flash<MarketplaceBuyResult>(MessageKeys.Marketplace.Offers.BuyResult);

            /// <summary>The contract for the outgoing <c>CancelMarketplaceOffer</c> message, which cancels one of the local user's marketplace offers.</summary>
            public static readonly MessageContract<CancelMarketplaceOffer> Cancel =
                Flash<CancelMarketplaceOffer>(MessageKeys.Marketplace.Offers.Cancel);

            /// <summary>The contract for the incoming <c>MarketplaceCancelOfferResult</c> message, which reports whether a marketplace offer was canceled.</summary>
            public static readonly MessageContract<MarketplaceCancelOfferResult> CancelResult =
                Flash<MarketplaceCancelOfferResult>(MessageKeys.Marketplace.Offers.CancelResult);

            /// <summary>The contract for the outgoing <c>CancelAllMarketplaceOffers</c> message, which cancels every open marketplace offer of the local user.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<CancelAllMarketplaceOffers> CancelAll =
                ModernFlashMarketplace<CancelAllMarketplaceOffers>(MessageKeys.Marketplace.Offers.CancelAll);

            /// <summary>The contract for the incoming <c>MarketplaceCancelAllOffersResult</c> message, which carries the result of canceling every open marketplace offer.</summary>
            public static readonly MessageContract<MarketplaceCancelAllOffersResult> CancelAllResult =
                Flash<MarketplaceCancelAllOffersResult>(MessageKeys.Marketplace.Offers.CancelAllResult);

            /// <summary>The contract for the outgoing <c>ClearMarketplaceOwnHistory</c> message, which clears sold or expired offers from the local user's marketplace history.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<ClearMarketplaceOwnHistory> ClearOwnHistory =
                ModernFlashMarketplace<ClearMarketplaceOwnHistory>(MessageKeys.Marketplace.Offers.ClearOwnHistory);

            /// <summary>The contract for the incoming <c>MarketplaceClearOwnHistoryResult</c> message, which reports whether the marketplace history was cleared.</summary>
            /// <remarks>The contract reports <c>flashMarketplaceLayout</c> as missing while the Flash marketplace wire layout is unknown, and <c>flashMarketplaceModernLayout</c> as missing when the active build uses the legacy layout.</remarks>
            public static readonly MessageContract<MarketplaceClearOwnHistoryResult> ClearOwnHistoryResult =
                ModernFlashMarketplace<MarketplaceClearOwnHistoryResult>(MessageKeys.Marketplace.Offers.ClearOwnHistoryResult);
        }

        /// <summary>Contains the marketplace item statistics message contracts.</summary>
        public static class ItemStats
        {
            /// <summary>The contract for the outgoing <c>GetMarketplaceItemStats</c> message, which requests the sale statistics of one furni type.</summary>
            /// <remarks>The <c>flashMarketplaceLayout</c> capability is available only when the Flash wire profile has been analyzed and the active build has a known marketplace wire layout.</remarks>
            public static readonly MessageContract<GetMarketplaceItemStats> Request =
                MarketplaceLayout<GetMarketplaceItemStats>(MessageKeys.Marketplace.ItemStats.Request);

            /// <summary>The contract for the incoming <c>MarketplaceItemStats</c> message, which carries the sale statistics of one furni type.</summary>
            public static readonly MessageContract<MarketplaceItemStats> Snapshot =
                Flash<MarketplaceItemStats>(MessageKeys.Marketplace.ItemStats.Snapshot);
        }
    }

    /// <summary>Contains the club and Builders Club subscription message contracts.</summary>
    public static class Subscriptions
    {
        /// <summary>The contract for the incoming <c>ScrSendUserInfo</c> message, which carries the local user's subscription status for a product.</summary>
        public static readonly MessageContract<ScrSendUserInfo> UserInfo =
            Flash<ScrSendUserInfo>(MessageKeys.Subscriptions.UserInfo);

        /// <summary>The contract for the outgoing <c>ScrGetUserInfo</c> message, which requests the local user's subscription status for a product.</summary>
        public static readonly MessageContract<SubscriptionGetUserInfo> UserInfoRequest =
            Flash<SubscriptionGetUserInfo>(MessageKeys.Subscriptions.UserInfoRequest);

        /// <summary>The contract for the incoming <c>ScrSendKickbackInfo</c> message, which carries the local user's subscription kickback summary.</summary>
        public static readonly MessageContract<ScrSendKickbackInfo> KickbackInfo =
            Flash<ScrSendKickbackInfo>(MessageKeys.Subscriptions.KickbackInfo);

        /// <summary>The contract for the outgoing <c>ScrGetKickbackInfo</c> message, which requests the local user's subscription kickback summary.</summary>
        public static readonly MessageContract<SubscriptionGetKickbackInfo> KickbackInfoRequest =
            Flash<SubscriptionGetKickbackInfo>(MessageKeys.Subscriptions.KickbackInfoRequest);

        /// <summary>The contract for the incoming <c>HabboClubOffers</c> message, which carries the club subscription offers.</summary>
        public static readonly MessageContract<HabboClubOffers> ClubOffersSnapshot =
            Flash<HabboClubOffers>(MessageKeys.Subscriptions.ClubOffersSnapshot);

        /// <summary>The contract for the outgoing <c>GetClubOffers</c> message, which requests the club subscription offers of an offer type.</summary>
        public static readonly MessageContract<GetClubOffers> ClubOffersRequest =
            Flash<GetClubOffers>(MessageKeys.Subscriptions.ClubOffersRequest);

        /// <summary>The contract for the incoming <c>BuildersClubFurniCount</c> message, which carries the local user's Builders Club furni count.</summary>
        public static readonly MessageContract<BuildersClubFurniCount> BuildersClubFurniCount =
            Flash<BuildersClubFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCount);

        /// <summary>The contract for the outgoing <c>BuildersClubQueryFurniCount</c> message, which requests the local user's Builders Club furni count.</summary>
        public static readonly MessageContract<BuildersClubQueryFurniCount> BuildersClubFurniCountRequest =
            Flash<BuildersClubQueryFurniCount>(MessageKeys.Subscriptions.BuildersClubFurniCountRequest);

        /// <summary>The contract for the incoming <c>BuildersClubSubscriptionStatus</c> message, which carries the local user's Builders Club membership time and furni limit.</summary>
        public static readonly MessageContract<BuildersClubMembershipStatus> BuildersClubMembershipStatus =
            Flash<BuildersClubMembershipStatus>(MessageKeys.Subscriptions.BuildersClubMembershipStatus);

        /// <summary>The contract for the incoming <c>BuildersClubPlacementWarning</c> message, which warns about placing a Builders Club offer in the room.</summary>
        public static readonly MessageContract<BuildersClubPlacementWarning> BuildersClubPlacementWarning =
            Flash<BuildersClubPlacementWarning>(MessageKeys.Subscriptions.BuildersClubPlacementWarning);

        /// <summary>The contract for the outgoing <c>BuildersClubPlaceRoomItem</c> message, which places a Builders Club floor offer in the current room.</summary>
        public static readonly MessageContract<BuildersClubPlaceRoomItem> BuildersClubFloorOfferPlace =
            Flash<BuildersClubPlaceRoomItem>(MessageKeys.Subscriptions.BuildersClubFloorOfferPlace);

        /// <summary>The contract for the outgoing <c>BuildersClubPlaceWallItem</c> message, which places a Builders Club wall offer in the current room.</summary>
        public static readonly MessageContract<BuildersClubPlaceWallItem> BuildersClubWallOfferPlace =
            Flash<BuildersClubPlaceWallItem>(
                MessageKeys.Subscriptions.BuildersClubWallOfferPlace);
    }

    /// <summary>Contains the crafting message contracts.</summary>
    public static class Crafting
    {
        /// <summary>The contract for the outgoing <c>GetCraftableProducts</c> message, which requests the products a crafting furni can make.</summary>
        public static readonly MessageContract<GetCraftableProducts> ProductsRequest =
            Flash<GetCraftableProducts>(MessageKeys.Crafting.ProductsRequest);

        /// <summary>The contract for the incoming <c>CraftableProducts</c> message, which carries the products a crafting furni can make and the furni classes it accepts.</summary>
        public static readonly MessageContract<CraftableProducts> ProductsSnapshot =
            Flash<CraftableProducts>(MessageKeys.Crafting.ProductsSnapshot);

        /// <summary>The contract for the outgoing <c>GetCraftingRecipe</c> message, which requests the ingredients of a crafting recipe.</summary>
        public static readonly MessageContract<GetCraftingRecipe> RecipeRequest =
            Flash<GetCraftingRecipe>(MessageKeys.Crafting.RecipeRequest);

        /// <summary>The contract for the incoming <c>CraftingRecipe</c> message, which carries the ingredients of a crafting recipe.</summary>
        public static readonly MessageContract<CraftingRecipe> RecipeSnapshot =
            Flash<CraftingRecipe>(MessageKeys.Crafting.RecipeSnapshot);

        /// <summary>The contract for the outgoing <c>Craft</c> message, which crafts a known recipe on a crafting furni.</summary>
        public static readonly MessageContract<Craft> Craft =
            Flash<Craft>(MessageKeys.Crafting.Craft);

        /// <summary>The contract for the outgoing <c>CraftSecret</c> message, which crafts a secret recipe from the chosen ingredient items.</summary>
        public static readonly MessageContract<CraftSecret> SecretCraft =
            Flash<CraftSecret>(MessageKeys.Crafting.SecretCraft);

        /// <summary>The contract for the outgoing <c>GetCraftingRecipesAvailable</c> message, which asks how many recipes the chosen ingredient items match on a crafting furni.</summary>
        public static readonly MessageContract<GetCraftingRecipesAvailable> AvailabilityRequest =
            Flash<GetCraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilityRequest);

        /// <summary>The contract for the incoming <c>CraftingRecipesAvailable</c> message, which carries how many recipes match the chosen ingredients and whether a recipe is complete.</summary>
        public static readonly MessageContract<CraftingRecipesAvailable> AvailabilitySnapshot =
            Flash<CraftingRecipesAvailable>(MessageKeys.Crafting.AvailabilitySnapshot);

        /// <summary>The contract for the incoming <c>CraftingResult</c> message, which carries the outcome of a craft and the product made.</summary>
        public static readonly MessageContract<CraftingResult> Result =
            Flash<CraftingResult>(MessageKeys.Crafting.Result);
    }

    /// <summary>Contains the recycler message contracts.</summary>
    public static class Recycler
    {
        /// <summary>The contract for the incoming <c>RecyclerStatus</c> message, which carries whether a recycler session is running and the seconds it has left.</summary>
        public static readonly MessageContract<RecyclerStatus> Status =
            Flash<RecyclerStatus>(MessageKeys.Recycler.Status);

        /// <summary>The contract for the incoming <c>RecyclerFinished</c> message, which reports that a recycler session ended and what it produced.</summary>
        public static readonly MessageContract<RecyclerFinished> Finished =
            Flash<RecyclerFinished>(MessageKeys.Recycler.Finished);
    }

    /// <summary>Contains the wired message contracts.</summary>
    public static class Wired
    {
        /// <summary>Contains Wired Account messages.</summary>
        public static class Account
        {
            /// <summary>The AccountPreferences message contract.</summary>
            public static readonly MessageContract<AccountPreferences> Preferences =
                Flash<AccountPreferences>(MessageKeys.Wired.Account.Preferences);
        }

        /// <summary>Contains Wired WebApi messages.</summary>
        public static class WebApi
        {
            /// <summary>The WiredGenerateWebApiKey message contract.</summary>
            public static readonly MessageContract<WiredGenerateWebApiKey> KeyGenerate =
                Flash<WiredGenerateWebApiKey>(MessageKeys.Wired.WebApi.KeyGenerate);
            /// <summary>The WiredWebApiKeyResult message contract.</summary>
            public static readonly MessageContract<WiredWebApiKeyResult> KeyResult =
                Flash<WiredWebApiKeyResult>(MessageKeys.Wired.WebApi.KeyResult);
        }

        /// <summary>Contains the wired state message contracts.</summary>
        public static class State
        {
            /// <summary>The contract for the incoming <c>WiredPermissions</c> message, which carries whether the local user can read and modify the room's wired.</summary>
            public static readonly MessageContract<WiredPermissions> Permissions =
                Flash<WiredPermissions>(MessageKeys.Wired.State.Permissions);

            /// <summary>The contract for the incoming <c>WiredEnvironment</c> message, which carries whether the room has user click wired and the enabled wired achievements.</summary>
            public static readonly MessageContract<WiredEnvironment> Environment =
                Flash<WiredEnvironment>(MessageKeys.Wired.State.Environment);

            /// <summary>The contract for the incoming <c>WiredClickSettings</c> message, which carries the room's wired user and furni click options.</summary>
            public static readonly MessageContract<WiredClickSettings> ClickSettings =
                Flash<WiredClickSettings>(MessageKeys.Wired.State.ClickSettings);

            /// <summary>The contract for the incoming <c>WiredMenuError</c> message, which reports a wired menu error code.</summary>
            public static readonly MessageContract<WiredMenuError> MenuError =
                Flash<WiredMenuError>(MessageKeys.Wired.State.MenuError);

            /// <summary>The contract for the incoming <c>WiredRewardResult</c> message, which reports the result of a wired reward.</summary>
            public static readonly MessageContract<WiredRewardResult> RewardResult =
                Flash<WiredRewardResult>(MessageKeys.Wired.State.RewardResult);
        }

        /// <summary>Contains the wired variable Fx bar message contracts.</summary>
        public static class VariableFx
        {
            /// <summary>The contract for the incoming <c>VariableFxConfigs</c> message, which carries Fx bar configurations the room adds or replaces.</summary>
            public static readonly MessageContract<VariableFxConfigUpdate> Configs =
                Flash<VariableFxConfigUpdate>(MessageKeys.Wired.VariableFx.Configs);

            /// <summary>The contract for the incoming <c>VariableFxConfigsRemoved</c> message, which carries the ids of Fx bar configurations the room removes.</summary>
            public static readonly MessageContract<VariableFxConfigRemoval> ConfigsRemoved =
                Flash<VariableFxConfigRemoval>(MessageKeys.Wired.VariableFx.ConfigsRemoved);

            /// <summary>The contract for the incoming <c>VariableFxStatus</c> message, which carries Fx bar values that changed.</summary>
            public static readonly MessageContract<VariableFxStatusUpdate> Statuses =
                Flash<VariableFxStatusUpdate>(MessageKeys.Wired.VariableFx.Statuses);

            /// <summary>The contract for the incoming <c>VariableFxStatusRemoved</c> message, which carries the keys of Fx bar values that were removed.</summary>
            public static readonly MessageContract<VariableFxStatusRemoval> StatusesRemoved =
                Flash<VariableFxStatusRemoval>(MessageKeys.Wired.VariableFx.StatusesRemoved);
        }

        /// <summary>Contains the wired furni configuration message contracts.</summary>
        public static class Configuration
        {
            /// <summary>The contract for the incoming <c>Open</c> message, which names the wired furni whose configuration the client opens.</summary>
            public static readonly MessageContract<WiredOpen> Opened =
                Flash<WiredOpen>(MessageKeys.Wired.Configuration.Opened);

            /// <summary>The contract for the outgoing <c>Open</c> message, which requests the configuration of a wired furni.</summary>
            public static readonly MessageContract<WiredOpen> OpenRequest =
                Flash<WiredOpen>(MessageKeys.Wired.Configuration.OpenRequest);

            /// <summary>The contract for the outgoing <c>ApplySnapshot</c> message, which stores the current state of a wired furni as its restore snapshot.</summary>
            public static readonly MessageContract<WiredApplySnapshot> ApplySnapshot =
                Flash<WiredApplySnapshot>(MessageKeys.Wired.Configuration.ApplySnapshot);

            /// <summary>The contract for the incoming <c>WiredFurniTrigger</c> message, which carries the configuration of a wired trigger.</summary>
            public static readonly MessageContract<WiredFurniTrigger> Trigger =
                Flash<WiredFurniTrigger>(MessageKeys.Wired.Configuration.Trigger);

            /// <summary>The contract for the incoming <c>WiredFurniAction</c> message, which carries the configuration of a wired action.</summary>
            public static readonly MessageContract<WiredFurniAction> Action =
                Flash<WiredFurniAction>(MessageKeys.Wired.Configuration.Action);

            /// <summary>The contract for the incoming <c>WiredFurniCondition</c> message, which carries the configuration of a wired condition.</summary>
            public static readonly MessageContract<WiredFurniCondition> Condition =
                Flash<WiredFurniCondition>(MessageKeys.Wired.Configuration.Condition);

            /// <summary>The contract for the incoming <c>WiredFurniSelector</c> message, which carries the configuration of a wired selector.</summary>
            public static readonly MessageContract<WiredFurniSelector> Selector =
                Flash<WiredFurniSelector>(MessageKeys.Wired.Configuration.Selector);

            /// <summary>The contract for the incoming <c>WiredFurniAddon</c> message, which carries the configuration of a wired add-on.</summary>
            public static readonly MessageContract<WiredFurniAddon> Addon =
                Flash<WiredFurniAddon>(MessageKeys.Wired.Configuration.Addon);

            /// <summary>The contract for the incoming <c>WiredFurniVariable</c> message, which carries the configuration of a wired variable.</summary>
            public static readonly MessageContract<WiredFurniVariable> Variable =
                Flash<WiredFurniVariable>(MessageKeys.Wired.Configuration.Variable);

            /// <summary>The contract for the outgoing <c>UpdateTrigger</c> message, which saves the configuration of a wired trigger.</summary>
            public static readonly MessageContract<UpdateTrigger> TriggerUpdate =
                Flash<UpdateTrigger>(MessageKeys.Wired.Configuration.TriggerUpdate);

            /// <summary>The contract for the outgoing <c>UpdateAction</c> message, which saves the configuration of a wired action.</summary>
            public static readonly MessageContract<UpdateAction> ActionUpdate =
                Flash<UpdateAction>(MessageKeys.Wired.Configuration.ActionUpdate);

            /// <summary>The contract for the outgoing <c>UpdateCondition</c> message, which saves the configuration of a wired condition.</summary>
            public static readonly MessageContract<UpdateCondition> ConditionUpdate =
                Flash<UpdateCondition>(MessageKeys.Wired.Configuration.ConditionUpdate);

            /// <summary>The contract for the outgoing <c>UpdateSelector</c> message, which saves the configuration of a wired selector.</summary>
            public static readonly MessageContract<UpdateSelector> SelectorUpdate =
                Flash<UpdateSelector>(MessageKeys.Wired.Configuration.SelectorUpdate);

            /// <summary>The contract for the outgoing <c>UpdateAddon</c> message, which saves the configuration of a wired add-on.</summary>
            public static readonly MessageContract<UpdateAddon> AddonUpdate =
                Flash<UpdateAddon>(MessageKeys.Wired.Configuration.AddonUpdate);

            /// <summary>The contract for the outgoing <c>UpdateVariable</c> message, which saves the configuration of a wired variable.</summary>
            public static readonly MessageContract<UpdateVariable> VariableUpdate =
                Flash<UpdateVariable>(MessageKeys.Wired.Configuration.VariableUpdate);

            /// <summary>The contract for the incoming <c>WiredSaveSuccess</c> message, which reports that a wired configuration was saved.</summary>
            public static readonly MessageContract<WiredSaveSuccess> SaveSucceeded =
                Flash<WiredSaveSuccess>(MessageKeys.Wired.Configuration.SaveSucceeded);

            /// <summary>The contract for the incoming <c>WiredValidationError</c> message, which reports why a wired configuration failed validation, as a localization key and parameters.</summary>
            public static readonly MessageContract<WiredValidationError> ValidationFailed =
                Flash<WiredValidationError>(MessageKeys.Wired.Configuration.ValidationFailed);
        }

        /// <summary>Contains the message contracts for the room's wired settings, statistics and logs.</summary>
        public static class Room
        {
            /// <summary>The contract for the outgoing <c>WiredGetRoomSettings</c> message, which requests the room's wired permission masks and timezone.</summary>
            public static readonly MessageContract<WiredGetRoomSettings> SettingsRequest =
                Flash<WiredGetRoomSettings>(MessageKeys.Wired.Room.SettingsRequest);

            /// <summary>The contract for the incoming <c>WiredRoomSettings</c> message, which carries the room's wired permission masks and timezone.</summary>
            public static readonly MessageContract<WiredRoomSettings> Settings =
                Flash<WiredRoomSettings>(MessageKeys.Wired.Room.Settings);

            /// <summary>The contract for the outgoing <c>WiredSetRoomSettings</c> message, which replaces the room's wired permission masks and timezone.</summary>
            public static readonly MessageContract<WiredSetRoomSettings> SettingsUpdate =
                Flash<WiredSetRoomSettings>(MessageKeys.Wired.Room.SettingsUpdate);

            /// <summary>The contract for the outgoing <c>WiredGetRoomStats</c> message, which requests the room's wired statistics.</summary>
            public static readonly MessageContract<WiredGetRoomStats> StatsRequest =
                Flash<WiredGetRoomStats>(MessageKeys.Wired.Room.StatsRequest);

            /// <summary>The contract for the incoming <c>WiredRoomStats</c> message, which carries the room's wired execution cost, furni limits and permanent variable usage.</summary>
            public static readonly MessageContract<WiredRoomStats> Stats =
                Flash<WiredRoomStats>(MessageKeys.Wired.Room.Stats);

            /// <summary>The contract for the outgoing <c>WiredGetRoomLogs</c> message, which requests a page of the room's wired logs.</summary>
            public static readonly MessageContract<WiredGetRoomLogs> LogsRequest =
                Flash<WiredGetRoomLogs>(MessageKeys.Wired.Room.LogsRequest);

            /// <summary>The contract for the incoming <c>WiredRoomLogs</c> message, which carries a page of the room's wired logs.</summary>
            public static readonly MessageContract<WiredRoomLogs> Logs =
                Flash<WiredRoomLogs>(MessageKeys.Wired.Room.Logs);

            /// <summary>The contract for the outgoing <c>WiredUpdateRoom</c> message, which reloads the room's state or rolls the room back to its last saved state.</summary>
            public static readonly MessageContract<WiredUpdateRoom> Update =
                Flash<WiredUpdateRoom>(MessageKeys.Wired.Room.Update);

            /// <summary>The contract for the outgoing <c>WiredSetPreferences</c> message, which updates the local user's wired menu preferences.</summary>
            public static readonly MessageContract<WiredSetPreferences> PreferencesUpdate =
                Flash<WiredSetPreferences>(MessageKeys.Wired.Room.PreferencesUpdate);
        }

        /// <summary>Contains the wired error log message contracts.</summary>
        public static class ErrorLogs
        {
            /// <summary>The contract for the outgoing <c>WiredGetErrorLogs</c> message, which requests the room's wired error logs.</summary>
            public static readonly MessageContract<WiredGetErrorLogs> Request =
                Flash<WiredGetErrorLogs>(MessageKeys.Wired.ErrorLogs.Request);

            /// <summary>The contract for the incoming <c>WiredErrorLogs</c> message, which carries the room's wired execution errors.</summary>
            public static readonly MessageContract<WiredErrorLogs> Snapshot =
                Flash<WiredErrorLogs>(MessageKeys.Wired.ErrorLogs.Snapshot);

            /// <summary>The contract for the outgoing <c>WiredClearErrorLogs</c> message, which clears the room's wired error logs.</summary>
            public static readonly MessageContract<WiredClearErrorLogs> Clear =
                Flash<WiredClearErrorLogs>(MessageKeys.Wired.ErrorLogs.Clear);
        }

        /// <summary>Contains the wired user click message contracts.</summary>
        public static class UserClick
        {
            /// <summary>The contract for the outgoing <c>WiredClickUser</c> message, which asks whether the wired user menu should open for an avatar in the room.</summary>
            public static readonly MessageContract<WiredClickUser> Request =
                Flash<WiredClickUser>(MessageKeys.Wired.UserClick.Request);

            /// <summary>The contract for the incoming <c>WiredClickUserResponse</c> message, which answers whether the wired user menu should open for an avatar in the room.</summary>
            public static readonly MessageContract<WiredClickUserResponse> Result =
                Flash<WiredClickUserResponse>(MessageKeys.Wired.UserClick.Result);
        }

        /// <summary>Contains the wired variable message contracts.</summary>
        public static class Variables
        {
            /// <summary>The contract for the outgoing <c>WiredGetAllVariablesHash</c> message, which requests the room-wide hash of the wired variable definitions.</summary>
            public static readonly MessageContract<WiredGetAllVariablesHash> HashRequest =
                Flash<WiredGetAllVariablesHash>(MessageKeys.Wired.Variables.HashRequest);

            /// <summary>The contract for the incoming <c>WiredAllVariablesHash</c> message, which carries the room-wide hash of the wired variable definitions.</summary>
            public static readonly MessageContract<WiredAllVariablesHash> Hash =
                Flash<WiredAllVariablesHash>(MessageKeys.Wired.Variables.Hash);

            /// <summary>The contract for the outgoing <c>WiredGetAllVariablesDiffs</c> message, which requests the wired variable definitions that differ from the cached hashes.</summary>
            public static readonly MessageContract<WiredGetAllVariablesDiffs> DifferencesRequest =
                Flash<WiredGetAllVariablesDiffs>(MessageKeys.Wired.Variables.DifferencesRequest);

            /// <summary>The contract for the incoming <c>WiredAllVariablesDiffs</c> message, which carries one chunk of wired variable definition differences.</summary>
            public static readonly MessageContract<WiredAllVariablesDiffs> Differences =
                Flash<WiredAllVariablesDiffs>(MessageKeys.Wired.Variables.Differences);

            /// <summary>The contract for the outgoing <c>WiredGetVariablesForObject</c> message, which requests the wired variable values of a furni, a user or the global scope.</summary>
            public static readonly MessageContract<WiredGetVariablesForObject> ObjectRequest =
                Flash<WiredGetVariablesForObject>(MessageKeys.Wired.Variables.ObjectRequest);

            /// <summary>The contract for the incoming <c>WiredVariablesForObject</c> message, which carries the wired variable values of a furni, a user or the global scope.</summary>
            public static readonly MessageContract<WiredVariablesForObject> Object =
                Flash<WiredVariablesForObject>(MessageKeys.Wired.Variables.Object);

            /// <summary>The contract for the outgoing <c>WiredGetAllVariableHolders</c> message, which requests a wired variable and every object that holds it.</summary>
            public static readonly MessageContract<WiredGetAllVariableHolders> HoldersRequest =
                Flash<WiredGetAllVariableHolders>(MessageKeys.Wired.Variables.HoldersRequest);

            /// <summary>The contract for the incoming <c>WiredAllVariableHolders</c> message, which carries a wired variable and every object that holds it.</summary>
            public static readonly MessageContract<WiredAllVariableHolders> Holders =
                Flash<WiredAllVariableHolders>(MessageKeys.Wired.Variables.Holders);

            /// <summary>The contract for the outgoing <c>WiredDeleteAllVariableHolders</c> message, which removes a wired variable from every furni and user that holds it.</summary>
            public static readonly MessageContract<WiredDeleteAllVariableHolders> HoldersDelete =
                Flash<WiredDeleteAllVariableHolders>(MessageKeys.Wired.Variables.HoldersDelete);

            /// <summary>The contract for the outgoing <c>WiredGetUserPermanentVariables</c> message, which requests the permanent wired variables of an entity.</summary>
            public static readonly MessageContract<WiredGetUserPermanentVariables> PermanentRequest =
                Flash<WiredGetUserPermanentVariables>(MessageKeys.Wired.Variables.PermanentRequest);

            /// <summary>The contract for the incoming <c>WiredUserPermanentVariables</c> message, which carries the permanent wired variables of an entity.</summary>
            public static readonly MessageContract<WiredUserPermanentVariables> Permanent =
                Flash<WiredUserPermanentVariables>(MessageKeys.Wired.Variables.Permanent);

            /// <summary>The contract for the outgoing <c>WiredGetVariableOwnersPage</c> message, which requests a page of the entities that hold a permanent wired variable.</summary>
            public static readonly MessageContract<WiredGetVariableOwnersPage> OwnersRequest =
                Flash<WiredGetVariableOwnersPage>(MessageKeys.Wired.Variables.OwnersRequest);

            /// <summary>The contract for the incoming <c>WiredUserVariablesList</c> message, which carries a page of the entities that hold a permanent wired variable.</summary>
            public static readonly MessageContract<WiredUserVariablesList> Owners =
                Flash<WiredUserVariablesList>(MessageKeys.Wired.Variables.Owners);

            /// <summary>The contract for the outgoing <c>WiredSetObjectVariableValue</c> message, which writes, creates or deletes a wired variable value on a furni, a user or the global scope.</summary>
            public static readonly MessageContract<WiredSetObjectVariableValue> ObjectValueSet =
                Flash<WiredSetObjectVariableValue>(MessageKeys.Wired.Variables.ObjectValueSet);

            /// <summary>The contract for the outgoing <c>WiredSetUserPermanentVariable</c> message, which writes, creates or deletes a permanent wired variable value.</summary>
            public static readonly MessageContract<WiredSetUserPermanentVariable> PermanentValueSet =
                Flash<WiredSetUserPermanentVariable>(MessageKeys.Wired.Variables.PermanentValueSet);

            /// <summary>The contract for the incoming <c>WiredSetUserPermanentVariableResult</c> message, which reports whether a permanent wired variable update succeeded.</summary>
            public static readonly MessageContract<WiredSetUserPermanentVariableResult> PermanentValueSetResult =
                Flash<WiredSetUserPermanentVariableResult>(MessageKeys.Wired.Variables.PermanentValueSetResult);
        }

        /// <summary>Contains the wired chest message contracts.</summary>
        public static class Chests
        {
            /// <summary>The contract for the incoming <c>OpenChest</c> message, which tells the client to open a wired chest.</summary>
            public static readonly MessageContract<OpenChest> Opened =
                Flash<OpenChest>(MessageKeys.Wired.Chests.Opened);

            /// <summary>The contract for the incoming <c>CoinsChestContents</c> message, which carries the coins stored in a wired chest.</summary>
            public static readonly MessageContract<CoinsChestContents> Coins =
                Flash<CoinsChestContents>(MessageKeys.Wired.Chests.Coins);

            /// <summary>The contract for the incoming <c>ItemsChestContentsChunk</c> message, which carries one fragment of the items stored in a wired chest.</summary>
            public static readonly MessageContract<ItemsChestContentsChunk> ItemsChunk =
                Flash<ItemsChestContentsChunk>(MessageKeys.Wired.Chests.ItemsChunk);

            /// <summary>The contract for the incoming <c>ItemsChestContentsUpdated</c> message, which carries the items removed from and added to a wired chest.</summary>
            public static readonly MessageContract<ItemsChestContentsUpdated> ItemsUpdated =
                Flash<ItemsChestContentsUpdated>(MessageKeys.Wired.Chests.ItemsUpdated);

            /// <summary>The contract for the incoming <c>UpgradeChestResult</c> message, which carries the result of a wired chest capacity upgrade.</summary>
            public static readonly MessageContract<UpgradeChestResult> UpgradeResult =
                Flash<UpgradeChestResult>(MessageKeys.Wired.Chests.UpgradeResult);

            /// <summary>The contract for the incoming <c>ChestPreferencesUpdateSuccess</c> message, which confirms an update to the preferences or notification preferences of a wired chest.</summary>
            public static readonly MessageContract<ChestPreferencesUpdateSuccess> PreferencesUpdated =
                Flash<ChestPreferencesUpdateSuccess>(MessageKeys.Wired.Chests.PreferencesUpdated);

            /// <summary>The contract for the outgoing <c>OpenChestAndGetContents</c> message, which opens a wired chest and requests its contents.</summary>
            public static readonly MessageContract<OpenChestAndGetContents> OpenRequest =
                Flash<OpenChestAndGetContents>(MessageKeys.Wired.Chests.OpenRequest);

            /// <summary>The contract for the outgoing <c>CloseChest</c> message, which closes a wired chest.</summary>
            public static readonly MessageContract<CloseChest> Close =
                Flash<CloseChest>(MessageKeys.Wired.Chests.Close);

            /// <summary>The contract for the outgoing <c>LockAllChests</c> message, which locks or unlocks wired chests in the room.</summary>
            public static readonly MessageContract<LockAllChests> LockAll =
                Flash<LockAllChests>(MessageKeys.Wired.Chests.LockAll);

            /// <summary>The contract for the outgoing <c>UpgradeChest</c> message, which buys capacity upgrades for a wired chest.</summary>
            public static readonly MessageContract<UpgradeChest> Upgrade =
                Flash<UpgradeChest>(MessageKeys.Wired.Chests.Upgrade);

            /// <summary>The contract for the outgoing <c>WithdrawAllFromChest</c> message, which withdraws everything from a wired chest.</summary>
            public static readonly MessageContract<WithdrawAllFromChest> WithdrawAll =
                Flash<WithdrawAllFromChest>(MessageKeys.Wired.Chests.WithdrawAll);

            /// <summary>The contract for the outgoing <c>WithdrawCoinsFromChest</c> message, which withdraws coins from a wired chest.</summary>
            public static readonly MessageContract<WithdrawCoinsFromChest> WithdrawCoins =
                Flash<WithdrawCoinsFromChest>(MessageKeys.Wired.Chests.WithdrawCoins);

            /// <summary>The contract for the outgoing <c>WithdrawItemsFromChest</c> message, which withdraws items of one type from a wired chest.</summary>
            public static readonly MessageContract<WithdrawItemsFromChest> WithdrawItems =
                Flash<WithdrawItemsFromChest>(MessageKeys.Wired.Chests.WithdrawItems);

            /// <summary>The contract for the outgoing <c>StartAddingToChest</c> message, which starts the wired trade used to deposit inventory items into a chest.</summary>
            public static readonly MessageContract<StartAddingToChest> StartAdding =
                Flash<StartAddingToChest>(MessageKeys.Wired.Chests.StartAdding);

            /// <summary>The contract for the outgoing <c>SetChestOptions</c> message, which updates the lock, auto lock and capacity options of a wired chest.</summary>
            public static readonly MessageContract<SetChestOptions> OptionsUpdate =
                Flash<SetChestOptions>(MessageKeys.Wired.Chests.OptionsUpdate);

            /// <summary>The contract for the outgoing <c>SetChestPreferences</c> message, which updates the name, description, state and preview preferences of a wired chest.</summary>
            public static readonly MessageContract<SetChestPreferences> PreferencesUpdate =
                Flash<SetChestPreferences>(MessageKeys.Wired.Chests.PreferencesUpdate);

            /// <summary>The contract for the outgoing <c>SetChestNotificationPreferences</c> message, which updates the notification preferences of a wired chest.</summary>
            public static readonly MessageContract<SetChestNotificationPreferences> NotificationPreferencesUpdate =
                Flash<SetChestNotificationPreferences>(MessageKeys.Wired.Chests.NotificationPreferencesUpdate);
        }

        /// <summary>Contains the wired transaction message contracts.</summary>
        public static class Transaction
        {
            /// <summary>The contract for the incoming <c>WiredTransactionSuccess</c> message, which reports a successful wired transaction and its optional reward.</summary>
            public static readonly MessageContract<WiredTransactionSuccess> Succeeded =
                Flash<WiredTransactionSuccess>(MessageKeys.Wired.Transaction.Succeeded);

            /// <summary>The contract for the incoming <c>WiredTransactionFail</c> message, which reports a failed wired transaction.</summary>
            public static readonly MessageContract<WiredTransactionFail> Failed =
                Flash<WiredTransactionFail>(MessageKeys.Wired.Transaction.Failed);

            /// <summary>The contract for the outgoing <c>WiredTransactionGetChestLogs</c> message, which requests a page of the transactions of a wired chest.</summary>
            public static readonly MessageContract<WiredTransactionGetChestLogs> ChestLogsRequest =
                Flash<WiredTransactionGetChestLogs>(MessageKeys.Wired.Transaction.ChestLogsRequest);

            /// <summary>The contract for the outgoing <c>WiredTransactionGetRoomLogs</c> message, which requests a page of the room's wired transactions.</summary>
            public static readonly MessageContract<WiredTransactionGetRoomLogs> RoomLogsRequest =
                Flash<WiredTransactionGetRoomLogs>(MessageKeys.Wired.Transaction.RoomLogsRequest);

            /// <summary>The contract for the incoming <c>WiredTransactionLogList</c> message, which carries a page of wired transactions.</summary>
            public static readonly MessageContract<WiredTransactionLogList> Logs =
                Flash<WiredTransactionLogList>(MessageKeys.Wired.Transaction.Logs);

            /// <summary>The contract for the outgoing <c>WiredTransactionGetLogDetails</c> message, which requests the details of one wired transaction.</summary>
            public static readonly MessageContract<WiredTransactionGetLogDetails> LogDetailsRequest =
                Flash<WiredTransactionGetLogDetails>(MessageKeys.Wired.Transaction.LogDetailsRequest);

            /// <summary>The contract for the incoming <c>WiredTransactionLogDetails</c> message, which carries the details of one wired transaction.</summary>
            public static readonly MessageContract<WiredTransactionLogDetails> LogDetails =
                Flash<WiredTransactionLogDetails>(MessageKeys.Wired.Transaction.LogDetails);
        }

        /// <summary>Contains the message contracts for wired contracts.</summary>
        public static class Contracts
        {
            /// <summary>The contract for the incoming <c>WiredContractContents</c> message, which carries the definition of a wired contract.</summary>
            public static readonly MessageContract<WiredContractContents> Contents =
                Flash<WiredContractContents>(MessageKeys.Wired.Contracts.Contents);

            /// <summary>The contract for the incoming <c>WiredOpenContract</c> message, which tells the client to open the editor of a wired contract.</summary>
            public static readonly MessageContract<WiredOpenContract> Opened =
                Flash<WiredOpenContract>(MessageKeys.Wired.Contracts.Opened);

            /// <summary>The contract for the outgoing <c>WiredOpenContract</c> message, which requests the contents of a wired contract.</summary>
            public static readonly MessageContract<WiredOpenContract> OpenRequest =
                Flash<WiredOpenContract>(MessageKeys.Wired.Contracts.OpenRequest);

            /// <summary>The contract for the outgoing <c>WiredUpdateContract</c> message, which replaces a wired contract.</summary>
            public static readonly MessageContract<WiredUpdateContract> Update =
                Flash<WiredUpdateContract>(MessageKeys.Wired.Contracts.Update);

            /// <summary>The contract for the incoming <c>WiredContractUpdateResult</c> message, which reports whether a wired contract update succeeded, with a failure code.</summary>
            public static readonly MessageContract<WiredContractUpdateResult> UpdateResult =
                Flash<WiredContractUpdateResult>(MessageKeys.Wired.Contracts.UpdateResult);
        }

        /// <summary>Contains the wired chest trade message contracts.</summary>
        public static class Trade
        {
            /// <summary>The contract for the incoming <c>WiredTradeInitiate</c> message, which announces a new wired chest trade and its requirement.</summary>
            public static readonly MessageContract<WiredTradeInitiate> Initiated =
                Flash<WiredTradeInitiate>(MessageKeys.Wired.Trade.Initiated);

            /// <summary>The contract for the incoming <c>WiredTradeItemsUpdate</c> message, which carries the items offered in the active wired trade and whether it can be accepted.</summary>
            public static readonly MessageContract<WiredTradeItemsUpdate> ItemsUpdated =
                Flash<WiredTradeItemsUpdate>(MessageKeys.Wired.Trade.ItemsUpdated);

            /// <summary>The contract for the incoming <c>WiredTradeCancelled</c> message, which reports that the active wired trade was canceled.</summary>
            public static readonly MessageContract<WiredTradeCancelled> Cancelled =
                Flash<WiredTradeCancelled>(MessageKeys.Wired.Trade.Cancelled);

            /// <summary>The contract for the incoming <c>WiredTradeCompleted</c> message, which reports that the active wired trade completed.</summary>
            public static readonly MessageContract<WiredTradeCompleted> Completed =
                Flash<WiredTradeCompleted>(MessageKeys.Wired.Trade.Completed);

            /// <summary>The contract for the outgoing <c>WiredTradeAddDeleteItems</c> message, which adds items to or removes items from the active wired trade.</summary>
            public static readonly MessageContract<WiredTradeAddDeleteItems> ItemsUpdate =
                Flash<WiredTradeAddDeleteItems>(MessageKeys.Wired.Trade.ItemsUpdate);

            /// <summary>The contract for the outgoing <c>WiredTradeConfirm</c> message, which sets the confirmation state of the active wired trade.</summary>
            public static readonly MessageContract<WiredTradeConfirm> Confirm =
                Flash<WiredTradeConfirm>(MessageKeys.Wired.Trade.Confirm);

            /// <summary>The contract for the outgoing <c>WiredTradeCancel</c> message, which cancels the active wired trade.</summary>
            public static readonly MessageContract<WiredTradeCancel> Cancel =
                Flash<WiredTradeCancel>(MessageKeys.Wired.Trade.Cancel);

            /// <summary>The contract for the incoming <c>WiredTradeTransactionNotification</c> message, which carries a wired trade transaction notification id.</summary>
            public static readonly MessageContract<WiredTradeTransactionNotification> Notification =
                Flash<WiredTradeTransactionNotification>(MessageKeys.Wired.Trade.Notification);
        }
    }

    /// <summary>Contains the notification message contracts.</summary>
    public static class Notifications
    {
        /// <summary>The contract for the incoming <c>MOTDNotification</c> message, which carries the hotel's messages of the day.</summary>
        public static readonly MessageContract<MOTDNotification> MessageOfTheDay =
            Flash<MOTDNotification>(MessageKeys.Notifications.MessageOfTheDay);
    }

    /// <summary>Contains the poll message contracts.</summary>
    public static class Polls
    {
        /// <summary>The contract for the incoming <c>PollContents</c> message, which carries the questions of a poll.</summary>
        public static readonly MessageContract<PollContents> Contents =
            Flash<PollContents>(MessageKeys.Polls.Contents);

        /// <summary>The contract for the incoming <c>PollError</c> message, which reports a poll error.</summary>
        public static readonly MessageContract<PollError> Error =
            Flash<PollError>(MessageKeys.Polls.Error);

        /// <summary>The contract for the incoming <c>PollOffer</c> message, which offers a poll to the local user.</summary>
        public static readonly MessageContract<PollOffer> Offer =
            Flash<PollOffer>(MessageKeys.Polls.Offer);

        /// <summary>The contract for the outgoing <c>PollAnswer</c> message, which sends answers to a poll.</summary>
        public static readonly MessageContract<PollAnswer> Answer =
            Flash<PollAnswer>(MessageKeys.Polls.Answer);

        /// <summary>The contract for the outgoing <c>PollReject</c> message, which rejects a poll.</summary>
        public static readonly MessageContract<RejectPoll> Reject =
            Flash<RejectPoll>(MessageKeys.Polls.Reject);

        /// <summary>The contract for the outgoing <c>PollStart</c> message, which starts a poll and requests its contents.</summary>
        public static readonly MessageContract<StartPoll> Start =
            Flash<StartPoll>(MessageKeys.Polls.Start);
    }

    /// <summary>Contains the room message contracts.</summary>
    public static class Room
    {
        /// <summary>The contract for the incoming <c>Objects</c> message, which carries a batch of floor items loaded into the room.</summary>
        public static readonly MessageContract<FloorItems> Objects =
            Flash<FloorItems>(MessageKeys.Room.Objects);

        /// <summary>The contract for the incoming <c>Items</c> message, which carries a batch of wall items loaded into the room.</summary>
        public static readonly MessageContract<WallItems> WallItems =
            Flash<WallItems>(MessageKeys.Room.WallItems);

        /// <summary>The contract for the outgoing <c>GetGuestRoom</c> message, which requests the navigator data of a room.</summary>
        public static readonly MessageContract<GetGuestRoomRequest> SnapshotRequest =
            Flash<GetGuestRoomRequest>(MessageKeys.Room.SnapshotRequest);

        /// <summary>The contract for the incoming <c>GetGuestRoomResult</c> message, which carries the navigator data of a room.</summary>
        public static readonly MessageContract<GuestRoomResult> Snapshot =
            Flash<GuestRoomResult>(MessageKeys.Room.Snapshot);

        /// <summary>The contract for the outgoing <c>ToggleStaffPick</c> message, which toggles whether a room is a staff pick.</summary>
        public static readonly MessageContract<ToggleRoomStaffPickRequest> StaffPickUpdateRequest =
            Flash<ToggleRoomStaffPickRequest>(MessageKeys.Room.StaffPickUpdateRequest);

        /// <summary>The contract for the outgoing <c>RateFlat</c> message, which rates the current room.</summary>
        public static readonly MessageContract<RateRoomRequest> RatingRequest =
            Flash<RateRoomRequest>(MessageKeys.Room.RatingRequest);

        /// <summary>Contains the room settings message contracts.</summary>
        public static class Settings
        {
            /// <summary>The contract for the outgoing <c>GetRoomSettings</c> message, which requests the settings of an owned room.</summary>
            public static readonly MessageContract<GetRoomSettingsRequest> Request =
                Flash<GetRoomSettingsRequest>(MessageKeys.Room.Settings.Request);

            /// <summary>The contract for the incoming <c>RoomSettingsData</c> message, which carries the settings of an owned room.</summary>
            public static readonly MessageContract<RoomSettings> Snapshot =
                new(
                    MessageKeys.Room.Settings.Snapshot,
                    MessageCodec<RoomSettings>.FromModel());

            /// <summary>The contract for the incoming <c>RoomSettingsError</c> message, which reports that the room settings could not be loaded.</summary>
            public static readonly MessageContract<RoomSettingsError> RequestFailed =
                Flash<RoomSettingsError>(MessageKeys.Room.Settings.RequestFailed);

            /// <summary>The contract for the outgoing <c>SaveRoomSettings</c> message, which saves the settings of an owned room.</summary>
            public static readonly MessageContract<SaveRoomSettingsRequest> Save =
                new(
                    MessageKeys.Room.Settings.Save,
                    MessageCodec<SaveRoomSettingsRequest>.FromModel());

            /// <summary>The contract for the incoming <c>RoomSettingsSaved</c> message, which confirms that the room settings were saved.</summary>
            public static readonly MessageContract<RoomSettingsSaved> SaveSucceeded =
                Flash<RoomSettingsSaved>(MessageKeys.Room.Settings.SaveSucceeded);

            /// <summary>The contract for the incoming <c>RoomSettingsSaveError</c> message, which reports that the room settings could not be saved.</summary>
            public static readonly MessageContract<RoomSettingsSaveError> SaveFailed =
                Flash<RoomSettingsSaveError>(MessageKeys.Room.Settings.SaveFailed);
        }

        /// <summary>Contains the room access message contracts.</summary>
        public static class Access
        {
            /// <summary>The contract for the outgoing <c>OpenFlatConnection</c> message, which requests entry to a room.</summary>
            public static readonly MessageContract<OpenFlatConnection> OpenRequest =
                Flash<OpenFlatConnection>(MessageKeys.Room.Access.OpenRequest);

            /// <summary>The contract for the incoming <c>OpenConnection</c> message, which confirms that the room connection was opened.</summary>
            public static readonly MessageContract<OpenConnectionConfirmation> OpenConfirmed =
                Flash<OpenConnectionConfirmation>(MessageKeys.Room.Access.OpenConfirmed);

            /// <summary>The contract for the incoming <c>Doorbell</c> message, which reports a user ringing the doorbell, or the local user waiting at the doorbell when the name is empty.</summary>
            public static readonly MessageContract<Doorbell> Doorbell =
                Flash<Doorbell>(MessageKeys.Room.Access.Doorbell);

            /// <summary>The contract for the outgoing <c>LetUserIn</c> message, which lets a user who rang the doorbell in or turns them away.</summary>
            public static readonly MessageContract<AnswerDoorbellRequest> DoorbellAnswer =
                Flash<AnswerDoorbellRequest>(MessageKeys.Room.Access.DoorbellAnswer);

            /// <summary>The contract for the incoming <c>RoomQueueStatus</c> message, which carries the queue status for entering a room.</summary>
            public static readonly MessageContract<RoomQueueStatus> QueueStatus =
                Flash<RoomQueueStatus>(MessageKeys.Room.Access.QueueStatus);

            /// <summary>The contract for the incoming <c>FlatAccessible</c> message, which reports that access to a room was granted to the local user or to the named user.</summary>
            public static readonly MessageContract<FlatAccessible> Granted =
                Flash<FlatAccessible>(MessageKeys.Room.Access.Granted);

            /// <summary>The contract for the incoming <c>FlatAccessDenied</c> message, which reports that access to a room was denied to the local user or to the named user.</summary>
            public static readonly MessageContract<FlatAccessDenied> Denied =
                Flash<FlatAccessDenied>(MessageKeys.Room.Access.Denied);

            /// <summary>The contract for the incoming <c>NoSuchFlat</c> message, which reports that the requested room does not exist.</summary>
            public static readonly MessageContract<NoSuchFlat> NotFound =
                Flash<NoSuchFlat>(MessageKeys.Room.Access.NotFound);

            /// <summary>The contract for the incoming <c>CantConnect</c> message, which reports why the room connection failed, such as a full room or a ban.</summary>
            public static readonly MessageContract<CanNotConnect> ConnectionFailed =
                Flash<CanNotConnect>(MessageKeys.Room.Access.ConnectionFailed);
        }

        /// <summary>Contains the room lifecycle message contracts.</summary>
        public static class Lifecycle
        {
            /// <summary>The contract for the incoming <c>RoomReady</c> message, which reports the room type and id once the room is ready to load.</summary>
            public static readonly MessageContract<RoomReady> Ready =
                Flash<RoomReady>(MessageKeys.Room.Lifecycle.Ready);

            /// <summary>The contract for the incoming <c>RoomEntryInfo</c> message, which carries the entered room id and whether the local user owns it.</summary>
            public static readonly MessageContract<RoomEntryInfo> Entry =
                Flash<RoomEntryInfo>(MessageKeys.Room.Lifecycle.Entry);

            /// <summary>The contract for the incoming <c>RoomForward</c> message, which forwards the local user to another room.</summary>
            public static readonly MessageContract<RoomForward> Forward =
                Flash<RoomForward>(MessageKeys.Room.Lifecycle.Forward);

            /// <summary>The contract for the incoming <c>CloseConnection</c> message, which reports that the room connection was closed, with an optional reason.</summary>
            public static readonly MessageContract<CloseConnection> ConnectionClosed =
                Flash<CloseConnection>(MessageKeys.Room.Lifecycle.ConnectionClosed);

            /// <summary>The contract for the outgoing <c>Quit</c> message, which leaves the current room.</summary>
            public static readonly MessageContract<QuitRoomRequest> Quit =
                Flash<QuitRoomRequest>(MessageKeys.Room.Lifecycle.Quit);
        }

        /// <summary>Contains the room environment message contracts.</summary>
        public static class Environment
        {
            /// <summary>The contract for the incoming <c>RoomEntryTile</c> message, which carries the room's door tile and direction.</summary>
            public static readonly MessageContract<RoomEntryTile> EntryTile =
                Flash<RoomEntryTile>(MessageKeys.Room.Environment.EntryTile);

            /// <summary>The contract for the incoming <c>RoomProperty</c> message, which carries a room property, such as the wallpaper, floor or landscape.</summary>
            public static readonly MessageContract<FlatProperty> Property =
                Flash<FlatProperty>(MessageKeys.Room.Environment.Property);

            /// <summary>The contract for the incoming <c>RoomVisualizationSettings</c> message, which carries whether the walls are hidden and the wall and floor thickness.</summary>
            public static readonly MessageContract<RoomVisualizationSettings> Visualization =
                Flash<RoomVisualizationSettings>(MessageKeys.Room.Environment.Visualization);

            /// <summary>The contract for the incoming <c>RoomChatSettings</c> message, which carries the room's chat settings.</summary>
            public static readonly MessageContract<RoomChatSettings> ChatSettings =
                Flash<RoomChatSettings>(MessageKeys.Room.Environment.ChatSettings);

            /// <summary>The contract for the incoming <c>FloorHeightmap</c> message, which carries the room's floor plan.</summary>
            public static readonly MessageContract<FloorPlan> FloorPlan =
                Flash<FloorPlan>(MessageKeys.Room.Environment.FloorPlan);

            /// <summary>The incoming AreaHide live region update.</summary>
            public static readonly MessageContract<AreaHideData> AreaHide =
                Flash<AreaHideData>(MessageKeys.Room.Environment.AreaHide);

            /// <summary>The outgoing SetAreaHideData editor update.</summary>
            public static readonly MessageContract<SetAreaHideData> AreaHideSet =
                Flash<SetAreaHideData>(MessageKeys.Room.Environment.AreaHideSet);
        }

        /// <summary>Contains the room chat message contracts.</summary>
        public static class Chat
        {
            /// <summary>The contract for the incoming <c>Chat</c> message, which carries a chat message said by an avatar in the room.</summary>
            public static readonly MessageContract<AvatarChat> Talk =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Talk);

            /// <summary>The contract for the incoming <c>Shout</c> message, which carries a chat message shouted by an avatar in the room.</summary>
            public static readonly MessageContract<AvatarChat> Shout =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Shout);

            /// <summary>The contract for the incoming <c>Whisper</c> message, which carries a whispered chat message.</summary>
            public static readonly MessageContract<AvatarChat> Whisper =
                Flash<AvatarChat>(MessageKeys.Room.Chat.Whisper);

            /// <summary>The contract for the outgoing <c>Whisper</c> message, which whispers a chat message to a user in the room.</summary>
            public static readonly MessageContract<WhisperRequest> WhisperSend =
                Flash<WhisperRequest>(MessageKeys.Room.Chat.WhisperSend);

            /// <summary>The contract for the incoming <c>SpecialSystemChat</c> message, which carries a special room chat signal associated with an avatar.</summary>
            public static readonly MessageContract<SpecialSystemChat> SpecialSystem =
                Flash<SpecialSystemChat>(MessageKeys.Room.Chat.SpecialSystem);

            /// <summary>The contract for the outgoing <c>Chat</c> message, which sends a chat message to the room.</summary>
            public static readonly MessageContract<TalkRequest> TalkSend =
                Flash<TalkRequest>(MessageKeys.Room.Chat.TalkSend);

            /// <summary>The contract for the outgoing <c>Shout</c> message, which shouts a chat message to the room.</summary>
            public static readonly MessageContract<ShoutRequest> ShoutSend =
                Flash<ShoutRequest>(MessageKeys.Room.Chat.ShoutSend);
        }

        /// <summary>Contains the room rights message contracts.</summary>
        public static class Authority
        {
            /// <summary>The contract for the outgoing <c>GetFlatControllers</c> message, which requests the users with rights in a room.</summary>
            public static readonly MessageContract<GetFlatControllersRequest> ControllersRequest =
                Flash<GetFlatControllersRequest>(MessageKeys.Room.Authority.ControllersRequest);

            /// <summary>The contract for the incoming <c>FlatControllers</c> message, which carries the users with rights in a room.</summary>
            public static readonly MessageContract<RightsList> ControllersSnapshot =
                Flash<RightsList>(MessageKeys.Room.Authority.ControllersSnapshot);

            /// <summary>The contract for the outgoing <c>AssignRights</c> message, which gives a user rights in the current room.</summary>
            public static readonly MessageContract<GiveRoomRightsRequest> ControllerGrantRequest =
                Flash<GiveRoomRightsRequest>(MessageKeys.Room.Authority.ControllerGrantRequest);

            /// <summary>The contract for the incoming <c>YouAreController</c> message, which reports the local user's rights level in the room.</summary>
            public static readonly MessageContract<YouAreController> ControllerGranted =
                Flash<YouAreController>(MessageKeys.Room.Authority.ControllerGranted);

            /// <summary>The contract for the incoming <c>YouAreNotController</c> message, which reports that the local user no longer has rights in the room.</summary>
            public static readonly MessageContract<YouAreNotController> ControllerRevoked =
                Flash<YouAreNotController>(MessageKeys.Room.Authority.ControllerRevoked);

            /// <summary>The contract for the incoming <c>YouAreOwner</c> message, which reports that the local user owns the room.</summary>
            public static readonly MessageContract<YouAreOwner> Owner =
                Flash<YouAreOwner>(MessageKeys.Room.Authority.Owner);

            /// <summary>The contract for the incoming <c>YouAreSpectator</c> message, which reports that the local user is in the room as a spectator.</summary>
            public static readonly MessageContract<YouAreSpectator> SpectatorGranted =
                Flash<YouAreSpectator>(MessageKeys.Room.Authority.SpectatorGranted);

            /// <summary>The contract for the incoming <c>YouAreNotSpectator</c> message, which reports that the local user is no longer a spectator in the room.</summary>
            public static readonly MessageContract<YouAreNotSpectator> SpectatorRevoked =
                Flash<YouAreNotSpectator>(MessageKeys.Room.Authority.SpectatorRevoked);
        }

        /// <summary>Contains the message contracts for avatars in the room.</summary>
        public static class Occupants
        {
            /// <summary>The contract for the incoming <c>Users</c> message, which carries avatars added to the room.</summary>
            public static readonly MessageContract<RoomUsers> Snapshot =
                Flash<RoomUsers>(MessageKeys.Room.Occupants.Snapshot);

            /// <summary>The contract for the incoming <c>UserRemove</c> message, which announces an avatar that left the room.</summary>
            public static readonly MessageContract<AvatarRemove> Removed =
                Flash<AvatarRemove>(MessageKeys.Room.Occupants.Removed);

            /// <summary>The contract for the incoming <c>UserUpdate</c> message, which carries status updates, such as position and posture, for avatars in the room.</summary>
            public static readonly MessageContract<UserUpdate> Status =
                Flash<UserUpdate>(MessageKeys.Room.Occupants.Status);

            /// <summary>The contract for the incoming <c>RespectNotification</c> message, which reports that a user received a respect, with their new respect total.</summary>
            public static readonly MessageContract<RespectNotification> Respect =
                Flash<RespectNotification>(MessageKeys.Room.Occupants.Respect);

            /// <summary>The contract for the outgoing <c>RespectUser</c> message, which gives a respect to a user.</summary>
            public static readonly MessageContract<RespectUserRequest> RespectRequest =
                Flash<RespectUserRequest>(MessageKeys.Room.Occupants.RespectRequest);

            /// <summary>Contains the avatar action message contracts.</summary>
            public static class Action
            {
                /// <summary>The contract for the incoming <c>Dance</c> message, which carries the dance of an avatar in the room.</summary>
                public static readonly MessageContract<AvatarDanceUpdate> Dance =
                    Flash<AvatarDanceUpdate>(MessageKeys.Room.Occupants.Action.Dance);

                /// <summary>The contract for the outgoing <c>Dance</c> message, which starts or stops the local user's dance.</summary>
                public static readonly MessageContract<AvatarDanceRequest> DanceRequest =
                    Flash<AvatarDanceRequest>(MessageKeys.Room.Occupants.Action.DanceRequest);

                /// <summary>The contract for the outgoing <c>Sign</c> message, which holds up a sign over the local user's avatar.</summary>
                public static readonly MessageContract<AvatarSignRequest> SignRequest =
                    Flash<AvatarSignRequest>(MessageKeys.Room.Occupants.Action.SignRequest);

                /// <summary>The contract for the incoming <c>AvatarEffect</c> message, which carries the effect an avatar in the room wears.</summary>
                public static readonly MessageContract<AvatarEffectUpdate> Effect =
                    Flash<AvatarEffectUpdate>(MessageKeys.Room.Occupants.Action.Effect);

                /// <summary>The contract for the outgoing <c>AvatarEffectSelected</c> message, which selects the avatar effect the local user wears.</summary>
                public static readonly MessageContract<AvatarEffectSelectionRequest> EffectSelectionRequest =
                    Flash<AvatarEffectSelectionRequest>(MessageKeys.Room.Occupants.Action.EffectSelectionRequest);

                /// <summary>The contract for the outgoing <c>ChangePosture</c> message, which sets the posture of the local user's avatar.</summary>
                public static readonly MessageContract<AvatarPostureRequest> PostureRequest =
                    Flash<AvatarPostureRequest>(MessageKeys.Room.Occupants.Action.PostureRequest);

                /// <summary>The contract for the incoming <c>CarryObject</c> message, which reports the hand item an avatar in the room holds.</summary>
                public static readonly MessageContract<AvatarCarryUpdate> Carry =
                    Flash<AvatarCarryUpdate>(MessageKeys.Room.Occupants.Action.Carry);

                /// <summary>The contract for the incoming <c>Sleep</c> message, which reports whether an avatar in the room is idle.</summary>
                public static readonly MessageContract<AvatarSleepUpdate> Sleep =
                    Flash<AvatarSleepUpdate>(MessageKeys.Room.Occupants.Action.Sleep);

                /// <summary>The contract for the incoming <c>UserTyping</c> message, which reports whether an avatar in the room is typing.</summary>
                public static readonly MessageContract<AvatarTypingUpdate> Typing =
                    Flash<AvatarTypingUpdate>(MessageKeys.Room.Occupants.Action.Typing);

                /// <summary>The contract for the incoming <c>Expression</c> message, which carries an expression, such as a wave, performed by an avatar in the room.</summary>
                public static readonly MessageContract<AvatarAction> Expression =
                    Flash<AvatarAction>(MessageKeys.Room.Occupants.Action.Expression);

                /// <summary>The contract for the outgoing <c>AvatarExpression</c> message, which performs an avatar expression, such as a wave.</summary>
                public static readonly MessageContract<AvatarExpressionRequest> ExpressionRequest =
                    Flash<AvatarExpressionRequest>(MessageKeys.Room.Occupants.Action.ExpressionRequest);
            }

            /// <summary>Contains the avatar identity message contracts.</summary>
            public static class Identity
            {
                /// <summary>The contract for the incoming <c>UserChange</c> message, which carries the changed figure, gender and motto of an avatar in the room.</summary>
                public static readonly MessageContract<UserChanged> Appearance =
                    Flash<UserChanged>(MessageKeys.Room.Occupants.Identity.Appearance);

                /// <summary>The contract for the incoming <c>UserNameChanged</c> message, which reports the new name of an avatar in the room.</summary>
                public static readonly MessageContract<UserNameChanged> Name =
                    Flash<UserNameChanged>(MessageKeys.Room.Occupants.Identity.Name);

                /// <summary>The contract for the incoming <c>FavouriteMembershipUpdate</c> message, which reports the favorite group an avatar in the room displays.</summary>
                public static readonly MessageContract<FavouriteMembershipUpdate> FavoriteGroup =
                    Flash<FavouriteMembershipUpdate>(MessageKeys.Room.Occupants.Identity.FavoriteGroup);
            }

            /// <summary>Contains the room pet message contracts.</summary>
            public static class Pet
            {
                /// <summary>The contract for the outgoing <c>GetPetInfo</c> message, which requests the statistics of a pet.</summary>
                public static readonly MessageContract<GetPetInfoRequest> InfoRequest =
                    Flash<GetPetInfoRequest>(MessageKeys.Room.Occupants.Pet.InfoRequest);

                /// <summary>The contract for the incoming <c>PetInfo</c> message, which carries the statistics of a pet.</summary>
                public static readonly MessageContract<PetInfo> Info =
                    Flash<PetInfo>(MessageKeys.Room.Occupants.Pet.Info);

                /// <summary>The contract for the incoming <c>PetFigureUpdate</c> message, which reports the new figure, saddle or rider of a pet in the room.</summary>
                public static readonly MessageContract<PetFigureUpdate> Figure =
                    Flash<PetFigureUpdate>(MessageKeys.Room.Occupants.Pet.Figure);

                /// <summary>The contract for the incoming <c>PetStatusUpdate</c> message, which reports a change to the breeding, harvesting or reviving state of a pet in the room.</summary>
                public static readonly MessageContract<PetStatusUpdate> Status =
                    Flash<PetStatusUpdate>(MessageKeys.Room.Occupants.Pet.Status);

                /// <summary>The contract for the incoming <c>PetLevelUpdate</c> message, which reports that a pet in the room gained a level.</summary>
                public static readonly MessageContract<PetLevelUpdate> Level =
                    Flash<PetLevelUpdate>(MessageKeys.Room.Occupants.Pet.Level);

                /// <summary>The contract for the outgoing <c>RespectPet</c> message, which gives a respect to a pet.</summary>
                public static readonly MessageContract<RespectPetRequest> RespectRequest =
                    Flash<RespectPetRequest>(MessageKeys.Room.Occupants.Pet.RespectRequest);

                /// <summary>The contract for the outgoing <c>MountPet</c> message, which mounts or dismounts a rideable pet.</summary>
                public static readonly MessageContract<MountPetRequest> MountRequest =
                    Flash<MountPetRequest>(MessageKeys.Room.Occupants.Pet.MountRequest);

                /// <summary>The contract for the outgoing <c>RemovePetFromFlat</c> message, which removes a pet from the room.</summary>
                public static readonly MessageContract<RemovePetFromRoomRequest> RemoveRequest =
                    Flash<RemovePetFromRoomRequest>(MessageKeys.Room.Occupants.Pet.RemoveRequest);
            }

            /// <summary>Contains the room bot message contracts.</summary>
            public static class Bot
            {
                /// <summary>The contract for the outgoing <c>RemoveBotFromFlat</c> message, which removes a bot from the room.</summary>
                public static readonly MessageContract<RemoveBotFromFlat> RemoveRequest =
                    Flash<RemoveBotFromFlat>(MessageKeys.Room.Occupants.Bot.RemoveRequest);
            }
        }

        /// <summary>Contains the hand item message contracts.</summary>
        public static class HandItem
        {
            /// <summary>The contract for the incoming <c>HandItemReceived</c> message, which reports a hand item another user passed to the local user.</summary>
            public static readonly MessageContract<HandItemReceived> Received =
                Flash<HandItemReceived>(MessageKeys.Room.HandItem.Received);

            /// <summary>The contract for the outgoing <c>DropCarryItem</c> message, which drops the item the local user's avatar holds.</summary>
            public static readonly MessageContract<DropHandItemRequest> Drop =
                Flash<DropHandItemRequest>(MessageKeys.Room.HandItem.Drop);

            /// <summary>The contract for the outgoing <c>PassCarryItem</c> message, which gives the item the local user's avatar holds to another user.</summary>
            public static readonly MessageContract<PassHandItemRequest> Pass =
                Flash<PassHandItemRequest>(MessageKeys.Room.HandItem.Pass);
        }

        /// <summary>Contains the room item message contracts.</summary>
        public static class Item
        {
            /// <summary>The contract for the outgoing <c>PlaceObject</c> message, which places a floor or wall item from the inventory in the room.</summary>
            public static readonly MessageContract<PlaceRoomItemRequest> Place =
                new(
                    MessageKeys.Room.Item.Place,
                    new MessageCodec<PlaceRoomItemRequest>(PlaceRoomItemRequest.ParseFlash,
                        PlaceRoomItemRequest.ComposeFlash));

            /// <summary>The contract for the outgoing <c>PickupObject</c> message, which picks up a floor or wall item from the room.</summary>
            public static readonly MessageContract<PickupRoomItemRequest> Pickup =
                Flash<PickupRoomItemRequest>(MessageKeys.Room.Item.Pickup);

            /// <summary>The contract for the outgoing <c>ClickFurni</c> message, which reports a click on a floor or wall item.</summary>
            public static readonly MessageContract<ClickRoomItemRequest> Click =
                Flash<ClickRoomItemRequest>(MessageKeys.Room.Item.Click);

            /// <summary>The contract for the incoming <c>ObjectRemoveConfirm</c> message, which asks the local user to confirm picking up an item.</summary>
            public static readonly MessageContract<PickupConfirmation> PickupConfirmation =
                Flash<PickupConfirmation>(MessageKeys.Room.Item.PickupConfirmation);
        }

        /// <summary>Contains the floor item message contracts.</summary>
        public static class FloorItem
        {
            /// <summary>The contract for the incoming <c>ObjectAdd</c> message, which announces a floor item added to the room.</summary>
            public static readonly MessageContract<FloorItemAdd> Added =
                Flash<FloorItemAdd>(MessageKeys.Room.FloorItem.Added);

            /// <summary>The contract for the incoming <c>ObjectRemove</c> message, which announces a floor item removed from the room.</summary>
            public static readonly MessageContract<FloorItemRemove> Removed =
                Flash<FloorItemRemove>(MessageKeys.Room.FloorItem.Removed);

            /// <summary>The contract for the incoming <c>ObjectRemoveMultiple</c> message, which announces several floor items removed from the room at once.</summary>
            public static readonly MessageContract<FloorItemsRemove> RemovedMultiple =
                Flash<FloorItemsRemove>(MessageKeys.Room.FloorItem.RemovedMultiple);

            /// <summary>The contract for the incoming <c>ObjectUpdate</c> message, which carries an updated floor item.</summary>
            public static readonly MessageContract<FloorItemUpdate> Updated =
                Flash<FloorItemUpdate>(MessageKeys.Room.FloorItem.Updated);

            /// <summary>The contract for the incoming <c>ObjectDataUpdate</c> message, which carries the new data of a floor item.</summary>
            public static readonly MessageContract<FloorItemDataUpdate> DataUpdated =
                Flash<FloorItemDataUpdate>(MessageKeys.Room.FloorItem.DataUpdated);

            /// <summary>The contract for the incoming <c>ObjectsDataUpdate</c> message, which carries the new data of several floor items at once.</summary>
            public static readonly MessageContract<FloorItemsDataUpdate> DataBatchUpdated =
                Flash<FloorItemsDataUpdate>(MessageKeys.Room.FloorItem.DataBatchUpdated);

            /// <summary>The contract for the outgoing <c>UseFurniture</c> message, which uses a floor item.</summary>
            public static readonly MessageContract<UseFloorItemRequest> Use =
                Flash<UseFloorItemRequest>(MessageKeys.Room.FloorItem.Use);

            /// <summary>The contract for the outgoing <c>MoveObject</c> message, which moves a floor item to a tile and direction.</summary>
            public static readonly MessageContract<MoveFloorItemRequest> Move =
                Flash<MoveFloorItemRequest>(MessageKeys.Room.FloorItem.Move);

            /// <summary>The contract for the outgoing <c>ThrowDice</c> message, which throws a dice.</summary>
            public static readonly MessageContract<ThrowDiceRequest> ThrowDice =
                Flash<ThrowDiceRequest>(MessageKeys.Room.FloorItem.ThrowDice);

            /// <summary>The contract for the outgoing <c>DiceOff</c> message, which turns a dice off.</summary>
            public static readonly MessageContract<DiceOffRequest> DiceOff =
                Flash<DiceOffRequest>(MessageKeys.Room.FloorItem.DiceOff);

            /// <summary>The contract for the incoming <c>DiceValue</c> message, which carries the value a dice shows.</summary>
            public static readonly MessageContract<DiceValue> DiceValue =
                Flash<DiceValue>(MessageKeys.Room.FloorItem.DiceValue);

            /// <summary>The contract for the incoming <c>OneWayDoorStatus</c> message, which carries the new state of a one way door.</summary>
            public static readonly MessageContract<OneWayDoorStatus> OneWayDoorStatus =
                Flash<OneWayDoorStatus>(MessageKeys.Room.FloorItem.OneWayDoorStatus);

            /// <summary>The contract for the outgoing <c>EnterOneWayDoor</c> message, which enters a one way door.</summary>
            public static readonly MessageContract<EnterOneWayDoorRequest> OneWayDoorEnter =
                Flash<EnterOneWayDoorRequest>(MessageKeys.Room.FloorItem.OneWayDoorEnter);
        }

        /// <summary>Contains the wall item message contracts.</summary>
        public static class WallItem
        {
            /// <summary>The contract for the incoming <c>ItemAdd</c> message, which announces a wall item added to the room.</summary>
            public static readonly MessageContract<WallItemAdd> Added =
                Flash<WallItemAdd>(MessageKeys.Room.WallItem.Added);

            /// <summary>The contract for the incoming <c>ItemRemove</c> message, which announces a wall item removed from the room.</summary>
            public static readonly MessageContract<WallItemRemove> Removed =
                Flash<WallItemRemove>(MessageKeys.Room.WallItem.Removed);

            /// <summary>The contract for the incoming <c>ItemRemoveMultiple</c> message, which announces several wall items removed from the room at once.</summary>
            public static readonly MessageContract<WallItemsRemove> RemovedMultiple =
                Flash<WallItemsRemove>(MessageKeys.Room.WallItem.RemovedMultiple);

            /// <summary>The contract for the incoming <c>ItemUpdate</c> message, which carries an updated wall item.</summary>
            public static readonly MessageContract<WallItemUpdate> Updated =
                Flash<WallItemUpdate>(MessageKeys.Room.WallItem.Updated);

            /// <summary>The contract for the incoming <c>ItemStateUpdate</c> message, which carries the new data of a wall item.</summary>
            public static readonly MessageContract<ItemStateUpdate> DataUpdated =
                Flash<ItemStateUpdate>(MessageKeys.Room.WallItem.DataUpdated);

            /// <summary>The contract for the incoming <c>ItemsStateUpdate</c> message, which carries the new data of several wall items at once.</summary>
            public static readonly MessageContract<WallItemsStateUpdate> DataBatchUpdated =
                Flash<WallItemsStateUpdate>(MessageKeys.Room.WallItem.DataBatchUpdated);

            /// <summary>The contract for the outgoing <c>UseWallItem</c> message, which uses a wall item.</summary>
            public static readonly MessageContract<UseWallItemRequest> Use =
                Flash<UseWallItemRequest>(MessageKeys.Room.WallItem.Use);

            /// <summary>The contract for the outgoing <c>MoveWallItem</c> message, which moves a wall item to a wall location.</summary>
            public static readonly MessageContract<MoveWallItemRequest> Move =
                new(
                    MessageKeys.Room.WallItem.Move,
                    MessageCodec<MoveWallItemRequest>.FromModel());

            /// <summary>The contract for the outgoing <c>RemoveItem</c> message, which deletes a wall item, such as a post-it note, from the room.</summary>
            public static readonly MessageContract<RemoveWallItemRequest> Remove =
                Flash<RemoveWallItemRequest>(MessageKeys.Room.WallItem.Remove);

            /// <summary>The contract for the outgoing <c>SetItemData</c> message, which sets the color and text of a post-it note.</summary>
            public static readonly MessageContract<SetStickyDataRequest> StickyDataSet =
                Flash<SetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataSet);

            /// <summary>The contract for the outgoing <c>GetItemData</c> message, which requests the color and text of a post-it note.</summary>
            public static readonly MessageContract<GetStickyDataRequest> StickyDataRequest =
                Flash<GetStickyDataRequest>(MessageKeys.Room.WallItem.StickyDataRequest);

            /// <summary>The contract for the incoming <c>ItemDataUpdate</c> message, which carries the color and text of a post-it note.</summary>
            public static readonly MessageContract<Sticky> StickyData =
                Flash<Sticky>(MessageKeys.Room.WallItem.StickyData);

            /// <summary>The contract for the outgoing <c>PlacePostIt</c> message, which places a post-it note on a wall.</summary>
            public static readonly MessageContract<PlacePostItRequest> PostItPlace =
                Flash<PlacePostItRequest>(MessageKeys.Room.WallItem.PostItPlace);

            /// <summary>The contract for the outgoing <c>AddSpamWallPostIt</c> message, which places a post-it note on a wall with its color and text.</summary>
            public static readonly MessageContract<AddSpamWallPostItRequest> SpamPostItAdd =
                Flash<AddSpamWallPostItRequest>(MessageKeys.Room.WallItem.SpamPostItAdd);
        }

        /// <summary>Contains the heightmap message contracts.</summary>
        public static class Heightmap
        {
            /// <summary>The contract for the incoming <c>HeightMap</c> message, which carries the stacking height of every tile in the room.</summary>
            public static readonly MessageContract<global::Qx.Model.Heightmap> Snapshot =
                Flash<global::Qx.Model.Heightmap>(MessageKeys.Room.Heightmap.Snapshot);

            /// <summary>The contract for the incoming <c>HeightMapUpdate</c> message, which carries the tiles of the room heightmap that changed.</summary>
            public static readonly MessageContract<HeightmapUpdate> Diff =
                Flash<HeightmapUpdate>(MessageKeys.Room.Heightmap.Diff);
        }

        /// <summary>Contains the room movement message contracts.</summary>
        public static class Movement
        {
            /// <summary>The contract for the outgoing <c>MoveAvatar</c> message, which walks the local user's avatar to a tile.</summary>
            public static readonly MessageContract<WalkRequest> Walk =
                Flash<WalkRequest>(MessageKeys.Room.Movement.Walk);

            /// <summary>The contract for the outgoing <c>LookTo</c> message, which turns the local user's avatar to face a tile.</summary>
            public static readonly MessageContract<LookToRequest> LookTo =
                Flash<LookToRequest>(MessageKeys.Room.Movement.LookTo);

            /// <summary>The contract for the incoming <c>SlideObjectBundle</c> message, which slides floor items and an avatar from one tile to another, such as on a roller.</summary>
            public static readonly MessageContract<SlideObjectBundle> Slide =
                Flash<SlideObjectBundle>(MessageKeys.Room.Movement.Slide);

            /// <summary>The contract for the incoming <c>WiredMovements</c> message, which carries movements of avatars and furni caused by wired.</summary>
            public static readonly MessageContract<WiredMovements> Wired =
                Flash<WiredMovements>(MessageKeys.Room.Movement.Wired);
        }

        /// <summary>Contains the typing indicator message contracts.</summary>
        public static class Typing
        {
            /// <summary>The contract for the outgoing <c>StartTyping</c> message, which shows the typing indicator over the local user's avatar.</summary>
            public static readonly MessageContract<StartTypingRequest> Start =
                Flash<StartTypingRequest>(MessageKeys.Room.Typing.Start);

            /// <summary>The contract for the outgoing <c>CancelTyping</c> message, which hides the typing indicator over the local user's avatar.</summary>
            public static readonly MessageContract<CancelTypingRequest> Cancel =
                Flash<CancelTypingRequest>(MessageKeys.Room.Typing.Cancel);
        }

        /// <summary>Contains the room moderation message contracts.</summary>
        public static class Moderation
        {
            /// <summary>The contract for the outgoing <c>GetBannedUsersFromRoom</c> message, which requests the users banned from a room.</summary>
            public static readonly MessageContract<GetRoomBansRequest> BansRequest =
                Flash<GetRoomBansRequest>(MessageKeys.Room.Moderation.BansRequest);

            /// <summary>The contract for the incoming <c>BannedUsersFromRoom</c> message, which carries the users banned from a room.</summary>
            public static readonly MessageContract<BannedUsersFromRoom> BansSnapshot =
                Flash<BannedUsersFromRoom>(MessageKeys.Room.Moderation.BansSnapshot);

            /// <summary>The contract for the incoming <c>UserUnbannedFromRoom</c> message, which reports that a user was unbanned from a room.</summary>
            public static readonly MessageContract<UserUnbannedFromRoom> UserUnbanned =
                Flash<UserUnbannedFromRoom>(MessageKeys.Room.Moderation.UserUnbanned);

            /// <summary>The contract for the outgoing <c>MuteUser</c> message, which mutes a user in a room for a number of minutes.</summary>
            public static readonly MessageContract<MuteRoomUserRequest> Mute =
                Flash<MuteRoomUserRequest>(MessageKeys.Room.Moderation.Mute);

            /// <summary>The contract for the outgoing <c>KickUser</c> message, which kicks a user from the current room.</summary>
            public static readonly MessageContract<KickRoomUserRequest> Kick =
                Flash<KickRoomUserRequest>(MessageKeys.Room.Moderation.Kick);

            /// <summary>The contract for the outgoing <c>BanUserWithDuration</c> message, which bans a user from a room for a duration.</summary>
            public static readonly MessageContract<BanRoomUserRequest> Ban =
                Flash<BanRoomUserRequest>(MessageKeys.Room.Moderation.Ban);

            /// <summary>The contract for the outgoing <c>UnbanUserFromRoom</c> message, which unbans a user from a room.</summary>
            public static readonly MessageContract<UnbanRoomUserRequest> Unban =
                Flash<UnbanRoomUserRequest>(MessageKeys.Room.Moderation.Unban);
        }
    }

    /// <summary>Contains the friend and messenger message contracts.</summary>
    public static class Friends
    {
        /// <summary>The contract for the outgoing <c>MessengerInit</c> message, which initializes the messenger and requests the friend list.</summary>
        public static readonly MessageContract<FriendInitializationRequest> InitializeRequest =
            Flash<FriendInitializationRequest>(MessageKeys.Friends.InitializeRequest);

        /// <summary>The contract for the incoming <c>MessengerInit</c> message, which carries the friend list limits and categories.</summary>
        public static readonly MessageContract<MessengerInit> Initialized =
            Flash<MessengerInit>(MessageKeys.Friends.Initialized);

        /// <summary>The contract for the incoming <c>FriendsListFragment</c> message, which carries one fragment of the friend list.</summary>
        public static readonly MessageContract<FriendListFragment> ListFragment =
            Flash<FriendListFragment>(MessageKeys.Friends.ListFragment);

        /// <summary>The contract for the incoming <c>FriendListUpdate</c> message, which carries changes to the friend list and its categories.</summary>
        public static readonly MessageContract<FriendListUpdate> ListUpdated =
            Flash<FriendListUpdate>(MessageKeys.Friends.ListUpdated);

        /// <summary>The contract for the outgoing <c>SendMsg</c> message, which sends a private message to a friend.</summary>
        public static readonly MessageContract<SendPrivateMessage> PrivateMessageSend =
            new(
                MessageKeys.Friends.PrivateMessageSend,
                MessageCodec<SendPrivateMessage>.FromModel());

        /// <summary>The contract for the incoming <c>NewConsoleMessage</c> message, which carries a private message received from a friend.</summary>
        public static readonly MessageContract<NewConsoleMessage> PrivateMessageReceived =
            new(
                MessageKeys.Friends.PrivateMessageReceived,
                MessageCodec<NewConsoleMessage>.FromModel());

        /// <summary>The contract for the incoming <c>MessengerError</c> message, which reports that the hotel refused a messenger operation.</summary>
        public static readonly MessageContract<MessengerError> OperationFailed =
            Flash<MessengerError>(MessageKeys.Friends.OperationFailed);

        /// <summary>The contract for the incoming <c>InstantMessageError</c> message, which reports that a private message could not be delivered.</summary>
        public static readonly MessageContract<InstantMessageError> PrivateMessageFailed =
            Flash<InstantMessageError>(MessageKeys.Friends.PrivateMessageFailed);

        /// <summary>The contract for the outgoing <c>RequestFriend</c> message, which sends a friend request to a user by name.</summary>
        public static readonly MessageContract<FriendRequest> FriendRequestSend =
            Flash<FriendRequest>(MessageKeys.Friends.FriendRequestSend);

        /// <summary>The contract for the incoming <c>NewFriendRequest</c> message, which carries a friend request sent to the local user.</summary>
        public static readonly MessageContract<NewFriendRequest> FriendRequestReceived =
            Flash<NewFriendRequest>(MessageKeys.Friends.FriendRequestReceived);

        /// <summary>The contract for the outgoing <c>GetFriendRequests</c> message, which requests the pending friend requests.</summary>
        public static readonly MessageContract<PendingFriendRequestsRequest> FriendRequestsRequest =
            Flash<PendingFriendRequestsRequest>(MessageKeys.Friends.FriendRequestsRequest);

        /// <summary>The contract for the incoming <c>FriendRequests</c> message, which carries the pending friend requests.</summary>
        public static readonly MessageContract<PendingFriendRequests> FriendRequestsSnapshot =
            Flash<PendingFriendRequests>(MessageKeys.Friends.FriendRequestsSnapshot);

        /// <summary>The contract for the outgoing <c>AcceptFriend</c> message, which accepts pending friend requests.</summary>
        public static readonly MessageContract<AcceptFriends> FriendRequestAccept =
            Flash<AcceptFriends>(MessageKeys.Friends.FriendRequestAccept);

        /// <summary>The contract for the outgoing <c>DeclineFriend</c> message, which declines selected or all pending friend requests.</summary>
        public static readonly MessageContract<DeclineFriends> FriendRequestDecline =
            Flash<DeclineFriends>(MessageKeys.Friends.FriendRequestDecline);

        /// <summary>The contract for the outgoing <c>RemoveFriend</c> message, which removes users from the friend list.</summary>
        public static readonly MessageContract<RemoveFriends> Remove =
            Flash<RemoveFriends>(MessageKeys.Friends.Remove);

        /// <summary>The contract for the outgoing <c>FollowFriend</c> message, which follows a friend to their current room.</summary>
        public static readonly MessageContract<FollowFriendRequest> Follow =
            Flash<FollowFriendRequest>(MessageKeys.Friends.Follow);

        /// <summary>The contract for the outgoing <c>HabboSearch</c> message, which searches users by name.</summary>
        public static readonly MessageContract<FriendSearchRequest> SearchRequest =
            Flash<FriendSearchRequest>(MessageKeys.Friends.SearchRequest);

        /// <summary>The contract for the incoming <c>HabboSearchResult</c> message, which carries the users that match a search, split into friends and others.</summary>
        public static readonly MessageContract<UserSearchResults> SearchResult =
            Flash<UserSearchResults>(MessageKeys.Friends.SearchResult);

        /// <summary>The contract for the outgoing <c>SetRelationshipStatus</c> message, which sets the relationship shown for a friend.</summary>
        public static readonly MessageContract<SetFriendRelationshipRequest> RelationshipSet =
            Flash<SetFriendRelationshipRequest>(MessageKeys.Friends.RelationshipSet);
    }

    /// <summary>Contains the trade message contracts.</summary>
    public static class Trade
    {
        /// <summary>The contract for the incoming <c>TradingOpen</c> message, which reports that a trade was opened between the local user and another user.</summary>
        public static readonly MessageContract<TradeOpened> Opened =
            Flash<TradeOpened>(MessageKeys.Trade.Opened);

        /// <summary>The contract for the incoming <c>TradingItemList</c> message, which carries the items both users offer in the trade.</summary>
        public static readonly MessageContract<TradeOffers> Offers =
            Flash<TradeOffers>(MessageKeys.Trade.Offers);

        /// <summary>The contract for the incoming <c>TradingAccept</c> message, which reports whether a user in the trade has accepted.</summary>
        public static readonly MessageContract<TradeAccepted> AcceptanceUpdated =
            Flash<TradeAccepted>(MessageKeys.Trade.AcceptanceUpdated);

        /// <summary>The contract for the incoming <c>TradingConfirmation</c> message, which asks both users to confirm the trade.</summary>
        public static readonly MessageContract<TradeConfirmation> Confirmation =
            Flash<TradeConfirmation>(MessageKeys.Trade.Confirmation);

        /// <summary>The contract for the incoming <c>TradingCompleted</c> message, which reports that the trade completed.</summary>
        public static readonly MessageContract<TradeCompleted> Completed =
            Flash<TradeCompleted>(MessageKeys.Trade.Completed);

        /// <summary>The contract for the incoming <c>TradingClose</c> message, which reports that the trade was closed, with the user and reason.</summary>
        public static readonly MessageContract<TradeClosed> Closed =
            Flash<TradeClosed>(MessageKeys.Trade.Closed);

        /// <summary>The contract for the incoming <c>TradeOpenFailed</c> message, which reports why a trade could not be opened.</summary>
        public static readonly MessageContract<TradeOpenFailed> OpenFailed =
            Flash<TradeOpenFailed>(MessageKeys.Trade.OpenFailed);

        /// <summary>The contract for the incoming <c>TradeNftAssets</c> message, which carries the NFT assets both users offer in the trade.</summary>
        public static readonly MessageContract<TradeNftAssets> NftOffers =
            Flash<TradeNftAssets>(MessageKeys.Trade.NftOffers);

        /// <summary>The contract for the incoming <c>TradeNftAssetInventory</c> message, which carries the local user's NFT assets available for trading.</summary>
        public static readonly MessageContract<TradeNftAssetInventory> NftInventory =
            Flash<TradeNftAssetInventory>(MessageKeys.Trade.NftInventory);

        /// <summary>The contract for the incoming <c>TradeSilverSet</c> message, which carries the silver both users offer in the trade.</summary>
        public static readonly MessageContract<TradeSilverSet> SilverUpdated =
            Flash<TradeSilverSet>(MessageKeys.Trade.SilverUpdated);

        /// <summary>The contract for the incoming <c>TradeSilverFee</c> message, which carries the silver fee of the trade.</summary>
        public static readonly MessageContract<TradeSilverFee> SilverFee =
            Flash<TradeSilverFee>(MessageKeys.Trade.SilverFee);

        /// <summary>The contract for the outgoing <c>OpenTrading</c> message, which opens a trade with an avatar in the room.</summary>
        public static readonly MessageContract<OpenTradeRequest> OpenRequest =
            Flash<OpenTradeRequest>(MessageKeys.Trade.OpenRequest);

        /// <summary>The contract for the outgoing <c>AddItemsToTrade</c> message, which adds inventory items to the trade offer.</summary>
        public static readonly MessageContract<AddTradeItemsRequest> ItemsAdd =
            Flash<AddTradeItemsRequest>(MessageKeys.Trade.ItemsAdd);

        /// <summary>The contract for the outgoing <c>RemoveItemFromTrade</c> message, which removes an item from the trade offer.</summary>
        public static readonly MessageContract<RemoveTradeItemRequest> ItemRemove =
            Flash<RemoveTradeItemRequest>(MessageKeys.Trade.ItemRemove);

        /// <summary>The contract for the outgoing <c>AcceptTrading</c> message, which accepts the current trade offer.</summary>
        public static readonly MessageContract<AcceptTradeRequest> Accept =
            Flash<AcceptTradeRequest>(MessageKeys.Trade.Accept);

        /// <summary>The contract for the outgoing <c>UnacceptTrading</c> message, which withdraws acceptance of the trade.</summary>
        public static readonly MessageContract<UnacceptTradeRequest> Unaccept =
            Flash<UnacceptTradeRequest>(MessageKeys.Trade.Unaccept);

        /// <summary>The contract for the outgoing <c>ConfirmAcceptTrading</c> message, which confirms the trade.</summary>
        public static readonly MessageContract<ConfirmTradeRequest> Confirm =
            Flash<ConfirmTradeRequest>(MessageKeys.Trade.Confirm);

        /// <summary>The contract for the outgoing <c>CloseTrading</c> message, which closes the trade.</summary>
        public static readonly MessageContract<CloseTradeRequest> Close =
            Flash<CloseTradeRequest>(MessageKeys.Trade.Close);

        /// <summary>The contract for the outgoing <c>GetNftTradeInventory</c> message, which requests the local user's NFT assets available for trading.</summary>
        public static readonly MessageContract<GetNftTradeInventoryRequest> NftInventoryRequest =
            Flash<GetNftTradeInventoryRequest>(MessageKeys.Trade.NftInventoryRequest);
    }

    /// <summary>Contains the user and account message contracts.</summary>
    public static class Users
    {
        /// <summary>Contains the relationship status message contracts.</summary>
        public static class Relationship
        {
            /// <summary>The contract for the outgoing <c>GetRelationshipStatusInfo</c> message, which requests the relationship summary of a user.</summary>
            public static readonly MessageContract<RelationshipStatusRequest> Request =
                Flash<RelationshipStatusRequest>(MessageKeys.Users.Relationship.Request);

            /// <summary>The contract for the incoming <c>RelationshipStatusInfo</c> message, which carries the relationship summary of a user.</summary>
            public static readonly MessageContract<RelationshipStatus> Snapshot =
                Flash<RelationshipStatus>(MessageKeys.Users.Relationship.Snapshot);
        }

        /// <summary>Contains the block list message contracts.</summary>
        public static class Block
        {
            /// <summary>The contract for the outgoing <c>BlockListInit</c> message, which requests the local user's block list.</summary>
            public static readonly MessageContract<BlockListRequest> ListRequest =
                Flash<BlockListRequest>(MessageKeys.Users.Block.ListRequest);

            /// <summary>The contract for the incoming <c>BlockList</c> message, which carries the local user's block list.</summary>
            public static readonly MessageContract<BlockList> ListSnapshot =
                Flash<BlockList>(MessageKeys.Users.Block.ListSnapshot);

            /// <summary>The contract for the incoming <c>BlockUserUpdate</c> message, which carries the result of blocking or unblocking a user.</summary>
            public static readonly MessageContract<BlockUserUpdate> Updated =
                Flash<BlockUserUpdate>(MessageKeys.Users.Block.Updated);

            /// <summary>The contract for the outgoing <c>BlockUser</c> message, which blocks a user.</summary>
            public static readonly MessageContract<BlockUserRequest> Add =
                Flash<BlockUserRequest>(MessageKeys.Users.Block.Add);

            /// <summary>The contract for the outgoing <c>UnblockUser</c> message, which unblocks a user.</summary>
            public static readonly MessageContract<UnblockUserRequest> Remove =
                Flash<UnblockUserRequest>(MessageKeys.Users.Block.Remove);
        }

        /// <summary>Contains the ignore list message contracts.</summary>
        public static class Ignore
        {
            /// <summary>The contract for the outgoing <c>GetIgnoredUsers</c> message, which requests the local user's ignore list.</summary>
            public static readonly MessageContract<IgnoreListRequest> ListRequest =
                Flash<IgnoreListRequest>(MessageKeys.Users.Ignore.ListRequest);

            /// <summary>The contract for the incoming <c>IgnoredUsers</c> message, which carries the local user's ignore list.</summary>
            public static readonly MessageContract<IgnoredUsers> ListSnapshot =
                Flash<IgnoredUsers>(MessageKeys.Users.Ignore.ListSnapshot);

            /// <summary>The contract for the incoming <c>IgnoreResult</c> message, which carries the result of ignoring or unignoring a user.</summary>
            public static readonly MessageContract<IgnoreUserResult> Updated =
                Flash<IgnoreUserResult>(MessageKeys.Users.Ignore.Updated);

            /// <summary>The contract for the outgoing <c>IgnoreUser</c> message, which ignores a user by id.</summary>
            public static readonly MessageContract<IgnoreUserByIdRequest> AddByIdRequest =
                Flash<IgnoreUserByIdRequest>(MessageKeys.Users.Ignore.AddByIdRequest);

            /// <summary>The contract for the outgoing <c>UnignoreUser</c> message, which stops ignoring a user.</summary>
            /// <remarks>The contract always reports the <c>flashUnignoreIdSchema</c> capability as available.</remarks>
            public static readonly MessageContract<UnignoreUserRequest> Remove =
                new(
                    MessageKeys.Users.Ignore.Remove,
                    MessageCodec<UnignoreUserRequest>.FromModel(static (_, _) => MessageCapability.Ready("flashUnignoreIdSchema")));
        }

        /// <summary>Contains the figure set message contracts.</summary>
        public static class FigureSets
        {
            /// <summary>The contract for the incoming <c>FigureSetIdAdded</c> message, which reports a figure set the local user now owns.</summary>
            public static readonly MessageContract<FigureSetIdAdded> Added =
                Flash<FigureSetIdAdded>(MessageKeys.Users.FigureSets.Added);

            /// <summary>The contract for the incoming <c>FigureSetIdRemoved</c> message, which reports a figure set the local user no longer owns.</summary>
            public static readonly MessageContract<FigureSetIdRemoved> Removed =
                Flash<FigureSetIdRemoved>(MessageKeys.Users.FigureSets.Removed);

            /// <summary>The contract for the incoming <c>FigureSetIds</c> message, which carries the figure sets the local user owns.</summary>
            public static readonly MessageContract<FigureSetIds> Snapshot =
                Flash<FigureSetIds>(MessageKeys.Users.FigureSets.Snapshot);
        }

        /// <summary>Contains the sanction status message contracts.</summary>
        public static class Sanctions
        {
            /// <summary>The contract for the outgoing <c>GetMySanctionStatus</c> message, which requests the local user's sanction status.</summary>
            public static readonly MessageContract<SanctionStatusRequest> Request =
                Flash<SanctionStatusRequest>(MessageKeys.Users.Sanctions.Request);

            /// <summary>The contract for the incoming <c>SanctionStatus</c> message, which carries the local user's sanction status.</summary>
            public static readonly MessageContract<AccountSanctionStatus> Snapshot =
                Flash<AccountSanctionStatus>(MessageKeys.Users.Sanctions.Snapshot);
        }

        /// <summary>Contains the favorite group message contracts.</summary>
        public static class FavoriteGroup
        {
            /// <summary>The contract for the outgoing <c>SelectFavouriteHabboGroup</c> message, which selects the local user's favorite group.</summary>
            public static readonly MessageContract<SelectFavoriteGroupRequest> Select =
                Flash<SelectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Select);

            /// <summary>The contract for the outgoing <c>DeselectFavouriteHabboGroup</c> message, which deselects the local user's favorite group.</summary>
            public static readonly MessageContract<DeselectFavoriteGroupRequest> Deselect =
                Flash<DeselectFavoriteGroupRequest>(MessageKeys.Users.FavoriteGroup.Deselect);
        }

        /// <summary>The contract for the outgoing <c>ChangeMotto</c> message, which changes the local user's motto.</summary>
        public static readonly MessageContract<MottoUpdateRequest> MottoUpdate =
            Flash<MottoUpdateRequest>(MessageKeys.Users.MottoUpdate);

        /// <summary>The contract for the outgoing <c>InfoRetrieve</c> message, which requests the local user's account data.</summary>
        public static readonly MessageContract<ProfileRequest> ProfileRequest =
            Flash<ProfileRequest>(MessageKeys.Users.ProfileRequest);

        /// <summary>The contract for the incoming <c>UserObject</c> message, which carries the local user's account data.</summary>
        public static readonly MessageContract<UserData> ProfileSnapshot =
            Flash<UserData>(MessageKeys.Users.ProfileSnapshot);

        /// <summary>The contract for the incoming <c>FigureUpdate</c> message, which carries the local user's new figure and gender.</summary>
        public static readonly MessageContract<FigureUpdate> FigureUpdated =
            Flash<FigureUpdate>(MessageKeys.Users.FigureUpdated);

        /// <summary>The contract for the incoming <c>ChangeUserNameResult</c> message, which carries the result of a name change and name suggestions.</summary>
        public static readonly MessageContract<ChangeUserNameResult> NameChangeResult =
            Flash<ChangeUserNameResult>(MessageKeys.Users.NameChangeResult);

        /// <summary>The contract for the incoming <c>AccountSafetyLockStatusChange</c> message, which reports a change to the account's safety lock status.</summary>
        public static readonly MessageContract<AccountSafetyLockStatusChange> SafetyLockChanged =
            Flash<AccountSafetyLockStatusChange>(MessageKeys.Users.SafetyLockChanged);

        /// <summary>The contract for the outgoing <c>GetExtendedProfile</c> message, which requests the profile of a user.</summary>
        public static readonly MessageContract<ExtendedProfileRequest> ExtendedProfileRequest =
            Flash<ExtendedProfileRequest>(MessageKeys.Users.ExtendedProfileRequest);

        /// <summary>The contract for the incoming <c>ExtendedProfile</c> message, which carries the profile of a user.</summary>
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
        IMessageResolver messages,
        Header header)
    {
        MessageWireProfile profile = messages.GetWireProfile();
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
        IMessageResolver messages,
        Header header)
    {
        MessageCapability layout =
            FlashMarketplaceLayoutCapability(messages, header);
        if (!layout.Available)
            return layout;
        return messages.GetWireProfile().FlashMarketplaceLayout is
            FlashMarketplaceWireLayout.Modern
                ? MessageCapability.Ready("flashMarketplaceModernLayout")
                : MessageCapability.Missing(
                    "flashMarketplaceModernLayout",
                    "The active Flash build uses the legacy marketplace layout.");
    }
}
