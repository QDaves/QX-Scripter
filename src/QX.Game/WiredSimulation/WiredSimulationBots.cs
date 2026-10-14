using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static IReadOnlyList<WiredSimulationBotMovementChoice>? copy_bot_movement_choices(
        IReadOnlyList<WiredSimulationBotMovementChoice>? choices)
    {
        if (choices is null) return null;
        return choices.Select(choice =>
        {
            if (choice is null || choice.Steps is null || choice.Steps.Any(step => step is null))
                throw new ArgumentException("Bot movement choices require nonnull steps.", nameof(choices));
            return choice with { Steps = choice.Steps.ToArray() };
        }).ToArray();
    }

    private bool shared_carrier(WiredSimulationFurniture source, WiredSimulationFurniture destination,
        IReadOnlyList<WiredSimulationUser> occupants, IReadOnlyList<WiredSimulationNode> addons) =>
        occupants.Count == 2 && occupants.Count(user => user.IsBot) == 1 &&
        occupants.All(user => user.Index is >= 0) && occupants.Select(user => user.Index).Distinct().Count() == 2 &&
        occupants.All(user => Math.Abs(user.Z - occupants[0].Z) < 0.01) &&
        source.Width == 1 && source.Length == 1 && source.Walkable && source.Stance == AvatarStance.Stand &&
        source.Direction == destination.Direction &&
        (source.X == destination.X && source.Y == destination.Y || !overlaps(source, destination)) &&
        occupants.All(user => covers(source, user.X, user.Y) && !user.IsPet && !user.Frozen && user.Effect == 0 &&
            user.NextStep is null && user.Stance == AvatarStance.Stand) &&
        !addons.Any(node => node.Name is "MovementPhysics" or "JumpStrength" or "Projectile");

    private bool movable_bot(WiredSimulationUser bot, WiredSimulationFurniture source, WiredSimulationFurniture destination,
        IReadOnlyList<WiredSimulationNode> addons, bool stationary = false, bool allow_curve = false) =>
        (source.Width == 1 && source.Length == 1 || source.Width is 1 or 2 && source.Length is 1 or 2 &&
            source.Direction == destination.Direction &&
            !overlaps(source, destination) && !addons.Any(node => node.Name is "MovementPhysics" or "JumpStrength")) &&
        source.Walkable && source.Stance == AvatarStance.Stand &&
        covers(source, bot.X, bot.Y) && (!covers(destination, bot.X, bot.Y) || stationary &&
            source.X == destination.X && source.Y == destination.Y && source.Direction == destination.Direction) && bot.Stance == AvatarStance.Stand &&
        !bot.Frozen && bot.Effect == 0 && bot.Follow is null && bot.Delivery is null && !pending_teleport(bot.Id) &&
        !addons.Any(node => node.Name == "Projectile" || node.Name == "JumpStrength" && !allow_curve) &&
        !queue.UnorderedItems.Any(entry => entry.Element.BotWalk?.UserId == bot.Id);

    private void move_bots(WiredSimulationNode node, Frame frame, long[] objects, long[] people)
    {
        if (objects.Length == 0 || people.Length == 0) return;
        if (objects.Length == 1 && people.Length == 1)
        {
            move_bot(node, frame, people[0], objects[0]);
            return;
        }
        if (people.Length is < 2 or > 3 || objects.Length > people.Length || people.Any(id => users[id].Stance != AvatarStance.Stand) ||
            objects.Any(id => furniture[id].Width != 1 || furniture[id].Length != 1 || furniture[id].Stance != AvatarStance.Stand))
            unsupported(node, "Larger bot groups, seated actors and larger destinations require further conformance.");
        WiredSimulationBotMovementStep[] steps;
        if (options.BotMovementChoices is { } choices && bot_movement_choice < choices.Count)
        {
            var choice = choices[bot_movement_choice];
            if (choice.ActionId != node.FurnitureId || choice.Steps.Count != people.Length ||
                choice.Steps.Select(step => step.BotId).Distinct().Count() != people.Length ||
                choice.Steps.Any(step => !people.Contains(step.BotId) || !objects.Contains(step.TargetId)))
                unsupported(node, "The next bot movement choice must assign every selected bot exactly once to a selected destination for this action.");
            if (choice.Steps.Select(step => step.TargetId).Distinct().Count() != objects.Length)
                unsupported(node, "Repeated destinations must still cover every selected destination in this bot movement branch.");
            steps = choice.Steps.ToArray();
            bot_movement_choice++;
        }
        else
        {
            uncertain("bot.movement.choice", "Bot order and destination choices are not recoverable from the saved selection. Supply BotMovementChoices for a known branch; permissive mode samples one possible branch.", node.FurnitureId);
            var order = people.ToArray();
            for (int index = order.Length - 1; index > 0; index--)
            {
                int other = (int)(next_random() % (uint)(index + 1));
                (order[index], order[other]) = (order[other], order[index]);
            }
            var targets = objects.ToArray();
            for (int index = targets.Length - 1; index > 0; index--)
            {
                int other = (int)(next_random() % (uint)(index + 1));
                (targets[index], targets[other]) = (targets[other], targets[index]);
            }
            steps = order.Select((id, index) => new WiredSimulationBotMovementStep(id, targets[index % targets.Length])).ToArray();
        }
        var group = people.ToHashSet();
        Dictionary<long, IReadOnlyList<Tile>>? paths = null;
        if (node.Name == "BotMove")
        {
            if (objects.Select(id => furniture[id]).Any(target => users.Values.Any(user => user.X == target.X && user.Y == target.Y)))
                unsupported(node, "Grouped walking to an initially occupied destination requires further conformance.");
            paths = steps.ToDictionary(step => step.BotId, step =>
                bot_route(node, frame, users[step.BotId], new(furniture[step.TargetId].X, furniture[step.TargetId].Y)));
        }
        foreach (var step in steps) move_bot(node, frame, step.BotId, step.TargetId, group, paths?.GetValueOrDefault(step.BotId));
    }

    private void move_bot(WiredSimulationNode node, Frame frame, long bot_id, long target_id,
        IReadOnlySet<long>? group = null, IReadOnlyList<Tile>? path = null)
    {
        var bot = users[bot_id];
        var target = furniture[target_id];
        bool own_effect = node.Name == "BotTeleport" && initial.BotTiming?.Teleport?.EffectId == bot.Effect &&
            queue.UnorderedItems.Any(entry => entry.Element.Teleport is { Bot: true, Restore: true } ending && ending.UserId == bot.Id);
        if (bot.Follow is not null || bot.Delivery is not null || bot.NextStep is not null ||
            pending_teleport(bot.Id, arrivals_only: node.Name == "BotTeleport") || bot.Frozen || bot.Effect != 0 && !own_effect)
            unsupported(node, "Moving, frozen or affected bots require separate conformance.");
        bool seated_start = node.Name == "BotMove" && bot.Stance == AvatarStance.Sit;
        if (node.Name == "BotMove" && (bot.Stance == AvatarStance.Lay || target.Stance == AvatarStance.Lay || target.Width != 1 || target.Length != 1))
            unsupported(node, "Walking from lying bot poses and to larger or lying destinations requires separate conformance.");
        if (seated_start)
        {
            var supports = furniture.Values.Where(item => covers(item, bot.X, bot.Y)).ToArray();
            if (supports.Length != 1 || supports[0].Stance != AvatarStance.Sit || !supports[0].Walkable ||
                supports[0].Width != 1 || supports[0].Length != 1 || bot.ActionHeight is not { } height ||
                Math.Abs(bot.Z - supports[0].Z) > 0.001 || Math.Abs(height - supports[0].Height) > 0.001 ||
                target.Stance != AvatarStance.Stand)
                unsupported(node, "A seated walking start requires one matching single-tile chair and a standing destination.");
        }
        if (node.Name == "BotMove" && target.Stance == AvatarStance.Sit &&
            furniture.Values.Any(item => item.Id != target.Id && covers(item, target.X, target.Y)))
            unsupported(node, "A seated walking destination overlapping other furniture requires separate conformance.");
        if (siblings(node).Any(box => box.Name is "AnimationTime" or "NoMoveAnimation" or "MovementPhysics" or "CarryUsers"))
            unsupported(node, "Bot movement modifiers require separate conformance.");
        if (!walkable(target.X, target.Y, bot.Id, bot_teleport: node.Name == "BotTeleport"))
        {
            if (node.Name == "BotMove")
            {
                record("bot.walk_blocked", node.FurnitureId, "The selected destination is blocked.", [bot.Id]);
                return;
            }
            unsupported(node, "Blocked bot destinations require fallback and route conformance.");
        }
        if (node.Name == "BotMove" && bot.X == target.X && bot.Y == target.Y) return;
        var timing = initial.BotTiming;
        if (timing is null)
            unsupported(node, "Bot movement requires a calibrated Room.BotTiming; it cannot be modeled as an instant transfer.");
        if (node.Name == "BotTeleport")
        {
            var teleport = timing!.Teleport;
            if (teleport is null || teleport.ArrivalMilliseconds <= 0 || teleport.EffectMilliseconds <= teleport.ArrivalMilliseconds || teleport.EffectId < 0)
                unsupported(node, "Invalid bot teleport timing profile.");
            var transfer = new Teleport(bot.Id, target.Id, target.X, target.Y, bot.Effect, Bot: true,
                TargetTop: target.Z + target.Height, TargetStance: target.Stance);
            long offset = bot_teleport_delay();
            enqueue(checked(now + offset + teleport!.ArrivalMilliseconds), frame, teleport: transfer);
            if (!own_effect)
            {
                users[bot.Id] = bot with { Effect = teleport.EffectId };
                effects.Add(new(now, "TeleportAnimation", node.FurnitureId, [bot.Id], Value: offset + teleport.EffectMilliseconds));
                enqueue(checked(now + offset + teleport.EffectMilliseconds), frame, teleport: transfer with { Restore = true });
            }
            return;
        }
        if (timing!.StepMilliseconds <= 0 || timing.NextStepOffsetMilliseconds < 0 || timing.NextStepOffsetMilliseconds >= timing.StepMilliseconds)
            unsupported(node, "Invalid bot walking timing profile.");
        path ??= bot_route(node, frame, bot, new(target.X, target.Y), target.Stance == AvatarStance.Sit);
        if (path.Count < 2)
        {
            record("bot.walk_blocked", node.FurnitureId, "The selected destination has no traversable route.", [bot.Id]);
            return;
        }
        for (int index = 1; index < path.Count; index++)
        {
            double next_height = index == path.Count - 1 && target.Stance == AvatarStance.Sit
                ? target.Z : standing(path[index].X, path[index].Y);
            double previous_height = index == 1 && seated_start ? bot.Z : standing(path[index - 1].X, path[index - 1].Y);
            if (Math.Abs(next_height - previous_height) > 0.2001)
                unsupported(node, "Bot step heights beyond the verified 0.2 range require climbing conformance.");
        }
        users[bot.Id] = bot with { NextStep = path[1] };
        enqueue(checked(now + bot_step_delay()), frame,
            bot_walk: new(bot.Id, target.Id, target.X, target.Y, timing.StepMilliseconds, node.FurnitureId, target.Z + target.Height, path,
                BotGroup: group, TargetStance: target.Stance, TargetBase: target.Z));
    }

    private int bot_step_delay()
    {
        int interval = initial.BotTiming?.StepMilliseconds ?? 0;
        return interval > 0 && bot_phase.HasValue ? (int)((bot_phase.Value - now % interval + interval) % interval) : 0;
    }

    private int bot_teleport_delay()
    {
        if (initial.BotTiming?.TeleportClock is not { } clock) return 0;
        int offset = (int)((bot_teleport_phase!.Value - now % clock.TickMilliseconds + clock.TickMilliseconds) % clock.TickMilliseconds);
        return offset == 0 ? clock.TickMilliseconds : offset;
    }

    private int bot_follow_delay()
    {
        long interval = (initial.BotTiming?.StepMilliseconds ?? 0) * 2L;
        return interval > 0 && bot_follow_phase.HasValue
            ? (int)((bot_follow_phase.Value - now % interval + interval) % interval)
            : bot_step_delay();
    }

    private bool bot_teleport_overlap(WiredSimulationUser user) =>
        user.IsBot || !user.IsPet && user.Stance == AvatarStance.Stand && !user.Frozen && user.Effect == 0 &&
        user.NextStep is null && user.Follow is null && user.Delivery is null &&
        !pending_teleport(user.Id, arrivals_only: true);

    private bool avatar_obstruction(long moving_id, int x, int y, IReadOnlySet<long>? group) =>
        floor(x, y) >= 0 && !furniture.Values.Any(item => covers(item, x, y) &&
            (!item.Walkable || item.Stance != AvatarStance.Stand)) &&
        users.Values.Any(other => other.Id != moving_id && !other.IsPet && (other.IsBot || !other.Frozen && other.Effect == 0) &&
            other.X == x && other.Y == y &&
            (group?.Contains(other.Id) == true || other.Stance == AvatarStance.Stand &&
                other.NextStep is null && other.Follow is null && other.Delivery is null &&
                !pending_teleport(other.Id, arrivals_only: true)));

    private bool closed_gate_obstruction(long moving_id, int x, int y)
    {
        if (floor(x, y) < 0 || users.Values.Any(user => user.Id != moving_id && user.X == x && user.Y == y))
            return false;
        var blockers = furniture.Values.Where(item => covers(item, x, y) && !item.Walkable).ToArray();
        return blockers.Length > 0 && blockers.All(item => item.Width == 1 && item.Length == 1 &&
            item.Height == 0 && item.Stance == AvatarStance.Stand && item.States is { } states &&
            state_changes.TryGetValue(item.Id, out var change) &&
            states.TryGetValue(change.PreviousState, out var previous) && previous.Walkable);
    }

    private void bot_step(BotWalk walking, Frame frame)
    {
        if (!users.TryGetValue(walking.UserId, out var bot)) return;
        if (walking.FollowRevision != 0 && bot.Follow?.Revision != walking.FollowRevision) return;
        if (walking.DeliveryRevision != 0 && bot.Delivery?.Revision != walking.DeliveryRevision) return;
        var node = nodes[walking.ActionId];
        charge(frame, 1, node.FurnitureId);
        if (walking.TargetStance == AvatarStance.Sit && (!furniture.TryGetValue(walking.TargetId, out var seat) ||
            seat.X != walking.TargetX || seat.Y != walking.TargetY || Math.Abs(seat.Z - walking.TargetBase) > 0.001))
            unsupported(node, "Moving or removing a seated walking destination requires separate conformance.");
        if (walking.FollowRevision == 0 && walking.DeliveryRevision == 0 && furniture.TryGetValue(walking.TargetId, out var target) &&
            target.X == walking.TargetX && target.Y == walking.TargetY &&
            (target.Stance != walking.TargetStance || Math.Abs(target.Z + target.Height - walking.TargetTop) > 0.001))
            unsupported(node, "A bot destination's geometry changed in place; further conformance is required.");
        if (walking.DeliveryRevision != 0) delivery_recipient(node, bot);
        var previous = walking.Path[walking.Step - 1];
        if (bot.X != previous.X || bot.Y != previous.Y)
            unsupported(node, "The walking bot moved outside its scheduled path.");
        if (walking.FollowRevision != 0 && bot.Follow is { Stopping: false })
        {
            var remaining = bot_route(node, frame, bot, new(walking.TargetX, walking.TargetY));
            if (remaining.Count < 2) unsupported(node, "The active follow destination is no longer reachable.");
            walking = walking with { Path = remaining, Step = 1 };
        }
        var next = walking.Path[walking.Step];
        int x = next.X, y = next.Y;
        int dx = x - bot.X, dy = y - bot.Y;
        bool seated_destination = walking.Step == walking.Path.Count - 1 && walking.TargetStance == AvatarStance.Sit;
        if (!bot_can_step(previous, next, bot.Id, seated_destination))
        {
            bool known_obstacle = avatar_obstruction(bot.Id, x, y, walking.BotGroup) ||
                walking.BotGroup is null && walking.FollowRevision == 0 && walking.DeliveryRevision == 0 &&
                closed_gate_obstruction(bot.Id, x, y);
            if (walking.Step < walking.Path.Count - 1 && known_obstacle)
            {
                var remaining = bot_route(node, frame, bot, new(walking.TargetX, walking.TargetY));
                if (remaining.Count < 2)
                    unsupported(node, "An obstacle blocks the route and the original destination is no longer reachable.");
                for (int index = 1; index < remaining.Count; index++)
                    if (Math.Abs(standing(remaining[index].X, remaining[index].Y) - standing(remaining[index - 1].X, remaining[index - 1].Y)) > 0.2001)
                        unsupported(node, "The rerouted bot path exceeds the verified step height.");
                walking = walking with { Path = remaining, Step = 1 };
                next = remaining[1];
                x = next.X; y = next.Y;
                dx = x - bot.X; dy = y - bot.Y;
                record("bot.reroute", node.FurnitureId, "An obstacle blocked the route; the original destination was retained.", [bot.Id]);
            }
            else
            {
                if (walking.Step == walking.Path.Count - 1 && known_obstacle)
                {
                    users[bot.Id] = bot with { NextStep = null };
                    if (dx != 0 && dy != 0)
                    {
                        var fallback = dx < 0 ? new Tile(x, bot.Y) : new Tile(bot.X, y);
                        if (!bot_can_step(previous, fallback, bot.Id))
                            unsupported(node, "The first blocked-destination fallback is unavailable; further conformance is required.");
                        if (Math.Abs(standing(fallback.X, fallback.Y) - standing(previous.X, previous.Y)) > 0.2001)
                            unsupported(node, "The blocked-destination fallback exceeds the verified bot step height.");
                        users[bot.Id] = users[bot.Id] with { Direction = dx < 0 ? 6 : dy < 0 ? 0 : 4 };
                        record("bot.step", node.FurnitureId, fallback.X + "," + fallback.Y, [bot.Id]);
                        move_user(users[bot.Id], fallback.X, fallback.Y, frame);
                    }
                    record("bot.walk_blocked", node.FurnitureId, "An obstacle blocked the destination before arrival.", [bot.Id]);
                    return;
                }
                unsupported(node, "A bot route became blocked while walking.");
            }
        }
        int heading = (dx, dy) switch
        {
            (0, -1) => 0, (1, -1) => 1, (1, 0) => 2, (1, 1) => 3,
            (0, 1) => 4, (-1, 1) => 5, (-1, 0) => 6, (-1, -1) => 7,
            _ => throw new InvalidOperationException("The bot walking step has no direction.")
        };
        users[bot.Id] = bot = bot with { NextStep = null, Direction = heading };
        record("bot.step", node.FurnitureId, x + "," + y, [bot.Id]);
        move_user(bot, x, y, frame, align_posture: seated_destination);
        if (walking.FollowRevision != 0 && users[bot.Id].Follow is { Stopping: true })
        {
            users[bot.Id] = users[bot.Id] with { Follow = null, NextStep = null };
            return;
        }
        if (users[bot.Id].X != x || users[bot.Id].Y != y || users[bot.Id].NextStep is not null || pending_teleport(bot.Id) ||
            queue.UnorderedItems.Any(entry => entry.Element.BotWalk?.UserId == bot.Id))
            unsupported(node, "A triggered action redirected the walking bot; interruption ordering requires conformance.");
        if (walking.Step == walking.Path.Count - 1)
        {
            if (walking.FollowRevision != 0)
                enqueue(checked(now + initial.BotTiming!.FollowArrivalMilliseconds), frame, bot_arrival: new(bot.Id, walking.FollowRevision));
            if (walking.DeliveryRevision != 0)
                enqueue(checked(now + initial.BotTiming!.DeliveryArrivalMilliseconds!.Value), frame, bot_handoff: new(bot.Id, walking.DeliveryRevision));
            return;
        }
        users[bot.Id] = users[bot.Id] with { NextStep = walking.Path[walking.Step + 1] };
        enqueue(checked(now + walking.StepMilliseconds), frame, bot_walk: walking with { Step = walking.Step + 1, StepsTaken = walking.StepsTaken + 1 });
    }

    private bool bot_can_step(Tile from, Tile to, long id, bool seated_destination = false)
    {
        if (!walkable(to.X, to.Y, id) || furniture.Values.Any(item => covers(item, to.X, to.Y) && item.Stance != AvatarStance.Stand &&
            !(seated_destination && item.Stance == AvatarStance.Sit && item.Width == 1 && item.Length == 1)))
            return false;
        return from.X == to.X || from.Y == to.Y || walkable(from.X, to.Y, id) || walkable(to.X, from.Y, id);
    }

    private IReadOnlyList<Tile> bot_route(WiredSimulationNode node, Frame frame, WiredSimulationUser bot, Tile goal,
        bool seated_destination = false)
    {
        var start = new Tile(bot.X, bot.Y);
        var costs = new Dictionary<Tile, int> { [start] = 0 };
        var previous = new Dictionary<Tile, Tile>();
        var visited = new HashSet<Tile>();
        var pending = new PriorityQueue<Tile, (int Score, int Estimate, long Order)>();
        long order = 0;
        int estimate(Tile tile)
        {
            int x = Math.Abs(tile.X - goal.X), y = Math.Abs(tile.Y - goal.Y);
            return checked(Math.Max(x, y) * 10 + Math.Min(x, y) * 4);
        }
        pending.Enqueue(start, (estimate(start), estimate(start), order++));
        while (pending.TryDequeue(out var at, out _))
        {
            charge(frame, 1, node.FurnitureId);
            if (!visited.Add(at)) continue;
            if (at == goal)
            {
                var path = new List<Tile> { at };
                while (at != start) { at = previous[at]; path.Add(at); }
                path.Reverse();
                return path.ToArray();
            }
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var next = new Tile(at.X + dx, at.Y + dy);
                if (!bot_can_step(at, next, bot.Id, seated_destination && next == goal)) continue;
                int cost = checked(costs[at] + (dx != 0 && dy != 0 ? 14 : 10));
                if (costs.TryGetValue(next, out int known) && cost >= known) continue;
                costs[next] = cost;
                previous[next] = at;
                int remaining = estimate(next);
                pending.Enqueue(next, (checked(cost + remaining), remaining, order++));
            }
        }
        return [];
    }
}
