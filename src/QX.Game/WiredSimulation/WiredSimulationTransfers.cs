using Qx.Model;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private bool pending_teleport(long id, bool arrivals_only = false) => queue.UnorderedItems.Any(entry =>
        entry.Element.Teleport is { } transfer && transfer.UserId == id && (!arrivals_only || !transfer.Restore) ||
        entry.Element.RoomDeparture is { } departure && departure.UserId == id);

    private void transfer_users(WiredSimulationNode node, Frame frame, long[] objects, long[] people)
    {
        if (objects.Length == 0 || people.Length == 0) return;
        if (objects.Length != 1 || people.Length != 1)
            unsupported(node, "Multiple teleport destinations or users require selection-order and occupancy conformance.");
        var user = users[people[0]];
        var target = furniture[objects[0]];
        if (user.IsPet || user.IsBot && node.Name != "MoveUserToFurni")
            unsupported(node, "Classic transfers of bots and transfers of pets require separate conformance.");
        if (user.IsBot && (!idle_standing_bot(user) || node.Number("walking_mode") != 2 ||
            target.Width != 1 || target.Length != 1 || target.Stance == AvatarStance.Lay))
            unsupported(node, "Generic bot transfers require an idle standing bot, stop-walking mode and a one-tile standing or seated destination.");
        if (node.Name != "MoveUserToFurni" && (target.Width != 1 || target.Length != 1))
            unsupported(node, "Classic teleport targets with larger footprints require separate conformance.");
        if (node.Name == "MoveUserToFurni")
        {
            if (pending_teleport(user.Id, arrivals_only: true))
                unsupported(node, "A direct transfer before a pending classic arrival requires separate conformance.");
            long mode = node.Number("walking_mode");
            if (mode is < 0 or > 2) unsupported(node, "Unknown transfer walking mode.");
            if (user.NextStep is not null)
                unsupported(node, "A captured next step is not the final walking destination. Transfers during walking require a verified continuation model.");
            if (!walkable(target.X, target.Y, user.Id))
            { record("user.transfer_blocked", node.FurnitureId, "The destination is not walkable.", [user.Id]); return; }
            var addons = siblings(node);
            bool curved = addons.Any(box => box.Name == "JumpStrength") && !addons.Any(box => box.Name == "NoMoveAnimation");
            if (curved && mode != 2) unsupported(node, "Curved transfers with walking continuation require separate conformance.");
            int? curve = movement_curve(addons, frame);
            move_user(user, target.X, target.Y, frame, align_posture: true);
            long duration = movement_duration(addons);
            effects.Add(new(now, "MovementAnimation", node.FurnitureId, [user.Id], Value: duration,
                Movement: curved ? user_motion(user, users[user.Id], duration, curve) : null));
            return;
        }
        if (pending_teleport(user.Id)) unsupported(node, "Overlapping classic teleports require separate conformance.");
        if (user.NextStep is not null) unsupported(node, "Classic teleport during walking requires separate path-cancellation conformance.");
        if (user.Frozen && user.Freeze is null) unsupported(node, "Classic teleport of a captured frozen user requires freeze cancellation metadata.");
        var timings = initial.TeleportTimings;
        if (timings is null) unsupported(node, "Classic teleport requires calibrated Room.TeleportTimings; transport observations do not establish exact server delays.");
        var timing = node.Flag("fast_teleport") ? timings!.Fast : timings!.Normal;
        if (timing is null || timing.ArrivalMilliseconds <= 0 || timing.EffectMilliseconds <= timing.ArrivalMilliseconds || timing.EffectId < 0)
            unsupported(node, "Invalid classic teleport timing profile.");
        if (siblings(node).Any(box => box.Name is "AnimationTime" or "NoMoveAnimation"))
            unsupported(node, "Animation modifiers with classic teleport require separate conformance.");
        int previous = user.Freeze is { CancelOnTeleport: true } frozen ? frozen.PreviousEffect : user.Effect;
        var transfer = new Teleport(user.Id, target.Id, target.X, target.Y, previous);
        users[user.Id] = user with { Effect = timing!.EffectId };
        effects.Add(new(now, "TeleportAnimation", node.FurnitureId, [user.Id], Value: timing.EffectMilliseconds));
        enqueue(checked(now + timing.ArrivalMilliseconds), frame, teleport: transfer);
        enqueue(checked(now + timing.EffectMilliseconds), frame, teleport: transfer with { Restore = true });
    }

    private void finish_teleport(Teleport transfer, Frame frame)
    {
        if (!users.TryGetValue(transfer.UserId, out var user)) return;
        if (transfer.Restore)
        {
            users[user.Id] = user with { Effect = transfer.PreviousEffect };
            return;
        }
        if (!furniture.ContainsKey(transfer.TargetId))
        {
            uncertain("teleport.destination_removed", "The classic teleport destination disappeared before arrival.", frame.Stack ?? 0);
            return;
        }
        if (transfer.Bot)
        {
            var target = furniture[transfer.TargetId];
            if (target.X != transfer.TargetX || target.Y != transfer.TargetY ||
                target.Stance != transfer.TargetStance || Math.Abs(target.Z + target.Height - transfer.TargetTop) > 0.001 ||
                !walkable(transfer.TargetX, transfer.TargetY, user.Id, bot_teleport: true))
            {
                uncertain("bot.destination_changed", "Bot teleport retargeting or fallback requires separate conformance.", frame.Stack ?? 0);
                return;
            }
        }
        if (walkable(transfer.TargetX, transfer.TargetY, user.Id, bot_teleport: transfer.Bot))
        {
            teleport_arrival(user, transfer.TargetX, transfer.TargetY, frame, bot_transfer: transfer.Bot);
            return;
        }
        foreach (int direction in new[] { 0, 1, 2, 3 })
        {
            var offset = direction_offset(direction);
            int x = transfer.TargetX + offset.X, y = transfer.TargetY + offset.Y;
            if (!walkable(x, y, user.Id)) continue;
            teleport_arrival(user, x, y, frame);
            return;
        }
        uncertain("teleport.fallback", "The verified nearby fallback destinations are blocked; the remaining search order requires conformance.", frame.Stack ?? 0);
    }

    private void teleport_arrival(WiredSimulationUser user, int x, int y, Frame frame, bool bot_transfer = false)
    {
        if (user.Freeze is { CancelOnTeleport: true })
        {
            user = user with { Frozen = false, Freeze = null };
            users[user.Id] = user;
        }
        move_user(user, x, y, frame, align_posture: bot_transfer,
            changed_contacts_only: bot_transfer || user.X == x && user.Y == y, refresh_same_tile: !bot_transfer);
    }
}
