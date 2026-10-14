using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void trade_input(Frame frame)
    {
        var input = frame.Event;
        if (input.UserId is not { } user_id || !users.TryGetValue(user_id, out var user) || user.IsBot || user.IsPet ||
            user.Trade is not { Active: true } trade || input.Trade is not { } payment)
        {
            fail("transaction.offer_missing", "Trade input requires an active offer for an existing human.");
            return;
        }
        if (payment.Revision != trade.Revision)
        {
            fail("transaction.offer_changed", "Trade input refers to a different offer revision.");
            return;
        }
        if (trade.ActionId is not { } action_id || !nodes.TryGetValue(action_id, out var node) ||
            trade.ContractId is not { } contract_id || trade.CreditChestId is null)
        {
            fail("transaction.offer_incomplete", "The offer is missing its source action, contract or credit chest.");
            return;
        }
        var definition = trade.Initiation?.Requirement.Rules;
        if (definition is not { Type: 0, Multiplier: 1 } ||
            definition.Definition.YouGiveRule is not { Count: 1 } payments ||
            payments[0].Nodes is not { Count: 1 } requirements || requirements[0] is not { Type: 0, Amount: 1 } ||
            definition.Definition.YouGetRule?.Nodes is not { Count: 1 } rewards ||
            rewards[0] is not { Type: 1, Amount: 1, ItemType: { IsWallItem: false } })
            unsupported(node, "Trade input currently models a normal one-credit payment for one floor-furniture reward; other offer shapes need conformance.");
        if (input.Kind == WiredSimulationEventKind.OfferTrade)
        {
            if (trade.State != 1 || trade.AcceptedAt is not null || payment.Credits != 1)
                unsupported(node, "Only an exact one-credit normalized offer before acceptance has been verified; inventory valuation and excess payments are not inferred.");
            users[user_id] = user with { Trade = trade with { OfferedCredits = 1, Multiplier = 1, CanAccept = true } };
            effects.Add(new(now, "TradeOffered", action_id, [user_id], Value: 1));
            record("transaction.offered", action_id, "The external offer supplies one verified credit of payment value.", [user_id]);
            return;
        }
        if (payment.Credits is not null)
        {
            fail("transaction.unexpected_payment", "Acceptance and confirmation cannot change an existing payment.", action_id);
            return;
        }
        if (trade.CanAccept != true || trade.OfferedCredits != 1 || trade.Multiplier != 1)
        {
            fail("transaction.not_payable", "The offer has no verified exact payment.", action_id);
            return;
        }
        if (input.Kind == WiredSimulationEventKind.AcceptTrade)
        {
            if (trade.State != 1 || trade.AcceptedAt is not null)
            {
                fail("transaction.already_accepted", "The offer is not in its adding-items state.", action_id);
                return;
            }
            users[user_id] = user with { Trade = trade with { State = 2, AcceptedAt = now } };
            effects.Add(new(now, "TradeAccepted", action_id, [user_id]));
            record("transaction.accepted", action_id, "The exact offer was accepted; resources remain reserved until confirmation.", [user_id]);
            return;
        }
        if (trade.State != 2 || trade.AcceptedAt is not { } accepted_at || now - accepted_at < 3000)
            unsupported(node, "Confirmation requires an accepted offer and the native client's three-second countdown; early confirmations require separate conformance.");
        settle_trade(node, frame, user, trade, contract_id);
    }

    private void settle_trade(WiredSimulationNode node, Frame frame, WiredSimulationUser user, WiredSimulationTrade trade, long contract_id)
    {
        if (!furniture.TryGetValue(trade.CreditChestId!.Value, out var target) ||
            target.Chest is not { StoresCoins: true, WiredEnabled: true, Locked: false, Coins: >= 0, Capacity: > 0 } credit)
            unsupported(node, "Settlement requires a known unlocked upgraded credit chest, balance and capacity.");
        var credit_chest = target!.Chest!;
        if (credit_chest.Coins >= credit_chest.Capacity)
            unsupported(node, "Settlement at chest capacity needs a verified rejection model.");
        if (trade.Reservations is not { Count: 1 } reservations || reservations[0].InventoryIds.Count != 1 ||
            !furniture.TryGetValue(reservations[0].ChestId, out var source) ||
            source.Chest is not { StoresCoins: false, WiredEnabled: true, Locked: false, ItemsComplete: true, Items: not null })
            unsupported(node, "Settlement requires its single known reserved reward in an unlocked upgraded furniture chest.");
        var reservation = trade.Reservations![0];
        var source_item = furniture[reservation.ChestId];
        int selected_id = reservation.InventoryIds[0];
        var selected = source_item.Chest!.Items!.Where(item => item.InventoryId == selected_id).ToArray();
        if (selected.Length != 1 || selected[0].LockState != 2)
        {
            fail("transaction.reservation_missing", "The reserved reward changed before confirmation.", node.FurnitureId);
            return;
        }
        var expected = trade.Initiation!.Requirement.Rules!.Definition.YouGetRule!.Nodes[0].ItemType;
        if (selected[0].Type != expected)
        {
            fail("transaction.reservation_changed", "The reserved reward no longer matches its reviewed contract.", node.FurnitureId);
            return;
        }
        var transferred = selected.Select(copy_chest_entry).ToArray();
        var remaining = source_item.Chest.Items!.Where(item => item.InventoryId != selected_id).ToArray();
        int balance = checked(credit_chest.Coins!.Value + 1);
        if (queue.Count >= options.MaximumPendingEvents)
        {
            fail("simulation.pending_limit", "No capacity remains for the transaction completion event.", node.FurnitureId);
            return;
        }
        charge(frame, 1, node.FurnitureId);
        furniture[target.Id] = target with { Chest = credit_chest with { Coins = balance } };
        furniture[source_item.Id] = source_item with { Chest = source_item.Chest with { Items = remaining, PreviewItems = null } };
        users[user.Id] = user with { Trade = new(false) };
        effects.Add(new(now, "TradePayment", node.FurnitureId, [user.Id], Value: 1,
            ChestTransfer: new(target.Id, user.Id, [], 1)));
        effects.Add(new(now, "FurnitureReward", node.FurnitureId, [user.Id], Value: 1,
            ChestTransfer: new(source_item.Id, user.Id, transferred)));
        effects.Add(new(now, "TradeCompleted", node.FurnitureId, [user.Id], Value: 1));
        transaction_event(node, frame, user.Id, null, 1, contract_id: contract_id, deposit_coins: 1);
    }
}
