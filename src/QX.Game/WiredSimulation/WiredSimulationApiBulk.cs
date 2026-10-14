using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private WiredSimulationApiResponse bulk_delete(WiredSimulationApiRequest request, WiredSimulationApiAccess access, Frame frame)
    {
        WiredSimulationApiResponse response(int code, string? error = null) => new(code, now, request.Id, error);
        var addon = nodes.Values.SingleOrDefault(node => node.Name == "VariablesWebApi");
        if (!access.AllowMassDeletion || addon is not null && !addon.Flag("allow_mass_deletion"))
            return response(403, "wired.variables.bulk_delete_not_enabled");
        if (request.Domain != WiredSourceDomain.Global || request.Variable is not null || request.EntityId is not null ||
            request.TargetKind is not null || request.Value is not null || request.Values is not null || request.Operations is not null)
            return response(400, "Bulk deletion requires a room-scoped variable-name list only.");
        if (request.Variables is not { Count: > 0 }) return response(400, "wired.variables.bulk_delete_empty");
        var selected = new List<WiredSimulationVariable>();
        foreach (string name in request.Variables.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(name)) return response(400, "wired.variables.bulk_delete_invalid_variable");
            var matches = variables.Values.Where(variable => variable.Name == name &&
                variable.Domain is WiredSourceDomain.Furniture or WiredSourceDomain.User &&
                !variable.Name.StartsWith('@') && !variable.Name.StartsWith('~') && variable.Reference is null &&
                variable.PointerAccessor is null && variable.RequiresExecutionContext != true).ToArray();
            if (matches.Length != 1) return response(400, "wired.variables.bulk_delete_invalid_variable");
            if (!(matches[0].CanCreateAndDelete ?? matches[0].Writable))
                return response(403, "A variable does not permit deleting holder values.");
            selected.Add(matches[0]);
        }
        if (!access.AllHoldersKnown)
            incomplete_bulk("Bulk deletion needs AllHoldersKnown and every affected holder represented in the snapshot.");
        if (access.BulkDeleteNotifications is null)
            incomplete_bulk("Bulk deletion notification behavior is not established by the public API schema; supply a calibrated BulkDeleteNotifications value.");
        foreach (var variable in selected.Where(variable => variable.Domain == WiredSourceDomain.User && variable.Availability is 10 or 11))
        {
            if (!complete_stores.Contains(variable.Id))
                incomplete_bulk("Bulk deletion of a permanent user variable requires a complete StoredVariables entry.");
            if (access.BulkDeleteNotifications == true && stored_values[variable.Id].Keys.Any(id => !users.ContainsKey(id)))
                incomplete_bulk("Bulk deletion notifications for users outside the room require separate conformance.");
        }
        foreach (var variable in selected)
        {
            long[] holders = variable.Domain == WiredSourceDomain.Furniture
                ? furniture.Values.Where(item => item.Variables?.ContainsKey(variable.Id) == true).Select(item => item.Id).ToArray()
                : users.Values.Where(user => user.Variables?.ContainsKey(variable.Id) == true).Select(user => user.Id).ToArray();
            foreach (long id in holders)
                write(variable.Domain, id, variable.Id, 0, frame, remove: true, origin: 3, notify: access.BulkDeleteNotifications!.Value);
            if (variable.Domain == WiredSourceDomain.User && stored_values.TryGetValue(variable.Id, out var entries))
                foreach (long id in entries.Keys.ToArray())
                {
                    entries.Remove(id);
                    writes++;
                    charge(frame, 1, frame.Stack);
                    record("variable", frame.Stack, variable.Id, [id]);
                }
        }
        record("api.bulk_delete", frame.Stack, "Stored values removed; variable definitions retained.");
        return response(204);
    }

    private void incomplete_bulk(string message)
    {
        note(WiredSimulationSeverity.Unsupported, "api.bulk_delete.incomplete", message);
        status = WiredSimulationStatus.Faulted;
        throw new SimulationFailure();
    }
}
