using System.Buffers.Binary;
using System.Text;

namespace Qx.Messages;

/// <summary>Provides methods that read big-endian values from a packet body and advance a shared position.</summary>
/// <remarks>
/// Every read throws <see cref="IndexOutOfRangeException"/> when it would pass the end of the body.
/// </remarks>
/// <param name="packet">The packet to read from.</param>
/// <param name="pos">A reference to the position to read from, which advances as values are read.</param>
/// <param name="context">The parser context, or <see langword="null"/> for none.</param>
public readonly ref struct PacketReader(IPacket packet, ref int pos, IParserContext? context = null)
{
    private readonly IPacket Packet = packet ?? throw new ArgumentNullException(nameof(packet));
    /// <summary>A reference to the current read position.</summary>
    public readonly ref int Pos = ref pos;
    /// <summary>Gets the parser context, or <see langword="null"/> when none was given.</summary>
    public IParserContext? Context => context;
    /// <summary>Gets the header of the packet.</summary>
    public Header Header => Packet.Header;
    /// <summary>Gets the bytes of the packet body.</summary>
    public ReadOnlySpan<byte> Span => Packet.Buffer.Span;
    /// <summary>Gets the length of the packet body in bytes.</summary>
    public int Length => Packet.Length;
    /// <summary>Gets the number of bytes left to read after the current position.</summary>
    public int Available => Packet.Length - Pos;
    /// <summary>Gets the encoding of strings, which is UTF-8.</summary>
    public Encoding Encoding => Encoding.UTF8;

    /// <summary>Initializes a new reader at the packet's current position, without a parser context.</summary>
    /// <param name="packet">The packet to read from.</param>
    public PacketReader(IPacket packet) : this(packet, ref packet.Position) { }

    /// <summary>Reads the specified number of bytes.</summary>
    /// <param name="n">The number of bytes to read.</param>
    /// <returns>The bytes that were read.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="n"/> is negative.</exception>
    /// <exception cref="IndexOutOfRangeException">Thrown when fewer than <paramref name="n"/> bytes are left.</exception>
    public ReadOnlySpan<byte> ReadSpan(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (Pos + n > Span.Length)
            throw new IndexOutOfRangeException($"Attempted to read past the packet length: {n} bytes from position {Pos} when length is {Length}.");
        Pos += n;
        return Span[(Pos - n)..Pos];
    }

    /// <summary>Reads a boolean stored as one byte, where any nonzero value is <see langword="true"/>.</summary>
    /// <returns>The value that was read.</returns>
    public bool ReadBool() => ReadSpan(1)[0] != 0;

    /// <summary>Reads one byte.</summary>
    /// <returns>The value that was read.</returns>
    public byte ReadByte() => ReadSpan(1)[0];

    /// <summary>Reads a 16-bit big-endian signed integer.</summary>
    /// <returns>The value that was read.</returns>
    public short ReadShort() => BinaryPrimitives.ReadInt16BigEndian(ReadSpan(2));

    /// <summary>Reads an array length followed by that many 16-bit big-endian signed integers.</summary>
    /// <returns>The values that were read.</returns>
    public short[] ReadShortArray()
    {
        short[] array = new short[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadShort();
        return array;
    }

    /// <summary>Reads a 32-bit big-endian signed integer.</summary>
    /// <returns>The value that was read.</returns>
    public int ReadInt() => BinaryPrimitives.ReadInt32BigEndian(ReadSpan(4));

    /// <summary>Reads an array length followed by that many 32-bit big-endian signed integers.</summary>
    /// <returns>The values that were read.</returns>
    public int[] ReadIntArray()
    {
        int[] array = new int[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadInt();
        return array;
    }

    /// <summary>Reads a float, which Flash sends as a string.</summary>
    /// <returns>The value that was read, parsed with the invariant culture.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid number.</exception>
    public float ReadFloat() => (float)(FloatString)ReadString();

    /// <summary>Reads a 32-bit big-endian IEEE 754 float.</summary>
    /// <returns>The value that was read.</returns>
    public float ReadFloatBinary() => BinaryPrimitives.ReadSingleBigEndian(ReadSpan(4));

    /// <summary>Reads a 64-bit big-endian signed integer.</summary>
    /// <returns>The value that was read.</returns>
    public long ReadLong() => BinaryPrimitives.ReadInt64BigEndian(ReadSpan(8));

    /// <summary>Reads a 64-bit big-endian IEEE 754 double.</summary>
    /// <returns>The value that was read.</returns>
    public double ReadDouble() => BinaryPrimitives.ReadDoubleBigEndian(ReadSpan(8));

    /// <summary>Reads a UTF-8 string prefixed by its byte length as an unsigned 16-bit big-endian integer.</summary>
    /// <returns>The value that was read.</returns>
    public string ReadString() => Encoding.GetString(ReadSpan((ushort)ReadShort()));

    /// <summary>Reads an array length followed by that many strings.</summary>
    /// <returns>The values that were read.</returns>
    public string[] ReadStringArray()
    {
        string[] array = new string[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadString();
        return array;
    }

    /// <summary>Reads an ID, which Flash sends as a 32-bit big-endian signed integer.</summary>
    /// <returns>The value that was read.</returns>
    public Id ReadId() => ReadInt();

    /// <summary>Reads an array length followed by that many IDs.</summary>
    /// <returns>The values that were read.</returns>
    public Id[] ReadIdArray()
    {
        Id[] array = new Id[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = ReadId();
        return array;
    }

    /// <summary>Reads an array length, which Flash sends as a 32-bit big-endian signed integer.</summary>
    /// <returns>The value that was read.</returns>
    /// <exception cref="OverflowException">Thrown when the value is negative or greater than 65535.</exception>
    public Length ReadLength() => (Length)ReadInt();

    /// <summary>Reads a value of a type that implements <see cref="IParser{T}"/>.</summary>
    /// <typeparam name="T">The type to read.</typeparam>
    /// <returns>The value that was read.</returns>
    public T Parse<T>() where T : IParser<T> => T.Parse(in this);

    /// <summary>Reads an array length followed by that many values of a type that implements <see cref="IParser{T}"/>.</summary>
    /// <typeparam name="T">The type to read.</typeparam>
    /// <returns>The values that were read.</returns>
    public T[] ParseArray<T>() where T : IParser<T>
    {
        T[] array = new T[ReadLength()];
        for (int i = 0; i < array.Length; i++)
            array[i] = Parse<T>();
        return array;
    }
}
