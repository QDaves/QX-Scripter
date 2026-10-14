using Qx.Model.Messages.Incoming;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private WiredSimulationMovement? projectile_motion(IReadOnlyList<WiredSimulationNode> addons,
        WiredSimulationFurniture before, WiredSimulationFurniture after, Frame frame, bool transfer)
    {
        var projectiles = addons.Where(node => node.Name == "Projectile").ToArray();
        if (projectiles.Length == 0) return null;
        var projectile = projectiles[0];
        if (projectiles.Length > 1) unsupported(projectile, "Competing projectile add-ons require separate precedence conformance.");
        if (!transfer || before.Width != 1 || before.Length != 1)
            unsupported(projectile, "This projectile movement family requires separate conformance.");
        if (addons.Any(node => node.Name is "NoMoveAnimation" or "JumpStrength"))
            unsupported(projectile, "Projectile combinations with animation cancellation or a separate movement curve require conformance.");
        if (projectile.Flag("bunny_hop"))
            unsupported(projectile, "Projectile bunny-hop timing requires conformance.");
        double duration = movement_duration(addons);
        long travel_time = 0;
        double travel_distance = 0;
        if (projectile.Flag("scale_time"))
        {
            var time = operand(projectile, frame, "time_operand_kind", "time_per_tile", "time_target", "time_variable", 1);
            if (time is null or < 0) unsupported(projectile, "Missing or negative projectile travel time requires conformance.");
            double dx = projectile.Flag("distance_x") ? after.X - (double)before.X : 0;
            double dy = projectile.Flag("distance_y") ? after.Y - (double)before.Y : 0;
            double dz = projectile.Flag("distance_z") ? after.Z - before.Z : 0;
            travel_time = time!.Value;
            travel_distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double scaled = projectile_duration(travel_time, travel_distance, projectile.Number("speed_increase"));
            if (!double.IsFinite(scaled) || scaled > int.MaxValue)
                unsupported(projectile, "Projectile travel time exceeds the native animation integer range.");
            duration = scaled;
        }
        int? overshoot = null;
        int distance_mode = checked((int)projectile.Number("distance_mode"));
        if (distance_mode is < 0 or > 2) unsupported(projectile, "Unknown projectile distance mode.");
        if (distance_mode != 0)
        {
            var distance = operand(projectile, frame, "distance_operand_kind", "distance", "distance_target", "distance_variable", 2);
            if (distance is null) unsupported(projectile, "Missing projectile distance operand requires conformance.");
            if (distance != 0)
            {
                double steps = Math.Max(Math.Abs(after.X - (double)before.X), Math.Abs(after.Y - (double)before.Y));
                if (steps == 0) unsupported(projectile, "Projectile range changes without horizontal displacement require conformance.");
                double extra = distance_mode == 1 ? distance!.Value : distance!.Value - steps;
                double factor = 1 + extra / steps;
                if (factor == 0) unsupported(projectile, "A zero-length projectile trajectory requires conformance.");
                double grid_length = Math.Abs(after.X - (double)before.X) + Math.Abs(after.Y - (double)before.Y);
                double extended_length = grid_length + grid_length * extra / steps;
                double total = projectile.Flag("scale_time")
                    ? projectile_duration(travel_time, travel_distance * factor, projectile.Number("speed_increase"))
                    : duration;
                if (!double.IsFinite(total) || total is < int.MinValue or > int.MaxValue)
                    unsupported(projectile, "Projectile animation time exceeds the native integer range.");
                int total_time = (int)total;
                double extended = total_time - total_time * (grid_length / extended_length);
                if (!double.IsFinite(extended) || extended is < int.MinValue or > int.MaxValue)
                    unsupported(projectile, "Projectile overshoot time exceeds the native integer range.");
                int extra_time = (int)extended;
                duration = (long)total_time - extra_time;
                if (duration is < int.MinValue or > int.MaxValue)
                    unsupported(projectile, "Projectile movement time exceeds the native integer range.");
                if (extra_time != 0) overshoot = extra_time;
            }
        }
        int direction = before.Direction;
        if (projectile.Flag("rotate_projectile"))
            direction = projectile_direction(projectile, before, after);
        int curve = checked((int)projectile.Number("curve_strength"));
        return new(WiredMovementType.FloorItem, position(before), position(after), checked((int)duration),
            direction, CurveStrength: curve == 0 ? null : curve, OvershootDistance: overshoot);
    }

    private static double projectile_duration(long time, double distance, long increase)
    {
        double duration = time * distance;
        if (increase <= 0 || time <= increase || distance <= 1) return duration;
        double accelerated = Math.Min(distance, (double)time / increase);
        double reduction = accelerated * (accelerated - 1) * increase / 2;
        return duration - Math.Truncate(reduction);
    }

    private (WiredSimulationUser User, int Direction)[] projectile_shooters(IReadOnlyList<WiredSimulationNode> addons,
        WiredSimulationFurniture before, WiredSimulationFurniture after, Frame frame)
    {
        var projectile = addons.FirstOrDefault(node => node.Name == "Projectile");
        if (projectile is null || !projectile.Flag("rotate_projectile") || !projectile.Flag("rotate_shooter")) return [];
        var shooters = us(projectile, frame);
        if (shooters.Length > 1 || shooters.Any(id => users[id].IsBot || users[id].IsPet || users[id].NextStep is not null))
            unsupported(projectile, "Multiple, walking or non-human projectile shooters require separate conformance.");
        int dx = after.X - before.X, dy = after.Y - before.Y;
        if (dx == 0 && dy == 0) return [];
        int direction = ((int)(Math.Atan2(Math.Sign(dy), Math.Sign(dx)) * 4 / Math.PI + 2) + 8) % 8;
        return shooters.Select(id => (users[id], direction)).ToArray();
    }

    private int projectile_direction(WiredSimulationNode projectile, WiredSimulationFurniture before, WiredSimulationFurniture after)
    {
        if (before.Directions is not { Count: > 0 } directions)
        {
            unsupported(projectile, "Projectile rotation requires the furniture's supported Directions metadata.");
            return before.Direction;
        }
        int dx = after.X - before.X, dy = after.Y - before.Y;
        if (dx == 0 && dy == 0) return before.Direction;
        int system = checked((int)projectile.Number("direction_system"));
        double angle;
        if (system == 0)
            angle = Math.Atan2(Math.Sign(dy), Math.Sign(dx)) * 4 / Math.PI + 2;
        else if (system == 1)
            angle = Math.Atan2(dy, dx) * 4 / Math.PI + 2;
        else if (system is 2 or 3)
        {
            bool horizontal = Math.Abs(dx) > Math.Abs(dy) || system == 3 && Math.Abs(dx) == Math.Abs(dy);
            angle = horizontal ? dx > 0 ? 2 : 6 : dy > 0 ? 4 : 0;
        }
        else
        {
            unsupported(projectile, "Unknown projectile direction system.");
            return before.Direction;
        }
        angle = (angle + 8) % 8;
        int nearest = directions.OrderBy(direction => Math.Min(Math.Abs(direction - angle), 8 - Math.Abs(direction - angle)))
            .ThenBy(direction => direction).First();
        var ordered = directions.Order().ToArray();
        return ordered[(Array.IndexOf(ordered, nearest) + (int)projectile.Number("rotation_offset")) % ordered.Length];
    }
}
