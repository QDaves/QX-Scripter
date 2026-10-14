using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FurniRecolorEntry(IReadOnlyList<Id> FurnitureIds, int Red, int Green, int Blue);

public sealed record FurniRecolorUpdate(IReadOnlyList<FurniRecolorEntry> Updates) : IParserComposer<FurniRecolorUpdate>
{
    public static FurniRecolorUpdate Parse(in PacketReader p) => FlashWire.Parse(in p, ParseFlash);

    private static FurniRecolorUpdate ParseFlash(in PacketReader p)
    {
        int count = InventoryWire.RequireCount(p.ReadLength(), p.Available, 10, nameof(Updates));
        var updates = new FurniRecolorEntry[count];
        for (int index = 0; index < count; index++)
        {
            int length = InventoryWire.RequireCount(p.ReadLength(), p.Available - 6, 4, nameof(FurniRecolorEntry.FurnitureIds));
            var ids = new Id[length];
            for (int item = 0; item < length; item++) ids[item] = p.ReadId();
            updates[index] = new(ids, p.ReadShort(), p.ReadShort(), p.ReadShort());
        }
        RoomPlacementWire.RequireEmpty(in p, nameof(FurniRecolorUpdate));
        return new(updates);
    }

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FurniRecolorUpdate value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Updates.Count);
        foreach (var update in value.Updates)
        {
            p.WriteLength((Length)update.FurnitureIds.Count);
            foreach (var id in update.FurnitureIds) p.WriteId(id);
            p.WriteShort(checked((short)update.Red));
            p.WriteShort(checked((short)update.Green));
            p.WriteShort(checked((short)update.Blue));
        }
    }
}
