using System.Buffers.Binary;
using Qx.Messages;

namespace Qx.Interception.GEarth;

public static class EvaWire
{
    public static Packet ToPacket(ReadOnlySpan<byte> raw, ClientType client, Direction direction)
    {
        if (raw.Length < 6)
            throw new InvalidDataException("The intercepted packet is shorter than its wire header.");
        int declared_length = BinaryPrimitives.ReadInt32BigEndian(raw);
        if (declared_length < 2 || declared_length != raw.Length - 4)
        {
            throw new InvalidDataException(
                $"The intercepted packet is incomplete: declared {(long)declared_length + 4} bytes, received {raw.Length}.");
        }
        short header = BinaryPrimitives.ReadInt16BigEndian(raw.Slice(4, 2));
        ReadOnlySpan<byte> body = raw[6..];
        return new Packet(new Header(direction, header), client, new PacketBuffer(body));
    }

    public static byte[] FromPacket(IPacket packet)
    {
        ReadOnlySpan<byte> body = packet.Buffer.Span;
        byte[] raw = new byte[6 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(raw, 2 + body.Length);
        BinaryPrimitives.WriteInt16BigEndian(raw.AsSpan(4), packet.Header.Value);
        body.CopyTo(raw.AsSpan(6));
        return raw;
    }
}
