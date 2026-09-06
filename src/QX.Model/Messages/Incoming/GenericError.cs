using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GenericError(int ErrorCode) : IParserComposer<GenericError>
{
    public static GenericError Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GenericError ParseFlash(in PacketReader p) => new(p.ReadInt());

    private static GenericError ParseUnity(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GenericError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);

    private static void ComposeUnity(GenericError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
