using System.Buffers.Binary;
using System.Text;

namespace Qx.Messages;

/// <summary>Provides methods that write big-endian values to a packet body and advance a shared position.</summary>
/// <remarks>
/// Writing inside the body overwrites the bytes that are there, and the body grows when a write
/// passes its end. The <c>Replace</c> methods instead resize the value at the position to fit the new
/// one.
/// </remarks>
/// <param name="packet">The packet to write to.</param>
/// <param name="pos">A reference to the position to write at, which advances as values are written.</param>
/// <param name="context">The parser context, or <see langword="null"/> for none.</param>
public readonly ref struct PacketWriter(IPacket packet, ref int pos, IParserContext? context = null)
{
    private readonly IPacket Packet = packet ?? throw new ArgumentNullException(nameof(packet));
    /// <summary>A reference to the current write position.</summary>
    public readonly ref int Pos = ref pos;
    /// <summary>Gets the parser context, or <see langword="null"/> when none was given.</summary>
    public IParserContext? Context => context;
    /// <summary>Gets the header of the packet.</summary>
    public Header Header => Packet.Header;
    /// <summary>Gets the bytes of the packet body.</summary>
    public Span<byte> Span => Packet.Buffer.Span;
    /// <summary>Gets the length of the packet body in bytes.</summary>
    public int Length => Packet.Length;
    /// <summary>Gets the encoding of strings, which is UTF-8.</summary>
    public Encoding Encoding => Encoding.UTF8;

    /// <summary>Initializes a new writer at the packet's current position, without a parser context.</summary>
    /// <param name="packet">The packet to write to.</param>
    public PacketWriter(IPacket packet) : this(packet, ref packet.Position) { }

    /// <summary>Gets a reader that shares this writer's position and context.</summary>
    /// <returns>A reader over the same packet.</returns>
    public PacketReader Reader() => new(Packet, ref Pos, Context);
    /// <summary>Gets a reader at the specified position with this writer's context.</summary>
    /// <param name="pos">A reference to the position to read from.</param>
    /// <returns>A reader over the same packet.</returns>
    public PacketReader ReaderAt(ref int pos) => new(Packet, ref pos, Context);
    /// <summary>Gets a writer at the specified position with this writer's context.</summary>
    /// <param name="pos">A reference to the position to write at.</param>
    /// <returns>A writer over the same packet.</returns>
    public PacketWriter WriterAt(ref int pos) => new(Packet, ref pos, Context);

    /// <summary>Reserves bytes at the current position, growing the body when needed, and advances past them.</summary>
    /// <param name="n">The number of bytes to reserve.</param>
    /// <returns>The reserved bytes, which overlap any existing bytes at that position.</returns>
    public Span<byte> Allocate(int n)
    {
        Span<byte> buf = Packet.Buffer.Allocate(Pos, n);
        Pos += n;
        return buf;
    }

    /// <summary>Resizes the bytes at the current position, moves the bytes after them to fit, and advances past the resized range.</summary>
    /// <param name="pre">The current number of bytes in the range.</param>
    /// <param name="post">The new number of bytes in the range.</param>
    /// <returns>The resized range.</returns>
    public Span<byte> Resize(int pre, int post)
    {
        Span<byte> resized = Packet.Buffer.Resize(Pos..(Pos + pre), post);
        Pos += post;
        return resized;
    }

    /// <summary>Writes bytes at the current position.</summary>
    /// <param name="span">The bytes to write.</param>
    public void WriteSpan(ReadOnlySpan<byte> span) => span.CopyTo(Allocate(span.Length));

    /// <summary>Writes a boolean as one byte, 1 for <see langword="true"/> and 0 for <see langword="false"/>.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteBool(bool value) => WriteByte((byte)(value ? 1 : 0));

    /// <summary>Writes one byte.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteByte(byte value) => Allocate(1)[0] = value;

    /// <summary>Writes a 16-bit big-endian signed integer.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteShort(short value) => BinaryPrimitives.WriteInt16BigEndian(Allocate(2), value);

    /// <summary>Writes an array length followed by each value as a 16-bit big-endian signed integer.</summary>
    /// <param name="values">The values to write.</param>
    /// <exception cref="OverflowException">Thrown when there are more than 65535 values.</exception>
    public void WriteShortArray(IEnumerable<short> values)
    {
        short[] array = (values as short[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (short value in array)
            WriteShort(value);
    }

    /// <summary>Writes a 32-bit big-endian signed integer.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteInt(int value) => BinaryPrimitives.WriteInt32BigEndian(Allocate(4), value);

    /// <summary>Writes an array length followed by each value as a 32-bit big-endian signed integer.</summary>
    /// <param name="values">The values to write.</param>
    /// <exception cref="OverflowException">Thrown when there are more than 65535 values.</exception>
    public void WriteIntArray(IEnumerable<int> values)
    {
        int[] array = (values as int[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (int value in array)
            WriteInt(value);
    }

    /// <summary>Writes a float, which Flash sends as a string formatted with the invariant culture, with at least one and at most 15 decimal places.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteFloat(float value) => WriteString((FloatString)value);

    /// <summary>Writes a 32-bit big-endian IEEE 754 float.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteFloatBinary(float value) =>
        BinaryPrimitives.WriteSingleBigEndian(Allocate(4), value);

    /// <summary>Writes a 64-bit big-endian signed integer.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteLong(long value) => BinaryPrimitives.WriteInt64BigEndian(Allocate(8), value);

    /// <summary>Writes a 64-bit big-endian IEEE 754 double.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteDouble(double value) => BinaryPrimitives.WriteDoubleBigEndian(Allocate(8), value);

    /// <summary>Writes a UTF-8 string prefixed by its byte length as an unsigned 16-bit big-endian integer.</summary>
    /// <param name="value">The value to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the UTF-8 form of <paramref name="value"/> is longer than 65535 bytes.</exception>
    public void WriteString(string value)
    {
        int len = StringByteCount(value);
        WriteShort((short)len);
        Span<byte> span = Allocate(len);
        Encoding.GetBytes(value, span);
    }

    private int StringByteCount(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int len = Encoding.GetByteCount(value);
        if (len > ushort.MaxValue)
            throw new ArgumentException($"String byte length ({len}) exceeds the maximum value ({ushort.MaxValue}) of a {nameof(UInt16)}.", nameof(value));
        return len;
    }

    /// <summary>Writes an array length followed by each value as a string.</summary>
    /// <param name="values">The values to write.</param>
    /// <exception cref="OverflowException">Thrown when there are more than 65535 values.</exception>
    public void WriteStringArray(IEnumerable<string> values)
    {
        string[] array = (values as string[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (string value in array)
            WriteString(value);
    }

    /// <summary>Writes an ID, which Flash sends as a 32-bit big-endian signed integer.</summary>
    /// <param name="value">The value to write.</param>
    /// <exception cref="OverflowException">Thrown when <paramref name="value"/> does not fit in a 32-bit signed integer.</exception>
    public void WriteId(Id value) => WriteInt(checked((int)(long)value));

    /// <summary>Writes an array length followed by each ID.</summary>
    /// <remarks>Every ID is range-checked before anything is written.</remarks>
    /// <param name="values">The IDs to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="OverflowException">Thrown when an ID does not fit in a 32-bit signed integer or there are more than 65535 IDs.</exception>
    public void WriteIdArray(IEnumerable<Id> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Id[] array = (values as Id[]) ?? [.. values];
        foreach (Id value in array)
            _ = checked((int)(long)value);
        WriteLength((Length)array.Length);
        foreach (Id value in array)
            WriteId(value);
    }

    /// <summary>Writes an array length, which Flash sends as a 32-bit big-endian signed integer.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteLength(Length value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)value);
        WriteInt(value);
    }

    /// <summary>Writes a value with the write method that matches its runtime type.</summary>
    /// <remarks>
    /// Supported types are <see cref="int"/>, <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="short"/>, <see cref="long"/>, <see cref="byte"/>, <see cref="float"/> (written by
    /// <see cref="WriteFloat(float)"/>), <see cref="double"/>, <see cref="char"/> (written as a
    /// one-character string), <see cref="Id"/>, <see cref="Qx.Length"/> and <see cref="IComposer"/>.
    /// </remarks>
    /// <param name="value">The value to write.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or of an unsupported type.</exception>
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

    /// <summary>Writes each value in order with <see cref="WriteValue(object)"/>.</summary>
    /// <param name="values">The values to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a value is <see langword="null"/> or of an unsupported type.</exception>
    public void WriteValues(IEnumerable<object> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (object value in values)
            WriteValue(value);
    }

    /// <summary>Writes a value of a type that implements <see cref="IComposer"/>.</summary>
    /// <typeparam name="T">The type to write.</typeparam>
    /// <param name="value">The value to write.</param>
    public void Compose<T>(T value) where T : IComposer => value.Compose(in this);

    /// <summary>Writes an array length followed by each value of a type that implements <see cref="IComposer"/>.</summary>
    /// <typeparam name="T">The type to write.</typeparam>
    /// <param name="values">The values to write.</param>
    /// <exception cref="OverflowException">Thrown when there are more than 65535 values.</exception>
    public void ComposeArray<T>(IEnumerable<T> values) where T : IComposer
    {
        T[] array = (values as T[]) ?? [.. values];
        WriteLength((Length)array.Length);
        foreach (T value in array)
            Compose(value);
    }

    /// <summary>Overwrites the boolean at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceBool(bool value) => WriteBool(value);

    /// <summary>Overwrites the byte at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceByte(byte value) => WriteByte(value);

    /// <summary>Overwrites the 16-bit integer at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceShort(short value) => WriteShort(value);

    /// <summary>Overwrites the 32-bit integer at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceInt(int value) => WriteInt(value);

    /// <summary>Replaces the float at the current position, which Flash sends as a string, and resizes the body to fit.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceFloat(float value) => ReplaceString((FloatString)value);

    /// <summary>Overwrites the 64-bit integer at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceLong(long value) => WriteLong(value);

    /// <summary>Replaces the length-prefixed string at the current position and resizes the body to fit.</summary>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the UTF-8 form of <paramref name="value"/> is longer than 65535 bytes.</exception>
    public void ReplaceString(string value)
    {
        int postLen = StringByteCount(value);
        int start = Pos;
        int preLen = (ushort)Reader().ReadShort();
        Pos = start;
        WriteShort((short)postLen);
        Encoding.GetBytes(value, Resize(preLen, postLen));
    }

    /// <summary>Overwrites the array length at the current position.</summary>
    /// <param name="value">The new value.</param>
    public void ReplaceLength(Length value) => WriteLength(value);

    /// <summary>Overwrites the ID at the current position.</summary>
    /// <param name="value">The new value.</param>
    /// <exception cref="OverflowException">Thrown when <paramref name="value"/> does not fit in a 32-bit signed integer.</exception>
    public void ReplaceId(Id value) => WriteId(value);

    /// <summary>Replaces the value at the current position with another value of the same type and resizes the body to fit.</summary>
    /// <remarks>The current value is parsed first to find its size.</remarks>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The new value.</param>
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
}
