namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private long? timer_origin;
    private long? timer_reset_at;
    private bool timer_reset_queued;
    private bool timers_initialized;

    private void restore_timer(WiredSimulationTimer? timer)
    {
        if (timer is null) return;
        if (timer.Value < 0 || timer.Value > (long.MaxValue - options.MaximumTimeMilliseconds) / options.PulseMilliseconds - 1 ||
            timer.TickOffsetMilliseconds < 0 || timer.TickOffsetMilliseconds >= options.PulseMilliseconds)
            throw new ArgumentException("Invalid room timer metadata.");
        long offset = timer.TickOffsetMilliseconds == 0 ? options.PulseMilliseconds : timer.TickOffsetMilliseconds;
        timer_origin = checked(offset - (timer.Value + 1) * options.PulseMilliseconds);
        if (timer.ResetPending) timer_reset_at = timer.TickOffsetMilliseconds;
    }

    private long room_timer_value()
    {
        if (!timer_origin.HasValue)
        {
            note(WiredSimulationSeverity.Unsupported, "timer.state_unknown", "Supply the room timer value and next tick offset, or explicitly initialize a fresh simulation with Start.");
            status = WiredSimulationStatus.Faulted;
            throw new SimulationFailure();
        }
        return now < timer_origin.Value ? 0 : (now - timer_origin.Value) / options.PulseMilliseconds;
    }

    private bool timer_condition(WiredSimulationNode node, bool more)
    {
        if (timer_reset_at.HasValue)
            uncertain("timer.condition_reset_pending", "An elapsed-time condition evaluated during a pending room reset can precede an action's timer read. Confirm the reset tick before comparing this transition.", node.FurnitureId);
        return more ? room_timer_value() >= node.Number("time_threshold") : room_timer_value() <= node.Number("time_threshold");
    }

    private long timer_next_tick()
    {
        long origin = timer_origin!.Value;
        if (origin > now) return origin;
        return checked(origin + ((now - origin) / options.PulseMilliseconds + 1) * options.PulseMilliseconds);
    }

    private WiredSimulationTimer? timer_snapshot() => timer_origin.HasValue
        ? new(room_timer_value(), (int)(((timer_reset_at ?? timer_next_tick()) - now) % options.PulseMilliseconds), timer_reset_at.HasValue)
        : null;

    private static bool timer_trigger(WiredSimulationNode node) =>
        node.Name is "TriggerOnce" or "TriggerPeriodically" or "PeriodicShort" or "PeriodicLong";

    private void schedule_timer(WiredSimulationNode trigger, Frame frame, bool include_current = false)
    {
        long interval = trigger.Name switch
        {
            "TriggerOnce" => trigger.Number("delay_pulses") * options.PulseMilliseconds,
            "TriggerPeriodically" => trigger.Number("interval_pulses") * options.PulseMilliseconds,
            "PeriodicShort" => trigger.Number("interval_units") * options.ShortPulseMilliseconds,
            "PeriodicLong" => trigger.Number("interval_units") * 5000,
            _ => 0
        };
        if (interval <= 0) return;
        long at;
        if (trigger.Name == "TriggerOnce") at = checked(timer_origin!.Value + interval);
        else if (include_current && timer_origin == now) at = now;
        else at = checked(timer_origin!.Value + ((now - timer_origin.Value) / interval + 1) * interval);
        if (timer_reset_at is { } reset && at >= reset) return;
        if (at > now || include_current && at == now) enqueue(at, copy(frame) with { Root = ++root_sequence }, trigger: trigger.FurnitureId);
    }

    private void reset_timers(WiredSimulationNode node)
    {
        if (!timer_origin.HasValue)
            uncertain("timer.phase_unknown", "Reset Timers needs a supplied room timer tick phase or a fresh Start event.", node.FurnitureId);
        timer_origin ??= now;
        timer_reset_at ??= timer_next_tick();
        schedule_timer_reset();
        record("timer.reset_requested", node.FurnitureId, "Room timer reset requested for the next timer tick; delayed effects retained.");
    }

    private void schedule_timer_reset()
    {
        if (timer_reset_at is not { } at || timer_reset_queued) return;
        var frame = new Frame(++root_sequence, new(WiredSimulationEventKind.Start, now), new(StringComparer.Ordinal), [], [], []);
        enqueue(at, frame, immediate: true, timer_reset: true);
        timer_reset_queued = true;
    }

    private void apply_timer_reset()
    {
        var pending = queue.UnorderedItems.Where(entry => !entry.Element.TimerReset && (entry.Element.Trigger is not { } id ||
            !nodes.TryGetValue(id, out var trigger) || !timer_trigger(trigger))).ToArray();
        queue.Clear();
        foreach (var entry in pending) queue.Enqueue(entry.Element, entry.Priority);
        timer_origin = now;
        timer_reset_at = null;
        timer_reset_queued = false;
        timers_initialized = true;
        var frame = new Frame(++root_sequence, new(WiredSimulationEventKind.Start, now), new(StringComparer.Ordinal), [], [], []);
        foreach (var trigger in nodes.Values.Where(timer_trigger)) schedule_timer(trigger, frame, include_current: true);
        record("timer.reset_applied", null, "Room timer reached zero on its next tick.");
    }
}
