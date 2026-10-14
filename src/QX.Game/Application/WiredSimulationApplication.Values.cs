using Qx.Game.WiredSimulation;
using Qx.Model.Wired;

namespace Qx.Game.Application;

internal sealed partial class WiredSimulationApplication
{
    private static IReadOnlyList<WiredVariablesObjectSnapshot>? freeze_object_values(IReadOnlyList<WiredVariablesObjectSnapshot>? snapshots)
    {
        if (snapshots is null) return null;
        var result = new List<WiredVariablesObjectSnapshot>(snapshots.Count);
        var holders = new HashSet<(WiredTarget, int)>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot is null || snapshot.Values is null)
                throw new ArgumentException("Object-variable readouts and their value collections cannot be null.");
            if (snapshot.Target is not (WiredTarget.Furni or WiredTarget.User or WiredTarget.Global) ||
                (snapshot.Target == WiredTarget.Furni && snapshot.ObjectId <= 0) ||
                (snapshot.Target == WiredTarget.User && snapshot.ObjectId < 0) ||
                (snapshot.Target == WiredTarget.Global && snapshot.ObjectId != 0))
                throw new ArgumentException("Object-variable readouts require a furniture ID, user room index or global object zero.");
            if (!holders.Add((snapshot.Target, snapshot.ObjectId)))
                throw new ArgumentException("Only one object-variable readout may be supplied per holder.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in snapshot.Values)
                if (value is null || string.IsNullOrWhiteSpace(value.VariableId) || !ids.Add(value.VariableId))
                    throw new ArgumentException("Object-variable values require distinct nonempty variable IDs.");
            result.Add(snapshot with { Values = Array.AsReadOnly(snapshot.Values.ToArray()),
                ConfiguredInWireds = Array.AsReadOnly((snapshot.ConfiguredInWireds ?? []).ToArray()) });
        }
        return result.AsReadOnly();
    }

    private static WiredSimulationRoom attach_object_values(WiredSimulationRoom room,
        IReadOnlyList<WiredVariablesObjectSnapshot>? snapshots, long generation, bool can_read)
    {
        if (snapshots is not { Count: > 0 }) return room;
        if (!can_read) throw new InvalidOperationException("Current server-granted Wired read permission is required for object-variable readouts.");
        var furniture = room.Furniture.ToArray();
        var users = room.Users.ToArray();
        var globals = room.GlobalVariables;
        var variable_definitions = (room.Variables ?? []).ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        var progress_boxes = room.Nodes.Where(node => node.Category == WiredFormCategory.Variable &&
            node.Name is "Quest" or "QuestChain" or "DailyTask").Select(node => node.FurnitureId).ToHashSet();
        var missing = new List<string>();
        var furniture_definitions = (room.Variables ?? []).Where(variable => variable.Domain == WiredSourceDomain.Furniture).ToArray();
        var user_definitions = (room.Variables ?? []).Where(variable => variable.Domain == WiredSourceDomain.User).ToArray();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.Generation != generation)
                throw new ArgumentException("An object-variable readout belongs to another Wired generation.");
            var values = snapshot.Values.ToDictionary(value => value.VariableId,
                value => WiredSimulationValue.FromClientDisplay(value.Value), StringComparer.Ordinal);
            if (snapshot.Target == WiredTarget.Furni)
            {
                int index = Array.FindIndex(furniture, item => item.Id == snapshot.ObjectId);
                if (index < 0) throw new ArgumentException("An object-variable readout refers to furniture absent from the captured room.");
                var item = furniture[index];
                var definitions = furniture_definitions;
                foreach (var identity in new[] { (Name: "@id", Value: item.Id), (Name: "@class_id", Value: (long)item.Kind) })
                    if (native_readout(values, definitions, identity.Name) is { IsExact: true } observed && observed.Value != identity.Value)
                        throw new ArgumentException("An object-variable readout disagrees with the current furniture identity.");
                int? type = item.IntrinsicType;
                if (native_readout(values, definitions, "@type") is { } origin)
                {
                    if (origin.IsExact && origin.Value is >= 0 and <= 2) type = (int)origin.Value;
                    else missing.Add("Furniture origin type is not modeled: " + item.Id);
                }
                item = furniture_geometry(room, item, values, definitions, missing);
                furniture[index] = item with { Variables = values, IntrinsicType = type, Temporary = type is null ? item.Temporary : type == 2 };
            }
            else if (snapshot.Target == WiredTarget.User)
            {
                var matches = users.Select((user, index) => (user, index)).Where(item => item.user.Index == snapshot.ObjectId).ToArray();
                if (matches.Length != 1) throw new ArgumentException("An object-variable readout requires a unique current user room index.");
                int index = matches[0].index;
                if (!users[index].IsBot && !users[index].IsPet)
                    foreach (var variable in user_definitions.Where(variable => variable.DefinitionFurnitureId is { } box && progress_boxes.Contains(box)))
                    {
                        var current = variable_definitions[variable.Id];
                        variable_definitions[variable.Id] = current with
                        {
                            ObservedHolders = (current.ObservedHolders ?? []).Append(users[index].Id).Distinct().ToArray()
                        };
                    }
                var definitions = user_definitions;
                var activity = users[index].Activity ?? new();
                if (definitions.Any(variable => variable.Name == "@dance") || values.ContainsKey("@dance"))
                {
                    var dance = native_readout(values, definitions, "@dance");
                    if (dance is null) activity = activity with { Dance = 0 };
                    else if (dance.IsExact && dance.Value is >= 1 and <= 4) activity = activity with { Dance = (int)dance.Value };
                    else missing.Add("The dance readout is not an exact supported style: " + users[index].Id);
                }
                if (definitions.Any(variable => variable.Name == "@sign") || values.ContainsKey("@sign"))
                {
                    var sign = native_readout(values, definitions, "@sign");
                    if (sign is null) activity = activity with { Sign = null, SignKnown = true, SignRemainingMilliseconds = null, SignObservedAt = 0 };
                    else if (sign.IsExact && sign.Value is >= 0 and <= 17)
                        activity = activity with { Sign = (int)sign.Value, SignKnown = true, SignRemainingMilliseconds = null, SignObservedAt = 0 };
                    else missing.Add("The sign readout is not an exact supported sign: " + users[index].Id);
                }
                users[index] = user_geometry(room, users[index], values, definitions, missing) with { Variables = values, Activity = activity };
            }
            else globals = values;
        }
        return room with { Furniture = furniture, Users = users, GlobalVariables = globals, Variables = variable_definitions.Values.ToArray(),
            Incomplete = [.. room.Incomplete ?? [], .. missing, "Explicit object-variable readouts provide values but no timestamps and may become stale within the same room generation. Refresh them after writes or furniture replacement."] };
    }

    private static WiredSimulationValue? native_readout(IReadOnlyDictionary<string, WiredSimulationValue> values,
        IReadOnlyList<WiredSimulationVariable> definitions, string name)
    {
        var found = definitions.Where(variable => variable.Name == name).Select(variable => values.GetValueOrDefault(variable.Id))
            .Append(values.GetValueOrDefault(name)).Where(value => value is not null).Distinct().ToArray();
        if (found.Length > 1) throw new ArgumentException("Native variable aliases contain conflicting readout values.");
        return found.FirstOrDefault();
    }
}
