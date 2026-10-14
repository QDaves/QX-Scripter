using Qx.Model.Messages.Incoming;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private sealed record ProjectileCell(int X, int Y, int Altitude, double CollisionAltitude, int EntryNumerator, int EntryDenominator);
    private sealed record ProjectileFlight(long Revision, long StartedAt, IReadOnlyList<ProjectileCell> Path, bool Scheduled = false);

    private void validate_projectiles()
    {
        if (initial.ProjectileTiming is { } timing && (timing.TickMilliseconds is <= 0 or > 1000 ||
            timing.NextTickOffsetMilliseconds < 0 || timing.NextTickOffsetMilliseconds >= timing.TickMilliseconds))
            fail("snapshot.projectile_clock", "Invalid projectile update clock.");
        foreach (var item in furniture.Values.Where(item => item.Projectile is not null))
        {
            var state = item.Projectile!;
            var movement = state.Movement;
            var furniture_hits = state.FurnitureHits;
            var user_hits = state.UserHits;
            if (movement is null || furniture_hits is null || user_hits is null)
            { fail("snapshot.projectile", "Incomplete projectile state.", item.Id); continue; }
            bool invalid = state.VariableMask is < 0 or > 127 || state.ElapsedMilliseconds < 0;
            if (!invalid)
            {
                invalid = movement.Type != WiredMovementType.FloorItem || movement.AnimationTime <= 0 ||
                    movement.OvershootDistance.HasValue || !float.IsFinite(movement.Source.Z) || !float.IsFinite(movement.Destination.Z) ||
                    movement.Source.X < 0 || movement.Source.X >= initial.Width || movement.Source.Y < 0 || movement.Source.Y >= initial.Height ||
                    movement.Destination.X < 0 || movement.Destination.X >= initial.Width || movement.Destination.Y < 0 || movement.Destination.Y >= initial.Height ||
                    Math.Abs(movement.Destination.X - (long)movement.Source.X) + Math.Abs(movement.Destination.Y - (long)movement.Source.Y) > options.MaximumEvents ||
                    state.ElapsedMilliseconds > movement.AnimationTime + 1000L ||
                    furniture_hits.Count > state.Cell || user_hits.Count > state.Cell ||
                    state.Travelling && (initial.ProjectileTiming is null || !nodes.TryGetValue(state.ModifierId, out var modifier) || modifier.Name != "Projectile");
            }
            if (!invalid)
            {
                try
                {
                    var path = projectile_path(movement);
                    invalid = state.Cell < 0 || state.Cell >= path.Count || state.Travelling != (state.Cell < path.Count - 1);
                    if (!invalid)
                    {
                        var cell = path[state.Cell];
                        invalid = state.X != cell.X || state.Y != cell.Y || state.Altitude != cell.Altitude;
                    }
                }
                catch (ArithmeticException) { invalid = true; }
            }
            if (invalid) fail("snapshot.projectile", "Invalid projectile state.", item.Id);
        }
    }

    private static IReadOnlyList<ProjectileCell> projectile_path(WiredSimulationMovement movement)
    {
        int dx = movement.Destination.X - movement.Source.X, dy = movement.Destination.Y - movement.Source.Y;
        double dz = movement.Destination.Z - movement.Source.Z;
        var cuts = new SortedSet<(int Numerator, int Denominator)>(Comparer<(int Numerator, int Denominator)>.Create(
            (left, right) => ((long)left.Numerator * right.Denominator).CompareTo((long)right.Numerator * left.Denominator))) { (0, 1), (1, 1) };
        for (int step = 0; step < Math.Abs(dx); step++) cuts.Add((step * 2 + 1, Math.Abs(dx) * 2));
        for (int step = 0; step < Math.Abs(dy); step++) cuts.Add((step * 2 + 1, Math.Abs(dy) * 2));
        var boundaries = cuts.ToArray();
        var path = new List<ProjectileCell>(boundaries.Length - 1);
        double length = Math.Sqrt((double)dx * dx + (double)dy * dy + dz * dz);
        for (int index = 0; index < boundaries.Length - 1; index++)
        {
            double point = ((double)boundaries[index].Numerator / boundaries[index].Denominator +
                (double)boundaries[index + 1].Numerator / boundaries[index + 1].Denominator) / 2;
            int x = checked((int)Math.Floor(movement.Source.X + dx * point + 0.5));
            int y = checked((int)Math.Floor(movement.Source.Y + dy * point + 0.5));
            float altitude = (float)(movement.Source.Z + dz * point + movement.CurveStrength.GetValueOrDefault() / 100.0 * length * point * (1 - point));
            if (index == 0) altitude = movement.Source.Z;
            if (index == boundaries.Length - 2) altitude = movement.Destination.Z;
            path.Add(new(x, y, checked((int)Math.Floor(altitude * 100f + 0.5)), altitude, boundaries[index].Numerator, boundaries[index].Denominator));
        }
        return path;
    }

    private WiredSimulationFurniture projectile_snapshot(WiredSimulationFurniture item)
    {
        if (item.Projectile is { Travelling: true } state && projectiles.TryGetValue(item.Id, out var flight))
            item = item with { Projectile = state with { ElapsedMilliseconds = checked(now - flight.StartedAt) } };
        return copy(item);
    }

    private void restore_projectiles()
    {
        foreach (var item in furniture.Values.Where(item => item.Projectile is { Travelling: true }))
        {
            var state = item.Projectile!;
            projectiles.Add(item.Id, new(++projectile_revision, -state.ElapsedMilliseconds, projectile_path(state.Movement)));
        }
    }

    private void schedule_projectiles()
    {
        foreach (long id in projectiles.Where(pair => !pair.Value.Scheduled).Select(pair => pair.Key).ToArray()) queue_projectile(id);
    }

    private void queue_projectile(long id)
    {
        if (!projectiles.TryGetValue(id, out var flight) || !furniture.TryGetValue(id, out var item) || item.Projectile is not { Travelling: true } state) return;
        if (state.Cell + 1 >= flight.Path.Count) return;
        var timing = initial.ProjectileTiming!;
        var cell = flight.Path[state.Cell + 1];
        long offset = ((long)state.Movement.AnimationTime * cell.EntryNumerator + cell.EntryDenominator - 1) / cell.EntryDenominator;
        long earliest = Math.Max(now, checked(flight.StartedAt + offset));
        long delay = (projectile_phase!.Value - earliest % timing.TickMilliseconds + timing.TickMilliseconds) % timing.TickMilliseconds;
        var frame = new Frame(++root_sequence, new(WiredSimulationEventKind.Start, now), [], [], [], [], state.ModifierId);
        enqueue(checked(earliest + delay), frame, projectile: new(id, flight.Revision));
        projectiles[id] = flight with { Scheduled = true };
    }

    private WiredSimulationFurniture start_projectile(WiredSimulationFurniture before, WiredSimulationFurniture after,
        WiredSimulationMovement? movement, IReadOnlyList<WiredSimulationNode> addons)
    {
        var modifier = addons.FirstOrDefault(node => node.Name == "Projectile");
        int mask = (int)(modifier?.Number("internal_variables") ?? 0);
        if (modifier is not null && (mask & ~127) != 0) unsupported(modifier, "Unknown projectile variable mask.");
        if (before.Projectile is { Travelling: true } active)
        {
            var prior = modifier ?? nodes.GetValueOrDefault(active.ModifierId);
            if (prior is null) throw new InvalidOperationException("An active projectile has no modifier.");
            if (movement is null || modifier is null || modifier.FurnitureId != active.ModifierId || mask != active.VariableMask)
                unsupported(prior, "Changing an active projectile's modifier or variable mask requires conformance.");
        }
        if (modifier is not null && before.Projectile is { } existing && existing.VariableMask != mask)
            unsupported(modifier, "Changing an existing projectile variable mask requires conformance.");
        if (mask == 0) return after;
        if (modifier is null || movement is null) throw new InvalidOperationException("Missing projectile motion.");
        if (initial.ProjectileTiming is null)
            unsupported(modifier, "Projectile variables require an explicit ProjectileTiming update clock.");
        if (movement.AnimationTime <= 0 || movement.OvershootDistance.HasValue || modifier.Number("speed_increase") != 0)
            unsupported(modifier, "Instant, ranged or accelerated projectile variables require further timeline conformance.");
        if (Math.Abs(movement.Destination.X - (long)movement.Source.X) + Math.Abs(movement.Destination.Y - (long)movement.Source.Y) > options.MaximumEvents)
            unsupported(modifier, "The projectile path exceeds the configured simulation event limit.");
        var path = projectile_path(movement);
        if (path.Count < 2) unsupported(modifier, "A projectile without horizontal cell transitions requires conformance.");
        var origin = path[0];
        var state = new WiredSimulationProjectileState(modifier.FurnitureId, movement, mask, 0, 0,
            origin.X, origin.Y, origin.Altitude, true, [], []);
        projectiles[before.Id] = new(++projectile_revision, now, path);
        return after with { Projectile = state };
    }

    private void projectile_step(ProjectileStep step, Frame frame)
    {
        if (!projectiles.TryGetValue(step.FurnitureId, out var flight) || flight.Revision != step.Revision) return;
        if (!furniture.TryGetValue(step.FurnitureId, out var item) || item.Projectile is not { Travelling: true } state)
        { projectiles.Remove(step.FurnitureId); return; }
        var modifier = nodes.GetValueOrDefault(state.ModifierId);
        if (modifier is null) throw new InvalidOperationException("A projectile modifier was removed during flight.");
        int next = state.Cell + 1;
        var cell = flight.Path[next];
        var previous = state;
        var contacts = furniture.Values.Where(other => other.Id != item.Id && covers(other, cell.X, cell.Y) &&
            cell.CollisionAltitude >= (float)other.Z && cell.CollisionAltitude <= (float)(other.Z + other.Height)).Select(other => other.Id).ToArray();
        if (contacts.Length > 1)
            unsupported(modifier, "Overlapping projectile collision geometry requires conformance.");
        long[] user_contacts = [];
        if ((state.VariableMask & 2) != 0)
        {
            if (users.Values.Any(user => projectile_user_unverified(user) &&
                (user.X == cell.X && user.Y == cell.Y || user.NextStep is { } step && step.X == cell.X && step.Y == cell.Y)))
                unsupported(modifier, "Projectile contacts with moving or seated bots and pets require conformance.");
            var occupants = users.Values.Select(user => (User: user, Position: projectile_user_position(user)))
                .Where(entry => entry.Position.X == cell.X && entry.Position.Y == cell.Y).ToArray();
            if (occupants.Length > 1 || occupants.Any(entry => projectile_user_unverified(entry.User) ||
                entry.User.NextStep is { } target && Math.Abs(target.Z - entry.User.Z) > 0.001))
                unsupported(modifier, "Projectile contacts with moving or seated bots, pets, overlapping avatars or walking height changes require conformance.");
            user_contacts = occupants.Where(entry => cell.CollisionAltitude >= entry.Position.Z && cell.CollisionAltitude <= entry.Position.Z + 2.1)
                .Select(entry => entry.User.Id).ToArray();
        }
        state = state with { Cell = next, X = cell.X, Y = cell.Y, Altitude = cell.Altitude,
            ElapsedMilliseconds = checked(now - flight.StartedAt), Travelling = next < flight.Path.Count - 1,
            FurnitureHits = state.FurnitureHits.Concat(contacts).ToArray(),
            UserHits = state.UserHits.Concat(user_contacts).ToArray() };
        furniture[item.Id] = item with { Projectile = state };
        charge(frame, 1, state.ModifierId);
        record("projectile.cell", state.ModifierId, $"{cell.X},{cell.Y},{cell.Altitude}", [item.Id]);
        projectile_updates(item.Id, previous, state, frame);
        if (state.Travelling) projectiles[item.Id] = flight with { Scheduled = false };
        else projectiles.Remove(item.Id);
        queue_projectile(item.Id);
    }

    private static (int X, int Y, double Z) projectile_user_position(WiredSimulationUser user) =>
        user.NextStep is { } target ? (target.X, target.Y, target.Z) : (user.X, user.Y, user.Z);

    private static bool projectile_user_unverified(WiredSimulationUser user) =>
        user.IsPet || user.IsBot && (user.NextStep is not null || user.Stance != Qx.Model.AvatarStance.Stand);

    private void projectile_updates(long id, WiredSimulationProjectileState previous, WiredSimulationProjectileState current, Frame frame)
    {
        foreach (var variable in variables.Values.Where(variable => variable.Domain == WiredSourceDomain.Furniture && variable.Name.StartsWith("@projectile.animation.", StringComparison.Ordinal)))
        {
            long? before = projectile_value(previous, variable.Name), after = projectile_value(current, variable.Name);
            if (before == after) continue;
            fx_changes.Add((WiredSourceDomain.Furniture, id, variable.Id));
            if (variable.Name is "@projectile.animation.position.x" or "@projectile.animation.position.y" or "@projectile.animation.position.altitude") continue;
            enqueue(now, frame with { Event = new(WiredSimulationEventKind.VariableChanged, now, FurnitureId: id,
                Variable: variable.Id, OldValue: before, NewValue: after, Change: after.HasValue ? 2 : 4), InlineActions = true }, external: true);
        }
    }

    private static long? projectile_value(WiredSimulationProjectileState? state, string name)
    {
        if (state is null) return null;
        var value = name switch
        {
            "@projectile.animation.tiles_travelled" => (Bit: 1, Value: (long?)state.Cell),
            "@projectile.animation.user_collisions" => (Bit: 2, Value: (long?)state.UserHits.Count),
            "@projectile.animation.furni_collisions" => (Bit: 4, Value: (long?)state.FurnitureHits.Count),
            "@projectile.animation.position.x" => (Bit: 8, Value: (long?)state.X),
            "@projectile.animation.position.y" => (Bit: 16, Value: (long?)state.Y),
            "@projectile.animation.position.altitude" => (Bit: 32, Value: (long?)state.Altitude),
            "@projectile.animation.is_travelling" => (Bit: 64, Value: state.Travelling ? -1L : (long?)null),
            _ => (Bit: 0, Value: (long?)null)
        };
        return (state.VariableMask & value.Bit) != 0 ? value.Value : null;
    }

    private static bool projectile_variable(string name) => name is "@projectile.animation.tiles_travelled" or
        "@projectile.animation.user_collisions" or "@projectile.animation.furni_collisions" or "@projectile.animation.position.x" or
        "@projectile.animation.position.y" or "@projectile.animation.position.altitude" or "@projectile.animation.is_travelling";
}
