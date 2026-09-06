using System.Buffers.Binary;
using System.Text;

namespace Qx.Messages;

public readonly ref struct PacketWriter(IPacket packet, ref int pos, IParserContext? context = null)
{
    private readonly IPacket Packet = RequireSupportedClient(packet);
    public readonly ref int Pos = ref pos;
    public readonly IParserContext? Context = context;
    public Header Header => Packet.Header;
    public ClientType Client => Packet.Client;
    public Span<byte> Span => Packet.Buffer.Span;
    public int Length => Packet.Length;
    public Encoding Encoding => Encoding.UTF8;

    public PacketWriter(IPacket packet) : this(packet, ref packet.Position) { }

    public PacketReader Reader() => new(Packet, ref Pos, Context);
    public PacketReader ReaderAt(ref int pos) => new(Packet, ref pos, Context);
    public PacketWriter WriterAt(ref int pos) => new(Packet, ref pos, Context);

    public Span<byte> Allocate(int n)
    {
        Span<byte> buf = Packet.Buffer.Allocate(Pos, n);
        Pos += n;
        return buf;
    }

    public Span<byte> Resize(int pre, int post)
    {
        Span<byte> resized = Packet.Buffer.Resize(Pos..(Pos + pre), post);
        Pos += post;
        return resized;
    }

    public void WriteSpan(ReadOnlySpan<byte> span) => span.CopyTo(Allocate(span.Length));

    public void WriteBool(bool value) => WriteByte((byte)(value ? 1 : 0));

    public void WriteByte(byte value) => Allocate(1)[0] = value;

    public void WriteShort(short value) => BinaryPrimitives.WriteInt16BigEndian(Allocate(2), value);

    public void WriteShortArray(IEnumerable<short> values)
    {
        short[] array = (values as short[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (short value in array)
            WriteShort(value);
    }

    public void WriteInt(int value) => BinaryPrimitives.WriteInt32BigEndian(Allocate(4), value);

    public void WriteIntArray(IEnumerable<int> values)
    {
        int[] array = (values as int[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (int value in array)
            WriteInt(value);
    }

    public void WriteFloat(float value)
    {
        switch (Client)
        {
            case ClientType.Flash:
                WriteString((FloatString)value);
                break;
            case ClientType.Unity:
                BinaryPrimitives.WriteSingleBigEndian(Allocate(4), value);
                break;
            default:
                throw new UnsupportedClientException(Client);
        }
    }

    public void WriteFloatBinary(float value) =>
        BinaryPrimitives.WriteSingleBigEndian(Allocate(4), value);

    public void WriteLong(long value) => BinaryPrimitives.WriteInt64BigEndian(Allocate(8), value);

    public void WriteDouble(double value) => BinaryPrimitives.WriteDoubleBigEndian(Allocate(8), value);

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int len = Encoding.GetByteCount(value);
        if (len > ushort.MaxValue)
            throw new ArgumentException($"String byte length ({len}) exceeds the maximum value ({ushort.MaxValue}) of a {nameof(UInt16)}.", nameof(value));

        WriteShort((short)len);
        Span<byte> span = Allocate(len);
        Encoding.GetBytes(value, span);
    }

    public void WriteStringArray(IEnumerable<string> values)
    {
        string[] array = (values as string[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (string value in array)
            WriteString(value);
    }

    public void WriteId(Id value)
    {
        switch (Client)
        {
            case ClientType.Unity:
                WriteLong(value);
                break;
            case ClientType.Flash:
                WriteInt(AllowsLegacyIdProjection
                    ? unchecked((int)(long)value)
                    : checked((int)(long)value));
                break;
            default:
                throw new UnsupportedClientException(Client);
        }
    }

    public void WriteIdArray(IEnumerable<Id> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Id[] array = (values as Id[]) ?? [.. values];
        switch (Client)
        {
            case ClientType.Unity:
                break;
            case ClientType.Flash:
                if (!AllowsLegacyIdProjection)
                {
                    foreach (Id value in array)
                        _ = checked((int)(long)value);
                }
                break;
            default:
                throw new UnsupportedClientException(Client);
        }
        WriteLength((Length)array.Length);
        foreach (Id value in array)
            WriteId(value);
    }

    public void WriteLength(Length value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)value);

        switch (Client)
        {
            case ClientType.Unity:
                WriteShort((short)(ushort)value);
                break;
            case ClientType.Flash:
                WriteInt(value);
                break;
            default:
                throw new UnsupportedClientException(Client);
        }
    }

    public void WriteValue(object value)
    {
        switch (value)
        {
            case int number: WriteInt(number); break;
            case string text: WriteString(text); break;
            case bool state: WriteBool(state); break;
            case short number: WriteShort(number); break;
            case long number: WriteLong(number); break;
            case byte number: WriteByte(number); break;
            case float number: WriteFloat(number); break;
            case double number: WriteDouble(number); break;
            case char character: WriteString(character.ToString()); break;
            case Id id: WriteId(id); break;
            case Length length: WriteLength(length); break;
            case IComposer composer: composer.Compose(in this); break;
            default: throw new ArgumentException($"Unsupported packet value type: {value?.GetType().Name ?? "null"}.", nameof(value));
        }
    }

    public void WriteValues(IEnumerable<object> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (object value in values)
            WriteValue(value);
    }

    public void Compose<T>(T value) where T : IComposer => value.Compose(in this);

    public void ComposeArray<T>(IEnumerable<T> values) where T : IComposer
    {
        T[] array = (values as T[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (T value in array)
            Compose(value);
    }

    public void ReplaceBool(bool value) => WriteBool(value);

    public void ReplaceByte(byte value) => WriteByte(value);

    public void ReplaceShort(short value) => WriteShort(value);

    public void ReplaceInt(int value) => WriteInt(value);

    public void ReplaceFloat(float value)
    {
        switch (Client)
        {
            case ClientType.Flash:
                ReplaceString((FloatString)value);
                break;
            case ClientType.Unity:
                WriteFloat(value);
                break;
            default:
                throw new UnsupportedClientException(Client);
        }
    }

    public void ReplaceLong(long value) => WriteLong(value);

    public void ReplaceString(string value)
    {
        int start = Pos;
        int preLen = Reader().ReadShort();
        int postLen = Encoding.GetByteCount(value);
        Pos = start;
        WriteShort((short)postLen);
        Encoding.GetBytes(value, Resize(preLen, postLen));
    }

    public void ReplaceLength(Length value) => WriteLength(value);

    public void ReplaceId(Id value) => WriteId(value);

    public void ReplaceStruct<T>(T value) where T : IParserComposer<T>
    {
        int start = Pos, end = Pos;
        ReaderAt(ref end).Parse<T>();
        int preSize = end - start;
        start = Length; end = Length;
        WriterAt(ref end).Compose(value);
        int postSize = end - start;
        Span<byte> resized = Resize(preSize, postSize);
        Span[^postSize..].CopyTo(resized);
        Packet.Buffer.Resize(^postSize.., 0);
    }

    private bool AllowsLegacyIdProjection =>
        Packet is Qx.Messages.Packet { AllowLegacyIdProjection: true };

    private static IPacket RequireSupportedClient(IPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Client != ClientType.None && !ClientTypes.IsSupported(packet.Client))
            throw new UnsupportedClientException(packet.Client);
        return packet;
    }
}
