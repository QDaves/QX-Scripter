using System.Buffers.Binary;
using System.Text;

namespace Qx.Interception.GEarth;

/// <summary>Represents a forward-only reader over the body of a G-Earth control frame.</summary>
/// <remarks>Numbers are big-endian. Reading past the end of the body throws.</remarks>
/// <param name="body">The frame body to read.</param>
internal ref struct GControlReader(ReadOnlySpan<byte> body)
{
    private readonly ReadOnlySpan<byte> _span = body;
    private int _pos = 0;

    /// <summary>Gets the number of bytes left to read.</summary>
    public readonly int Available => _span.Length - _pos;
    /// <summary>Gets whether every byte of the body has been read.</summary>
    public readonly bool IsEof => _pos >= _span.Length;

    /// <summary>Reads one byte as a boolean, where any nonzero value is <see langword="true"/>.</summary>
    /// <returns>The value read.</returns>
    public bool ReadBool() => _span[_pos++] != 0;

    /// <summary>Reads a 32-bit integer.</summary>
    /// <returns>The value read.</returns>
    public int ReadInt()
    {
        int value = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_pos, 4));
        _pos += 4;
        return value;
    }

    /// <summary>Reads a Latin-1 string prefixed with an unsigned 16-bit byte length.</summary>
    /// <returns>The string read.</returns>
    public string ReadString()
    {
        int len = BinaryPrimitives.ReadUInt16BigEndian(_span.Slice(_pos, 2));
        _pos += 2;
        string value = Encoding.Latin1.GetString(_span.Slice(_pos, len));
        _pos += len;
        return value;
    }

    /// <summary>Reads a Latin-1 string prefixed with a 32-bit byte length.</summary>
    /// <returns>The string read.</returns>
    public string ReadLongString()
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_pos, 4));
        _pos += 4;
        string value = Encoding.Latin1.GetString(_span.Slice(_pos, len));
        _pos += len;
        return value;
    }
}
