using Qx.Messages;

namespace Qx.Model;

internal static class HabbiconWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStrings = 196_608;
    public const int MaximumStringBytes = 16 * 1024 * 1024;
    public const int StringPrefixBytes = sizeof(short);
    public const int HabbiconMinimumBytes = sizeof(int) * 6 + StringPrefixBytes;
    public const int CollectionFixedBytes = sizeof(int) * 6 + sizeof(bool) + StringPrefixBytes;
    public const int UserStateBytes = sizeof(int) * 2;
    public const int RoomUseBytes = sizeof(int) * 2;

    public static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }

    public static int CountWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(short),
        _ => throw new UnsupportedClientException(client)
    };

    public static int CollectionMinimumBytes(ClientType client) =>
        checked(CollectionFixedBytes + CountWidth(client));

    public static int ReadCount(
        in PacketReader p,
        int minimum_element_bytes,
        int trailing_bytes,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_element_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        RequireRemaining(in p, CountWidth(p.Client), trailing_bytes, name);
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => unchecked((ushort)p.ReadShort()),
            _ => throw new UnsupportedClientException(p.Client)
        };
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

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        int count = RequireListCount(values, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
        return Array.AsReadOnly(copy);
    }

    public static void WriteCount(int count, in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        RequireCount(count, nameof(count));
        if (p.Client is ClientType.Flash)
            p.WriteInt(count);
        else
            p.WriteShort(unchecked((short)count));
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

    public static HabbiconStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);
}

internal struct HabbiconStringBudget
{
    private readonly int maximum_count;
    private readonly int maximum_bytes;
    private int count;
    private int bytes;

    public HabbiconStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        this.maximum_count = maximum_count;
        this.maximum_bytes = maximum_bytes;
    }

    public string Read(in PacketReader p, string name, int trailing_bytes)
    {
        HabbiconWire.RequireRemaining(in p, HabbiconWire.StringPrefixBytes, trailing_bytes, name);
        int byte_count = unchecked((ushort)p.ReadShort());
        HabbiconWire.RequireRemaining(in p, byte_count, trailing_bytes, name);
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
        if (count >= maximum_count)
            throw new InvalidDataException($"{name} exceeds the string-count limit {maximum_count}.");
        if (byte_count > maximum_bytes - bytes)
            throw new InvalidDataException($"{name} exceeds the string-byte budget {maximum_bytes}.");
        count++;
        bytes += byte_count;
    }
}
