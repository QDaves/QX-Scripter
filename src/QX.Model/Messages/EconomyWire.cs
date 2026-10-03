using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model;

internal static class EconomyWire
{
    internal const int ActivityPointBytes = sizeof(int) * 2;

    internal static int RequireCount(int count, int available, int minimum_bytes, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (available < 0 || minimum_bytes <= 0 || count > available / minimum_bytes)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }

    internal static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    internal static IReadOnlyList<ActivityPoint> FreezePoints(
        IReadOnlyList<ActivityPoint> values,
        string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return Array.AsReadOnly(values.ToArray());
    }

    internal static ActivityPoint[] PreparePoints(
        IReadOnlyList<ActivityPoint> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ActivityPoint[] snapshot = values.ToArray();
        return snapshot;
    }

    internal static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
