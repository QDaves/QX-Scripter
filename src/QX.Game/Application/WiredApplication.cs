using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Wired;
using Qx.Protocol;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Channels;

namespace Qx.Game.Application;

internal sealed class WiredApplication : IApplicationFeature
{
    private static readonly TimeSpan trade_confirmation_delay = TimeSpan.FromSeconds(3);
    private readonly IInterceptor interceptor;
    private readonly GameState game;
    private readonly WiredManager wired;
    private readonly ApplicationMessageDispatcher messages;
    private readonly TimeProvider time_provider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim configuration_lock = new(1, 1);
    private readonly SemaphoreSlim save_lock = new(1, 1);
    private readonly SemaphoreSlim deposit_lock = new(1, 1);
    private readonly ApplicationEventSource<WiredChanged> changed;
    private readonly ApplicationEventSource<WiredEvent<WiredPermissions>> permissions_changed;
    private readonly ApplicationEventSource<WiredEvent<WiredEnvironment>> environment_changed;
    private readonly ApplicationEventSource<WiredEvent<WiredClickSettings>> click_settings_changed;
    private readonly ApplicationEventSource<WiredEvent<WiredRoomSettings>> room_settings_changed;
    private readonly ApplicationEventSource<WiredEvent<Id>> configuration_opened;
    private readonly ApplicationEventSource<WiredEvent<WiredConfigurationSnapshot>> configuration_received;
    private readonly ApplicationEventSource<WiredEvent<WiredConfigurationSaveResult>> configuration_save_result;
    private readonly ApplicationEventSource<WiredEvent<WiredMenuError>> menu_error;
    private readonly ApplicationEventSource<WiredEvent<WiredRewardResult>> reward_result;
    private readonly ApplicationEventSource<WiredEvent<OpenChest>> chest_opened;
    private readonly ApplicationEventSource<WiredEvent<CoinsChestContents>> chest_coins_received;
    private readonly ApplicationEventSource<WiredEvent<WiredChestItemsChunkSnapshot>> chest_items_chunk_received;
    private readonly ApplicationEventSource<WiredEvent<WiredChestItemsUpdatedSnapshot>> chest_items_updated;
    private readonly ApplicationEventSource<WiredEvent<UpgradeChestResult>> chest_upgrade_result;
    private readonly ApplicationEventSource<WiredEvent<ChestPreferencesUpdateSuccess>> chest_preferences_updated;
    private readonly ApplicationEventSource<WiredEvent<WiredTransactionSuccess>> transaction_succeeded;
    private readonly ApplicationEventSource<WiredEvent<WiredTransactionFail>> transaction_failed;
    private readonly ApplicationEventSource<WiredEvent<WiredContractContents>> contract_contents_received;
    private readonly ApplicationEventSource<WiredEvent<WiredOpenContract>> contract_opened;
    private readonly ApplicationEventSource<WiredEvent<WiredContractUpdateResult>> contract_update_result;
    private readonly ApplicationEventSource<WiredEvent<WiredTradeInitiate>> trade_initiated;
    private readonly ApplicationEventSource<WiredEvent<WiredTradingItemsSnapshot>> trade_items_updated;
    private readonly ApplicationEventSource<WiredEvent<WiredTradeCancelled>> trade_cancelled;
    private readonly ApplicationEventSource<WiredEvent<WiredTradeCompleted>> trade_completed;
    private readonly ApplicationEventSource<WiredEvent<WiredTradeTransactionNotification>> trade_notification;
    private int disposed;

    public WiredApplication(
        IInterceptor interceptor,
        GameState game,
        ApplicationMessageDispatcher messages,
        TimeProvider time_provider,
        Action<Exception>? observer_error = null)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(time_provider);
        this.interceptor = interceptor;
        this.game = game;
        wired = game.Wired;
        this.messages = messages;
        this.time_provider = time_provider;
        changed = new(observer_error);
        permissions_changed = new(observer_error);
        environment_changed = new(observer_error);
        click_settings_changed = new(observer_error);
        room_settings_changed = new(observer_error);
        configuration_opened = new(observer_error);
        configuration_received = new(observer_error);
        configuration_save_result = new(observer_error);
        menu_error = new(observer_error);
        reward_result = new(observer_error);
        chest_opened = new(observer_error);
        chest_coins_received = new(observer_error);
        chest_items_chunk_received = new(observer_error);
        chest_items_updated = new(observer_error);
        chest_upgrade_result = new(observer_error);
        chest_preferences_updated = new(observer_error);
        transaction_succeeded = new(observer_error);
        transaction_failed = new(observer_error);
        contract_contents_received = new(observer_error);
        contract_opened = new(observer_error);
        contract_update_result = new(observer_error);
        trade_initiated = new(observer_error);
        trade_items_updated = new(observer_error);
        trade_cancelled = new(observer_error);
        trade_completed = new(observer_error);
        trade_notification = new(observer_error);

        try
        {
            Bindings = Array.AsReadOnly<IApplicationBinding>(
            [
                new ApplicationCallBinding<WiredStateRequest, WiredStateView>(
                    WiredApplicationDescriptors.State, ReadState),
                new ApplicationCallBinding<WiredConfigurationOpenRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ConfigurationOpen, OpenConfiguration),
                new ApplicationCallBinding<WiredConfigurationGetRequest, WiredConfigurationSnapshot>(
                    WiredApplicationDescriptors.ConfigurationGet, GetConfiguration),
                new ApplicationCallBinding<WiredConfigurationApplySnapshotRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ConfigurationSnapshotApply, ApplyConfigurationSnapshot),
                new ApplicationCallBinding<WiredTriggerSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationTriggerSave, SaveTrigger),
                new ApplicationCallBinding<WiredActionSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationActionSave, SaveAction),
                new ApplicationCallBinding<WiredConditionSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationConditionSave, SaveCondition),
                new ApplicationCallBinding<WiredSelectorSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationSelectorSave, SaveSelector),
                new ApplicationCallBinding<WiredAddonSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationAddonSave, SaveAddon),
                new ApplicationCallBinding<WiredVariableSaveRequest, WiredConfigurationSaveResult>(
                    WiredApplicationDescriptors.ConfigurationVariableSave, SaveVariable),
                new ApplicationCallBinding<WiredTimeoutRequest, WiredAllVariablesHash>(
                    WiredApplicationDescriptors.VariablesHashGet, GetVariablesHash),
                new ApplicationCallBinding<WiredVariableDifferencesRequest, WiredVariableDifferencesSnapshot>(
                    WiredApplicationDescriptors.VariablesDifferencesGet, GetVariableDifferences),
                new ApplicationCallBinding<WiredVariableListRequest, WiredVariableCollectionSnapshot>(
                    WiredApplicationDescriptors.VariablesList, ListVariables),
                new ApplicationCallBinding<WiredVariablesObjectRequest, WiredVariablesObjectSnapshot>(
                    WiredApplicationDescriptors.VariablesObjectGet, GetObjectVariables),
                new ApplicationCallBinding<WiredVariableHoldersRequest, WiredVariableHoldersSnapshot>(
                    WiredApplicationDescriptors.VariablesHoldersGet, GetVariableHolders),
                new ApplicationCallBinding<WiredPermanentVariablesRequest, WiredPermanentVariablesSnapshot>(
                    WiredApplicationDescriptors.VariablesPermanentGet, GetPermanentVariables),
                new ApplicationCallBinding<WiredVariableOwnersRequest, WiredVariableOwnersSnapshot>(
                    WiredApplicationDescriptors.VariablesOwnersGet, GetVariableOwners),
                new ApplicationCallBinding<WiredObjectVariableSetRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.VariablesObjectSet, SetObjectVariable),
                new ApplicationCallBinding<WiredPermanentVariableSetRequest, WiredSetUserPermanentVariableResult>(
                    WiredApplicationDescriptors.VariablesPermanentSet, SetPermanentVariable),
                new ApplicationCallBinding<WiredPermanentVariableSendRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.VariablesPermanentSetSend, SendPermanentVariable),
                new ApplicationCallBinding<WiredTimeoutRequest, WiredRoomSettings>(
                    WiredApplicationDescriptors.RoomSettingsGet, GetRoomSettings),
                new ApplicationCallBinding<WiredRoomSettingsSetRequest, WiredRoomSettings>(
                    WiredApplicationDescriptors.RoomSettingsSet, SetRoomSettings),
                new ApplicationCallBinding<WiredTimeoutRequest, WiredRoomStats>(
                    WiredApplicationDescriptors.RoomStatsGet, GetRoomStats),
                new ApplicationCallBinding<WiredRoomLogsRequest, WiredRoomLogs>(
                    WiredApplicationDescriptors.RoomLogsGet, GetRoomLogs),
                new ApplicationCallBinding<WiredTimeoutRequest, WiredErrorLogs>(
                    WiredApplicationDescriptors.RoomErrorLogsGet, GetErrorLogs),
                new ApplicationCallBinding<WiredCommandRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.RoomErrorLogsClear, ClearErrorLogs),
                new ApplicationCallBinding<WiredUserClickRequest, WiredClickUserResponse>(
                    WiredApplicationDescriptors.RoomUserClick, ClickUser),
                new ApplicationCallBinding<WiredCommandRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.RoomReload, ReloadRoom),
                new ApplicationCallBinding<WiredCommandRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.RoomRollback, RollbackRoom),
                new ApplicationCallBinding<WiredPreferencesSetRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.PreferencesSet, SetPreferences),
                new ApplicationCallBinding<WiredChestRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestOpen, OpenChest),
                new ApplicationCallBinding<WiredChestRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestClose, CloseChest),
                new ApplicationCallBinding<WiredChestsLockRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestsLock, LockChests),
                new ApplicationCallBinding<WiredChestUpgradeRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestUpgrade, UpgradeChest),
                new ApplicationCallBinding<WiredChestRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestWithdrawAll, WithdrawAll),
                new ApplicationCallBinding<WiredChestCoinsWithdrawRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestWithdrawCoins, WithdrawCoins),
                new ApplicationCallBinding<WiredChestItemsWithdrawRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestWithdrawItems, WithdrawItems),
                new ApplicationCallBinding<WiredChestRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestAddStart, StartAddingToChest),
                new ApplicationCallBinding<WiredChestOptionsSetRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestOptionsSet, SetChestOptions),
                new ApplicationCallBinding<WiredChestPreferencesSetRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestPreferencesSet, SetChestPreferences),
                new ApplicationCallBinding<WiredChestNotificationPreferencesSetRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ChestNotificationPreferencesSet, SetChestNotificationPreferences),
                new ApplicationCallBinding<WiredChestDepositRequest, WiredChestDepositResult>(
                    WiredApplicationDescriptors.ChestDeposit, DepositToChest),
                new ApplicationCallBinding<WiredTransactionChestLogsRequest, WiredTransactionLogList>(
                    WiredApplicationDescriptors.TransactionChestLogsGet, GetChestTransactions),
                new ApplicationCallBinding<WiredTransactionRoomLogsRequest, WiredTransactionLogList>(
                    WiredApplicationDescriptors.TransactionRoomLogsGet, GetRoomTransactions),
                new ApplicationCallBinding<WiredTransactionDetailsRequest, WiredTransactionLogDetails>(
                    WiredApplicationDescriptors.TransactionDetailsGet, GetTransactionDetails),
                new ApplicationCallBinding<WiredContractOpenRequest, WiredContractContents>(
                    WiredApplicationDescriptors.ContractOpen, OpenContract),
                new ApplicationCallBinding<WiredContractOpenSendRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ContractOpenSend, SendOpenContract),
                new ApplicationCallBinding<WiredContractUpdateRequest, WiredContractUpdateResult>(
                    WiredApplicationDescriptors.ContractUpdate, UpdateContract),
                new ApplicationCallBinding<WiredContractSendRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.ContractUpdateSend, SendContractUpdate),
                new ApplicationCallBinding<WiredTradeItemsRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.TradeItemsAdd, AddTradeItems),
                new ApplicationCallBinding<WiredTradeItemsRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.TradeItemsRemove, RemoveTradeItems),
                new ApplicationCallBinding<WiredTradeConfirmRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.TradeConfirm, ConfirmTrade),
                new ApplicationCallBinding<WiredCommandRequest, WiredDispatchResult>(
                    WiredApplicationDescriptors.TradeCancel, CancelTrade),
                Event(WiredApplicationDescriptors.Changed, changed),
                Event(WiredApplicationDescriptors.PermissionsChanged, permissions_changed),
                Event(WiredApplicationDescriptors.EnvironmentChanged, environment_changed),
                Event(WiredApplicationDescriptors.ClickSettingsChanged, click_settings_changed),
                Event(WiredApplicationDescriptors.RoomSettingsChanged, room_settings_changed),
                Event(WiredApplicationDescriptors.ConfigurationOpened, configuration_opened),
                Event(WiredApplicationDescriptors.ConfigurationReceived, configuration_received),
                Event(WiredApplicationDescriptors.ConfigurationSaveResult, configuration_save_result),
                Event(WiredApplicationDescriptors.MenuError, menu_error),
                Event(WiredApplicationDescriptors.RewardResult, reward_result),
                Event(WiredApplicationDescriptors.ChestOpened, chest_opened),
                Event(WiredApplicationDescriptors.ChestCoinsReceived, chest_coins_received),
                Event(WiredApplicationDescriptors.ChestItemsChunkReceived, chest_items_chunk_received),
                Event(WiredApplicationDescriptors.ChestItemsUpdated, chest_items_updated),
                Event(WiredApplicationDescriptors.ChestUpgradeResult, chest_upgrade_result),
                Event(WiredApplicationDescriptors.ChestPreferencesUpdated, chest_preferences_updated),
                Event(WiredApplicationDescriptors.TransactionSucceeded, transaction_succeeded),
                Event(WiredApplicationDescriptors.TransactionFailed, transaction_failed),
                Event(WiredApplicationDescriptors.ContractContentsReceived, contract_contents_received),
                Event(WiredApplicationDescriptors.ContractOpened, contract_opened),
                Event(WiredApplicationDescriptors.ContractUpdateResult, contract_update_result),
                Event(WiredApplicationDescriptors.TradeInitiated, trade_initiated),
                Event(WiredApplicationDescriptors.TradeItemsUpdated, trade_items_updated),
                Event(WiredApplicationDescriptors.TradeCancelled, trade_cancelled),
                Event(WiredApplicationDescriptors.TradeCompleted, trade_completed),
                Event(WiredApplicationDescriptors.TradeNotification, trade_notification)
            ]);
            wired.StateChanged += OnStateChanged;
        }
        catch
        {
            lifetime.Cancel();
            DisposeEvents();
            throw;
        }
    }

    public IReadOnlyList<IApplicationBinding> Bindings { get; }

    private ValueTask<WiredStateView> ReadState(
        WiredStateRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ChestOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ChestLimit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.ChestLimit, 10);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ItemOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ItemLimit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.ItemLimit, 50);
        cancellation_token.ThrowIfCancellationRequested();
        lifetime.Token.ThrowIfCancellationRequested();
        WiredSnapshot state = wired.Snapshot;
        WiredChestStateEntry[] chests =
        [
            .. state.Chests
                .OrderBy(chest => (long)chest.ChestId)
                .Skip(request.ChestOffset)
                .Take(request.ChestLimit)
                .Select(chest => new WiredChestStateEntry(
                    chest.ChestId,
                    chest.Coins,
                    chest.Items.Count,
                    request.ItemOffset,
                    request.ItemLimit,
                    Array.AsReadOnly(chest.Items
                        .Skip(request.ItemOffset)
                        .Take(request.ItemLimit)
                        .ToArray()),
                    chest.ItemsComplete,
                    chest.ExpectedFragments,
                    chest.ReceivedFragments,
                    chest.LastUpgradeResult,
                    chest.LastPreferencesResult))
        ];
        var page = new WiredChestStatePage(
            state.Chests.Count,
            request.ChestOffset,
            request.ChestLimit,
            request.ItemOffset,
            request.ItemLimit,
            Array.AsReadOnly(chests));
        return ValueTask.FromResult(new WiredStateView(
            state.Generation,
            state.Revision,
            state.Permissions,
            state.Environment,
            state.ClickSettings,
            state.RoomSettings,
            state.OpenFurniId,
            state.Configuration,
            state.LastSaveSucceeded,
            state.LastValidationError,
            state.LastMenuError,
            state.LastRewardResult,
            state.LastOpenedChestId,
            page,
            state.Contract,
            state.Trade));
    }

    private async ValueTask<WiredConfigurationSnapshot> GetConfiguration(
        WiredConfigurationGetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        long started = time_provider.GetTimestamp();
        await EnterExclusive(
            configuration_lock,
            started,
            request.TimeoutMilliseconds,
            MessageKeys.Wired.Configuration.OpenRequest.Value,
            "wired configuration",
            operation_token).ConfigureAwait(false);
        try
        {
            CaptureCurrentState(scope, operation_token);
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            DispatchInRoom(
                MessageContracts.Wired.Configuration.OpenRequest,
                new WiredOpen(request.FurniId),
                scope,
                operation_token);
            WiredStateUpdate update = await updates.WaitAsync(
                candidate =>
                    candidate.Kind is WiredStateChangeKind.ConfigurationReceived &&
                    candidate.Value is WiredConfigurationSnapshot value &&
                    value.Id == request.FurniId,
                interceptor,
                scope.Session,
                time_provider,
                started,
                request.TimeoutMilliseconds,
                updates.Revision,
                MessageKeys.Wired.Configuration.OpenRequest.Value,
                "wired configuration",
                operation_token).ConfigureAwait(false);
            CaptureCurrentState(scope, operation_token);
            return (WiredConfigurationSnapshot)update.Value!;
        }
        finally
        {
            configuration_lock.Release();
        }
    }

    private ValueTask<WiredDispatchResult> OpenConfiguration(
        WiredConfigurationOpenRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        return Dispatch(
            MessageContracts.Wired.Configuration.OpenRequest,
            new WiredOpen(request.FurniId),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> ApplyConfigurationSnapshot(
        WiredConfigurationApplySnapshotRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.FurniId, nameof(request.FurniId));
        return Dispatch(
            MessageContracts.Wired.Configuration.ApplySnapshot,
            new WiredApplySnapshot(request.FurniId),
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveTrigger(
        WiredTriggerSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.TriggerUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveAction(
        WiredActionSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.ActionUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveCondition(
        WiredConditionSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.ConditionUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveSelector(
        WiredSelectorSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.SelectorUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveAddon(
        WiredAddonSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.AddonUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredConfigurationSaveResult> SaveVariable(
        WiredVariableSaveRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SaveConfiguration(
            request.Update,
            MessageContracts.Wired.Configuration.VariableUpdate,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private async ValueTask<WiredConfigurationSaveResult> SaveConfiguration<T>(
        T update,
        MessageContract<T> message_contract,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
        where T : WiredConfigWrite, IParserComposer<T>
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(update);
        ValidateId(update.FurniId, nameof(update.FurniId));
        ValidateTimeout(timeout_milliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        long started = time_provider.GetTimestamp();
        await EnterExclusive(
            save_lock,
            started,
            timeout_milliseconds,
            message_contract.Key.Value,
            "wired configuration save result",
            operation_token).ConfigureAwait(false);
        try
        {
            CaptureCurrentState(scope, operation_token);
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            DispatchInRoom(
                message_contract,
                update,
                scope,
                operation_token);
            WiredStateUpdate result = await updates.WaitAsync(
                candidate => candidate.Kind is
                    WiredStateChangeKind.SaveSucceeded or
                    WiredStateChangeKind.ValidationFailed,
                interceptor,
                scope.Session,
                time_provider,
                started,
                timeout_milliseconds,
                updates.Revision,
                message_contract.Key.Value,
                "wired configuration save result",
                operation_token).ConfigureAwait(false);
            CaptureCurrentState(scope, operation_token);
            var value = new WiredConfigurationSaveResult(
                result.Kind is WiredStateChangeKind.SaveSucceeded,
                result.Value as WiredValidationError,
                result.State.Generation,
                result.State.Revision);
            return value;
        }
        finally
        {
            save_lock.Release();
        }
    }

    private ValueTask<WiredAllVariablesHash> GetVariablesHash(
        WiredTimeoutRequest request,
        CancellationToken cancellation_token)
    {
        ValidateRequest(request);
        return Request(
            MessageContracts.Wired.Variables.HashRequest,
            new WiredGetAllVariablesHash(),
            MessageContracts.Wired.Variables.Hash,
            null,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredVariableDifferencesSnapshot> GetVariableDifferences(
        WiredVariableDifferencesRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
        IReadOnlyList<VariableHashEntry> cache = request.Cache ?? [];
        ValidateVariableCache(cache);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        return GetVariableDifferences(request, cache, scope, cancellation_token);
    }

    private ValueTask<WiredVariableDifferencesSnapshot> GetVariableDifferences(
        WiredVariableDifferencesRequest request,
        IReadOnlyList<VariableHashEntry> cache,
        WiredOperationScope scope,
        CancellationToken cancellation_token)
    {
        return Request(
            MessageContracts.Wired.Variables.DifferencesRequest,
            new WiredGetAllVariablesDiffs(cache),
            MessageContracts.Wired.Variables.Differences,
            null,
            value => SnapshotOf(value, scope.WiredGeneration),
            request.TimeoutMilliseconds,
            scope,
            cancellation_token);
    }

    private async ValueTask<WiredVariableCollectionSnapshot> ListVariables(
        WiredVariableListRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaximumChunks, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.MaximumChunks, 256);
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        long started = time_provider.GetTimestamp();
        var entries = new Dictionary<string, WiredVariableWithHashSnapshot>(StringComparer.Ordinal);
        var order = new List<string>();
        int all_variables_hash = 0;
        for (int chunk = 1; chunk <= request.MaximumChunks; chunk++)
        {
            VariableHashEntry[] cache =
            [
                .. order
                    .Where(entries.ContainsKey)
                    .Select(id => new VariableHashEntry(id, entries[id].PerVariableHash))
            ];
            int remaining = RemainingMilliseconds(started, request.TimeoutMilliseconds);
            WiredVariableDifferencesSnapshot difference = await GetVariableDifferences(
                new WiredVariableDifferencesRequest(cache, remaining),
                cache,
                scope,
                cancellation_token).ConfigureAwait(false);
            if (difference.Generation != scope.WiredGeneration)
                throw new RequestDisconnectedException("wired variables", "wired variable differences");
            all_variables_hash = difference.AllVariablesHash;
            int changed = 0;
            foreach (string id in difference.RemovedVariables)
            {
                if (entries.Remove(id))
                {
                    order.Remove(id);
                    changed++;
                }
            }
            foreach (WiredVariableWithHashSnapshot entry in difference.AddedOrUpdated)
            {
                string id = entry.Variable.VariableId;
                if (!entries.TryGetValue(id, out WiredVariableWithHashSnapshot? previous))
                {
                    order.Add(id);
                    changed++;
                }
                else if (previous.PerVariableHash != entry.PerVariableHash)
                {
                    changed++;
                }
                entries[id] = entry;
            }
            if (difference.IsLastChunk)
            {
                WiredVariableWithHashSnapshot[] values =
                [
                    .. order.Where(entries.ContainsKey).Select(id => entries[id])
                ];
                VariableHashEntry[] final_cache =
                [
                    .. values.Select(value => new VariableHashEntry(
                        value.Variable.VariableId,
                        value.PerVariableHash))
                ];
                return new WiredVariableCollectionSnapshot(
                    scope.WiredGeneration,
                    all_variables_hash,
                    chunk,
                    Array.AsReadOnly(values),
                    Array.AsReadOnly(final_cache));
            }
            if (changed == 0)
            {
                throw new InvalidDataException(
                    "The Wired variable difference stream did not make progress.");
            }
        }
        throw new InvalidDataException(
            $"The Wired variable difference stream exceeded {request.MaximumChunks} chunks.");
    }

    private ValueTask<WiredVariablesObjectSnapshot> GetObjectVariables(
        WiredVariablesObjectRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateVariableTarget(request.Target, request.ObjectId);
        ValidateTimeout(request.TimeoutMilliseconds);
        int target = (int)request.Target;
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        return Request(
            MessageContracts.Wired.Variables.ObjectRequest,
            new WiredGetVariablesForObject(target, request.ObjectId),
            MessageContracts.Wired.Variables.Object,
            value => MatchesObject(value.Data, target, request.ObjectId),
            value => SnapshotOf(value, scope.WiredGeneration),
            request.TimeoutMilliseconds,
            scope,
            cancellation_token);
    }

    private ValueTask<WiredVariableHoldersSnapshot> GetVariableHolders(
        WiredVariableHoldersRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.VariableId, nameof(request.VariableId), false);
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        return Request(
            MessageContracts.Wired.Variables.HoldersRequest,
            new WiredGetAllVariableHolders(request.VariableId),
            MessageContracts.Wired.Variables.Holders,
            value => value.VariableInfoAndHolders.Variable.VariableId == request.VariableId,
            value => SnapshotOf(value, scope.WiredGeneration),
            request.TimeoutMilliseconds,
            scope,
            cancellation_token);
    }

    private ValueTask<WiredPermanentVariablesSnapshot> GetPermanentVariables(
        WiredPermanentVariablesRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateEntity(request.EntityType, request.EntityId);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Variables.PermanentRequest,
            new WiredGetUserPermanentVariables(request.EntityType, request.EntityId),
            MessageContracts.Wired.Variables.Permanent,
            value =>
                value.List.EntityType == request.EntityType &&
                value.List.EntityId == request.EntityId,
            SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredVariableOwnersSnapshot> GetVariableOwners(
        WiredVariableOwnersRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.VariableId, nameof(request.VariableId), false);
        ValidatePage(request.Page, request.PageSize);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Variables.OwnersRequest,
            new WiredGetVariableOwnersPage(
                request.VariableId,
                request.Page,
                request.PageSize,
                request.UserTypeFilter,
                request.SortTypeFilter),
            MessageContracts.Wired.Variables.Owners,
            value =>
                value.Page.VariableId == request.VariableId &&
                value.Page.CurrentPage == request.Page &&
                value.Page.UserTypeFilter == request.UserTypeFilter &&
                value.Page.SortTypeFilter == request.SortTypeFilter,
            SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SetObjectVariable(
        WiredObjectVariableSetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateVariableTarget(request.Target, request.ObjectId);
        ValidateText(request.VariableId, nameof(request.VariableId), false);
        ValidateVariableOperation(request.Operation);
        return Dispatch(
            MessageContracts.Wired.Variables.ObjectValueSet,
            new WiredSetObjectVariableValue(
                (int)request.Target,
                request.ObjectId,
                request.VariableId,
                request.Value,
                request.Operation),
            cancellation_token);
    }

    private ValueTask<WiredSetUserPermanentVariableResult> SetPermanentVariable(
        WiredPermanentVariableSetRequest request,
        CancellationToken cancellation_token)
    {
        ValidatePermanentVariableRequest(request);
        return Request(
            MessageContracts.Wired.Variables.PermanentValueSet,
            new WiredSetUserPermanentVariable(
                request.EntityType,
                request.EntityId,
                request.VariableId,
                request.Value,
                request.Operation),
            MessageContracts.Wired.Variables.PermanentValueSetResult,
            null,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SendPermanentVariable(
        WiredPermanentVariableSendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateEntity(request.EntityType, request.EntityId);
        ValidateText(request.VariableId, nameof(request.VariableId), false);
        ValidateVariableOperation(request.Operation);
        return Dispatch(
            MessageContracts.Wired.Variables.PermanentValueSet,
            new WiredSetUserPermanentVariable(
                request.EntityType,
                request.EntityId,
                request.VariableId,
                request.Value,
                request.Operation),
            cancellation_token);
    }

    private ValueTask<WiredRoomSettings> GetRoomSettings(
        WiredTimeoutRequest request,
        CancellationToken cancellation_token)
    {
        ValidateRequest(request);
        return Request(
            MessageContracts.Wired.Room.SettingsRequest,
            new WiredGetRoomSettings(),
            MessageContracts.Wired.Room.Settings,
            null,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredRoomSettings> SetRoomSettings(
        WiredRoomSettingsSetRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateText(request.Timezone, nameof(request.Timezone), false);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Room.SettingsUpdate,
            new WiredSetRoomSettings(
                request.ModifyPermissionMask,
                request.ReadPermissionMask,
                request.Timezone),
            MessageContracts.Wired.Room.Settings,
            value =>
                value.ModifyPermissionMask == request.ModifyPermissionMask &&
                value.ReadPermissionMask == request.ReadPermissionMask &&
                value.Timezone == request.Timezone,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredRoomStats> GetRoomStats(
        WiredTimeoutRequest request,
        CancellationToken cancellation_token)
    {
        ValidateRequest(request);
        return Request(
            MessageContracts.Wired.Room.StatsRequest,
            new WiredGetRoomStats(),
            MessageContracts.Wired.Room.Stats,
            null,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredRoomLogs> GetRoomLogs(
        WiredRoomLogsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Page, request.PageSize);
        ValidateText(request.Query, nameof(request.Query), true);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Room.LogsRequest,
            new WiredGetRoomLogs(
                request.Page,
                request.PageSize,
                request.LogLevelFilter,
                request.LogSourceFilter,
                request.Query),
            MessageContracts.Wired.Room.Logs,
            value =>
                value.Page.CurrentPage == request.Page &&
                (value.Page.LogLevelFilter ?? -1) == request.LogLevelFilter &&
                (value.Page.LogSourceFilter ?? -1) == request.LogSourceFilter &&
                (value.Page.Query ?? string.Empty) == request.Query,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredErrorLogs> GetErrorLogs(
        WiredTimeoutRequest request,
        CancellationToken cancellation_token)
    {
        ValidateRequest(request);
        return Request(
            MessageContracts.Wired.ErrorLogs.Request,
            new WiredGetErrorLogs(),
            MessageContracts.Wired.ErrorLogs.Snapshot,
            null,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> ClearErrorLogs(
        WiredCommandRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.ErrorLogs.Clear,
            new WiredClearErrorLogs(),
            cancellation_token);
    }

    private ValueTask<WiredClickUserResponse> ClickUser(
        WiredUserClickRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Index);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.UserClick.Request,
            new WiredClickUser(request.Index),
            MessageContracts.Wired.UserClick.Result,
            value => value.Index == request.Index,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> ReloadRoom(
        WiredCommandRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.Room.Update,
            WiredUpdateRoom.Reload,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> RollbackRoom(
        WiredCommandRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.Room.Update,
            WiredUpdateRoom.RollBack,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SetPreferences(
        WiredPreferencesSetRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preferences);
        ValidateText(request.Preferences.UiStyle, nameof(request.Preferences.UiStyle), true);
        return Dispatch(
            MessageContracts.Wired.Room.PreferencesUpdate,
            request.Preferences,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> OpenChest(
        WiredChestRequest request,
        CancellationToken cancellation_token)
    {
        ValidateChestRequest(request);
        return Dispatch(
            MessageContracts.Wired.Chests.OpenRequest,
            new OpenChestAndGetContents(request.ChestId),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> CloseChest(
        WiredChestRequest request,
        CancellationToken cancellation_token)
    {
        ValidateChestRequest(request);
        return Dispatch(
            MessageContracts.Wired.Chests.Close,
            new CloseChest(request.ChestId),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> LockChests(
        WiredChestsLockRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.Chests.LockAll,
            new LockAllChests(request.Locked, request.ApplyToAllInRoom),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> UpgradeChest(
        WiredChestUpgradeRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ChestId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.UpgradeAmount);
        return Dispatch(
            MessageContracts.Wired.Chests.Upgrade,
            new UpgradeChest(request.ChestId, request.UpgradeAmount),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> WithdrawAll(
        WiredChestRequest request,
        CancellationToken cancellation_token)
    {
        ValidateChestRequest(request);
        return Dispatch(
            MessageContracts.Wired.Chests.WithdrawAll,
            new WithdrawAllFromChest(request.ChestId),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> WithdrawCoins(
        WiredChestCoinsWithdrawRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ChestId, nameof(request.ChestId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.CoinAmount);
        return Dispatch(
            MessageContracts.Wired.Chests.WithdrawCoins,
            new WithdrawCoinsFromChest(request.ChestId, request.CoinAmount),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> WithdrawItems(
        WiredChestItemsWithdrawRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ChestId, nameof(request.ChestId));
        ArgumentNullException.ThrowIfNull(request.ItemType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Count);
        return Dispatch(
            MessageContracts.Wired.Chests.WithdrawItems,
            new WithdrawItemsFromChest(request.ChestId, request.ItemType, request.Count),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> StartAddingToChest(
        WiredChestRequest request,
        CancellationToken cancellation_token)
    {
        ValidateChestRequest(request);
        return Dispatch(
            MessageContracts.Wired.Chests.StartAdding,
            new StartAddingToChest(request.ChestId),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SetChestOptions(
        WiredChestOptionsSetRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Options);
        ValidateId(request.Options.ChestId, nameof(request.Options.ChestId));
        ArgumentOutOfRangeException.ThrowIfNegative(request.Options.Capacity);
        return Dispatch(
            MessageContracts.Wired.Chests.OptionsUpdate,
            request.Options,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SetChestPreferences(
        WiredChestPreferencesSetRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preferences);
        ValidateId(request.Preferences.ChestId, nameof(request.Preferences.ChestId));
        ValidateText(request.Preferences.ChestName, nameof(request.Preferences.ChestName), true);
        ValidateText(request.Preferences.ChestDescription, nameof(request.Preferences.ChestDescription), true);
        return Dispatch(
            MessageContracts.Wired.Chests.PreferencesUpdate,
            request.Preferences,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SetChestNotificationPreferences(
        WiredChestNotificationPreferencesSetRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preferences);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Preferences.ChestId);
        return Dispatch(
            MessageContracts.Wired.Chests.NotificationPreferencesUpdate,
            request.Preferences,
            cancellation_token);
    }

    private async ValueTask<WiredChestDepositResult> DepositToChest(
        WiredChestDepositRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ChestId, nameof(request.ChestId));
        if ((long)request.ChestId > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.ChestId));
        Id[] inventory_ids = ValidateInventoryIds(request.InventoryIds);
        ValidateTimeout(request.TimeoutMilliseconds);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        long started = time_provider.GetTimestamp();
        await EnterExclusive(
            deposit_lock,
            started,
            request.TimeoutMilliseconds,
            MessageKeys.Wired.Chests.StartAdding.Value,
            "wired chest deposit",
            operation_token).ConfigureAwait(false);
        bool trade_active = false;
        try
        {
            CaptureCurrentState(scope, operation_token);
            using var updates = new WiredUpdateQueue(wired, scope.WiredGeneration);
            DispatchInRoom(
                MessageContracts.Wired.Chests.OpenRequest,
                new OpenChestAndGetContents(request.ChestId),
                scope,
                operation_token);
            WiredStateUpdate opened = await updates.WaitAsync(
                candidate =>
                    candidate.Kind is WiredStateChangeKind.ChestItemsChunk &&
                    candidate.Value is WiredChestItemsChunkSnapshot chunk &&
                    (Id)chunk.ChestId == request.ChestId &&
                    candidate.State.Chests.Any(chest =>
                        chest.ChestId == request.ChestId && chest.ItemsComplete),
                interceptor,
                scope.Session,
                time_provider,
                started,
                request.TimeoutMilliseconds,
                updates.Revision,
                MessageKeys.Wired.Chests.OpenRequest.Value,
                MessageKeys.Wired.Chests.ItemsChunk.Value,
                operation_token).ConfigureAwait(false);
            DispatchInRoom(
                MessageContracts.Wired.Chests.StartAdding,
                new StartAddingToChest(request.ChestId),
                scope,
                operation_token);
            trade_active = true;
            WiredStateUpdate initiated = await WaitTradeUpdate(
                updates,
                candidate => candidate.Kind is WiredStateChangeKind.TradeInitiated,
                scope.Session,
                started,
                request.TimeoutMilliseconds,
                opened.State.Revision,
                operation_token).ConfigureAwait(false);
            if (TryDepositFailure(initiated, inventory_ids.Length, out WiredChestDepositResult? failure))
            {
                trade_active = false;
                return failure;
            }
            if (initiated.Kind is WiredStateChangeKind.TradeCompleted)
            {
                trade_active = false;
                return DepositFailure(
                    "The Wired trade completed before it was initiated for this deposit.",
                    inventory_ids.Length,
                    initiated.State);
            }
            DispatchInRoom(
                MessageContracts.Wired.Trade.ItemsUpdate,
                WiredTradeAddDeleteItems.Add(inventory_ids),
                scope,
                operation_token);
            var requested = new HashSet<Id>(inventory_ids);
            WiredStateUpdate offered = await WaitTradeUpdate(
                updates,
                candidate => candidate.Kind is WiredStateChangeKind.TradeItemsUpdated &&
                    candidate.Value is WiredTradingItemsSnapshot trade &&
                    trade.CanAccept &&
                    trade.FirstUserItems.Any(item => requested.Contains(item.ItemId)),
                scope.Session,
                started,
                request.TimeoutMilliseconds,
                initiated.State.Revision,
                operation_token).ConfigureAwait(false);
            if (TryDepositFailure(offered, inventory_ids.Length, out failure))
            {
                trade_active = false;
                return failure;
            }
            if (offered.Kind is WiredStateChangeKind.TradeCompleted)
            {
                trade_active = false;
                return DepositFailure(
                    "The Wired trade completed before the deposit offer was accepted.",
                    inventory_ids.Length,
                    offered.State);
            }
            var trade_items = (WiredTradingItemsSnapshot)offered.Value!;
            Id[] accepted_ids =
            [
                .. trade_items.FirstUserItems
                    .Select(item => item.ItemId)
                    .Where(requested.Contains)
                    .Distinct()
            ];
            if (accepted_ids.Length == 0)
                return DepositFailure("The Wired chest accepted none of the requested items.", inventory_ids.Length, offered.State);
            DispatchInRoom(
                MessageContracts.Wired.Trade.Confirm,
                new WiredTradeConfirm(false),
                scope,
                operation_token);
            int confirmation_remaining = RemainingMilliseconds(started, request.TimeoutMilliseconds);
            TimeSpan delay = trade_confirmation_delay < TimeSpan.FromMilliseconds(confirmation_remaining)
                ? trade_confirmation_delay
                : throw new RequestTimeoutException(
                    MessageKeys.Wired.Trade.Confirm.Value,
                    MessageKeys.Wired.Trade.Completed.Value,
                    request.TimeoutMilliseconds);
            await Task.Delay(delay, time_provider, operation_token).ConfigureAwait(false);
            CaptureCurrentState(scope, operation_token);
            if (updates.TryTake(
                IsTradeTerminal,
                offered.State.Revision,
                out WiredStateUpdate? early_terminal))
            {
                trade_active = false;
                if (TryDepositFailure(early_terminal, inventory_ids.Length, out failure))
                    return failure;
                return DepositFailure(
                    "The Wired trade completed before final confirmation.",
                    inventory_ids.Length,
                    early_terminal.State);
            }
            DispatchInRoom(
                MessageContracts.Wired.Trade.Confirm,
                new WiredTradeConfirm(true),
                scope,
                operation_token);
            WiredStateUpdate settled = await WaitTradeUpdate(
                updates,
                candidate => candidate.Kind is WiredStateChangeKind.TradeCompleted,
                scope.Session,
                started,
                request.TimeoutMilliseconds,
                offered.State.Revision,
                operation_token).ConfigureAwait(false);
            trade_active = false;
            if (TryDepositFailure(settled, inventory_ids.Length, out failure))
                return failure;
            var accepted = new HashSet<int>(accepted_ids.Select(id => checked((int)(long)id)));
            var observed = new HashSet<int>();
            long contents_revision = updates.Revision;
            void ApplyContents(WiredStateUpdate contents)
            {
                contents_revision = contents.State.Revision;
                var contents_update = (WiredChestItemsUpdatedSnapshot)contents.Value!;
                foreach (WiredChestStorageSnapshot item in contents_update.AddedStorage)
                {
                    if (accepted.Contains(item.InventoryId))
                        observed.Add(item.InventoryId);
                }
            }
            while (true)
            {
                while (updates.TryTakeChestItemsUpdated(
                    request.ChestId,
                    contents_revision,
                    out WiredStateUpdate? buffered_contents))
                {
                    ApplyContents(buffered_contents);
                }
                WiredSnapshot current = CaptureCurrentState(scope, operation_token);
                WiredChestContentsSnapshot? chest = current.Chests.FirstOrDefault(
                    value => value.ChestId == request.ChestId);
                if (observed.IsSupersetOf(accepted) && chest is not null)
                {
                    Dictionary<int, WiredChestStorageSnapshot> current_items = chest.Items
                        .Where(item => accepted.Contains(item.InventoryId))
                        .GroupBy(item => item.InventoryId)
                        .ToDictionary(group => group.Key, group => group.Last());
                    if (accepted.All(current_items.ContainsKey))
                    {
                        WiredChestStorageSnapshot[] stored =
                        [
                            .. accepted_ids.Select(id =>
                                current_items[checked((int)(long)id)])
                        ];
                        return new WiredChestDepositResult(
                            true,
                            string.Empty,
                            inventory_ids.Length,
                            accepted_ids.Length,
                            Array.AsReadOnly(stored),
                            current.Generation,
                            current.Revision);
                    }
                }
                WiredStateUpdate contents = await updates.WaitChestItemsUpdatedAsync(
                    request.ChestId,
                    interceptor,
                    scope.Session,
                    time_provider,
                    started,
                    request.TimeoutMilliseconds,
                    contents_revision,
                    MessageKeys.Wired.Trade.Confirm.Value,
                    MessageKeys.Wired.Chests.ItemsUpdated.Value,
                    operation_token).ConfigureAwait(false);
                ApplyContents(contents);
            }
        }
        finally
        {
            if (trade_active && IsCurrent(scope))
            {
                try
                {
                    DispatchInRoom(
                        MessageContracts.Wired.Trade.Cancel,
                        new WiredTradeCancel(),
                        scope,
                        CancellationToken.None);
                }
                catch
                {
                }
            }
            deposit_lock.Release();
        }
    }

    private async ValueTask<WiredStateUpdate> WaitTradeUpdate(
        WiredUpdateQueue updates,
        Func<WiredStateUpdate, bool> expected,
        Session session,
        long started,
        int timeout_milliseconds,
        long minimum_revision,
        CancellationToken cancellation_token) =>
        await updates.WaitAsync(
            candidate => expected(candidate) || IsTradeTerminal(candidate),
            interceptor,
            session,
            time_provider,
            started,
            timeout_milliseconds,
            minimum_revision,
            MessageKeys.Wired.Chests.StartAdding.Value,
            "wired trade state",
            cancellation_token).ConfigureAwait(false);

    private static bool IsTradeTerminal(WiredStateUpdate update) => update.Kind is
        WiredStateChangeKind.TradeCancelled or
        WiredStateChangeKind.TransactionFailed or
        WiredStateChangeKind.TradeCompleted;

    private static bool TryDepositFailure(
        WiredStateUpdate update,
        int requested,
        [NotNullWhen(true)]
        out WiredChestDepositResult? result)
    {
        string? reason = update.Kind switch
        {
            WiredStateChangeKind.TradeCancelled when update.Value is WiredTradeCancelled cancelled =>
                $"The Wired chest cancelled the trade with failure type {cancelled.TransactionFailureTypeId}.",
            WiredStateChangeKind.TransactionFailed when update.Value is WiredTransactionFail failed =>
                $"The Wired chest transaction failed with type {failed.TransactionFailureTypeId}.",
            _ => null
        };
        result = reason is null ? null : DepositFailure(reason, requested, update.State);
        return result is not null;
    }

    private static WiredChestDepositResult DepositFailure(
        string reason,
        int requested,
        WiredSnapshot state) => new(
        false,
        reason,
        requested,
        0,
        [],
        state.Generation,
        state.Revision);

    private ValueTask<WiredTransactionLogList> GetChestTransactions(
        WiredTransactionChestLogsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.LogListId);
        ValidatePage(request.Page, request.PageSize);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Transaction.ChestLogsRequest,
            new WiredTransactionGetChestLogs(request.LogListId, request.PageSize, request.Page),
            MessageContracts.Wired.Transaction.Logs,
            value =>
                value.Logs.LogListType == WiredTransactionLogPage.TypeChestLogs &&
                value.Logs.LogListId == request.LogListId &&
                value.Logs.CurrentPage == request.Page,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredTransactionLogList> GetRoomTransactions(
        WiredTransactionRoomLogsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Page, request.PageSize);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Transaction.RoomLogsRequest,
            new WiredTransactionGetRoomLogs(request.PageSize, request.Page),
            MessageContracts.Wired.Transaction.Logs,
            value =>
                value.Logs.LogListType == WiredTransactionLogPage.TypeRoomLogs &&
                value.Logs.CurrentPage == request.Page,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredTransactionLogDetails> GetTransactionDetails(
        WiredTransactionDetailsRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.TransactionId);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Transaction.LogDetailsRequest,
            new WiredTransactionGetLogDetails(request.TransactionId),
            MessageContracts.Wired.Transaction.LogDetails,
            value => value.Details.TransactionInfo.TransactionId == request.TransactionId,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredContractContents> OpenContract(
        WiredContractOpenRequest request,
        CancellationToken cancellation_token)
    {
        ValidateContractOpenRequest(request);
        return Request(
            MessageContracts.Wired.Contracts.OpenRequest,
            new WiredOpenContract(request.ContractId),
            MessageContracts.Wired.Contracts.Contents,
            value => value.ContractId == request.ContractId,
            WiredManager.SnapshotOf,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SendOpenContract(
        WiredContractOpenSendRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ContractId);
        return Dispatch(
            MessageContracts.Wired.Contracts.OpenRequest,
            new WiredOpenContract(request.ContractId),
            cancellation_token);
    }

    private ValueTask<WiredContractUpdateResult> UpdateContract(
        WiredContractUpdateRequest request,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Contract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Contract.ContractId);
        ValidateTimeout(request.TimeoutMilliseconds);
        return Request(
            MessageContracts.Wired.Contracts.Update,
            new WiredUpdateContract(request.Contract),
            MessageContracts.Wired.Contracts.UpdateResult,
            value => value.ContractId == request.Contract.ContractId,
            static value => value,
            request.TimeoutMilliseconds,
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> SendContractUpdate(
        WiredContractSendRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Contract);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Contract.ContractId);
        return Dispatch(
            MessageContracts.Wired.Contracts.Update,
            new WiredUpdateContract(request.Contract),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> AddTradeItems(
        WiredTradeItemsRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        Id[] values = ValidateInventoryIds(request.InventoryIds);
        return Dispatch(
            MessageContracts.Wired.Trade.ItemsUpdate,
            WiredTradeAddDeleteItems.Add(values),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> RemoveTradeItems(
        WiredTradeItemsRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        Id[] values = ValidateInventoryIds(request.InventoryIds);
        return Dispatch(
            MessageContracts.Wired.Trade.ItemsUpdate,
            WiredTradeAddDeleteItems.Remove(values),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> ConfirmTrade(
        WiredTradeConfirmRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.Trade.Confirm,
            new WiredTradeConfirm(request.Confirm),
            cancellation_token);
    }

    private ValueTask<WiredDispatchResult> CancelTrade(
        WiredCommandRequest request,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Dispatch(
            MessageContracts.Wired.Trade.Cancel,
            new WiredTradeCancel(),
            cancellation_token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        lifetime.Cancel();
        wired.StateChanged -= OnStateChanged;
        DisposeEvents();
    }

    private async ValueTask<TResult> Request<TRequest, TResponse, TResult>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<TResponse> response_contract,
        Func<TResponse, bool>? match,
        Func<TResponse, TResult> snapshot,
        int timeout_milliseconds,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        return await Request(
            request_contract,
            request,
            response_contract,
            match,
            snapshot,
            timeout_milliseconds,
            scope,
            cancellation_token).ConfigureAwait(false);
    }

    private async ValueTask<TResult> Request<TRequest, TResponse, TResult>(
        MessageContract<TRequest> request_contract,
        TRequest request,
        MessageContract<TResponse> response_contract,
        Func<TResponse, bool>? match,
        Func<TResponse, TResult> snapshot,
        int timeout_milliseconds,
        WiredOperationScope scope,
        CancellationToken cancellation_token)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(timeout_milliseconds);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        CaptureCurrentState(scope, operation_token);
        TResponse response = await game.Requests.RequestAsync(
            request_contract,
            request,
            response_contract,
            scope.Session,
            value =>
                IsCurrent(scope) &&
                (match?.Invoke(value) ?? true),
            timeout_milliseconds,
            block: false,
            cancellation_token: operation_token,
            max_attempts: 1,
            dispatch_guard: () => CaptureCurrentState(scope, operation_token)).ConfigureAwait(false);
        CaptureCurrentState(scope, operation_token);
        return snapshot(response);
    }

    private ValueTask<WiredDispatchResult> Dispatch<T>(
        MessageContract<T> message_contract,
        T message,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        WiredOperationScope scope = CaptureOperation(cancellation_token);
        using CancellationTokenSource operation = LinkCancellation(cancellation_token);
        CancellationToken operation_token = operation.Token;
        DispatchInRoom(
            message_contract,
            message,
            scope,
            operation_token);
        WiredSnapshot state = CaptureCurrentState(scope, operation_token);
        return ValueTask.FromResult(new WiredDispatchResult(
            scope.Session.Client,
            time_provider.GetUtcNow(),
            state.Generation,
            state.Revision));
    }

    private void OnStateChanged(WiredStateUpdate update)
    {
        DateTimeOffset received_at = time_provider.GetUtcNow();
        changed.Publish(new WiredChanged(
            ChangeKind(update.Kind),
            received_at,
            update.State.Generation,
            update.State.Revision));
        switch (update.Kind)
        {
            case WiredStateChangeKind.Permissions when update.Value is WiredPermissions value:
                Publish(permissions_changed, update, received_at, value);
                break;
            case WiredStateChangeKind.Environment when update.Value is WiredEnvironment value:
                Publish(environment_changed, update, received_at, value);
                break;
            case WiredStateChangeKind.ClickSettings when update.Value is WiredClickSettings value:
                Publish(click_settings_changed, update, received_at, value);
                break;
            case WiredStateChangeKind.RoomSettings when update.Value is WiredRoomSettings value:
                Publish(room_settings_changed, update, received_at, value);
                break;
            case WiredStateChangeKind.ConfigurationOpened when update.Value is WiredOpen value:
                Publish(configuration_opened, update, received_at, value.StuffId);
                break;
            case WiredStateChangeKind.ConfigurationReceived
                when update.Value is WiredConfigurationSnapshot value:
                Publish(configuration_received, update, received_at, value);
                break;
            case WiredStateChangeKind.SaveSucceeded:
                Publish(
                    configuration_save_result,
                    update,
                    received_at,
                    new WiredConfigurationSaveResult(
                        true,
                        null,
                        update.State.Generation,
                        update.State.Revision));
                break;
            case WiredStateChangeKind.ValidationFailed
                when update.Value is WiredValidationError value:
                Publish(
                    configuration_save_result,
                    update,
                    received_at,
                    new WiredConfigurationSaveResult(
                        false,
                        value,
                        update.State.Generation,
                        update.State.Revision));
                break;
            case WiredStateChangeKind.MenuError when update.Value is WiredMenuError value:
                Publish(menu_error, update, received_at, value);
                break;
            case WiredStateChangeKind.RewardResult when update.Value is WiredRewardResult value:
                Publish(reward_result, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestOpened when update.Value is OpenChest value:
                Publish(chest_opened, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestCoins when update.Value is CoinsChestContents value:
                Publish(chest_coins_received, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestItemsChunk
                when update.Value is WiredChestItemsChunkSnapshot value:
                Publish(chest_items_chunk_received, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestItemsUpdated
                when update.Value is WiredChestItemsUpdatedSnapshot value:
                Publish(chest_items_updated, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestUpgradeResult
                when update.Value is UpgradeChestResult value:
                Publish(chest_upgrade_result, update, received_at, value);
                break;
            case WiredStateChangeKind.ChestPreferencesUpdated
                when update.Value is ChestPreferencesUpdateSuccess value:
                Publish(chest_preferences_updated, update, received_at, value);
                break;
            case WiredStateChangeKind.TransactionSucceeded
                when update.Value is WiredTransactionSuccess value:
                Publish(transaction_succeeded, update, received_at, value);
                break;
            case WiredStateChangeKind.TransactionFailed
                when update.Value is WiredTransactionFail value:
                Publish(transaction_failed, update, received_at, value);
                break;
            case WiredStateChangeKind.ContractContents
                when update.Value is WiredContractContents value:
                Publish(contract_contents_received, update, received_at, value);
                break;
            case WiredStateChangeKind.ContractOpened when update.Value is WiredOpenContract value:
                Publish(contract_opened, update, received_at, value);
                break;
            case WiredStateChangeKind.ContractUpdateResult
                when update.Value is WiredContractUpdateResult value:
                Publish(contract_update_result, update, received_at, value);
                break;
            case WiredStateChangeKind.TradeInitiated when update.Value is WiredTradeInitiate value:
                Publish(trade_initiated, update, received_at, value);
                break;
            case WiredStateChangeKind.TradeItemsUpdated
                when update.Value is WiredTradingItemsSnapshot value:
                Publish(trade_items_updated, update, received_at, value);
                break;
            case WiredStateChangeKind.TradeCancelled when update.Value is WiredTradeCancelled value:
                Publish(trade_cancelled, update, received_at, value);
                break;
            case WiredStateChangeKind.TradeCompleted when update.Value is WiredTradeCompleted value:
                Publish(trade_completed, update, received_at, value);
                break;
            case WiredStateChangeKind.TradeNotification
                when update.Value is WiredTradeTransactionNotification value:
                Publish(trade_notification, update, received_at, value);
                break;
        }
    }

    private static void Publish<T>(
        ApplicationEventSource<WiredEvent<T>> source,
        WiredStateUpdate update,
        DateTimeOffset received_at,
        T value) => source.Publish(new WiredEvent<T>(
        update.State.Generation,
        update.State.Revision,
        received_at,
        value));

    private static WiredChangeKind ChangeKind(WiredStateChangeKind kind) => kind switch
    {
        WiredStateChangeKind.Permissions => WiredChangeKind.Permissions,
        WiredStateChangeKind.Environment => WiredChangeKind.Environment,
        WiredStateChangeKind.ClickSettings => WiredChangeKind.ClickSettings,
        WiredStateChangeKind.RoomSettings => WiredChangeKind.RoomSettings,
        WiredStateChangeKind.ConfigurationOpened => WiredChangeKind.ConfigurationOpened,
        WiredStateChangeKind.ConfigurationReceived => WiredChangeKind.ConfigurationReceived,
        WiredStateChangeKind.SaveSucceeded => WiredChangeKind.SaveSucceeded,
        WiredStateChangeKind.ValidationFailed => WiredChangeKind.ValidationFailed,
        WiredStateChangeKind.MenuError => WiredChangeKind.MenuError,
        WiredStateChangeKind.RewardResult => WiredChangeKind.RewardResult,
        WiredStateChangeKind.RoomStats => WiredChangeKind.RoomStats,
        WiredStateChangeKind.RoomLogs => WiredChangeKind.RoomLogs,
        WiredStateChangeKind.ErrorLogs => WiredChangeKind.ErrorLogs,
        WiredStateChangeKind.UserClickResult => WiredChangeKind.UserClickResult,
        WiredStateChangeKind.VariablesHash => WiredChangeKind.VariablesHash,
        WiredStateChangeKind.VariablesDifferences => WiredChangeKind.VariablesDifferences,
        WiredStateChangeKind.VariablesObject => WiredChangeKind.VariablesObject,
        WiredStateChangeKind.VariableHolders => WiredChangeKind.VariableHolders,
        WiredStateChangeKind.PermanentVariables => WiredChangeKind.PermanentVariables,
        WiredStateChangeKind.VariableOwners => WiredChangeKind.VariableOwners,
        WiredStateChangeKind.PermanentVariableSetResult => WiredChangeKind.PermanentVariableSetResult,
        WiredStateChangeKind.ChestOpened => WiredChangeKind.ChestOpened,
        WiredStateChangeKind.ChestCoins => WiredChangeKind.ChestCoins,
        WiredStateChangeKind.ChestItemsChunk => WiredChangeKind.ChestItemsChunk,
        WiredStateChangeKind.ChestItemsUpdated => WiredChangeKind.ChestItemsUpdated,
        WiredStateChangeKind.ChestUpgradeResult => WiredChangeKind.ChestUpgradeResult,
        WiredStateChangeKind.ChestPreferencesUpdated => WiredChangeKind.ChestPreferencesUpdated,
        WiredStateChangeKind.TransactionSucceeded => WiredChangeKind.TransactionSucceeded,
        WiredStateChangeKind.TransactionFailed => WiredChangeKind.TransactionFailed,
        WiredStateChangeKind.TransactionLogs => WiredChangeKind.TransactionLogs,
        WiredStateChangeKind.TransactionLogDetails => WiredChangeKind.TransactionLogDetails,
        WiredStateChangeKind.ContractContents => WiredChangeKind.ContractContents,
        WiredStateChangeKind.ContractOpened => WiredChangeKind.ContractOpened,
        WiredStateChangeKind.ContractUpdateResult => WiredChangeKind.ContractUpdateResult,
        WiredStateChangeKind.TradeInitiated => WiredChangeKind.TradeInitiated,
        WiredStateChangeKind.TradeItemsUpdated => WiredChangeKind.TradeItemsUpdated,
        WiredStateChangeKind.TradeCancelled => WiredChangeKind.TradeCancelled,
        WiredStateChangeKind.TradeCompleted => WiredChangeKind.TradeCompleted,
        WiredStateChangeKind.TradeNotification => WiredChangeKind.TradeNotification,
        WiredStateChangeKind.Reset => WiredChangeKind.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static WiredVariableDifferencesSnapshot SnapshotOf(
        WiredAllVariablesDiffs value,
        long generation) => new(
        generation,
        value.AllVariablesHash,
        value.IsLastChunk,
        ReadOnly(value.RemovedVariables),
        ReadOnly(value.AddedOrUpdated.Select(entry => new WiredVariableWithHashSnapshot(
            entry.PerVariableHash,
            WiredManager.SnapshotOf(entry.Variable)))));

    private static WiredVariablesObjectSnapshot SnapshotOf(
        WiredVariablesForObject value,
        long generation)
    {
        WiredObjectInspectionData data = value.Data;
        int object_id = data.Type == WiredVariableTarget.User ? data.UserIndex : data.ObjectId;
        return new WiredVariablesObjectSnapshot(
            generation,
            (WiredTarget)data.Type,
            object_id,
            ReadOnly(data.VariableValues.Select(pair =>
                new WiredVariableValueSnapshot(pair.Key, pair.Value))),
            data.ConfiguredInWireds is null ? [] : ReadOnly(data.ConfiguredInWireds));
    }

    private static WiredVariableHoldersSnapshot SnapshotOf(
        WiredAllVariableHolders value,
        long generation) => new(
        generation,
        value.LeadingValue,
        WiredManager.SnapshotOf(value.VariableInfoAndHolders.Variable),
        ReadOnly(value.VariableInfoAndHolders.Holders.Select(holder =>
            new WiredObjectValueSnapshot(holder.ObjectId, holder.Value))));

    private static WiredPermanentVariablesSnapshot SnapshotOf(
        WiredUserPermanentVariables value)
    {
        WiredUserPermanentVariablesList list = value.List;
        return new WiredPermanentVariablesSnapshot(
            list.EntityType,
            list.EntityId,
            list.EntityName,
            list.EntityFigure,
            list.HasOwner ? list.OwnerId : null,
            list.OwnerName,
            list.OwnerFigure,
            ReadOnly(list.VariableStorage.Select(SnapshotOf)));
    }

    private static WiredVariableOwnersSnapshot SnapshotOf(WiredUserVariablesList value)
    {
        WiredUserVariablesPage page = value.Page;
        return new WiredVariableOwnersSnapshot(
            page.VariableId,
            page.TotalEntries,
            page.CurrentPage,
            page.Amount,
            ReadOnly(page.Elements.Select(element => new WiredVariableOwnerSnapshot(
                element.EntityType,
                element.EntityId,
                element.EntityName,
                SnapshotOf(element.Storage)))),
            page.UserTypeFilter,
            page.SortTypeFilter);
    }

    private static WiredVariableStorageSnapshot SnapshotOf(
        WiredVariableStorageParameter value) => new(
        value.VariableId,
        value.Value,
        value.CreationTime,
        value.CreationTimeStr,
        value.LastUpdateTime,
        value.LastUpdateTimeStr);

    private async ValueTask EnterExclusive(
        SemaphoreSlim gate,
        long started,
        int timeout_milliseconds,
        string outgoing_name,
        string incoming_name,
        CancellationToken cancellation_token)
    {
        int remaining = RemainingMilliseconds(started, timeout_milliseconds);
        using var timeout = new CancellationTokenSource(remaining);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            timeout.Token);
        try
        {
            await gate.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new RequestTimeoutException(outgoing_name, incoming_name, timeout_milliseconds);
        }
    }

    private WiredOperationScope CaptureOperation(CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        Session session = interceptor.Session ??
            throw new InvalidOperationException("No hotel session is connected.");
        return game.Room.Capture(room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(interceptor.Session, session))
                throw new RequestDisconnectedException("wired operation", "wired response");
            if (!room.IsInRoom)
                throw new InvalidOperationException("No hotel room is active.");
            WiredSnapshot state = wired.Snapshot;
            return new WiredOperationScope(
                session,
                room.Generation,
                state.Generation);
        });
    }

    private CancellationTokenSource LinkCancellation(CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        return CancellationTokenSource.CreateLinkedTokenSource(
            cancellation_token,
            lifetime.Token);
    }

    private WiredSnapshot CaptureCurrentState(
        WiredOperationScope scope,
        CancellationToken cancellation_token)
    {
        ThrowIfDisposed();
        cancellation_token.ThrowIfCancellationRequested();
        return game.Room.Capture(room =>
        {
            cancellation_token.ThrowIfCancellationRequested();
            WiredSnapshot state = wired.Snapshot;
            if (!ReferenceEquals(interceptor.Session, scope.Session) ||
                !room.IsInRoom ||
                room.Generation != scope.RoomGeneration ||
                state.Generation != scope.WiredGeneration)
            {
                throw new RequestDisconnectedException("wired operation", "wired response");
            }
            return state;
        });
    }

    private bool IsCurrent(WiredOperationScope scope)
    {
        if (Volatile.Read(ref disposed) != 0 ||
            !ReferenceEquals(interceptor.Session, scope.Session))
        {
            return false;
        }
        return game.Room.Capture(room =>
        {
            WiredSnapshot state = wired.Snapshot;
            return ReferenceEquals(interceptor.Session, scope.Session) &&
                room.IsInRoom &&
                room.Generation == scope.RoomGeneration &&
                state.Generation == scope.WiredGeneration;
        });
    }

    private void DispatchInRoom<T>(
        MessageContract<T> message_contract,
        T message,
        WiredOperationScope scope,
        CancellationToken cancellation_token)
        where T : IParserComposer<T>
    {
        CaptureCurrentState(scope, cancellation_token);
        messages.Dispatch(
            message_contract,
            message,
            scope.Session,
            cancellation_token,
            () => CaptureCurrentState(scope, cancellation_token));
        CaptureCurrentState(scope, cancellation_token);
    }

    private static void ValidateRequest(WiredTimeoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidateChestRequest(WiredChestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateId(request.ChestId, nameof(request.ChestId));
    }

    private static void ValidateContractOpenRequest(WiredContractOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ContractId);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidatePermanentVariableRequest(
        WiredPermanentVariableSetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEntity(request.EntityType, request.EntityId);
        ValidateText(request.VariableId, nameof(request.VariableId), false);
        ValidateVariableOperation(request.Operation);
        ValidateTimeout(request.TimeoutMilliseconds);
    }

    private static void ValidateVariableCache(IReadOnlyList<VariableHashEntry> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (VariableHashEntry entry in cache)
        {
            ValidateText(entry.VariableId, nameof(cache), false);
            if (!ids.Add(entry.VariableId))
                throw new ArgumentException("Wired variable cache identifiers must be unique.", nameof(cache));
        }
    }

    private static void ValidateVariableTarget(WiredTarget target, int object_id)
    {
        if (target is not WiredTarget.Furni and not WiredTarget.User and not WiredTarget.Global)
            throw new ArgumentOutOfRangeException(nameof(target));
        if (target is WiredTarget.Global)
        {
            if (object_id != 0)
                throw new ArgumentOutOfRangeException(nameof(object_id));
            return;
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(object_id);
    }

    private static bool MatchesObject(
        WiredObjectInspectionData value,
        int target,
        int object_id) =>
        value.Type == target && target switch
        {
            WiredVariableTarget.Furni => value.ObjectId == object_id,
            WiredVariableTarget.User => value.UserIndex == object_id,
            WiredVariableTarget.Global => object_id == 0,
            _ => false
        };

    private static void ValidateEntity(int entity_type, int entity_id)
    {
        if (entity_type == 0)
            throw new ArgumentOutOfRangeException(nameof(entity_type));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entity_id);
    }

    private static void ValidateVariableOperation(int operation)
    {
        if (operation is < WiredVariableOperation.Write or > WiredVariableOperation.Delete)
            throw new ArgumentOutOfRangeException(nameof(operation));
    }

    private static Id[] ValidateInventoryIds(IReadOnlyList<Id> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(values));
        var unique = new HashSet<Id>();
        var result = new Id[values.Count];
        int index = 0;
        foreach (Id value in values)
        {
            long wire_id = value;
            if (wire_id == 0 || wire_id is < int.MinValue or > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(values));
            if (!unique.Add(value))
                throw new ArgumentException("Inventory identifiers must be unique.", nameof(values));
            result[index++] = value;
        }
        return result;
    }

    private static void ValidatePage(int page, int page_size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(page_size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(page_size, 250);
    }

    private static void ValidateTimeout(int timeout_milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout_milliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout_milliseconds, 120000);
    }

    private static void ValidateId(Id value, string argument_name)
    {
        if ((long)value <= 0)
            throw new ArgumentOutOfRangeException(argument_name);
    }

    private static void ValidateText(string value, string argument_name, bool allow_empty)
    {
        ArgumentNullException.ThrowIfNull(value, argument_name);
        if (!allow_empty && value.Length == 0)
            throw new ArgumentException("The value cannot be empty.", argument_name);
        if (Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("The UTF-8 value exceeds the wire limit.", argument_name);
    }

    private int RemainingMilliseconds(long started, int timeout_milliseconds)
    {
        double remaining = timeout_milliseconds - time_provider.GetElapsedTime(started).TotalMilliseconds;
        if (remaining < 1)
            throw new RequestTimeoutException("wired operation", "wired response", timeout_milliseconds);
        return Math.Max(1, (int)Math.Ceiling(remaining));
    }

    private void DisposeEvents()
    {
        changed.Dispose();
        permissions_changed.Dispose();
        environment_changed.Dispose();
        click_settings_changed.Dispose();
        room_settings_changed.Dispose();
        configuration_opened.Dispose();
        configuration_received.Dispose();
        configuration_save_result.Dispose();
        menu_error.Dispose();
        reward_result.Dispose();
        chest_opened.Dispose();
        chest_coins_received.Dispose();
        chest_items_chunk_received.Dispose();
        chest_items_updated.Dispose();
        chest_upgrade_result.Dispose();
        chest_preferences_updated.Dispose();
        transaction_succeeded.Dispose();
        transaction_failed.Dispose();
        contract_contents_received.Dispose();
        contract_opened.Dispose();
        contract_update_result.Dispose();
        trade_initiated.Dispose();
        trade_items_updated.Dispose();
        trade_cancelled.Dispose();
        trade_completed.Dispose();
        trade_notification.Dispose();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private sealed class WiredUpdateQueue : IDisposable
    {
        private readonly WiredManager manager;
        private readonly Channel<WiredStateUpdate> channel = Channel.CreateUnbounded<WiredStateUpdate>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        private readonly Channel<WiredStateUpdate> chest_updates = Channel.CreateUnbounded<WiredStateUpdate>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
        private readonly Queue<WiredStateUpdate> deferred = new();
        private int disposed;

        public long Generation { get; }
        public long Revision { get; }

        public WiredUpdateQueue(WiredManager manager, long expected_generation)
        {
            this.manager = manager;
            manager.StateChanged += OnStateChanged;
            WiredSnapshot state = manager.Snapshot;
            if (state.Generation != expected_generation)
            {
                manager.StateChanged -= OnStateChanged;
                channel.Writer.TryComplete();
                chest_updates.Writer.TryComplete();
                throw new RequestDisconnectedException("wired operation", "wired response");
            }
            Generation = expected_generation;
            Revision = state.Revision;
        }

        public async ValueTask<WiredStateUpdate> WaitChestItemsUpdatedAsync(
            Id chest_id,
            IInterceptor interceptor,
            Session session,
            TimeProvider time_provider,
            long started,
            int timeout_milliseconds,
            long minimum_revision,
            string outgoing_name,
            string incoming_name,
            CancellationToken cancellation_token)
        {
            while (true)
            {
                cancellation_token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(interceptor.Session, session))
                    throw new RequestDisconnectedException(outgoing_name, incoming_name);
                double remaining = timeout_milliseconds -
                    time_provider.GetElapsedTime(started).TotalMilliseconds;
                if (remaining < 1)
                    throw new RequestTimeoutException(outgoing_name, incoming_name, timeout_milliseconds);
                using var timeout = new CancellationTokenSource(
                    Math.Max(1, (int)Math.Ceiling(remaining)));
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellation_token,
                    timeout.Token);
                WiredStateUpdate update;
                try
                {
                    update = await chest_updates.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellation_token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    throw new RequestTimeoutException(outgoing_name, incoming_name, timeout_milliseconds);
                }
                if (update.Kind is WiredStateChangeKind.Reset ||
                    update.State.Generation != Generation)
                {
                    throw new RequestDisconnectedException(outgoing_name, incoming_name);
                }
                if (update.State.Revision <= minimum_revision)
                    continue;
                if (update.Value is WiredChestItemsUpdatedSnapshot contents &&
                    (Id)contents.ChestId == chest_id)
                {
                    return update;
                }
            }
        }

        public bool TryTakeChestItemsUpdated(
            Id chest_id,
            long minimum_revision,
            [NotNullWhen(true)]
            out WiredStateUpdate? result)
        {
            while (chest_updates.Reader.TryRead(out WiredStateUpdate? update))
            {
                if (update.Kind is WiredStateChangeKind.Reset ||
                    update.State.Generation != Generation)
                {
                    throw new RequestDisconnectedException("wired chest deposit", "wired chest contents");
                }
                if (update.State.Revision <= minimum_revision)
                    continue;
                if (update.Value is WiredChestItemsUpdatedSnapshot contents &&
                    (Id)contents.ChestId == chest_id)
                {
                    result = update;
                    return true;
                }
            }
            result = null;
            return false;
        }

        public async ValueTask<WiredStateUpdate> WaitAsync(
            Func<WiredStateUpdate, bool> predicate,
            IInterceptor interceptor,
            Session session,
            TimeProvider time_provider,
            long started,
            int timeout_milliseconds,
            long minimum_revision,
            string outgoing_name,
            string incoming_name,
            CancellationToken cancellation_token)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            while (true)
            {
                cancellation_token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(interceptor.Session, session))
                    throw new RequestDisconnectedException(outgoing_name, incoming_name);
                if (TryTake(predicate, minimum_revision, out WiredStateUpdate? buffered))
                    return buffered;
                double remaining = timeout_milliseconds -
                    time_provider.GetElapsedTime(started).TotalMilliseconds;
                if (remaining < 1)
                    throw new RequestTimeoutException(outgoing_name, incoming_name, timeout_milliseconds);
                using var timeout = new CancellationTokenSource(
                    Math.Max(1, (int)Math.Ceiling(remaining)));
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellation_token,
                    timeout.Token);
                WiredStateUpdate update;
                try
                {
                    update = await channel.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellation_token);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    throw new RequestTimeoutException(outgoing_name, incoming_name, timeout_milliseconds);
                }
                if (update.Kind is WiredStateChangeKind.Reset)
                    throw new RequestDisconnectedException(outgoing_name, incoming_name);
                if (update.State.Generation != Generation)
                    throw new RequestDisconnectedException(outgoing_name, incoming_name);
                if (update.State.Revision <= minimum_revision)
                    continue;
                if (predicate(update))
                    return update;
                deferred.Enqueue(update);
            }
        }

        public bool TryTake(
            Func<WiredStateUpdate, bool> predicate,
            long minimum_revision,
            [NotNullWhen(true)]
            out WiredStateUpdate? result)
        {
            while (channel.Reader.TryRead(out WiredStateUpdate? update))
                deferred.Enqueue(update);
            int count = deferred.Count;
            for (int index = 0; index < count; index++)
            {
                WiredStateUpdate update = deferred.Dequeue();
                if (update.State.Generation != Generation ||
                    update.State.Revision <= minimum_revision)
                {
                    continue;
                }
                if (predicate(update))
                {
                    result = update;
                    return true;
                }
                deferred.Enqueue(update);
            }
            result = null;
            return false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            manager.StateChanged -= OnStateChanged;
            channel.Writer.TryComplete();
            chest_updates.Writer.TryComplete();
            deferred.Clear();
        }

        private void OnStateChanged(WiredStateUpdate update)
        {
            channel.Writer.TryWrite(update);
            if (update.Kind is WiredStateChangeKind.ChestItemsUpdated or WiredStateChangeKind.Reset)
                chest_updates.Writer.TryWrite(update);
        }
    }

    private readonly record struct WiredOperationScope(
        Session Session,
        long RoomGeneration,
        long WiredGeneration);

    private static ApplicationEventBinding<TEvent> Event<TEvent>(
        ApplicationDescriptor descriptor,
        ApplicationEventSource<TEvent> source) =>
        new(descriptor, source.Subscribe);
}
