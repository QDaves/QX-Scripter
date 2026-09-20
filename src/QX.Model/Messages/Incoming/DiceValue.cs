using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record DiceValue(Id ItemId, int Value) : IParserComposer<DiceValue>
{
    public static DiceValue Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DiceValue ParseFlash(in PacketReader p) => new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DiceValue value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.Value);
    }
}
