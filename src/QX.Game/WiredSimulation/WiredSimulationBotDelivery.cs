using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private int bot_delivery_delay()
    {
        long interval = (initial.BotTiming?.StepMilliseconds ?? 0) * 2L;
        return interval > 0 && bot_delivery_phase.HasValue
            ? (int)((bot_delivery_phase.Value - now % interval + interval) % interval)
            : 0;
    }

    private void start_delivery(WiredSimulationNode node, Frame frame, WiredSimulationUser bot,
        WiredSimulationUser recipient, int item)
    {
        var timing = initial.BotTiming;
        if (timing is null || timing.DeliveryArrivalMilliseconds is not >= 0 || timing.DeliveryStartOffsetMilliseconds is null)
            unsupported(node, "Distant hand-item delivery requires calibrated delivery arrival and start-clock timing.");
        if (siblings(node).Any(box => box.Name is "AnimationTime" or "NoMoveAnimation" or "MovementPhysics" or "CarryUsers"))
            unsupported(node, "Bot delivery with movement modifiers requires separate conformance.");
        var path = delivery_route(node, frame, bot, recipient);
        if (path.Count < 2)
            unsupported(node, "The delivery recipient has no reachable adjacent destination; blocked delivery requires conformance.");
        for (int index = 1; index < path.Count; index++)
            if (Math.Abs(standing(path[index].X, path[index].Y) - standing(path[index - 1].X, path[index - 1].Y)) > 0.2001)
                unsupported(node, "Bot delivery across unverified step heights requires climbing conformance.");
        long revision = checked(++bot_revision);
        hand_item(node, bot.Id, item);
        users[bot.Id] = users[bot.Id] with
        {
            Delivery = new(recipient.Id, node.FurnitureId, revision, item, recipient.X, recipient.Y, recipient.Z, path[^1]),
            NextStep = path[1]
        };
        effects.Add(new(now, "BotDeliveryDestination", node.FurnitureId, [bot.Id, recipient.Id], Text: path[^1].X + "," + path[^1].Y));
        enqueue(checked(now + bot_delivery_delay()), frame,
            bot_walk: new(bot.Id, 0, path[^1].X, path[^1].Y, timing!.StepMilliseconds,
                node.FurnitureId, 0, path, DeliveryRevision: revision));
    }

    private IReadOnlyList<Tile> delivery_route(WiredSimulationNode node, Frame frame,
        WiredSimulationUser bot, WiredSimulationUser recipient)
    {
        var candidates = new List<(IReadOnlyList<Tile> Path, int Cost)>();
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0) continue;
            var destination = new Tile(recipient.X + x, recipient.Y + y);
            if (!walkable(destination.X, destination.Y, bot.Id) || furniture.Values.Any(item => covers(item, destination.X, destination.Y) && item.Stance != AvatarStance.Stand)) continue;
            var path = bot_route(node, frame, bot, destination);
            if (path.Count < 2) continue;
            int cost = 0;
            for (int step = 1; step < path.Count; step++)
                cost += path[step].X != path[step - 1].X && path[step].Y != path[step - 1].Y ? 14 : 10;
            candidates.Add((path, cost));
        }
        if (candidates.Count == 0) return [];
        int minimum = candidates.Min(candidate => candidate.Cost);
        var closest = candidates.Where(candidate => candidate.Cost == minimum).ToArray();
        if (options.BotDeliveryChoices is { } choices && bot_delivery_choice < choices.Count)
        {
            var choice = choices[bot_delivery_choice++];
            if (choice.BotId != bot.Id || choice.UserId != recipient.Id)
                unsupported(node, "The next explicit delivery choice belongs to different actors.");
            var selected = closest.Where(candidate => candidate.Path[^1] == choice.Destination).ToArray();
            if (selected.Length != 1)
                unsupported(node, "The explicit delivery destination is not among the nearest reachable candidates.");
            return selected[0].Path;
        }
        if (closest.Length != 1)
            note(WiredSimulationSeverity.Uncertain, "bot.delivery.choice", "Several delivery destinations have equal route cost. Supply a known BotDeliveryChoices entry to replay a particular choice.", node.FurnitureId);
        return closest[closest.Length == 1 ? 0 : (int)(next_random() % closest.Length)].Path;
    }

    private WiredSimulationUser delivery_recipient(WiredSimulationNode node, WiredSimulationUser bot)
    {
        var delivery = bot.Delivery!;
        if (!users.TryGetValue(delivery.UserId, out var recipient))
            unsupported(node, "The delivery recipient left; pending delivery cancellation requires conformance.");
        if (Math.Abs(recipient!.Z - delivery.TargetZ) > 0.2001)
            unsupported(node, "The delivery recipient moved across an unverified elevation.");
        if (recipient.Stance != AvatarStance.Stand || bot.Stance != AvatarStance.Stand || bot.Frozen || bot.Effect != 0 ||
            bot.HandItem != delivery.HandItem)
            unsupported(node, "The delivery actor posture, effect or carried item changed during delivery.");
        return recipient;
    }

    private void bot_handoff(BotHandoff handoff, Frame frame)
    {
        if (!users.TryGetValue(handoff.BotId, out var bot) || bot.Delivery is not { } delivery || delivery.Revision != handoff.Revision) return;
        var node = nodes[delivery.ActionId];
        var recipient = delivery_recipient(node, bot);
        if (bot.NextStep is not null || bot.X != delivery.Destination.X || bot.Y != delivery.Destination.Y)
            unsupported(node, "The bot left its recorded delivery destination before the handoff.");
        hand_item(node, recipient.Id, delivery.HandItem);
        hand_item(node, bot.Id, 0);
        users[bot.Id] = users[bot.Id] with { Delivery = null };
        if (Math.Max(Math.Abs(bot.X - recipient.X), Math.Abs(bot.Y - recipient.Y)) <= 1)
            dispatch(frame with { Root = ++root_sequence, Context = new(StringComparer.Ordinal),
                Event = new(WiredSimulationEventKind.BotAvatarReached, now, bot.Id, Users: [recipient.Id]) });
    }
}
