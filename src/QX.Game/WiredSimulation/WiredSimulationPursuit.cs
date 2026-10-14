namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void pursue(WiredSimulationNode node, Frame frame, bool flee)
    {
        var selected = fs(node, frame).ToArray();
        if (selected.Length == 0) return;
        if (selected.Length != 1)
            unsupported(node, "Pursuit with multiple selected furniture items requires separate ordering and collision conformance.");
        if (users.Count != 1 || users.Values.Any(user => user.IsBot || user.IsPet))
            unsupported(node, "Pursuit target selection with multiple users, bots or pets needs separate server conformance.");
        var addons = siblings(node).ToArray();
        if (addons.Any(box => box.Name == "CarryUsers"))
            unsupported(node, "Pursuit combined with carrying users requires separate server conformance.");
        var physics = addons.SingleOrDefault(box => box.Name == "MovementPhysics");
        var target = users.Values.Single();
        foreach (long id in selected)
        {
            var item = furniture[id];
            if (item.Width != 1 || item.Length != 1)
                unsupported(node, "Pursuit footprints larger than one tile require separate collision conformance.");
            if (target.X == item.X && target.Y == item.Y)
                unsupported(node, "Pursuit starting on the target's tile has not been verified.");
            if (Math.Abs(target.X - item.X) + Math.Abs(target.Y - item.Y) == 1)
            {
                if (physics?.Flag("move_through_users") == true && physics.Flag("block_by_furni") && pursuit_obstacle(item, target.X, target.Y))
                    unsupported(node, "Pursuit collision with simultaneous furniture blocking and user overlap requires separate server conformance.");
                movement_collision(node, frame, item.Id, target.Id);
                if (physics?.Flag("move_through_users") == true)
                {
                    move_item(item, target.X, target.Y, null, frame);
                    if (flee) furniture[item.Id] = furniture[item.Id] with { FleeDirection = null };
                }
                continue;
            }
            int dx, dy;
            if ((target.X == item.X || target.Y == item.Y) && Math.Max(Math.Abs(target.X - item.X), Math.Abs(target.Y - item.Y)) <= 3)
            {
                int sign = flee ? -1 : 1;
                dx = Math.Sign(target.X - item.X) * sign;
                dy = Math.Sign(target.Y - item.Y) * sign;
            }
            else if (flee && item.FleeDirection is { } retained)
            {
                if (retained is not (0 or 2 or 4 or 6))
                    unsupported(node, "Retained fleeing direction must be a cardinal direction.");
                (dx, dy) = direction_offset(retained);
            }
            else
            {
                uncertain("pursuit.random_direction", "A diagonal or distant target does not determine the next cardinal step. Server choices and retained movement history are not available in this snapshot.", node.FurnitureId);
                (dx, dy) = direction_offset((int)(next_random() % 4) * 2);
            }
            int x = checked(item.X + dx), y = checked(item.Y + dy);
            if (physics?.Flag("block_by_furni") == true && pursuit_obstacle(item, x, y))
            {
                uncertain("pursuit.obstacle_direction", "Pursuit can change direction around a blocked destination; the alternate direction is not established by this snapshot.", node.FurnitureId);
                var alternatives = new[] { 0, 2, 4, 6 }.Select(direction_offset)
                    .Where(offset => fits(item, item.X + offset.X, item.Y + offset.Y) && !pursuit_obstacle(item, item.X + offset.X, item.Y + offset.Y)).ToArray();
                if (alternatives.Length == 0)
                    unsupported(node, "Pursuit with every adjacent destination blocked requires separate server conformance.");
                (dx, dy) = alternatives[(int)(next_random() % alternatives.Length)];
                x = checked(item.X + dx); y = checked(item.Y + dy);
            }
            if (x == target.X && y == target.Y)
            {
                movement_collision(node, frame, item.Id, target.Id);
                continue;
            }
            move_item(item, x, y, null, frame);
            if (flee && furniture[item.Id] is var moved && moved.X == x && moved.Y == y)
                furniture[item.Id] = moved with { FleeDirection = dx > 0 ? 2 : dx < 0 ? 6 : dy > 0 ? 4 : 0 };
        }
    }

    private bool pursuit_obstacle(WiredSimulationFurniture item, int x, int y) =>
        furniture.Values.Any(other => other.Id != item.Id && overlaps(item with { X = x, Y = y }, other));

    private void movement_collision(WiredSimulationNode node, Frame frame, long item, long user)
    {
        record("collision", node.FurnitureId, "Movement reached an occupied user destination.", [item, user]);
        enqueue(now, frame with { Event = new(WiredSimulationEventKind.AvatarCaught, now, user, item, Users: [user]) }, external: true);
    }
}
