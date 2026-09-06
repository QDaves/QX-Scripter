using Qx.Messages;

namespace Qx.Model;

internal delegate T ModernPacketParser<T>(in PacketReader reader);

internal delegate void ModernPacketComposer<in T>(T value, in PacketWriter writer);

internal static class ModernWireClients
{
    public static T Parse<T>(
        in PacketReader reader,
        ModernPacketParser<T> flash,
        ModernPacketParser<T> unity) => reader.Client switch
    {
        ClientType.Flash => flash(in reader),
        ClientType.Unity => unity(in reader),
        _ => throw new UnsupportedClientException(reader.Client)
    };

    public static void Compose<T>(
        T value,
        in PacketWriter writer,
        ModernPacketComposer<T> flash,
        ModernPacketComposer<T> unity)
    {
        switch (writer.Client)
        {
            case ClientType.Flash:
                flash(value, in writer);
                return;
            case ClientType.Unity:
                unity(value, in writer);
                return;
            default:
                throw new UnsupportedClientException(writer.Client);
        }
    }

    public static T ParseFlash<T>(in PacketReader reader, ModernPacketParser<T> flash)
    {
        if (reader.Client is not ClientType.Flash)
            throw new UnsupportedClientException(reader.Client);
        return flash(in reader);
    }

    public static void ComposeFlash<T>(
        T value,
        in PacketWriter writer,
        ModernPacketComposer<T> flash)
    {
        if (writer.Client is not ClientType.Flash)
            throw new UnsupportedClientException(writer.Client);
        flash(value, in writer);
    }

    public static T ParseUnity<T>(in PacketReader reader, ModernPacketParser<T> unity)
    {
        if (reader.Client is not ClientType.Unity)
            throw new UnsupportedClientException(reader.Client);
        return unity(in reader);
    }

    public static void ComposeUnity<T>(
        T value,
        in PacketWriter writer,
        ModernPacketComposer<T> unity)
    {
        if (writer.Client is not ClientType.Unity)
            throw new UnsupportedClientException(writer.Client);
        unity(value, in writer);
    }
}
