using Qx.Messages;

namespace Qx.Model;

internal static class CatalogWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int StringMinimumBytes = sizeof(short);

    public static int CountWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(short),
        _ => throw new UnsupportedClientException(client)
    };

    public static int ReadCount(
        in PacketReader p,
        int minimum_element_bytes,
        int trailing_bytes,
        int maximum,
        string name)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_element_bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => unchecked((ushort)p.ReadShort()),
            _ => throw new UnsupportedClientException(p.Client)
        };
        RequireCount(count, maximum, name);
        int available = p.Available - trailing_bytes;
        if (available < 0 || count > available / minimum_element_bytes)
        {
            throw new InvalidDataException(
                $"{name} count {count} exceeds the remaining payload capacity.");
        }
        return count;
    }

    public static int RequireCount(int count, int maximum, string name)
    {
        if (count < 0)
            throw new InvalidDataException($"{name} contains a negative count {count}.");
        if (count > maximum)
            throw new InvalidDataException($"{name} count {count} exceeds the limit {maximum}.");
        return count;
    }

    public static int RequireListCount<T>(IReadOnlyList<T> values, int maximum, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return RequireCount(values.Count, maximum, name);
    }

    public static IReadOnlyList<T> FreezeValues<T>(
        IReadOnlyList<T> values,
        int maximum,
        string name)
    {
        int count = RequireListCount(values, maximum, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<T> FreezeReferences<T>(
        IReadOnlyList<T> values,
        int maximum,
        string name) where T : class
    {
        int count = RequireListCount(values, maximum, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<T>? FreezeOptionalReferences<T>(
        IReadOnlyList<T>? values,
        int maximum,
        string name) where T : class =>
        values is null ? null : FreezeReferences(values, maximum, name);

    public static T[] SnapshotReferences<T>(
        IReadOnlyList<T> values,
        int maximum,
        string name) where T : class
    {
        int count = RequireListCount(values, maximum, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return copy;
    }

    public static T[] SnapshotValues<T>(
        IReadOnlyList<T> values,
        int maximum,
        string name)
    {
        int count = RequireListCount(values, maximum, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
        return copy;
    }

    public static T RequireReference<T>(T? value, string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void WriteCount(int count, in PacketWriter p) =>
        p.WriteLength((Length)count);
}

internal struct CatalogStringBudget
{
    private readonly int _maximum_count;
    private readonly int _maximum_bytes;
    private int _count;
    private int _bytes;

    public CatalogStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        _maximum_count = maximum_count;
        _maximum_bytes = maximum_bytes;
    }

    public string Read(in PacketReader p, string name) => Read(in p, name, 0);

    public string Read(in PacketReader p, string name, int trailing_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trailing_bytes);
        int byte_count = unchecked((ushort)p.ReadShort());
        int available = p.Available - trailing_bytes;
        if (available < 0 || byte_count > available)
        {
            throw new InvalidDataException(
                $"{name} length {byte_count} exceeds the remaining payload capacity.");
        }
        Take(byte_count, name);
        return p.Encoding.GetString(p.ReadSpan(byte_count));
    }

    public void Require(string value, string name, in PacketWriter p)
    {
        CatalogWire.RequireString(value, name, in p);
        int byte_count = p.Encoding.GetByteCount(value);
        Take(byte_count, name);
    }

    private void Take(int byte_count, string name)
    {
        if (_count >= _maximum_count)
            throw new InvalidDataException($"{name} exceeds the string-count limit {_maximum_count}.");
        if (byte_count > _maximum_bytes - _bytes)
            throw new InvalidDataException($"{name} exceeds the string-byte budget {_maximum_bytes}.");
        _count++;
        _bytes += byte_count;
    }
}
