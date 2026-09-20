using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record YouAreNotSpectator(Id FlatId) : IParserComposer<YouAreNotSpectator>
{
    public Id RoomId => FlatId;

    public static YouAreNotSpectator Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static YouAreNotSpectator ParseFlash(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(YouAreNotSpectator value, in PacketWriter p) =>
        p.WriteId(value.FlatId);
}
