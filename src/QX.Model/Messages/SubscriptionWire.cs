using Qx.Messages;

namespace Qx.Model;

internal static class SubscriptionWire
{
    public static int? ReadIntTail(
        in PacketReader p,
        bool required,
        string message_name)
    {
        if (p.Available == sizeof(int))
            return p.ReadInt();
        if (!required && p.Available == 0)
            return null;

        string expectation = required
            ? "exactly one trailing Int32"
            : "zero or one trailing Int32";
        throw new InvalidDataException($"{message_name} requires {expectation}.");
    }

    public static void RequireEmpty(in PacketReader p, string message_name)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{message_name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
