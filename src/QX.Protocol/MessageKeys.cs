namespace Qx.Protocol;

/// <summary>Contains the stable message keys declared in <c>Resources/messages.ini</c>, grouped by feature.</summary>
public static class MessageKeys
{
    /// <summary>Contains the error message keys.</summary>
    public static class Errors
    {
        /// <summary>The <c>errors.generic</c> key of the incoming Flash message <c>GenericError</c>.</summary>
        public static readonly MessageKey Generic = new("errors.generic");
    }

    /// <summary>Contains the session message keys.</summary>
    public static class Session
    {
        /// <summary>The <c>session.disconnect.reason</c> key of the incoming Flash message <c>DisconnectReason</c>.</summary>
        public static readonly MessageKey DisconnectReason = new("session.disconnect.reason");
    }

    /// <summary>Contains the achievement message keys.</summary>
    public static class Achievements
    {
        /// <summary>The <c>achievements.request</c> key of the outgoing Flash message <c>GetAchievements</c>.</summary>
        public static readonly MessageKey Request = new("achievements.request");
        /// <summary>The <c>achievements.snapshot</c> key of the incoming Flash message <c>Achievements</c>.</summary>
        public static readonly MessageKey Snapshot = new("achievements.snapshot");
        /// <summary>The <c>achievement.updated</c> key of the incoming Flash message <c>Achievement</c>.</summary>
        public static readonly MessageKey Updated = new("achievement.updated");
        /// <summary>The <c>achievement.score</c> key of the incoming Flash message <c>AchievementsScore</c>.</summary>
        public static readonly MessageKey Score = new("achievement.score");
        /// <summary>The <c>achievement.point_limits.request</c> key of the outgoing Flash message <c>GetBadgePointLimits</c>.</summary>
        public static readonly MessageKey PointLimitsRequest = new("achievement.point_limits.request");
        /// <summary>The <c>achievement.point_limits</c> key of the incoming Flash message <c>BadgePointLimits</c>.</summary>
        public static readonly MessageKey PointLimits = new("achievement.point_limits");
        /// <summary>The <c>achievement.notification</c> key of the incoming Flash message <c>HabboAchievementNotification</c>.</summary>
        public static readonly MessageKey Notification = new("achievement.notification");
    }

    /// <summary>Contains the badge message keys.</summary>
    public static class Badges
    {
        /// <summary>The <c>badges.request</c> key of the outgoing Flash message <c>GetBadges</c>.</summary>
        public static readonly MessageKey Request = new("badges.request");
        /// <summary>The <c>badges.snapshot</c> key of the incoming Flash message <c>Badges</c>.</summary>
        public static readonly MessageKey Snapshot = new("badges.snapshot");
        /// <summary>The <c>badges.selected.request</c> key of the outgoing Flash message <c>GetSelectedBadges</c>.</summary>
        public static readonly MessageKey SelectedRequest = new("badges.selected.request");
        /// <summary>The <c>badge.received</c> key of the incoming Flash message <c>BadgeReceived</c>.</summary>
        public static readonly MessageKey Received = new("badge.received");
        /// <summary>The <c>badge.selected</c> key of the incoming Flash message <c>HabboUserBadges</c>.</summary>
        public static readonly MessageKey Selected = new("badge.selected");
    }

    /// <summary>Contains the wallet message keys.</summary>
    public static class Wallet
    {
        /// <summary>The <c>wallet.credits.request</c> key of the outgoing Flash message <c>GetCreditsInfo</c>.</summary>
        public static readonly MessageKey CreditsRequest = new("wallet.credits.request");
        /// <summary>The <c>wallet.credits.balance</c> key of the incoming Flash message <c>CreditBalance</c>.</summary>
        public static readonly MessageKey CreditsBalance = new("wallet.credits.balance");
        /// <summary>The <c>wallet.activity_points</c> key of the incoming Flash message <c>ActivityPoints</c>.</summary>
        public static readonly MessageKey ActivityPoints = new("wallet.activity_points");
        /// <summary>The <c>wallet.activity_point.updated</c> key of the incoming Flash message <c>HabboActivityPointNotification</c>.</summary>
        public static readonly MessageKey ActivityPointUpdated = new("wallet.activity_point.updated");
    }

    /// <summary>Contains the earnings message keys.</summary>
    public static class Earnings
    {
        /// <summary>The <c>earnings.status.request</c> key of the outgoing Flash message <c>IncomeRewardStatus</c>.</summary>
        public static readonly MessageKey StatusRequest = new("earnings.status.request");
        /// <summary>The <c>earnings.status.snapshot</c> key of the incoming Flash message <c>IncomeRewardStatus</c>.</summary>
        public static readonly MessageKey StatusSnapshot = new("earnings.status.snapshot");
        /// <summary>The <c>earnings.claim</c> key of the outgoing Flash message <c>IncomeRewardClaim</c>.</summary>
        public static readonly MessageKey Claim = new("earnings.claim");
        /// <summary>The <c>earnings.claimed</c> key of the incoming Flash message <c>IncomeRewardClaimResponse</c>.</summary>
        public static readonly MessageKey Claimed = new("earnings.claimed");
        /// <summary>The <c>earnings.notification</c> key of the incoming Flash message <c>IncomeRewardNotification</c>.</summary>
        public static readonly MessageKey Notification = new("earnings.notification");
    }

    /// <summary>Contains the subscription and Builders Club message keys.</summary>
    public static class Subscriptions
    {
        /// <summary>The <c>subscriptions.user_info</c> key of the incoming Flash message <c>ScrSendUserInfo</c>.</summary>
        public static readonly MessageKey UserInfo = new("subscriptions.user_info");
        /// <summary>The <c>subscriptions.user_info.request</c> key of the outgoing Flash message <c>ScrGetUserInfo</c>.</summary>
        public static readonly MessageKey UserInfoRequest = new("subscriptions.user_info.request");
        /// <summary>The <c>subscriptions.kickback_info</c> key of the incoming Flash message <c>ScrSendKickbackInfo</c>.</summary>
        public static readonly MessageKey KickbackInfo = new("subscriptions.kickback_info");
        /// <summary>The <c>subscriptions.kickback_info.request</c> key of the outgoing Flash message <c>ScrGetKickbackInfo</c>.</summary>
        public static readonly MessageKey KickbackInfoRequest = new("subscriptions.kickback_info.request");
        /// <summary>The <c>subscriptions.club_offers.snapshot</c> key of the incoming Flash message <c>HabboClubOffers</c>.</summary>
        public static readonly MessageKey ClubOffersSnapshot = new("subscriptions.club_offers.snapshot");
        /// <summary>The <c>subscriptions.club_offers.request</c> key of the outgoing Flash message <c>GetClubOffers</c>.</summary>
        public static readonly MessageKey ClubOffersRequest = new("subscriptions.club_offers.request");
        /// <summary>The <c>subscriptions.builders_club.furni_count</c> key of the incoming Flash message <c>BuildersClubFurniCount</c>.</summary>
        public static readonly MessageKey BuildersClubFurniCount = new("subscriptions.builders_club.furni_count");
        /// <summary>The <c>subscriptions.builders_club.furni_count.request</c> key of the outgoing Flash message <c>BuildersClubQueryFurniCount</c>.</summary>
        public static readonly MessageKey BuildersClubFurniCountRequest = new("subscriptions.builders_club.furni_count.request");
        /// <summary>The <c>subscriptions.builders_club.membership_status</c> key of the incoming Flash message <c>BuildersClubSubscriptionStatus</c>.</summary>
        public static readonly MessageKey BuildersClubMembershipStatus = new("subscriptions.builders_club.membership_status");
        /// <summary>The <c>subscriptions.builders_club.placement_warning</c> key of the incoming Flash message <c>BuildersClubPlacementWarning</c>.</summary>
        public static readonly MessageKey BuildersClubPlacementWarning = new("subscriptions.builders_club.placement_warning");
        /// <summary>The <c>subscriptions.builders_club.floor_offer.place</c> key of the outgoing Flash message <c>BuildersClubPlaceRoomItem</c>.</summary>
        public static readonly MessageKey BuildersClubFloorOfferPlace = new("subscriptions.builders_club.floor_offer.place");
        /// <summary>The <c>subscriptions.builders_club.wall_offer.place</c> key of the outgoing Flash message <c>BuildersClubPlaceWallItem</c>.</summary>
        public static readonly MessageKey BuildersClubWallOfferPlace = new("subscriptions.builders_club.wall_offer.place");
    }

    /// <summary>Contains the crafting message keys.</summary>
    public static class Crafting
    {
        /// <summary>The <c>crafting.products.request</c> key of the outgoing Flash message <c>GetCraftableProducts</c>.</summary>
        public static readonly MessageKey ProductsRequest = new("crafting.products.request");
        /// <summary>The <c>crafting.products.snapshot</c> key of the incoming Flash message <c>CraftableProducts</c>.</summary>
        public static readonly MessageKey ProductsSnapshot = new("crafting.products.snapshot");
        /// <summary>The <c>crafting.recipe.request</c> key of the outgoing Flash message <c>GetCraftingRecipe</c>.</summary>
        public static readonly MessageKey RecipeRequest = new("crafting.recipe.request");
        /// <summary>The <c>crafting.recipe.snapshot</c> key of the incoming Flash message <c>CraftingRecipe</c>.</summary>
        public static readonly MessageKey RecipeSnapshot = new("crafting.recipe.snapshot");
        /// <summary>The <c>crafting.craft</c> key of the outgoing Flash message <c>Craft</c>.</summary>
        public static readonly MessageKey Craft = new("crafting.craft");
        /// <summary>The <c>crafting.secret_craft</c> key of the outgoing Flash message <c>CraftSecret</c>.</summary>
        public static readonly MessageKey SecretCraft = new("crafting.secret_craft");
        /// <summary>The <c>crafting.availability.request</c> key of the outgoing Flash message <c>GetCraftingRecipesAvailable</c>.</summary>
        public static readonly MessageKey AvailabilityRequest = new("crafting.availability.request");
        /// <summary>The <c>crafting.availability.snapshot</c> key of the incoming Flash message <c>CraftingRecipesAvailable</c>.</summary>
        public static readonly MessageKey AvailabilitySnapshot = new("crafting.availability.snapshot");
        /// <summary>The <c>crafting.result</c> key of the incoming Flash message <c>CraftingResult</c>.</summary>
        public static readonly MessageKey Result = new("crafting.result");
    }

    /// <summary>Contains the recycler message keys.</summary>
    public static class Recycler
    {
        /// <summary>The <c>recycler.status</c> key of the incoming Flash message <c>RecyclerStatus</c>.</summary>
        public static readonly MessageKey Status = new("recycler.status");
        /// <summary>The <c>recycler.finished</c> key of the incoming Flash message <c>RecyclerFinished</c>.</summary>
        public static readonly MessageKey Finished = new("recycler.finished");
    }

    /// <summary>Contains the wired message keys.</summary>
    public static class Wired
    {
        /// <summary>Contains Wired Account messages.</summary>
        public static class Account
        {
            /// <summary>The AccountPreferences message key.</summary>
            public static readonly MessageKey Preferences = new("wired.account.preferences");
        }

        /// <summary>Contains Wired WebApi messages.</summary>
        public static class WebApi
        {
            /// <summary>The WiredGenerateWebApiKey message key.</summary>
            public static readonly MessageKey KeyGenerate = new("wired.web_api.key.generate");
            /// <summary>The WiredWebApiKeyResult message key.</summary>
            public static readonly MessageKey KeyResult = new("wired.web_api.key.result");
        }

        /// <summary>Contains the wired state message keys.</summary>
        public static class State
        {
            /// <summary>The <c>wired.permissions</c> key of the incoming Flash message <c>WiredPermissions</c>.</summary>
            public static readonly MessageKey Permissions = new("wired.permissions");
            /// <summary>The <c>wired.environment</c> key of the incoming Flash message <c>WiredEnvironment</c>.</summary>
            public static readonly MessageKey Environment = new("wired.environment");
            /// <summary>The <c>wired.click_settings</c> key of the incoming Flash message <c>WiredClickSettings</c>.</summary>
            public static readonly MessageKey ClickSettings = new("wired.click_settings");
            /// <summary>The <c>wired.menu.error</c> key of the incoming Flash message <c>WiredMenuError</c>.</summary>
            public static readonly MessageKey MenuError = new("wired.menu.error");
            /// <summary>The <c>wired.reward.result</c> key of the incoming Flash message <c>WiredRewardResult</c>.</summary>
            public static readonly MessageKey RewardResult = new("wired.reward.result");
        }

        /// <summary>Contains the wired configuration message keys.</summary>
        public static class Configuration
        {
            /// <summary>The <c>wired.configuration.opened</c> key of the incoming Flash message <c>Open</c>.</summary>
            public static readonly MessageKey Opened = new("wired.configuration.opened");
            /// <summary>The <c>wired.configuration.open.request</c> key of the outgoing Flash message <c>Open</c>.</summary>
            public static readonly MessageKey OpenRequest = new("wired.configuration.open.request");
            /// <summary>The <c>wired.configuration.snapshot.apply</c> key of the outgoing Flash message <c>ApplySnapshot</c>.</summary>
            public static readonly MessageKey ApplySnapshot = new("wired.configuration.snapshot.apply");
            /// <summary>The <c>wired.configuration.trigger</c> key of the incoming Flash message <c>WiredFurniTrigger</c>.</summary>
            public static readonly MessageKey Trigger = new("wired.configuration.trigger");
            /// <summary>The <c>wired.configuration.action</c> key of the incoming Flash message <c>WiredFurniAction</c>.</summary>
            public static readonly MessageKey Action = new("wired.configuration.action");
            /// <summary>The <c>wired.configuration.condition</c> key of the incoming Flash message <c>WiredFurniCondition</c>.</summary>
            public static readonly MessageKey Condition = new("wired.configuration.condition");
            /// <summary>The <c>wired.configuration.selector</c> key of the incoming Flash message <c>WiredFurniSelector</c>.</summary>
            public static readonly MessageKey Selector = new("wired.configuration.selector");
            /// <summary>The <c>wired.configuration.addon</c> key of the incoming Flash message <c>WiredFurniAddon</c>.</summary>
            public static readonly MessageKey Addon = new("wired.configuration.addon");
            /// <summary>The <c>wired.configuration.variable</c> key of the incoming Flash message <c>WiredFurniVariable</c>.</summary>
            public static readonly MessageKey Variable = new("wired.configuration.variable");
            /// <summary>The <c>wired.configuration.trigger.update</c> key of the outgoing Flash message <c>UpdateTrigger</c>.</summary>
            public static readonly MessageKey TriggerUpdate = new("wired.configuration.trigger.update");
            /// <summary>The <c>wired.configuration.action.update</c> key of the outgoing Flash message <c>UpdateAction</c>.</summary>
            public static readonly MessageKey ActionUpdate = new("wired.configuration.action.update");
            /// <summary>The <c>wired.configuration.condition.update</c> key of the outgoing Flash message <c>UpdateCondition</c>.</summary>
            public static readonly MessageKey ConditionUpdate = new("wired.configuration.condition.update");
            /// <summary>The <c>wired.configuration.selector.update</c> key of the outgoing Flash message <c>UpdateSelector</c>.</summary>
            public static readonly MessageKey SelectorUpdate = new("wired.configuration.selector.update");
            /// <summary>The <c>wired.configuration.addon.update</c> key of the outgoing Flash message <c>UpdateAddon</c>.</summary>
            public static readonly MessageKey AddonUpdate = new("wired.configuration.addon.update");
            /// <summary>The <c>wired.configuration.variable.update</c> key of the outgoing Flash message <c>UpdateVariable</c>.</summary>
            public static readonly MessageKey VariableUpdate = new("wired.configuration.variable.update");
            /// <summary>The <c>wired.configuration.save.succeeded</c> key of the incoming Flash message <c>WiredSaveSuccess</c>.</summary>
            public static readonly MessageKey SaveSucceeded = new("wired.configuration.save.succeeded");
            /// <summary>The <c>wired.configuration.validation.failed</c> key of the incoming Flash message <c>WiredValidationError</c>.</summary>
            public static readonly MessageKey ValidationFailed = new("wired.configuration.validation.failed");
        }

        /// <summary>Contains the wired room message keys.</summary>
        public static class Room
        {
            /// <summary>The <c>wired.room.settings.request</c> key of the outgoing Flash message <c>WiredGetRoomSettings</c>.</summary>
            public static readonly MessageKey SettingsRequest = new("wired.room.settings.request");
            /// <summary>The <c>wired.room.settings</c> key of the incoming Flash message <c>WiredRoomSettings</c>.</summary>
            public static readonly MessageKey Settings = new("wired.room.settings");
            /// <summary>The <c>wired.room.settings.update</c> key of the outgoing Flash message <c>WiredSetRoomSettings</c>.</summary>
            public static readonly MessageKey SettingsUpdate = new("wired.room.settings.update");
            /// <summary>The <c>wired.room.stats.request</c> key of the outgoing Flash message <c>WiredGetRoomStats</c>.</summary>
            public static readonly MessageKey StatsRequest = new("wired.room.stats.request");
            /// <summary>The <c>wired.room.stats</c> key of the incoming Flash message <c>WiredRoomStats</c>.</summary>
            public static readonly MessageKey Stats = new("wired.room.stats");
            /// <summary>The <c>wired.room.logs.request</c> key of the outgoing Flash message <c>WiredGetRoomLogs</c>.</summary>
            public static readonly MessageKey LogsRequest = new("wired.room.logs.request");
            /// <summary>The <c>wired.room.logs</c> key of the incoming Flash message <c>WiredRoomLogs</c>.</summary>
            public static readonly MessageKey Logs = new("wired.room.logs");
            /// <summary>The <c>wired.room.update</c> key of the outgoing Flash message <c>WiredUpdateRoom</c>.</summary>
            public static readonly MessageKey Update = new("wired.room.update");
            /// <summary>The <c>wired.preferences.update</c> key of the outgoing Flash message <c>WiredSetPreferences</c>.</summary>
            public static readonly MessageKey PreferencesUpdate = new("wired.preferences.update");
        }

        /// <summary>Contains the wired error log message keys.</summary>
        public static class ErrorLogs
        {
            /// <summary>The <c>wired.error_logs.request</c> key of the outgoing Flash message <c>WiredGetErrorLogs</c>.</summary>
            public static readonly MessageKey Request = new("wired.error_logs.request");
            /// <summary>The <c>wired.error_logs</c> key of the incoming Flash message <c>WiredErrorLogs</c>.</summary>
            public static readonly MessageKey Snapshot = new("wired.error_logs");
            /// <summary>The <c>wired.error_logs.clear</c> key of the outgoing Flash message <c>WiredClearErrorLogs</c>.</summary>
            public static readonly MessageKey Clear = new("wired.error_logs.clear");
        }

        /// <summary>Contains the wired user click message keys.</summary>
        public static class UserClick
        {
            /// <summary>The <c>wired.user_click.request</c> key of the outgoing Flash message <c>WiredClickUser</c>.</summary>
            public static readonly MessageKey Request = new("wired.user_click.request");
            /// <summary>The <c>wired.user_click.result</c> key of the incoming Flash message <c>WiredClickUserResponse</c>.</summary>
            public static readonly MessageKey Result = new("wired.user_click.result");
        }

        /// <summary>Contains the wired variable message keys.</summary>
        public static class Variables
        {
            /// <summary>The <c>wired.variables.hash.request</c> key of the outgoing Flash message <c>WiredGetAllVariablesHash</c>.</summary>
            public static readonly MessageKey HashRequest = new("wired.variables.hash.request");
            /// <summary>The <c>wired.variables.hash</c> key of the incoming Flash message <c>WiredAllVariablesHash</c>.</summary>
            public static readonly MessageKey Hash = new("wired.variables.hash");
            /// <summary>The <c>wired.variables.differences.request</c> key of the outgoing Flash message <c>WiredGetAllVariablesDiffs</c>.</summary>
            public static readonly MessageKey DifferencesRequest = new("wired.variables.differences.request");
            /// <summary>The <c>wired.variables.differences</c> key of the incoming Flash message <c>WiredAllVariablesDiffs</c>.</summary>
            public static readonly MessageKey Differences = new("wired.variables.differences");
            /// <summary>The <c>wired.variables.object.request</c> key of the outgoing Flash message <c>WiredGetVariablesForObject</c>.</summary>
            public static readonly MessageKey ObjectRequest = new("wired.variables.object.request");
            /// <summary>The <c>wired.variables.object</c> key of the incoming Flash message <c>WiredVariablesForObject</c>.</summary>
            public static readonly MessageKey Object = new("wired.variables.object");
            /// <summary>The <c>wired.variables.holders.request</c> key of the outgoing Flash message <c>WiredGetAllVariableHolders</c>.</summary>
            public static readonly MessageKey HoldersRequest = new("wired.variables.holders.request");
            /// <summary>The <c>wired.variables.holders</c> key of the incoming Flash message <c>WiredAllVariableHolders</c>.</summary>
            public static readonly MessageKey Holders = new("wired.variables.holders");
            /// <summary>The <c>wired.variables.holders.delete</c> key of the outgoing Flash message <c>WiredDeleteAllVariableHolders</c>.</summary>
            public static readonly MessageKey HoldersDelete = new("wired.variables.holders.delete");
            /// <summary>The <c>wired.variables.permanent.request</c> key of the outgoing Flash message <c>WiredGetUserPermanentVariables</c>.</summary>
            public static readonly MessageKey PermanentRequest = new("wired.variables.permanent.request");
            /// <summary>The <c>wired.variables.permanent</c> key of the incoming Flash message <c>WiredUserPermanentVariables</c>.</summary>
            public static readonly MessageKey Permanent = new("wired.variables.permanent");
            /// <summary>The <c>wired.variables.owners.request</c> key of the outgoing Flash message <c>WiredGetVariableOwnersPage</c>.</summary>
            public static readonly MessageKey OwnersRequest = new("wired.variables.owners.request");
            /// <summary>The <c>wired.variables.owners</c> key of the incoming Flash message <c>WiredUserVariablesList</c>.</summary>
            public static readonly MessageKey Owners = new("wired.variables.owners");
            /// <summary>The <c>wired.variables.object_value.set</c> key of the outgoing Flash message <c>WiredSetObjectVariableValue</c>.</summary>
            public static readonly MessageKey ObjectValueSet = new("wired.variables.object_value.set");
            /// <summary>The <c>wired.variables.permanent_value.set</c> key of the outgoing Flash message <c>WiredSetUserPermanentVariable</c>.</summary>
            public static readonly MessageKey PermanentValueSet = new("wired.variables.permanent_value.set");
            /// <summary>The <c>wired.variables.permanent_value.set.result</c> key of the incoming Flash message <c>WiredSetUserPermanentVariableResult</c>.</summary>
            public static readonly MessageKey PermanentValueSetResult = new("wired.variables.permanent_value.set.result");
        }

        /// <summary>Contains the wired variable FX message keys.</summary>
        public static class VariableFx
        {
            /// <summary>The <c>wired.variable_fx.configs</c> key of the incoming Flash message <c>VariableFxConfigs</c>.</summary>
            public static readonly MessageKey Configs = new("wired.variable_fx.configs");
            /// <summary>The <c>wired.variable_fx.configs.removed</c> key of the incoming Flash message <c>VariableFxConfigsRemoved</c>.</summary>
            public static readonly MessageKey ConfigsRemoved = new("wired.variable_fx.configs.removed");
            /// <summary>The <c>wired.variable_fx.statuses</c> key of the incoming Flash message <c>VariableFxStatus</c>.</summary>
            public static readonly MessageKey Statuses = new("wired.variable_fx.statuses");
            /// <summary>The <c>wired.variable_fx.statuses.removed</c> key of the incoming Flash message <c>VariableFxStatusRemoved</c>.</summary>
            public static readonly MessageKey StatusesRemoved = new("wired.variable_fx.statuses.removed");
        }

        /// <summary>Contains the wired chest message keys.</summary>
        public static class Chests
        {
            /// <summary>The <c>wired.chest.opened</c> key of the incoming Flash message <c>OpenChest</c>.</summary>
            public static readonly MessageKey Opened = new("wired.chest.opened");
            /// <summary>The <c>wired.chest.coins</c> key of the incoming Flash message <c>CoinsChestContents</c>.</summary>
            public static readonly MessageKey Coins = new("wired.chest.coins");
            /// <summary>The <c>wired.chest.items.chunk</c> key of the incoming Flash message <c>ItemsChestContentsChunk</c>.</summary>
            public static readonly MessageKey ItemsChunk = new("wired.chest.items.chunk");
            /// <summary>The <c>wired.chest.items.updated</c> key of the incoming Flash message <c>ItemsChestContentsUpdated</c>.</summary>
            public static readonly MessageKey ItemsUpdated = new("wired.chest.items.updated");
            /// <summary>The <c>wired.chest.upgrade.result</c> key of the incoming Flash message <c>UpgradeChestResult</c>.</summary>
            public static readonly MessageKey UpgradeResult = new("wired.chest.upgrade.result");
            /// <summary>The <c>wired.chest.preferences.updated</c> key of the incoming Flash message <c>ChestPreferencesUpdateSuccess</c>.</summary>
            public static readonly MessageKey PreferencesUpdated = new("wired.chest.preferences.updated");
            /// <summary>The <c>wired.chest.open.request</c> key of the outgoing Flash message <c>OpenChestAndGetContents</c>.</summary>
            public static readonly MessageKey OpenRequest = new("wired.chest.open.request");
            /// <summary>The <c>wired.chest.close</c> key of the outgoing Flash message <c>CloseChest</c>.</summary>
            public static readonly MessageKey Close = new("wired.chest.close");
            /// <summary>The <c>wired.chests.lock</c> key of the outgoing Flash message <c>LockAllChests</c>.</summary>
            public static readonly MessageKey LockAll = new("wired.chests.lock");
            /// <summary>The <c>wired.chest.upgrade</c> key of the outgoing Flash message <c>UpgradeChest</c>.</summary>
            public static readonly MessageKey Upgrade = new("wired.chest.upgrade");
            /// <summary>The <c>wired.chest.withdraw.all</c> key of the outgoing Flash message <c>WithdrawAllFromChest</c>.</summary>
            public static readonly MessageKey WithdrawAll = new("wired.chest.withdraw.all");
            /// <summary>The <c>wired.chest.withdraw.coins</c> key of the outgoing Flash message <c>WithdrawCoinsFromChest</c>.</summary>
            public static readonly MessageKey WithdrawCoins = new("wired.chest.withdraw.coins");
            /// <summary>The <c>wired.chest.withdraw.items</c> key of the outgoing Flash message <c>WithdrawItemsFromChest</c>.</summary>
            public static readonly MessageKey WithdrawItems = new("wired.chest.withdraw.items");
            /// <summary>The <c>wired.chest.add.start</c> key of the outgoing Flash message <c>StartAddingToChest</c>.</summary>
            public static readonly MessageKey StartAdding = new("wired.chest.add.start");
            /// <summary>The <c>wired.chest.options.update</c> key of the outgoing Flash message <c>SetChestOptions</c>.</summary>
            public static readonly MessageKey OptionsUpdate = new("wired.chest.options.update");
            /// <summary>The <c>wired.chest.preferences.update</c> key of the outgoing Flash message <c>SetChestPreferences</c>.</summary>
            public static readonly MessageKey PreferencesUpdate = new("wired.chest.preferences.update");
            /// <summary>The <c>wired.chest.notification_preferences.update</c> key of the outgoing Flash message <c>SetChestNotificationPreferences</c>.</summary>
            public static readonly MessageKey NotificationPreferencesUpdate = new("wired.chest.notification_preferences.update");
        }

        /// <summary>Contains the wired transaction message keys.</summary>
        public static class Transaction
        {
            /// <summary>The <c>wired.transaction.succeeded</c> key of the incoming Flash message <c>WiredTransactionSuccess</c>.</summary>
            public static readonly MessageKey Succeeded = new("wired.transaction.succeeded");
            /// <summary>The <c>wired.transaction.failed</c> key of the incoming Flash message <c>WiredTransactionFail</c>.</summary>
            public static readonly MessageKey Failed = new("wired.transaction.failed");
            /// <summary>The <c>wired.transaction.chest_logs.request</c> key of the outgoing Flash message <c>WiredTransactionGetChestLogs</c>.</summary>
            public static readonly MessageKey ChestLogsRequest = new("wired.transaction.chest_logs.request");
            /// <summary>The <c>wired.transaction.room_logs.request</c> key of the outgoing Flash message <c>WiredTransactionGetRoomLogs</c>.</summary>
            public static readonly MessageKey RoomLogsRequest = new("wired.transaction.room_logs.request");
            /// <summary>The <c>wired.transaction.logs</c> key of the incoming Flash message <c>WiredTransactionLogList</c>.</summary>
            public static readonly MessageKey Logs = new("wired.transaction.logs");
            /// <summary>The <c>wired.transaction.log_details.request</c> key of the outgoing Flash message <c>WiredTransactionGetLogDetails</c>.</summary>
            public static readonly MessageKey LogDetailsRequest = new("wired.transaction.log_details.request");
            /// <summary>The <c>wired.transaction.log_details</c> key of the incoming Flash message <c>WiredTransactionLogDetails</c>.</summary>
            public static readonly MessageKey LogDetails = new("wired.transaction.log_details");
        }

        /// <summary>Contains the wired contract message keys.</summary>
        public static class Contracts
        {
            /// <summary>The <c>wired.contract.contents</c> key of the incoming Flash message <c>WiredContractContents</c>.</summary>
            public static readonly MessageKey Contents = new("wired.contract.contents");
            /// <summary>The <c>wired.contract.opened</c> key of the incoming Flash message <c>WiredOpenContract</c>.</summary>
            public static readonly MessageKey Opened = new("wired.contract.opened");
            /// <summary>The <c>wired.contract.open.request</c> key of the outgoing Flash message <c>WiredOpenContract</c>.</summary>
            public static readonly MessageKey OpenRequest = new("wired.contract.open.request");
            /// <summary>The <c>wired.contract.update</c> key of the outgoing Flash message <c>WiredUpdateContract</c>.</summary>
            public static readonly MessageKey Update = new("wired.contract.update");
            /// <summary>The <c>wired.contract.update.result</c> key of the incoming Flash message <c>WiredContractUpdateResult</c>.</summary>
            public static readonly MessageKey UpdateResult = new("wired.contract.update.result");
        }

        /// <summary>Contains the wired trade message keys.</summary>
        public static class Trade
        {
            /// <summary>The <c>wired.trade.initiated</c> key of the incoming Flash message <c>WiredTradeInitiate</c>.</summary>
            public static readonly MessageKey Initiated = new("wired.trade.initiated");
            /// <summary>The <c>wired.trade.items.updated</c> key of the incoming Flash message <c>WiredTradeItemsUpdate</c>.</summary>
            public static readonly MessageKey ItemsUpdated = new("wired.trade.items.updated");
            /// <summary>The <c>wired.trade.cancelled</c> key of the incoming Flash message <c>WiredTradeCancelled</c>.</summary>
            public static readonly MessageKey Cancelled = new("wired.trade.cancelled");
            /// <summary>The <c>wired.trade.completed</c> key of the incoming Flash message <c>WiredTradeCompleted</c>.</summary>
            public static readonly MessageKey Completed = new("wired.trade.completed");
            /// <summary>The <c>wired.trade.items.update</c> key of the outgoing Flash message <c>WiredTradeAddDeleteItems</c>.</summary>
            public static readonly MessageKey ItemsUpdate = new("wired.trade.items.update");
            /// <summary>The <c>wired.trade.confirm</c> key of the outgoing Flash message <c>WiredTradeConfirm</c>.</summary>
            public static readonly MessageKey Confirm = new("wired.trade.confirm");
            /// <summary>The <c>wired.trade.cancel</c> key of the outgoing Flash message <c>WiredTradeCancel</c>.</summary>
            public static readonly MessageKey Cancel = new("wired.trade.cancel");
            /// <summary>The <c>wired.trade.notification</c> key of the incoming Flash message <c>WiredTradeTransactionNotification</c>.</summary>
            public static readonly MessageKey Notification = new("wired.trade.notification");
        }
    }

    /// <summary>Contains the quest message keys.</summary>
    public static class Quests
    {
        /// <summary>The <c>quests.request</c> key of the outgoing Flash message <c>GetQuests</c>.</summary>
        public static readonly MessageKey Request = new("quests.request");
        /// <summary>The <c>quests.snapshot</c> key of the incoming Flash message <c>Quests</c>.</summary>
        public static readonly MessageKey Snapshot = new("quests.snapshot");
        /// <summary>The <c>quests.seasonal.request</c> key of the outgoing Flash message <c>GetSeasonalQuestsOnly</c>.</summary>
        public static readonly MessageKey SeasonalRequest = new("quests.seasonal.request");
        /// <summary>The <c>quests.seasonal.snapshot</c> key of the incoming Flash message <c>SeasonalQuests</c>.</summary>
        public static readonly MessageKey SeasonalSnapshot = new("quests.seasonal.snapshot");
        /// <summary>The <c>quest.updated</c> key of the incoming Flash message <c>Quest</c>.</summary>
        public static readonly MessageKey Updated = new("quest.updated");
        /// <summary>The <c>quest.completed</c> key of the incoming Flash message <c>QuestCompleted</c>.</summary>
        public static readonly MessageKey Completed = new("quest.completed");
        /// <summary>The <c>quest.cancelled</c> key of the incoming Flash message <c>QuestCancelled</c>.</summary>
        public static readonly MessageKey Cancelled = new("quest.cancelled");
        /// <summary>The <c>quest.daily.request</c> key of the outgoing Flash message <c>GetDailyQuest</c>.</summary>
        public static readonly MessageKey DailyRequest = new("quest.daily.request");
        /// <summary>The <c>quest.daily</c> key of the incoming Flash message <c>QuestDaily</c>.</summary>
        public static readonly MessageKey Daily = new("quest.daily");
        /// <summary>The <c>quest.accept</c> key of the outgoing Flash message <c>AcceptQuest</c>.</summary>
        public static readonly MessageKey Accept = new("quest.accept");
        /// <summary>The <c>quest.activate</c> key of the outgoing Flash message <c>ActivateQuest</c>.</summary>
        public static readonly MessageKey Activate = new("quest.activate");
        /// <summary>The <c>quest.reject</c> key of the outgoing Flash message <c>RejectQuest</c>.</summary>
        public static readonly MessageKey Reject = new("quest.reject");
        /// <summary>The <c>quest.cancel</c> key of the outgoing Flash message <c>CancelQuest</c>.</summary>
        public static readonly MessageKey Cancel = new("quest.cancel");
        /// <summary>The <c>quest.tracker.open</c> key of the outgoing Flash message <c>OpenQuestTracker</c>.</summary>
        public static readonly MessageKey TrackerOpen = new("quest.tracker.open");
        /// <summary>The <c>quest.friend_request.completed</c> key of the outgoing Flash message <c>FriendRequestQuestComplete</c>.</summary>
        public static readonly MessageKey FriendRequestCompleted = new("quest.friend_request.completed");
    }

    /// <summary>Contains the daily task message keys.</summary>
    public static class DailyTasks
    {
        /// <summary>The <c>daily_tasks.request</c> key of the outgoing Flash message <c>GetDailyTasks</c>.</summary>
        public static readonly MessageKey Request = new("daily_tasks.request");
        /// <summary>The <c>daily_tasks.snapshot</c> key of the incoming Flash message <c>DailyTasksActiveList</c>.</summary>
        public static readonly MessageKey Snapshot = new("daily_tasks.snapshot");
        /// <summary>The <c>daily_tasks.added</c> key of the incoming Flash message <c>DailyTasksTasksAdded</c>.</summary>
        public static readonly MessageKey Added = new("daily_tasks.added");
        /// <summary>The <c>daily_task.updated</c> key of the incoming Flash message <c>DailyTasksTaskUpdate</c>.</summary>
        public static readonly MessageKey Updated = new("daily_task.updated");
        /// <summary>The <c>daily_task.claim</c> key of the outgoing Flash message <c>ClaimDailyTask</c>.</summary>
        public static readonly MessageKey Claim = new("daily_task.claim");
    }

    /// <summary>Contains the Habbicon message keys.</summary>
    public static class Habbicons
    {
        /// <summary>The <c>habbicons.shop.request</c> key of the outgoing Flash message <c>GetHabbiconShopData</c>.</summary>
        public static readonly MessageKey ShopRequest = new("habbicons.shop.request");
        /// <summary>The <c>habbicons.shop.snapshot</c> key of the incoming Flash message <c>HabbiconShopData</c>.</summary>
        public static readonly MessageKey ShopSnapshot = new("habbicons.shop.snapshot");
        /// <summary>The <c>habbicons.inventory.snapshot</c> key of the incoming Flash message <c>UserHabbicons</c>.</summary>
        public static readonly MessageKey InventorySnapshot = new("habbicons.inventory.snapshot");
        /// <summary>The <c>habbicon.status.updated</c> key of the incoming Flash message <c>UserHabbiconStatusChanged</c>.</summary>
        public static readonly MessageKey StatusUpdated = new("habbicon.status.updated");
        /// <summary>The <c>habbicon.info.request</c> key of the outgoing Flash message <c>GetHabbiconInfo</c>.</summary>
        public static readonly MessageKey InfoRequest = new("habbicon.info.request");
        /// <summary>The <c>habbicon.info.snapshot</c> key of the incoming Flash message <c>HabbiconInfo</c>.</summary>
        public static readonly MessageKey InfoSnapshot = new("habbicon.info.snapshot");
        /// <summary>The <c>habbicon.room.used</c> key of the incoming Flash message <c>RoomUseHabbicon</c>.</summary>
        public static readonly MessageKey RoomUsed = new("habbicon.room.used");
        /// <summary>The <c>habbicon.buy</c> key of the outgoing Flash message <c>BuyHabbicon</c>.</summary>
        public static readonly MessageKey Buy = new("habbicon.buy");
        /// <summary>The <c>habbicon.collection.buy</c> key of the outgoing Flash message <c>BuyHabbiconCollection</c>.</summary>
        public static readonly MessageKey BuyCollection = new("habbicon.collection.buy");
        /// <summary>The <c>habbicon.claim</c> key of the outgoing Flash message <c>ClaimHabbicon</c>.</summary>
        public static readonly MessageKey Claim = new("habbicon.claim");
        /// <summary>The <c>habbicon.favorite</c> key of the outgoing Flash message <c>FavoriteHabbicon</c>.</summary>
        public static readonly MessageKey Favorite = new("habbicon.favorite");
        /// <summary>The <c>habbicon.unfavorite</c> key of the outgoing Flash message <c>UnfavoriteHabbicon</c>.</summary>
        public static readonly MessageKey Unfavorite = new("habbicon.unfavorite");
    }

    /// <summary>Contains the leaderboard message keys.</summary>
    public static class Leaderboards
    {
        /// <summary>Contains the total leaderboard message keys.</summary>
        public static class Total
        {
            /// <summary>The <c>leaderboards.total.request</c> key of the outgoing Flash message <c>Game2GetTotalLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.total.request");
            /// <summary>The <c>leaderboards.total.snapshot</c> key of the incoming Flash message <c>Game2TotalLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.total.snapshot");
        }

        /// <summary>Contains the friends leaderboard message keys.</summary>
        public static class Friends
        {
            /// <summary>The <c>leaderboards.friends.request</c> key of the outgoing Flash message <c>Game2GetFriendsLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.friends.request");
            /// <summary>The <c>leaderboards.friends.snapshot</c> key of the incoming Flash message <c>Game2FriendsLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.friends.snapshot");
        }

        /// <summary>Contains the group leaderboard message keys.</summary>
        public static class Groups
        {
            /// <summary>The <c>leaderboards.groups.request</c> key of the outgoing Flash message <c>Game2GetTotalGroupLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.groups.request");
            /// <summary>The <c>leaderboards.groups.snapshot</c> key of the incoming Flash message <c>Game2TotalGroupLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.groups.snapshot");
        }

        /// <summary>Contains the weekly total leaderboard message keys.</summary>
        public static class WeeklyTotal
        {
            /// <summary>The <c>leaderboards.weekly.total.request</c> key of the outgoing Flash message <c>Game2GetWeeklyLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.weekly.total.request");
            /// <summary>The <c>leaderboards.weekly.total.snapshot</c> key of the incoming Flash message <c>Game2WeeklyLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.total.snapshot");
        }

        /// <summary>Contains the weekly friends leaderboard message keys.</summary>
        public static class WeeklyFriends
        {
            /// <summary>The <c>leaderboards.weekly.friends.request</c> key of the outgoing Flash message <c>Game2GetWeeklyFriendsLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.weekly.friends.request");
            /// <summary>The <c>leaderboards.weekly.friends.snapshot</c> key of the incoming Flash message <c>Game2WeeklyFriendsLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.friends.snapshot");
        }

        /// <summary>Contains the weekly group leaderboard message keys.</summary>
        public static class WeeklyGroups
        {
            /// <summary>The <c>leaderboards.weekly.groups.request</c> key of the outgoing Flash message <c>Game2GetWeeklyGroupLeaderboard</c>.</summary>
            public static readonly MessageKey Request = new("leaderboards.weekly.groups.request");
            /// <summary>The <c>leaderboards.weekly.groups.snapshot</c> key of the incoming Flash message <c>Game2WeeklyGroupLeaderboard</c>.</summary>
            public static readonly MessageKey Snapshot = new("leaderboards.weekly.groups.snapshot");
        }
    }

    /// <summary>Contains the inventory message keys.</summary>
    public static class Inventory
    {
        /// <summary>Contains the avatar effect inventory message keys.</summary>
        public static class AvatarEffects
        {
            /// <summary>The <c>inventory.avatar_effect.activation.request</c> key of the outgoing Flash message <c>AvatarEffectActivated</c>.</summary>
            public static readonly MessageKey ActivationRequest =
                new("inventory.avatar_effect.activation.request");
        }

        /// <summary>Contains the furni inventory message keys.</summary>
        public static class Furni
        {
            /// <summary>The <c>inventory.furni.request</c> key of the outgoing Flash message <c>RequestFurniInventory</c>.</summary>
            public static readonly MessageKey Request = new("inventory.furni.request");
            /// <summary>The <c>inventory.furni.snapshot</c> key of the incoming Flash message <c>FurniList</c>.</summary>
            public static readonly MessageKey Snapshot = new("inventory.furni.snapshot");
            /// <summary>The <c>inventory.furni.added_or_updated</c> key of the incoming Flash message <c>FurniListAddOrUpdate</c>.</summary>
            public static readonly MessageKey AddedOrUpdated = new("inventory.furni.added_or_updated");
            /// <summary>The <c>inventory.furni.removed</c> key of the incoming Flash message <c>FurniListRemove</c>.</summary>
            public static readonly MessageKey Removed = new("inventory.furni.removed");
            /// <summary>The <c>inventory.furni.removed_multiple</c> key of the incoming Flash message <c>FurniListRemoveMultiple</c>.</summary>
            public static readonly MessageKey RemovedMultiple = new("inventory.furni.removed_multiple");
            /// <summary>The <c>inventory.furni.invalidated</c> key of the incoming Flash message <c>FurniListInvalidate</c>.</summary>
            public static readonly MessageKey Invalidated = new("inventory.furni.invalidated");
            /// <summary>The <c>inventory.furni.post_it_placed</c> key of the incoming Flash message <c>PostItPlaced</c>.</summary>
            public static readonly MessageKey PostItPlaced = new("inventory.furni.post_it_placed");
        }

        /// <summary>Contains the pet inventory message keys.</summary>
        public static class Pets
        {
            /// <summary>The <c>inventory.pets.request</c> key of the outgoing Flash message <c>GetPetInventory</c>.</summary>
            public static readonly MessageKey Request = new("inventory.pets.request");
            /// <summary>The <c>inventory.pets.snapshot</c> key of the incoming Flash message <c>PetInventory</c>.</summary>
            public static readonly MessageKey Snapshot = new("inventory.pets.snapshot");
            /// <summary>The <c>inventory.pets.added</c> key of the incoming Flash message <c>PetAddedToInventory</c>.</summary>
            public static readonly MessageKey Added = new("inventory.pets.added");
            /// <summary>The <c>inventory.pets.removed</c> key of the incoming Flash message <c>PetRemovedFromInventory</c>.</summary>
            public static readonly MessageKey Removed = new("inventory.pets.removed");
        }
    }

    /// <summary>Contains the wardrobe message keys.</summary>
    public static class Wardrobe
    {
        /// <summary>The <c>wardrobe.request</c> key of the outgoing Flash message <c>GetWardrobe</c>.</summary>
        public static readonly MessageKey Request = new("wardrobe.request");
        /// <summary>The <c>wardrobe.snapshot</c> key of the incoming Flash message <c>Wardrobe</c>.</summary>
        public static readonly MessageKey Snapshot = new("wardrobe.snapshot");
        /// <summary>The <c>wardrobe.figure.update</c> key of the outgoing Flash message <c>UpdateFigureData</c>.</summary>
        public static readonly MessageKey FigureUpdate = new("wardrobe.figure.update");
        /// <summary>The <c>wardrobe.outfit.save</c> key of the outgoing Flash message <c>SaveWardrobeOutfit</c>.</summary>
        public static readonly MessageKey OutfitSave = new("wardrobe.outfit.save");
    }

    /// <summary>Contains the catalog message keys.</summary>
    public static class Catalog
    {
        /// <summary>The <c>catalog.index.request</c> key of the outgoing Flash message <c>GetCatalogIndex</c>.</summary>
        public static readonly MessageKey IndexRequest = new("catalog.index.request");
        /// <summary>The <c>catalog.index.snapshot</c> key of the incoming Flash message <c>CatalogIndex</c>.</summary>
        public static readonly MessageKey IndexSnapshot = new("catalog.index.snapshot");
        /// <summary>The <c>catalog.page.request</c> key of the outgoing Flash message <c>GetCatalogPage</c>.</summary>
        public static readonly MessageKey PageRequest = new("catalog.page.request");
        /// <summary>The <c>catalog.page.snapshot</c> key of the incoming Flash message <c>CatalogPage</c>.</summary>
        public static readonly MessageKey PageSnapshot = new("catalog.page.snapshot");
        /// <summary>The <c>catalog.purchase</c> key of the outgoing Flash message <c>PurchaseFromCatalog</c>.</summary>
        public static readonly MessageKey Purchase = new("catalog.purchase");
        /// <summary>The <c>catalog.purchase.accepted</c> key of the incoming Flash message <c>PurchaseOk</c>.</summary>
        public static readonly MessageKey PurchaseAccepted = new("catalog.purchase.accepted");
        /// <summary>The <c>catalog.purchase.failed</c> key of the incoming Flash message <c>PurchaseError</c>.</summary>
        public static readonly MessageKey PurchaseFailed = new("catalog.purchase.failed");
        /// <summary>The <c>catalog.purchase.forbidden</c> key of the incoming Flash message <c>PurchaseNotAllowed</c>.</summary>
        public static readonly MessageKey PurchaseForbidden = new("catalog.purchase.forbidden");
        /// <summary>The <c>catalog.published</c> key of the incoming Flash message <c>CatalogPublished</c>.</summary>
        public static readonly MessageKey Published = new("catalog.published");
        /// <summary>The <c>catalog.room_ad.info.request</c> key of the outgoing Flash message <c>GetRoomAdPurchaseInfo</c>.</summary>
        public static readonly MessageKey RoomAdInfoRequest = new("catalog.room_ad.info.request");
        /// <summary>The <c>catalog.room_ad.info</c> key of the incoming Flash message <c>RoomAdPurchaseInfo</c>.</summary>
        public static readonly MessageKey RoomAdInfo = new("catalog.room_ad.info");
    }

    /// <summary>Contains the marketplace message keys.</summary>
    public static class Marketplace
    {
        /// <summary>Contains the marketplace configuration message keys.</summary>
        public static class Configuration
        {
            /// <summary>The <c>marketplace.configuration.request</c> key of the outgoing Flash message <c>GetMarketplaceConfiguration</c>.</summary>
            public static readonly MessageKey Request = new("marketplace.configuration.request");
            /// <summary>The <c>marketplace.configuration</c> key of the incoming Flash message <c>MarketplaceConfiguration</c>.</summary>
            public static readonly MessageKey Snapshot = new("marketplace.configuration");
        }

        /// <summary>Contains the marketplace eligibility message keys.</summary>
        public static class Eligibility
        {
            /// <summary>The <c>marketplace.eligibility.request</c> key of the outgoing Flash message <c>GetMarketplaceCanMakeOffer</c>.</summary>
            public static readonly MessageKey Request = new("marketplace.eligibility.request");
            /// <summary>The <c>marketplace.eligibility.result</c> key of the incoming Flash message <c>MarketplaceCanMakeOfferResult</c>.</summary>
            public static readonly MessageKey Result = new("marketplace.eligibility.result");
        }

        /// <summary>Contains the marketplace credits message keys.</summary>
        public static class Credits
        {
            /// <summary>The <c>marketplace.credits.redeem</c> key of the outgoing Flash message <c>RedeemMarketplaceOfferCredits</c>.</summary>
            public static readonly MessageKey Redeem = new("marketplace.credits.redeem");
        }

        /// <summary>Contains the marketplace token message keys.</summary>
        public static class Tokens
        {
            /// <summary>The <c>marketplace.tokens.buy</c> key of the outgoing Flash message <c>BuyMarketplaceTokens</c>.</summary>
            public static readonly MessageKey Buy = new("marketplace.tokens.buy");
        }

        /// <summary>Contains the marketplace offer message keys.</summary>
        public static class Offers
        {
            /// <summary>The <c>marketplace.offers.search.request</c> key of the outgoing Flash message <c>GetMarketplaceOffers</c>.</summary>
            public static readonly MessageKey SearchRequest = new("marketplace.offers.search.request");
            /// <summary>The <c>marketplace.offers.search.result</c> key of the incoming Flash message <c>MarketPlaceOffers</c>.</summary>
            public static readonly MessageKey SearchResult = new("marketplace.offers.search.result");
            /// <summary>The <c>marketplace.offers.own.request</c> key of the outgoing Flash message <c>GetMarketplaceOwnOffers</c>.</summary>
            public static readonly MessageKey OwnRequest = new("marketplace.offers.own.request");
            /// <summary>The <c>marketplace.offers.own.snapshot</c> key of the incoming Flash message <c>MarketPlaceOwnOffers</c>.</summary>
            public static readonly MessageKey OwnSnapshot = new("marketplace.offers.own.snapshot");
            /// <summary>The <c>marketplace.offer.make</c> key of the outgoing Flash message <c>MakeOffer</c>.</summary>
            public static readonly MessageKey Make = new("marketplace.offer.make");
            /// <summary>The <c>marketplace.offer.make.result</c> key of the incoming Flash message <c>MarketplaceMakeOfferResult</c>.</summary>
            public static readonly MessageKey MakeResult = new("marketplace.offer.make.result");
            /// <summary>The <c>marketplace.offer.buy</c> key of the outgoing Flash message <c>BuyMarketplaceOffer</c>.</summary>
            public static readonly MessageKey Buy = new("marketplace.offer.buy");
            /// <summary>The <c>marketplace.offer.buy.result</c> key of the incoming Flash message <c>MarketplaceBuyOfferResult</c>.</summary>
            public static readonly MessageKey BuyResult = new("marketplace.offer.buy.result");
            /// <summary>The <c>marketplace.offer.cancel</c> key of the outgoing Flash message <c>CancelMarketplaceOffer</c>.</summary>
            public static readonly MessageKey Cancel = new("marketplace.offer.cancel");
            /// <summary>The <c>marketplace.offer.cancel.result</c> key of the incoming Flash message <c>MarketplaceCancelOfferResult</c>.</summary>
            public static readonly MessageKey CancelResult = new("marketplace.offer.cancel.result");
            /// <summary>The <c>marketplace.offers.cancel_all</c> key of the outgoing Flash message <c>CancelAllMarketplaceOffers</c>.</summary>
            public static readonly MessageKey CancelAll = new("marketplace.offers.cancel_all");
            /// <summary>The <c>marketplace.offers.cancel_all.result</c> key of the incoming Flash message <c>MarketplaceCancelAllOffersResult</c>.</summary>
            public static readonly MessageKey CancelAllResult = new("marketplace.offers.cancel_all.result");
            /// <summary>The <c>marketplace.offers.own_history.clear</c> key of the outgoing Flash message <c>ClearMarketplaceOwnHistory</c>.</summary>
            public static readonly MessageKey ClearOwnHistory = new("marketplace.offers.own_history.clear");
            /// <summary>The <c>marketplace.offers.own_history.clear.result</c> key of the incoming Flash message <c>MarketplaceClearOwnHistoryResult</c>.</summary>
            public static readonly MessageKey ClearOwnHistoryResult = new("marketplace.offers.own_history.clear.result");
        }

        /// <summary>Contains the marketplace item statistics message keys.</summary>
        public static class ItemStats
        {
            /// <summary>The <c>marketplace.item_stats.request</c> key of the outgoing Flash message <c>GetMarketplaceItemStats</c>.</summary>
            public static readonly MessageKey Request = new("marketplace.item_stats.request");
            /// <summary>The <c>marketplace.item_stats</c> key of the incoming Flash message <c>MarketplaceItemStats</c>.</summary>
            public static readonly MessageKey Snapshot = new("marketplace.item_stats");
        }
    }

    /// <summary>Contains the friend list and messenger message keys.</summary>
    public static class Friends
    {
        /// <summary>The <c>friends.initialize.request</c> key of the outgoing Flash message <c>MessengerInit</c>.</summary>
        public static readonly MessageKey InitializeRequest = new("friends.initialize.request");
        /// <summary>The <c>friends.initialized</c> key of the incoming Flash message <c>MessengerInit</c>.</summary>
        public static readonly MessageKey Initialized = new("friends.initialized");
        /// <summary>The <c>friends.list.fragment</c> key of the incoming Flash message <c>FriendListFragment</c>, also named <c>FriendsListFragment</c>.</summary>
        public static readonly MessageKey ListFragment = new("friends.list.fragment");
        /// <summary>The <c>friends.list.updated</c> key of the incoming Flash message <c>FriendListUpdate</c>.</summary>
        public static readonly MessageKey ListUpdated = new("friends.list.updated");
        /// <summary>The <c>friends.private_message.send</c> key of the outgoing Flash message <c>SendMsg</c>.</summary>
        public static readonly MessageKey PrivateMessageSend = new("friends.private_message.send");
        /// <summary>The <c>friends.private_message.received</c> key of the incoming Flash message <c>NewConsoleMessage</c>.</summary>
        public static readonly MessageKey PrivateMessageReceived = new("friends.private_message.received");
        /// <summary>The <c>friends.operation.failed</c> key of the incoming Flash message <c>MessengerError</c>.</summary>
        public static readonly MessageKey OperationFailed = new("friends.operation.failed");
        /// <summary>The <c>friends.private_message.failed</c> key of the incoming Flash message <c>InstantMessageError</c>.</summary>
        public static readonly MessageKey PrivateMessageFailed = new("friends.private_message.failed");
        /// <summary>The <c>friends.request.send</c> key of the outgoing Flash message <c>RequestFriend</c>.</summary>
        public static readonly MessageKey FriendRequestSend = new("friends.request.send");
        /// <summary>The <c>friends.request.received</c> key of the incoming Flash message <c>NewFriendRequest</c>.</summary>
        public static readonly MessageKey FriendRequestReceived = new("friends.request.received");
        /// <summary>The <c>friends.requests.request</c> key of the outgoing Flash message <c>GetFriendRequests</c>.</summary>
        public static readonly MessageKey FriendRequestsRequest = new("friends.requests.request");
        /// <summary>The <c>friends.requests.snapshot</c> key of the incoming Flash message <c>FriendRequests</c>.</summary>
        public static readonly MessageKey FriendRequestsSnapshot = new("friends.requests.snapshot");
        /// <summary>The <c>friends.request.accept</c> key of the outgoing Flash message <c>AcceptFriend</c>.</summary>
        public static readonly MessageKey FriendRequestAccept = new("friends.request.accept");
        /// <summary>The <c>friends.request.decline</c> key of the outgoing Flash message <c>DeclineFriend</c>.</summary>
        public static readonly MessageKey FriendRequestDecline = new("friends.request.decline");
        /// <summary>The <c>friends.remove</c> key of the outgoing Flash message <c>RemoveFriend</c>.</summary>
        public static readonly MessageKey Remove = new("friends.remove");
        /// <summary>The <c>friends.follow</c> key of the outgoing Flash message <c>FollowFriend</c>.</summary>
        public static readonly MessageKey Follow = new("friends.follow");
        /// <summary>The <c>friends.search.request</c> key of the outgoing Flash message <c>HabboSearch</c>.</summary>
        public static readonly MessageKey SearchRequest = new("friends.search.request");
        /// <summary>The <c>friends.search.result</c> key of the incoming Flash message <c>HabboSearchResult</c>.</summary>
        public static readonly MessageKey SearchResult = new("friends.search.result");
        /// <summary>The <c>friends.relationship.set</c> key of the outgoing Flash message <c>SetRelationshipStatus</c>.</summary>
        public static readonly MessageKey RelationshipSet = new("friends.relationship.set");
        /// <summary>The <c>friends.room_invite.received</c> key of the incoming Flash message <c>RoomInvite</c>.</summary>
        public static readonly MessageKey RoomInvite = new("friends.room_invite.received");
    }

    /// <summary>Contains the group message keys.</summary>
    public static class Groups
    {
        /// <summary>Contains the group details message keys.</summary>
        public static class Details
        {
            /// <summary>The <c>groups.details.request</c> key of the outgoing Flash message <c>GetHabboGroupDetails</c>.</summary>
            public static readonly MessageKey Request = new("groups.details.request");
            /// <summary>The <c>groups.details.snapshot</c> key of the incoming Flash message <c>HabboGroupDetails</c>.</summary>
            public static readonly MessageKey Snapshot = new("groups.details.snapshot");
        }

        /// <summary>Contains the group membership action message keys.</summary>
        public static class Membership
        {
            /// <summary>The <c>groups.membership.join</c> key of the outgoing Flash message <c>JoinHabboGroup</c>.</summary>
            public static readonly MessageKey Join = new("groups.membership.join");
            /// <summary>The <c>groups.membership.kick</c> key of the outgoing Flash message <c>KickMember</c>.</summary>
            public static readonly MessageKey Kick = new("groups.membership.kick");
            /// <summary>The <c>groups.membership.approve</c> key of the outgoing Flash message <c>ApproveMembershipRequest</c>.</summary>
            public static readonly MessageKey Approve = new("groups.membership.approve");
            /// <summary>The <c>groups.membership.reject</c> key of the outgoing Flash message <c>RejectMembershipRequest</c>.</summary>
            public static readonly MessageKey Reject = new("groups.membership.reject");
        }

        /// <summary>Contains the group member list message keys.</summary>
        public static class Members
        {
            /// <summary>The <c>groups.members.request</c> key of the outgoing Flash message <c>GetGuildMembers</c>.</summary>
            public static readonly MessageKey Request = new("groups.members.request");
            /// <summary>The <c>groups.members.snapshot</c> key of the incoming Flash message <c>GuildMembers</c>.</summary>
            public static readonly MessageKey Snapshot = new("groups.members.snapshot");
        }

        /// <summary>Contains the message keys for the groups a user belongs to.</summary>
        public static class Memberships
        {
            /// <summary>The <c>groups.memberships.request</c> key of the outgoing Flash message <c>GetGuildMemberships</c>.</summary>
            public static readonly MessageKey Request = new("groups.memberships.request");
            /// <summary>The <c>groups.memberships.snapshot</c> key of the incoming Flash message <c>GuildMemberships</c>.</summary>
            public static readonly MessageKey Snapshot = new("groups.memberships.snapshot");
        }
    }

    /// <summary>Contains the navigator message keys.</summary>
    public static class Navigator
    {
        /// <summary>Contains the navigator state message keys.</summary>
        public static class State
        {
            /// <summary>The <c>navigator.metadata.request</c> key of the outgoing Flash message <c>NewNavigatorInit</c>.</summary>
            public static readonly MessageKey MetadataRequest = new("navigator.metadata.request");
            /// <summary>The <c>navigator.metadata</c> key of the incoming Flash message <c>NavigatorMetaData</c>.</summary>
            public static readonly MessageKey Metadata = new("navigator.metadata");
            /// <summary>The <c>navigator.flat_categories.request</c> key of the outgoing Flash message <c>GetUserFlatCats</c>.</summary>
            public static readonly MessageKey FlatCategoriesRequest = new("navigator.flat_categories.request");
            /// <summary>The <c>navigator.flat_categories</c> key of the incoming Flash message <c>UserFlatCats</c>.</summary>
            public static readonly MessageKey FlatCategories = new("navigator.flat_categories");
            /// <summary>The <c>navigator.lifted_rooms</c> key of the incoming Flash message <c>NavigatorLiftedRooms</c>.</summary>
            public static readonly MessageKey LiftedRooms = new("navigator.lifted_rooms");
            /// <summary>The <c>navigator.settings</c> key of the incoming Flash message <c>NavigatorSettings</c>.</summary>
            public static readonly MessageKey Settings = new("navigator.settings");
            /// <summary>The <c>navigator.preferences</c> key of the incoming Flash message <c>NewNavigatorPreferences</c>.</summary>
            public static readonly MessageKey Preferences = new("navigator.preferences");
        }

        /// <summary>Contains the navigator search message keys.</summary>
        public static class Search
        {
            /// <summary>The <c>navigator.search.result</c> key of the incoming Flash message <c>NavigatorSearchResultBlocks</c>.</summary>
            public static readonly MessageKey Result = new("navigator.search.result");
            /// <summary>The <c>navigator.search.legacy_result</c> key of the incoming Flash message <c>GuestRoomSearchResult</c>.</summary>
            public static readonly MessageKey LegacyResult = new("navigator.search.legacy_result");
            /// <summary>The <c>navigator.search.view.request</c> key of the outgoing Flash message <c>NewNavigatorSearch</c>.</summary>
            public static readonly MessageKey View = new("navigator.search.view.request");
            /// <summary>The <c>navigator.search.my_rooms.request</c> key of the outgoing Flash message <c>MyRoomsSearch</c>.</summary>
            public static readonly MessageKey MyRooms = new("navigator.search.my_rooms.request");
            /// <summary>The <c>navigator.search.my_favourite_rooms.request</c> key of the outgoing Flash message <c>MyFavouriteRoomsSearch</c>.</summary>
            public static readonly MessageKey MyFavouriteRooms = new("navigator.search.my_favourite_rooms.request");
            /// <summary>The <c>navigator.search.my_room_rights.request</c> key of the outgoing Flash message <c>MyRoomRightsSearch</c>.</summary>
            public static readonly MessageKey MyRoomRights = new("navigator.search.my_room_rights.request");
            /// <summary>The <c>navigator.search.my_room_history.request</c> key of the outgoing Flash message <c>MyRoomHistorySearch</c>.</summary>
            public static readonly MessageKey MyRoomHistory = new("navigator.search.my_room_history.request");
            /// <summary>The <c>navigator.search.my_frequent_room_history.request</c> key of the outgoing Flash message <c>MyFrequentRoomHistorySearch</c>.</summary>
            public static readonly MessageKey MyFrequentRoomHistory = new("navigator.search.my_frequent_room_history.request");
            /// <summary>The <c>navigator.search.my_friends_rooms.request</c> key of the outgoing Flash message <c>MyFriendsRoomsSearch</c>.</summary>
            public static readonly MessageKey MyFriendsRooms = new("navigator.search.my_friends_rooms.request");
            /// <summary>The <c>navigator.search.rooms_where_friends_are.request</c> key of the outgoing Flash message <c>RoomsWhereMyFriendsAreSearch</c>.</summary>
            public static readonly MessageKey RoomsWhereFriendsAre = new("navigator.search.rooms_where_friends_are.request");
            /// <summary>The <c>navigator.search.my_guild_bases.request</c> key of the outgoing Flash message <c>MyGuildBasesSearch</c>.</summary>
            public static readonly MessageKey MyGuildBases = new("navigator.search.my_guild_bases.request");
            /// <summary>The <c>navigator.search.text.request</c> key of the outgoing Flash message <c>RoomTextSearch</c>.</summary>
            public static readonly MessageKey Text = new("navigator.search.text.request");
            /// <summary>The <c>navigator.search.popular.request</c> key of the outgoing Flash message <c>PopularRoomsSearch</c>.</summary>
            public static readonly MessageKey Popular = new("navigator.search.popular.request");
            /// <summary>The <c>navigator.search.highest_scoring.request</c> key of the outgoing Flash message <c>RoomsWithHighestScoreSearch</c>.</summary>
            public static readonly MessageKey HighestScoring = new("navigator.search.highest_scoring.request");
            /// <summary>The <c>navigator.search.guild_bases.request</c> key of the outgoing Flash message <c>GuildBaseSearch</c>.</summary>
            public static readonly MessageKey GuildBases = new("navigator.search.guild_bases.request");
        }

        /// <summary>Contains the navigator personalization and own room message keys.</summary>
        public static class Personalization
        {
            /// <summary>The <c>navigator.saved_searches</c> key of the incoming Flash message <c>NavigatorSavedSearches</c>.</summary>
            public static readonly MessageKey SavedSearches = new("navigator.saved_searches");
            /// <summary>The <c>navigator.saved_search.add</c> key of the outgoing Flash message <c>NavigatorAddSavedSearch</c>.</summary>
            public static readonly MessageKey SavedSearchAdd = new("navigator.saved_search.add");
            /// <summary>The <c>navigator.saved_search.delete</c> key of the outgoing Flash message <c>NavigatorDeleteSavedSearch</c>.</summary>
            public static readonly MessageKey SavedSearchDelete = new("navigator.saved_search.delete");
            /// <summary>The <c>navigator.collapsed_categories</c> key of the incoming Flash message <c>CollapsedCategories</c>.</summary>
            public static readonly MessageKey CollapsedCategories = new("navigator.collapsed_categories");
            /// <summary>The <c>navigator.collapsed_category.add</c> key of the outgoing Flash message <c>NavigatorAddCollapsedCategory</c>.</summary>
            public static readonly MessageKey CollapsedCategoryAdd = new("navigator.collapsed_category.add");
            /// <summary>The <c>navigator.collapsed_category.remove</c> key of the outgoing Flash message <c>NavigatorRemoveCollapsedCategory</c>.</summary>
            public static readonly MessageKey CollapsedCategoryRemove = new("navigator.collapsed_category.remove");
        }

        /// <summary>The <c>navigator.home_room.update</c> key of the outgoing Flash message <c>UpdateHomeRoom</c>.</summary>
        public static readonly MessageKey HomeRoomUpdate = new("navigator.home_room.update");
        /// <summary>The <c>navigator.room.create</c> key of the outgoing Flash message <c>CreateFlat</c>.</summary>
        public static readonly MessageKey RoomCreate = new("navigator.room.create");
        /// <summary>The <c>navigator.room.delete</c> key of the outgoing Flash message <c>DeleteRoom</c>.</summary>
        public static readonly MessageKey RoomDelete = new("navigator.room.delete");
    }

    /// <summary>Contains the user and profile message keys.</summary>
    public static class Users
    {
        /// <summary>Contains the relationship status message keys.</summary>
        public static class Relationship
        {
            /// <summary>The <c>users.relationship.request</c> key of the outgoing Flash message <c>GetRelationshipStatusInfo</c>.</summary>
            public static readonly MessageKey Request = new("users.relationship.request");
            /// <summary>The <c>users.relationship.snapshot</c> key of the incoming Flash message <c>RelationshipStatusInfo</c>.</summary>
            public static readonly MessageKey Snapshot = new("users.relationship.snapshot");
        }

        /// <summary>Contains the block list message keys.</summary>
        public static class Block
        {
            /// <summary>The <c>users.block.list.request</c> key of the outgoing Flash message <c>BlockListInit</c>.</summary>
            public static readonly MessageKey ListRequest = new("users.block.list.request");
            /// <summary>The <c>users.block.list.snapshot</c> key of the incoming Flash message <c>BlockList</c>.</summary>
            public static readonly MessageKey ListSnapshot = new("users.block.list.snapshot");
            /// <summary>The <c>users.block.updated</c> key of the incoming Flash message <c>BlockUserUpdate</c>.</summary>
            public static readonly MessageKey Updated = new("users.block.updated");
            /// <summary>The <c>users.block.add</c> key of the outgoing Flash message <c>BlockUser</c>.</summary>
            public static readonly MessageKey Add = new("users.block.add");
            /// <summary>The <c>users.block.remove</c> key of the outgoing Flash message <c>UnblockUser</c>.</summary>
            public static readonly MessageKey Remove = new("users.block.remove");
        }

        /// <summary>Contains the ignore list message keys.</summary>
        public static class Ignore
        {
            /// <summary>The <c>users.ignore.list.request</c> key of the outgoing Flash message <c>GetIgnoredUsers</c>.</summary>
            public static readonly MessageKey ListRequest = new("users.ignore.list.request");
            /// <summary>The <c>users.ignore.list.snapshot</c> key of the incoming Flash message <c>IgnoredUsers</c>.</summary>
            public static readonly MessageKey ListSnapshot = new("users.ignore.list.snapshot");
            /// <summary>The <c>users.ignore.updated</c> key of the incoming Flash message <c>IgnoreResult</c>.</summary>
            public static readonly MessageKey Updated = new("users.ignore.updated");
            /// <summary>The <c>users.ignore.add_by_id.request</c> key of the outgoing Flash message <c>IgnoreUser</c>.</summary>
            public static readonly MessageKey AddByIdRequest = new("users.ignore.add_by_id.request");
            /// <summary>The <c>users.ignore.remove</c> key of the outgoing Flash message <c>UnignoreUser</c>.</summary>
            public static readonly MessageKey Remove = new("users.ignore.remove");
        }

        /// <summary>Contains the figure set message keys.</summary>
        public static class FigureSets
        {
            /// <summary>The <c>users.figure_sets.added</c> key of the incoming Flash message <c>FigureSetIdAdded</c>.</summary>
            public static readonly MessageKey Added = new("users.figure_sets.added");
            /// <summary>The <c>users.figure_sets.removed</c> key of the incoming Flash message <c>FigureSetIdRemoved</c>.</summary>
            public static readonly MessageKey Removed = new("users.figure_sets.removed");
            /// <summary>The <c>users.figure_sets.snapshot</c> key of the incoming Flash message <c>FigureSetIds</c>.</summary>
            public static readonly MessageKey Snapshot = new("users.figure_sets.snapshot");
        }

        /// <summary>Contains the sanction status message keys.</summary>
        public static class Sanctions
        {
            /// <summary>The <c>users.sanctions.request</c> key of the outgoing Flash message <c>GetMySanctionStatus</c>.</summary>
            public static readonly MessageKey Request = new("users.sanctions.request");
            /// <summary>The <c>users.sanctions.snapshot</c> key of the incoming Flash message <c>MySanctionStatus</c>, also named <c>SanctionStatus</c>.</summary>
            public static readonly MessageKey Snapshot = new("users.sanctions.snapshot");
        }

        /// <summary>Contains the favorite group message keys.</summary>
        public static class FavoriteGroup
        {
            /// <summary>The <c>users.favorite_group.select</c> key of the outgoing Flash message <c>SelectFavouriteHabboGroup</c>.</summary>
            public static readonly MessageKey Select = new("users.favorite_group.select");
            /// <summary>The <c>users.favorite_group.deselect</c> key of the outgoing Flash message <c>DeselectFavouriteHabboGroup</c>.</summary>
            public static readonly MessageKey Deselect = new("users.favorite_group.deselect");
        }

        /// <summary>The <c>users.motto.update</c> key of the outgoing Flash message <c>ChangeMotto</c>.</summary>
        public static readonly MessageKey MottoUpdate = new("users.motto.update");
        /// <summary>The <c>users.profile.request</c> key of the outgoing Flash message <c>InfoRetrieve</c>.</summary>
        public static readonly MessageKey ProfileRequest = new("users.profile.request");
        /// <summary>The <c>users.profile.snapshot</c> key of the incoming Flash message <c>UserObject</c>.</summary>
        public static readonly MessageKey ProfileSnapshot = new("users.profile.snapshot");
        /// <summary>The <c>users.figure.updated</c> key of the incoming Flash message <c>FigureUpdate</c>.</summary>
        public static readonly MessageKey FigureUpdated = new("users.figure.updated");
        /// <summary>The <c>users.name_change.result</c> key of the incoming Flash message <c>ChangeUserNameResult</c>.</summary>
        public static readonly MessageKey NameChangeResult = new("users.name_change.result");
        /// <summary>The <c>users.safety_lock.changed</c> key of the incoming Flash message <c>AccountSafetyLockStatusChange</c>.</summary>
        public static readonly MessageKey SafetyLockChanged = new("users.safety_lock.changed");
        /// <summary>The <c>users.profile.extended.request</c> key of the outgoing Flash message <c>GetExtendedProfile</c>.</summary>
        public static readonly MessageKey ExtendedProfileRequest = new("users.profile.extended.request");
        /// <summary>The <c>users.profile.extended.snapshot</c> key of the incoming Flash message <c>ExtendedProfile</c>.</summary>
        public static readonly MessageKey ExtendedProfileSnapshot = new("users.profile.extended.snapshot");
    }

    /// <summary>Contains the notification message keys.</summary>
    public static class Notifications
    {
        /// <summary>The <c>notifications.dialog</c> key of the incoming Flash message <c>NotificationDialog</c>.</summary>
        public static readonly MessageKey Dialog = new("notifications.dialog");
        /// <summary>The <c>notifications.message_of_the_day</c> key of the incoming Flash message <c>MOTDNotification</c>.</summary>
        public static readonly MessageKey MessageOfTheDay = new("notifications.message_of_the_day");
    }

    /// <summary>Contains the group forum message keys.</summary>
    public static class Forums
    {
        /// <summary>The <c>forums.stats</c> key of the incoming Flash message <c>ForumData</c>.</summary>
        public static readonly MessageKey Stats = new("forums.stats");
        /// <summary>The <c>forums.list</c> key of the incoming Flash message <c>ForumsList</c>.</summary>
        public static readonly MessageKey List = new("forums.list");
        /// <summary>The <c>forums.threads</c> key of the incoming Flash message <c>ForumThreads</c>.</summary>
        public static readonly MessageKey Threads = new("forums.threads");
        /// <summary>The <c>forums.messages</c> key of the incoming Flash message <c>ThreadMessages</c>.</summary>
        public static readonly MessageKey Messages = new("forums.messages");
        /// <summary>The <c>forums.thread.created</c> key of the incoming Flash message <c>PostThread</c>.</summary>
        public static readonly MessageKey ThreadCreated = new("forums.thread.created");
        /// <summary>The <c>forums.message.created</c> key of the incoming Flash message <c>PostMessage</c>.</summary>
        public static readonly MessageKey MessageCreated = new("forums.message.created");
        /// <summary>The <c>forums.thread.updated</c> key of the incoming Flash message <c>UpdateThread</c>.</summary>
        public static readonly MessageKey ThreadUpdated = new("forums.thread.updated");
        /// <summary>The <c>forums.message.updated</c> key of the incoming Flash message <c>UpdateMessage</c>.</summary>
        public static readonly MessageKey MessageUpdated = new("forums.message.updated");
        /// <summary>The <c>forums.unread_count</c> key of the incoming Flash message <c>UnreadForumsCount</c>.</summary>
        public static readonly MessageKey UnreadCount = new("forums.unread_count");
        /// <summary>The <c>forums.stats.request</c> key of the outgoing Flash message <c>GetForumStats</c>.</summary>
        public static readonly MessageKey StatsRequest = new("forums.stats.request");
        /// <summary>The <c>forums.list.request</c> key of the outgoing Flash message <c>GetForumsList</c>.</summary>
        public static readonly MessageKey ListRequest = new("forums.list.request");
        /// <summary>The <c>forums.threads.request</c> key of the outgoing Flash message <c>GetThreads</c>.</summary>
        public static readonly MessageKey ThreadsRequest = new("forums.threads.request");
        /// <summary>The <c>forums.messages.request</c> key of the outgoing Flash message <c>GetMessages</c>.</summary>
        public static readonly MessageKey MessagesRequest = new("forums.messages.request");
        /// <summary>The <c>forums.thread.request</c> key of the outgoing Flash message <c>GetThread</c>.</summary>
        public static readonly MessageKey ThreadRequest = new("forums.thread.request");
        /// <summary>The <c>forums.unread_count.request</c> key of the outgoing Flash message <c>GetUnreadForumsCount</c>.</summary>
        public static readonly MessageKey UnreadCountRequest = new("forums.unread_count.request");
        /// <summary>The <c>forums.post</c> key of the outgoing Flash message <c>PostMessage</c>.</summary>
        public static readonly MessageKey Post = new("forums.post");
        /// <summary>The <c>forums.thread.moderate</c> key of the outgoing Flash message <c>ModerateThread</c>.</summary>
        public static readonly MessageKey ThreadModerate = new("forums.thread.moderate");
        /// <summary>The <c>forums.message.moderate</c> key of the outgoing Flash message <c>ModerateMessage</c>.</summary>
        public static readonly MessageKey MessageModerate = new("forums.message.moderate");
        /// <summary>The <c>forums.settings.update</c> key of the outgoing Flash message <c>UpdateForumSettings</c>.</summary>
        public static readonly MessageKey SettingsUpdate = new("forums.settings.update");
        /// <summary>The <c>forums.read_markers.update</c> key of the outgoing Flash message <c>UpdateForumReadMarker</c>.</summary>
        public static readonly MessageKey ReadMarkersUpdate = new("forums.read_markers.update");
        /// <summary>The <c>forums.thread.update</c> key of the outgoing Flash message <c>UpdateThread</c>.</summary>
        public static readonly MessageKey ThreadUpdate = new("forums.thread.update");
        /// <summary>The <c>forums.thread.report</c> key of the outgoing Flash message <c>CallForHelpFromForumThread</c>.</summary>
        public static readonly MessageKey ThreadReport = new("forums.thread.report");
        /// <summary>The <c>forums.message.report</c> key of the outgoing Flash message <c>CallForHelpFromForumMessage</c>.</summary>
        public static readonly MessageKey MessageReport = new("forums.message.report");
    }

    /// <summary>Contains the poll message keys.</summary>
    public static class Polls
    {
        /// <summary>The <c>polls.contents</c> key of the incoming Flash message <c>PollContents</c>.</summary>
        public static readonly MessageKey Contents = new("polls.contents");
        /// <summary>The <c>polls.error</c> key of the incoming Flash message <c>PollError</c>.</summary>
        public static readonly MessageKey Error = new("polls.error");
        /// <summary>The <c>polls.offer</c> key of the incoming Flash message <c>PollOffer</c>.</summary>
        public static readonly MessageKey Offer = new("polls.offer");
        /// <summary>The <c>polls.answer</c> key of the outgoing Flash message <c>PollAnswer</c>.</summary>
        public static readonly MessageKey Answer = new("polls.answer");
        /// <summary>The <c>polls.reject</c> key of the outgoing Flash message <c>PollReject</c>.</summary>
        public static readonly MessageKey Reject = new("polls.reject");
        /// <summary>The <c>polls.start</c> key of the outgoing Flash message <c>PollStart</c>.</summary>
        public static readonly MessageKey Start = new("polls.start");
    }

    /// <summary>Contains the gift message keys.</summary>
    public static class Gifts
    {
        /// <summary>The <c>gifts.wrapping.configuration</c> key of the incoming Flash message <c>GiftWrappingConfiguration</c>.</summary>
        public static readonly MessageKey WrappingConfiguration = new("gifts.wrapping.configuration");
        /// <summary>The <c>gifts.present.opened</c> key of the incoming Flash message <c>PresentOpened</c>.</summary>
        public static readonly MessageKey PresentOpened = new("gifts.present.opened");
        /// <summary>The <c>gifts.club.info</c> key of the incoming Flash message <c>ClubGiftInfo</c>.</summary>
        public static readonly MessageKey ClubInfo = new("gifts.club.info");
        /// <summary>The <c>gifts.club.selected</c> key of the incoming Flash message <c>ClubGiftSelected</c>.</summary>
        public static readonly MessageKey ClubSelected = new("gifts.club.selected");
        /// <summary>The <c>gifts.receiver.not_found</c> key of the incoming Flash message <c>GiftReceiverNotFound</c>.</summary>
        public static readonly MessageKey ReceiverNotFound = new("gifts.receiver.not_found");
        /// <summary>The <c>gifts.club.notification</c> key of the incoming Flash message <c>ClubGiftNotification</c>.</summary>
        public static readonly MessageKey ClubNotification = new("gifts.club.notification");
        /// <summary>The <c>gifts.offer.giftability</c> key of the incoming Flash message <c>IsOfferGiftable</c>.</summary>
        public static readonly MessageKey OfferGiftability = new("gifts.offer.giftability");
        /// <summary>The <c>gifts.new_user.offer</c> key of the incoming Flash message <c>NewUserExperienceGiftOffer</c>.</summary>
        public static readonly MessageKey NewUserOffer = new("gifts.new_user.offer");
        /// <summary>The <c>gifts.new_user.incomplete</c> key of the incoming Flash message <c>NewUserExperienceNotComplete</c>.</summary>
        public static readonly MessageKey NewUserIncomplete = new("gifts.new_user.incomplete");
        /// <summary>The <c>gifts.wrapping.configuration.request</c> key of the outgoing Flash message <c>GetGiftWrappingConfiguration</c>.</summary>
        public static readonly MessageKey WrappingConfigurationRequest = new("gifts.wrapping.configuration.request");
        /// <summary>The <c>gifts.present.open</c> key of the outgoing Flash message <c>PresentOpen</c>.</summary>
        public static readonly MessageKey PresentOpen = new("gifts.present.open");
        /// <summary>The <c>gifts.purchase</c> key of the outgoing Flash message <c>PurchaseFromCatalogAsGift</c>.</summary>
        public static readonly MessageKey Purchase = new("gifts.purchase");
        /// <summary>The <c>gifts.club.info.request</c> key of the outgoing Flash message <c>GetClubGift</c>.</summary>
        public static readonly MessageKey ClubInfoRequest = new("gifts.club.info.request");
        /// <summary>The <c>gifts.club.select</c> key of the outgoing Flash message <c>SelectClubGift</c>.</summary>
        public static readonly MessageKey ClubSelect = new("gifts.club.select");
        /// <summary>The <c>gifts.offer.giftability.request</c> key of the outgoing Flash message <c>GetIsOfferGiftable</c>.</summary>
        public static readonly MessageKey OfferGiftabilityRequest = new("gifts.offer.giftability.request");
        /// <summary>The <c>gifts.new_user.select</c> key of the outgoing Flash message <c>NewUserExperienceGetGifts</c>.</summary>
        public static readonly MessageKey NewUserSelect = new("gifts.new_user.select");
        /// <summary>The <c>gifts.new_user.advance</c> key of the outgoing Flash message <c>NewUserExperienceScriptProceed</c>.</summary>
        public static readonly MessageKey NewUserAdvance = new("gifts.new_user.advance");
    }

    /// <summary>Contains the trading message keys.</summary>
    public static class Trade
    {
        /// <summary>The <c>trade.opened</c> key of the incoming Flash message <c>TradingOpen</c>.</summary>
        public static readonly MessageKey Opened = new("trade.opened");
        /// <summary>The <c>trade.offers</c> key of the incoming Flash message <c>TradingItemList</c>.</summary>
        public static readonly MessageKey Offers = new("trade.offers");
        /// <summary>The <c>trade.acceptance.updated</c> key of the incoming Flash message <c>TradingAccept</c>.</summary>
        public static readonly MessageKey AcceptanceUpdated = new("trade.acceptance.updated");
        /// <summary>The <c>trade.confirmation</c> key of the incoming Flash message <c>TradingConfirmation</c>.</summary>
        public static readonly MessageKey Confirmation = new("trade.confirmation");
        /// <summary>The <c>trade.completed</c> key of the incoming Flash message <c>TradingCompleted</c>.</summary>
        public static readonly MessageKey Completed = new("trade.completed");
        /// <summary>The <c>trade.closed</c> key of the incoming Flash message <c>TradingClose</c>.</summary>
        public static readonly MessageKey Closed = new("trade.closed");
        /// <summary>The <c>trade.open.failed</c> key of the incoming Flash message <c>TradeOpenFailed</c>.</summary>
        public static readonly MessageKey OpenFailed = new("trade.open.failed");
        /// <summary>The <c>trade.nft.offers</c> key of the incoming Flash message <c>TradeNftAssets</c>.</summary>
        public static readonly MessageKey NftOffers = new("trade.nft.offers");
        /// <summary>The <c>trade.nft.inventory</c> key of the incoming Flash message <c>TradeNftAssetInventory</c>.</summary>
        public static readonly MessageKey NftInventory = new("trade.nft.inventory");
        /// <summary>The <c>trade.silver.updated</c> key of the incoming Flash message <c>TradeSilverSet</c>.</summary>
        public static readonly MessageKey SilverUpdated = new("trade.silver.updated");
        /// <summary>The <c>trade.silver.fee</c> key of the incoming Flash message <c>TradeSilverFee</c>.</summary>
        public static readonly MessageKey SilverFee = new("trade.silver.fee");
        /// <summary>The <c>trade.open.request</c> key of the outgoing Flash message <c>OpenTrading</c>.</summary>
        public static readonly MessageKey OpenRequest = new("trade.open.request");
        /// <summary>The <c>trade.items.add</c> key of the outgoing Flash message <c>AddItemsToTrade</c>.</summary>
        public static readonly MessageKey ItemsAdd = new("trade.items.add");
        /// <summary>The <c>trade.item.remove</c> key of the outgoing Flash message <c>RemoveItemFromTrade</c>.</summary>
        public static readonly MessageKey ItemRemove = new("trade.item.remove");
        /// <summary>The <c>trade.accept</c> key of the outgoing Flash message <c>AcceptTrading</c>.</summary>
        public static readonly MessageKey Accept = new("trade.accept");
        /// <summary>The <c>trade.unaccept</c> key of the outgoing Flash message <c>UnacceptTrading</c>.</summary>
        public static readonly MessageKey Unaccept = new("trade.unaccept");
        /// <summary>The <c>trade.confirm</c> key of the outgoing Flash message <c>ConfirmAcceptTrading</c>.</summary>
        public static readonly MessageKey Confirm = new("trade.confirm");
        /// <summary>The <c>trade.close</c> key of the outgoing Flash message <c>CloseTrading</c>.</summary>
        public static readonly MessageKey Close = new("trade.close");
        /// <summary>The <c>trade.nft.inventory.request</c> key of the outgoing Flash message <c>GetNftTradeInventory</c>.</summary>
        public static readonly MessageKey NftInventoryRequest = new("trade.nft.inventory.request");
    }

    /// <summary>Contains the room message keys.</summary>
    public static class Room
    {
        /// <summary>The <c>room.objects</c> key of the incoming Flash message <c>Objects</c>.</summary>
        public static readonly MessageKey Objects = new("room.objects");
        /// <summary>The <c>room.wall_items</c> key of the incoming Flash message <c>Items</c>.</summary>
        public static readonly MessageKey WallItems = new("room.wall_items");
        /// <summary>The <c>room.snapshot.request</c> key of the outgoing Flash message <c>GetGuestRoom</c>.</summary>
        public static readonly MessageKey SnapshotRequest = new("room.snapshot.request");
        /// <summary>The <c>room.snapshot</c> key of the incoming Flash message <c>GetGuestRoomResult</c>.</summary>
        public static readonly MessageKey Snapshot = new("room.snapshot");
        /// <summary>The <c>room.advertisement</c> key of the incoming Flash message <c>Interstitial</c>.</summary>
        public static readonly MessageKey Advertisement = new("room.advertisement");
        /// <summary>The <c>room.staff_pick.update.request</c> key of the outgoing Flash message <c>ToggleStaffPick</c>.</summary>
        public static readonly MessageKey StaffPickUpdateRequest =
            new("room.staff_pick.update.request");
        /// <summary>The <c>room.rating.request</c> key of the outgoing Flash message <c>RateFlat</c>.</summary>
        public static readonly MessageKey RatingRequest = new("room.rating.request");

        /// <summary>Contains the room settings message keys.</summary>
        public static class Settings
        {
            /// <summary>The <c>room.settings.request</c> key of the outgoing Flash message <c>GetRoomSettings</c>.</summary>
            public static readonly MessageKey Request = new("room.settings.request");
            /// <summary>The <c>room.settings.snapshot</c> key of the incoming Flash message <c>RoomSettingsData</c>.</summary>
            public static readonly MessageKey Snapshot = new("room.settings.snapshot");
            /// <summary>The <c>room.settings.request.failed</c> key of the incoming Flash message <c>RoomSettingsError</c>.</summary>
            public static readonly MessageKey RequestFailed = new("room.settings.request.failed");
            /// <summary>The <c>room.settings.save</c> key of the outgoing Flash message <c>SaveRoomSettings</c>.</summary>
            public static readonly MessageKey Save = new("room.settings.save");
            /// <summary>The <c>room.settings.save.succeeded</c> key of the incoming Flash message <c>RoomSettingsSaved</c>.</summary>
            public static readonly MessageKey SaveSucceeded = new("room.settings.save.succeeded");
            /// <summary>The <c>room.settings.save.failed</c> key of the incoming Flash message <c>RoomSettingsSaveError</c>.</summary>
            public static readonly MessageKey SaveFailed = new("room.settings.save.failed");
        }

        /// <summary>Contains the room access message keys.</summary>
        public static class Access
        {
            /// <summary>The <c>room.access.open.request</c> key of the outgoing Flash message <c>OpenFlatConnection</c>.</summary>
            public static readonly MessageKey OpenRequest = new("room.access.open.request");
            /// <summary>The <c>room.access.open.confirmed</c> key of the incoming Flash message <c>OpenConnection</c>.</summary>
            public static readonly MessageKey OpenConfirmed = new("room.access.open.confirmed");
            /// <summary>The <c>room.access.doorbell</c> key of the incoming Flash message <c>Doorbell</c>.</summary>
            public static readonly MessageKey Doorbell = new("room.access.doorbell");
            /// <summary>The <c>room.access.doorbell.answer</c> key of the outgoing Flash message <c>LetUserIn</c>.</summary>
            public static readonly MessageKey DoorbellAnswer = new("room.access.doorbell.answer");
            /// <summary>The <c>room.access.queue.status</c> key of the incoming Flash message <c>RoomQueueStatus</c>.</summary>
            public static readonly MessageKey QueueStatus = new("room.access.queue.status");
            /// <summary>The <c>room.access.granted</c> key of the incoming Flash message <c>FlatAccessible</c>.</summary>
            public static readonly MessageKey Granted = new("room.access.granted");
            /// <summary>The <c>room.access.denied</c> key of the incoming Flash message <c>FlatAccessDenied</c>.</summary>
            public static readonly MessageKey Denied = new("room.access.denied");
            /// <summary>The <c>room.access.not_found</c> key of the incoming Flash message <c>NoSuchFlat</c>.</summary>
            public static readonly MessageKey NotFound = new("room.access.not_found");
            /// <summary>The <c>room.access.connection_failed</c> key of the incoming Flash message <c>CantConnect</c>.</summary>
            public static readonly MessageKey ConnectionFailed = new("room.access.connection_failed");
        }

        /// <summary>Contains the room lifecycle message keys.</summary>
        public static class Lifecycle
        {
            /// <summary>The <c>room.lifecycle.ready</c> key of the incoming Flash message <c>RoomReady</c>.</summary>
            public static readonly MessageKey Ready = new("room.lifecycle.ready");
            /// <summary>The <c>room.lifecycle.entry</c> key of the incoming Flash message <c>RoomEntryInfo</c>.</summary>
            public static readonly MessageKey Entry = new("room.lifecycle.entry");
            /// <summary>The <c>room.lifecycle.forward</c> key of the incoming Flash message <c>RoomForward</c>.</summary>
            public static readonly MessageKey Forward = new("room.lifecycle.forward");
            /// <summary>The <c>room.lifecycle.connection_closed</c> key of the incoming Flash message <c>CloseConnection</c>.</summary>
            public static readonly MessageKey ConnectionClosed = new("room.lifecycle.connection_closed");
            /// <summary>The <c>room.lifecycle.quit</c> key of the outgoing Flash message <c>Quit</c>.</summary>
            public static readonly MessageKey Quit = new("room.lifecycle.quit");
        }

        /// <summary>Contains the room environment message keys.</summary>
        public static class Environment
        {
            /// <summary>The <c>room.environment.entry_tile</c> key of the incoming Flash message <c>RoomEntryTile</c>.</summary>
            public static readonly MessageKey EntryTile = new("room.environment.entry_tile");
            /// <summary>The <c>room.environment.property</c> key of the incoming Flash message <c>RoomProperty</c>.</summary>
            public static readonly MessageKey Property = new("room.environment.property");
            /// <summary>The <c>room.environment.visualization</c> key of the incoming Flash message <c>RoomVisualizationSettings</c>.</summary>
            public static readonly MessageKey Visualization = new("room.environment.visualization");
            /// <summary>The <c>room.environment.chat_settings</c> key of the incoming Flash message <c>RoomChatSettings</c>.</summary>
            public static readonly MessageKey ChatSettings = new("room.environment.chat_settings");
            /// <summary>The <c>room.environment.floor_plan</c> key of the incoming Flash message <c>FloorHeightmap</c>.</summary>
            public static readonly MessageKey FloorPlan = new("room.environment.floor_plan");
            /// <summary>The incoming AreaHide live region update.</summary>
            public static readonly MessageKey AreaHide = new("room.environment.area_hide");
            /// <summary>The outgoing SetAreaHideData editor update.</summary>
            public static readonly MessageKey AreaHideSet = new("room.environment.area_hide.set");
        }

        /// <summary>Contains the room chat message keys.</summary>
        public static class Chat
        {
            /// <summary>The <c>room.chat.talk</c> key of the incoming Flash message <c>Chat</c>.</summary>
            public static readonly MessageKey Talk = new("room.chat.talk");
            /// <summary>The <c>room.chat.shout</c> key of the incoming Flash message <c>Shout</c>.</summary>
            public static readonly MessageKey Shout = new("room.chat.shout");
            /// <summary>The <c>room.chat.whisper</c> key of the incoming Flash message <c>Whisper</c>.</summary>
            public static readonly MessageKey Whisper = new("room.chat.whisper");
            /// <summary>The <c>room.chat.whisper.send</c> key of the outgoing Flash message <c>Whisper</c>.</summary>
            public static readonly MessageKey WhisperSend = new("room.chat.whisper.send");
            /// <summary>The <c>room.chat.special_system</c> key of the incoming Flash message <c>SpecialSystemChat</c>.</summary>
            public static readonly MessageKey SpecialSystem = new("room.chat.special_system");
            /// <summary>The <c>room.chat.talk.send</c> key of the outgoing Flash message <c>Chat</c>.</summary>
            public static readonly MessageKey TalkSend = new("room.chat.talk.send");
            /// <summary>The <c>room.chat.shout.send</c> key of the outgoing Flash message <c>Shout</c>.</summary>
            public static readonly MessageKey ShoutSend = new("room.chat.shout.send");
        }

        /// <summary>Contains the room rights message keys.</summary>
        public static class Authority
        {
            /// <summary>The <c>room.authority.controllers.request</c> key of the outgoing Flash message <c>GetFlatControllers</c>.</summary>
            public static readonly MessageKey ControllersRequest = new("room.authority.controllers.request");
            /// <summary>The <c>room.authority.controllers.snapshot</c> key of the incoming Flash message <c>FlatControllers</c>.</summary>
            public static readonly MessageKey ControllersSnapshot = new("room.authority.controllers.snapshot");
            /// <summary>The <c>room.authority.controller.grant.request</c> key of the outgoing Flash message <c>AssignRights</c>.</summary>
            public static readonly MessageKey ControllerGrantRequest = new("room.authority.controller.grant.request");
            /// <summary>The <c>room.authority.controller.granted</c> key of the incoming Flash message <c>YouAreController</c>.</summary>
            public static readonly MessageKey ControllerGranted = new("room.authority.controller.granted");
            /// <summary>The <c>room.authority.controller.revoked</c> key of the incoming Flash message <c>YouAreNotController</c>.</summary>
            public static readonly MessageKey ControllerRevoked = new("room.authority.controller.revoked");
            /// <summary>The <c>room.authority.owner</c> key of the incoming Flash message <c>YouAreOwner</c>.</summary>
            public static readonly MessageKey Owner = new("room.authority.owner");
            /// <summary>The <c>room.authority.spectator.granted</c> key of the incoming Flash message <c>YouAreSpectator</c>.</summary>
            public static readonly MessageKey SpectatorGranted = new("room.authority.spectator.granted");
            /// <summary>The <c>room.authority.spectator.revoked</c> key of the incoming Flash message <c>YouAreNotSpectator</c>.</summary>
            public static readonly MessageKey SpectatorRevoked = new("room.authority.spectator.revoked");
        }

        /// <summary>Contains the room occupant message keys.</summary>
        public static class Occupants
        {
            /// <summary>The <c>room.occupants.snapshot</c> key of the incoming Flash message <c>Users</c>.</summary>
            public static readonly MessageKey Snapshot = new("room.occupants.snapshot");
            /// <summary>The <c>room.occupants.removed</c> key of the incoming Flash message <c>UserRemove</c>.</summary>
            public static readonly MessageKey Removed = new("room.occupants.removed");
            /// <summary>The <c>room.occupants.status</c> key of the incoming Flash message <c>UserUpdate</c>.</summary>
            public static readonly MessageKey Status = new("room.occupants.status");
            /// <summary>The <c>room.occupants.respect</c> key of the incoming Flash message <c>RespectNotification</c>.</summary>
            public static readonly MessageKey Respect = new("room.occupants.respect");
            /// <summary>The <c>room.occupants.respect.request</c> key of the outgoing Flash message <c>RespectUser</c>.</summary>
            public static readonly MessageKey RespectRequest = new("room.occupants.respect.request");

            /// <summary>Contains the avatar action message keys.</summary>
            public static class Action
            {
                /// <summary>The <c>room.occupants.action.dance</c> key of the incoming Flash message <c>Dance</c>.</summary>
                public static readonly MessageKey Dance = new("room.occupants.action.dance");
                /// <summary>The <c>room.occupants.action.dance.request</c> key of the outgoing Flash message <c>Dance</c>.</summary>
                public static readonly MessageKey DanceRequest = new("room.occupants.action.dance.request");
                /// <summary>The <c>room.occupants.action.sign.request</c> key of the outgoing Flash message <c>Sign</c>.</summary>
                public static readonly MessageKey SignRequest = new("room.occupants.action.sign.request");
                /// <summary>The <c>room.occupants.action.effect</c> key of the incoming Flash message <c>AvatarEffect</c>.</summary>
                public static readonly MessageKey Effect = new("room.occupants.action.effect");
                /// <summary>The <c>room.occupants.action.effect.selection.request</c> key of the outgoing Flash message <c>AvatarEffectSelected</c>.</summary>
                public static readonly MessageKey EffectSelectionRequest = new("room.occupants.action.effect.selection.request");
                /// <summary>The <c>room.occupants.action.posture.request</c> key of the outgoing Flash message <c>ChangePosture</c>.</summary>
                public static readonly MessageKey PostureRequest = new("room.occupants.action.posture.request");
                /// <summary>The <c>room.occupants.action.carry</c> key of the incoming Flash message <c>CarryObject</c>.</summary>
                public static readonly MessageKey Carry = new("room.occupants.action.carry");
                /// <summary>The <c>room.occupants.action.sleep</c> key of the incoming Flash message <c>Sleep</c>.</summary>
                public static readonly MessageKey Sleep = new("room.occupants.action.sleep");
                /// <summary>The <c>room.occupants.action.typing</c> key of the incoming Flash message <c>UserTyping</c>.</summary>
                public static readonly MessageKey Typing = new("room.occupants.action.typing");
                /// <summary>The <c>room.occupants.action.expression</c> key of the incoming Flash message <c>Expression</c>.</summary>
                public static readonly MessageKey Expression = new("room.occupants.action.expression");
                /// <summary>The <c>room.occupants.action.expression.request</c> key of the outgoing Flash message <c>AvatarExpression</c>.</summary>
                public static readonly MessageKey ExpressionRequest = new("room.occupants.action.expression.request");
            }

            /// <summary>Contains the occupant identity message keys.</summary>
            public static class Identity
            {
                /// <summary>The <c>room.occupants.identity.appearance</c> key of the incoming Flash message <c>UserChange</c>.</summary>
                public static readonly MessageKey Appearance = new("room.occupants.identity.appearance");
                /// <summary>The <c>room.occupants.identity.name</c> key of the incoming Flash message <c>UserNameChanged</c>.</summary>
                public static readonly MessageKey Name = new("room.occupants.identity.name");
                /// <summary>The <c>room.occupants.identity.favorite_group</c> key of the incoming Flash message <c>FavoriteMembershipUpdate</c>, also named <c>FavouriteMembershipUpdate</c>.</summary>
                public static readonly MessageKey FavoriteGroup = new("room.occupants.identity.favorite_group");
            }

            /// <summary>Contains the room pet message keys.</summary>
            public static class Pet
            {
                /// <summary>The <c>room.occupants.pet.info.request</c> key of the outgoing Flash message <c>GetPetInfo</c>.</summary>
                public static readonly MessageKey InfoRequest = new("room.occupants.pet.info.request");
                /// <summary>The <c>room.occupants.pet.info</c> key of the incoming Flash message <c>PetInfo</c>.</summary>
                public static readonly MessageKey Info = new("room.occupants.pet.info");
                /// <summary>The <c>room.occupants.pet.figure</c> key of the incoming Flash message <c>PetFigureUpdate</c>.</summary>
                public static readonly MessageKey Figure = new("room.occupants.pet.figure");
                /// <summary>The <c>room.occupants.pet.status</c> key of the incoming Flash message <c>PetStatusUpdate</c>.</summary>
                public static readonly MessageKey Status = new("room.occupants.pet.status");
                /// <summary>The <c>room.occupants.pet.level</c> key of the incoming Flash message <c>PetLevelUpdate</c>.</summary>
                public static readonly MessageKey Level = new("room.occupants.pet.level");
                /// <summary>The <c>room.occupants.pet.respect</c> key of the incoming Flash message <c>PetRespectNotification</c>.</summary>
                public static readonly MessageKey Respect = new("room.occupants.pet.respect");
                /// <summary>The <c>room.occupants.pet.respect.request</c> key of the outgoing Flash message <c>RespectPet</c>.</summary>
                public static readonly MessageKey RespectRequest = new("room.occupants.pet.respect.request");
                /// <summary>The <c>room.occupants.pet.mount.request</c> key of the outgoing Flash message <c>MountPet</c>.</summary>
                public static readonly MessageKey MountRequest = new("room.occupants.pet.mount.request");
                /// <summary>The <c>room.occupants.pet.remove.request</c> key of the outgoing Flash message <c>RemovePetFromFlat</c>.</summary>
                public static readonly MessageKey RemoveRequest = new("room.occupants.pet.remove.request");
            }

            /// <summary>Contains the room bot message keys.</summary>
            public static class Bot
            {
                /// <summary>The <c>room.occupants.bot.remove.request</c> key of the outgoing Flash message <c>RemoveBotFromFlat</c>.</summary>
                public static readonly MessageKey RemoveRequest = new("room.occupants.bot.remove.request");
            }
        }

        /// <summary>Contains the hand item message keys.</summary>
        public static class HandItem
        {
            /// <summary>The <c>room.hand_item.received</c> key of the incoming Flash message <c>HandItemReceived</c>.</summary>
            public static readonly MessageKey Received = new("room.hand_item.received");
            /// <summary>The <c>room.hand_item.drop</c> key of the outgoing Flash message <c>DropCarryItem</c>.</summary>
            public static readonly MessageKey Drop = new("room.hand_item.drop");
            /// <summary>The <c>room.hand_item.pass</c> key of the outgoing Flash message <c>PassCarryItem</c>.</summary>
            public static readonly MessageKey Pass = new("room.hand_item.pass");
        }

        /// <summary>Contains the floor item message keys.</summary>
        public static class FloorItem
        {
            /// <summary>The <c>room.floor_item.use</c> key of the outgoing Flash message <c>UseFurniture</c>.</summary>
            public static readonly MessageKey Use = new("room.floor_item.use");
            /// <summary>The <c>room.floor_item.move</c> key of the outgoing Flash message <c>MoveObject</c>.</summary>
            public static readonly MessageKey Move = new("room.floor_item.move");
            /// <summary>The <c>room.floor_item.dice.throw</c> key of the outgoing Flash message <c>ThrowDice</c>.</summary>
            public static readonly MessageKey ThrowDice = new("room.floor_item.dice.throw");
            /// <summary>The <c>room.floor_item.dice.off</c> key of the outgoing Flash message <c>DiceOff</c>.</summary>
            public static readonly MessageKey DiceOff = new("room.floor_item.dice.off");
            /// <summary>The <c>room.floor_item.dice_value</c> key of the incoming Flash message <c>DiceValue</c>.</summary>
            public static readonly MessageKey DiceValue = new("room.floor_item.dice_value");
            /// <summary>The <c>room.floor_item.one_way_door_status</c> key of the incoming Flash message <c>OneWayDoorStatus</c>.</summary>
            public static readonly MessageKey OneWayDoorStatus = new("room.floor_item.one_way_door_status");
            /// <summary>The <c>room.floor_item.one_way_door.enter</c> key of the outgoing Flash message <c>EnterOneWayDoor</c>.</summary>
            public static readonly MessageKey OneWayDoorEnter = new("room.floor_item.one_way_door.enter");
            /// <summary>The <c>room.floor_item.added</c> key of the incoming Flash message <c>ObjectAdd</c>.</summary>
            public static readonly MessageKey Added = new("room.floor_item.added");
            /// <summary>The <c>room.floor_item.removed</c> key of the incoming Flash message <c>ObjectRemove</c>.</summary>
            public static readonly MessageKey Removed = new("room.floor_item.removed");
            /// <summary>The <c>room.floor_item.removed_multiple</c> key of the incoming Flash message <c>ObjectRemoveMultiple</c>.</summary>
            public static readonly MessageKey RemovedMultiple = new("room.floor_item.removed_multiple");
            /// <summary>The <c>room.floor_item.updated</c> key of the incoming Flash message <c>ObjectUpdate</c>.</summary>
            public static readonly MessageKey Updated = new("room.floor_item.updated");
            /// <summary>The <c>room.floor_item.data_updated</c> key of the incoming Flash message <c>ObjectDataUpdate</c>.</summary>
            public static readonly MessageKey DataUpdated = new("room.floor_item.data_updated");
            /// <summary>The <c>room.floor_item.data_batch_updated</c> key of the incoming Flash message <c>ObjectsDataUpdate</c>.</summary>
            public static readonly MessageKey DataBatchUpdated = new("room.floor_item.data_batch_updated");
        }

        /// <summary>Contains the wall item message keys.</summary>
        public static class WallItem
        {
            /// <summary>The <c>room.wall_item.use</c> key of the outgoing Flash message <c>UseWallItem</c>.</summary>
            public static readonly MessageKey Use = new("room.wall_item.use");
            /// <summary>The <c>room.wall_item.move</c> key of the outgoing Flash message <c>MoveWallItem</c>.</summary>
            public static readonly MessageKey Move = new("room.wall_item.move");
            /// <summary>The <c>room.wall_item.remove</c> key of the outgoing Flash message <c>RemoveItem</c>.</summary>
            public static readonly MessageKey Remove = new("room.wall_item.remove");
            /// <summary>The <c>room.wall_item.sticky_data.set</c> key of the outgoing Flash message <c>SetItemData</c>.</summary>
            public static readonly MessageKey StickyDataSet = new("room.wall_item.sticky_data.set");
            /// <summary>The <c>room.wall_item.sticky_data.request</c> key of the outgoing Flash message <c>GetItemData</c>.</summary>
            public static readonly MessageKey StickyDataRequest = new("room.wall_item.sticky_data.request");
            /// <summary>The <c>room.wall_item.sticky_data</c> key of the incoming Flash message <c>ItemDataUpdate</c>.</summary>
            public static readonly MessageKey StickyData = new("room.wall_item.sticky_data");
            /// <summary>The <c>room.wall_item.post_it.place</c> key of the outgoing Flash message <c>PlacePostIt</c>.</summary>
            public static readonly MessageKey PostItPlace = new("room.wall_item.post_it.place");
            /// <summary>The <c>room.wall_item.spam_post_it.add</c> key of the outgoing Flash message <c>AddSpamWallPostIt</c>.</summary>
            public static readonly MessageKey SpamPostItAdd = new("room.wall_item.spam_post_it.add");
            /// <summary>The <c>room.wall_item.added</c> key of the incoming Flash message <c>ItemAdd</c>.</summary>
            public static readonly MessageKey Added = new("room.wall_item.added");
            /// <summary>The <c>room.wall_item.removed</c> key of the incoming Flash message <c>ItemRemove</c>.</summary>
            public static readonly MessageKey Removed = new("room.wall_item.removed");
            /// <summary>The <c>room.wall_item.removed_multiple</c> key of the incoming Flash message <c>ItemRemoveMultiple</c>.</summary>
            public static readonly MessageKey RemovedMultiple = new("room.wall_item.removed_multiple");
            /// <summary>The <c>room.wall_item.updated</c> key of the incoming Flash message <c>ItemUpdate</c>.</summary>
            public static readonly MessageKey Updated = new("room.wall_item.updated");
            /// <summary>The <c>room.wall_item.data_updated</c> key of the incoming Flash message <c>ItemStateUpdate</c>.</summary>
            public static readonly MessageKey DataUpdated = new("room.wall_item.data_updated");
            /// <summary>The <c>room.wall_item.data_batch_updated</c> key of the incoming Flash message <c>ItemsStateUpdate</c>.</summary>
            public static readonly MessageKey DataBatchUpdated = new("room.wall_item.data_batch_updated");
        }

        /// <summary>Contains the heightmap message keys.</summary>
        public static class Heightmap
        {
            /// <summary>The <c>room.heightmap.snapshot</c> key of the incoming Flash message <c>HeightMap</c>.</summary>
            public static readonly MessageKey Snapshot = new("room.heightmap.snapshot");
            /// <summary>The <c>room.heightmap.diff</c> key of the incoming Flash message <c>HeightMapUpdate</c>.</summary>
            public static readonly MessageKey Diff = new("room.heightmap.diff");
        }

        /// <summary>Contains the avatar movement message keys.</summary>
        public static class Movement
        {
            /// <summary>The <c>room.movement.walk</c> key of the outgoing Flash message <c>MoveAvatar</c>.</summary>
            public static readonly MessageKey Walk = new("room.movement.walk");
            /// <summary>The <c>room.movement.look_to</c> key of the outgoing Flash message <c>LookTo</c>.</summary>
            public static readonly MessageKey LookTo = new("room.movement.look_to");
            /// <summary>The <c>room.movement.slide</c> key of the incoming Flash message <c>SlideObjectBundle</c>.</summary>
            public static readonly MessageKey Slide = new("room.movement.slide");
            /// <summary>The <c>room.movement.wired</c> key of the incoming Flash message <c>WiredMovements</c>.</summary>
            public static readonly MessageKey Wired = new("room.movement.wired");
        }

        /// <summary>Contains the typing indicator message keys.</summary>
        public static class Typing
        {
            /// <summary>The <c>room.typing.start</c> key of the outgoing Flash message <c>StartTyping</c>.</summary>
            public static readonly MessageKey Start = new("room.typing.start");
            /// <summary>The <c>room.typing.cancel</c> key of the outgoing Flash message <c>CancelTyping</c>.</summary>
            public static readonly MessageKey Cancel = new("room.typing.cancel");
        }

        /// <summary>Contains the room item message keys.</summary>
        public static class Item
        {
            /// <summary>The <c>room.item.place</c> key of the outgoing Flash message <c>PlaceObject</c>.</summary>
            public static readonly MessageKey Place = new("room.item.place");
            /// <summary>The <c>room.item.click</c> key of the outgoing Flash message <c>ClickFurni</c>.</summary>
            public static readonly MessageKey Click = new("room.item.click");
            /// <summary>The <c>room.item.pickup</c> key of the outgoing Flash message <c>PickupObject</c>.</summary>
            public static readonly MessageKey Pickup = new("room.item.pickup");
            /// <summary>The <c>room.item.pickup.confirmation</c> key of the incoming Flash message <c>ObjectRemoveConfirm</c>.</summary>
            public static readonly MessageKey PickupConfirmation = new("room.item.pickup.confirmation");
        }

        /// <summary>Contains the room moderation message keys.</summary>
        public static class Moderation
        {
            /// <summary>The <c>room.moderation.bans.request</c> key of the outgoing Flash message <c>GetBannedUsersFromRoom</c>.</summary>
            public static readonly MessageKey BansRequest = new("room.moderation.bans.request");
            /// <summary>The <c>room.moderation.bans.snapshot</c> key of the incoming Flash message <c>BannedUsersFromRoom</c>.</summary>
            public static readonly MessageKey BansSnapshot = new("room.moderation.bans.snapshot");
            /// <summary>The <c>room.moderation.user.unbanned</c> key of the incoming Flash message <c>UserUnbannedFromRoom</c>.</summary>
            public static readonly MessageKey UserUnbanned = new("room.moderation.user.unbanned");
            /// <summary>The <c>room.moderation.mute</c> key of the outgoing Flash message <c>MuteUser</c>.</summary>
            public static readonly MessageKey Mute = new("room.moderation.mute");
            /// <summary>The <c>room.moderation.kick</c> key of the outgoing Flash message <c>KickUser</c>.</summary>
            public static readonly MessageKey Kick = new("room.moderation.kick");
            /// <summary>The <c>room.moderation.ban</c> key of the outgoing Flash message <c>BanUserWithDuration</c>.</summary>
            public static readonly MessageKey Ban = new("room.moderation.ban");
            /// <summary>The <c>room.moderation.unban</c> key of the outgoing Flash message <c>UnbanUserFromRoom</c>.</summary>
            public static readonly MessageKey Unban = new("room.moderation.unban");
        }
    }
}
