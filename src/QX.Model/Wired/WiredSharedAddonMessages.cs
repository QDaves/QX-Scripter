using Qx.Messages;

namespace Qx.Model.Wired;

public sealed record SynchronizeSharedAddons(Id FurniId) : IParserComposer<SynchronizeSharedAddons>
{
    public static SynchronizeSharedAddons Parse(in PacketReader p) =>
        FlashWire.Parse(in p, read);

    private static SynchronizeSharedAddons read(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, write);

    private static void write(SynchronizeSharedAddons value, in PacketWriter p) =>
        p.WriteInt(WiredWire.FlashId(value.FurniId));
}
