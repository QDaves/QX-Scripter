using System.Buffers.Binary;
using System.Text;

namespace Qx.Messages;

public readonly ref struct PacketReader(IPacket packet, ref int pos, IParserContext? context = null)
{
    private readonly IPacket Packet = RequireSupportedClient(packet);
    public readonly ref int Pos = ref pos;
    public IParserContext? Context => context;
    public Header Header => Packet.Header;
    public ClientType Client => Packet.Client;
    public ReadOnlySpan<byte> Span => Packet.Buffer.Span;
    public int Length => Packet.Length;
    public int Available => Packet.Length - Pos;
    public Encoding Encoding => Encoding.UTF8;

    public PacketReader(IPacket packet) : this(packet, ref packet.Position) { }

    public ReadOnlySpan<byte> ReadSpan(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (Pos + n > Span.Length)
            throw new IndexOutOfRangeException($"Attempted to read past the packet length: {n} bytes from position {Pos} when length is {Length}.");
        Pos += n;
        return Span[(Pos - n)..Pos];
    }

    public bool ReadBool() => ReadSpan(1)[0] != 0;

    public byte ReadByte() => ReadSpan(1)[0];

    public short ReadShort() => BinaryPrimitives.ReadInt16BigEndian(ReadSpan(2));

    public short[] ReadShortArray()
    {
        short[] array = new short[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadShort();
        return array;
    }

    public int ReadInt() => BinaryPrimitives.ReadInt32BigEndian(ReadSpan(4));

    public int[] ReadIntArray()
    {
        int[] array = new int[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadInt();
        return array;
    }

    public float ReadFloat() => Client switch
    {
        ClientType.Flash => (float)(FloatString)ReadString(),
        ClientType.Unity => BinaryPrimitives.ReadSingleBigEndian(ReadSpan(4)),
        _ => throw new UnsupportedClientException(Client)
    };

    public float ReadFloatBinary() => BinaryPrimitives.ReadSingleBigEndian(ReadSpan(4));

    public long ReadLong() => BinaryPrimitives.ReadInt64BigEndian(ReadSpan(8));

    public double ReadDouble() => BinaryPrimitives.ReadDoubleBigEndian(ReadSpan(8));

    public string ReadString() => Encoding.GetString(ReadSpan((ushort)ReadShort()));

    public string[] ReadStringArray()
    {
        string[] array = new string[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadString();
        return array;
    }

    public Id ReadId() => Client switch
    {
        ClientType.Unity => ReadLong(),
        ClientType.Flash => ReadInt(),
        _ => throw new UnsupportedClientException(Client),
    };

    public Id[] ReadIdArray()
    {
        Id[] array = new Id[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadId();
        return array;
    }

    public Length ReadLength() => Client switch
    {
        ClientType.Unity => unchecked((ushort)ReadShort()),
        ClientType.Flash => (Length)ReadInt(),
        _ => throw new UnsupportedClientException(Client),
    };

    public T Parse<T>() where T : IParser<T> => T.Parse(in this);

    public T[] ParseArray<T>() where T : IParser<T>
    {
        T[] array = new T[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = Parse<T>();
        return array;
    }

    private static IPacket RequireSupportedClient(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Client != ClientType.None && !ClientTypes.IsSupported(packet.Client))
            throw new UnsupportedClientException(packet.Client);
        return packet;
    }
}
