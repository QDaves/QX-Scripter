using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private bool variable_age(WiredSimulationNode node, Frame frame, WiredSourceDomain domain, long id)
    {
        string variable = node.Text("variable");
        if (derivation(resolve_reference(variable, frame)) is not null)
            unsupported(node, "Derived variables do not expose creation or update timestamps.");
        if (domain == WiredSourceDomain.Context || variable_name(resolve_reference(variable, frame)).StartsWith('@'))
            unsupported(node, "Age metadata for context or intrinsic variables requires separate conformance.");
        var current = value(domain, id, variable, frame, require_exact: false);
        if (current is null) return false;
        long factor = node.Number("duration_unit") switch
        {
            0 => 1, 1 => 1000, 2 => 60000, 3 => 3600000, 4 => 86400000, 5 => 604800000, _ => 0
        };
        if (factor == 0) unsupported(node, "Calendar duration units require verified month/year conversion rules.");
        long kind = node.Number("age_kind");
        if (kind is not (0 or 1)) unsupported(node, "Unknown variable age kind.");
        long timestamp = known_timestamp(current!, kind == 0, node);
        if (timestamp > now)
            unsupported(node, "Variable timestamps must be relative to simulation time and cannot be in the future.");
        long age = checked(now - timestamp);
        var duration_value = operand(node, frame, "duration_kind", "duration", "duration_target", "duration_variable");
        if (duration_value is null) return false;
        long duration = checked(duration_value.Value * factor);
        if (age == duration)
            uncertain("variable.age_boundary", "An exact age threshold requires separate server boundary conformance.", node.FurnitureId);
        return node.Number("comparison") switch
        {
            0 => age < duration,
            2 => age > duration,
            _ => invalid_age_comparison(node)
        };
    }

    private long known_timestamp(WiredSimulationValue value, bool creation, WiredSimulationNode node)
    {
        if (!(creation ? value.CreationTimeKnown : value.UpdateTimeKnown))
            unsupported(node, "The requested variable timestamp was not captured; a missing timestamp is not zero.");
        return creation ? value.CreatedAt : value.UpdatedAt;
    }

    private bool invalid_age_comparison(WiredSimulationNode node)
    {
        unsupported(node, "Unknown variable age comparison.");
        return false;
    }
}
