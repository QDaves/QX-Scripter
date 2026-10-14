using Qx.Game.WiredSimulation;

namespace Qx.Game.Application;

internal sealed partial class WiredSimulationApplication
{
    private static int? geometry_value(IReadOnlyDictionary<string, WiredSimulationValue> values,
        IReadOnlyList<WiredSimulationVariable> definitions, string name, long minimum, long maximum,
        string holder, List<string> missing)
    {
        var value = native_readout(values, definitions, name);
        if (value is null) return null;
        if (!value.IsExact || value.Value < minimum || value.Value > maximum)
        {
            missing.Add($"Geometry readout {name} is outside its exact supported range: {holder}");
            return null;
        }
        return (int)value.Value;
    }

    private static int? same_geometry(int? first, int? second, string field, string holder)
    {
        if (first.HasValue && second.HasValue && first != second)
            throw new ArgumentException($"Geometry readouts disagree on {field}: {holder}");
        return first ?? second;
    }

    private static (int X, int Y, int Direction) readout_position(WiredSimulationRoom room,
        IReadOnlyDictionary<string, WiredSimulationValue> values, IReadOnlyList<WiredSimulationVariable> definitions,
        int x, int y, int direction, bool furniture, string holder, List<string> missing)
    {
        int? read_x = geometry_value(values, definitions, "@position.x", 0, int.MaxValue - 1L, holder, missing);
        int? read_y = geometry_value(values, definitions, "@position.y", 0, int.MaxValue - 1L, holder, missing);
        int? read_direction = geometry_value(values, definitions, "@direction", 0, 7, holder, missing);
        if (geometry_value(values, definitions, "@position", 0, 0xffff, holder, missing) is { } position)
        {
            read_x = same_geometry(read_x, position >> 8, "X", holder);
            read_y = same_geometry(read_y, position & 255, "Y", holder);
        }
        if (furniture)
        {
            int? rotation = geometry_value(values, definitions, "@rotation", 0, 3, holder, missing);
            if (geometry_value(values, definitions, "@occupation", 0, 0xffffff, holder, missing) is { } occupation)
            {
                read_x = same_geometry(read_x, occupation >> 16, "X", holder);
                read_y = same_geometry(read_y, (occupation >> 8) & 255, "Y", holder);
                if ((occupation & 255) > 3)
                    missing.Add("Packed occupation has an unsupported rotation index: " + holder);
                else rotation = same_geometry(rotation, occupation & 255, "rotation", holder);
            }
            read_direction = same_geometry(read_direction, rotation * 2, "direction", holder);
        }
        int next_x = read_x ?? x, next_y = read_y ?? y;
        if ((read_x.HasValue || read_y.HasValue) && (room.Width <= 0 || room.Height <= 0 ||
            next_x < 0 || next_y < 0 || next_x >= room.Width || next_y >= room.Height))
        {
            missing.Add("Geometry readout position is outside the captured room bounds: " + holder);
            next_x = x;
            next_y = y;
        }
        return (next_x, next_y, read_direction ?? direction);
    }

    private static double readout_altitude(IReadOnlyDictionary<string, WiredSimulationValue> values,
        IReadOnlyList<WiredSimulationVariable> definitions, double cached, string holder, List<string> missing)
    {
        if (geometry_value(values, definitions, "@altitude", int.MinValue + 1L, int.MaxValue - 1L, holder, missing) is not { } altitude)
            return cached;
        return WiredSimulationGeometry.height_value(cached) == altitude ? cached : altitude / 100.0;
    }

    private static WiredSimulationFurniture furniture_geometry(WiredSimulationRoom room, WiredSimulationFurniture item,
        IReadOnlyDictionary<string, WiredSimulationValue> values, IReadOnlyList<WiredSimulationVariable> definitions, List<string> missing)
    {
        string holder = "furniture " + item.Id;
        var position = readout_position(room, values, definitions, item.X, item.Y, item.Direction, true, holder, missing);
        item = item with { X = position.X, Y = position.Y, Direction = position.Direction,
            Z = readout_altitude(values, definitions, item.Z, holder, missing) };
        if (geometry_value(values, definitions, "@state", int.MinValue + 1L, int.MaxValue - 1L, holder, missing) is { } state)
        {
            if (item.IntrinsicState.HasValue)
                item = item with { IntrinsicState = state };
            else
            {
                item = item with { State = state };
                if (item.StateCount is { } count && (state < 0 || state >= count))
                    missing.Add("State readout is outside the captured furniture state count: " + item.Id);
                if (item.States?.TryGetValue(state, out var surface) == true)
                    item = item with { Height = surface.Height, Walkable = surface.Walkable,
                        Stackable = surface.Stackable, Stance = surface.Stance };
                else if (item.States is not null)
                    missing.Add("State readout has no captured furniture geometry: " + item.Id);
            }
        }
        if (geometry_value(values, definitions, "@height", 0, int.MaxValue - 1L, holder, missing) is { } height &&
            WiredSimulationGeometry.height_value(item.Height) != height)
            missing.Add("Height readout disagrees with captured furniture metadata: " + item.Id);
        return item;
    }

    private static WiredSimulationUser user_geometry(WiredSimulationRoom room, WiredSimulationUser user,
        IReadOnlyDictionary<string, WiredSimulationValue> values, IReadOnlyList<WiredSimulationVariable> definitions, List<string> missing)
    {
        foreach (var identity in new[] { (Name: "@user_id", Value: user.Id), (Name: "@id", Value: user.Id),
                     (Name: "@index", Value: (long)user.Index!.Value) })
            if (native_readout(values, definitions, identity.Name) is { IsExact: true } observed && observed.Value != identity.Value)
                throw new ArgumentException("An object-variable readout disagrees with the current user identity.");
        var position = readout_position(room, values, definitions, user.X, user.Y, user.Direction, false,
            "user " + user.Id, missing);
        return user with { X = position.X, Y = position.Y, Direction = position.Direction,
            Z = readout_altitude(values, definitions, user.Z, "user " + user.Id, missing) };
    }
}
