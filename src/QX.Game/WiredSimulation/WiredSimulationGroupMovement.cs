namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void move_group(WiredSimulationNode node, Frame frame, long[] objects)
    {
        foreach (var plan in group_plan(node, frame, objects))
            move_item(plan.Item, plan.X, plan.Y, null, frame, relative: true, group_movement: true);
    }

    private (WiredSimulationFurniture Item, int X, int Y)[] group_plan(WiredSimulationNode node, Frame frame, long[] objects)
    {
        if (objects.Length == 0) return [];
        var sources = objects.Select(id => furniture[id]).ToArray();
        var occupants = users.Values.Where(user => sources.Any(item => covers(item, user.X, user.Y))).ToArray();
        if (occupants.Length > 1 || occupants.Any(user => user.IsBot && sources.Length != 1 || user.IsPet || user.NextStep is not null || user.Stance == Qx.Model.AvatarStance.Lay))
            unsupported(node, "Grouped movement with multiple occupants, bots on multiple selected furniture, pets, walking or lying users requires separate conformance.");
        if (occupants.Length > 0 && sources.Any(item => sources.Any(other => other.Id != item.Id && overlaps(item, other))))
            unsupported(node, "Grouped movement from overlapping occupied members requires separate carry conformance.");
        var carry = siblings(node).SingleOrDefault(addon => addon.Name == "CarryUsers");
        if (carry is not null && carry.Number("carry_mode") is not (0 or 1))
            unsupported(carry, "Unknown carry mode.");
        if (occupants.Length > 0 && siblings(node).Any(addon => addon.Name == "Projectile"))
            unsupported(node, "Grouped projectiles carrying or moving away from users require separate conformance.");
        int x, y;
        if (node.Flag("target_is_user"))
        {
            var targets = us(node, frame);
            if (targets.Length == 0) return [];
            if (targets.Length > 1) unsupported(node, "Grouped movement with multiple target users requires separate conformance.");
            var target = users[targets[0]];
            x = target.X; y = target.Y;
        }
        else
        {
            var targets = fs(node, frame, 1);
            if (targets.Length == 0) return [];
            if (targets.Length > 1) unsupported(node, "Grouped movement with multiple target furniture requires separate conformance.");
            var target = furniture[targets[0]];
            x = target.X; y = target.Y;
        }
        int dx = checked(x + (int)node.Number("x_offset") - sources.Min(item => item.X));
        int dy = checked(y + (int)node.Number("y_offset") - sources.Min(item => item.Y));
        if (dx == 0 && dy == 0)
            return [];
        bool independent = siblings(node).Any(addon => addon.Name == "MovementPhysics" &&
            addon.Flag("keep_altitude") && addon.Flag("move_through_furni") && !addon.Flag("block_by_furni"));
        foreach (var item in sources)
            if ((!independent || occupants.Length > 0) && sources.Any(other => other.Id != item.Id && overlaps(item with { X = item.X + dx, Y = item.Y + dy }, other)))
                unsupported(node, "Overlapping group source and destination tiles can produce different heights or blocked members from the same captured state. Saved selection order and movement packet order do not establish execution order. Verified order-independent movement requires KeepAltitude and MoveThroughFurni with BlockByFurni disabled.");
        return sources.OrderByDescending(item => item.Id)
            .Select(item => (item, checked(item.X + dx), checked(item.Y + dy))).ToArray();
    }
}
