using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly List<WiredSimulationApiResponse> api_responses = [];

    private static WiredSimulationApiRequest copy(WiredSimulationApiRequest request, int depth = 0)
    {
        if (depth > 1 || request.Operations is { Count: > 50 })
            throw new ArgumentException("API batches support at most 50 non-nested operations.");
        return request with
        {
            Values = request.Values?.ToDictionary(StringComparer.Ordinal), Variables = request.Variables?.ToArray(),
            Operations = request.Operations?.Select(operation => copy(operation, depth + 1)).ToArray()
        };
    }

    private static WiredSimulationApiResponse copy(WiredSimulationApiResponse response) => response with
    {
        Values = response.Values?.ToDictionary(StringComparer.Ordinal),
        Results = response.Results?.Select(copy).ToArray()
    };

    private WiredSimulationApiResponse api_request(WiredSimulationApiRequest request, Frame frame, bool batch = false)
    {
        WiredSimulationApiResponse result(int status, string? error = null,
            IReadOnlyDictionary<string, WiredSimulationValue>? values = null,
            IReadOnlyList<WiredSimulationApiResponse>? results = null) => new(status, now, request.Id, error, values, results);

        if (!Enum.IsDefined(request.Operation)) return result(400, "Unknown operation.");
        var access = initial.Api;
        if (access?.Enabled != true) return result(403, "The room API is not enabled in this snapshot.");
        bool read = request.Operation is WiredSimulationApiOperation.ReadValue or WiredSimulationApiOperation.ReadProfile;
        if (read ? !access.Read : !access.Write) return result(403, "The supplied permission profile does not authorize this operation.");
        if (request.Operation == WiredSimulationApiOperation.BulkDelete)
            return bulk_delete(request, access, frame);
        if (request.Variables is not null) return result(400, "A variable-name list is only valid for bulk deletion.");
        if (request.Domain is not (WiredSourceDomain.Furniture or WiredSourceDomain.User or WiredSourceDomain.Global))
            return result(400, "The API does not expose this variable domain.");

        if (request.Operation == WiredSimulationApiOperation.Batch)
        {
            if (batch || request.Domain == WiredSourceDomain.Global || !access.Read || request.Operations is not { Count: >= 1 and <= 50 })
                return result(400, "A batch requires read and write access and 1 to 50 holder operations.");
            if (string.IsNullOrWhiteSpace(request.Variable) || request.Operations.Any(operation =>
                operation.Domain != request.Domain || operation.Variable != request.Variable ||
                operation.Operation is not (WiredSimulationApiOperation.ReadValue or WiredSimulationApiOperation.ReplaceValue or
                    WiredSimulationApiOperation.PatchValue or WiredSimulationApiOperation.DeleteValue)))
                return result(400, "Batch operations must address one variable and domain.");
            return result(200, results: request.Operations.Select(operation => api_request(operation, frame, true)).ToArray());
        }

        long holder = 0;
        if (request.Domain == WiredSourceDomain.Global)
        {
            if (request.EntityId is not null || request.TargetKind is not null) return result(400, "Global variables have no holder address.");
            if (request.Operation is WiredSimulationApiOperation.ReplaceValue or WiredSimulationApiOperation.DeleteValue)
                return result(405, "The global API supports reads and patches only.");
        }
        else
        {
            if (!request.EntityId.HasValue || string.IsNullOrWhiteSpace(request.TargetKind)) return result(400, "A holder address is required.");
            if (request.Domain == WiredSourceDomain.User)
            {
                holder = request.EntityId.Value;
                if (!users.TryGetValue(holder, out var user) || request.TargetKind != (user.IsBot ? "bots" : user.IsPet ? "pets" : "users"))
                    return result(404, "The holder is not represented in the snapshot.");
            }
            else
            {
                if (request.TargetKind is not ("furni" or "furni-bc" or "wall-items" or "wall-items-bc")) return result(400, "Unknown furniture address kind.");
                var address = access.Addresses?.SingleOrDefault(address => address.Kind == request.TargetKind && address.ExternalId == request.EntityId);
                if (address is null || !furniture.ContainsKey(address.EntityId)) return result(404, "No explicit API-to-room furniture mapping exists.");
                holder = address.EntityId;
            }
        }

        WiredSimulationVariable? definition(string? name) => variables.Values.FirstOrDefault(variable =>
            variable.Name == name && variable.Domain == request.Domain && !variable.Name.StartsWith('@') && variable.Reference is null &&
            variable.PointerAccessor is null && variable.RequiresExecutionContext != true);
        var entries = request.Domain switch
        {
            WiredSourceDomain.Global => globals,
            WiredSourceDomain.User => users[holder].Variables,
            _ => furniture[holder].Variables
        };
        var profile = variables.Values.Where(variable => variable.Domain == request.Domain && !variable.Name.StartsWith('@') && variable.Reference is null &&
            variable.PointerAccessor is null && variable.RequiresExecutionContext != true)
            .Where(variable => entries?.ContainsKey(variable.Id) == true)
            .ToDictionary(variable => variable.Name, variable => entries![variable.Id], StringComparer.Ordinal);
        if (request.Operation == WiredSimulationApiOperation.ReadProfile) return result(200, values: profile);
        if (request.Operation == WiredSimulationApiOperation.PatchProfile)
        {
            if (request.Values is not { Count: > 0 }) return result(400, "A nonempty variable patch is required.");
            var selected = request.Values.Select(pair => (pair.Key, pair.Value, Definition: definition(pair.Key))).ToArray();
            if (selected.Any(pair => pair.Definition is null)) return result(404, "An addressed variable definition is absent.");
            if (selected.Any(pair => !pair.Definition!.Writable || !pair.Definition.HasValue)) return result(403, "A variable does not support numeric writes.");
            if (selected.Any(pair => pair.Value is null) && request.Domain == WiredSourceDomain.Global)
                return result(400, "Global profiles do not support null removals.");
            if (selected.Any(pair => (pair.Value is null || entries?.ContainsKey(pair.Definition!.Id) != true) &&
                !(pair.Definition!.CanCreateAndDelete ?? pair.Definition.Writable)))
                return result(403, "A variable does not permit creating or deleting holder values.");
            foreach (var pair in selected)
                write(request.Domain, holder, pair.Definition!.Id, pair.Value ?? 0, frame,
                    create: pair.Value is not null && entries?.ContainsKey(pair.Definition.Id) != true,
                    remove: pair.Value is null, origin: 3);
            return result(200, values: api_profile(request.Domain, holder));
        }

        var variable = definition(request.Variable);
        if (variable is null) return result(404, "The variable definition is absent.");
        var prior = entries?.GetValueOrDefault(variable.Id);
        if (request.Operation == WiredSimulationApiOperation.ReadValue)
            return prior is null ? result(404, "The holder value is absent.") : result(200, values: new Dictionary<string, WiredSimulationValue> { [variable.Name] = prior });
        if (!variable.Writable || !variable.HasValue) return result(403, "The variable does not support numeric writes.");
        if (request.Operation is WiredSimulationApiOperation.PatchValue or WiredSimulationApiOperation.DeleteValue && prior is null)
            return result(404, "The holder value is absent.");
        bool remove = request.Operation == WiredSimulationApiOperation.DeleteValue;
        if (!remove && request.Value is null) return result(400, "A signed 64-bit value is required.");
        bool create = request.Operation == WiredSimulationApiOperation.ReplaceValue && prior is null;
        if ((create || remove) && !(variable.CanCreateAndDelete ?? variable.Writable))
            return result(403, "The variable does not permit creating or deleting holder values.");
        write(request.Domain, holder, variable.Id, request.Value ?? 0, frame,
            create: create,
            remove: remove, origin: 3, replace: request.Operation == WiredSimulationApiOperation.ReplaceValue);
        return result(200, values: remove ? new Dictionary<string, WiredSimulationValue>() :
            new Dictionary<string, WiredSimulationValue> { [variable.Name] = api_profile(request.Domain, holder)[variable.Name] });
    }

    private Dictionary<string, WiredSimulationValue> api_profile(WiredSourceDomain domain, long holder)
    {
        var entries = domain switch
        {
            WiredSourceDomain.Global => globals,
            WiredSourceDomain.User => users[holder].Variables,
            _ => furniture[holder].Variables
        };
        return variables.Values.Where(variable => variable.Domain == domain && !variable.Name.StartsWith('@') && variable.Reference is null &&
            variable.PointerAccessor is null && variable.RequiresExecutionContext != true)
            .Where(variable => entries?.ContainsKey(variable.Id) == true)
            .ToDictionary(variable => variable.Name, variable => entries![variable.Id], StringComparer.Ordinal);
    }
}
