using Qx.Messages;

namespace Qx.Model.Forums;

internal static class ForumProtocol
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStrings = 196_608;
    public const int MaximumStringBytes = 16 * 1024 * 1024;
    public const int StringPrefixBytes = sizeof(short);
    public const int SummaryMinimumBytes = 40;
    public const int PermissionsMinimumBytes = 26;
    public const int DetailsMinimumBytes = 68;
    public const int ThreadMinimumBytes = 51;
    public const int PostMinimumBytes = 37;

    public static Id ReadFlashId(in PacketReader p) => ReadFlashId(in p, 0, "forum id");

    public static Id ReadFlashId(
        in PacketReader p,
        int trailing_bytes,
        string name)
    {
        RequireRemaining(in p, sizeof(int), trailing_bytes, name);
        return p.ReadInt();
    }

    public static void WriteFlashId(in PacketWriter p, Id value)
    {
        p.WriteInt(RequireFlashId(value, "forum"));
    }

    public static int ReadFlashCount(
        in PacketReader p,
        int minimum_element_bytes,
        int trailing_bytes,
        string collection)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimum_element_bytes);
        RequireRemaining(in p, sizeof(int), trailing_bytes, collection);
        int count = p.ReadInt();
        RequireCount(count, collection);
        int available = p.Available - trailing_bytes;
        if (available < 0 || count > available / minimum_element_bytes)
        {
            throw new InvalidDataException(
                $"{collection} count {count} exceeds the remaining payload capacity.");
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

    public static IReadOnlyList<T> FreezeReferences<T>(
        IReadOnlyList<T> values,
        string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        int count = RequireCount(values.Count, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<T> FreezeValues<T>(
        IReadOnlyList<T> values,
        string name) where T : struct
    {
        ArgumentNullException.ThrowIfNull(values, name);
        int count = RequireCount(values.Count, name);
        var copy = new T[count];
        for (int index = 0; index < count; index++)
            copy[index] = values[index];
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

    public static int RequireFlashId(Id value, string name)
    {
        long id = value;
        if (id is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException(
                $"Flash {name} id {id} exceeds the signed 32-bit range.");
        }
        return (int)id;
    }

    public static ForumStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);

    public static T UnsupportedUnity<T>(ClientType client) =>
        throw new UnsupportedClientException(client);

    public static void UnsupportedUnity(ClientType client) =>
        throw new UnsupportedClientException(client);
}

internal struct ForumStringBudget
{
    private readonly int maximum_count;
    private readonly int maximum_bytes;
    private int count;
    private int bytes;

    public ForumStringBudget(int maximum_count, int maximum_bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum_bytes);
        this.maximum_count = maximum_count;
        this.maximum_bytes = maximum_bytes;
    }

    public string Read(in PacketReader p, string name, int trailing_bytes)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.StringPrefixBytes,
            trailing_bytes,
            name);
        int byte_count = unchecked((ushort)p.ReadShort());
        ForumProtocol.RequireRemaining(in p, byte_count, trailing_bytes, name);
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
