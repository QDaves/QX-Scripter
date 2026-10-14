using Qx.Game.WiredSimulation;
using Qx.Model.Wired;

namespace Qx.Game.Application;

internal sealed partial class WiredSimulationApplication
{
    private static void import_reference_definitions(IEnumerable<WiredFormView> forms,
        Dictionary<string, WiredSimulationVariable> definitions)
    {
        foreach (var view in forms)
        {
            if (view.CreateForm() is not WiredReferenceVariableForm form) continue;
            var matches = view.Configuration.Context.SelectMany(entry => entry.SharedVariables ?? [])
                .Where(variable => variable.VariableId == form.ReferenceVariable).ToArray();
            if (matches.Length == 0 || definitions.ContainsKey(form.ReferenceVariable)) continue;
            var variable = matches[0];
            if (!Enum.IsDefined((WiredSourceDomain)variable.VariableTarget)) continue;
            if (matches.Any(other => other.RoomId != variable.RoomId || other.VariableTarget != variable.VariableTarget ||
                other.AvailabilityType != variable.AvailabilityType || other.CanWriteValue != variable.CanWriteValue ||
                other.CanCreateAndDelete != variable.CanCreateAndDelete || other.HasValue != variable.HasValue))
                throw new ArgumentException("Conflicting shared variable definitions: " + variable.VariableId);
            if ((variable.TextConnectorIds is null) != (variable.TextConnectorValues is null) ||
                variable.TextConnectorIds?.Count != variable.TextConnectorValues?.Count)
                throw new ArgumentException("Shared variable text connector keys and values do not match: " + variable.VariableId);
            var text = captured_text(variable.TextConnectorIds?.Select((id, index) => ((long)id, variable.TextConnectorValues![index])));
            definitions[variable.VariableId] = new(variable.VariableId, variable.VariableName,
                (WiredSourceDomain)variable.VariableTarget, variable.AvailabilityType, variable.HasValue,
                variable.CanWriteValue, text, TextFallback: variable.TextConnectorFallback,
                CanCreateAndDelete: variable.CanCreateAndDelete, RequiresExecutionContext: variable.RequiresExecutionContext);
        }
    }

    private static void bind_reference(WiredFormView view, WiredReferenceVariableForm form,
        Dictionary<string, WiredSimulationVariable> definitions, List<string> missing)
    {
        var ids = view.Configuration.Context.Where(entry => entry.Variable is not null)
            .Select(entry => entry.Variable!.VariableId).Distinct(StringComparer.Ordinal).ToArray();
        var matching = ids.Length == 0
            ? definitions.Values.Where(variable => variable.Availability == 20 && variable.Name == form.Name).ToArray()
            : definitions.Values.Where(variable => ids.Contains(variable.Id, StringComparer.Ordinal)).ToArray();
        if (matching.Length != 1)
        {
            missing.Add("Reference variable definition is missing or ambiguous: " + form.Name);
            return;
        }
        var definition = matching[0];
        definitions[definition.Id] = definition with
        {
            DefinitionFurnitureId = (long)view.Configuration.Id,
            Reference = form.ReferenceVariable,
            Writable = definition.Writable && !form.ReadOnly,
            CanCreateAndDelete = form.ReadOnly ? false : definition.CanCreateAndDelete
        };
        if (!definitions.TryGetValue(form.ReferenceVariable, out var target))
            missing.Add("Reference target definition unavailable: " + form.ReferenceVariable);
        else if (target.Domain != definition.Domain)
            missing.Add("Reference target domain differs from its definition: " + definition.Id);
        missing.Add("Shared reference metadata does not include holder values or cross-room updates: " + definition.Id);
    }
}
