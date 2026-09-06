using Qx.Messages;

namespace Qx.Model;

internal static class AchievementBadgeWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStrings = 196_608;
    public const int MaximumStringBytes = 16 * 1024 * 1024;
    public const int StringPrefixBytes = sizeof(short);
    public const int AchievementMinimumBytes = 45;
    public const int BadgeMinimumBytes = sizeof(int) + StringPrefixBytes;
    public const int SelectedBadgeMinimumBytes = sizeof(int) + StringPrefixBytes;
    public const int PointLimitMinimumBytes = sizeof(int) * 2;

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

    public static int UserIdWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(long),
        _ => throw new UnsupportedClientException(client)
    };

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

    public static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string name)
    {
        int count = RequireListCount(values, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
        return Array.AsReadOnly(copy);
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

    public static Id ReadUserId(
        in PacketReader p,
        int trailing_bytes,
        string name)
    {
        RequireRemaining(in p, UserIdWidth(p.Client), trailing_bytes, name);
        return p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => p.ReadLong(),
            _ => throw new UnsupportedClientException(p.Client)
        };
    }

    public static void RequireUserId(Id value, ClientType client)
    {
        RequireSupportedClient(client);
        if (client is ClientType.Flash)
            _ = checked((int)(long)value);
    }

    public static void WriteUserId(Id value, in PacketWriter p)
    {
        if (p.Client is ClientType.Flash)
            p.WriteInt(checked((int)(long)value));
        else if (p.Client is ClientType.Unity)
            p.WriteLong(value);
        else
            throw new UnsupportedClientException(p.Client);
    }

    public static int RequireBadgeId(Id value) => checked((int)(long)value);

    public static void WriteCount(int count, in PacketWriter p)
    {
        if (p.Client is ClientType.Flash)
            p.WriteInt(count);
        else if (p.Client is ClientType.Unity)
            p.WriteShort(unchecked((short)count));
        else
            throw new UnsupportedClientException(p.Client);
    }

    public static AchievementBadgeStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);
}

internal struct AchievementBadgeStringBudget
{
    private readonly int _maximum_count;
    private readonly int _maximum_bytes;
    private int _count;
    private int _bytes;

    public AchievementBadgeStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        _maximum_count = maximum_count;
        _maximum_bytes = maximum_bytes;
    }

    public string Read(in PacketReader p, string name, int trailing_bytes)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            AchievementBadgeWire.StringPrefixBytes,
            trailing_bytes,
            name);
        int byte_count = unchecked((ushort)p.ReadShort());
        AchievementBadgeWire.RequireRemaining(in p, byte_count, trailing_bytes, name);
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
