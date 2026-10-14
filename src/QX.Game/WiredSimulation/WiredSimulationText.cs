using System.Globalization;
using System.Text.RegularExpressions;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private string output_text(WiredSimulationNode node, Frame frame, string? template = null)
    {
        var placeholders = siblings(node).Where(box => box.Name is "UsernamePlaceholder" or "VariablePlaceholder" or "FurniNamePlaceholder")
            .GroupBy(box => box.Text("placeholder_name"), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var globals = nodes.Values.Where(box => box.Name == "GlobalPlaceholder" && furniture.ContainsKey(box.FurnitureId))
            .GroupBy(box => box.Text("placeholder_name"), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        string expand(string text) => Regex.Replace(text, @"\$\(([^()]*)\)", match =>
        {
            string name = match.Groups[1].Value;
            globals.TryGetValue(name, out var global);
            if (global is not null && (placeholders.ContainsKey(name) || frame.TextValues?.ContainsKey(name) == true || name == "trigger_text"))
                unsupported(node, "Global and execution-local placeholder name collisions require separate server conformance.");
            if (frame.TextValues?.TryGetValue(name, out var supplied) == true) return supplied;
            if (name == "trigger_text" && frame.Event.Kind == WiredSimulationEventKind.Chat)
                return frame.Event.Text ?? "";
            if (name == "trigger_text" && frame.Event.Kind == WiredSimulationEventKind.LinkMessage)
                return frame.Event.Link?.Message ?? "";
            if (!placeholders.TryGetValue(name, out var matching))
            {
                if (global is null) return match.Value;
                matching = global;
            }
            if (matching.Length != 1) unsupported(node, "Several text add-ons define the same placeholder; precedence requires conformance.");
            if (values.TryGetValue(name, out var cached)) return cached ?? match.Value;
            if (!visiting.Add(name)) unsupported(node, "Cyclic text placeholder expansion requires separate conformance.");
            if (visiting.Count > 64) unsupported(node, "Nested text exceeds the local expansion depth; this is not a hotel limit.");
            string? value = matching[0].Name == "GlobalPlaceholder" ? global_placeholder(matching[0], []) : placeholder_text(matching[0], frame);
            values[name] = value = value is null ? null : expand(value);
            visiting.Remove(name);
            return value ?? match.Value;
        }, RegexOptions.CultureInvariant);
        return expand(template ?? node.Text("message"));
    }

    private string? placeholder_text(WiredSimulationNode node, Frame frame)
    {
        if (node.Flag("show_multiple") && node.Text("delimiter").Length == 0)
            unsupported(node, "Multiple-value placeholders require a nonempty delimiter; the server rejects the empty configuration.");
        string[] values = node.Name switch
        {
            "UsernamePlaceholder" => us(node, frame, include_departed: true).OrderByDescending(id => id)
                .Select(id => context_user(id, frame)!.Name).ToArray(),
            "FurniNamePlaceholder" => fs(node, frame).OrderByDescending(id => id).Select(id => furniture_name(node, furniture[id])).ToArray(),
            "VariablePlaceholder" => targets(node, frame, "target", include_departed: true).OrderByDescending(target => target.Id).Select(target => variable_text(node, target.Domain, target.Id, frame)).ToArray(),
            _ => throw new InvalidOperationException("Unknown text placeholder.")
        };
        if (values.Length == 0) return node.Flag("show_multiple") ? "" : null;
        if (node.Flag("show_multiple"))
        {
            if (values.Distinct(StringComparer.Ordinal).Skip(1).Any())
                note(WiredSimulationSeverity.Uncertain, "text.placeholder_order", "Multiple-value text ordering is not recoverable from holder IDs or selection order; observed user order changed after room entry.", node.FurnitureId);
            return string.Join(node.Text("delimiter"), values);
        }
        if (values.Length == 1) return values[0];
        note(WiredSimulationSeverity.Uncertain, "text.placeholder_choice", "Single-value output with several selected sources requires a verified selection rule.", node.FurnitureId);
        return values[(int)(next_random() % values.Length)];
    }

    private string furniture_name(WiredSimulationNode node, WiredSimulationFurniture item)
    {
        if (string.IsNullOrEmpty(item.Name)) unsupported(node, "Furniture-name substitution requires the localized Furniture.Name metadata.");
        return item.Name!;
    }

    private string variable_text(WiredSimulationNode node, WiredSourceDomain domain, long id, Frame frame)
    {
        string variable = node.Text("variable");
        var current = value(domain, id, variable, frame);
        if (current is null) return "null";
        if (!node.Flag("text_mode")) return current.Value.ToString(CultureInfo.InvariantCulture);
        string resolved = variables.TryGetValue(variable, out var original) &&
            (original.Availability == 21 || original.DefinitionFurnitureId is { } box && nodes.TryGetValue(box, out var definition_node) && definition_node.Name == "EchoVariable")
            ? variable : resolve_reference(variable, frame);
        if (!variables.TryGetValue(resolved, out var definition) || definition.Text is null)
            unsupported(node, "Text-mode substitution requires resolved text connector metadata or its bound connector form; native key readouts may be saturated. An empty mapping represents a known absence of entries.");
        return definition!.Text!.GetValueOrDefault(current.Value, definition.TextFallback ?? current.Value.ToString(CultureInfo.InvariantCulture));
    }
}
