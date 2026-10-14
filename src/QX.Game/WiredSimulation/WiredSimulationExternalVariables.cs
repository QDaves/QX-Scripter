using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly HashSet<string> external_variables = new(StringComparer.Ordinal);

    private static bool external_variable(WiredSimulationNode node) =>
        node.Category == WiredFormCategory.Variable && node.Name is "Quest" or "QuestChain" or "DailyTask";

    private void bind_external_variables()
    {
        foreach (var node in nodes.Values.Where(external_variable))
        {
            var matches = variables.Values.Where(variable => variable.DefinitionFurnitureId == node.FurnitureId).ToArray();
            if (matches.Length != 1)
            {
                fail("external.definition", "An external progress variable requires exactly one captured definition bound to its furniture.", node.FurnitureId);
                continue;
            }
            var definition = matches[0];
            if (definition.Domain != WiredSourceDomain.User || !definition.HasValue || definition.Writable ||
                definition.CanCreateAndDelete == true || definition.Reference is not null || definition.Derived is not null ||
                definition.PointerAccessor is not null || definition.RequiresExecutionContext == true ||
                definition.Name != node.Text("name"))
            {
                fail("external.definition", "External progress variables require a matching numeric read-only user definition without aliases or derived evaluators.", node.FurnitureId);
                continue;
            }
            external_variables.Add(definition.Id);
            if (definition.ObservedHolders is { } holders && (holders.Distinct().Count() != holders.Count ||
                holders.Any(id => !users.TryGetValue(id, out var user) || user.IsBot || user.IsPet)))
                fail("external.holders", "Observed progress holders must identify distinct captured human users.", node.FurnitureId);
            note(WiredSimulationSeverity.Information, "external.progress", "Only explicit progress observations are evaluated; quest progression, resets, rewards and notification generation are not inferred.", node.FurnitureId);
        }
        foreach (var definition in variables.Values.Where(variable => variable.ObservedHolders is not null && !external_variables.Contains(variable.Id)))
            fail("external.definition", "ObservedHolders is reserved for a bound Quest, QuestChain or DailyTask definition.", definition.DefinitionFurnitureId);
    }

    private WiredSimulationValue? external_value(string variable, WiredSourceDomain domain, long id, bool require_exact)
    {
        var definition = variables[variable];
        if (domain != WiredSourceDomain.User || !users.TryGetValue(id, out var user) || user.IsBot || user.IsPet)
            pointer_failure("external.holder", "Progress observations require the captured human user's account ID.", null);
        if (definition.ObservedHolders?.Contains(id) != true)
            pointer_failure("external.value_unknown", "No progress observation exists for variable " + variable + " and user " + id + ". Supply a captured value or an explicitly observed absence.", null);
        return exact_value(users[id].Variables?.GetValueOrDefault(variable), definition.Name, require_exact);
    }

    private void observe_variable(Frame frame)
    {
        var observation = frame.Event.Observation!;
        if (string.IsNullOrWhiteSpace(observation.VariableId) || !external_variables.Contains(observation.VariableId))
            pointer_failure("external.variable", "The observation must identify a bound Quest, QuestChain or DailyTask variable.", frame);
        if (!users.TryGetValue(observation.UserId, out var user) || user.IsBot || user.IsPet)
            pointer_failure("external.holder", "The observation requires a captured human user's account ID.", frame);
        if (observation.Change.HasValue != observation.Origin.HasValue || observation.Change is not (null or 1 or 2 or 4) ||
            observation.Origin is < 0 or > 3)
            pointer_failure("external.notification", "A notification requires both an observed change code (1, 2 or 4) and origin (0..3). Omit both when no notification was observed.", frame);
        var definition = variables[observation.VariableId];
        var previous = user!.Variables?.GetValueOrDefault(definition.Id);
        if (observation.Change is { } change)
        {
            if (definition.ObservedHolders?.Contains(user.Id) != true)
                pointer_failure("external.previous_unknown", "A notification requires the previous value or absence to be explicitly known.", frame);
            if (change == 1 && (previous is not null || observation.Value is null) ||
                change == 2 && (previous is null || observation.Value is null) ||
                change == 4 && (previous is null || observation.Value is not null))
                pointer_failure("external.transition", "The observed change code disagrees with the previous and new presence state.", frame);
            if (previous is { IsExact: false } || observation.Value is { IsExact: false })
                pointer_failure("external.precision", "A change notification requires exact values; clamped display values cannot determine its direction.", frame);
        }
        var values = user.Variables?.ToDictionary(StringComparer.Ordinal) ?? new();
        if (observation.Value is { } current) values[definition.Id] = current;
        else values.Remove(definition.Id);
        users[user.Id] = user with { Variables = values };
        variables[definition.Id] = definition with { ObservedHolders = (definition.ObservedHolders ?? []).Append(user.Id).Distinct().ToArray() };
        fx_changes.Add((WiredSourceDomain.User, user.Id, definition.Id));
        record("variable.observed", definition.DefinitionFurnitureId, definition.Id, [user.Id], previous?.Value, observation.Value?.Value);
        if (observation.Change is { } observed_change)
            enqueue(now, frame with { Event = new(WiredSimulationEventKind.VariableChanged, now, UserId: user.Id,
                Variable: definition.Id, OldValue: previous?.Value, NewValue: observation.Value?.Value,
                Change: observed_change, Origin: observation.Origin!.Value) }, external: true);
    }
}
