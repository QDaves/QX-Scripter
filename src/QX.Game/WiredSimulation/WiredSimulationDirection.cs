namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private sealed record DirectionPlan(int X, int Y, int Direction, bool Moves, IReadOnlyList<long> Collisions);

    private void move_direction(WiredSimulationNode node, Frame frame)
    {
        var selected = direction_selection(node, frame);
        if (selected.Length == 0) return;
        var directions = node.MovementDirections?.ToDictionary() ?? [];
        foreach (long id in selected)
        {
            var item = furniture[id];
            var plan = direction_plan(node, item);
            foreach (long user in plan.Collisions) movement_collision(node, frame, item.Id, user);
            if (plan.Moves) move_item(item, plan.X, plan.Y, null, frame);
            directions[id] = plan.Direction;
        }
        nodes[node.FurnitureId] = node with { MovementDirections = directions };
    }

    private long[] direction_selection(WiredSimulationNode node, Frame frame)
    {
        var selected = fs(node, frame).ToArray();
        if (selected.Length == 0) return selected;
        if (selected.Length != 1)
            unsupported(node, "Directional movement of multiple selected furniture items requires separate ordering conformance.");
        if (siblings(node).Any(box => box.Name is "MovementPhysics" or "CarryUsers"))
            unsupported(node, "Directional movement combined with physics or carrying requires separate conformance.");
        if (node.MovementDirections is null)
            uncertain("direction.history_unknown", "Supply the effect's per-furniture MovementDirections. An empty map means a fresh effect; visual furniture rotation does not reveal retained movement direction.", node.FurnitureId);
        return selected;
    }

    private DirectionPlan direction_plan(WiredSimulationNode node, WiredSimulationFurniture item)
    {
        if (item.Width != 1 || item.Length != 1)
            unsupported(node, "Directional movement of furniture larger than one tile requires separate conformance.");
        int direction = node.MovementDirections?.GetValueOrDefault(item.Id, (int)node.Number("direction")) ?? (int)node.Number("direction");
        var attempted = new HashSet<int>();
        var collisions = new List<long>();
        while (attempted.Add(direction))
        {
            var offset = direction_offset(direction);
            int x = checked(item.X + offset.X), y = checked(item.Y + offset.Y);
            if (!fits(item, x, y))
                unsupported(node, "Directional movement against missing floor requires separate boundary conformance.");
            var occupants = users.Values.Where(user => user.X == x && user.Y == y).ToArray();
            if (occupants.Length > 1 || occupants.Any(user => user.IsBot || user.IsPet))
                unsupported(node, "Directional movement into multiple users, bots or pets requires separate collision conformance.");
            if (occupants.Length == 1)
            {
                collisions.Add(occupants[0].Id);
                if (node.Flag("block_on_user")) return new(item.X, item.Y, direction, false, collisions);
            }
            if (occupants.Length == 0 && !furniture.Values.Any(other => other.Id != item.Id && overlaps(item with { X = x, Y = y }, other) &&
                (!other.Stackable || other.ClassName == "exe_gate"))) return new(x, y, direction, true, collisions);
            int turn = (int)node.Number("blocked_turn");
            if (turn == 0) return new(item.X, item.Y, direction, false, collisions);
            if (turn == 6)
                uncertain("direction.random_turn", "The server's random blocked-turn choice cannot be predicted from a room snapshot.", node.FurnitureId);
            direction = turn switch
            {
                1 => (direction + 1) % 8,
                2 => (direction + 2) % 8,
                3 => (direction + 7) % 8,
                4 => (direction + 6) % 8,
                5 => (direction + 4) % 8,
                6 => (int)(next_random() % 8),
                _ => throw new InvalidOperationException("Unknown directional turn.")
            };
            if (attempted.Contains(direction))
                uncertain("direction.trapped", "All attempted turn directions are blocked; trapped movement history requires separate conformance.", node.FurnitureId);
        }
        return new(item.X, item.Y, direction, false, collisions);
    }
}
