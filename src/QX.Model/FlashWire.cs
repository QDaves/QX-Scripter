using Qx.Messages;

namespace Qx.Model;

internal delegate T FlashPacketParser<T>(in PacketReader reader);

internal delegate void FlashPacketComposer<in T>(T value, in PacketWriter writer);

internal static class FlashWire
{
    public static T Parse<T>(in PacketReader reader, FlashPacketParser<T> parser)
    {
        if (reader.Client is not ClientType.Flash)
            throw new UnsupportedClientException(reader.Client);
        return parser(in reader);
    }

    public static void Compose<T>(T value, in PacketWriter writer, FlashPacketComposer<T> composer)
    {
        if (writer.Client is not ClientType.Flash)
            throw new UnsupportedClientException(writer.Client);
        composer(value, in writer);
    }
}
