using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void validate_pointers()
    {
        foreach (var node in nodes.Values.Where(node => node.Name == "Pointer"))
        {
            var matches = variables.Values.Where(variable => variable.DefinitionFurnitureId == node.FurnitureId).ToArray();
            if (matches.Length != 1)
            {
                fail("pointer.definition", "A Pointer node requires exactly one variable definition with its DefinitionFurnitureId.", node.FurnitureId);
                continue;
            }
            var definition = matches[0];
            if (definition.RequiresExecutionContext == false || definition.Domain != (WiredSourceDomain)node.Number("target") ||
                definition.PointerAccessor is { } supplied && supplied != node.Text("accessor_variable"))
            {
                fail("pointer.definition", "The Pointer form and variable definition disagree.", node.FurnitureId);
                continue;
            }
            variables[definition.Id] = definition with { PointerAccessor = node.Text("accessor_variable"), RequiresExecutionContext = true };
        }
        foreach (var definition in variables.Values.Where(variable => variable.PointerAccessor is not null))
        {
            if (definition.Reference is not null || definition.Derived is not null || definition.RequiresExecutionContext == false ||
                !Enum.IsDefined(definition.Domain))
                fail("pointer.definition", "A Pointer is context-dependent and cannot also declare a fixed reference or derived value.", definition.DefinitionFurnitureId);
            if (!variables.TryGetValue(definition.PointerAccessor!, out var accessor) || !valid_pointer_accessor(accessor))
                fail("pointer.accessor", "A Pointer accessor requires an ordinary numeric user-created Context variable without execution-context dependencies.", definition.DefinitionFurnitureId);
        }
    }

    private static bool valid_pointer_accessor(WiredSimulationVariable variable) =>
        variable.Domain == WiredSourceDomain.Context && variable.HasValue && variable.Availability < 100 &&
        !variable.Name.StartsWith('@') && variable.Reference is null && variable.Derived is null &&
        variable.PointerAccessor is null && variable.RequiresExecutionContext != true;

    private string pointer_target(WiredSimulationVariable pointer, Frame? frame)
    {
        if (frame is null)
            pointer_failure("pointer.context", "Pointer resolution requires the current execution context.", frame);
        if (!frame!.Context.TryGetValue(pointer.PointerAccessor!, out long id))
            pointer_failure("pointer.unresolved", "The Pointer accessor has no value in this execution context.", frame);
        reads++;
        if (!furniture.ContainsKey(id))
            pointer_failure("pointer.unresolved", "The Pointer target definition furniture is absent.", frame);
        var matches = variables.Values.Where(variable => variable.DefinitionFurnitureId == id).Take(2).ToArray();
        if (matches.Length != 1)
            pointer_failure("pointer.unresolved", "The Pointer target furniture must identify exactly one captured variable definition.", frame);
        var target = matches[0];
        if (target.Domain != pointer.Domain)
            pointer_failure("pointer.domain", "The Pointer target domain differs from the referenced variable domain.", frame);
        return target.Id;
    }

    private void pointer_failure(string code, string message, Frame? frame)
    {
        note(WiredSimulationSeverity.Unsupported, code, message, frame?.Stack);
        status = WiredSimulationStatus.Faulted;
        throw new SimulationFailure();
    }
}
