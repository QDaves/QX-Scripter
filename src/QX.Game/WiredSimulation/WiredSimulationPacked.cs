namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private long rotation_value(WiredSimulationFurniture item)
    {
        if (item.Direction % 2 != 0)
            unsupported_variable("@rotation for an unverified odd furniture direction");
        return item.Direction / 2;
    }

    private int? rotation_direction(WiredSimulationFurniture item, long value, Frame frame)
    {
        if (item.Directions?.Any(direction => direction % 2 != 0) == true)
            unsupported_variable("@rotation with unverified odd allowed furniture directions");
        if (item.Directions is null && (item.Direction % 2 != 0 || value != item.Direction / 2))
            unsupported_variable("@rotation without known allowed furniture Directions");
        if (value is < 0 or > 3)
        {
            record("rotation.rejected", frame.Stack, "The rotation index is outside 0..3.", [item.Id]);
            return null;
        }
        int direction = (int)value * 2;
        if (item.Directions is { } directions && !directions.Contains(direction))
        {
            record("rotation.rejected", frame.Stack, "The furniture does not support this rotation index.", [item.Id]);
            return null;
        }
        return direction;
    }

    private void rotate_variable(WiredSimulationFurniture item, long value, Frame frame)
    {
        if (rotation_direction(item, value, frame) is not { } direction) return;
        var rotated = item with { Direction = direction };
        if (!fits(rotated, item.X, item.Y))
        {
            record("rotation.rejected", frame.Stack, "The rotated footprint has no complete floor.", [item.Id]);
            return;
        }
        if (furniture.Values.Any(other => other.Id != item.Id && overlaps(rotated, other) && !overlaps(item, other)))
            unsupported_variable("@rotation into newly overlapping furniture");
        if (direction != item.Direction && users.Values.Any(user => covers(item, user.X, user.Y) || covers(rotated, user.X, user.Y)))
            unsupported_variable("@rotation with an affected avatar");
        rotate_item(item, direction, frame);
    }

    private void occupation_variable(WiredSimulationFurniture item, long value, Frame frame)
    {
        if (value is < 0 or > 0xffffff)
        {
            record("occupation.rejected", frame.Stack, "The packed occupation is outside 24 bits.", [item.Id]);
            return;
        }
        int x = (int)(value >> 16), y = (int)((value >> 8) & 255);
        if (rotation_direction(item, value & 255, frame) is not { } direction) return;
        var destination = item with { X = x, Y = y, Direction = direction };
        if (!fits(destination, x, y))
        {
            record("occupation.rejected", frame.Stack, "The destination footprint has no complete floor.", [item.Id]);
            return;
        }
        var addons = frame.Stack is { } stack && nodes.TryGetValue(stack, out var node) ? siblings(node) : [];
        if (item.X == x && item.Y == y)
        {
            if (item.Direction == direction)
            {
                move_item(item, x, y, null, frame, packed_movement: true);
                return;
            }
            rotate_variable(item, value & 255, frame);
            if (item.Direction != direction && addons.Any(addon => addon.Name == "NoMoveAnimation"))
                effects.Add(new(now, "MovementAnimation", frame.Stack ?? 0, [item.Id], Value: 0,
                    Movement: furniture_motion(item, furniture[item.Id], 0, null)));
            return;
        }
        var physics = addons.SingleOrDefault(addon => addon.Name == "MovementPhysics");
        if (relative_stack_blocked(item with { Direction = direction }, x, y, physics))
        {
            record("occupation.rejected", frame.Stack, "The destination is blocked by furniture.", [item.Id]);
            return;
        }
        bool occupied = users.Values.Any(user => covers(item, user.X, user.Y) || covers(destination, user.X, user.Y));
        if (occupied && (direction != item.Direction || overlaps(item, destination)))
            unsupported_variable("@occupation rotation or overlapping footprints with an affected avatar");
        if (addons.Any(addon => addon.Name == "Projectile"))
            unsupported_variable("@occupation combined with projectile modifiers");
        move_item(item, x, y, null, frame, target_direction: direction, packed_movement: true);
    }
}
