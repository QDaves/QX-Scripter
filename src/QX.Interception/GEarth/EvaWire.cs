using System.Buffers.Binary;
using Qx.Messages;

namespace Qx.Interception.GEarth;

/// <summary>Provides conversion between packets and the raw wire bytes that G-Earth exchanges.</summary>
/// <remarks>
/// A raw packet is a big-endian 32-bit length that counts the header and body, a big-endian 16-bit
/// header and the body.
/// </remarks>
internal static class EvaWire
{
    /// <summary>Parses raw wire bytes into a packet.</summary>
    /// <param name="raw">The complete raw packet, including its length prefix.</param>
    /// <param name="direction">The direction of the packet.</param>
    /// <returns>A packet with a copy of the body.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when <paramref name="raw"/> is shorter than six bytes or its declared length does not match.
    /// </exception>
    public static Packet ToPacket(ReadOnlySpan<byte> raw, MessageDirection direction)
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
        return new Packet(new Header(direction, header), new PacketBuffer(body));
    }

    /// <summary>Encodes a packet into raw wire bytes.</summary>
    /// <param name="packet">The packet to encode.</param>
    /// <returns>The length prefix, header and body of the packet.</returns>
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
