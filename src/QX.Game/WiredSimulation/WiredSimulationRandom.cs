namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private readonly Dictionary<long, WiredSimulationRandomHistory> random_histories = [];
    private int random_choice;

    private static IReadOnlyList<WiredSimulationRandomChoice>? copy_random_choices(IReadOnlyList<WiredSimulationRandomChoice>? choices)
    {
        if (choices is null) return null;
        return choices.Select(choice => choice is null || choice.ActionIds is null
            ? throw new ArgumentException("Random choices require a nonnull action list.", nameof(choices))
            : choice with { ActionIds = choice.ActionIds.ToArray() }).ToArray();
    }

    private static IReadOnlyList<WiredSimulationRandomHistory>? copy_random_history(IReadOnlyList<WiredSimulationRandomHistory>? histories)
    {
        if (histories is null) return null;
        return histories.Select(history => history is null || history.Executions is null || history.Executions.Any(batch => batch is null)
            ? throw new ArgumentException("Random history requires nonnull execution batches.", nameof(histories))
            : history with { Executions = history.Executions.Select(batch => (IReadOnlyList<long>)batch.ToArray()).ToArray() }).ToArray();
    }

    private void restore_random_history()
    {
        foreach (var history in initial.RandomHistory ?? [])
            if (!random_histories.TryAdd(history.AddonId, history))
                fail("snapshot.random_history", "Random history contains duplicate add-ons.", history.AddonId);
    }

    private void validate_random_history()
    {
        long entries = 0;
        foreach (var history in random_histories.Values)
        {
            entries += history.Executions.Sum(batch => (long)batch.Count);
            if (!nodes.TryGetValue(history.AddonId, out var addon) || addon.Name != "Random" || history.Executions.Count > 100 ||
                history.Executions.Any(batch => batch.Count is < 1 or > 100 || batch.Count != batch.Distinct().Count()))
                fail("snapshot.random_history", "Random history requires a matching Random add-on and up to 100 distinct-action batches.", history.AddonId);
        }
        if (entries > options.MaximumEvents)
            fail("snapshot.random_history", "Random history exceeds the configured event capacity.");
    }

    private List<WiredSimulationNode> random_actions(WiredSimulationNode addon, List<WiredSimulationNode> actions)
    {
        if (addon.Number("skip_recent") is < 0 or > 100 || addon.Number("pick_count", 1) is < 1 or > 100)
            unsupported(addon, "Random skip_recent must be 0..100 and pick_count must be 1..100.");
        int skip = checked((int)addon.Number("skip_recent"));
        int count = (int)Math.Clamp(addon.Number("pick_count", 1), 1, actions.Count);
        var history = random_histories.GetValueOrDefault(addon.FurnitureId) ?? new(addon.FurnitureId, [], false);
        if (skip > 0 && !history.Complete && history.Executions.Count < skip)
            uncertain("random.history_unknown", "Recent Random executions are not recoverable from settings. Supply RandomHistory or begin from a known fresh add-on.", addon.FurnitureId);
        var recent = history.Executions.TakeLast(skip).Reverse().ToArray();
        var ranks = actions.ToDictionary(action => action.FurnitureId, action =>
        {
            for (int age = 0; age < recent.Length; age++)
                if (recent[age].Contains(action.FurnitureId)) return age;
            return recent.Length;
        });
        int threshold = ranks.Values.OrderDescending().ElementAt(count - 1);
        int highest = ranks.Values.Max();
        List<WiredSimulationNode> selected;
        if (options.RandomChoices is { } choices && random_choice < choices.Count)
        {
            var choice = choices[random_choice];
            if (choice.AddonId != addon.FurnitureId || choice.ActionIds.Count != count || choice.ActionIds.Distinct().Count() != count ||
                choice.ActionIds.Any(id => !ranks.TryGetValue(id, out int rank) || rank < threshold) ||
                ranks.Any(pair => pair.Value > threshold && !choice.ActionIds.Contains(pair.Key)))
                unsupported(addon, "The next Random choice must select the required distinct effects and prefer effects outside the recent history.");
            selected = choice.ActionIds.Select(id => actions.Single(action => action.FurnitureId == id)).ToList();
            random_choice++;
        }
        else if (count == 1 && ranks.Count(pair => pair.Value == highest) == 1)
            selected = [actions.Single(action => ranks[action.FurnitureId] == highest)];
        else
        {
            record("random.candidates", addon.FurnitureId, "Eligible effects are ranked by their most recent execution.", ranks.Where(pair => pair.Value >= threshold).Select(pair => pair.Key).ToArray());
            uncertain("random.choice", "Supply RandomChoices for a known selection and execution order. A permissive run samples one admissible branch, not the hotel's random generator.", addon.FurnitureId);
            selected = actions.OrderByDescending(action => ranks[action.FurnitureId]).ThenBy(_ => next_random()).Take(count).ToList();
        }
        var executions = history.Executions.Append((IReadOnlyList<long>)selected.Select(action => action.FurnitureId).ToArray()).TakeLast(100).ToArray();
        random_histories[addon.FurnitureId] = history with { Executions = executions, Complete = history.Complete || executions.Length == 100 };
        record("random.selection", addon.FurnitureId, "Selected a valid Random effect batch.", selected.Select(action => action.FurnitureId).ToArray());
        return selected;
    }
}
