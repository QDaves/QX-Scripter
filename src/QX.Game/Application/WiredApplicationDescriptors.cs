using Qx.Model.Wired;
using Qx.Protocol;

namespace Qx.Game.Application;

internal static class WiredApplicationDescriptors
{
    private static readonly ApplicationExposure event_exposure =
        ApplicationExposure.Ui | ApplicationExposure.Cli | ApplicationExposure.Scripting;

    public static ApplicationDescriptor State { get; } = new(
        ApplicationMemberIds.WiredState,
        "Wired state",
        "Reads the immutable generation and revision bound Wired room state.",
        ApplicationMemberKind.Query,
        ApplicationExposure.All,
        typeof(WiredStateRequest),
        typeof(WiredStateView),
        [
            new("chest_offset", typeof(int), false, 0, "Zero-based first Wired chest index.", new(Minimum: 0)),
            new("chest_limit", typeof(int), false, 5, "Maximum Wired chests returned.", new(Minimum: 0, Maximum: 10)),
            new("item_offset", typeof(int), false, 0, "Zero-based first item index within every returned chest.", new(Minimum: 0)),
            new("item_limit", typeof(int), false, 20, "Maximum items returned per chest.", new(Minimum: 0, Maximum: 50))
        ],
        messages: StateMessages(),
        tool_hints: new(true, false, true, false),
        invocation_scope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor ConfigurationGet { get; } = Call<
        WiredConfigurationGetRequest,
        WiredConfigurationSnapshot>(
        ApplicationMemberIds.WiredConfigurationGet,
        "Get Wired configuration",
        "Requests and returns the exact trigger, action, condition, selector, add-on or variable configuration for one furni.",
        [
            new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint()),
            TimeoutParameter()
        ],
        [
            Send(MessageKeys.Wired.Configuration.OpenRequest),
            Observe(MessageKeys.Wired.Configuration.Trigger, false),
            Observe(MessageKeys.Wired.Configuration.Action, false),
            Observe(MessageKeys.Wired.Configuration.Condition, false),
            Observe(MessageKeys.Wired.Configuration.Selector, false),
            Observe(MessageKeys.Wired.Configuration.Addon, false),
            Observe(MessageKeys.Wired.Configuration.Variable, false)
        ],
        ReadHints());

    public static ApplicationDescriptor ConfigurationOpen { get; } = SendCall<
        WiredConfigurationOpenRequest>(
        ApplicationMemberIds.WiredConfigurationOpen,
        "Open Wired configuration",
        "Requests one Wired furni configuration without waiting for its kind-specific response.",
        MessageKeys.Wired.Configuration.OpenRequest,
        [new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint())],
        ReadHints());

    public static ApplicationDescriptor ConfigurationSnapshotApply { get; } = SendCall<
        WiredConfigurationApplySnapshotRequest>(
        ApplicationMemberIds.WiredConfigurationSnapshotApply,
        "Apply Wired snapshot",
        "Stores the current state of one Wired furni as its restore snapshot.",
        MessageKeys.Wired.Configuration.ApplySnapshot,
        [new("furni_id", typeof(Id), true, null, "Positive Wired furni identifier.", IdConstraint())],
        WriteHints(false, true));

    public static ApplicationDescriptor ConfigurationTriggerSave { get; } = Save<
        WiredTriggerSaveRequest>(
        ApplicationMemberIds.WiredConfigurationTriggerSave,
        "Save Wired trigger",
        "Replaces a trigger configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.TriggerUpdate,
        typeof(UpdateTrigger));

    public static ApplicationDescriptor ConfigurationActionSave { get; } = Save<
        WiredActionSaveRequest>(
        ApplicationMemberIds.WiredConfigurationActionSave,
        "Save Wired action",
        "Replaces an action configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.ActionUpdate,
        typeof(UpdateAction));

    public static ApplicationDescriptor ConfigurationConditionSave { get; } = Save<
        WiredConditionSaveRequest>(
        ApplicationMemberIds.WiredConfigurationConditionSave,
        "Save Wired condition",
        "Replaces a condition configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.ConditionUpdate,
        typeof(UpdateCondition));

    public static ApplicationDescriptor ConfigurationSelectorSave { get; } = Save<
        WiredSelectorSaveRequest>(
        ApplicationMemberIds.WiredConfigurationSelectorSave,
        "Save Wired selector",
        "Replaces a selector configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.SelectorUpdate,
        typeof(UpdateSelector));

    public static ApplicationDescriptor ConfigurationAddonSave { get; } = Save<
        WiredAddonSaveRequest>(
        ApplicationMemberIds.WiredConfigurationAddonSave,
        "Save Wired add-on",
        "Replaces an add-on configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.AddonUpdate,
        typeof(UpdateAddon));

    public static ApplicationDescriptor ConfigurationVariableSave { get; } = Save<
        WiredVariableSaveRequest>(
        ApplicationMemberIds.WiredConfigurationVariableSave,
        "Save Wired variable",
        "Replaces a variable-box configuration and returns the hotel save or validation result.",
        MessageKeys.Wired.Configuration.VariableUpdate,
        typeof(UpdateVariable));

    public static ApplicationDescriptor VariablesHashGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredAllVariablesHash>(
        ApplicationMemberIds.WiredVariablesHashGet,
        "Get Wired variables hash",
        "Returns the room-wide hash used to detect Wired variable definition changes.",
        MessageKeys.Wired.Variables.HashRequest,
        MessageKeys.Wired.Variables.Hash,
        [TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor VariablesDifferencesGet { get; } = RequestResponse<
        WiredVariableDifferencesRequest,
        WiredVariableDifferencesSnapshot>(
        ApplicationMemberIds.WiredVariablesDifferencesGet,
        "Get Wired variable differences",
        "Returns one correlated variable-definition difference chunk for the supplied cache hashes.",
        MessageKeys.Wired.Variables.DifferencesRequest,
        MessageKeys.Wired.Variables.Differences,
        [
            new("cache", typeof(IReadOnlyList<VariableHashEntry>), false, null, "Known variable identifiers and per-variable hashes."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor VariablesList { get; } = RequestResponse<
        WiredVariableListRequest,
        WiredVariableCollectionSnapshot>(
        ApplicationMemberIds.WiredVariablesList,
        "List Wired variables",
        "Runs the chunked difference protocol to completion and returns an immutable full variable-definition snapshot.",
        MessageKeys.Wired.Variables.DifferencesRequest,
        MessageKeys.Wired.Variables.Differences,
        [
            new("maximum_chunks", typeof(int), false, 64, "Fail-closed upper bound for difference chunks.", new(Minimum: 1, Maximum: 256)),
            TimeoutParameter(30000)
        ],
        ReadHints());

    public static ApplicationDescriptor VariablesObjectGet { get; } = RequestResponse<
        WiredVariablesObjectRequest,
        WiredVariablesObjectSnapshot>(
        ApplicationMemberIds.WiredVariablesObjectGet,
        "Inspect Wired object variables",
        "Returns the Wired variable values held by one furni, room user or global scope.",
        MessageKeys.Wired.Variables.ObjectRequest,
        MessageKeys.Wired.Variables.Object,
        [
            new("target", typeof(WiredTarget), true, null, "Furni, user or global variable target."),
            new("object_id", typeof(int), true, null, "Furni identifier, room user index or zero for global scope."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor VariablesHoldersGet { get; } = RequestResponse<
        WiredVariableHoldersRequest,
        WiredVariableHoldersSnapshot>(
        ApplicationMemberIds.WiredVariablesHoldersGet,
        "Get Wired variable holders",
        "Returns one variable definition and every object currently holding its value.",
        MessageKeys.Wired.Variables.HoldersRequest,
        MessageKeys.Wired.Variables.Holders,
        [VariableIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor VariablesPermanentGet { get; } = RequestResponse<
        WiredPermanentVariablesRequest,
        WiredPermanentVariablesSnapshot>(
        ApplicationMemberIds.WiredVariablesPermanentGet,
        "Get permanent Wired variables",
        "Returns permanent variable storage and timestamps for one entity.",
        MessageKeys.Wired.Variables.PermanentRequest,
        MessageKeys.Wired.Variables.Permanent,
        [EntityTypeParameter(), EntityIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor VariablesOwnersGet { get; } = RequestResponse<
        WiredVariableOwnersRequest,
        WiredVariableOwnersSnapshot>(
        ApplicationMemberIds.WiredVariablesOwnersGet,
        "Get Wired variable owners",
        "Returns one correlated page of entities that own a permanent variable.",
        MessageKeys.Wired.Variables.OwnersRequest,
        MessageKeys.Wired.Variables.Owners,
        [
            VariableIdParameter(),
            new("page", typeof(int), false, 1, "One-based result page.", new(Minimum: 1)),
            new("page_size", typeof(int), false, 50, "Rows requested from the hotel.", new(Minimum: 1, Maximum: 250)),
            new("user_type_filter", typeof(int), false, 0, "Hotel entity-type filter."),
            new("sort_type_filter", typeof(int), false, -1, "Hotel sort-order filter."),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor VariablesObjectSet { get; } = SendCall<
        WiredObjectVariableSetRequest>(
        ApplicationMemberIds.WiredVariablesObjectSet,
        "Set Wired object variable",
        "Writes, creates or deletes one variable value on a furni, user or global target.",
        MessageKeys.Wired.Variables.ObjectValueSet,
        VariableSetParameters(false),
        WriteHints(true, false));

    public static ApplicationDescriptor VariablesPermanentSet { get; } = RequestResponse<
        WiredPermanentVariableSetRequest,
        WiredSetUserPermanentVariableResult>(
        ApplicationMemberIds.WiredVariablesPermanentSet,
        "Set permanent Wired variable",
        "Writes, creates or deletes one permanent variable and waits for the verified Flash result.",
        MessageKeys.Wired.Variables.PermanentValueSet,
        MessageKeys.Wired.Variables.PermanentValueSetResult,
        [.. VariableSetParameters(true), TimeoutParameter()],
        WriteHints(true, false));

    public static ApplicationDescriptor VariablesPermanentSetSend { get; } = SendCall<
        WiredPermanentVariableSendRequest>(
        ApplicationMemberIds.WiredVariablesPermanentSetSend,
        "Send permanent Wired variable update",
        "Sends a permanent variable update without waiting for its result.",
        MessageKeys.Wired.Variables.PermanentValueSet,
        VariableSetParameters(true),
        WriteHints(true, false));

    public static ApplicationDescriptor RoomSettingsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredRoomSettings>(
        ApplicationMemberIds.WiredRoomSettingsGet,
        "Get Wired room settings",
        "Returns the room's modify mask, read mask and Wired timezone.",
        MessageKeys.Wired.Room.SettingsRequest,
        MessageKeys.Wired.Room.Settings,
        [TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor RoomSettingsSet { get; } = RequestResponse<
        WiredRoomSettingsSetRequest,
        WiredRoomSettings>(
        ApplicationMemberIds.WiredRoomSettingsSet,
        "Set Wired room settings",
        "Replaces the room's Wired permission masks and timezone and returns the committed settings.",
        MessageKeys.Wired.Room.SettingsUpdate,
        MessageKeys.Wired.Room.Settings,
        [
            new("modify_permission_mask", typeof(int), true, null, "Who may modify Wired in the room."),
            new("read_permission_mask", typeof(int), true, null, "Who may read Wired in the room."),
            new("timezone", typeof(string), true, null, "Wired room timezone.", TextConstraint()),
            TimeoutParameter()
        ],
        WriteHints(true, true));

    public static ApplicationDescriptor RoomStatsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredRoomStats>(
        ApplicationMemberIds.WiredRoomStatsGet,
        "Get Wired room statistics",
        "Returns execution cost, furniture limits and permanent-variable usage for the room.",
        MessageKeys.Wired.Room.StatsRequest,
        MessageKeys.Wired.Room.Stats,
        [TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor RoomLogsGet { get; } = RequestResponse<
        WiredRoomLogsRequest,
        WiredRoomLogs>(
        ApplicationMemberIds.WiredRoomLogsGet,
        "Get Wired room logs",
        "Returns one exactly correlated page of Wired room log entries and filters.",
        MessageKeys.Wired.Room.LogsRequest,
        MessageKeys.Wired.Room.Logs,
        [
            new("page", typeof(int), false, 1, "One-based log page.", new(Minimum: 1)),
            new("page_size", typeof(int), false, 50, "Log rows requested.", new(Minimum: 1, Maximum: 250)),
            new("log_level_filter", typeof(int), false, -1, "Log-level filter or -1."),
            new("log_source_filter", typeof(int), false, -1, "Log-source filter or -1."),
            new("query", typeof(string), false, string.Empty, "Optional log text query.", TextConstraint()),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor RoomErrorLogsGet { get; } = RequestResponse<
        WiredTimeoutRequest,
        WiredErrorLogs>(
        ApplicationMemberIds.WiredRoomErrorLogsGet,
        "Get Wired error logs",
        "Returns the room's aggregated Wired execution errors.",
        MessageKeys.Wired.ErrorLogs.Request,
        MessageKeys.Wired.ErrorLogs.Snapshot,
        [TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor RoomErrorLogsClear { get; } = SendCall<WiredCommandRequest>(
        ApplicationMemberIds.WiredRoomErrorLogsClear,
        "Clear Wired error logs",
        "Clears the room's Wired execution error counters.",
        MessageKeys.Wired.ErrorLogs.Clear,
        [],
        WriteHints(true, true));

    public static ApplicationDescriptor RoomUserClick { get; } = RequestResponse<
        WiredUserClickRequest,
        WiredClickUserResponse>(
        ApplicationMemberIds.WiredRoomUserClick,
        "Click Wired room user",
        "Asks the hotel whether the Wired user menu should open for one room index.",
        MessageKeys.Wired.UserClick.Request,
        MessageKeys.Wired.UserClick.Result,
        [
            new("index", typeof(int), true, null, "Room user index.", new(Minimum: 0)),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor RoomReload { get; } = SendCall<WiredCommandRequest>(
        ApplicationMemberIds.WiredRoomReload,
        "Reload Wired room state",
        "Reloads the room's saved Wired state without rolling back changes.",
        MessageKeys.Wired.Room.Update,
        [],
        WriteHints(false, true));

    public static ApplicationDescriptor RoomRollback { get; } = SendCall<WiredCommandRequest>(
        ApplicationMemberIds.WiredRoomRollback,
        "Roll back Wired room state",
        "Discards room state changes made since the last saved Wired state.",
        MessageKeys.Wired.Room.Update,
        [],
        WriteHints(true, false));

    public static ApplicationDescriptor PreferencesSet { get; } = SendCall<WiredPreferencesSetRequest>(
        ApplicationMemberIds.WiredPreferencesSet,
        "Set Wired preferences",
        "Updates Wired menu, inspection, play-test, whisper, notification and UI preferences.",
        MessageKeys.Wired.Room.PreferencesUpdate,
        [new("preferences", typeof(WiredSetPreferences), true, null, "Complete Wired preference set.")],
        WriteHints(false, true));

    public static ApplicationDescriptor ChestOpen { get; } = SendCall<WiredChestRequest>(
        ApplicationMemberIds.WiredChestOpen,
        "Open Wired chest",
        "Requests the current contents of one Wired chest.",
        MessageKeys.Wired.Chests.OpenRequest,
        [ChestIdParameter()],
        ReadHints());

    public static ApplicationDescriptor ChestClose { get; } = SendCall<WiredChestRequest>(
        ApplicationMemberIds.WiredChestClose,
        "Close Wired chest",
        "Closes one Wired chest for the active session.",
        MessageKeys.Wired.Chests.Close,
        [ChestIdParameter()],
        WriteHints(false, true));

    public static ApplicationDescriptor ChestsLock { get; } = SendCall<WiredChestsLockRequest>(
        ApplicationMemberIds.WiredChestsLock,
        "Lock Wired chests",
        "Locks or unlocks the selected chest scope in the room.",
        MessageKeys.Wired.Chests.LockAll,
        [
            new("locked", typeof(bool), true, null, "Whether the target chests are locked."),
            new("apply_to_all_in_room", typeof(bool), false, false, "Apply to every chest in the room.")
        ],
        WriteHints(true, true));

    public static ApplicationDescriptor ChestUpgrade { get; } = SendCall<WiredChestUpgradeRequest>(
        ApplicationMemberIds.WiredChestUpgrade,
        "Upgrade Wired chest",
        "Purchases one or more capacity upgrades for a Wired chest.",
        MessageKeys.Wired.Chests.Upgrade,
        [
            new("chest_id", typeof(int), true, null, "Positive chest identifier.", new(Minimum: 1)),
            new("upgrade_amount", typeof(int), true, null, "Number of upgrades to buy.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApplicationDescriptor ChestWithdrawAll { get; } = SendCall<WiredChestRequest>(
        ApplicationMemberIds.WiredChestWithdrawAll,
        "Withdraw all from Wired chest",
        "Withdraws every available item or coin from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawAll,
        [ChestIdParameter()],
        WriteHints(true, false));

    public static ApplicationDescriptor ChestWithdrawCoins { get; } = SendCall<
        WiredChestCoinsWithdrawRequest>(
        ApplicationMemberIds.WiredChestWithdrawCoins,
        "Withdraw coins from Wired chest",
        "Withdraws a positive coin amount from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawCoins,
        [
            ChestIdParameter(),
            new("coin_amount", typeof(int), true, null, "Positive number of coins to withdraw.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApplicationDescriptor ChestWithdrawItems { get; } = SendCall<
        WiredChestItemsWithdrawRequest>(
        ApplicationMemberIds.WiredChestWithdrawItems,
        "Withdraw items from Wired chest",
        "Withdraws a furniture type and count from one Wired chest.",
        MessageKeys.Wired.Chests.WithdrawItems,
        [
            ChestIdParameter(),
            new("item_type", typeof(ChestItemType), true, null, "Exact furniture type descriptor."),
            new("count", typeof(int), true, null, "Positive number of items to withdraw.", new(Minimum: 1))
        ],
        WriteHints(true, false));

    public static ApplicationDescriptor ChestAddStart { get; } = SendCall<WiredChestRequest>(
        ApplicationMemberIds.WiredChestAddStart,
        "Start adding to Wired chest",
        "Starts the chest-backed Wired trade used to deposit inventory items.",
        MessageKeys.Wired.Chests.StartAdding,
        [ChestIdParameter()],
        WriteHints(false, false));

    public static ApplicationDescriptor ChestOptionsSet { get; } = SendCall<
        WiredChestOptionsSetRequest>(
        ApplicationMemberIds.WiredChestOptionsSet,
        "Set Wired chest options",
        "Updates lock, auto-lock and capacity options for one Wired chest.",
        MessageKeys.Wired.Chests.OptionsUpdate,
        [new("options", typeof(SetChestOptions), true, null, "Complete chest option set.")],
        WriteHints(true, true));

    public static ApplicationDescriptor ChestPreferencesSet { get; } = SendCall<
        WiredChestPreferencesSetRequest>(
        ApplicationMemberIds.WiredChestPreferencesSet,
        "Set Wired chest preferences",
        "Updates the name, description, state and preview preferences for one Wired chest.",
        MessageKeys.Wired.Chests.PreferencesUpdate,
        [new("preferences", typeof(SetChestPreferences), true, null, "Complete chest preference set.")],
        WriteHints(true, true));

    public static ApplicationDescriptor ChestNotificationPreferencesSet { get; } = SendCall<
        WiredChestNotificationPreferencesSetRequest>(
        ApplicationMemberIds.WiredChestNotificationPreferencesSet,
        "Set Wired chest notification preferences",
        "Updates notification and event flags for one Wired chest.",
        MessageKeys.Wired.Chests.NotificationPreferencesUpdate,
        [new("preferences", typeof(SetChestNotificationPreferences), true, null, "Complete chest notification preference set.")],
        WriteHints(false, true));

    public static ApplicationDescriptor ChestDeposit { get; } = Call<
        WiredChestDepositRequest,
        WiredChestDepositResult>(
        ApplicationMemberIds.WiredChestDeposit,
        "Deposit into Wired chest",
        "Runs one exclusive session-bound chest trade through open, offer, acceptance, confirmation and exact contents update.",
        [
            ChestIdParameter(),
            new("inventory_ids", typeof(IReadOnlyList<Id>), true, null, "Distinct non-zero signed 32-bit inventory item identifiers.", new(MinItems: 1, MaxItems: 1000)),
            TimeoutParameter(30000)
        ],
        [
            Send(MessageKeys.Wired.Chests.OpenRequest),
            Observe(MessageKeys.Wired.Chests.ItemsChunk),
            Send(MessageKeys.Wired.Chests.StartAdding),
            Observe(MessageKeys.Wired.Trade.Initiated),
            Send(MessageKeys.Wired.Trade.ItemsUpdate),
            Observe(MessageKeys.Wired.Trade.ItemsUpdated),
            Send(MessageKeys.Wired.Trade.Confirm),
            Observe(MessageKeys.Wired.Trade.Completed),
            Observe(MessageKeys.Wired.Trade.Cancelled),
            Observe(MessageKeys.Wired.Transaction.Failed),
            Observe(MessageKeys.Wired.Chests.ItemsUpdated),
            Send(MessageKeys.Wired.Trade.Cancel)
        ],
        WriteHints(true, false));

    public static ApplicationDescriptor TransactionChestLogsGet { get; } = RequestResponse<
        WiredTransactionChestLogsRequest,
        WiredTransactionLogList>(
        ApplicationMemberIds.WiredTransactionChestLogsGet,
        "Get Wired chest transactions",
        "Returns one exactly correlated page of transactions for a chest log list.",
        MessageKeys.Wired.Transaction.ChestLogsRequest,
        MessageKeys.Wired.Transaction.Logs,
        TransactionPagingParameters(true),
        ReadHints());

    public static ApplicationDescriptor TransactionRoomLogsGet { get; } = RequestResponse<
        WiredTransactionRoomLogsRequest,
        WiredTransactionLogList>(
        ApplicationMemberIds.WiredTransactionRoomLogsGet,
        "Get Wired room transactions",
        "Returns one exactly correlated page of room-wide Wired transactions.",
        MessageKeys.Wired.Transaction.RoomLogsRequest,
        MessageKeys.Wired.Transaction.Logs,
        TransactionPagingParameters(false),
        ReadHints());

    public static ApplicationDescriptor TransactionDetailsGet { get; } = RequestResponse<
        WiredTransactionDetailsRequest,
        WiredTransactionLogDetails>(
        ApplicationMemberIds.WiredTransactionDetailsGet,
        "Get Wired transaction details",
        "Returns the exact chest and furniture details for one 64-bit transaction identifier.",
        MessageKeys.Wired.Transaction.LogDetailsRequest,
        MessageKeys.Wired.Transaction.LogDetails,
        [
            new("transaction_id", typeof(long), true, null, "Positive 64-bit transaction identifier.", new(Minimum: 1)),
            TimeoutParameter()
        ],
        ReadHints());

    public static ApplicationDescriptor ContractOpen { get; } = RequestResponse<
        WiredContractOpenRequest,
        WiredContractContents>(
        ApplicationMemberIds.WiredContractOpen,
        "Open Wired contract",
        "Requests and returns the exact contents of one Wired contract on verified Flash layouts.",
        MessageKeys.Wired.Contracts.OpenRequest,
        MessageKeys.Wired.Contracts.Contents,
        [ContractIdParameter(), TimeoutParameter()],
        ReadHints());

    public static ApplicationDescriptor ContractOpenSend { get; } = SendCall<WiredContractOpenSendRequest>(
        ApplicationMemberIds.WiredContractOpenSend,
        "Send Wired contract open",
        "Requests one Wired contract without waiting for its contents.",
        MessageKeys.Wired.Contracts.OpenRequest,
        [ContractIdParameter()],
        ReadHints());

    public static ApplicationDescriptor ContractUpdate { get; } = RequestResponse<
        WiredContractUpdateRequest,
        WiredContractUpdateResult>(
        ApplicationMemberIds.WiredContractUpdate,
        "Update Wired contract",
        "Replaces one Wired contract and waits for the verified Flash result.",
        MessageKeys.Wired.Contracts.Update,
        MessageKeys.Wired.Contracts.UpdateResult,
        [
            new("contract", typeof(WiredContractContents), true, null, "Complete contract definition."),
            TimeoutParameter()
        ],
        WriteHints(true, true));

    public static ApplicationDescriptor ContractUpdateSend { get; } = SendCall<
        WiredContractSendRequest>(
        ApplicationMemberIds.WiredContractUpdateSend,
        "Send Wired contract update",
        "Sends a complete contract update without waiting for its result.",
        MessageKeys.Wired.Contracts.Update,
        [new("contract", typeof(WiredContractContents), true, null, "Complete contract definition.")],
        WriteHints(true, true));

    public static ApplicationDescriptor TradeItemsAdd { get; } = SendCall<WiredTradeItemsRequest>(
        ApplicationMemberIds.WiredTradeItemsAdd,
        "Add Wired trade items",
        "Adds distinct inventory items to the active Wired chest trade.",
        MessageKeys.Wired.Trade.ItemsUpdate,
        [InventoryIdsParameter()],
        WriteHints(false, false));

    public static ApplicationDescriptor TradeItemsRemove { get; } = SendCall<WiredTradeItemsRequest>(
        ApplicationMemberIds.WiredTradeItemsRemove,
        "Remove Wired trade items",
        "Removes distinct inventory items from the active Wired chest trade.",
        MessageKeys.Wired.Trade.ItemsUpdate,
        [InventoryIdsParameter()],
        WriteHints(false, false));

    public static ApplicationDescriptor TradeConfirm { get; } = SendCall<WiredTradeConfirmRequest>(
        ApplicationMemberIds.WiredTradeConfirm,
        "Confirm Wired trade",
        "Updates the confirmation state of the active Wired chest trade.",
        MessageKeys.Wired.Trade.Confirm,
        [new("confirm", typeof(bool), false, true, "Confirmation flag sent to the hotel.")],
        WriteHints(false, true));

    public static ApplicationDescriptor TradeCancel { get; } = SendCall<WiredCommandRequest>(
        ApplicationMemberIds.WiredTradeCancel,
        "Cancel Wired trade",
        "Cancels the active Wired chest trade.",
        MessageKeys.Wired.Trade.Cancel,
        [],
        WriteHints(true, true));

    public static ApplicationDescriptor Changed { get; } = Event<WiredChanged>(
        ApplicationMemberIds.WiredChanged,
        "Wired state changed",
        "Publishes every ordered immutable Wired state revision, including reset.",
        StateMessages());

    public static ApplicationDescriptor PermissionsChanged { get; } = Event<WiredEvent<WiredPermissions>>(
        ApplicationMemberIds.WiredPermissionsChanged,
        "Wired permissions changed",
        "Publishes the active user's latest Wired read and modify permissions.",
        [Observe(MessageKeys.Wired.State.Permissions)]);

    public static ApplicationDescriptor EnvironmentChanged { get; } = Event<WiredEvent<WiredEnvironment>>(
        ApplicationMemberIds.WiredEnvironmentChanged,
        "Wired environment changed",
        "Publishes the room's Wired environment and enabled achievements.",
        [Observe(MessageKeys.Wired.State.Environment)]);

    public static ApplicationDescriptor ClickSettingsChanged { get; } = Event<WiredEvent<WiredClickSettings>>(
        ApplicationMemberIds.WiredClickSettingsChanged,
        "Wired click settings changed",
        "Publishes the latest Wired user and furniture click options.",
        [Observe(MessageKeys.Wired.State.ClickSettings)]);

    public static ApplicationDescriptor RoomSettingsChanged { get; } = Event<WiredEvent<WiredRoomSettings>>(
        ApplicationMemberIds.WiredRoomSettingsChanged,
        "Wired room settings changed",
        "Publishes the latest room permission masks and Wired timezone.",
        [Observe(MessageKeys.Wired.Room.Settings)]);

    public static ApplicationDescriptor ConfigurationOpened { get; } = Event<WiredEvent<Id>>(
        ApplicationMemberIds.WiredConfigurationOpened,
        "Wired configuration opened",
        "Publishes the furni identifier named by each configuration-open handshake.",
        [Observe(MessageKeys.Wired.Configuration.Opened)]);

    public static ApplicationDescriptor ConfigurationReceived { get; } = Event<
        WiredEvent<WiredConfigurationSnapshot>>(
        ApplicationMemberIds.WiredConfigurationReceived,
        "Wired configuration received",
        "Publishes immutable complete configurations for every supported Wired kind.",
        [
            Observe(MessageKeys.Wired.Configuration.Trigger, false),
            Observe(MessageKeys.Wired.Configuration.Action, false),
            Observe(MessageKeys.Wired.Configuration.Condition, false),
            Observe(MessageKeys.Wired.Configuration.Selector, false),
            Observe(MessageKeys.Wired.Configuration.Addon, false),
            Observe(MessageKeys.Wired.Configuration.Variable, false)
        ]);

    public static ApplicationDescriptor ConfigurationSaveResult { get; } = Event<
        WiredEvent<WiredConfigurationSaveResult>>(
        ApplicationMemberIds.WiredConfigurationSaveResult,
        "Wired configuration save result",
        "Publishes each successful save or exact validation failure.",
        [
            Observe(MessageKeys.Wired.Configuration.SaveSucceeded),
            Observe(MessageKeys.Wired.Configuration.ValidationFailed)
        ]);

    public static ApplicationDescriptor MenuError { get; } = Event<WiredEvent<WiredMenuError>>(
        ApplicationMemberIds.WiredMenuError,
        "Wired menu error",
        "Publishes Wired menu error codes reported by the hotel.",
        [Observe(MessageKeys.Wired.State.MenuError)]);

    public static ApplicationDescriptor RewardResult { get; } = Event<WiredEvent<WiredRewardResult>>(
        ApplicationMemberIds.WiredRewardResult,
        "Wired reward result",
        "Publishes each Wired reward result reason.",
        [Observe(MessageKeys.Wired.State.RewardResult)]);

    public static ApplicationDescriptor ChestOpened { get; } = Event<WiredEvent<OpenChest>>(
        ApplicationMemberIds.WiredChestOpened,
        "Wired chest opened",
        "Publishes hotel requests to open a Wired chest.",
        [Observe(MessageKeys.Wired.Chests.Opened)]);

    public static ApplicationDescriptor ChestCoinsReceived { get; } = Event<WiredEvent<CoinsChestContents>>(
        ApplicationMemberIds.WiredChestCoinsReceived,
        "Wired chest coins received",
        "Publishes complete coin contents for one Wired chest.",
        [Observe(MessageKeys.Wired.Chests.Coins)]);

    public static ApplicationDescriptor ChestItemsChunkReceived { get; } = Event<
        WiredEvent<WiredChestItemsChunkSnapshot>>(
        ApplicationMemberIds.WiredChestItemsChunkReceived,
        "Wired chest item chunk received",
        "Publishes one immutable item-content fragment for a Wired chest.",
        [Observe(MessageKeys.Wired.Chests.ItemsChunk)]);

    public static ApplicationDescriptor ChestItemsUpdated { get; } = Event<
        WiredEvent<WiredChestItemsUpdatedSnapshot>>(
        ApplicationMemberIds.WiredChestItemsUpdated,
        "Wired chest items updated",
        "Publishes exact removed identifiers and added immutable chest storage entries.",
        [Observe(MessageKeys.Wired.Chests.ItemsUpdated)]);

    public static ApplicationDescriptor ChestUpgradeResult { get; } = Event<WiredEvent<UpgradeChestResult>>(
        ApplicationMemberIds.WiredChestUpgradeResult,
        "Wired chest upgrade result",
        "Publishes each chest capacity-upgrade result.",
        [Observe(MessageKeys.Wired.Chests.UpgradeResult)]);

    public static ApplicationDescriptor ChestPreferencesUpdated { get; } = Event<
        WiredEvent<ChestPreferencesUpdateSuccess>>(
        ApplicationMemberIds.WiredChestPreferencesUpdated,
        "Wired chest preferences updated",
        "Publishes confirmed chest preference and notification-preference updates.",
        [Observe(MessageKeys.Wired.Chests.PreferencesUpdated)]);

    public static ApplicationDescriptor TransactionSucceeded { get; } = Event<
        WiredEvent<WiredTransactionSuccess>>(
        ApplicationMemberIds.WiredTransactionSucceeded,
        "Wired transaction succeeded",
        "Publishes successful Wired transaction outcomes and optional rewards.",
        [Observe(MessageKeys.Wired.Transaction.Succeeded)]);

    public static ApplicationDescriptor TransactionFailed { get; } = Event<
        WiredEvent<WiredTransactionFail>>(
        ApplicationMemberIds.WiredTransactionFailed,
        "Wired transaction failed",
        "Publishes Wired transaction failure type identifiers.",
        [Observe(MessageKeys.Wired.Transaction.Failed)]);

    public static ApplicationDescriptor ContractContentsReceived { get; } = Event<
        WiredEvent<WiredContractContents>>(
        ApplicationMemberIds.WiredContractContentsReceived,
        "Wired contract contents received",
        "Publishes complete immutable Wired contract definitions.",
        [Observe(MessageKeys.Wired.Contracts.Contents)]);

    public static ApplicationDescriptor ContractOpened { get; } = Event<WiredEvent<WiredOpenContract>>(
        ApplicationMemberIds.WiredContractOpened,
        "Wired contract opened",
        "Publishes contract-editor open requests from the hotel.",
        [Observe(MessageKeys.Wired.Contracts.Opened)]);

    public static ApplicationDescriptor ContractUpdateResult { get; } = Event<
        WiredEvent<WiredContractUpdateResult>>(
        ApplicationMemberIds.WiredContractUpdateResult,
        "Wired contract update result",
        "Publishes success and failure codes for contract updates.",
        [Observe(MessageKeys.Wired.Contracts.UpdateResult)]);

    public static ApplicationDescriptor TradeInitiated { get; } = Event<WiredEvent<WiredTradeInitiate>>(
        ApplicationMemberIds.WiredTradeInitiated,
        "Wired trade initiated",
        "Publishes the exact requirement and timeout for a new Wired chest trade.",
        [Observe(MessageKeys.Wired.Trade.Initiated)]);

    public static ApplicationDescriptor TradeItemsUpdated { get; } = Event<
        WiredEvent<WiredTradingItemsSnapshot>>(
        ApplicationMemberIds.WiredTradeItemsUpdated,
        "Wired trade items updated",
        "Publishes immutable item offers and acceptance readiness for the active Wired trade.",
        [Observe(MessageKeys.Wired.Trade.ItemsUpdated)]);

    public static ApplicationDescriptor TradeCancelled { get; } = Event<WiredEvent<WiredTradeCancelled>>(
        ApplicationMemberIds.WiredTradeCancelled,
        "Wired trade cancelled",
        "Publishes the hotel failure type for a cancelled Wired trade.",
        [Observe(MessageKeys.Wired.Trade.Cancelled)]);

    public static ApplicationDescriptor TradeCompleted { get; } = Event<WiredEvent<WiredTradeCompleted>>(
        ApplicationMemberIds.WiredTradeCompleted,
        "Wired trade completed",
        "Publishes completion of the active Wired trade.",
        [Observe(MessageKeys.Wired.Trade.Completed)]);

    public static ApplicationDescriptor TradeNotification { get; } = Event<
        WiredEvent<WiredTradeTransactionNotification>>(
        ApplicationMemberIds.WiredTradeNotification,
        "Wired trade notification",
        "Publishes Wired trade transaction notification identifiers.",
        [Observe(MessageKeys.Wired.Trade.Notification)]);

    private static ApplicationDescriptor Save<TRequest>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        Type update_type) => Call<TRequest, WiredConfigurationSaveResult>(
        id,
        title,
        description,
        [
            new("update", update_type, true, null, "Complete replacement configuration."),
            TimeoutParameter()
        ],
        [
            Send(request_key),
            Observe(MessageKeys.Wired.Configuration.SaveSucceeded),
            Observe(MessageKeys.Wired.Configuration.ValidationFailed)
        ],
        WriteHints(true, true));

    private static ApplicationDescriptor RequestResponse<TRequest, TResult>(
        string id,
        string title,
        string description,
        MessageKey request_key,
        MessageKey response_key,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        ApplicationToolHints hints) => Call<TRequest, TResult>(
        id,
        title,
        description,
        parameters,
        [Send(request_key), Observe(response_key)],
        hints);

    private static ApplicationDescriptor Call<TRequest, TResult>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        IReadOnlyList<ApplicationMessageRequirement> messages,
        ApplicationToolHints hints) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Operation,
        ApplicationExposure.All,
        typeof(TRequest),
        typeof(TResult),
        parameters,
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive],
        messages: messages,
        tool_hints: hints);

    private static ApplicationDescriptor SendCall<TRequest>(
        string id,
        string title,
        string description,
        MessageKey message,
        IReadOnlyList<ApplicationParameterDescriptor> parameters,
        ApplicationToolHints hints) => Call<TRequest, WiredDispatchResult>(
        id,
        title,
        description,
        parameters,
        [Send(message)],
        hints);

    private static ApplicationDescriptor Event<TEvent>(
        string id,
        string title,
        string description,
        IReadOnlyList<ApplicationMessageRequirement> messages) => new(
        id,
        title,
        description,
        ApplicationMemberKind.Event,
        event_exposure,
        null,
        typeof(TEvent),
        messages: messages);

    private static ApplicationMessageRequirement Send(MessageKey key) =>
        new(key, Direction.Out, ApplicationMessageRole.Send);

    private static ApplicationMessageRequirement Observe(MessageKey key, bool required = true) =>
        new(key, Direction.In, ApplicationMessageRole.Observe, required);

    private static ApplicationParameterDescriptor TimeoutParameter(int default_value = 10000) => new(
        "timeout_milliseconds",
        typeof(int),
        false,
        default_value,
        "Total maximum time for the session-bound operation.",
        new(Minimum: 1, Maximum: 120000));

    private static ApplicationParameterDescriptor VariableIdParameter() => new(
        "variable_id",
        typeof(string),
        true,
        null,
        "Non-empty Wired variable identifier.",
        new(MinLength: 1, MaxUtf8Bytes: ushort.MaxValue));

    private static ApplicationParameterDescriptor EntityTypeParameter() => new(
        "entity_type",
        typeof(int),
        true,
        null,
        "Hotel entity type identifier.");

    private static ApplicationParameterDescriptor EntityIdParameter() => new(
        "entity_id",
        typeof(int),
        true,
        null,
        "Positive entity identifier.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor ChestIdParameter() => new(
        "chest_id",
        typeof(Id),
        true,
        null,
        "Positive Wired chest identifier.",
        IdConstraint());

    private static ApplicationParameterDescriptor ContractIdParameter() => new(
        "contract_id",
        typeof(int),
        true,
        null,
        "Positive Wired contract identifier.",
        new(Minimum: 1));

    private static ApplicationParameterDescriptor InventoryIdsParameter() => new(
        "inventory_ids",
        typeof(IReadOnlyList<Id>),
        true,
        null,
        "Distinct non-zero signed 32-bit inventory item identifiers.",
        new(MinItems: 1, MaxItems: 1000));

    private static ApplicationParameterDescriptor[] VariableSetParameters(bool permanent) =>
    [
        permanent
            ? EntityTypeParameter()
            : new("target", typeof(WiredTarget), true, null, "Furni, user or global variable target."),
        permanent
            ? EntityIdParameter()
            : new("object_id", typeof(int), true, null, "Furni identifier, room user index or zero for global scope."),
        VariableIdParameter(),
        new("value", typeof(int), true, null, "Integer variable value; ignored for delete operations."),
        new("operation", typeof(int), false, WiredVariableOperation.Write, "Zero writes, one creates and two deletes.", new(Minimum: 0, Maximum: 2))
    ];

    private static ApplicationParameterDescriptor[] TransactionPagingParameters(bool chest)
    {
        var values = new List<ApplicationParameterDescriptor>();
        if (chest)
        {
            values.Add(new(
                "log_list_id",
                typeof(int),
                true,
                null,
                "Positive chest transaction-log list identifier.",
                new(Minimum: 1)));
        }
        values.Add(new("page", typeof(int), false, 1, "One-based transaction page.", new(Minimum: 1)));
        values.Add(new("page_size", typeof(int), false, 50, "Transaction rows requested.", new(Minimum: 1, Maximum: 250)));
        values.Add(TimeoutParameter());
        return values.ToArray();
    }

    private static ApplicationParameterConstraints IdConstraint() =>
        new(Pattern: @"^[1-9][0-9]*$");

    private static ApplicationParameterConstraints TextConstraint() =>
        new(MaxUtf8Bytes: ushort.MaxValue);

    private static ApplicationToolHints ReadHints() => new(true, false, true, true);

    private static ApplicationToolHints WriteHints(bool destructive, bool idempotent) =>
        new(false, destructive, idempotent, true);

    private static ApplicationMessageRequirement[] StateMessages() =>
    [
        Observe(MessageKeys.Wired.State.Permissions, false),
        Observe(MessageKeys.Wired.State.Environment, false),
        Observe(MessageKeys.Wired.State.ClickSettings, false),
        Observe(MessageKeys.Wired.State.MenuError, false),
        Observe(MessageKeys.Wired.State.RewardResult, false),
        Observe(MessageKeys.Wired.Configuration.Opened, false),
        Observe(MessageKeys.Wired.Configuration.Trigger, false),
        Observe(MessageKeys.Wired.Configuration.Action, false),
        Observe(MessageKeys.Wired.Configuration.Condition, false),
        Observe(MessageKeys.Wired.Configuration.Selector, false),
        Observe(MessageKeys.Wired.Configuration.Addon, false),
        Observe(MessageKeys.Wired.Configuration.Variable, false),
        Observe(MessageKeys.Wired.Configuration.SaveSucceeded, false),
        Observe(MessageKeys.Wired.Configuration.ValidationFailed, false),
        Observe(MessageKeys.Wired.Room.Settings, false),
        Observe(MessageKeys.Wired.Chests.Opened, false),
        Observe(MessageKeys.Wired.Chests.Coins, false),
        Observe(MessageKeys.Wired.Chests.ItemsChunk, false),
        Observe(MessageKeys.Wired.Chests.ItemsUpdated, false),
        Observe(MessageKeys.Wired.Chests.UpgradeResult, false),
        Observe(MessageKeys.Wired.Chests.PreferencesUpdated, false),
        Observe(MessageKeys.Wired.Transaction.Succeeded, false),
        Observe(MessageKeys.Wired.Transaction.Failed, false),
        Observe(MessageKeys.Wired.Contracts.Contents, false),
        Observe(MessageKeys.Wired.Contracts.Opened, false),
        Observe(MessageKeys.Wired.Contracts.UpdateResult, false),
        Observe(MessageKeys.Wired.Trade.Initiated, false),
        Observe(MessageKeys.Wired.Trade.ItemsUpdated, false),
        Observe(MessageKeys.Wired.Trade.Cancelled, false),
        Observe(MessageKeys.Wired.Trade.Completed, false),
        Observe(MessageKeys.Wired.Trade.Notification, false)
    ];
}
