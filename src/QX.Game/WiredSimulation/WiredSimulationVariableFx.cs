using System.Globalization;
using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private IReadOnlyList<WiredSimulationVariableFx> fx_values()
    {
        var result = new List<WiredSimulationVariableFx>();
        if (!has_fx || status == WiredSimulationStatus.Faulted) return result;
        long previous_reads = reads;
        try
        {
            foreach (var node in nodes.Values.Where(n => n.Category == WiredFormCategory.Addon && n.Code is >= 1200 and <= 1205))
            {
                int category = node.Code - 1200;
                var config = fx_configuration(node, category);
                var domain = (WiredSourceDomain)node.Number("source_type");
                if (domain is not (WiredSourceDomain.Furniture or WiredSourceDomain.User))
                    unsupported(node, "Variable FX requires a furniture or user source.");
                var frame = new Frame(0, new(WiredSimulationEventKind.Start, now), [], [], [], []);
                var bindings = siblings(node).Where(n => n.Category == WiredFormCategory.Variable &&
                    n.Name == (domain == WiredSourceDomain.Furniture ? "Furniture" : "UserVariable"))
                    .SelectMany(n => variables.Values.Where(v => v.Domain == domain && v.Name == n.Text("name") && v.HasValue && v.Derived is null)).ToArray();
                if (bindings.Length == 0) continue;
                IEnumerable<long> holders = domain == WiredSourceDomain.Furniture ? furniture.Keys : users.Keys;
                foreach (var binding in bindings)
                foreach (long holder in holders)
                {
                    var current = value(domain, holder, binding.Id, frame);
                    if (current is null) continue;
                    long? minimum = null, maximum = null;
                    var extra = new Dictionary<string, string>();
        if (node.Number("stack_order") != 0)
            extra["stack_order"] = node.Number("stack_order").ToString(CultureInfo.InvariantCulture);
                    if (category == 2)
                    {
                        var levels = siblings(node).Where(n => n.Name == "VariableLevelUp").ToArray();
                        if (levels.Length != 1) unsupported(node, "Level FX requires exactly one verified Level Up add-on.");
                        long read(int index) => level_value(levels[0], index, current.Value)!.Value;
                        minimum = read(1) - read(2);
                        maximum = minimum + read(4);
                        extra["current_level"] = read(0).ToString(CultureInfo.InvariantCulture);
                        extra["max_level"] = read(7).ToString(CultureInfo.InvariantCulture);
                        extra["is_maxed"] = level_value(levels[0], 6, current.Value) is null ? "false" : "true";
                    }
                    else
                    {
                        if (node.Flag("override_minimum")) minimum = fx_override(node, "minimum", domain, holder, frame);
                        if (node.Flag("override_maximum")) maximum = fx_override(node, "maximum", domain, holder, frame);
                        if (minimum.HasValue || maximum.HasValue)
                        {
                            long lower = minimum ?? config.DefaultMinValue;
                            long upper = maximum ?? config.DefaultMaxValue;
                            if (lower < upper) { minimum = lower; maximum = upper; }
                            else { minimum = null; maximum = null; }
                        }
                    }
                    result.Add(new(config, binding.Id, domain, holder, current.Value, minimum, maximum, extra,
                        fx_audience(node, domain, holder, frame)));
                }
            }
        }
        catch (SimulationFailure) { }
        finally { reads = previous_reads; }
        return result;
    }

    private long? fx_override(WiredSimulationNode node, string bound, WiredSourceDomain domain, long holder, Frame frame)
    {
        var target = (WiredSourceDomain)node.Number(bound + "_target");
        if (target != domain && target != WiredSourceDomain.Global)
            unsupported(node, "Variable FX range overrides require the source domain or a global variable.");
        return value(target, target == WiredSourceDomain.Global ? 0 : holder, node.Text(bound + "_variable"), frame)?.Value;
    }

    private long[] fx_audience(WiredSimulationNode node, WiredSourceDomain domain, long holder, Frame frame)
    {
        var viewers = users.Values.Where(u => !u.IsBot && !u.IsPet);
        return node.Number("audience") switch
        {
            0 when domain == WiredSourceDomain.User => viewers.Where(u => u.Id == holder).Select(u => u.Id).ToArray(),
            1 when domain == WiredSourceDomain.User => fx_team_audience(node, holder, viewers),
            2 => viewers.Select(u => u.Id).ToArray(),
            3 => viewers.Where(u => value(WiredSourceDomain.User, u.Id, node.Text("audience_variable"), frame) is not null).Select(u => u.Id).ToArray(),
            4 => viewers.Where(u => value(WiredSourceDomain.User, u.Id, node.Text("audience_variable"), frame)?.Value == node.Number("audience_value")).Select(u => u.Id).ToArray(),
            _ => unsupported_fx_audience(node)
        };
    }

    private long[] fx_team_audience(WiredSimulationNode node, long holder, IEnumerable<WiredSimulationUser> viewers)
    {
        if (!users.TryGetValue(holder, out var owner) || owner.Team == 0)
            unsupported(node, "Same-team FX visibility without a game team requires separate conformance.");
        return viewers.Where(u => u.Team == owner!.Team).Select(u => u.Id).ToArray();
    }

    private long[] unsupported_fx_audience(WiredSimulationNode node)
    {
        unsupported(node, "This Variable FX audience/source combination requires separate conformance.");
        return [];
    }

    private VariableFxConfigEntry fx_configuration(WiredSimulationNode node, int category)
    {
        var style = WiredFxStyles.Find((WiredFxCategory)category, (int)node.Number("style_id"));
        if (style is null) unsupported(node, "Unknown Variable FX style.");
        int renderer = (int)node.Number("renderer_id"), color = (int)node.Number("color_id"), width = (int)node.Number("width_id");
        if (!style!.Renderers.Contains((WiredFxRenderer)renderer) || !style.Colors.Contains((WiredFxColor)color) || !style.Widths.Contains((WiredFxWidth)width))
            unsupported(node, "Variable FX style choices must be taken from inspected settings or the native style catalog.");
        if (node.FurnitureId is < int.MinValue or > int.MaxValue)
            unsupported(node, "Variable FX configuration IDs must fit the native protocol.");
        int effective_renderer = renderer;
        if (category == 2)
        {
            int sub_renderer = (int)node.Number("sub_renderer_id");
            if (node.Number("style_id") == 0 ? sub_renderer is not (2 or 3 or 4) : sub_renderer != 1)
                unsupported(node, "Level FX requires a supported sub-renderer for its style.");
            if (renderer == 20) effective_renderer = sub_renderer;
        }
        long segments = node.Number("segments");
        if (segments is < 0 or > 100 || segments > 0 && !WiredFxStyles.SupportsSegments((WiredFxRenderer)effective_renderer))
            unsupported(node, "Variable FX segments require a supported renderer and a count from 1 to 100.");
        string icon = node.Text("icon");
        if (icon.Length != 0 && (category != 5 || !WiredFxStyles.StandardIcons.Contains(icon)))
            unsupported(node, "This Variable FX icon requires separate availability and server conformance.");
        var extra = new Dictionary<string, string>();
        if (node.Number("stack_order") != 0)
            extra["stack_order"] = node.Number("stack_order").ToString(CultureInfo.InvariantCulture);
        int id = (int)node.Number("style_id");
        if (category == 0 && id == 0) extra["icon"] = "misc_heart";
        if (category == 2) extra["sub_renderer"] = node.Number("sub_renderer_id").ToString(CultureInfo.InvariantCulture);
        if (category == 3)
        {
            string[] icons = ["energy", "shield", "magic", "food", "stamina", "poison", "mana", "health", "gold", "gems", "honor", "reputation", "cooldown", "timeleft", "burning", "freezing", "battery", "repairing", "stealth", "upgrading", "star_power", "droplet"];
            string[] colors = ["ffd83d", "4aa9f6", "8751d1", "ff9f24", "86d213", "8ddc35", "268fff", "7dce35", "ffc83d", "416bdd", "fac384", "ffd83d", "b8c3cc", "74b9e8", "ff5a1f", "82cfff", "", "c9c5b8", "6254a8", "6bdc34", "ffd900", "4aabf5"];
            extra["icon"] = icons[id];
            if (id != 16)
            {
                extra["color"] = "#" + colors[id];
                extra["metallic"] = id is 8 or 9 or 17 or 20 ? "true" : "false";
            }
        }
        if (category == 4 && id == 0) { extra["icon"] = "misc_skull"; extra["icon_alignment"] = "double"; }
        if (category == 5) extra["design"] = id switch { 0 => "freeze_style", 1 => "shalimar", _ => "blocky" };
        if (segments > 0) extra["segments"] = segments.ToString(CultureInfo.InvariantCulture);
        if (icon.Length != 0)
        {
            int alignment = (int)node.Number("icon_alignment");
            if (alignment is < 0 or > 2) unsupported(node, "Variable FX icon alignment must be left, right or both sides.");
            extra["icon"] = icon;
            extra["icon_alignment"] = alignment switch { 1 => "right", 2 => "double", _ => "left" };
        }
        long bound(string name) => node.Fields.ContainsKey(name + "_high")
            ? unchecked((node.Number(name + "_high") << 32) | (uint)node.Number(name + "_value")) : node.Number(name + "_value");
        return new((int)node.FurnitureId, node.Number("source_type") == 1, (int)node.Number("show_mode"),
            (int)node.Number("update_mask"), node.Flag("show_on_hover") || node.Number("show_mode") == 2, (int)node.Number("duration_milliseconds"),
            category, id, color, width, renderer, bound("minimum"), bound("maximum"), extra);
    }
}
