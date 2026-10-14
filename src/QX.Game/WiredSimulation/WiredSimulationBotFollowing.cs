using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void follow_bots(WiredSimulationNode node, Frame frame)
    {
        long mode = node.Number("following");
        if (mode is not (0 or 1)) unsupported(node, "Unknown bot following mode.");
        var selected = has_user_source(node)
            ? us(node, frame).Where(id => users[id].IsBot).ToArray()
            : users.Values.Where(user => user.IsBot && user.Name.Equals(node.Text("bot_name"), StringComparison.OrdinalIgnoreCase)).Select(user => user.Id).ToArray();
        if (selected.Length == 0) return;
        if (selected.Length != 1) unsupported(node, "Following with several selected bots requires ordering conformance.");
        var bot = users[selected[0]];
        var targets = us(node, frame, 1);
        if (targets.Length == 0) return;
        if (targets.Length != 1 || users[targets[0]].IsBot || users[targets[0]].IsPet)
            unsupported(node, "Following multiple users, bots or pets requires separate conformance.");
        if (mode == 0)
        {
            if (bot.Delivery is not null)
                unsupported(node, "Stopping following during item delivery requires separate conformance.");
            if (bot.Follow is null)
            {
                if (bot.NextStep is not null) unsupported(node, "Stopping follow during an unrelated bot movement requires conformance.");
                return;
            }
            if (bot.Follow.UserId != targets[0]) unsupported(node, "Stopping follow with a different user requires separate conformance.");
            var pending = queue.UnorderedItems.Select(entry => entry.Element.BotWalk)
                .FirstOrDefault(walk => walk is not null && walk.UserId == bot.Id && walk.FollowRevision == bot.Follow.Revision);
            if (pending is { StepsTaken: > 0 })
            {
                users[bot.Id] = bot with { Follow = bot.Follow with { Stopping = true } };
                return;
            }
            users[bot.Id] = bot with { Follow = null, NextStep = null };
            return;
        }
        if (bot.NextStep is not null || bot.Delivery is not null || pending_teleport(bot.Id) || bot.Frozen || bot.Effect != 0 || bot.Stance != AvatarStance.Stand)
            unsupported(node, "Starting following during another bot movement or effect requires separate conformance.");
        if (bot.Follow is { Arrived: false })
            unsupported(node, "Restarting follow before a pending arrival callback requires separate conformance.");
        if (siblings(node).Any(box => box.Name is "AnimationTime" or "NoMoveAnimation" or "MovementPhysics" or "CarryUsers"))
            unsupported(node, "Following with movement modifiers requires separate conformance.");
        users[bot.Id] = bot with { Follow = new(targets[0], node.FurnitureId, checked(++bot_revision)) };
        plan_follow(bot.Id, frame, false);
    }

    private void refresh_followers(long user_id, Frame frame)
    {
        foreach (var bot in users.Values.Where(user => user.IsBot && user.Follow is { Stopping: false } follow && follow.UserId == user_id).ToArray())
        {
            var target = users[user_id];
            if (Math.Max(Math.Abs(bot.X - target.X), Math.Abs(bot.Y - target.Y)) <= 1) continue;
            if (!bot.Follow!.Arrived) continue;
            users[bot.Id] = bot with { Follow = bot.Follow with { Revision = checked(++bot_revision), Arrived = false, Destination = null } };
            plan_follow(bot.Id, frame, true);
        }
    }

    private void plan_follow(long bot_id, Frame frame, bool refresh, bool retry = false)
    {
        var bot = users[bot_id];
        var follow = bot.Follow!;
        var node = nodes[follow.ActionId];
        if (!users.TryGetValue(follow.UserId, out var target))
            unsupported(node, "The followed user is absent.");
        if (target!.Stance != AvatarStance.Stand || Math.Abs(bot.Z - target.Z) > 0.2001)
            unsupported(node, "Following a seated user or across unverified elevations requires conformance.");
        var timing = initial.BotTiming;
        if (timing is null || timing.StepMilliseconds <= 0 || timing.NextStepOffsetMilliseconds < 0 ||
            timing.NextStepOffsetMilliseconds >= timing.StepMilliseconds || timing.FollowArrivalMilliseconds < 0 || timing.FollowRefreshMilliseconds < 0 || timing.FollowRetryMilliseconds < 0 ||
            timing.FollowStartOffsetMilliseconds is { } follow_offset && (follow_offset < 0 || follow_offset >= timing.StepMilliseconds * 2L))
            unsupported(node, "Following requires a valid calibrated Room.BotTiming profile.");
        if (Math.Max(Math.Abs(bot.X - target!.X), Math.Abs(bot.Y - target.Y)) <= 1)
        {
            users[bot.Id] = bot with { Follow = follow with { Arrived = true } };
            return;
        }
        var available = new List<Tile>();
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            var tile = new Tile(target.X + dx, target.Y + dy);
            if (walkable(tile.X, tile.Y, bot.Id) && !furniture.Values.Any(item => covers(item, tile.X, tile.Y) && item.Stance != AvatarStance.Stand))
                available.Add(tile);
        }
        if (available.Count == 0) unsupported(node, "The followed user has no open adjacent destination; retry behavior requires conformance.");
        Tile destination;
        if (options.BotFollowChoices is { } choices && bot_choice < choices.Count)
        {
            var choice = choices[bot_choice++];
            if (choice.BotId != bot.Id || choice.UserId != target.Id || !available.Contains(choice.Destination))
                unsupported(node, "The supplied bot follow choice does not match this bot, user or open destination.");
            destination = choice.Destination;
        }
        else
        {
            destination = available[(int)(next_random() % available.Count)];
            note(WiredSimulationSeverity.Uncertain, "bot.follow.choice", "A possible adjacent destination was sampled. Supply BotFollowChoices to replay a known choice; the hotel random choice cannot be recovered from this snapshot.", node.FurnitureId);
        }
        var path = bot_route(node, frame, bot, destination);
        if (path.Count < 2) unsupported(node, "The chosen follow destination has no known route.");
        for (int index = 1; index < path.Count; index++)
            if (Math.Abs(standing(path[index].X, path[index].Y) - standing(path[index - 1].X, path[index - 1].Y)) > 0.2001)
                unsupported(node, "Bot following across unverified step heights requires climbing conformance.");
        follow = follow with { Destination = destination, Arrived = false };
        users[bot.Id] = bot with { Follow = follow, NextStep = path[1] };
        effects.Add(new(now, "BotFollowDestination", node.FurnitureId, [bot.Id, target.Id], Text: destination.X + "," + destination.Y));
        long delay = retry ? (long)bot_step_delay() + timing!.FollowRetryMilliseconds
            : refresh ? (long)bot_step_delay() + timing!.FollowRefreshMilliseconds : bot_follow_delay();
        enqueue(checked(now + delay), frame, bot_walk: new(bot.Id, 0, destination.X, destination.Y,
            timing!.StepMilliseconds, node.FurnitureId, 0, path, FollowRevision: follow.Revision));
    }

    private void bot_arrival(BotArrival arrival, Frame frame)
    {
        if (!users.TryGetValue(arrival.BotId, out var bot) || bot.Follow is not { Stopping: false } follow || follow.Revision != arrival.Revision || follow.Arrived) return;
        var node = nodes[follow.ActionId];
        if (!users.TryGetValue(follow.UserId, out var target)) unsupported(node, "The followed user disappeared before arrival.");
        if (target!.Stance != AvatarStance.Stand || Math.Abs(bot.Z - target.Z) > 0.2001)
            unsupported(node, "The followed user's arrival posture or elevation changed.");
        if (Math.Max(Math.Abs(bot.X - target!.X), Math.Abs(bot.Y - target.Y)) > 1)
        {
            users[bot.Id] = bot with { Follow = follow with { Revision = checked(++bot_revision), Destination = null } };
            plan_follow(bot.Id, frame, false, true);
            return;
        }
        users[bot.Id] = bot with { Follow = follow with { Arrived = true } };
        dispatch(frame with { Root = ++root_sequence, Context = new(StringComparer.Ordinal),
            Event = new(WiredSimulationEventKind.BotAvatarReached, now, bot.Id, Users: [target.Id]) });
    }
}
