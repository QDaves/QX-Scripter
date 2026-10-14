namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private sealed record LimitWindow(Queue<long> Activations, Queue<long> ConditionFailures, bool Complete, long ObservedSince);

    private readonly Dictionary<long, LimitWindow> limited = [];

    private static IReadOnlyList<WiredSimulationLimitHistory>? copy_limit_history(IReadOnlyList<WiredSimulationLimitHistory>? histories)
    {
        if (histories is null) return null;
        return histories.Select(history => history is null || history.Activations is null
            ? throw new ArgumentException("Execution limit history requires a nonnull activation list.", nameof(histories))
            : history with { Activations = history.Activations.ToArray(), ConditionFailures = history.ConditionFailures?.ToArray() }).ToArray();
    }

    private void restore_limit_history()
    {
        foreach (var history in initial.LimitHistory ?? [])
            if (!limited.TryAdd(history.AddonId, new(new(history.Activations), new(history.ConditionFailures ?? []), history.Complete, history.ObservedSince)))
                fail("snapshot.limit_history", "Execution limit history contains duplicate add-ons.", history.AddonId);
    }

    private void validate_limit_history()
    {
        long entries = 0;
        foreach (var pair in limited)
        {
            var history = pair.Value;
            entries += history.Activations.Count + history.ConditionFailures.Count;
            if (!nodes.TryGetValue(pair.Key, out var addon) || addon.Name != "ExecutionLimit" || history.Activations.Count > 100 || history.ConditionFailures.Count > 100 ||
                history.ObservedSince > 0 ||
                history.Activations.Concat(history.ConditionFailures).Any(time => time > 0 || !history.Complete && time < history.ObservedSince) ||
                !history.Activations.SequenceEqual(history.Activations.Order()) || !history.ConditionFailures.SequenceEqual(history.ConditionFailures.Order()))
                fail("snapshot.limit_history", "Execution limit history requires a matching add-on and ordered past activation times within the declared observation period.", pair.Key);
        }
        if (entries > options.MaximumEvents)
            fail("snapshot.limit_history", "Execution limit history exceeds the configured event capacity.");
    }

    private bool limit_allows(WiredSimulationNode addon, bool condition_failed = false, bool reserve = true)
    {
        if (!limited.TryGetValue(addon.FurnitureId, out var history))
            limited[addon.FurnitureId] = history = new(new(), new(), false, 0);
        long window = Math.Clamp(addon.Number("window_pulses"), 1, 20) * options.PulseMilliseconds;
        while (history.Activations.TryPeek(out long time) && time <= now - window) history.Activations.Dequeue();
        while (history.ConditionFailures.TryPeek(out long time) && time <= now - window) history.ConditionFailures.Dequeue();
        if (history.Activations.Count >= addon.Number("execution_count", 1))
        {
            record("limited", addon.FurnitureId, "Execution limiter blocked the stack.");
            return false;
        }
        if (condition_failed && history.ConditionFailures.Count >= addon.Number("execution_count", 1))
        {
            record("limited.failure", addon.FurnitureId, "Execution limiter blocked the condition-failure branch.");
            return false;
        }
        if (!history.Complete && history.ObservedSince > now - window)
            uncertain("limit.history_unknown", "Execution limits depend on prior accepted activations. Supply LimitHistory or wait until the unobserved window expires.", addon.FurnitureId);
        if (reserve) (condition_failed ? history.ConditionFailures : history.Activations).Enqueue(now);
        return true;
    }

    private IReadOnlyList<WiredSimulationLimitHistory> limit_history() => nodes.Values.Where(node => node.Name == "ExecutionLimit")
        .Select(node => node.FurnitureId).Concat(limited.Keys).Distinct().Order().Select(id =>
    {
        var history = limited.GetValueOrDefault(id) ?? new(new(), new(), false, 0);
        long window = nodes.TryGetValue(id, out var addon) ? Math.Clamp(addon.Number("window_pulses"), 1, 20) * options.PulseMilliseconds : 0;
        bool complete = history.Complete || history.ObservedSince <= now - window;
        return new WiredSimulationLimitHistory(id, history.Activations.Where(time => time > now - window).Select(time => time - now).ToArray(),
            complete, complete ? 0 : Math.Max(history.ObservedSince, now - window) - now,
            history.ConditionFailures.Where(time => time > now - window).Select(time => time - now).ToArray());
    }).ToArray();
}
