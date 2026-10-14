namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private int? clock_phase;
    private bool clock_tick_pending;

    private WiredSimulationClock clock_state(WiredSimulationFurniture item, WiredSimulationNode node)
    {
        if (item.Clock is not { } clock)
        {
            unsupported(node, "Clock state requires explicit elapsed time, running state and initialization history; visible furniture state alone is insufficient.");
            throw new SimulationFailure();
        }
        return clock;
    }

    private static long clock_threshold(WiredSimulationNode node) =>
        checked(node.Number("minutes") * 60000 + node.Number("seconds") * 1000 + node.Number("half_second") * 500);

    private void control_clock(WiredSimulationNode node, Frame frame)
    {
        foreach (long id in fs(node, frame))
        {
            var item = furniture[id];
            var clock = clock_state(item, node);
            bool controls_game = game_clock(item, node);
            switch (node.Number("clock_control"))
            {
                case 0:
                    if (controls_game && !clock.Initialized && clock.ElapsedMilliseconds != 0)
                        unsupported(node, "Starting a game-aware clock with a preset nonzero value requires separate conformance.");
                    require_clock_phase(node.FurnitureId);
                    clock = clock with { ElapsedMilliseconds = clock.Initialized ? 0 : clock.ElapsedMilliseconds, Running = true, Initialized = true };
                    break;
                case 1: case 3:
                    clock = clock with { Running = false };
                    break;
                case 2:
                    if (clock.Initialized) clock = clock with { ElapsedMilliseconds = 0, Running = false, Initialized = false };
                    break;
                case 4:
                    if (controls_game && !clock.Initialized) break;
                    if (!clock.Initialized) unsupported(node, "Resuming a never-started clock requires separate server conformance.");
                    require_clock_phase(node.FurnitureId);
                    clock = clock with { Running = true };
                    break;
                default: unsupported(node); break;
            }
            write_clock(item, clock, frame, "clock.control");
            if (controls_game) game_clock_control(node, id);
        }
        reschedule_clock();
    }

    private void adjust_clock(WiredSimulationNode node, Frame frame)
    {
        long amount = clock_threshold(node);
        foreach (long id in fs(node, frame))
        {
            var item = furniture[id];
            var clock = clock_state(item, node);
            long value = node.Number("operation") switch
            {
                0 => checked(clock.ElapsedMilliseconds + amount),
                1 => Math.Max(0, clock.ElapsedMilliseconds - amount),
                2 => amount,
                _ => throw new InvalidOperationException("Unknown clock adjustment.")
            };
            if (value > 5999500) unsupported(node, "Clock overflow above 99:59.5 requires separate server conformance.");
            write_clock(item, clock with { ElapsedMilliseconds = value }, frame, "clock.adjust");
        }
    }

    private void write_clock(WiredSimulationFurniture item, WiredSimulationClock clock, Frame frame, string kind)
    {
        furniture[item.Id] = item with { Clock = clock, State = checked((int)(clock.ElapsedMilliseconds / 1000)) };
        charge(frame, 1, frame.Stack);
        record(kind, frame.Stack, item.Key, [item.Id], item.Clock?.ElapsedMilliseconds, clock.ElapsedMilliseconds);
    }

    private void require_clock_phase(long source)
    {
        if (clock_phase.HasValue) return;
        uncertain("clock.phase_unknown", "Clock execution uses a half-second tick phase that must be supplied or calibrated; a furniture snapshot does not reveal it.", source);
        clock_phase = 0;
    }

    private void schedule_clock()
    {
        if (clock_tick_pending || !furniture.Values.Any(item => item.Clock?.Running == true)) return;
        require_clock_phase(furniture.Values.First(item => item.Clock?.Running == true).Id);
        long delay = (clock_phase!.Value - now % 500 + 500) % 500;
        if (delay == 0) delay = 500;
        var frame = new Frame(++root_sequence, new(WiredSimulationEventKind.ClockReached), new(StringComparer.Ordinal), [], [], []);
        enqueue(checked(now + delay), frame, clock_tick: true);
        clock_tick_pending = true;
    }

    private void reschedule_clock()
    {
        if (!furniture.Values.Any(item => item.Clock?.Running == true) && clock_tick_pending)
        {
            var remaining = queue.UnorderedItems.Where(entry => !entry.Element.ClockTick).ToArray();
            queue.Clear();
            foreach (var entry in remaining) queue.Enqueue(entry.Element, entry.Priority);
            clock_tick_pending = false;
        }
        schedule_clock();
    }

    private void clock_tick()
    {
        clock_tick_pending = false;
        foreach (var item in furniture.Values.Where(item => item.Clock?.Running == true).ToArray())
        {
            var clock = item.Clock!;
            long elapsed = checked(clock.ElapsedMilliseconds + 500);
            if (elapsed > 5999500)
            {
                fail("clock.overflow_unverified", "Clock progression beyond 99:59.5 is not verified.", item.Id);
                throw new SimulationFailure();
            }
            var frame = new Frame(++root_sequence, new(WiredSimulationEventKind.ClockReached, now, FurnitureId: item.Id, NewValue: elapsed), new(StringComparer.Ordinal), [], [], []);
            write_clock(item, clock with { ElapsedMilliseconds = elapsed }, frame, "clock.tick");
            enqueue(now, frame, external: true);
        }
        schedule_clock();
    }
}
