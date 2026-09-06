using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record YouAreNotSpectator(Id FlatId) : IParserComposer<YouAreNotSpectator>
{
    public Id RoomId => FlatId;

    public static YouAreNotSpectator Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static YouAreNotSpectator ParseFlash(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(YouAreNotSpectator value, in PacketWriter p) =>
        p.WriteId(value.FlatId);
}

public sealed record SpectatingEnded : IParserComposer<SpectatingEnded>
{
    public static SpectatingEnded Parse(in PacketReader p) =>
        ModernWireClients.ParseUnity(in p, ParseUnity);

    private static SpectatingEnded ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeUnity(this, in p, ComposeUnity);

    private static void ComposeUnity(SpectatingEnded value, in PacketWriter p)
    {
    }
}
