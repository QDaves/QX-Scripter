using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Doorbell(string UserName) : IParserComposer<Doorbell>
{

    public static Doorbell Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Doorbell ParseFlash(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Doorbell value, in PacketWriter p) =>
        p.WriteString(value.UserName);
}
