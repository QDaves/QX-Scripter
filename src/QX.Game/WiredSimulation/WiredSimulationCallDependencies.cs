using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private long action_delay(WiredSimulationNode node) =>
        node.Delay < 0 ? -(long)node.Delay : node.Delay * (long)options.PulseMilliseconds;

    private string? furniture_dependency(string reference, Frame frame)
    {
        string name = variable_name(resolve_reference(reference, frame));
        return name == "@state" ? name : recolor_component(name) >= 0 ? "~recolorable_furni.color.rgb" : null;
    }

    private IEnumerable<(long Id, string Variable)> furniture_writes(WiredSimulationNode node, Frame frame)
    {
        if (node.Name != "ChangeVariable" || node.Number("destination_target") != (int)WiredSourceDomain.Furniture ||
            furniture_dependency(node.Text("destination"), frame) is not { } name) yield break;
        foreach (long id in fs(node, frame)) yield return (id, name);
    }

    private IEnumerable<(long Id, string Variable)> furniture_accesses(WiredSimulationNode node, Frame frame)
    {
        foreach (var target in furniture_writes(node, frame)) yield return target;
        if (node.Name == "PlaceFurni")
        {
            foreach (long id in fs(node, frame))
            {
                if (furniture_source(node) != WiredFurnitureSource.Snapshot) yield return (id, "@state");
                if (furniture[id].RecolorRgb.HasValue) yield return (id, "~recolorable_furni.color.rgb");
            }
        }
        if (node.Name is not ("Log" or "Chat" or "BotTalk" or "BotTalkDirectToAvtr")) yield break;
        foreach (var placeholder in siblings(node).Where(box => box.Name == "VariablePlaceholder" &&
            box.Number("target") == (int)WiredSourceDomain.Furniture &&
            node.Text("message").Contains("$(" + box.Text("placeholder_name") + ")", StringComparison.Ordinal)))
        {
            if (furniture_dependency(placeholder.Text("variable"), frame) is not { } name) continue;
            foreach (long id in fs(placeholder, frame)) yield return (id, name);
        }
    }

    private void validate_call_dependencies(IReadOnlyList<WiredSimulationNode> actions_here, Frame frame)
    {
        if (!actions_here.Any(node => node.Name == "CallAnotherStack")) return;
        var writes = actions_here.SelectMany(node => furniture_writes(node, frame)
            .Select(target => (Target: target, At: action_delay(node)))).ToHashSet();
        if (writes.Count == 0) return;
        long last_write = writes.Max(value => value.At);
        var visited = new HashSet<(long Stack, long At)>();
        var pending = new Queue<(IReadOnlyList<WiredSimulationNode> Actions, Frame Frame, long At)>();
        pending.Enqueue((actions_here, frame, 0));
        while (pending.TryDequeue(out var entry))
        foreach (var call in entry.Actions.Where(node => node.Name == "CallAnotherStack"))
        foreach (var target in stack_targets(call, entry.Frame))
        {
            long at = checked(entry.At + action_delay(call));
            if (at > last_write || !visited.Add((target.FurnitureId, at))) continue;
            if (visited.Count > options.MaximumEvents)
                unsupported(call, "Call dependency analysis exceeded the configured event budget; ordering has not been established.");
            var selected = selection_for(target, entry.Frame, []);
            var child = entry.Frame with { Stack = target.FurnitureId, SelectedFurniture = selected.Furniture, SelectedUsers = selected.Users };
            var actions = siblings(target).Where(node => node.Category == WiredFormCategory.Action && !negative_action(node)).ToArray();
            foreach (var action in actions)
            foreach (var access in furniture_accesses(action, child))
            {
                if (!writes.Contains((access, checked(at + action_delay(action))))) continue;
                uncertain("order.call_dependency", $"A caller and a called stack access furniture {access.Id} variable {access.Variable} at the same scheduled time. Server controls show that inline child execution does not reliably predict these reads and writes. Separate the dependent phases with an explicit delay; this simulation cannot certify their ordering.", call.FurnitureId);
                return;
            }
            pending.Enqueue((actions, child, at));
        }
    }
}
