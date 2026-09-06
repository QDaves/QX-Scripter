using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Qx.Interception.GEarth;

public sealed class GControlWriter(short header)
{
    private readonly ArrayBufferWriter<byte> _body = new();

    public short Header { get; } = header;

    public void WriteByte(byte value)
    {
        _body.GetSpan(1)[0] = value;
        _body.Advance(1);
    }

    public void WriteBool(bool value)
    {
        _body.GetSpan(1)[0] = (byte)(value ? 1 : 0);
        _body.Advance(1);
    }

    public void WriteShort(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_body.GetSpan(2), value);
        _body.Advance(2);
    }

    public void WriteInt(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_body.GetSpan(4), value);
        _body.Advance(4);
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(_body.GetSpan(bytes.Length));
        _body.Advance(bytes.Length);
    }

    public void WriteString(string value)
    {
        int len = Encoding.Latin1.GetByteCount(value);
        BinaryPrimitives.WriteUInt16BigEndian(_body.GetSpan(2), (ushort)len);
        _body.Advance(2);
        Encoding.Latin1.GetBytes(value, _body.GetSpan(len));
        _body.Advance(len);
    }

    public void WriteLongString(string value)
    {
        int len = Encoding.Latin1.GetByteCount(value);
        BinaryPrimitives.WriteInt32BigEndian(_body.GetSpan(4), len);
        _body.Advance(4);
        Encoding.Latin1.GetBytes(value, _body.GetSpan(len));
        _body.Advance(len);
    }

    public void WriteLongStringUtf8(string value)
    {
        int len = Encoding.UTF8.GetByteCount(value);
        BinaryPrimitives.WriteInt32BigEndian(_body.GetSpan(4), len);
        _body.Advance(4);
        Encoding.UTF8.GetBytes(value, _body.GetSpan(len));
        _body.Advance(len);
    }

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
