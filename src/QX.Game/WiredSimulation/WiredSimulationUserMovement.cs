using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void move_users(WiredSimulationNode node, Frame frame, long[] people)
    {
        if (siblings(node).Any(addon => addon.Name == "JumpStrength") && !siblings(node).Any(addon => addon.Name == "NoMoveAnimation"))
            unsupported(node, "Step-and-rotate user movement curves require separate conformance.");
        if (people.Length > 1)
            unsupported(node, "Multiple moving users require selection-order and occupancy conformance.");
        int direction = (int)node.Number("movement", -1);
        int rotation = (int)node.Number("rotation", -1);
        if (direction is < -1 or > 7 || rotation is < -1 or > 10 or 8)
            unsupported(node, "Unknown user movement or rotation option.");
        foreach (long id in people)
        {
            var user = users[id];
            if (user.IsPet || user.NextStep is not null || user.Stance == AvatarStance.Lay)
                unsupported(node, "Moving pets, walking or lying users requires separate conformance.");
            if (user.IsBot && !idle_standing_bot(user))
                unsupported(node, "Generic bot movement requires an idle standing bot without an effect.");
            if (direction >= 0)
            {
                var offset = direction_offset(direction);
                int x = user.X + offset.X, y = user.Y + offset.Y;
                if (walkable(x, y, id))
                {
                    if (user.IsBot && furniture.Values.Any(item => covers(item, x, y) && item.Stance != AvatarStance.Stand))
                        unsupported(node, "Generic bot steps onto seated or lying furniture require separate conformance.");
                    move_user(user, x, y, frame);
                    effects.Add(new(now, "MovementAnimation", node.FurnitureId, [id], Value: movement_duration(siblings(node))));
                    user = users[id];
                    if (user.Stance == AvatarStance.Sit)
                    {
                        var seat = furniture.Values.Where(item => covers(item, x, y))
                            .OrderByDescending(item => item.Z + item.Height).ThenBy(item => item.Id).First();
                        users[id] = user = user with { Direction = seat.Direction };
                    }
                }
            }
            if (rotation >= 0 && user.Stance == AvatarStance.Stand)
                users[id] = user with { Direction = rotation < 8 ? rotation : (user.Direction + (rotation == 9 ? 1 : 7)) % 8 };
        }
    }

    private bool idle_standing_bot(WiredSimulationUser user) =>
        user.Stance == AvatarStance.Stand && !user.Frozen && user.Effect == 0 && user.NextStep is null &&
        user.Follow is null && user.Delivery is null && !pending_teleport(user.Id) &&
        !queue.UnorderedItems.Any(entry => entry.Element.BotWalk?.UserId == user.Id);

    private static long movement_duration(IEnumerable<WiredSimulationNode> addons) =>
        addons.Any(node => node.Name == "NoMoveAnimation") ? 0 :
        addons.SingleOrDefault(node => node.Name == "AnimationTime")?.Number("animation_milliseconds") ?? 500;
}
