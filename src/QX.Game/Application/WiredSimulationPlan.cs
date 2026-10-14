using Qx.Game.WiredSimulation;
using Qx.Model.Wired;

namespace Qx.Game.Application;

public static class WiredSimulationPlan
{
    public static WiredSimulationRoom Apply(WiredBuildPlan plan, WiredSimulationRoom room) => Apply(plan.ToRequest(), room);

    public static WiredSimulationRoom Apply(WiredBuildRequest plan, WiredSimulationRoom room)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(room);
        var analysis = WiredBuildAnalyzer.Analyze(plan);
        if (!analysis.Valid) throw new ArgumentException(string.Join("\n", analysis.Errors), nameof(plan));
        var items = room.Furniture.ToDictionary(item => item.Id);
        var nodes = room.Nodes.ToDictionary(node => node.FurnitureId);
        var keys = new Dictionary<string, long>(StringComparer.Ordinal);
        var game = room.Game;
        long next = Math.Max(0, items.Keys.DefaultIfEmpty(0).Max());
        foreach (var entry in plan.Items)
        {
            WiredSimulationFurniture? existing = entry.ExistingId is { } id ? items.GetValueOrDefault((long)id) : null;
            if (entry.ExistingId is not null && existing is null) throw new ArgumentException($"Missing existing furniture: {entry.Key}.");
            if (existing is null && entry.ReuseAtPosition)
            {
                var matches = items.Values.Where(item => item.ClassName == entry.ClassName && item.X == entry.X && item.Y == entry.Y && !keys.ContainsValue(item.Id)).ToArray();
                if (matches.Length > 1) throw new ArgumentException($"Ambiguous existing furniture: {entry.Key}.");
                existing = matches.SingleOrDefault();
            }
            if (existing is null && !entry.PlaceIfMissing) throw new ArgumentException($"Placement disabled for missing furniture: {entry.Key}.");
            var metadata = existing ?? room.Furniture.FirstOrDefault(item => item.ClassName == entry.ClassName)
                ?? throw new ArgumentException($"Supply furniture metadata for {entry.ClassName} in the simulation room.");
            if (existing is not null && existing.ClassName != entry.ClassName) throw new ArgumentException($"Furniture class mismatch: {entry.Key}.");
            if (existing is not null && (existing.X != entry.X || existing.Y != entry.Y || existing.Direction != entry.Direction))
                throw new ArgumentException($"Existing furniture position or direction mismatch: {entry.Key}.");
            long assigned = existing?.Id ?? checked(++next);
            if (keys.ContainsValue(assigned)) throw new ArgumentException($"Furniture reused by multiple plan entries: {entry.Key}.");
            keys.Add(entry.Key, assigned);
            items[assigned] = metadata with { Id = assigned, Key = entry.Key, X = entry.X, Y = entry.Y,
                Direction = entry.Direction, State = entry.State ?? existing?.State ?? 0,
                Z = entry.AltitudeCentimeters is { } altitude ? altitude / 100.0 : existing?.Z ?? 0,
                Variables = existing?.Variables, Temporary = existing?.Temporary ?? false, Hidden = existing?.Hidden ?? false,
                FleeDirection = existing?.FleeDirection,
                Clock = existing?.Clock ?? (metadata.Clock is not null ? new((entry.State ?? 0) * 1000L) : null) };
        }
        foreach (var entry in plan.Items.Where(item => item.Category.HasValue && item.Code.HasValue))
        {
            var form = WiredFormRegistry.Draft(entry.Category!.Value, entry.Code!.Value);
            var previous = nodes.GetValueOrDefault(keys[entry.Key]);
            if (previous is not null)
            {
                if (previous.Category != entry.Category || previous.Code != entry.Code)
                    throw new ArgumentException($"Existing Wired definition mismatch: {entry.Key}.");
                foreach (var field in form.Definition.Fields.Where(field => field.Writable && previous.Fields.ContainsKey(field.Name)))
                    form.SetField(field.Name, previous.Fields[field.Name]);
                form.SelectFurniture((previous.Furniture ?? []).Select(id => (Id)id).ToArray());
                form.SelectFurniture((previous.SecondaryFurniture ?? []).Select(id => (Id)id).ToArray(), true);
                for (int index = 0; index < previous.FurnitureSources?.Count; index++) form.SelectFurnitureSource(index, previous.FurnitureSources[index]);
                for (int index = 0; index < previous.UserSources?.Count; index++) form.SelectUserSource(index, previous.UserSources[index]);
                if (form.ActionDelay.HasValue) form.ActionDelay = previous.Delay;
                if (form.ConditionQuantifier.HasValue) form.ConditionQuantifier = previous.Quantifier;
                if (form.SelectorFilter.HasValue) form.SelectorFilter = previous.Filter;
                if (form.SelectorInvert.HasValue) form.SelectorInvert = previous.Invert;
            }
            entry.Settings?.Apply(form);
            if (entry.FurnitureKeys is not null) form.SelectFurniture(entry.FurnitureKeys.Select(key => (Id)keys[key]).ToArray());
            if (entry.SecondaryFurnitureKeys is not null) form.SelectFurniture(entry.SecondaryFurnitureKeys.Select(key => (Id)keys[key]).ToArray(), true);
            foreach (var variable in entry.VariablesByName ?? new Dictionary<string, string>())
            {
                var matches = (room.Variables ?? []).Where(value => value.Name == variable.Value).ToArray();
                if (matches.Length != 1) throw new ArgumentException($"Variable name must resolve uniquely: {variable.Value}.");
                form.SetField(variable.Key, new(Text: matches[0].Id));
            }
            var node = WiredSimulationNode.From(form) with { FurnitureId = keys[entry.Key], InputSources = previous?.InputSources };
            if (previous is not null)
            {
                var fields = node.Fields.ToDictionary(StringComparer.Ordinal);
                var edited = (entry.Settings?.Fields ?? []).Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
                edited.UnionWith(entry.VariablesByName?.Keys ?? []);
                foreach (var field in form.Definition.Fields.Where(field => previous.Fields.ContainsKey(field.Name) && !edited.Contains(field.Name)))
                {
                    string source = field.Name switch
                    {
                        "operand_high" => "operand_value", "reference_high" => "reference_value",
                        "duration_high" => "duration", "minimum_high" => "minimum_value",
                        "maximum_high" => "maximum_value", "audience_value_high" => "audience_value",
                        "initial_value_high" => "initial_value", _ => ""
                    };
                    if (!edited.Contains(source)) fields[field.Name] = previous.Fields[field.Name];
                }
                node = node with { Fields = fields, SavedFurniture = previous.SavedFurniture, SavedSnapshot = previous.SavedSnapshot };
            }
            bool save = previous is null || entry.CaptureSnapshot || node.Delay != previous.Delay || node.Quantifier != previous.Quantifier ||
                node.Fields.Count != previous.Fields.Count || node.Fields.Any(pair => !previous.Fields.TryGetValue(pair.Key, out var value) || pair.Value != value) ||
                !(node.Furniture ?? []).SequenceEqual(previous.Furniture ?? []) || !(node.SecondaryFurniture ?? []).SequenceEqual(previous.SecondaryFurniture ?? []) ||
                !(node.FurnitureSources ?? []).SequenceEqual(previous.FurnitureSources ?? []) || !(node.UserSources ?? []).SequenceEqual(previous.UserSources ?? []);
            if (WiredSimulationSnapshots.supports(node) && save)
                node = node with { SavedFurniture = null, SavedSnapshot = WiredSimulationSnapshots.capture(node, (node.Furniture ?? []).Select(id => items[id])) };
            else if (entry.CaptureSnapshot)
                node = node with { SavedFurniture = (node.Furniture ?? []).Select(id => items[id]).ToArray() };
            if (node.Name == "MoveToDirection")
                node = node with { MovementDirections = previous is null ? new Dictionary<long, int>() :
                    node.Fields.Count == previous.Fields.Count && node.Fields.All(pair => previous.Fields.TryGetValue(pair.Key, out var value) && pair.Value == value)
                        ? previous.MovementDirections : null };
            if (previous is not null && node.Name is "GiveScore" or "GiveScoreToPredefinedTeam" && game?.ScoreUses is { } uses)
            {
                bool fields_changed = node.Fields.Count != previous.Fields.Count || node.Fields.Any(pair => !previous.Fields.TryGetValue(pair.Key, out var value) || pair.Value != value);
                bool inputs_changed = node.Delay != previous.Delay ||
                    !(node.UserSources ?? []).SequenceEqual(previous.UserSources ?? []) ||
                    !(node.FurnitureSources ?? []).SequenceEqual(previous.FurnitureSources ?? []) ||
                    !(node.Furniture ?? []).SequenceEqual(previous.Furniture ?? []) ||
                    !(node.SecondaryFurniture ?? []).SequenceEqual(previous.SecondaryFurniture ?? []);
                if (fields_changed || inputs_changed)
                {
                    bool verified_reset = node.Number("points") != previous.Number("points") || node.Number("team") != previous.Number("team");
                    game = game with { ScoreUses = verified_reset ? uses.Where(use => use.ActionId != node.FurnitureId).ToArray() : null };
                }
            }
            if (previous is not null && node.Name == "ScoreAchieved" && game?.FiredScoreTriggers is { } fired &&
                (node.Number("score") != previous.Number("score") || node.Number("team") != previous.Number("team")))
                game = game with { FiredScoreTriggers = fired.Where(id => id != node.FurnitureId).ToArray() };
            nodes[node.FurnitureId] = node;
        }
        return Qx.Game.WiredSimulation.WiredSimulation.CopyRoom(room with { Furniture = items.Values.ToArray(), Nodes = nodes.Values.ToArray(), Game = game, Source = "offline build plan" });
    }
}
