using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Qx.Game;

public sealed record WiredTextConnectorSnapshot(Id Id, string Value);

public enum WiredConfigurationKind
{
    Trigger,
    Action,
    Condition,
    Selector,
    Addon,
    Variable
}

public sealed record WiredInputSourcesSnapshot(
    IReadOnlyList<IReadOnlyList<int>> AllowedFurniSources,
    IReadOnlyList<IReadOnlyList<int>> AllowedUserSources,
    IReadOnlyList<int> DefaultFurniSources,
    IReadOnlyList<int> DefaultUserSources);

public sealed record WiredVariableSnapshot(
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<WiredTextConnectorSnapshot>? TextConnector);

public sealed record WiredContextVariableSnapshot(
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<Id>? TextConnectorIds,
    IReadOnlyList<string>? TextConnectorValues);

public sealed record WiredObjectValueSnapshot(Id ObjectId, long Value);

public sealed record WiredSharedVariableSnapshot(
    Id RoomId,
    string RoomName,
    string VariableId,
    int VariableType,
    string VariableName,
    int AvailabilityType,
    int VariableTarget,
    bool AlwaysAvailable,
    bool CanCreateAndDelete,
    bool HasValue,
    bool CanWriteValue,
    bool CanInterceptChanges,
    bool IsInvisible,
    bool CanReadCreationTime,
    bool CanReadLastUpdateTime,
    IReadOnlyList<Id>? TextConnectorIds,
    IReadOnlyList<string>? TextConnectorValues);

public sealed record WiredSharedPlaceholderSnapshot(
    Id RoomId,
    string RoomName,
    string PlaceholderName);

public enum WiredContextValueKind
{
    RoomVariables,
    FurniVariable,
    UserVariable,
    GlobalVariable,
    ReferenceVariables,
    RulesetVariables,
    ReferencePlaceholders
}

public sealed record WiredContextEntrySnapshot(
    int Tag,
    WiredContextValueKind Kind,
    int? AllVariablesHash,
    WiredContextVariableSnapshot? Variable,
    IReadOnlyList<WiredObjectValueSnapshot>? Holders,
    long? Value,
    IReadOnlyList<WiredSharedVariableSnapshot>? SharedVariables,
    IReadOnlyList<WiredContextVariableSnapshot>? Variables,
    IReadOnlyList<WiredSharedPlaceholderSnapshot>? SharedPlaceholders);

public sealed record WiredConfigurationSnapshot(
    WiredConfigurationKind Kind,
    int FurniLimit,
    IReadOnlyList<Id> StuffIds,
    IReadOnlyList<Id> StuffIds2,
    int StuffTypeId,
    Id Id,
    string StringParam,
    IReadOnlyList<int> IntParams,
    IReadOnlyList<string> VariableIds,
    IReadOnlyList<int> FurniSourceTypes,
    IReadOnlyList<int> UserSourceTypes,
    int Code,
    bool AdvancedMode,
    WiredInputSourcesSnapshot InputSources,
    bool AllowWallFurni,
    IReadOnlyList<WiredContextEntrySnapshot> Context,
    IReadOnlyList<int> DefaultIntParams,
    IReadOnlyList<int> UnityContextTags,
    UnityWiredContextLayout UnityContextLayout,
    bool? UnityConditionHasSeparateInvert,
    int? DelayInPulses,
    int? QuantifierCode,
    int? QuantifierType,
    bool? DefinitionIsInvert,
    bool? IsFilter,
    bool? IsInvert);

public sealed record WiredChestStorageSnapshot(
    int InventoryId,
    int LockState,
    long TransactionId,
    ChestItemType Type,
    bool Groupable,
    int SpecialType,
    ItemDataSnapshot Data,
    int Extra);

public sealed record WiredChestContentsSnapshot(
    Id ChestId,
    int? Coins,
    IReadOnlyList<WiredChestStorageSnapshot> Items,
    bool ItemsComplete,
    int ExpectedFragments,
    IReadOnlyList<int> ReceivedFragments,
    UpgradeChestResult? LastUpgradeResult,
    ChestPreferencesUpdateSuccess? LastPreferencesResult);

public sealed record WiredTradeItemSnapshot(
    Id ItemId,
    ItemType Type,
    Id Id,
    int Kind,
    int Category,
    bool IsGroupable,
    ItemDataSnapshot Data,
    int CreationDay,
    int CreationMonth,
    int CreationYear,
    long Extra);

public sealed record WiredTradingItemsSnapshot(
    Id FirstUserId,
    IReadOnlyList<WiredTradeItemSnapshot> FirstUserItems,
    int FirstUserNumItems,
    int FirstUserNumCredits,
    Id SecondUserId,
    IReadOnlyList<WiredTradeItemSnapshot> SecondUserItems,
    int SecondUserNumItems,
    int SecondUserNumCredits,
    bool CanAccept,
    int Extra);

public enum WiredTradeStatus
{
    None,
    Initiated,
    Active,
    Cancelled,
    Completed
}

public sealed record WiredTradeSnapshot(
    WiredTradeStatus Status,
    WiredTradeInitiate? Initiation,
    WiredTradingItemsSnapshot? Items,
    WiredTradeCancelled? Cancellation,
    WiredTransactionSuccess? LastTransactionSuccess,
    WiredTransactionFail? LastTransactionFailure,
    WiredTradeTransactionNotification? LastNotification);

public sealed record WiredContractSnapshot(
    int? OpenContractId,
    WiredContractContents? Contents,
    WiredContractUpdateResult? LastUpdateResult);

public sealed record WiredSnapshot(
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
    IReadOnlyList<WiredChestContentsSnapshot> Chests,
    WiredContractSnapshot Contract,
    WiredTradeSnapshot Trade)
{
    public static WiredSnapshot Empty { get; } = new(
        0,
        0,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        [],
        new(null, null, null),
        new(WiredTradeStatus.None, null, null, null, null, null, null));

    public bool CanModify => Permissions?.CanModify is true;
    public bool CanRead => Permissions?.CanRead is true;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Trigger =>
        Configuration?.Kind is WiredConfigurationKind.Trigger ? Configuration : null;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Effect =>
        Configuration?.Kind is WiredConfigurationKind.Action ? Configuration : null;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Condition =>
        Configuration?.Kind is WiredConfigurationKind.Condition ? Configuration : null;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Selector =>
        Configuration?.Kind is WiredConfigurationKind.Selector ? Configuration : null;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Addon =>
        Configuration?.Kind is WiredConfigurationKind.Addon ? Configuration : null;
    [JsonIgnore]
    public WiredConfigurationSnapshot? Variable =>
        Configuration?.Kind is WiredConfigurationKind.Variable ? Configuration : null;

}

internal enum WiredStateChangeKind
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

internal sealed record WiredStateUpdate(
    WiredStateChangeKind Kind,
    WiredSnapshot State,
    object? Value);

public sealed class WiredManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Dictionary<Id, ChestState> chests = [];
    private WiredSnapshot snapshot = WiredSnapshot.Empty;
    private WiredPermissions? permissions;
    private WiredEnvironment? environment;
    private WiredClickSettings? click_settings;
    private WiredRoomSettings? room_settings;
    private Id? open_furni_id;
    private WiredConfigurationSnapshot? configuration;
    private bool? last_save_succeeded;
    private WiredValidationError? last_validation_error;
    private WiredMenuError? last_menu_error;
    private WiredRewardResult? last_reward_result;
    private Id? last_opened_chest_id;
    private WiredContractSnapshot contract = new(null, null, null);
    private WiredTradeSnapshot trade = new(WiredTradeStatus.None, null, null, null, null, null, null);
    private long generation;
    private long revision;
    private long committed_generation;
    private long reset_generation = -1;
    private bool room_active;

    public WiredSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal event Action<WiredStateUpdate>? StateChanged;

    protected override void OnAttach()
    {
        lock (publication_sync)
        {
            lock (state_sync)
            {
                generation++;
                committed_generation = CurrentStateGeneration;
                reset_generation = -1;
                PublishState();
            }
        }
        OnIncoming(
            MessageContracts.Wired.State.Permissions,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.Permissions,
                message,
                () => permissions = message));
        OnIncoming(
            MessageContracts.Wired.State.Environment,
            (message, state_generation) =>
            {
                WiredEnvironment value = SnapshotOf(message);
                Store(state_generation, WiredStateChangeKind.Environment, value, () => environment = value);
            });
        OnIncoming(
            MessageContracts.Wired.State.ClickSettings,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ClickSettings,
                message,
                () => click_settings = message));
        OnIncoming(
            MessageContracts.Wired.Room.Settings,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.RoomSettings,
                message,
                () => room_settings = message));
        OnIncoming(
            MessageContracts.Wired.Configuration.Opened,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ConfigurationOpened,
                message,
                () =>
                {
                    open_furni_id = message.StuffId;
                    configuration = null;
                    last_save_succeeded = null;
                    last_validation_error = null;
                }));
        OnConfiguration(
            MessageContracts.Wired.Configuration.Trigger,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Action,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Condition,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Selector,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Addon,
            message => message.Config);
        OnConfiguration(
            MessageContracts.Wired.Configuration.Variable,
            message => message.Config);
        OnIncoming(
            MessageContracts.Wired.Configuration.SaveSucceeded,
            (_, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.SaveSucceeded,
                new WiredSaveSuccess(),
                () =>
                {
                    last_save_succeeded = true;
                    last_validation_error = null;
                }));
        OnIncoming(
            MessageContracts.Wired.Configuration.ValidationFailed,
            (message, state_generation) =>
            {
                WiredValidationError value = SnapshotOf(message);
                Store(
                    state_generation,
                    WiredStateChangeKind.ValidationFailed,
                    value,
                    () =>
                    {
                        last_save_succeeded = false;
                        last_validation_error = value;
                    });
            });
        OnIncoming(
            MessageContracts.Wired.State.MenuError,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.MenuError,
                message,
                () => last_menu_error = message));
        OnIncoming(
            MessageContracts.Wired.State.RewardResult,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.RewardResult,
                message,
                () => last_reward_result = message));
        Observe(MessageContracts.Wired.Room.Stats, WiredStateChangeKind.RoomStats, SnapshotOf);
        Observe(MessageContracts.Wired.Room.Logs, WiredStateChangeKind.RoomLogs, SnapshotOf);
        Observe(MessageContracts.Wired.ErrorLogs.Snapshot, WiredStateChangeKind.ErrorLogs, SnapshotOf);
        Observe(MessageContracts.Wired.UserClick.Result, WiredStateChangeKind.UserClickResult, static value => value);
        Observe(MessageContracts.Wired.Variables.Hash, WiredStateChangeKind.VariablesHash, static value => value);
        Observe(MessageContracts.Wired.Variables.Differences, WiredStateChangeKind.VariablesDifferences, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Object, WiredStateChangeKind.VariablesObject, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Holders, WiredStateChangeKind.VariableHolders, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Permanent, WiredStateChangeKind.PermanentVariables, SnapshotOf);
        Observe(MessageContracts.Wired.Variables.Owners, WiredStateChangeKind.VariableOwners, SnapshotOf);
        Observe(
            MessageContracts.Wired.Variables.PermanentValueSetResult,
            WiredStateChangeKind.PermanentVariableSetResult,
            static value => value);
        OnIncoming(
            MessageContracts.Wired.Chests.Opened,
            (message, state_generation) => Store(
                state_generation,
                WiredStateChangeKind.ChestOpened,
                message,
                () => last_opened_chest_id = message.ChestId));
        OnIncoming(MessageContracts.Wired.Chests.Coins, OnChestCoins);
        OnIncoming(MessageContracts.Wired.Chests.ItemsChunk, OnChestItemsChunk);
        OnIncoming(MessageContracts.Wired.Chests.ItemsUpdated, OnChestItemsUpdated);
        OnIncoming(MessageContracts.Wired.Chests.UpgradeResult, OnChestUpgradeResult);
        OnIncoming(MessageContracts.Wired.Chests.PreferencesUpdated, OnChestPreferencesUpdated);
        OnIncoming(MessageContracts.Wired.Transaction.Succeeded, OnTransactionSucceeded);
        OnIncoming(MessageContracts.Wired.Transaction.Failed, OnTransactionFailed);
        Observe(MessageContracts.Wired.Transaction.Logs, WiredStateChangeKind.TransactionLogs, SnapshotOf);
        Observe(MessageContracts.Wired.Transaction.LogDetails, WiredStateChangeKind.TransactionLogDetails, SnapshotOf);
        OnIncoming(MessageContracts.Wired.Contracts.Contents, OnContractContents);
        OnIncoming(MessageContracts.Wired.Contracts.Opened, OnContractOpened);
        OnIncoming(MessageContracts.Wired.Contracts.UpdateResult, OnContractUpdateResult);
        OnIncoming(MessageContracts.Wired.Trade.Initiated, OnTradeInitiated);
        OnIncoming(MessageContracts.Wired.Trade.ItemsUpdated, OnTradeItemsUpdated);
        OnIncoming(MessageContracts.Wired.Trade.Cancelled, OnTradeCancelled);
        OnIncoming(MessageContracts.Wired.Trade.Completed, OnTradeCompleted);
        OnIncoming(MessageContracts.Wired.Trade.Notification, OnTradeNotification);
    }

    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || state_generation == reset_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = state_generation;
                if (!room_active)
                    return;
                room_active = false;
                ClearState();
                generation++;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(WiredStateChangeKind.Reset, updated, null));
        }
    }

    internal void EnterRoom(Id _)
    {
        ChangeRoom(true);
    }

    internal void LeaveRoom()
    {
        ChangeRoom(false);
    }

    private void ChangeRoom(bool active)
    {
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                room_active = active;
                ClearState();
                generation++;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(WiredStateChangeKind.Reset, updated, null));
        }
    }

    private void ClearState()
    {
        permissions = null;
        environment = null;
        click_settings = null;
        room_settings = null;
        open_furni_id = null;
        configuration = null;
        last_save_succeeded = null;
        last_validation_error = null;
        last_menu_error = null;
        last_reward_result = null;
        last_opened_chest_id = null;
        chests.Clear();
        contract = new(null, null, null);
        trade = new(WiredTradeStatus.None, null, null, null, null, null, null);
    }

    private void OnConfiguration<T>(
        MessageContract<T> message_contract,
        Func<T, WiredConfig> select)
        where T : Qx.Messages.IParserComposer<T> =>
        OnIncoming(
            message_contract,
            (message, state_generation) =>
            {
                WiredConfigurationSnapshot value = SnapshotOf(select(message));
                Store(
                    state_generation,
                    WiredStateChangeKind.ConfigurationReceived,
                    value,
                    () =>
                    {
                        configuration = value;
                        open_furni_id = value.Id;
                    });
            });

    private void Observe<T>(
        MessageContract<T> message_contract,
        WiredStateChangeKind kind,
        Func<T, T> snapshot_value)
        where T : Qx.Messages.IParserComposer<T> =>
        OnIncoming(
            message_contract,
            (message, state_generation) =>
            {
                T value = snapshot_value(message);
                Store(state_generation, kind, value, static () => { });
            });

    private void OnChestCoins(CoinsChestContents message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestCoins,
            message,
            () => Chest(message.ChestId).Coins = message.Coins);
    }

    private void OnChestItemsChunk(ItemsChestContentsChunk message, long state_generation)
    {
        WiredChestItemsChunkSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ChestItemsChunk,
            value,
            () => Chest(message.ChestId).Apply(value));
    }

    private void OnChestItemsUpdated(ItemsChestContentsUpdated message, long state_generation)
    {
        WiredChestItemsUpdatedSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ChestItemsUpdated,
            value,
            () => Chest(message.ChestId).Apply(value));
    }

    private void OnChestUpgradeResult(UpgradeChestResult message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestUpgradeResult,
            message,
            () => Chest(message.ChestId).LastUpgradeResult = message);
    }

    private void OnChestPreferencesUpdated(
        ChestPreferencesUpdateSuccess message,
        long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ChestPreferencesUpdated,
            message,
            () => Chest(message.ChestId).LastPreferencesResult = message);
    }

    private void OnTransactionSucceeded(WiredTransactionSuccess message, long state_generation)
    {
        WiredTransactionSuccess value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TransactionSucceeded,
            value,
            () => trade = trade with { LastTransactionSuccess = value, LastTransactionFailure = null });
    }

    private void OnTransactionFailed(WiredTransactionFail message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TransactionFailed,
            message,
            () => trade = trade with { LastTransactionFailure = message });
    }

    private void OnContractContents(WiredContractContents message, long state_generation)
    {
        WiredContractContents value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.ContractContents,
            value,
            () => contract = contract with
            {
                OpenContractId = value.ContractId,
                Contents = value
            });
    }

    private void OnContractOpened(WiredOpenContract message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ContractOpened,
            message,
            () => contract = contract with
            {
                OpenContractId = message.ContractId,
                Contents = null,
                LastUpdateResult = null
            });
    }

    private void OnContractUpdateResult(WiredContractUpdateResult message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.ContractUpdateResult,
            message,
            () => contract = contract with { LastUpdateResult = message });
    }

    private void OnTradeInitiated(WiredTradeInitiate message, long state_generation)
    {
        WiredTradeInitiate value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TradeInitiated,
            value,
            () => trade = new(WiredTradeStatus.Initiated, value, null, null, null, null, null));
    }

    private void OnTradeItemsUpdated(WiredTradeItemsUpdate message, long state_generation)
    {
        WiredTradingItemsSnapshot value = SnapshotOf(message);
        Store(
            state_generation,
            WiredStateChangeKind.TradeItemsUpdated,
            value,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Active,
                Items = value,
                Cancellation = null
            });
    }

    private void OnTradeCancelled(WiredTradeCancelled message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeCancelled,
            message,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Cancelled,
                Cancellation = message
            });
    }

    private void OnTradeCompleted(WiredTradeCompleted message, long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeCompleted,
            message,
            () => trade = trade with
            {
                Status = WiredTradeStatus.Completed,
                Cancellation = null
            });
    }

    private void OnTradeNotification(
        WiredTradeTransactionNotification message,
        long state_generation)
    {
        Store(
            state_generation,
            WiredStateChangeKind.TradeNotification,
            message,
            () => trade = trade with { LastNotification = message });
    }

    private void Store(
        long state_generation,
        WiredStateChangeKind kind,
        object? value,
        Action mutation)
    {
        lock (publication_sync)
        {
            WiredSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation || !room_active)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                mutation();
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new WiredStateUpdate(kind, updated, value));
        }
    }

    private WiredSnapshot PublishState()
    {
        IReadOnlyList<WiredChestContentsSnapshot> chest_snapshot = ReadOnly(
            chests
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.Snapshot(pair.Key)));
        var updated = new WiredSnapshot(
            generation,
            revision,
            permissions,
            environment,
            click_settings,
            room_settings,
            open_furni_id,
            configuration,
            last_save_succeeded,
            last_validation_error,
            last_menu_error,
            last_reward_result,
            last_opened_chest_id,
            chest_snapshot,
            contract,
            trade);
        Volatile.Write(ref snapshot, updated);
        return updated;
    }

    private ChestState Chest(Id chest_id)
    {
        if (!chests.TryGetValue(chest_id, out ChestState? state))
        {
            state = new ChestState();
            chests.Add(chest_id, state);
        }
        return state;
    }

    internal static WiredConfigurationSnapshot SnapshotOf(WiredConfig value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WiredConfigurationKind kind = value switch
        {
            WiredTriggerConfig => WiredConfigurationKind.Trigger,
            WiredActionConfig => WiredConfigurationKind.Action,
            WiredConditionConfig => WiredConfigurationKind.Condition,
            WiredSelectorConfig => WiredConfigurationKind.Selector,
            WiredAddonConfig => WiredConfigurationKind.Addon,
            WiredVariableConfig => WiredConfigurationKind.Variable,
            _ => throw new InvalidDataException($"Unsupported wired configuration type '{value.GetType().FullName}'.")
        };
        return new WiredConfigurationSnapshot(
            kind,
            value.FurniLimit,
            ReadOnly(value.StuffIds),
            ReadOnly(value.StuffIds2),
            value.StuffTypeId,
            value.Id,
            value.StringParam,
            ReadOnly(value.IntParams),
            ReadOnly(value.VariableIds),
            ReadOnly(value.FurniSourceTypes),
            ReadOnly(value.UserSourceTypes),
            value.Code,
            value.AdvancedMode,
            SnapshotOf(value.InputSources),
            value.AllowWallFurni,
            SnapshotOf(value.Context),
            ReadOnly(value.DefaultIntParams),
            ReadOnly(value.UnityContextTags),
            value.UnityContextLayout,
            value.UnityConditionHasSeparateInvert,
            (value as WiredActionConfig)?.DelayInPulses,
            (value as WiredConditionConfig)?.QuantifierCode,
            (value as WiredConditionConfig)?.QuantifierType,
            (value as WiredConditionConfig)?.DefinitionIsInvert,
            (value as WiredSelectorConfig)?.IsFilter,
            value switch
            {
                WiredConditionConfig condition => condition.IsInvert,
                WiredSelectorConfig selector => selector.IsInvert,
                _ => null
            });
    }

    internal static WiredAllVariablesDiffs SnapshotOf(WiredAllVariablesDiffs value) => new(
        value.AllVariablesHash,
        value.IsLastChunk,
        ReadOnly(value.RemovedVariables),
        ReadOnly(value.AddedOrUpdated.Select(entry =>
            new WiredVariableWithHash(entry.PerVariableHash, CloneOf(entry.Variable)))));

    internal static WiredVariablesForObject SnapshotOf(WiredVariablesForObject value) => new(
        new WiredObjectInspectionData(
            value.Data.Type,
            value.Data.ObjectId,
            value.Data.UserIndex,
            ReadOnly(value.Data.VariableValues),
            value.Data.ConfiguredInWireds is null ? null : ReadOnly(value.Data.ConfiguredInWireds)));

    internal static WiredAllVariableHolders SnapshotOf(WiredAllVariableHolders value) => new(
        value.LeadingValue,
        CloneOf(value.VariableInfoAndHolders));

    internal static WiredUserPermanentVariables SnapshotOf(WiredUserPermanentVariables value)
    {
        WiredUserPermanentVariablesList list = value.List;
        return new WiredUserPermanentVariables(new WiredUserPermanentVariablesList(
            list.EntityType,
            list.EntityId,
            list.EntityName,
            list.EntityFigure,
            list.OwnerId,
            list.OwnerName,
            list.OwnerFigure,
            ReadOnly(list.VariableStorage)));
    }

    internal static WiredUserVariablesList SnapshotOf(WiredUserVariablesList value)
    {
        WiredUserVariablesPage page = value.Page;
        return new WiredUserVariablesList(new WiredUserVariablesPage(
            page.VariableId,
            page.TotalEntries,
            page.CurrentPage,
            page.Amount,
            ReadOnly(page.Elements),
            page.UserTypeFilter,
            page.SortTypeFilter));
    }

    internal static WiredRoomStats SnapshotOf(WiredRoomStats value) => value;

    internal static WiredRoomLogs SnapshotOf(WiredRoomLogs value) => new(
        new WiredLogPage(
            value.Page.TotalEntries,
            value.Page.CurrentPage,
            value.Page.Amount,
            ReadOnly(value.Page.Elements),
            value.Page.LogLevelFilter,
            value.Page.LogSourceFilter,
            value.Page.Query));

    internal static WiredErrorLogs SnapshotOf(WiredErrorLogs value) =>
        new(ReadOnly(value.Errors));

    internal static WiredTransactionLogList SnapshotOf(WiredTransactionLogList value) => new(
        new WiredTransactionLogPage(
            value.Logs.LogListType,
            value.Logs.LogListId,
            value.Logs.TotalLogs,
            value.Logs.CurrentPage,
            value.Logs.Amount,
            ReadOnly(value.Logs.Logs)));

    internal static WiredTransactionLogDetails SnapshotOf(WiredTransactionLogDetails value) => new(
        new WiredTransactionDetails(
            value.Details.TransactionInfo,
            ReadOnly(value.Details.ChestIds),
            ReadOnly(value.Details.DepositedFurnis),
            ReadOnly(value.Details.WithdrawnFurnis),
            value.Details.IsIncompleteData));

    internal static WiredContractContents SnapshotOf(WiredContractContents value) => new(
        value.ContractId,
        value.ContractType,
        SnapshotOf(value.Definition),
        value.PaymentMode,
        value.ReceiveText,
        value.LayoutType,
        value.RewardCategory,
        value.ShowDialog,
        value.RewardText);

    internal static WiredTransactionSuccess SnapshotOf(WiredTransactionSuccess value) => new(
        value.TransactionSuccessTypeId,
        value.RewardContents is null ? null : SnapshotOf(value.RewardContents),
        value.RewardText,
        value.OpenByDefault);

    internal static WiredChestItemsChunkSnapshot SnapshotOf(ItemsChestContentsChunk value) => new(
        value.ChestId,
        value.TotalFragments,
        value.FragmentNo,
        ReadOnly(value.StorageChunk.Select(SnapshotOf)));

    internal static WiredChestItemsUpdatedSnapshot SnapshotOf(ItemsChestContentsUpdated value) => new(
        value.ChestId,
        ReadOnly(value.RemovedIds),
        ReadOnly(value.AddedStorage.Select(SnapshotOf)));

    internal static WiredTradingItemsSnapshot SnapshotOf(WiredTradeItemsUpdate value) => new(
        value.TradingItems.FirstUserId,
        ReadOnly(value.TradingItems.FirstUserItems.Select(SnapshotOf)),
        value.TradingItems.FirstUserNumItems,
        value.TradingItems.FirstUserNumCredits,
        value.TradingItems.SecondUserId,
        ReadOnly(value.TradingItems.SecondUserItems.Select(SnapshotOf)),
        value.TradingItems.SecondUserNumItems,
        value.TradingItems.SecondUserNumCredits,
        value.CanAccept,
        value.Extra);

    internal static WiredTradeInitiate SnapshotOf(WiredTradeInitiate value) => new(
        SnapshotOf(value.Requirement),
        value.ShowRequirementsImmediate,
        value.OverridePreviousTrade,
        value.TimeoutSeconds);

    internal static WiredVariableSnapshot SnapshotOf(WiredVariable value) => new(
        value.VariableId,
        value.VariableType,
        value.VariableName,
        value.AvailabilityType,
        value.VariableTarget,
        value.AlwaysAvailable,
        value.CanCreateAndDelete,
        value.HasValue,
        value.CanWriteValue,
        value.CanInterceptChanges,
        value.IsInvisible,
        value.CanReadCreationTime,
        value.CanReadLastUpdateTime,
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair =>
                new WiredTextConnectorSnapshot(pair.Key, pair.Value))));

    private static WiredEnvironment SnapshotOf(WiredEnvironment value) => new(
        value.HasClickUserWired,
        value.EnabledAchievements is null ? null : ReadOnly(value.EnabledAchievements));

    private static WiredValidationError SnapshotOf(WiredValidationError value) => new(
        value.LocalizationKey,
        ReadOnly(value.Parameters));

    private static WiredChestStorageSnapshot SnapshotOf(ChestStorage value) => new(
        value.InventoryId,
        value.LockState,
        value.TransactionId,
        value.Type,
        value.Groupable,
        value.SpecialType,
        SnapshotOf(value.StuffData),
        value.Extra);

    private static WiredTradeItemSnapshot SnapshotOf(TradeItem value) => new(
        value.ItemId,
        value.Type,
        value.Id,
        value.Kind,
        value.Category,
        value.IsGroupable,
        SnapshotOf(value.Data),
        value.CreationDay,
        value.CreationMonth,
        value.CreationYear,
        value.Extra);

    private static ItemDataSnapshot SnapshotOf(ItemData value)
    {
        ItemDataSnapshot data = SnapshotFactory.From(value);
        return data with
        {
            MapEntries = data.MapEntries is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(data.MapEntries, StringComparer.Ordinal)),
            StringValues = data.StringValues is null ? null : ReadOnly(data.StringValues),
            IntValues = data.IntValues is null ? null : ReadOnly(data.IntValues),
            HighScores = data.HighScores is null
                ? null
                : ReadOnly(data.HighScores.Select(score => score with
                {
                    Names = ReadOnly(score.Names)
                }))
        };
    }

    private static WiredInputSourcesSnapshot SnapshotOf(InputSourcesConf value) => new(
        ReadOnly(value.AllowedFurniSources.Select(ReadOnly)),
        ReadOnly(value.AllowedUserSources.Select(ReadOnly)),
        ReadOnly(value.DefaultFurniSources),
        ReadOnly(value.DefaultUserSources));

    private static IReadOnlyList<WiredContextEntrySnapshot> SnapshotOf(WiredContext value) =>
        ReadOnly(value.Entries.Select(entry => SnapshotOf(entry.Tag, entry.Value)));

    private static WiredContextEntrySnapshot SnapshotOf(
        int tag,
        IWiredContextEntry value) => (tag, value) switch
    {
        (WiredContext.TagRoomVariables, AllVariablesInRoom room) => new(
            tag,
            WiredContextValueKind.RoomVariables,
            room.Hash,
            null,
            null,
            null,
            null,
            null,
            null),
        (WiredContext.TagFurniVariableInfo, VariableInfoAndHolders holders) => new(
            tag,
            WiredContextValueKind.FurniVariable,
            null,
            ContextSnapshotOf(holders.Variable),
            ReadOnly(holders.Holders.Select(item =>
                new WiredObjectValueSnapshot(item.ObjectId, item.Value))),
            null,
            null,
            null,
            null),
        (WiredContext.TagUserVariableInfo, VariableInfoAndHolders holders) => new(
            tag,
            WiredContextValueKind.UserVariable,
            null,
            ContextSnapshotOf(holders.Variable),
            ReadOnly(holders.Holders.Select(item =>
                new WiredObjectValueSnapshot(item.ObjectId, item.Value))),
            null,
            null,
            null,
            null),
        (WiredContext.TagGlobalVariableInfo, VariableInfoAndValue current) => new(
            tag,
            WiredContextValueKind.GlobalVariable,
            null,
            ContextSnapshotOf(current.Variable),
            null,
            current.Value,
            null,
            null,
            null),
        (WiredContext.TagReferenceVariables, SharedVariableList shared) => new(
            tag,
            WiredContextValueKind.ReferenceVariables,
            null,
            null,
            null,
            null,
            ReadOnly(shared.SharedVariables.Select(item =>
                SnapshotOf(item))),
            null,
            null),
        (WiredContext.TagRulesetVariables, VariableList variables) => new(
            tag,
            WiredContextValueKind.RulesetVariables,
            null,
            null,
            null,
            null,
            null,
            ReadOnly(variables.Variables.Select(ContextSnapshotOf)),
            null),
        (WiredContext.TagReferencePlaceholders, SharedGlobalPlaceholderList placeholders) => new(
            tag,
            WiredContextValueKind.ReferencePlaceholders,
            null,
            null,
            null,
            null,
            null,
            null,
            ReadOnly(placeholders.SharedPlaceholders.Select(item =>
                new WiredSharedPlaceholderSnapshot(
                    item.RoomId,
                    item.RoomName,
                    item.PlaceholderName)))),
        _ => throw new InvalidDataException(
            $"Unsupported Wired context tag {tag} with value '{value.GetType().FullName}'.")
    };

    private static WiredVariable CloneOf(WiredVariable value) => new()
    {
        VariableId = value.VariableId,
        VariableType = value.VariableType,
        VariableName = value.VariableName,
        AvailabilityType = value.AvailabilityType,
        VariableTarget = value.VariableTarget,
        AlwaysAvailable = value.AlwaysAvailable,
        CanCreateAndDelete = value.CanCreateAndDelete,
        HasValue = value.HasValue,
        CanWriteValue = value.CanWriteValue,
        CanInterceptChanges = value.CanInterceptChanges,
        IsInvisible = value.IsInvisible,
        CanReadCreationTime = value.CanReadCreationTime,
        CanReadLastUpdateTime = value.CanReadLastUpdateTime,
        TextConnector = value.TextConnector is null ? null : ReadOnly(value.TextConnector)
    };

    private static WiredSharedVariableSnapshot SnapshotOf(SharedVariable value)
    {
        WiredVariable variable = value.WiredVariable;
        return new WiredSharedVariableSnapshot(
            value.RoomId,
            value.RoomName,
            variable.VariableId,
            variable.VariableType,
            variable.VariableName,
            variable.AvailabilityType,
            variable.VariableTarget,
            variable.AlwaysAvailable,
            variable.CanCreateAndDelete,
            variable.HasValue,
            variable.CanWriteValue,
            variable.CanInterceptChanges,
            variable.IsInvisible,
            variable.CanReadCreationTime,
            variable.CanReadLastUpdateTime,
            variable.TextConnector is null
                ? null
                : ReadOnly(variable.TextConnector.Select(pair => pair.Key)),
            variable.TextConnector is null
                ? null
                : ReadOnly(variable.TextConnector.Select(pair => pair.Value)));
    }

    private static WiredContextVariableSnapshot ContextSnapshotOf(WiredVariable value) => new(
        value.VariableId,
        value.VariableType,
        value.VariableName,
        value.AvailabilityType,
        value.VariableTarget,
        value.AlwaysAvailable,
        value.CanCreateAndDelete,
        value.HasValue,
        value.CanWriteValue,
        value.CanInterceptChanges,
        value.IsInvisible,
        value.CanReadCreationTime,
        value.CanReadLastUpdateTime,
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair => pair.Key)),
        value.TextConnector is null
            ? null
            : ReadOnly(value.TextConnector.Select(pair => pair.Value)));

    private static VariableInfoAndHolders CloneOf(VariableInfoAndHolders value) => new(
        CloneOf(value.Variable),
        ReadOnly(value.Holders));

    private static TradeRequirement SnapshotOf(TradeRequirement value) => new(
        value.Type,
        value.YouGetText,
        value.LayoutType,
        value.Rules is null ? null : new TradeRequirementRules(
            SnapshotOf(value.Rules.Definition),
            value.Rules.Type,
            value.Rules.Multiplier,
            value.Rules.AutoMultiplierMax));

    private static TradeRequirementRulesDefinition SnapshotOf(
        TradeRequirementRulesDefinition value) => new(
        value.YouGiveRule is null
            ? null
            : ReadOnly(value.YouGiveRule.Select(SnapshotOf)),
        value.YouGetRule is null ? null : SnapshotOf(value.YouGetRule));

    private static TradeRequirementRule SnapshotOf(TradeRequirementRule value) => new(
        ReadOnly(value.Nodes));

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed class ChestState
    {
        private readonly Dictionary<int, IReadOnlyList<WiredChestStorageSnapshot>> fragments = [];
        private IReadOnlyList<WiredChestStorageSnapshot> items = [];

        public int? Coins { get; set; }
        public int ExpectedFragments { get; private set; }
        public bool ItemsComplete { get; private set; }
        public UpgradeChestResult? LastUpgradeResult { get; set; }
        public ChestPreferencesUpdateSuccess? LastPreferencesResult { get; set; }

        public void Apply(WiredChestItemsChunkSnapshot value)
        {
            if (value.TotalFragments <= 0 || value.FragmentNo < 0)
                return;
            if (ExpectedFragments != value.TotalFragments ||
                fragments.ContainsKey(value.FragmentNo) &&
                (ItemsComplete || value.FragmentNo is 0 or 1))
            {
                fragments.Clear();
                ExpectedFragments = value.TotalFragments;
                ItemsComplete = false;
            }
            fragments[value.FragmentNo] = value.StorageChunk;
            items = ReadOnly(fragments
                .OrderBy(pair => pair.Key)
                .SelectMany(pair => pair.Value)
                .GroupBy(item => item.InventoryId)
                .Select(group => group.Last()));
            ItemsComplete = fragments.Count == ExpectedFragments &&
                (fragments.Keys.All(index => index >= 0 && index < ExpectedFragments) ||
                    fragments.Keys.All(index => index >= 1 && index <= ExpectedFragments));
        }

        public void Apply(WiredChestItemsUpdatedSnapshot value)
        {
            var removed = new HashSet<int>(value.RemovedIds);
            items = ReadOnly(items
                .Where(item => !removed.Contains(item.InventoryId))
                .Concat(value.AddedStorage)
                .GroupBy(item => item.InventoryId)
                .Select(group => group.Last()));
        }

        public WiredChestContentsSnapshot Snapshot(Id chest_id) => new(
            chest_id,
            Coins,
            items,
            ItemsComplete,
            ExpectedFragments,
            ReadOnly(fragments.Keys.Order()),
            LastUpgradeResult,
            LastPreferencesResult);
    }
}

public sealed record WiredChestItemsChunkSnapshot(
    int ChestId,
    int TotalFragments,
    int FragmentNo,
    IReadOnlyList<WiredChestStorageSnapshot> StorageChunk);

public sealed record WiredChestItemsUpdatedSnapshot(
    int ChestId,
    IReadOnlyList<int> RemovedIds,
    IReadOnlyList<WiredChestStorageSnapshot> AddedStorage);
