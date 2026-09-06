using Qx.Model.Wired;

namespace Qx.Game.Application;

public sealed record WiredStateRequest(
    int ChestOffset = 0,
    int ChestLimit = 5,
    int ItemOffset = 0,
    int ItemLimit = 20);

public sealed record WiredChestStateEntry(
    Id ChestId,
    int? Coins,
    int TotalItems,
    int ItemOffset,
    int ItemLimit,
    IReadOnlyList<WiredChestStorageSnapshot> Items,
    bool ItemsComplete,
    int ExpectedFragments,
    IReadOnlyList<int> ReceivedFragments,
    UpgradeChestResult? LastUpgradeResult,
    ChestPreferencesUpdateSuccess? LastPreferencesResult);

public sealed record WiredChestStatePage(
    int TotalChests,
    int ChestOffset,
    int ChestLimit,
    int ItemOffset,
    int ItemLimit,
    IReadOnlyList<WiredChestStateEntry> Entries);

public sealed record WiredStateView(
    long Generation,
    long Revision,
    WiredPermissions? Permissions,
    WiredEnvironment? Environment,
    WiredClickSettings? ClickSettings,
    WiredRoomSettings? RoomSettings,
    Id? OpenFurniId,
    WiredConfigurationSnapshot? Configuration,
    bool? LastSaveSucceeded,
    WiredValidationError? LastValidationError,
    WiredMenuError? LastMenuError,
    WiredRewardResult? LastRewardResult,
    Id? LastOpenedChestId,
    WiredChestStatePage Chests,
    WiredContractSnapshot Contract,
    WiredTradeSnapshot Trade)
{
    public bool CanModify => Permissions?.CanModify is true;
    public bool CanRead => Permissions?.CanRead is true;
}

public sealed record WiredTimeoutRequest(int TimeoutMilliseconds = 10000);

public sealed record WiredConfigurationGetRequest(
    Id FurniId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredConfigurationOpenRequest(Id FurniId);

public sealed record WiredConfigurationApplySnapshotRequest(Id FurniId);

public sealed record WiredTriggerSaveRequest(
    UpdateTrigger Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredActionSaveRequest(
    UpdateAction Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredConditionSaveRequest(
    UpdateCondition Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredSelectorSaveRequest(
    UpdateSelector Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredAddonSaveRequest(
    UpdateAddon Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredVariableSaveRequest(
    UpdateVariable Update,
    int TimeoutMilliseconds = 10000);

public sealed record WiredConfigurationSaveResult(
    bool Success,
    WiredValidationError? ValidationError,
    long Generation,
    long Revision);

public sealed record WiredVariableDifferencesRequest(
    IReadOnlyList<VariableHashEntry>? Cache = null,
    int TimeoutMilliseconds = 10000);

public sealed record WiredVariableListRequest(
    int MaximumChunks = 64,
    int TimeoutMilliseconds = 30000);

public sealed record WiredVariableCollectionSnapshot(
    long Generation,
    int AllVariablesHash,
    int Chunks,
    IReadOnlyList<WiredVariableWithHashSnapshot> Variables,
    IReadOnlyList<VariableHashEntry> Cache);

public sealed record WiredVariableWithHashSnapshot(
    int PerVariableHash,
    WiredVariableSnapshot Variable);

public sealed record WiredVariableDifferencesSnapshot(
    long Generation,
    int AllVariablesHash,
    bool IsLastChunk,
    IReadOnlyList<string> RemovedVariables,
    IReadOnlyList<WiredVariableWithHashSnapshot> AddedOrUpdated);

public sealed record WiredVariableValueSnapshot(string VariableId, int Value);

public sealed record WiredVariablesObjectSnapshot(
    long Generation,
    WiredTarget Target,
    int ObjectId,
    IReadOnlyList<WiredVariableValueSnapshot> Values,
    IReadOnlyList<int> ConfiguredInWireds);

public sealed record WiredVariableHoldersSnapshot(
    long Generation,
    int LeadingValue,
    WiredVariableSnapshot Variable,
    IReadOnlyList<WiredObjectValueSnapshot> Holders);

public sealed record WiredVariableStorageSnapshot(
    string? VariableId,
    int Value,
    long CreationTime,
    string CreationTimeText,
    long LastUpdateTime,
    string LastUpdateTimeText);

public sealed record WiredPermanentVariablesSnapshot(
    int EntityType,
    int EntityId,
    string EntityName,
    string EntityFigure,
    int? OwnerId,
    string? OwnerName,
    string? OwnerFigure,
    IReadOnlyList<WiredVariableStorageSnapshot> Variables);

public sealed record WiredVariableOwnerSnapshot(
    int EntityType,
    int EntityId,
    string EntityName,
    WiredVariableStorageSnapshot Storage);

public sealed record WiredVariableOwnersSnapshot(
    string VariableId,
    int TotalEntries,
    int CurrentPage,
    int Amount,
    IReadOnlyList<WiredVariableOwnerSnapshot> Owners,
    int UserTypeFilter,
    int SortTypeFilter);

public sealed record WiredVariablesObjectRequest(
    WiredTarget Target,
    int ObjectId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredVariableHoldersRequest(
    string VariableId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredPermanentVariablesRequest(
    int EntityType,
    int EntityId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredVariableOwnersRequest(
    string VariableId,
    int Page = 1,
    int PageSize = 50,
    int UserTypeFilter = 0,
    int SortTypeFilter = -1,
    int TimeoutMilliseconds = 10000);

public sealed record WiredObjectVariableSetRequest(
    WiredTarget Target,
    int ObjectId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write);

public sealed record WiredPermanentVariableSetRequest(
    int EntityType,
    int EntityId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write,
    int TimeoutMilliseconds = 10000);

public sealed record WiredPermanentVariableSendRequest(
    int EntityType,
    int EntityId,
    string VariableId,
    int Value,
    int Operation = WiredVariableOperation.Write);

public sealed record WiredRoomSettingsSetRequest(
    int ModifyPermissionMask,
    int ReadPermissionMask,
    string Timezone,
    int TimeoutMilliseconds = 10000);

public sealed record WiredRoomLogsRequest(
    int Page = 1,
    int PageSize = 50,
    int LogLevelFilter = -1,
    int LogSourceFilter = -1,
    string Query = "",
    int TimeoutMilliseconds = 10000);

public sealed record WiredUserClickRequest(
    int Index,
    int TimeoutMilliseconds = 10000);

public sealed record WiredPreferencesSetRequest(WiredSetPreferences Preferences);

public sealed record WiredChestRequest(Id ChestId);

public sealed record WiredChestsLockRequest(
    bool Locked,
    bool ApplyToAllInRoom = false);

public sealed record WiredChestUpgradeRequest(
    int ChestId,
    int UpgradeAmount);

public sealed record WiredChestCoinsWithdrawRequest(
    Id ChestId,
    int CoinAmount);

public sealed record WiredChestItemsWithdrawRequest(
    Id ChestId,
    ChestItemType ItemType,
    int Count);

public sealed record WiredChestOptionsSetRequest(SetChestOptions Options);

public sealed record WiredChestPreferencesSetRequest(SetChestPreferences Preferences);

public sealed record WiredChestNotificationPreferencesSetRequest(
    SetChestNotificationPreferences Preferences);

public sealed record WiredChestDepositRequest(
    Id ChestId,
    IReadOnlyList<Id> InventoryIds,
    int TimeoutMilliseconds = 30000);

public sealed record WiredChestDepositResult(
    bool Success,
    string Failure,
    int Requested,
    int Accepted,
    IReadOnlyList<WiredChestStorageSnapshot> Stored,
    long Generation,
    long Revision);

public sealed record WiredTransactionChestLogsRequest(
    int LogListId,
    int Page = 1,
    int PageSize = 50,
    int TimeoutMilliseconds = 10000);

public sealed record WiredTransactionRoomLogsRequest(
    int Page = 1,
    int PageSize = 50,
    int TimeoutMilliseconds = 10000);

public sealed record WiredTransactionDetailsRequest(
    long TransactionId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredContractOpenRequest(
    int ContractId,
    int TimeoutMilliseconds = 10000);

public sealed record WiredContractOpenSendRequest(int ContractId);

public sealed record WiredContractUpdateRequest(
    WiredContractContents Contract,
    int TimeoutMilliseconds = 10000);

public sealed record WiredContractSendRequest(WiredContractContents Contract);

public sealed record WiredTradeItemsRequest(IReadOnlyList<Id> InventoryIds);

public sealed record WiredTradeConfirmRequest(bool Confirm = true);

public sealed record WiredCommandRequest;

public sealed record WiredDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long Generation,
    long Revision);

public enum WiredChangeKind
{
    Permissions,
    Environment,
    ClickSettings,
    RoomSettings,
    ConfigurationOpened,
    ConfigurationReceived,
    SaveSucceeded,
    ValidationFailed,
    MenuError,
    RewardResult,
    RoomStats,
    RoomLogs,
    ErrorLogs,
    UserClickResult,
    VariablesHash,
    VariablesDifferences,
    VariablesObject,
    VariableHolders,
    PermanentVariables,
    VariableOwners,
    PermanentVariableSetResult,
    ChestOpened,
    ChestCoins,
    ChestItemsChunk,
    ChestItemsUpdated,
    ChestUpgradeResult,
    ChestPreferencesUpdated,
    TransactionSucceeded,
    TransactionFailed,
    TransactionLogs,
    TransactionLogDetails,
    ContractContents,
    ContractOpened,
    ContractUpdateResult,
    TradeInitiated,
    TradeItemsUpdated,
    TradeCancelled,
    TradeCompleted,
    TradeNotification,
    Reset
}

public sealed record WiredChanged(
    WiredChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    long Generation,
    long Revision);

public sealed record WiredEvent<T>(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    T Value);
