using Qx.Messages;

namespace Qx.Model;

internal static class DailyTaskWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStrings = 196608;
    public const int MaximumStringBytes = 16 * 1024 * 1024;
    public const int StringPrefixBytes = sizeof(short);
    public const int RewardMinimumBytes = sizeof(short) + StringPrefixBytes * 2 + sizeof(int);
    public const int TaskMinimumBytes = sizeof(long) + StringPrefixBytes * 4 + sizeof(byte) * 2
        + sizeof(int) * 4;

    public static void RequireSupportedClient(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }

    public static int ReadCount(
        in PacketReader p,
        int minimum_element_bytes,
        int trailing_bytes,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_element_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        RequireRemaining(in p, sizeof(int), trailing_bytes, name);
        int count = p.ReadInt();
        RequireCount(count, name);
        int available = p.Available - trailing_bytes;
        if (available < 0 || count > available / minimum_element_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    public static int RequireCount(int count, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (count > MaximumCollectionCount)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the limit {MaximumCollectionCount}.");
        }
        return count;
    }

    public static int RequireListCount<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return RequireCount(values.Count, name);
    }

    public static IReadOnlyList<T> FreezeReferences<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        int count = RequireListCount(values, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireRemaining(
        in PacketReader p,
        int required_bytes,
        int trailing_bytes,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(required_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        int total = checked(required_bytes + trailing_bytes);
        if (p.Available < total)
        {
            throw new InvalidDataException(
                $"{name} requires {total} bytes but only {p.Available} remain.");
        }
    }

    public static void WriteCount(int count, in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        RequireCount(count, nameof(count));
        p.WriteInt(count);
    }

    public static DailyTaskStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);
}

internal struct DailyTaskStringBudget
{
    private readonly int _maximum_count;
    private readonly int _maximum_bytes;
    private int _count;
    private int _bytes;

    public DailyTaskStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        _maximum_count = maximum_count;
        _maximum_bytes = maximum_bytes;
    }

    public string Read(in PacketReader p, string name, int trailing_bytes)
    {
        DailyTaskWire.RequireRemaining(
            in p,
            DailyTaskWire.StringPrefixBytes,
            trailing_bytes,
            name);
        int byte_count = unchecked((ushort)p.ReadShort());
        DailyTaskWire.RequireRemaining(in p, byte_count, trailing_bytes, name);
        Take(byte_count, name);
        return p.Encoding.GetString(p.ReadSpan(byte_count));
    }

    public void Require(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int byte_count = p.Encoding.GetByteCount(value);
        if (byte_count > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
        Take(byte_count, name);
    }

    private void Take(int byte_count, string name)
    {
        if (_count >= _maximum_count)
        {
            throw new InvalidDataException(
                $"{name} exceeds the string-count limit {_maximum_count}.");
        }
        if (byte_count > _maximum_bytes - _bytes)
        {
            throw new InvalidDataException(
                $"{name} exceeds the string-byte budget {_maximum_bytes}.");
        }
        _count++;
        _bytes += byte_count;
    }
}
