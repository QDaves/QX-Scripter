using System.Globalization;
using System.Text;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly Dictionary<string, Dictionary<long, WiredSimulationStoredUserValue>> stored_values = new(StringComparer.Ordinal);
    private readonly HashSet<string> complete_stores = new(StringComparer.Ordinal);

    private static IReadOnlyList<WiredSimulationStoredVariable>? copy_stores(IReadOnlyList<WiredSimulationStoredVariable>? stores) =>
        stores?.Select(store => store is null || store.Values is null
            ? throw new ArgumentException("Stored variable entries cannot be null.") : store with { Values = store.Values.ToArray() }).ToArray();

    private void restore_stores()
    {
        foreach (var store in initial.StoredVariables ?? [])
        {
            if (store is null || !variables.TryGetValue(store.VariableId, out var variable) || variable.Domain != WiredSourceDomain.User ||
                variable.Availability is not (10 or 11) || store.Values is null || stored_values.ContainsKey(store.VariableId))
                throw new ArgumentException("Stored leaderboard data requires a unique permanent user variable.");
            var entries = new Dictionary<long, WiredSimulationStoredUserValue>();
            foreach (var entry in store.Values)
            {
                if (entry is null || entry.Value is null || entry.UserId is <= 0 or > int.MaxValue || entry.Name is null || entry.Figure is null ||
                    !entries.TryAdd(entry.UserId, entry))
                    throw new ArgumentException("Stored leaderboard users require unique positive IDs, profiles and values.");
                if (users.TryGetValue(entry.UserId, out var user))
                {
                    if (user.IsBot || user.IsPet) throw new ArgumentException("Permanent leaderboard storage cannot contain bots or pets.");
                    if (user.Variables?.TryGetValue(store.VariableId, out var current) == true && current != entry.Value)
                        throw new ArgumentException("Stored and current user variable values disagree.");
                }
            }
            if (store.Complete && users.Values.Any(user => !user.IsBot && !user.IsPet &&
                user.Variables?.ContainsKey(store.VariableId) == true && !entries.ContainsKey(user.Id)))
                throw new ArgumentException("Complete leaderboard storage is missing a known current user value.");
            stored_values.Add(store.VariableId, entries);
            if (store.Complete) complete_stores.Add(store.VariableId);
        }
    }

    private IReadOnlyList<WiredSimulationStoredVariable>? store_snapshot() => initial.StoredVariables is null ? null :
        stored_values.Select(store => new WiredSimulationStoredVariable(store.Key, store.Value.Values.OrderBy(value => value.UserId).ToArray(), complete_stores.Contains(store.Key))).ToArray();

    private void store_user_value(string variable, long id, WiredSimulationValue value, bool remove)
    {
        if (!stored_values.TryGetValue(variable, out var stored) || !users.TryGetValue(id, out var user)) return;
        if (user.IsBot || user.IsPet) return;
        if (remove) stored.Remove(id);
        else stored[id] = new(id, user.Name, user.Figure, value);
    }

    public WiredSimulationLeaderboardPage PreviewLeaderboard(long furniture_id, string? variable_id = null, int page = 1)
    {
        var preview = new WiredSimulation(snapshot(), options) { now = now };
        try { return preview.preview_leaderboard(furniture_id, variable_id, page); }
        catch (SimulationFailure error)
        {
            throw new InvalidOperationException(preview.diagnostics.LastOrDefault()?.Message ?? "The leaderboard cannot be predicted from this snapshot.", error);
        }
    }

    private WiredSimulationLeaderboardPage preview_leaderboard(long furniture_id, string? variable_id, int page)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        if (!nodes.TryGetValue(furniture_id, out var node) || node.Name != "VariableLeaderboard")
            throw new ArgumentException("The selected furniture is not a variable leaderboard.", nameof(furniture_id));
        var board = leaderboard_view(node);
        string selected = variable_id ?? board.DefaultVariableId ?? "";
        if (!board.Published) return new(board, selected, page, 0, []);
        if (!board.Choices.Any(choice => choice.VariableId == selected))
            throw new ArgumentException("The selected variable is not offered by this leaderboard.", nameof(variable_id));
        var definition = variables[selected];
        var rows = leaderboard_store(node, selected).Values.ToArray();
        if (node.Flag("filter_enabled"))
        {
            string filter = node.Text("filter_variable");
            if (!variables.TryGetValue(filter, out var filter_definition) || filter_definition.Domain != WiredSourceDomain.User || filter_definition.Availability is not (10 or 11))
                unsupported(node, "Leaderboard filtering requires a permanent user variable.");
            var filter_values = leaderboard_store(node, filter);
            long? comparison = null;
            if (node.Flag("comparison_enabled"))
            {
                if (!filter_definition!.HasValue) unsupported(node, "A leaderboard comparison requires a value-bearing filter variable.");
                comparison = node.Number("operand_kind") switch
                {
                    0 => leaderboard_literal(node),
                    1 => value(WiredSourceDomain.Global, 0, node.Text("global_operand_variable"), new(0, new(WiredSimulationEventKind.Start), [], [], [], []))?.Value,
                    _ => null
                };
                if (comparison is null) unsupported(node, "The leaderboard comparison operand is unavailable.");
            }
            rows = rows.Where(row => filter_values.TryGetValue(row.UserId, out var filter_value) &&
                (!comparison.HasValue || exact_value(filter_value.Value, filter, true)!.Value == comparison.Value)).ToArray();
        }
        long sort_key(WiredSimulationStoredUserValue row) => node.Number("sort_mode") switch
        {
            0 or 1 when definition.HasValue => exact_value(row.Value, selected, true)!.Value,
            2 or 3 => known_timestamp(row.Value, true, node),
            4 or 5 => known_timestamp(row.Value, false, node),
            _ => throw new InvalidOperationException("The selected leaderboard sorting mode is not supported by its variable.")
        };
        if (rows.GroupBy(sort_key).Any(group => group.Count() > 1))
            unsupported(node, "Equal leaderboard sort keys require a verified tie-break; user IDs do not establish server order.");
        var sorted = node.Number("sort_mode") is 0 or 3 or 5 ? rows.OrderByDescending(sort_key) : rows.OrderBy(sort_key);
        long offset = checked(((long)page - 1) * board.PageSize);
        var entries = sorted.Skip((int)Math.Min(offset, int.MaxValue)).Take(board.PageSize).Select((row, index) => leaderboard_row(node, definition, row, checked((int)offset + index + 1))).ToArray();
        return new(board, selected, page, rows.Length, entries);
    }

    private Dictionary<long, WiredSimulationStoredUserValue> leaderboard_store(WiredSimulationNode node, string variable)
    {
        if (!complete_stores.Contains(variable) || !stored_values.TryGetValue(variable, out var rows))
        {
            unsupported(node, "Leaderboard ranking requires complete StoredVariables for " + variable + "; room occupants are not the full persistent dataset.");
            throw new SimulationFailure();
        }
        return rows;
    }

    private static long leaderboard_literal(WiredSimulationNode node)
    {
        string text = node.Text("literal_operand");
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number) || number.ToString(CultureInfo.InvariantCulture) != text)
            throw new ArgumentException("The leaderboard literal must be a canonical signed 64-bit integer.");
        return number;
    }

    private WiredSimulationLeaderboardView leaderboard_view(WiredSimulationNode node)
    {
        if (node.Text("title").Length is < 1 or > 50 || node.Text("description").Length > 80 ||
            node.Number("offset_x") is < -32 or > 32)
            unsupported(node, "Leaderboard title, description or horizontal offset is outside the supported range.");
        string[] text = [node.Text("title"), node.Text("description"), .. Enumerable.Range(1, 4).Select(index => node.Text("column_" + index + "_label")),
            node.Text("literal_operand"), .. Enumerable.Range(1, 10).Select(index => node.Text("display_name_" + index))];
        if (text.Any(value => value.Contains('\t')) || Encoding.UTF8.GetByteCount(string.Join('\t', text)) > 1000)
            unsupported(node, "Leaderboard text must fit 1000 UTF-8 bytes and cannot contain embedded tabs.");
        var bindings = new List<(WiredSimulationNode Node, WiredSimulationVariable Variable)>();
        foreach (var box in siblings(node).Where(box => box.Name == "UserVariable").OrderBy(box => furniture[box.FurnitureId].Z).ThenBy(box => box.FurnitureId))
        {
            var matches = variables.Values.Where(variable => variable.Domain == WiredSourceDomain.User && variable.Availability is 10 or 11 &&
                (variable.DefinitionFurnitureId == box.FurnitureId || variable.DefinitionFurnitureId is null && variable.Name == box.Text("name"))).ToArray();
            if (matches.Length > 1) unsupported(node, "A stacked variable definition is ambiguous.");
            if (matches.Length == 1) bindings.Add((box, matches[0]));
        }
        if (bindings.GroupBy(binding => furniture[binding.Node.FurnitureId].Z).Any(group => group.Count() > 1))
            unsupported(node, "Stacked leaderboard variables with equal heights require separate ordering conformance.");
        var selected = bindings.Take(10).Select(binding => binding.Variable).ToArray();
        var names = selected.ToDictionary(variable => variable.Id, variable => variable.DisplayName, StringComparer.Ordinal);
        long overrides = node.Number("display_override_count");
        if (overrides is < 0 or > 10) unsupported(node, "A leaderboard accepts at most ten display overrides.");
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 1; index <= overrides; index++)
        {
            string variable = node.Text("display_variable_" + index), name = node.Text("display_name_" + index);
            if (!names.ContainsKey(variable) || !assigned.Add(variable) || name.Length is < 1 or > 22)
                unsupported(node, "Leaderboard display overrides require distinct stacked variable IDs and names of 1..22 characters.");
            names[variable] = name;
        }
        if (names.Values.Any(name => string.IsNullOrEmpty(name))) unsupported(node, "Supply each variable's observed DisplayName or a leaderboard display override.");
        long count = node.Number("column_count");
        if (count is < 1 or > 4) unsupported(node, "A leaderboard needs one to four columns.");
        var columns = new List<WiredLeaderboardColumn>();
        for (int index = 1; index <= count; index++)
        {
            int type = checked((int)node.Number("column_" + index + "_type")), width = checked((int)node.Number("column_" + index + "_width"));
            string label = node.Text("column_" + index + "_label");
            if (type is < 0 or > 5 || width is < 0 or > 3 || label.Length is < 2 or > 12 || columns.Any(column => column.Type == type))
                unsupported(node, "Leaderboard columns require supported distinct types, widths 0..3 and labels of 2..12 characters.");
            columns.Add(new(type, label, width));
        }
        int page_size = node.Number("page_size_option") switch { 0 => 10, 1 => 15, 2 => 20, _ => 0 };
        if (page_size == 0 || node.Number("sort_mode") is < 0 or > 5) unsupported(node, "Unknown leaderboard pagination or sorting mode.");
        return new(node.FurnitureId, node.Text("title"), node.Text("description"),
            new(checked((int)node.Number("tile_x")), checked((int)node.Number("tile_y")), checked((int)node.Number("offset_x")), checked((int)node.Number("altitude"))),
            (WiredSortOrderOption)node.Number("sort_mode"), columns.ToArray(), selected.Select(variable => new WiredLeaderboardChoice(variable.Id, names[variable.Id]!)).ToArray(),
            selected.FirstOrDefault()?.Id, page_size, selected.Length > 0);
    }

    private WiredLeaderboardRow leaderboard_row(WiredSimulationNode node, WiredSimulationVariable definition, WiredSimulationStoredUserValue row, int rank)
    {
        bool shows(int type) => Enumerable.Range(1, checked((int)node.Number("column_count"))).Any(index => node.Number("column_" + index + "_type") == type);
        bool created = shows(3), updated = shows(2);
        if ((created || updated) && initial.Epoch is null) unsupported(node, "Leaderboard timestamp projection requires an explicit room Epoch.");
        long origin = initial.Epoch?.ToUnixTimeMilliseconds() ?? 0;
        long number = shows(1) || shows(4) || shows(5) ? exact_value(row.Value, definition.Name, true)!.Value : 0;
        var levels = shows(4) ? siblings(node).Where(box => box.Name == "VariableLevelUp").ToArray() : [];
        if (levels.Length > 1) unsupported(node, "Several leaderboard level calculators require separate applicability conformance.");
        string? level = levels.Length == 1 ? level_value(levels[0], 0, number)?.Value.ToString(CultureInfo.InvariantCulture) : null;
        string? text = !shows(5) || definition.Text is null ? null : definition.Text.GetValueOrDefault(number, definition.TextFallback ?? number.ToString(CultureInfo.InvariantCulture));
        if (shows(4) && level is null)
            unsupported(node, "A level column needs a verified applicable level-up calculator.");
        return new(rank, checked((int)row.UserId), row.Name, row.Figure, shows(1) && definition.HasValue ? number.ToString(CultureInfo.InvariantCulture) : "",
            created ? checked(origin + known_timestamp(row.Value, true, node)) : 0,
            updated ? checked(origin + known_timestamp(row.Value, false, node)) : 0, level, text);
    }
}
