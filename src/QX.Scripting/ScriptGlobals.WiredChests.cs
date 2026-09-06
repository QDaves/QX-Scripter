using Qx.Model.Wired;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Scripting;

/// <content>
/// Wired chests, transaction logs, contracts and wired trades. These are part of the same wired
/// room-events feature set as the wired menu and share its constraints.
/// <para>
/// <b>Wired trades are not user-to-user trades.</b> The messages below drive contract-driven
/// trades run by wired; the ordinary player-to-player trading window has its own separate API.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Raised when a chest reports its coin balance, both on opening and on later updates. The
    /// update flag distinguishes the initial dump from an incremental change.
    /// </summary>
    /// <param name="handler">Receives the chest id, the coin count and the update flag.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    public IDisposable OnChestCoins(Action<CoinsChestContents> handler) =>
        wired_event(ApplicationMemberIds.WiredChestCoinsReceived, handler);

    /// <summary>
    /// Raised for each fragment of a chest's item contents. A full chest arrives across several
    /// fragments; use the fragment number and total to know when the dump is complete.
    /// </summary>
    /// <param name="handler">Receives one fragment.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestItems(Action<WiredChestItemsChunkSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredChestItemsChunkReceived, handler);

    /// <summary>
    /// Raised when a chest's contents change incrementally after a deposit or a withdrawal,
    /// carrying the removed inventory ids and the added storage rows rather than a full dump.
    /// </summary>
    /// <param name="handler">Receives the delta.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestItemsUpdated(Action<WiredChestItemsUpdatedSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredChestItemsUpdated, handler);

    /// <summary>Raised when the server resolves a chest capacity upgrade.</summary>
    /// <param name="handler">
    /// Receives the chest id and the result code, where 0 means success.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestUpgradeResult(Action<UpgradeChestResult> handler) =>
        wired_event(ApplicationMemberIds.WiredChestUpgradeResult, handler);

    /// <summary>
    /// Raised when the server acknowledges a chest preference change. The carried flag says which
    /// of the two preference messages it answers: true for the notification preferences, false for
    /// the general chest preferences.
    /// </summary>
    /// <param name="handler">Receives the chest id and the notification-preferences flag.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestPreferencesSaved(Action<ChestPreferencesUpdateSuccess> handler) =>
        wired_event(ApplicationMemberIds.WiredChestPreferencesUpdated, handler);

    /// <summary>
    /// Raised when the server confirms a chest was opened. The coin balance and the item fragments
    /// follow immediately after.
    /// </summary>
    /// <param name="handler">Receives the chest id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestOpen(Action<OpenChest> handler) =>
        wired_event(ApplicationMemberIds.WiredChestOpened, handler);

    /// <summary>
    /// Opens a chest and asks for its contents. Returns immediately; the server answers with the
    /// open confirmation, then the coin balance, then the item fragments.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    public void OpenChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestOpen,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Closes a chest that was opened. Returns immediately; the server sends no acknowledgement.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    public void CloseChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestClose,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Locks or unlocks chests in bulk. Returns immediately; the server sends no acknowledgement.
    /// </summary>
    /// <param name="locked">True to lock, false to unlock.</param>
    /// <param name="applyToAllInRoom">
    /// False applies to the nearby or selected chests only; true applies to every chest in the
    /// room, which the game client guards behind a confirmation dialog.
    /// </param>
    public void LockChests(bool locked, bool applyToAllInRoom = false) =>
        wired_send(
            ApplicationMemberIds.WiredChestsLock,
            new WiredChestsLockRequest(locked, applyToAllInRoom));

    /// <summary>
    /// Buys extra capacity for a chest. Returns immediately; the outcome arrives as an upgrade
    /// result.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="upgradeAmount">
    /// How many capacity steps to buy. The game client sends the selected dropdown index plus one,
    /// so the smallest upgrade is 1.
    /// </param>
    public void UpgradeChest(int chestId, int upgradeAmount) =>
        wired_send(
            ApplicationMemberIds.WiredChestUpgrade,
            new WiredChestUpgradeRequest(chestId, upgradeAmount));

    /// <summary>
    /// Takes everything out of a chest at once. Returns immediately; the change arrives as a
    /// contents update.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    public void WithdrawAllFromChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawAll,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Takes coins out of a chest. Returns immediately; the new balance arrives as a coin-contents
    /// message with the update flag set.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="coinAmount">How many coins to withdraw.</param>
    public void WithdrawCoinsFromChest(Id chestId, int coinAmount) =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawCoins,
            new WiredChestCoinsWithdrawRequest(chestId, coinAmount));

    /// <summary>
    /// Takes items of one furni type out of a chest. Returns immediately; the change arrives as a
    /// contents update.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="isWallItem">Whether the furni type is a wall item rather than a floor item.</param>
    /// <param name="typeId">The furni type id, shared by every copy of that furni.</param>
    /// <param name="count">How many copies to withdraw.</param>
    /// <param name="legacyPosterId">
    /// The poster variant discriminator, needed only for legacy poster wall items. Empty otherwise.
    /// </param>
    public void WithdrawItemsFromChest(Id chestId, bool isWallItem, int typeId, int count, string legacyPosterId = "") =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawItems,
            new WiredChestItemsWithdrawRequest(
                chestId,
                new ChestItemType(isWallItem, typeId, legacyPosterId),
                count));

    /// <summary>
    /// Puts the client into "adding to this chest" mode, which is what the game client sends
    /// before items are dropped in. Returns immediately; deposits then arrive as contents updates.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    public void StartAddingToChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestAddStart,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Sets a chest's lock and capacity options. All three values travel together, so pass the
    /// current value for anything that should stay as it is. Returns immediately.
    /// </summary>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="locked">Whether the chest is locked.</param>
    /// <param name="autoLock">Whether the chest re-locks itself.</param>
    /// <param name="capacity">The chest's item capacity.</param>
    public void SetChestOptions(Id chestId, bool locked, bool autoLock, int capacity) =>
        wired_send(
            ApplicationMemberIds.WiredChestOptionsSet,
            new WiredChestOptionsSetRequest(
                new SetChestOptions(chestId, locked, autoLock, capacity)));

    /// <summary>
    /// Stores a chest's general preferences: name, description, two display flags, the chest state
    /// and, for furni chests, the open state and amount preview. Returns immediately; the server
    /// acknowledges with a preferences-saved message whose notification flag is false.
    /// </summary>
    /// <param name="preferences">The complete preference set — every field is sent together.</param>
    public void SetChestPreferences(SetChestPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredChestPreferencesSet,
            new WiredChestPreferencesSetRequest(preferences));

    /// <summary>
    /// Stores a chest's notification preferences: the notification mode plus the notify and event
    /// toggles. Returns immediately; the server acknowledges with a preferences-saved message whose
    /// notification flag is true.
    /// </summary>
    /// <param name="preferences">The complete preference set — every field is sent together.</param>
    public void SetChestNotificationPreferences(SetChestNotificationPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredChestNotificationPreferencesSet,
            new WiredChestNotificationPreferencesSetRequest(preferences));

    /// <summary>
    /// Raised when a wired transaction completes. The message carries a success type and, for
    /// reward transactions, the reward contents and text.
    /// </summary>
    /// <param name="handler">Receives the success notification.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTransactionSuccess(Action<WiredTransactionSuccess> handler) =>
        wired_event(ApplicationMemberIds.WiredTransactionSucceeded, handler);

    /// <summary>Asks for a page of one chest's transaction log.</summary>
    /// <param name="logListId">
    /// Which log to read. For a chest log this is the chest's id, echoed back as the page's log
    /// list id.
    /// </param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">How many entries per page; the game client uses 50.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// One page of transactions. The room and chest logs share a reply message, distinguished by
    /// its log list type: 0 for a chest log, 1 for a room log.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredTransactionLogList> GetChestTransactionLogs(int logListId, int page = 1, int pageSize = 50, int timeoutMs = 10000) =>
        wired_call<WiredTransactionChestLogsRequest, WiredTransactionLogList>(
            ApplicationMemberIds.WiredTransactionChestLogsGet,
            new WiredTransactionChestLogsRequest(
                logListId,
                page,
                pageSize,
                timeoutMs));

    /// <summary>Asks for a page of the whole room's transaction log.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">How many entries per page; the game client uses 50.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>One page of transactions, with a log list type of 1.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredTransactionLogList> GetRoomTransactionLogs(int page = 1, int pageSize = 50, int timeoutMs = 10000) =>
        wired_call<WiredTransactionRoomLogsRequest, WiredTransactionLogList>(
            ApplicationMemberIds.WiredTransactionRoomLogsGet,
            new WiredTransactionRoomLogsRequest(page, pageSize, timeoutMs));

    /// <summary>Asks for the full detail of one transaction from a log page.</summary>
    /// <param name="transactionId">
    /// The transaction id from a log entry. It is a 64-bit value on the wire, so do not truncate it.
    /// </param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>The transaction's details, including the deposited and withdrawn furni counts.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredTransactionLogDetails> GetTransactionDetails(long transactionId, int timeoutMs = 10000) =>
        wired_call<WiredTransactionDetailsRequest, WiredTransactionLogDetails>(
            ApplicationMemberIds.WiredTransactionDetailsGet,
            new WiredTransactionDetailsRequest(transactionId, timeoutMs));

    /// <summary>
    /// Raised when the server sends a contract's contents. The shape depends on the contract type:
    /// 0 payment, 1 trade, 2 reward — only a payment carries the payment mode, receive text and
    /// layout, and only a reward carries the reward category, dialog flag and reward text.
    /// </summary>
    /// <param name="handler">Receives the contract.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnContractContents(Action<WiredContractContents> handler) =>
        wired_event(ApplicationMemberIds.WiredContractContentsReceived, handler);

    /// <summary>
    /// Raised when the server asks the client to open a contract editor, carrying only the
    /// contract id.
    /// </summary>
    /// <param name="handler">Receives the contract id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnOpenContract(Action<WiredOpenContract> handler) =>
        wired_event(ApplicationMemberIds.WiredContractOpened, handler);

    /// <summary>
    /// Saves a contract and waits for the server's verdict. The whole contract is replaced, so
    /// start from the contents the server sent rather than building a partial update.
    /// </summary>
    /// <param name="contract">The complete new contract, including its type-specific fields.</param>
    /// <param name="timeoutMs">How long to wait for the reply, in milliseconds.</param>
    /// <returns>
    /// The result: the contract id, whether it was accepted, and a failure code string when it
    /// was not.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">No reply arrived in time.</exception>
    public Task<WiredContractUpdateResult> UpdateContract(WiredContractContents contract, int timeoutMs = 10000) =>
        wired_call<WiredContractUpdateRequest, WiredContractUpdateResult>(
            ApplicationMemberIds.WiredContractUpdate,
            new WiredContractUpdateRequest(contract, timeoutMs));

    /// <summary>
    /// Raised when wired starts a trade with the local user, carrying what is being asked for and
    /// offered, whether the requirements dialog should open at once, whether it replaces a trade
    /// already in progress, and the timeout in seconds.
    /// </summary>
    /// <param name="handler">Receives the trade offer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeInitiate(Action<WiredTradeInitiate> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeInitiated, handler);

    /// <summary>
    /// Raised whenever the items on either side of a wired trade change, carrying both sides'
    /// contents and whether the trade may currently be confirmed.
    /// </summary>
    /// <param name="handler">Receives the trade contents.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeItems(Action<WiredTradingItemsSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeItemsUpdated, handler);

    /// <summary>
    /// Raised when a wired trade is cancelled, carrying only a failure type code that says why.
    /// </summary>
    /// <param name="handler">Receives the failure type code.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeCancelled(Action<WiredTradeCancelled> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeCancelled, handler);

    /// <summary>
    /// Raised when a wired trade completes. The message has no payload — it is a pure signal.
    /// </summary>
    /// <param name="handler">Receives the empty completion message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeCompleted(Action<WiredTradeCompleted> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeCompleted, handler);

    /// <summary>
    /// Adds inventory items to the open wired trade, taking 32-bit ids. Returns immediately; the
    /// new contents arrive as a trade items update.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to offer.</param>
    public void WiredTradeAddItems(IReadOnlyList<int> inventory_ids) =>
        WiredTradeAddItems(inventory_ids.Select(value => (Id)(long)value).ToArray());

    /// <summary>
    /// Adds inventory items to the open wired trade, taking 64-bit ids. Returns immediately.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to offer.</param>
    public void WiredTradeAddItems(IReadOnlyList<long> inventory_ids) =>
        WiredTradeAddItems(inventory_ids.Select(value => (Id)value).ToArray());

    /// <summary>
    /// Adds inventory items to the open wired trade. Returns immediately; the new contents arrive
    /// as a trade items update.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to offer.</param>
    public void WiredTradeAddItems(IReadOnlyList<Id> inventory_ids) =>
        wired_send(
            ApplicationMemberIds.WiredTradeItemsAdd,
            new WiredTradeItemsRequest(inventory_ids));

    /// <summary>
    /// Takes inventory items back off the open wired trade, taking 32-bit ids. Returns immediately.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to withdraw from the offer.</param>
    public void WiredTradeRemoveItems(IReadOnlyList<int> inventory_ids) =>
        WiredTradeRemoveItems(inventory_ids.Select(value => (Id)(long)value).ToArray());

    /// <summary>
    /// Takes inventory items back off the open wired trade, taking 64-bit ids. Returns immediately.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to withdraw from the offer.</param>
    public void WiredTradeRemoveItems(IReadOnlyList<long> inventory_ids) =>
        WiredTradeRemoveItems(inventory_ids.Select(value => (Id)value).ToArray());

    /// <summary>
    /// Takes inventory items back off the open wired trade. The add and remove paths share one
    /// wire message, distinguished by a leading flag. Returns immediately.
    /// </summary>
    /// <param name="inventory_ids">The inventory item ids to withdraw from the offer.</param>
    public void WiredTradeRemoveItems(IReadOnlyList<Id> inventory_ids) =>
        wired_send(
            ApplicationMemberIds.WiredTradeItemsRemove,
            new WiredTradeItemsRequest(inventory_ids));

    /// <summary>
    /// The wired chests standing in the room.
    /// </summary>
    /// <remarks>
    /// Recognised by furni class: the hotel's chests are the <c>wf_storage</c> family. Whether one
    /// takes coins or furni is read from its class name too, because nothing in the room data says
    /// so — only opening a chest reveals which contents message it answers with.
    /// </remarks>
    /// <param name="coins">
    /// <see langword="null"/> for every chest, <see langword="true"/> for coin chests only,
    /// <see langword="false"/> for furni chests only.
    /// </param>
    public IReadOnlyList<FloorItem> Chests(bool? coins = null) =>
    [
        .. Room.FloorItems
            .Where(item =>
            {
                string identifier = item.Identifier ?? "";
                if (!identifier.StartsWith("wf_storage", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (coins is not { } wanted)
                    return true;
                return identifier.Contains("coin", StringComparison.OrdinalIgnoreCase) == wanted;
            })
            .OrderBy(item => (long)item.Id)
    ];

    /// <summary>
    /// The first wired chest in the room, or <see langword="null"/> when there is none.
    /// </summary>
    /// <param name="coins">
    /// <see langword="null"/> for any chest, <see langword="true"/> for a coin chest,
    /// <see langword="false"/> for a furni chest.
    /// </param>
    public FloorItem? FirstChest(bool? coins = null) => Chests(coins).FirstOrDefault();

    /// <summary>
    /// Puts inventory items into a wired chest and waits for the hotel to confirm.
    /// </summary>
    /// <remarks>
    /// A deposit is a trade with the chest rather than a single message, which is why the pieces
    /// are named after trades: the chest is opened for adding, the items are offered, and the offer
    /// is confirmed. This drives all three and reports what actually landed, so a script does not
    /// have to sequence them or guess when each step is done.
    /// </remarks>
    /// <param name="chestId">The chest's room item id.</param>
    /// <param name="inventoryIds">The inventory item ids to put in.</param>
    /// <param name="timeoutMs">How long to wait for the deposit to finish, in milliseconds.</param>
    /// <returns>Whether it went through, and what the chest reported taking.</returns>
    /// <exception cref="ArgumentException">No items were named.</exception>
    public async Task<ChestDeposit> DepositToChest(
        Id chestId,
        IEnumerable<Id> inventoryIds,
        int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(inventoryIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        Id[] items = [.. inventoryIds];
        if (items.Length == 0)
            throw new ArgumentException("Name at least one inventory item to deposit.", nameof(inventoryIds));
        WiredChestDepositResult result =
            await wired_call<WiredChestDepositRequest, WiredChestDepositResult>(
                ApplicationMemberIds.WiredChestDeposit,
                new WiredChestDepositRequest(chestId, items, timeoutMs));
        return new ChestDeposit(
            result.Success,
            result.Failure,
            result.Requested,
            result.Stored)
        {
            Accepted = result.Accepted,
            Generation = result.Generation,
            Revision = result.Revision
        };
    }

    /// <summary>
    /// Puts inventory items into a wired chest and waits for the hotel to confirm.
    /// </summary>
    /// <remarks>
    /// Untradeable items are left out and listed in <see cref="ChestDeposit.Skipped"/>: a chest
    /// will not take them, and offering one drags the whole exchange down with it.
    /// </remarks>
    /// <param name="chest">The chest standing in the room.</param>
    /// <param name="items">The inventory items to put in.</param>
    /// <param name="timeoutMs">How long to wait for the deposit to finish, in milliseconds.</param>
    public async Task<ChestDeposit> DepositToChest(
        FloorItem chest,
        IEnumerable<InventoryItem> items,
        int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(chest);
        ArgumentNullException.ThrowIfNull(items);

        InventoryItem[] all = [.. items];
        InventoryItem[] takeable = [.. all.Where(item => item.IsTradeable)];
        Id[] skipped = [.. all.Where(item => !item.IsTradeable).Select(item => item.ItemId)];

        if (takeable.Length == 0)
        {
            return new ChestDeposit(
                false,
                all.Length == 0
                    ? "No items were named."
                    : "Every item named is untradeable, which a chest will not take.",
                all.Length,
                []) { Skipped = skipped };
        }

        ChestDeposit result = await DepositToChest(chest.Id, takeable.Select(item => item.ItemId), timeoutMs);
        return result with { Requested = all.Length, Skipped = skipped };
    }

    /// <summary>
    /// Confirms or un-confirms the open wired trade. Returns immediately; the outcome arrives as a
    /// trade completion or cancellation.
    /// </summary>
    /// <param name="confirm">True to confirm, false to withdraw a previous confirmation.</param>
    public void WiredTradeConfirm(bool confirm = true) =>
        wired_send(
            ApplicationMemberIds.WiredTradeConfirm,
            new WiredTradeConfirmRequest(confirm));

    /// <summary>
    /// Cancels the open wired trade. Returns immediately; the server answers with a trade
    /// cancellation.
    /// </summary>
    public void WiredTradeCancel() =>
        wired_send(
            ApplicationMemberIds.WiredTradeCancel,
            new WiredCommandRequest());

    /// <summary>
    /// How much a wired chest in the room can hold, and how far it can still be upgraded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// None of this is on the wire. The client derives it from two places the hotel never sends
    /// together: the chest furni's own stuff data, which carries <c>capacity_level</c>, and the
    /// external variables, which carry the sizes and the ceiling. That is why this needs the room
    /// item rather than a chest identifier.
    /// </para>
    /// <para>
    /// A starter chest is the exception: it holds a flat <c>starter_capacity</c> and cannot be
    /// upgraded at all, so the level plays no part.
    /// </para>
    /// </remarks>
    /// <param name="chest">The chest furni, taken from the room's floor items.</param>
    /// <param name="coins">
    /// <see langword="true"/> for a coin chest, <see langword="false"/> for a furni chest. The two
    /// are configured separately.
    /// </param>
    public WiredChestCapacity ChestCapacityOf(FloorItem chest, bool coins)
    {
        ArgumentNullException.ThrowIfNull(chest);

        string prefix = coins ? "wired.coins_chest." : "wired.furni_chest.";
        bool starter = IsStarterWiredChest(FurniClassOf(chest));
        int level = ChestCapacityLevel(chest);
        int maxUpgrades = ConfigNumber(prefix + "max_upgrades");

        int capacity = starter
            ? ConfigNumber(prefix + "starter_capacity")
            : ConfigNumber(prefix + "initial_capacity") + ConfigNumber(prefix + "upgrade_capacity") * level;

        int remaining = starter ? 0 : Math.Max(0, maxUpgrades - level);
        (int credits, int diamonds) = WiredChestUpgradeCost(1);

        return new WiredChestCapacity(
            capacity,
            level,
            maxUpgrades,
            remaining,
            starter,
            ConfigNumber(prefix + "upgrade_capacity"),
            credits,
            diamonds);
    }

    /// <summary>
    /// How many capacity upgrades a chest furni has had, read from its stuff data.
    /// </summary>
    /// <remarks>
    /// Returns zero for a furni that is not a chest, or one whose stuff data has not arrived: the
    /// client casts the same missing value to zero rather than treating it as unknown.
    /// </remarks>
    /// <param name="chest">The chest furni.</param>
    public int ChestCapacityLevel(FloorItem chest)
    {
        ArgumentNullException.ThrowIfNull(chest);
        if (chest.Data is not MapData map)
            return 0;
        return map.Entries.TryGetValue("capacity_level", out string? level) &&
            int.TryParse(level, out int parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// What it costs to buy several capacity upgrades for a chest at once.
    /// </summary>
    /// <remarks>
    /// The client offers one upgrade, then every amount up to the ceiling the chest has left, so an
    /// amount beyond <see cref="WiredChestCapacity.UpgradesRemaining"/> is not something it can
    /// send. Diamonds are activity point type 5.
    /// </remarks>
    /// <param name="upgrades">How many upgrades to buy.</param>
    public (int Credits, int Diamonds) ChestUpgradeCostFor(int upgrades) =>
        WiredChestUpgradeCost(upgrades);

    private string FurniClassOf(FloorItem chest) =>
        FurniOf(chest)?.ClassName ?? "";
}

/// <summary>
/// What a wired chest holds and how much room it has left to grow, worked out from the chest furni
/// and the hotel configuration together.
/// </summary>
/// <param name="Capacity">How much the chest holds now.</param>
/// <param name="CapacityLevel">How many upgrades it has had.</param>
/// <param name="MaxUpgrades">The most upgrades the hotel allows on a chest of this kind.</param>
/// <param name="UpgradesRemaining">How many more it will accept; zero for a starter chest.</param>
/// <param name="IsStarterChest">Whether this is a starter chest, which cannot be upgraded.</param>
/// <param name="CapacityPerUpgrade">How much one upgrade adds.</param>
/// <param name="UpgradeCostCredits">What one upgrade costs in credits.</param>
/// <param name="UpgradeCostDiamonds">What one upgrade costs in diamonds.</param>
public sealed record WiredChestCapacity(
    int Capacity,
    int CapacityLevel,
    int MaxUpgrades,
    int UpgradesRemaining,
    bool IsStarterChest,
    int CapacityPerUpgrade,
    int UpgradeCostCredits,
    int UpgradeCostDiamonds)
{
    /// <summary>Whether the chest will accept at least one more capacity upgrade.</summary>
    public bool CanUpgrade => UpgradesRemaining > 0;
}

/// <summary>
/// What came of putting items into a wired chest.
/// </summary>
/// <param name="Success">Whether the exchange completed.</param>
/// <param name="Failure">Why it did not, empty when it did.</param>
/// <param name="Requested">How many items were offered.</param>
/// <param name="Stored">
/// What the chest reported taking. Can be shorter than <paramref name="Requested"/> when the chest
/// filled up or refused an item, and can be empty on a chest that reports its contents only when
/// reopened.
/// </param>
public sealed record ChestDeposit(
    bool Success,
    string Failure,
    int Requested,
    IReadOnlyList<WiredChestStorageSnapshot> Stored)
{
    /// <summary>How many items the chest had in the offer when it was confirmed.</summary>
    public int Accepted { get; init; }

    /// <summary>
    /// Items that were never offered because a chest will not take them.
    /// </summary>
    /// <remarks>
    /// Untradeable furni, which the hotel refuses rather than stores. This is read off the item
    /// rather than proven from the client, whose own filter lives in the inventory view that
    /// decides what is clickable, so it is reported here instead of dropped quietly.
    /// </remarks>
    public IReadOnlyList<Id> Skipped { get; init; } = [];

    public long Generation { get; init; }

    public long Revision { get; init; }

    /// <summary>Whether every offered item was reported as taken.</summary>
    public bool StoredEverything => Success && Stored.Count >= Requested;
}
