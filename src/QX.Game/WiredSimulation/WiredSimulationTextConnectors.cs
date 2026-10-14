using System.Globalization;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void bind_text_connectors()
    {
        var connectors_by_tile = nodes.Values.Where(node => node.Name == "VariableTextConverter" && furniture.ContainsKey(node.FurnitureId))
            .GroupBy(node => (furniture[node.FurnitureId].X, furniture[node.FurnitureId].Y))
            .ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var variable in variables.Values.ToArray())
        {
            if (variable.DefinitionFurnitureId is not { } id || !nodes.TryGetValue(id, out var definition) ||
                definition.Category != WiredFormCategory.Variable || !furniture.TryGetValue(id, out var item)) continue;
            bool echo = definition.Name == "EchoVariable";
            if (!connectors_by_tile.TryGetValue((item.X, item.Y), out var connectors))
            {
                if (echo) variables[variable.Id] = variable with { Text = new Dictionary<long, string>(), TextFallback = null };
                continue;
            }
            WiredSourceDomain? domain = definition.Name switch
            {
                "Furniture" => WiredSourceDomain.Furniture,
                "UserVariable" => WiredSourceDomain.User,
                "GlobalVariable" => WiredSourceDomain.Global,
                "ContextVariable" => WiredSourceDomain.Context,
                "EchoVariable" => variable.Domain,
                _ => null
            };
            if (domain != variable.Domain || !echo && variable.Reference is not null || variable.PointerAccessor is not null || variable.Derived is not null)
            {
                note(WiredSimulationSeverity.Unsupported, "text.connector_binding", "Text connectors require an ordinary or Echo definition with a matching domain; other indirect variables require separate conformance.", id);
                status = WiredSimulationStatus.Faulted;
                continue;
            }
            var invalid = connectors.FirstOrDefault(connector => connector.Text("text_mapping")
                .Any(character => char.IsSurrogate(character) || character < ' ' && character is not ('\r' or '\n' or '\t')));
            if (invalid is not null)
            {
                if (connectors.Length == 1 && variable.Text is not null) continue;
                note(WiredSimulationSeverity.Unsupported, "text.mapping_characters", "Raw text mappings containing surrogate or unverified control characters require an accepted server readback.", invalid.FurnitureId);
                status = WiredSimulationStatus.Faulted;
                continue;
            }
            var values = new Dictionary<long, string>();
            string? fallback = null;
            bool conflict = false;
            foreach (var connector in connectors)
            {
                var mapping = parse_text_mapping(connector.Text("text_mapping"));
                foreach (var entry in mapping.Values)
                {
                    if (values.TryGetValue(entry.Key, out string? previous) && previous != entry.Value) conflict = true;
                    values[entry.Key] = entry.Value;
                }
                if (mapping.Fallback is not null)
                {
                    if (fallback is not null && fallback != mapping.Fallback) conflict = true;
                    fallback = mapping.Fallback;
                }
            }
            if (conflict)
            {
                note(WiredSimulationSeverity.Unsupported, "text.connector_conflict", "Same-tile text connectors disagree on a key or fallback. Server precedence is not reliably determined by height, numeric ID or save order; captured variable text can disagree with output. Use a single connector or consistent mappings.", id);
                status = WiredSimulationStatus.Faulted;
                continue;
            }
            variables[variable.Id] = variable with { Text = values, TextFallback = fallback };
        }
    }

    private static (IReadOnlyDictionary<long, string> Values, string? Fallback) parse_text_mapping(string text)
    {
        var values = new Dictionary<long, string>();
        string? fallback = null;
        foreach (string line in text.Replace('\r', ' ').Replace('\t', ' ').Split('\n'))
        {
            int separator = line.IndexOf('=');
            if (separator < 0) continue;
            string key = line[..separator];
            string value = line[(separator + 1)..];
            if (key == "_") fallback = value;
            else
            {
                string digits = string.Concat(key.Select(character => char.IsDigit(character)
                    ? (char)('0' + (int)char.GetNumericValue(character)) : character));
                if (long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number))
                    values[number] = value;
            }
        }
        return (values, fallback);
    }
}
