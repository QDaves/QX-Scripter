using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static WiredTradeInitiate? copy_initiation(WiredTradeInitiate? value) => value is null ? null : value with
    {
        Requirement = value.Requirement with
        {
            Rules = value.Requirement.Rules is { } rules ? rules with
            {
                Definition = rules.Definition with
                {
                    YouGiveRule = rules.Definition.YouGiveRule?.Select(rule => rule with { Nodes = rule.Nodes.ToArray() }).ToArray(),
                    YouGetRule = rules.Definition.YouGetRule is { } reward ? reward with { Nodes = reward.Nodes.ToArray() } : null
                }
            } : null
        }
    };

    private static WiredSimulationTrade? copy_trade(WiredSimulationTrade? trade) => trade is null ? null : trade with
    {
        Initiation = copy_initiation(trade.Initiation),
        Reservations = trade.Reservations?.Select(value => value with { InventoryIds = value.InventoryIds.ToArray() }).ToArray()
    };

    private WiredSimulationValue? trade_value(WiredSimulationUser user, string name)
    {
        if (name is not ("@transaction.in_trade" or "@transaction.contract_id" or "@transaction.state" or
            "@transaction.current_multiplier" or "@transaction.start_time" or "@transaction.can_accept")) unsupported_variable(name);
        if (user.Trade is not { } trade) unsupported_variable(name + " without captured transaction state");
        var current = user.Trade!;
        if (!current.Active) return null;
        if (name == "@transaction.can_accept")
        {
            if (current.CanAccept is { } can_accept) return can_accept ? new(-1) : null;
            unsupported_variable(name + " without captured eligibility");
        }
        long? number = name switch
        {
            "@transaction.in_trade" => -1,
            "@transaction.contract_id" => current.ContractId,
            "@transaction.state" => current.State,
            "@transaction.current_multiplier" => current.Multiplier,
            _ => null
        };
        if (number is { } value) return new(value);
        if (name == "@transaction.contract_id" && current.ChestDeposit) return null;
        unsupported_variable(name + " without captured value");
        return null;
    }

    private void initiate_transaction(WiredSimulationNode node, Frame frame, long[] chests, long[] recipients)
    {
        long[] contracts = fs(node, frame, 1);
        if (chests.Length is < 1 or > 2 || recipients.Length != 1 || contracts.Length != 1)
            unsupported(node, "Transaction initiation requires one credit chest, at most one companion furniture chest, one human and one selected contract.");
        if (furniture[contracts[0]].Contract is { ContractType: WiredContractContents.TypeReward } static_reward)
        {
            reward_contract(node, frame, chests, recipients[0], static_reward);
            return;
        }
        var specification = contract_terms(node, contracts[0]);
        var user = users[recipients[0]];
        if (user.IsBot || user.IsPet || user.Trade is null)
            unsupported(node, "Transaction initiation requires a human with known pending-trade state.");
        if (user.Trade!.Active)
            unsupported(node, "Starting another offer while a transaction is active requires separate conformance.");
        if (chests.Count(id => furniture[id].Chest?.StoresCoins == true) != 1 ||
            chests.Any(id => furniture[id].Chest is not { WiredEnabled: not null, Locked: not null }))
            unsupported(node, "The transaction requires one known credit chest and known Wired upgrade and lock states for each selected chest.");
        if (chests.Any(id => furniture[id].Chest!.WiredEnabled != true || furniture[id].Chest!.Locked == true))
        {
            transaction_event(node, frame, user.Id, 15, 0);
            return;
        }
        int mode = checked((int)node.Number("transaction_mode"));
        if (mode is < 0 or > 2 || node.Number("multiplier_kind") != 0)
            unsupported(node, "Transaction multiplier variables require separate conformance.");
        if (specification.Donation && mode != 0)
            unsupported(node, "Donation contracts with multiplier modes require separate conformance.");
        if (specification.Reward is not null && mode != 0)
            unsupported(node, "Trade contracts with multiplier modes require separate conformance.");
        int multiplier = checked((int)node.Number("multiplier"));
        if (mode != 0 && multiplier is < 2 or > 500)
            unsupported(node, "The transaction multiplier is outside the verified range.");
        int timeout = node.Flag("timeout_enabled") ? checked((int)node.Number("timeout_seconds")) : 0;
        if (timeout != 0 && timeout is < 30 or > 3600)
            unsupported(node, "The transaction timeout is outside the verified range.");
        var rules = specification.Donation ? null : new TradeRequirementRules(specification.Definition,
            mode, mode == 1 ? multiplier : 1, mode == 2 ? multiplier : 1);
        var initiation = new WiredTradeInitiate(new(specification.Donation ? 2 : 4,
            specification.Text, specification.Layout, rules), true, false, timeout);
        WiredSimulationReservation[] reservations = [];
        if (specification.Reward is { } reward)
        {
            var sources = chests.Select(id => furniture[id]).Where(item => item.Chest is { StoresCoins: false }).ToArray();
            if (sources.Length != 1) unsupported(node, "The trade reward needs one known furniture chest.");
            var source = sources[0];
            require_chest_items(source, node);
            int[] selected = source.Chest!.Items!.Where(item => item.Type == reward.ItemType)
                .Take(reward.Amount).Select(item => item.InventoryId).ToArray();
            if (selected.Length != reward.Amount)
            {
                transaction_event(node, frame, user.Id, 6, 0, contract_id: contracts[0]);
                return;
            }
            reservations = [new(source.Id, selected)];
            var selected_ids = selected.ToHashSet();
            furniture[source.Id] = source with { Chest = source.Chest with
            { Items = source.Chest.Items!.Select(item => selected_ids.Contains(item.InventoryId) ? item with { LockState = 2 } : item).ToArray(), PreviewItems = null } };
        }
        long revision = checked(++transaction_revision);
        users[user.Id] = user with { Trade = new(true, contracts[0], State: 1, Multiplier: 0,
            Initiation: initiation, Revision: revision, Reservations: reservations, CanAccept: false,
            CreditChestId: chests.Single(id => furniture[id].Chest!.StoresCoins), ActionId: node.FurnitureId) };
        effects.Add(new(now, "TradeInitiated", node.FurnitureId, [user.Id], TradeInitiation: copy_initiation(initiation)));
        if (timeout > 0)
            enqueue(checked(now + timeout * 1000L), frame, trade_timeout: new(user.Id, revision, node.FurnitureId));
        record("transaction.initiated", node.FurnitureId, "A payment offer was opened without transferring resources.", [user.Id]);
    }

    private (TradeRequirementRulesDefinition Definition, string Text, string Layout, bool Donation, TradeRequirementNode? Reward) contract_terms(
        WiredSimulationNode effect, long id)
    {
        if (furniture[id].Contract is { } contract)
        {
            if (contract.ContractId != id || contract.ContractType is not (WiredContractContents.TypePayment or WiredContractContents.TypeTrade) || contract.PaymentMode is not (0 or 1))
                unsupported(effect, "Unsupported static contract type or mode.");
            bool donation = contract.ContractType == WiredContractContents.TypePayment && contract.PaymentMode == 0;
            if (!donation && (contract.Definition.YouGiveRule is not { Count: > 0 } alternatives ||
                alternatives.Any(rule => rule.Nodes.Count == 0 || rule.Nodes.Any(item => item.Type != 0 || item.Amount < 1))))
                unsupported(effect, "Static payment contracts currently require nonempty credit-only alternatives.");
            TradeRequirementNode? reward = null;
            if (contract.ContractType == WiredContractContents.TypeTrade)
            {
                if (contract.Definition.YouGetRule?.Nodes is not { Count: 1 } rewards ||
                    rewards[0] is not { Type: 1, Amount: > 0, ItemType: { IsWallItem: false } })
                    unsupported(effect, "Trade contracts currently support one floor-furniture reward.");
                reward = contract.Definition.YouGetRule!.Nodes[0];
            }
            return (WiredManager.SnapshotOf(contract).Definition, contract.ReceiveText, contract.LayoutType, donation, reward);
        }
        if (!nodes.TryGetValue(id, out var custom) || custom.Name != "CustomContract")
            unsupported(effect, "The selected contract needs captured static contents or a typed custom-contract node.");
        var configuration = nodes[id];
        if (!configuration.Flag("payment_enabled") || configuration.Flag("reward_enabled") ||
            configuration.Number("payment_type") != 0 || configuration.Number("payment_operand_kind") != 0)
            unsupported(effect, "Custom transactions currently support a constant credit payment with no reward.");
        long payment = configuration.Number("payment_amount");
        if (payment is < 1 or > 100000)
            unsupported(effect, "The custom payment amount is outside its verified range.");
        return (new([new([new(0, (int)payment, null)])], null), "", "", false, null);
    }

    private void trade_timeout(TradeTimeout timeout)
    {
        if (!users.TryGetValue(timeout.UserId, out var user) || user.Trade is not { Active: true } trade || trade.Revision != timeout.Revision)
            return;
        release_trade(user.Id, timeout.ActionId);
        effects.Add(new(now, "TradeCancelled", timeout.ActionId, [user.Id], Value: 2));
        record("transaction.timeout", timeout.ActionId, "The pending offer expired without transferring resources.", [user.Id]);
    }

    private void reward_contract(WiredSimulationNode node, Frame frame, long[] chests, long recipient_id, WiredContractContents contract)
    {
        if (contract.ContractId != fs(node, frame, 1).Single() || node.Number("transaction_mode") != 0 ||
            contract.Definition.YouGetRule?.Nodes is not { Count: 1 } rewards ||
            rewards[0] is not { Type: 1, Amount: > 0, ItemType: { IsWallItem: false } })
            unsupported(node, "Static reward contracts currently support one floor-furniture reward in normal transaction mode.");
        var reward = contract.Definition.YouGetRule!.Nodes[0];
        var recipient = users[recipient_id];
        if (recipient.IsBot || recipient.IsPet)
            unsupported(node, "Static contract rewards to bots or pets require separate conformance.");
        var candidates = chests.Select(id => furniture[id]).Where(item => item.Chest is { StoresCoins: false }).ToArray();
        if (candidates.Length != 1 || candidates[0].Chest is not { WiredEnabled: not null, Locked: not null })
            unsupported(node, "Static furniture rewards require one known furniture chest.");
        var item = candidates[0];
        if (item.Chest!.WiredEnabled != true || item.Chest.Locked == true)
        {
            transaction_event(node, frame, recipient_id, 15, 0, contract_id: contract.ContractId);
            return;
        }
        require_chest_items(item, node);
        var matching = item.Chest.Items!.Where(entry => entry.Type == reward.ItemType).Take(reward.Amount).ToArray();
        if (matching.Length < reward.Amount)
        {
            transaction_event(node, frame, recipient_id, 6, 0, contract_id: contract.ContractId);
            return;
        }
        transfer_furniture(node, frame, item, recipient, matching,
            contract.ShowDialog ? contract.RewardText : "", contract.ContractId);
    }
}
