using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record DiceValue(Id ItemId, int Value) : IParserComposer<DiceValue>
{
    public static DiceValue Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DiceValue ParseFlash(in PacketReader p) => new(p.ReadId(), p.ReadInt());

    private static DiceValue ParseUnity(in PacketReader p) => new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DiceValue value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.Value);
    }

    private static void ComposeUnity(DiceValue value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.Value);
    }
}
