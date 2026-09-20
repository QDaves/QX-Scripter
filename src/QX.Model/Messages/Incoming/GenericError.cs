using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GenericError(int ErrorCode) : IParserComposer<GenericError>
{
    public static GenericError Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GenericError ParseFlash(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GenericError value, in PacketWriter p) =>
        p.WriteInt(value.ErrorCode);
}
