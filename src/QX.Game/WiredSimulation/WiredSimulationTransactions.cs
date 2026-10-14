using Qx.Model.Wired;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void cancel_transaction(WiredSimulationNode node, Frame frame, long[] recipients)
    {
        if (recipients.Length > 1) unsupported(node, "Cancellation for several recipients requires separate conformance.");
        long criteria = node.Number("match_criteria");
        if (criteria is not (0 or 1)) unsupported(node, "Unknown transaction cancellation mode.");
        foreach (long id in recipients)
        {
            if (users[id].Trade is not { } trade)
                unsupported(node, "Transaction cancellation requires known active or inactive trade state for the recipient.");
            var current = users[id].Trade!;
            if (!current.Active) continue;
            if (criteria == 0)
            {
                if (current.ChestDeposit) continue;
                if (current.ContractId is not { } contract)
                    unsupported(node, "Filtered cancellation requires the pending contract ID.");
                if (!fs(node, frame).Contains(current.ContractId!.Value)) continue;
            }
            if (!current.ChestDeposit && current.Initiation is null)
                unsupported(node, "Cancellation of contract-driven transactions requires separate conformance.");
            if (current.Initiation?.Requirement.Rules?.Definition.YouGetRule is { Nodes.Count: > 0 } && current.Reservations is null)
                unsupported(node, "Cancellation of a reward-bearing offer requires its reservation metadata.");
            release_trade(id, node.FurnitureId);
            effects.Add(new(now, "TradeCancelled", node.FurnitureId, [id], Value: 3));
            record("transaction.cancelled", node.FurnitureId, "The pending transaction was cancelled without transferring resources.", [id]);
        }
    }

    private static WiredSimulationEffect copy_effect(WiredSimulationEffect effect) => effect with
    {
        Targets = effect.Targets.ToArray(),
        Link = effect.Link is { } link ? copy(link) : null,
        Visual = effect.Visual is { } visual ? visual with { Entities = visual.Entities.ToArray() } : null,
        TradeInitiation = copy_initiation(effect.TradeInitiation),
        ChestTransfer = effect.ChestTransfer is { } transfer ? transfer with
        {
            SourceItems = transfer.SourceItems.Select(copy_chest_entry).ToArray()
        } : null
    };

    private void reward_furniture(WiredSimulationNode node, Frame frame, long[] chests, long[] recipients)
    {
        if (chests.Length != 1 || recipients.Length != 1)
            unsupported(node, "Furniture rewards currently require one chest and one recipient; distribution and empty-source errors need separate conformance.");
        var item = furniture[chests[0]];
        var recipient = users[recipients[0]];
        if (recipient.IsBot || recipient.IsPet)
            unsupported(node, "Chest rewards to bots and pets require separate conformance.");
        if (item.Chest is not { StoresCoins: false, WiredEnabled: not null, Locked: not null } chest)
            unsupported(node, "Furniture rewards require a captured furniture chest, Wired upgrade status and lock state.");
        var storage = item.Chest!;
        if (storage.WiredEnabled != true || storage.Locked == true)
        {
            transaction_event(node, frame, recipient.Id, 15, 0);
            return;
        }
        require_chest_items(item, node);
        long mode = node.Number("rewarding_mode"), order = node.Number("iteration_mode");
        if (mode is not (0 or 1) || order is not (1 or 2))
            unsupported(node, "Furniture reward modes other than specified/all and FIFO/LIFO require separate conformance.");
        long? amount = mode == 1 ? storage.Items!.Count : operand(node, frame, "amount_kind", "amount", "amount_target", "amount_variable");
        long quantity = amount.GetValueOrDefault();
        if (!amount.HasValue || quantity < 1)
            unsupported(node, "Missing, empty or nonpositive furniture reward amounts require separate conformance.");
        if (quantity > storage.Items!.Count)
        {
            transaction_event(node, frame, recipient.Id, 6, 0);
            return;
        }
        var source = order == 1 ? storage.Items : storage.Items.Reverse();
        var transferred = source.Take(checked((int)quantity)).ToArray();
        transfer_furniture(node, frame, item, recipient, transferred,
            node.Flag("show_popup") ? node.Text("popup_text") : "");
    }

    private void transfer_furniture(WiredSimulationNode node, Frame frame, WiredSimulationFurniture item,
        WiredSimulationUser recipient, WiredChestStorageSnapshot[] transferred, string text, long? contract_id = null)
    {
        var storage = item.Chest!;
        var selected = transferred.Select(entry => entry.InventoryId).ToHashSet();
        furniture[item.Id] = item with { Chest = storage with
        {
            Items = storage.Items!.Where(entry => !selected.Contains(entry.InventoryId)).ToArray(), PreviewItems = null
        } };
        charge(frame, transferred.Length, node.FurnitureId);
        effects.Add(new(now, "FurnitureReward", node.FurnitureId, [recipient.Id],
            text, transferred.Length,
            ChestTransfer: new(item.Id, recipient.Id, transferred.Select(copy_chest_entry).ToArray())));
        transaction_event(node, frame, recipient.Id, null, transferred.Length, contract_id: contract_id);
    }

    private void reward_currency(WiredSimulationNode node, Frame frame, long[] chests, long[] recipients)
    {
        if (chests.Length != 1 || recipients.Length != 1)
            unsupported(node, "Currency rewards currently require one chest and one recipient; distribution and empty-source errors need separate conformance.");
        var item = furniture[chests[0]];
        var recipient = users[recipients[0]];
        if (recipient.IsBot || recipient.IsPet)
            unsupported(node, "Currency rewards to bots and pets require separate conformance.");
        if (item.Chest is not { StoresCoins: true, Coins: >= 0, WiredEnabled: not null, Locked: not null })
            unsupported(node, "Currency rewards require a captured credit chest, known balance, Wired upgrade status and lock state.");
        var storage = item.Chest!;
        if (storage.WiredEnabled != true || storage.Locked == true)
        {
            transaction_event(node, frame, recipient.Id, 15, 0);
            return;
        }
        long mode = node.Number("rewarding_mode"), category = node.Number("earnings_category");
        if (mode is not (0 or 1) || category is not (11 or 13))
            unsupported(node, "Currency rewards support specified/all amounts and the verified games/agency earnings categories.");
        long? amount = mode == 1 ? storage.Coins : operand(node, frame, "amount_kind", "amount", "amount_target", "amount_variable");
        if (!amount.HasValue || amount < 0)
            unsupported(node, "Missing or negative currency reward operands require separate conformance.");
        long quantity = amount.GetValueOrDefault();
        if (quantity == 0)
        {
            transaction_event(node, frame, recipient.Id, 10, 0);
            return;
        }
        if (quantity > storage.Coins)
        {
            transaction_event(node, frame, recipient.Id, 6, 0);
            return;
        }
        int coins = checked((int)quantity);
        furniture[item.Id] = item with { Chest = storage with { Coins = storage.Coins - coins } };
        effects.Add(new(now, "CurrencyReward", node.FurnitureId, [recipient.Id],
            node.Flag("show_popup") ? node.Text("popup_text") : "", coins,
            ChestTransfer: new(item.Id, recipient.Id, [], coins, (EarningCategory)category)));
        transaction_event(node, frame, recipient.Id, null, 0, coins);
    }

    private void transaction_event(WiredSimulationNode node, Frame frame, long recipient, int? failure, int count, int coins = 0, long? contract_id = null, int deposit_coins = 0)
    {
        var values = failure is { } reason
            ? new Dictionary<string, long> { ["@event.transaction_failed.reason"] = reason }
            : new Dictionary<string, long>
            {
                ["@event.transaction_complete.multiplier"] = 1,
                ["@event.transaction_complete.deposit.furni_count"] = 0,
                ["@event.transaction_complete.deposit.coins_count"] = deposit_coins,
                ["@event.transaction_complete.withdrawal.furni_count"] = count,
                ["@event.transaction_complete.withdrawal.coins_count"] = coins
            };
        var context = variables.Values.Where(variable => variable.Domain == WiredSourceDomain.Context && values.ContainsKey(variable.Name))
            .ToDictionary(variable => variable.Id, variable => values[variable.Name], StringComparer.Ordinal);
        var kind = failure.HasValue ? WiredSimulationEventKind.TransactionFailed : WiredSimulationEventKind.TransactionCompleted;
        var input = new WiredSimulationEvent(kind, now, recipient, contract_id ?? node.FurnitureId, NewValue: failure ?? count + coins);
        enqueue(now, frame with { Root = ++root_sequence, Event = input, Context = context }, external: true);
        record("transaction", node.FurnitureId, failure.HasValue ? "failed" : "completed", [recipient], after: failure ?? count + coins);
    }
}
