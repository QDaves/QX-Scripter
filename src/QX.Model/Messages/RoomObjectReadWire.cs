using Qx.Messages;

namespace Qx.Model;

internal static class RoomObjectReadWire
{
    public const int MaximumCollectionCount = ushort.MaxValue;
    public const int MaximumStringBytes = 16 * 1024 * 1024;

    public static void RequireSupportedClient(ClientType client)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new UnsupportedClientException(client);
    }

    public static int IdWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(long),
        _ => throw new UnsupportedClientException(client)
    };

    public static int CountWidth(ClientType client) => client switch
    {
        ClientType.Flash => sizeof(int),
        ClientType.Unity => sizeof(short),
        _ => throw new UnsupportedClientException(client)
    };

    public static Id ReadRootId(in PacketReader p, string name)
    {
        RequireRemaining(in p, IdWidth(p.Client), 0, name);
        Id value = p.ReadId();
        RequireEmpty(in p, name);
        return value;
    }

    public static void WriteRootId<T>(T value, Id id, in PacketWriter p)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        RequireSupportedClient(p.Client);
        RequireWireId(p.Client, id, nameof(id));
        p.WriteId(id);
    }

    public static int ReadCount(
        in PacketReader p,
        int element_bytes,
        int trailing_bytes,
        string name)
    {
        RequireRemaining(in p, CountWidth(p.Client), trailing_bytes, name);
        int count = p.Client switch
        {
            ClientType.Flash => p.ReadInt(),
            ClientType.Unity => unchecked((ushort)p.ReadShort()),
            _ => throw new UnsupportedClientException(p.Client)
        };
        if (count < 0 || count > MaximumCollectionCount)
            throw new InvalidDataException($"{name} count {count} is outside the supported range.");
        int available = p.Available - trailing_bytes;
        if (available < 0 || count > available / element_bytes)
            throw new InvalidDataException($"{name} count {count} exceeds the remaining payload capacity.");
        return count;
    }

    public static void WriteCount(int count, in PacketWriter p)
    {
        RequireSupportedClient(p.Client);
        if (count is < 0 or > MaximumCollectionCount)
            throw new InvalidDataException($"Collection count {count} is outside the supported range.");
        if (p.Client is ClientType.Flash)
            p.WriteInt(count);
        else
            p.WriteShort(unchecked((short)count));
    }

    public static IReadOnlyList<int> Freeze(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count > MaximumCollectionCount)
            throw new ArgumentOutOfRangeException(nameof(values));
        return Array.AsReadOnly(values.ToArray());
    }

    public static void RequireWireId(ClientType client, Id value, string name)
    {
        RequireSupportedClient(client);
        if (client is ClientType.Flash)
            _ = checked((int)(long)value);
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
        int total = checked(required_bytes + trailing_bytes);
        if (required_bytes < 0 || trailing_bytes < 0 || p.Available < total)
            throw new InvalidDataException($"{name} requires {total} bytes but only {p.Available} remain.");
    }
}

internal struct RoomObjectReadStringBudget
{
    private int bytes;

    public string Read(in PacketReader p, int trailing_bytes, string name)
    {
        RoomObjectReadWire.RequireRemaining(in p, sizeof(short), trailing_bytes, name);
        int byte_count = unchecked((ushort)p.ReadShort());
        RoomObjectReadWire.RequireRemaining(in p, byte_count, trailing_bytes, name);
        Take(byte_count, name);
        return p.Encoding.GetString(p.ReadSpan(byte_count));
    }

    public void Require(string value, in PacketWriter p, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        int byte_count = p.Encoding.GetByteCount(value);
        if (byte_count > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
        Take(byte_count, name);
    }

    private void Take(int byte_count, string name)
    {
        if (byte_count > RoomObjectReadWire.MaximumStringBytes - bytes)
            throw new InvalidDataException($"{name} exceeds the string-byte budget.");
        bytes += byte_count;
    }
}
