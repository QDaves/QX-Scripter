using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private bool can_perform_move(WiredSimulationNode node, Frame frame)
    {
        var boxes = siblings(node);
        if (boxes.Any(box => box.Name is "Random" or "Unseen"))
            unsupported(node, "Movement preflight with random effect selection requires separate conformance.");
        var plans = new List<(WiredSimulationFurniture Item, int X, int Y, bool Allowed)>();
        foreach (var action in boxes.Where(box => box.Category == WiredFormCategory.Action))
        {
            if (action.Name is "ToggleFurniState" or "Log") continue;
            if (action.Name == "MoveToDirection")
            {
                foreach (long id in direction_selection(action, frame))
                {
                    charge(frame, 1, node.FurnitureId);
                    var item = furniture[id];
                    var plan = direction_plan(action, item);
                    plans.Add((item, plan.X, plan.Y, plan.Moves));
                }
            }
            else if (action.Name == "RelativeFurniMove")
            {
                var selected = fs(action, frame);
                if (selected.Length > 1) unsupported(node, "Relative movement preflight for multiple selected furniture requires separate ordering conformance.");
                foreach (long id in selected)
                {
                    charge(frame, 1, node.FurnitureId);
                    var item = furniture[id];
                    int x = checked(item.X + (int)action.Number("x_offset"));
                    int y = checked(item.Y + (int)action.Number("y_offset"));
                    plans.Add((item, x, y, relative_preflight(action, item, x, y)));
                }
            }
            else if (action.Name == "MoveAsGroup")
            {
                foreach (var plan in group_plan(action, frame, fs(action, frame)))
                {
                    charge(frame, 1, node.FurnitureId);
                    plans.Add((plan.Item, plan.X, plan.Y, relative_preflight(action, plan.Item, plan.X, plan.Y, allow_large: true)));
                }
            }
            else unsupported(node, "Movement preflight currently covers directional, relative and disjoint group effects with state-toggle and log companions.");
        }
        for (int i = 0; i < plans.Count; i++)
            for (int j = i + 1; j < plans.Count; j++)
            {
                var first = plans[i];
                var second = plans[j];
                if (first.Item.Id == second.Item.Id ||
                    first.Allowed && (overlaps(first.Item with { X = first.X, Y = first.Y }, second.Item) ||
                        second.Allowed && overlaps(first.Item with { X = first.X, Y = first.Y }, second.Item with { X = second.X, Y = second.Y })) ||
                    second.Allowed && overlaps(second.Item with { X = second.X, Y = second.Y }, first.Item))
                    unsupported(node, "Movement preflight with shared targets or intersecting paths requires separate conformance.");
            }
        return plans.All(entry => entry.Allowed);
    }

    private bool relative_preflight(WiredSimulationNode node, WiredSimulationFurniture item, int x, int y, bool allow_large = false)
    {
        if (!allow_large && (item.Width != 1 || item.Length != 1))
            unsupported(node, "Relative movement preflight for larger furniture requires separate conformance.");
        var boxes = siblings(node);
        var carry = boxes.SingleOrDefault(box => box.Name == "CarryUsers");
        if (carry is not null)
        {
            if (carry.Number("carry_mode") is not (0 or 1)) unsupported(carry, "Unknown carry mode.");
            var riders = users.Values.Where(user => carry.Number("carry_mode") == 1
                ? covers(item, user.X, user.Y) : on_furniture(user, item)).ToArray();
            if (riders.Length > 1 || riders.Any(user => user.IsBot || user.IsPet))
                unsupported(node, "Relative movement preflight carrying multiple users, bots or pets requires separate conformance.");
        }
        if (!fits(item, x, y)) return false;
        if (item.X == x && item.Y == y) return true;
        var destination = item with { X = x, Y = y };
        var occupants = furniture.Values.Where(other => other.Id != item.Id && overlaps(destination, other)).ToArray();
        var physics = boxes.SingleOrDefault(box => box.Name == "MovementPhysics");
        if (relative_stack_blocked(item, x, y, physics)) return false;
        if (physics?.Flag("block_by_furni") == true && occupants.Length > 0) return false;
        var people = users.Values.Where(user => covers(destination, user.X, user.Y)).ToArray();
        if (people.Length > 1 || people.Any(user => user.IsBot || user.IsPet))
            unsupported(node, "Relative movement preflight against multiple users, bots or pets requires separate conformance.");
        return people.Length == 0 || physics?.Flag("move_through_users") == true;
    }

    private bool relative_stack_blocked(WiredSimulationFurniture item, int x, int y, WiredSimulationNode? physics)
    {
        if (item.X == x && item.Y == y || physics?.Flag("move_through_furni") == true) return false;
        var destination = item with { X = x, Y = y };
        return furniture.Values.Any(other => other.Id != item.Id && overlaps(destination, other) &&
            (!other.Stackable || other.ClassName == "exe_gate"));
    }
}
