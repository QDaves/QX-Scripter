namespace Qx.Messages;

/// <summary>Represents a packet, made of a header and a pooled body buffer.</summary>
/// <remarks>Dispose the packet to return its buffer memory to the pool.</remarks>
/// <param name="header">The message header.</param>
/// <param name="buffer">The body buffer, or <see langword="null"/> to start with an empty one.</param>
public sealed class Packet(Header header, PacketBuffer? buffer = null) : IPacket
{
    /// <summary>Gets or sets the message header.</summary>
    public Header Header { get; set; } = header;
    /// <summary>Gets the buffer that holds the packet body.</summary>
    public PacketBuffer Buffer { get; } = buffer ?? new PacketBuffer();
    /// <summary>Gets or sets the parser context that readers and writers of this packet receive, or <see langword="null"/> for none.</summary>
    public IParserContext? Context { get; set; }

    private int _position;
    /// <summary>Gets a reference to the current read and write position in the body.</summary>
    public ref int Position => ref _position;
    /// <summary>Gets the length of the packet body in bytes.</summary>
    public int Length => Buffer.Length;
    /// <summary>Gets the number of bytes between the current position and the end of the body.</summary>
    public int Available => Buffer.Length - Position;

    /// <summary>Creates a copy of the packet with the same header and context and its own body buffer.</summary>
    /// <remarks>The copy starts at position 0.</remarks>
    /// <returns>The copied packet.</returns>
    public Packet Copy() => new(Header, Buffer.Copy()) { Context = Context };
    IPacket IPacket.Copy() => Copy();

    /// <summary>Empties the packet body and resets the position to 0.</summary>
    public void Clear()
    {
        Position = 0;
        Buffer.Clear();
    }

    /// <summary>Returns the body buffer memory to the pool.</summary>
    public void Dispose() => Buffer.Dispose();

    /// <summary>Gets a reader that reads from the current position and advances it.</summary>
    /// <returns>A reader over this packet that receives its <see cref="Context"/>.</returns>
    public PacketReader Reader() => new(this, ref _position, Context);
    /// <summary>Gets a reader that reads from the specified position and advances it.</summary>
    /// <param name="pos">A reference to the position to read from.</param>
    /// <returns>A reader over this packet that receives its <see cref="Context"/>.</returns>
    public PacketReader ReaderAt(ref int pos) => new(this, ref pos, Context);
    /// <summary>Gets a writer that writes at the current position and advances it.</summary>
    /// <returns>A writer over this packet that receives its <see cref="Context"/>.</returns>
    public PacketWriter Writer() => new(this, ref _position, Context);
    /// <summary>Gets a writer that writes at the specified position and advances it.</summary>
    /// <param name="pos">A reference to the position to write at.</param>
    /// <returns>A writer over this packet that receives its <see cref="Context"/>.</returns>
    public PacketWriter WriterAt(ref int pos) => new(this, ref pos, Context);

    /// <summary>Reserves bytes at the current position, growing the body when needed, and advances past them.</summary>
    /// <param name="n">The number of bytes to reserve.</param>
    /// <returns>The reserved bytes, which overlap any existing bytes at that position.</returns>
    public Span<byte> Allocate(int n) => Writer().Allocate(n);
    /// <summary>Reads bytes from the current position and advances past them.</summary>
    /// <param name="n">The number of bytes to read.</param>
    /// <returns>The bytes that were read.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when fewer than <paramref name="n"/> bytes are left.</exception>
    public ReadOnlySpan<byte> ReadSpan(int n) => Reader().ReadSpan(n);
    /// <summary>Writes bytes at the current position, overwriting existing bytes, and advances past them.</summary>
    /// <param name="span">The bytes to write.</param>
    public void WriteSpan(ReadOnlySpan<byte> span) => Writer().WriteSpan(span);
}
