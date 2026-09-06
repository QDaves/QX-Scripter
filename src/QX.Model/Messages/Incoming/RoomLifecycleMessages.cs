using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomReady(string RoomType, Id RoomId) : IParserComposer<RoomReady>
{
    public static RoomReady Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomReady ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadId());

    private static RoomReady ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomReady value, in PacketWriter p)
    {
        p.WriteString(value.RoomType);
        p.WriteId(value.RoomId);
    }

    private static void ComposeUnity(RoomReady value, in PacketWriter p)
    {
        p.WriteString(value.RoomType);
        p.WriteId(value.RoomId);
    }
}

public sealed record RoomForward(Id RoomId) : IParserComposer<RoomForward>
{
    public static RoomForward Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomForward ParseFlash(in PacketReader p) => new(p.ReadId());

    private static RoomForward ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomForward value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(RoomForward value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

/// <summary>
/// The hotel closing the connection.
/// </summary>
/// <param name="Reason">
/// The trailing value, or <see langword="null"/> when the message carried none. It is a documented
/// reason code on Unity. On Flash the decompiled client's parser body is empty, so the value is
/// read but its meaning is not established for that client; live Flash sessions do carry two
/// trailing bytes here, and refusing to read them made the handler throw on every disconnect.
/// </param>
public sealed record CloseConnection(short? Reason) : IParserComposer<CloseConnection>
{
    public static CloseConnection Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CloseConnection ParseFlash(in PacketReader p) =>
        new(p.Available >= 2 ? p.ReadShort() : null);

    private static CloseConnection ParseUnity(in PacketReader p) =>
        new(p.ReadShort());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CloseConnection value, in PacketWriter p)
    {
        if (value.Reason is short reason)
            p.WriteShort(reason);
    }

    private static void ComposeUnity(CloseConnection value, in PacketWriter p)
    {
        p.WriteShort(value.Reason ?? 0);
    }
}

public sealed record RoomExitReason(Id RoomId, short Reason) : IParserComposer<RoomExitReason>
{
    public static RoomExitReason Parse(in PacketReader p) =>
        ModernWireClients.ParseUnity(in p, ParseUnity);

    private static RoomExitReason ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadShort());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeUnity(this, in p, ComposeUnity);

    private static void ComposeUnity(RoomExitReason value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteShort(value.Reason);
    }
}
