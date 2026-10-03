using Qx.Messages;

namespace Qx.Model;

internal static class SubscriptionAdjunctWire
{
    public const int MaximumOfferCount = ushort.MaxValue;
    public const int MaximumStringBytes = 8 * 1024 * 1024;
    public const int MinimumOfferSize = 45;
    public const int MinimumOfferTailSize = 39;

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireSize(in PacketReader p, int expected, string name)
    {
        if (p.Available != expected)
        {
            throw new InvalidDataException(
                $"{name} requires exactly {expected} bytes, received {p.Available}.");
        }
    }

    public static void RequireMinimum(in PacketReader p, int minimum, string name)
    {
        if (p.Available < minimum)
        {
            throw new InvalidDataException(
                $"{name} requires at least {minimum} bytes, received {p.Available}.");
        }
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
