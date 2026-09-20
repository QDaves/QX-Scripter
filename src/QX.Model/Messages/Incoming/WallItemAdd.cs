using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record WallItemAdd(WallItem Item) : IParserComposer<WallItemAdd>
{
    public static WallItemAdd Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemAdd ParseFlash(in PacketReader p) => ParseItem(in p, 24);

    private static WallItemAdd ParseItem(in PacketReader p, int minimum_size)
    {
        RoomPlacementWire.RequireMinimum(in p, minimum_size, nameof(WallItemAdd));
        var item = p.Parse<WallItem>();
        item.OwnerName = p.ReadString();
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemAdd));
        return new WallItemAdd(item);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemAdd value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateWallItem(value.Item, true, in p);
        value.ComposeItem(in p);
    }

    private void ComposeItem(in PacketWriter p)
    {
        p.Compose(Item);
        p.WriteString(Item.OwnerName);
    }
}
