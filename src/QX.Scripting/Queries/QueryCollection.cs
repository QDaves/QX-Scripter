using System.Collections;
using Qx.Model;

namespace Qx.Scripting;

public abstract class QueryCollection<T> : IReadOnlyList<T>
{
    private readonly T[] _items;

    protected QueryCollection(IEnumerable<T> items)
        : this(items, null)
    {
    }

    protected QueryCollection(IEnumerable<T> items, Func<T, T>? snapshot)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = snapshot is null
            ? items.ToArray()
            : items.Select(snapshot).ToArray();
    }

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    protected IEnumerable<T> Items => _items;

    public T[] ToArray() => [.. _items];

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class QueryValues
{
    public static HashSet<T> Set<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ToHashSet();
    }

    public static HashSet<string> Strings(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Where(value => value is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static void Epsilon(float epsilon)
    {
        if (!float.IsFinite(epsilon) || epsilon < 0)
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon must be finite and non-negative.");
    }

    public static bool Position(
        Tile location,
        int? x,
        int? y,
        float? z,
        int? direction,
        int actualDirection,
        float epsilon)
    {
        return
            (!x.HasValue || location.X == x.Value) &&
            (!y.HasValue || location.Y == y.Value) &&
            (!z.HasValue || Math.Abs(location.Z - z.Value) <= epsilon) &&
            (!direction.HasValue || actualDirection == direction.Value);
    }

    public static double DistanceSquared(Point first, Point second)
    {
        double dx = (double)first.X - second.X;
        double dy = (double)first.Y - second.Y;
        return dx * dx + dy * dy;
    }

    public static double DistanceSquared(Area area, Point point)
    {
        double dx = point.X < area.X1
            ? (double)area.X1 - point.X
            : point.X > area.X2
                ? (double)point.X - area.X2
                : 0d;
        double dy = point.Y < area.Y1
            ? (double)area.Y1 - point.Y
            : point.Y > area.Y2
                ? (double)point.Y - area.Y2
                : 0d;
        return dx * dx + dy * dy;
    }

    public static bool Adjacent(Area area, Point point, bool diagonals)
    {
        long dx = point.X < area.X1
            ? (long)area.X1 - point.X
            : point.X > area.X2
                ? (long)point.X - area.X2
                : 0;
        long dy = point.Y < area.Y1
            ? (long)area.Y1 - point.Y
            : point.Y > area.Y2
                ? (long)point.Y - area.Y2
                : 0;
        if (dx == 0 && dy == 0)
            return false;
        return diagonals ? Math.Max(dx, dy) == 1 : dx + dy == 1;
    }

    public static bool Adjacent(Area first, Area second, bool diagonals)
    {
        long dx = first.X2 < second.X1
            ? (long)second.X1 - first.X2
            : second.X2 < first.X1
                ? (long)first.X1 - second.X2
                : 0;
        long dy = first.Y2 < second.Y1
            ? (long)second.Y1 - first.Y2
            : second.Y2 < first.Y1
                ? (long)first.Y1 - second.Y2
                : 0;
        if (dx == 0 && dy == 0)
            return false;
        return diagonals ? Math.Max(dx, dy) == 1 : dx + dy == 1;
    }
}
