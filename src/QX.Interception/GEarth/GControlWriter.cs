using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Qx.Interception.GEarth;

/// <summary>Represents a builder for one G-Earth control frame.</summary>
/// <remarks>Numbers are written big-endian.</remarks>
/// <param name="header">The control header of the frame, one of the <see cref="GControl"/> constants.</param>
internal sealed class GControlWriter(short header)
{
    private readonly ArrayBufferWriter<byte> _body = new();

    /// <summary>Gets the control header of the frame.</summary>
    public short Header { get; } = header;

    /// <summary>Writes one byte.</summary>
    /// <param name="value">The byte to write.</param>
    public void WriteByte(byte value)
    {
        _body.GetSpan(1)[0] = value;
        _body.Advance(1);
    }

    /// <summary>Writes a boolean as one byte, 1 for <see langword="true"/> and 0 for <see langword="false"/>.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteBool(bool value)
    {
        _body.GetSpan(1)[0] = (byte)(value ? 1 : 0);
        _body.Advance(1);
    }

    /// <summary>Writes a 32-bit integer.</summary>
    /// <param name="value">The value to write.</param>
    public void WriteInt(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_body.GetSpan(4), value);
        _body.Advance(4);
    }

    /// <summary>Writes raw bytes without a length prefix.</summary>
    /// <param name="bytes">The bytes to write.</param>
    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(_body.GetSpan(bytes.Length));
        _body.Advance(bytes.Length);
    }

    /// <summary>Writes a Latin-1 string prefixed with an unsigned 16-bit byte length.</summary>
    /// <remarks>
    /// Characters outside Latin-1 are written as <c>?</c>. The length prefix is not checked, so the string
    /// must not be longer than 65535 characters.
    /// </remarks>
    /// <param name="value">The string to write.</param>
    public void WriteString(string value)
    {
        int len = Encoding.Latin1.GetByteCount(value);
        BinaryPrimitives.WriteUInt16BigEndian(_body.GetSpan(2), (ushort)len);
        _body.Advance(2);
        Encoding.Latin1.GetBytes(value, _body.GetSpan(len));
        _body.Advance(len);
    }

    /// <summary>Writes a Latin-1 string prefixed with a 32-bit byte length.</summary>
    /// <remarks>Characters outside Latin-1 are written as <c>?</c>.</remarks>
    /// <param name="value">The string to write.</param>
    public void WriteLongString(string value)
    {
        int len = Encoding.Latin1.GetByteCount(value);
        BinaryPrimitives.WriteInt32BigEndian(_body.GetSpan(4), len);
        _body.Advance(4);
        Encoding.Latin1.GetBytes(value, _body.GetSpan(len));
        _body.Advance(len);
    }

    /// <summary>Builds the complete frame from the length prefix, the header and the body written so far.</summary>
    /// <returns>A new array that holds the frame.</returns>
    public byte[] ToFrame()
    {
        ReadOnlySpan<byte> body = _body.WrittenSpan;
        byte[] frame = new byte[6 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, 2 + body.Length);
        BinaryPrimitives.WriteInt16BigEndian(frame.AsSpan(4), Header);
        body.CopyTo(frame.AsSpan(6));
        return frame;
    }
}
