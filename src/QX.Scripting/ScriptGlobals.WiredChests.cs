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
    /// Registers a handler that runs when a chest reports its coin balance, both on opening and on
    /// later updates.
    /// </summary>
    /// <remarks>
    /// The update flag distinguishes the initial balance from an incremental change.
    /// </remarks>
    /// <param name="handler">The handler to call with the chest id, the coin count and the update flag.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestCoins(Action<CoinsChestContents> handler) =>
        wired_event(ApplicationMemberIds.WiredChestCoinsReceived, handler);

    /// <summary>
    /// Registers a handler that runs for each fragment of a chest's item contents.
    /// </summary>
    /// <remarks>
    /// A full chest arrives across several fragments; use the fragment number and total to know
    /// when the contents are complete.
    /// </remarks>
    /// <param name="handler">The handler to call with one fragment.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestItems(Action<WiredChestItemsChunkSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredChestItemsChunkReceived, handler);

    /// <summary>
    /// Registers a handler that runs when a chest's contents change after a deposit or a withdrawal.
    /// </summary>
    /// <remarks>
    /// The update carries the removed inventory ids and the added storage rows rather than the full
    /// contents.
    /// </remarks>
    /// <param name="handler">The handler to call with the change.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestItemsUpdated(Action<WiredChestItemsUpdatedSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredChestItemsUpdated, handler);

    /// <summary>Registers a handler that runs when the server resolves a chest capacity upgrade.</summary>
    /// <param name="handler">
    /// The handler to call with the chest id and the result code, where 0 means success.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestUpgradeResult(Action<UpgradeChestResult> handler) =>
        wired_event(ApplicationMemberIds.WiredChestUpgradeResult, handler);

    /// <summary>
    /// Registers a handler that runs when the server acknowledges a chest preference change.
    /// </summary>
    /// <remarks>
    /// The carried flag says which of the two preference messages it answers:
    /// <see langword="true"/> for the notification preferences, <see langword="false"/> for the
    /// general chest preferences.
    /// </remarks>
    /// <param name="handler">The handler to call with the chest id and the notification preferences flag.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestPreferencesSaved(Action<ChestPreferencesUpdateSuccess> handler) =>
        wired_event(ApplicationMemberIds.WiredChestPreferencesUpdated, handler);

    /// <summary>
    /// Registers a handler that runs when the server confirms a chest was opened.
    /// </summary>
    /// <remarks>
    /// The coin balance and the item fragments follow immediately after.
    /// </remarks>
    /// <param name="handler">The handler to call with the chest id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnChestOpen(Action<OpenChest> handler) =>
        wired_event(ApplicationMemberIds.WiredChestOpened, handler);

    /// <summary>
    /// Opens a chest and asks for its contents without waiting for them.
    /// </summary>
    /// <remarks>
    /// The server answers with the open confirmation, then the coin balance, then the item
    /// fragments.
    /// </remarks>
    /// <param name="chestId">The chest's item id.</param>
    public void OpenChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestOpen,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Closes a chest that was opened.
    /// </summary>
    /// <remarks>The request is sent without waiting; the server sends no acknowledgement.</remarks>
    /// <param name="chestId">The chest's item id.</param>
    public void CloseChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestClose,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Locks or unlocks chests in bulk.
    /// </summary>
    /// <remarks>The request is sent without waiting; the server sends no acknowledgement.</remarks>
    /// <param name="locked"><see langword="true"/> to lock; <see langword="false"/> to unlock.</param>
    /// <param name="applyToAllInRoom">
    /// <see langword="true"/> to apply to every chest in the room, which the game client guards
    /// behind a confirmation dialog; <see langword="false"/> to apply only to the user's own
    /// chests in the room.
    /// </param>
    public void LockChests(bool locked, bool applyToAllInRoom = false) =>
        wired_send(
            ApplicationMemberIds.WiredChestsLock,
            new WiredChestsLockRequest(locked, applyToAllInRoom));

    /// <summary>
    /// Buys extra capacity for a chest.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the outcome arrives as an upgrade result, see
    /// <see cref="OnChestUpgradeResult(Action{UpgradeChestResult})"/>.
    /// </remarks>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="upgradeAmount">
    /// The number of capacity steps to buy. The game client sends the selected dropdown index plus
    /// one, so the smallest upgrade is 1.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="chestId"/> or <paramref name="upgradeAmount"/> is zero or negative.
    /// </exception>
    public void UpgradeChest(int chestId, int upgradeAmount) =>
        wired_send(
            ApplicationMemberIds.WiredChestUpgrade,
            new WiredChestUpgradeRequest(chestId, upgradeAmount));

    /// <summary>
    /// Takes everything out of a chest at once.
    /// </summary>
    /// <remarks>The request is sent without waiting; the change arrives as a contents update.</remarks>
    /// <param name="chestId">The chest's item id.</param>
    public void WithdrawAllFromChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawAll,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Takes coins out of a chest.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the new balance arrives as a coin contents message
    /// with the update flag set.
    /// </remarks>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="coinAmount">The number of coins to withdraw.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="coinAmount"/> is zero or negative.</exception>
    public void WithdrawCoinsFromChest(Id chestId, int coinAmount) =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawCoins,
            new WiredChestCoinsWithdrawRequest(chestId, coinAmount));

    /// <summary>
    /// Takes items of one furni type out of a chest.
    /// </summary>
    /// <remarks>The request is sent without waiting; the change arrives as a contents update.</remarks>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="isWallItem">
    /// <see langword="true"/> when the furni type is a wall item; <see langword="false"/> for a
    /// floor item.
    /// </param>
    /// <param name="typeId">The furni type id, shared by every copy of that furni.</param>
    /// <param name="count">The number of copies to withdraw.</param>
    /// <param name="legacyPosterId">
    /// The poster variant discriminator, needed only for legacy poster wall items and empty
    /// otherwise.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is zero or negative.</exception>
    public void WithdrawItemsFromChest(Id chestId, bool isWallItem, int typeId, int count, string legacyPosterId = "") =>
        wired_send(
            ApplicationMemberIds.WiredChestWithdrawItems,
            new WiredChestItemsWithdrawRequest(
                chestId,
                new ChestItemType(isWallItem, typeId, legacyPosterId),
                count));

    /// <summary>
    /// Starts adding items to a chest, which is what the game client sends before items are put in.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting. The server answers by starting a wired trade with the
    /// chest; offer items with <see cref="WiredTradeAddItems(IReadOnlyList{Id})"/> and confirm with
    /// <see cref="WiredTradeConfirm(bool)"/>. <see cref="DepositToChest(Id, IEnumerable{Id}, int)"/>
    /// runs the whole exchange.
    /// </remarks>
    /// <param name="chestId">The chest's item id.</param>
    public void StartAddingToChest(Id chestId) =>
        wired_send(
            ApplicationMemberIds.WiredChestAddStart,
            new WiredChestRequest(chestId));

    /// <summary>
    /// Sets a chest's lock and capacity options.
    /// </summary>
    /// <remarks>
    /// All three values travel together, so pass the current value for anything that should stay as
    /// it is. The request is sent without waiting.
    /// </remarks>
    /// <param name="chestId">The chest's item id.</param>
    /// <param name="locked"><see langword="true"/> to lock the chest; otherwise, <see langword="false"/>.</param>
    /// <param name="autoLock"><see langword="true"/> to have the chest lock itself again; otherwise, <see langword="false"/>.</param>
    /// <param name="capacity">The chest's item capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
    public void SetChestOptions(Id chestId, bool locked, bool autoLock, int capacity) =>
        wired_send(
            ApplicationMemberIds.WiredChestOptionsSet,
            new WiredChestOptionsSetRequest(
                new SetChestOptions(chestId, locked, autoLock, capacity)));

    /// <summary>
    /// Stores a chest's general preferences.
    /// </summary>
    /// <remarks>
    /// The preferences are the name, description, two display flags, the chest state and, for furni
    /// chests, the open state and amount preview. The request is sent without waiting; the server
    /// acknowledges with a preferences saved message whose notification flag is
    /// <see langword="false"/>.
    /// </remarks>
    /// <param name="preferences">The complete preference set, since every field is sent together.</param>
    public void SetChestPreferences(SetChestPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredChestPreferencesSet,
            new WiredChestPreferencesSetRequest(preferences));

    /// <summary>
    /// Stores a chest's notification preferences.
    /// </summary>
    /// <remarks>
    /// The preferences are the notification mode plus the notify and event toggles. The request is
    /// sent without waiting; the server acknowledges with a preferences saved message whose
    /// notification flag is <see langword="true"/>.
    /// </remarks>
    /// <param name="preferences">The complete preference set, since every field is sent together.</param>
    public void SetChestNotificationPreferences(SetChestNotificationPreferences preferences) =>
        wired_send(
            ApplicationMemberIds.WiredChestNotificationPreferencesSet,
            new WiredChestNotificationPreferencesSetRequest(preferences));

    /// <summary>
    /// Registers a handler that runs when a wired transaction completes.
    /// </summary>
    /// <remarks>
    /// The message carries a success type and, for reward transactions, the reward contents and
    /// text.
    /// </remarks>
    /// <param name="handler">The handler to call with the success notification.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnTransactionSuccess(Action<WiredTransactionSuccess> handler) =>
        wired_event(ApplicationMemberIds.WiredTransactionSucceeded, handler);

    /// <summary>Requests a page of one chest's transaction log.</summary>
    /// <param name="logListId">
    /// The log to read. For a chest log this is the chest's id, echoed back as the page's log list
    /// id.
    /// </param>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of entries per page, from 1 to 250; the game client uses 50.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// One page of transactions. The room and chest logs share a reply message, distinguished by
    /// its log list type: 0 for a chest log, 1 for a room log.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredTransactionLogList> GetChestTransactionLogs(int logListId, int page = 1, int pageSize = 50, int timeoutMs = 10000) =>
        wired_call<WiredTransactionChestLogsRequest, WiredTransactionLogList>(
            ApplicationMemberIds.WiredTransactionChestLogsGet,
            new WiredTransactionChestLogsRequest(
                logListId,
                page,
                pageSize,
                timeoutMs));

    /// <summary>Requests a page of the whole room's transaction log.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The number of entries per page, from 1 to 250; the game client uses 50.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>One page of transactions, with a log list type of 1.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredTransactionLogList> GetRoomTransactionLogs(int page = 1, int pageSize = 50, int timeoutMs = 10000) =>
        wired_call<WiredTransactionRoomLogsRequest, WiredTransactionLogList>(
            ApplicationMemberIds.WiredTransactionRoomLogsGet,
            new WiredTransactionRoomLogsRequest(page, pageSize, timeoutMs));

    /// <summary>Requests the full detail of one transaction from a log page.</summary>
    /// <param name="transactionId">
    /// The transaction id from a log entry. It is a 64-bit value on the wire, so do not truncate it.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The transaction's details, including the deposited and withdrawn furni counts.</returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredTransactionLogDetails> GetTransactionDetails(long transactionId, int timeoutMs = 10000) =>
        wired_call<WiredTransactionDetailsRequest, WiredTransactionLogDetails>(
            ApplicationMemberIds.WiredTransactionDetailsGet,
            new WiredTransactionDetailsRequest(transactionId, timeoutMs));

    /// <summary>
    /// Registers a handler that runs when the server sends a contract's contents.
    /// </summary>
    /// <remarks>
    /// The shape depends on the contract type, which is 0 for payment, 1 for trade and 2 for
    /// reward. Only a payment carries the payment mode, receive text and layout, and only a reward
    /// carries the reward category, dialog flag and reward text.
    /// </remarks>
    /// <param name="handler">The handler to call with the contract.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnContractContents(Action<WiredContractContents> handler) =>
        wired_event(ApplicationMemberIds.WiredContractContentsReceived, handler);

    /// <summary>
    /// Registers a handler that runs when the server asks the client to open a contract editor.
    /// </summary>
    /// <param name="handler">The handler to call with the message, which carries only the contract id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnOpenContract(Action<WiredOpenContract> handler) =>
        wired_event(ApplicationMemberIds.WiredContractOpened, handler);

    /// <summary>
    /// Saves a contract and waits for the server's verdict.
    /// </summary>
    /// <remarks>
    /// The whole contract is replaced, so start from the contents the server sent rather than
    /// building a partial update.
    /// </remarks>
    /// <param name="contract">The complete new contract, including its type-specific fields.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>
    /// The result: the contract id, whether it was accepted, and a failure code string when it
    /// was not.
    /// </returns>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when no reply arrived in time.</exception>
    public Task<WiredContractUpdateResult> UpdateContract(WiredContractContents contract, int timeoutMs = 10000) =>
        wired_call<WiredContractUpdateRequest, WiredContractUpdateResult>(
            ApplicationMemberIds.WiredContractUpdate,
            new WiredContractUpdateRequest(contract, timeoutMs));

    /// <summary>
    /// Registers a handler that runs when wired starts a trade with the local user.
    /// </summary>
    /// <remarks>
    /// The message carries what is being asked for and offered, whether the requirements dialog
    /// should open at once, whether it replaces a trade already in progress, and the timeout in
    /// seconds.
    /// </remarks>
    /// <param name="handler">The handler to call with the trade offer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeInitiate(Action<WiredTradeInitiate> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeInitiated, handler);

    /// <summary>
    /// Registers a handler that runs whenever the items on either side of a wired trade change.
    /// </summary>
    /// <remarks>
    /// The update carries both sides' contents and whether the trade may currently be confirmed.
    /// </remarks>
    /// <param name="handler">The handler to call with the trade contents.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeItems(Action<WiredTradingItemsSnapshot> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeItemsUpdated, handler);

    /// <summary>
    /// Registers a handler that runs when a wired trade is canceled.
    /// </summary>
    /// <param name="handler">The handler to call with the message, which carries only a failure type code.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeCancelled(Action<WiredTradeCancelled> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeCancelled, handler);

    /// <summary>
    /// Registers a handler that runs when a wired trade completes.
    /// </summary>
    /// <param name="handler">The handler to call with the completion message, which has no payload.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnWiredTradeCompleted(Action<WiredTradeCompleted> handler) =>
        wired_event(ApplicationMemberIds.WiredTradeCompleted, handler);

    /// <summary>
    /// Adds inventory items to the open wired trade, taking 32-bit ids.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the new contents arrive as a trade items update.
    /// </remarks>
    /// <param name="inventoryIds">The inventory item ids to offer, between 1 and 1000 unique non-zero ids.</param>
    public void WiredTradeAddItems(IReadOnlyList<int> inventoryIds) =>
        WiredTradeAddItems(inventoryIds.Select(value => (Id)(long)value).ToArray());

    /// <summary>
    /// Adds inventory items to the open wired trade, taking ids as <see cref="long"/> values.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the new contents arrive as a trade items update.
    /// </remarks>
    /// <param name="inventoryIds">The inventory item ids to offer, between 1 and 1000 unique non-zero 32-bit ids.</param>
    public void WiredTradeAddItems(IReadOnlyList<long> inventoryIds) =>
        WiredTradeAddItems(inventoryIds.Select(value => (Id)value).ToArray());

    /// <summary>
    /// Adds inventory items to the open wired trade.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the new contents arrive as a trade items update.
    /// </remarks>
    /// <param name="inventoryIds">The inventory item ids to offer, between 1 and 1000 unique non-zero 32-bit ids.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the list is empty, too long, or holds an id that is zero or does not fit 32 bits.</exception>
    /// <exception cref="ArgumentException">Thrown when the list holds the same id twice.</exception>
    public void WiredTradeAddItems(IReadOnlyList<Id> inventoryIds) =>
        wired_send(
            ApplicationMemberIds.WiredTradeItemsAdd,
            new WiredTradeItemsRequest(inventoryIds));

    /// <summary>
    /// Takes inventory items back off the open wired trade, taking 32-bit ids.
    /// </summary>
    /// <remarks>The request is sent without waiting.</remarks>
    /// <param name="inventoryIds">The inventory item ids to withdraw from the offer, between 1 and 1000 unique non-zero ids.</param>
    public void WiredTradeRemoveItems(IReadOnlyList<int> inventoryIds) =>
        WiredTradeRemoveItems(inventoryIds.Select(value => (Id)(long)value).ToArray());

    /// <summary>
    /// Takes inventory items back off the open wired trade, taking ids as <see cref="long"/> values.
    /// </summary>
    /// <remarks>The request is sent without waiting.</remarks>
    /// <param name="inventoryIds">The inventory item ids to withdraw from the offer, between 1 and 1000 unique non-zero 32-bit ids.</param>
    public void WiredTradeRemoveItems(IReadOnlyList<long> inventoryIds) =>
        WiredTradeRemoveItems(inventoryIds.Select(value => (Id)value).ToArray());

    /// <summary>
    /// Takes inventory items back off the open wired trade.
    /// </summary>
    /// <remarks>
    /// The add and remove paths share one wire message, distinguished by a leading flag. The
    /// request is sent without waiting.
    /// </remarks>
    /// <param name="inventoryIds">The inventory item ids to withdraw from the offer, between 1 and 1000 unique non-zero 32-bit ids.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the list is empty, too long, or holds an id that is zero or does not fit 32 bits.</exception>
    /// <exception cref="ArgumentException">Thrown when the list holds the same id twice.</exception>
    public void WiredTradeRemoveItems(IReadOnlyList<Id> inventoryIds) =>
        wired_send(
            ApplicationMemberIds.WiredTradeItemsRemove,
            new WiredTradeItemsRequest(inventoryIds));

    /// <summary>
    /// Gets the wired chests standing in the room.
    /// </summary>
    /// <remarks>
    /// Chests are recognized by furni class: the hotel's chests are the <c>wf_storage</c> family,
    /// matched case insensitively. Whether one takes coins or furni is read from its class name too
    /// (a coin chest contains <c>coin</c>), because nothing in the room data says so. Only opening
    /// a chest reveals which contents message it answers with.
    /// </remarks>
    /// <param name="coins">
    /// <see langword="null"/> for every chest, <see langword="true"/> for coin chests only,
    /// <see langword="false"/> for furni chests only.
    /// </param>
    /// <returns>The matching chests, ordered by item id.</returns>
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
    /// Gets the first wired chest in the room, in item id order, or <see langword="null"/> when
    /// there is none.
    /// </summary>
    /// <param name="coins">
    /// <see langword="null"/> for any chest, <see langword="true"/> for a coin chest,
    /// <see langword="false"/> for a furni chest.
    /// </param>
    /// <returns>The chest, or <see langword="null"/> when the room has no matching chest.</returns>
    public FloorItem? FirstChest(bool? coins = null) => Chests(coins).FirstOrDefault();

    /// <summary>
    /// Puts inventory items into a wired chest and waits for the hotel to confirm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A deposit is a trade with the chest rather than a single message, which is why the pieces
    /// are named after trades: the chest is opened for adding, the items are offered, and the offer
    /// is confirmed. This drives all three and reports what actually landed, so a script does not
    /// have to sequence them or guess when each step is done.
    /// </para>
    /// <para>
    /// The chest is opened and its contents read first, and the final confirmation follows the
    /// first one after a pause of 3 seconds, as in the game client. Every step shares
    /// <paramref name="timeoutMs"/>, so a budget of less than about 3 seconds cannot succeed.
    /// Deposits are served one at a time. When the call fails or times out while the trade is still
    /// open, the trade is canceled.
    /// </para>
    /// </remarks>
    /// <param name="chestId">The chest's room item id.</param>
    /// <param name="inventoryIds">The inventory item ids to put in, at most 1000 unique ids.</param>
    /// <param name="timeoutMs">The total timeout in milliseconds for the whole deposit, from 1 to 120000.</param>
    /// <returns>
    /// Whether it went through, and what the chest reported taking. A canceled or refused trade is
    /// reported through <see cref="ChestDeposit.Success"/> and <see cref="ChestDeposit.Failure"/>
    /// rather than thrown.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inventoryIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no items were named, or an id is repeated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is out of range, or an id is invalid.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the deposit did not finish in time.</exception>
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
    /// will not take them, and offering one drags the whole exchange down with it. When no
    /// tradeable item is left, nothing is sent and an unsuccessful result is returned. Otherwise
    /// this runs <see cref="DepositToChest(Id, IEnumerable{Id}, int)"/>, and
    /// <see cref="ChestDeposit.Requested"/> counts every item named, skipped ones included.
    /// </remarks>
    /// <param name="chest">The chest standing in the room.</param>
    /// <param name="items">The inventory items to put in.</param>
    /// <param name="timeoutMs">The total timeout in milliseconds for the whole deposit.</param>
    /// <returns>Whether it went through, what the chest reported taking, and which items were skipped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chest"/> or <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the deposit did not finish in time.</exception>
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
                [])
            { Skipped = skipped };
        }

        ChestDeposit result = await DepositToChest(chest.Id, takeable.Select(item => item.ItemId), timeoutMs);
        return result with { Requested = all.Length, Skipped = skipped };
    }

    /// <summary>
    /// Sends initial acceptance or final confirmation of the open wired trade.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the outcome arrives as a trade completion or
    /// cancellation.
    /// </remarks>
    /// <param name="confirm">
    /// <see langword="false"/> for initial acceptance; <see langword="true"/> for final confirmation
    /// after the three-second countdown. Prefer <see cref="CompleteWiredTrade(long, long, int)"/> for the full sequence.
    /// </param>
    public void WiredTradeConfirm(bool confirm = true) =>
        wired_send(
            ApplicationMemberIds.WiredTradeConfirm,
            new WiredTradeConfirmRequest(confirm));

    /// <summary>
    /// Cancels the open wired trade.
    /// </summary>
    /// <remarks>
    /// The request is sent without waiting; the server answers with a trade cancellation.
    /// </remarks>
    public void WiredTradeCancel() =>
        wired_send(
            ApplicationMemberIds.WiredTradeCancel,
            new WiredCommandRequest());

    /// <summary>
    /// Gets how much a wired chest in the room can hold, and how far it can still be upgraded.
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
    /// <see langword="true"/> for a coin chest; <see langword="false"/> for a furni chest. The two
    /// are configured separately.
    /// </param>
    /// <returns>
    /// The chest's capacity, upgrade level and limits, with the price of one upgrade. Configuration
    /// figures read as 0 until the game data has downloaded.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chest"/> is <see langword="null"/>.</exception>
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
    /// Gets how many capacity upgrades a chest furni has had, read from its stuff data.
    /// </summary>
    /// <remarks>
    /// The value is the <c>capacity_level</c> entry of the furni's map data. The client casts a
    /// missing value to zero rather than treating it as unknown, and so does this.
    /// </remarks>
    /// <param name="chest">The chest furni.</param>
    /// <returns>
    /// The upgrade level, or 0 for a furni that is not a chest, has no map data, or has no
    /// parsable <c>capacity_level</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chest"/> is <see langword="null"/>.</exception>
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
    /// Gets what it costs to buy several capacity upgrades for a chest at once.
    /// </summary>
    /// <remarks>
    /// The client offers one upgrade, then every amount up to the ceiling the chest has left, so an
    /// amount beyond <see cref="WiredChestCapacity.UpgradesRemaining"/> is not something it can
    /// send. Diamonds are activity point type 5. Same as
    /// <see cref="WiredChestUpgradeCost(int)"/>.
    /// </remarks>
    /// <param name="upgrades">The number of upgrades to buy.</param>
    /// <returns>The total credit and diamond price of <paramref name="upgrades"/> upgrades.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="upgrades"/> is negative.</exception>
    public (int Credits, int Diamonds) ChestUpgradeCostFor(int upgrades) =>
        WiredChestUpgradeCost(upgrades);

    /// <summary>Accepts and completes the reviewed Wired offer if its state is still current.</summary>
    /// <param name="expectedGeneration">The generation from the reviewed Wired state.</param>
    /// <param name="expectedRevision">The revision from the reviewed Wired state.</param>
    /// <param name="timeoutMs">The whole-operation timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The completion result, including a cancellation or failure reason.</returns>
    /// <remarks>The offer must already be acceptable. Changed offers are never confirmed.</remarks>
    public Task<WiredTradeCompleteResult> CompleteWiredTrade(
        long expectedGeneration,
        long expectedRevision,
        int timeoutMs = 30000) =>
        _application.InvokeAsync<WiredTradeCompleteRequest, WiredTradeCompleteResult>(
            ApplicationMemberIds.WiredTradeComplete,
            new WiredTradeCompleteRequest(expectedGeneration, expectedRevision, timeoutMs),
            Ct).AsTask();

    /// <summary>Sends one explicitly named Wired confirmation stage without waiting.</summary>
    /// <param name="stage">Accept first; Confirm only after the countdown.</param>
    public void WiredTradeConfirm(WiredTradeConfirmationStage stage) =>
        wired_send(ApplicationMemberIds.WiredTradeStageSend, new WiredTradeStageRequest(stage));

    private string FurniClassOf(FloorItem chest) =>
        FurniOf(chest)?.ClassName ?? "";
}

/// <summary>
/// Represents what a wired chest holds and how much room it has left to grow.
/// </summary>
/// <remarks>
/// It is worked out from the chest furni and the hotel configuration together, see
/// <see cref="ScriptGlobals.ChestCapacityOf(FloorItem, bool)"/>.
/// </remarks>
/// <param name="Capacity">The number of items the chest holds now.</param>
/// <param name="CapacityLevel">The number of upgrades it has had.</param>
/// <param name="MaxUpgrades">The most upgrades the hotel allows on a chest of this kind.</param>
/// <param name="UpgradesRemaining">The number of further upgrades it will accept, which is zero for a starter chest.</param>
/// <param name="IsStarterChest">A value indicating whether this is a starter chest, which cannot be upgraded.</param>
/// <param name="CapacityPerUpgrade">The capacity one upgrade adds.</param>
/// <param name="UpgradeCostCredits">The price of one upgrade in credits.</param>
/// <param name="UpgradeCostDiamonds">The price of one upgrade in diamonds.</param>
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
    /// <summary>Gets whether the chest will accept at least one more capacity upgrade.</summary>
    public bool CanUpgrade => UpgradesRemaining > 0;
}

/// <summary>
/// Represents the outcome of putting items into a wired chest.
/// </summary>
/// <param name="Success">A value indicating whether the exchange completed and the chest reported storing the accepted items.</param>
/// <param name="Failure">The reason the deposit failed, or an empty string when it succeeded.</param>
/// <param name="Requested">The number of items named for the deposit.</param>
/// <param name="Stored">
/// The storage rows the chest reported for the items it took, one per accepted item. It is shorter
/// than <paramref name="Requested"/> when the offer did not take every item, and empty when the
/// deposit failed.
/// </param>
public sealed record ChestDeposit(
    bool Success,
    string Failure,
    int Requested,
    IReadOnlyList<WiredChestStorageSnapshot> Stored)
{
    /// <summary>Gets the number of requested items the chest had in the offer when it was confirmed.</summary>
    public int Accepted { get; init; }

    /// <summary>
    /// Gets the ids of the items that were never offered because a chest will not take them.
    /// </summary>
    /// <remarks>
    /// These are untradeable furni, which the hotel refuses rather than stores. This is read off
    /// the item rather than proven from the client, whose own filter lives in the inventory view
    /// that decides what is clickable, so it is reported here instead of dropped quietly.
    /// </remarks>
    public IReadOnlyList<Id> Skipped { get; init; } = [];

    /// <summary>Gets the wired state generation the result was taken at.</summary>
    /// <remarks>The generation changes when the wired state is reset, for example on a room change.</remarks>
    public long Generation { get; init; }

    /// <summary>Gets the wired state revision the result was taken at.</summary>
    public long Revision { get; init; }

    /// <summary>Gets whether the deposit succeeded and the chest stored every requested item.</summary>
    public bool StoredEverything => Success && Stored.Count >= Requested;
}
