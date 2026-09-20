using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record WallItemRemove(Id Id, Id PickerId) : IParserComposer<WallItemRemove>
{
    public static WallItemRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemRemove ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 6, nameof(WallItemRemove));
        var result = new WallItemRemove(
            RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)),
            p.ReadId());
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemRemove));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId), in p);
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        p.WriteId(value.PickerId);
    }
}

public sealed record WallItemsRemove(IReadOnlyList<Id> Ids, Id PickerId)
    : IParserComposer<WallItemsRemove>
{
    public static WallItemsRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemsRemove ParseFlash(in PacketReader p)
    {
        int count = p.ReadLength();
        var ids = new Id[count];
        for (int i = 0; i < count; i++)
            ids[i] = p.ReadId();
        return new WallItemsRemove(ids, p.ReadId());
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemsRemove value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Ids.Count);
        foreach (Id id in value.Ids)
            p.WriteId(id);
        p.WriteId(value.PickerId);
    }
}
