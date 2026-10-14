using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly HashSet<(WiredSourceDomain Domain, long Id, string Variable)> derived_reads = [];

    [GeneratedRegex(@"^s(?<parent>.+)-(?<addon>[0-9]+)-(?<index>[0-9]+)(?:\t(?<source>[012]))?$", RegexOptions.CultureInvariant)]
    private static partial Regex derived_id();

    private WiredSimulationDerivedVariable? derivation(string variable)
    {
        if (variables.TryGetValue(variable, out var definition) && definition.Derived is { } known) return known;
        var match = derived_id().Match(variable);
        if (!match.Success || !long.TryParse(match.Groups["addon"].Value, CultureInfo.InvariantCulture, out long addon)
            || !int.TryParse(match.Groups["index"].Value, CultureInfo.InvariantCulture, out int index)) return null;
        return new(match.Groups["parent"].Value, addon, index,
            match.Groups["source"].Success ? int.Parse(match.Groups["source"].Value, CultureInfo.InvariantCulture) : null);
    }

    private WiredSimulationValue? derived_value(WiredSourceDomain domain, long id, string variable, WiredSimulationDerivedVariable derived, Frame frame)
    {
        if (!nodes.TryGetValue(derived.AddonId, out var addon))
        { unsupported_variable(variable); return null; }
        var key = (domain, id, variable);
        if (!derived_reads.Add(key) || derived_reads.Count > 64)
            unsupported(addon, "Cyclic or excessively deep derived-variable dependencies cannot be evaluated.");
        try
        {
            if (derived.Index is < 0 or > 30 || string.IsNullOrEmpty(derived.Parent))
                unsupported(addon, "Invalid derived-variable metadata.");
            bool timestamp = addon.Name == "VariableTimeUtil" && derived.Index == 0 &&
                addon.Number("time_source") is 1 or 2;
            if (!timestamp && (addon.Number("subvariable_mask") & (1L << derived.Index)) == 0) return null;
            if (addon.Name == "VariableTimeUtil" && addon.Number("time_source") is 1 or 2 &&
                (domain == WiredSourceDomain.Context || variable_name(resolve_reference(derived.Parent, frame)).StartsWith('@') ||
                    derivation(resolve_reference(derived.Parent, frame)) is not null))
                unsupported(addon, "Creation and update timestamps are unavailable for context, intrinsic or derived parent variables.");
            var source = value(domain, id, derived.Parent, frame,
                require_exact: !(addon.Name == "VariableTimeUtil" && addon.Number("time_source") is 1 or 2));
            if (source is null) return null;
            return addon.Name switch
            {
                "VariableLevelUp" => level_value(addon, derived.Index, source.Value),
                "VariableTimeUtil" => time_utility_value(addon, derived, source),
                _ => unknown_derivation(addon)
            };
        }
        finally { derived_reads.Remove(key); }
    }

    private WiredSimulationValue? unknown_derivation(WiredSimulationNode addon)
    {
        unsupported(addon, "This derived-variable add-on has no verified evaluator.");
        return null;
    }

    private WiredSimulationValue? level_value(WiredSimulationNode addon, int index, long input)
    {
        if (index > 7) unsupported(addon, "Unknown level subvariable index.");
        long mode = addon.Number("mode"), step = addon.Number("step_or_first_xp");
        if (mode is not (0 or 1 or 2)) unsupported(addon, "Unknown level progression mode.");
        var points = new List<(long Level, long Xp)> { (1, 0) };
        if (mode == 0)
        {
            foreach (string line in addon.Text("level_mapping").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split('=');
                long level = 0, xp = 0;
                if (fields.Length != 2 || !long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out level)
                    || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out xp)
                    || level <= points[^1].Level || xp <= points[^1].Xp || level > int.MaxValue)
                    unsupported(addon, "Manual level thresholds must contain strictly increasing level and XP pairs.");
                points.Add((level, xp));
            }
            if (points.Count == 1) unsupported(addon, "An empty manual level map requires separate conformance.");
        }
        long maximum = mode == 0 ? points[^1].Level : addon.Number(mode == 1 ? "maximum_or_increase" : "maximum_level");
        long increase = addon.Number("maximum_or_increase");
        if (mode != 0 && (step is < 1 or > 100000 || maximum is < 2 or > 100000 || mode == 2 && increase is < 1 or > 100000))
            unsupported(addon, "The level progression settings are outside the supported form bounds.");
        long threshold(long level)
        {
            if (mode == 0)
            {
                int upper_point = points.FindIndex(point => point.Level >= level);
                if (upper_point == 0) return 0;
                var start = points[upper_point - 1];
                var end = points[upper_point];
                return start.Xp + (long)((BigInteger)(end.Xp - start.Xp) * (level - start.Level) / (end.Level - start.Level));
            }
            if (mode == 1) return step * (level - 1);
            double strength = increase / 100.0;
            double result = step * (Math.Pow(1 + strength, level - 1) - 1 + 1e-9) / strength;
            if (!double.IsFinite(result) || result > 9007199254740991)
                unsupported(addon, "Exponential thresholds beyond exact integer double precision require separate conformance.");
            return (long)result;
        }
        long maximum_xp = threshold(maximum);
        long current_xp = Math.Clamp(input, 0, maximum_xp);
        long lower = 1, upper = maximum;
        while (lower < upper)
        {
            long middle = lower + (upper - lower + 1) / 2;
            if (threshold(middle) <= current_xp) lower = middle; else upper = middle - 1;
        }
        long current_level = lower;
        bool maxed = current_level == maximum;
        long progress = maxed ? 0 : current_xp - threshold(current_level);
        long required = maxed ? 0 : threshold(current_level + 1) - threshold(current_level);
        if (index == 6) return maxed ? new(-1) : null;
        return new(index switch
        {
            0 => current_level,
            1 => current_xp,
            2 => progress,
            3 => required == 0 ? 0 : (long)((BigInteger)progress * 100 / required),
            4 => required,
            5 => required - progress,
            7 => maximum,
            _ => throw new InvalidOperationException("Unknown level output.")
        });
    }
}
