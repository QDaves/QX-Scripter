using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private WiredSimulationNode? stack_at(long id)
    {
        if (!furniture.TryGetValue(id, out var item)) return null;
        return nodes.Values.Where(node => furniture.TryGetValue(node.FurnitureId, out var other) && item.X == other.X && item.Y == other.Y)
            .OrderBy(node => node.FurnitureId).FirstOrDefault();
    }

    private IReadOnlyList<WiredSimulationNode> stack_targets(WiredSimulationNode node, Frame frame) =>
        fs(node, frame).Select(stack_at).OfType<WiredSimulationNode>().DistinctBy(node => node.FurnitureId).ToArray();

    private Selection selection_for(WiredSimulationNode node, Frame parent, HashSet<long> path)
    {
        var boxes = siblings(node);
        long key = boxes[0].FurnitureId;
        var selections = parent.Selections ?? throw new InvalidOperationException("Stack selection context is missing.");
        if (selections.TryGetValue(key, out var existing)) return existing;
        if (path.Count >= options.MaximumSelectionDepth)
        {
            fail("simulation.selection_depth", "The configured remote-selection depth was reached; evaluation is incomplete.", node.FurnitureId);
            throw new SimulationFailure();
        }
        if (!path.Add(key)) unsupported(node, "Cyclic remote selection has no verified resolution rule.");
        try
        {
            var frame = parent with { Stack = key, SelectedFurniture = [], SelectedUsers = [] };
            foreach (var selector in boxes.Where(box => box.Category == WiredFormCategory.Selector))
                frame = select(selector, frame, path);
            frame = filter(boxes, frame);
            var result = new Selection(frame.SelectedFurniture, frame.SelectedUsers);
            selections.Add(key, result);
            return result;
        }
        finally { path.Remove(key); }
    }

    private Frame remote_selection(WiredSimulationNode node, Frame frame, HashSet<long> path)
    {
        var targets = stack_targets(node, frame);
        if (node.Number("random_stack_count") != 0)
            unsupported(node, "Random remote-stack selection requires server distribution and ordering verification.");
        long operation = node.Number("set_operation");
        if (operation is not (0 or 1)) unsupported(node, "Unknown remote set operation.");
        var selections = targets.Select(target => selection_for(target, frame, path)).ToArray();
        long[] combine(Func<Selection, long[]> values)
        {
            if (selections.Length == 0) return [];
            IEnumerable<long> result = values(selections[0]);
            foreach (var selection in selections.Skip(1))
                result = operation == 0 ? result.Union(values(selection)) : result.Intersect(values(selection));
            return result.Distinct().Order().ToArray();
        }
        long[] merge(long[] previous, long[] result, IEnumerable<long> universe)
        {
            if (node.Invert) result = universe.Except(result).ToArray();
            return (node.Filter ? previous.Intersect(result) : previous.Union(result)).Order().ToArray();
        }
        var selected = frame with
        {
            SelectedFurniture = merge(frame.SelectedFurniture, combine(value => value.Furniture), furniture.Keys),
            SelectedUsers = merge(frame.SelectedUsers, combine(value => value.Users), users.Keys)
        };
        record("selector.remote", node.FurnitureId, "Remote selections combined.", selected.SelectedFurniture);
        return selected;
    }

    private void prepare_calls(IReadOnlyList<WiredSimulationNode> actions_here, Frame frame)
    {
        var visited = new HashSet<long>();
        var pending = new Queue<(IReadOnlyList<WiredSimulationNode> Actions, Frame Frame)>();
        pending.Enqueue((actions_here, frame));
        while (pending.TryDequeue(out var entry))
        foreach (var action in entry.Actions.Where(node => node.Name == "CallAnotherStack"))
        foreach (var target in stack_targets(action, entry.Frame))
        {
            if (!visited.Add(target.FurnitureId)) continue;
            charge(entry.Frame, 1, action.FurnitureId);
            var selection = selection_for(target, entry.Frame, []);
            var child = entry.Frame with { Stack = target.FurnitureId, SelectedFurniture = selection.Furniture, SelectedUsers = selection.Users };
            var actions = siblings(target).Where(node => node.Category == WiredFormCategory.Action && !negative_action(node)).ToArray();
            pending.Enqueue((actions, child));
        }
    }
}
