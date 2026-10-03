namespace Qx.Messages;

/// <summary>Represents a packet, made of a header and a body buffer with a read and write position.</summary>
public interface IPacket : IDisposable
{
    /// <summary>Gets or sets the message header.</summary>
    Header Header { get; set; }
    /// <summary>Gets the parser context, or <see langword="null"/> when none is attached.</summary>
    IParserContext? Context { get; }
    /// <summary>Gets the buffer that holds the packet body.</summary>
    PacketBuffer Buffer { get; }
    /// <summary>Gets a reference to the current read and write position in the body.</summary>
    ref int Position { get; }
    /// <summary>Gets the length of the packet body in bytes.</summary>
    int Length { get; }

    /// <summary>Gets a reader that reads from the current position and advances it.</summary>
    /// <returns>A reader over this packet.</returns>
    PacketReader Reader();
    /// <summary>Gets a reader that reads from the specified position and advances it.</summary>
    /// <param name="pos">A reference to the position to read from.</param>
    /// <returns>A reader over this packet.</returns>
    PacketReader ReaderAt(ref int pos);
    /// <summary>Gets a writer that writes at the current position and advances it.</summary>
    /// <returns>A writer over this packet.</returns>
    PacketWriter Writer();
    /// <summary>Gets a writer that writes at the specified position and advances it.</summary>
    /// <param name="pos">A reference to the position to write at.</param>
    /// <returns>A writer over this packet.</returns>
    PacketWriter WriterAt(ref int pos);

    /// <summary>Reserves bytes at the current position, growing the body when needed, and advances past them.</summary>
    /// <param name="n">The number of bytes to reserve.</param>
    /// <returns>The reserved bytes, which overlap any existing bytes at that position.</returns>
    Span<byte> Allocate(int n);
    /// <summary>Reads bytes from the current position and advances past them.</summary>
    /// <param name="n">The number of bytes to read.</param>
    /// <returns>The bytes that were read.</returns>
    ReadOnlySpan<byte> ReadSpan(int n);
    /// <summary>Writes bytes at the current position, overwriting existing bytes, and advances past them.</summary>
    /// <param name="span">The bytes to write.</param>
    void WriteSpan(ReadOnlySpan<byte> span);

    /// <summary>Empties the packet body and resets the position to 0.</summary>
    void Clear();
    /// <summary>Creates a copy of the packet with its own body buffer.</summary>
    /// <returns>The copied packet.</returns>
    IPacket Copy();
}
