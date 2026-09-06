using System.Buffers.Binary;
using System.Text;

namespace Qx.Interception.GEarth;

public ref struct GControlReader(ReadOnlySpan<byte> body)
{
    private readonly ReadOnlySpan<byte> _span = body;
    private int _pos = 0;

    public readonly int Position => _pos;
    public readonly int Available => _span.Length - _pos;
    public readonly bool IsEof => _pos >= _span.Length;

    public byte ReadByte() => _span[_pos++];

    public bool ReadBool() => _span[_pos++] != 0;

    public short ReadShort()
    {
        short value = BinaryPrimitives.ReadInt16BigEndian(_span.Slice(_pos, 2));
        _pos += 2;
        return value;
    }

    public int ReadInt()
    {
        int value = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_pos, 4));
        _pos += 4;
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int n)
    {
        ReadOnlySpan<byte> slice = _span.Slice(_pos, n);
        _pos += n;
        return slice;
    }

    public string ReadString()
    {
        int len = BinaryPrimitives.ReadUInt16BigEndian(_span.Slice(_pos, 2));
        _pos += 2;
        string value = Encoding.Latin1.GetString(_span.Slice(_pos, len));
        _pos += len;
        return value;
    }

    public string ReadLongString()
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_pos, 4));
        _pos += 4;
        string value = Encoding.Latin1.GetString(_span.Slice(_pos, len));
        _pos += len;
        return value;
    }

    public string ReadLongStringUtf8()
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(_span.Slice(_pos, 4));
        _pos += 4;
        string value = Encoding.UTF8.GetString(_span.Slice(_pos, len));
        _pos += len;
        return value;
    }
}
