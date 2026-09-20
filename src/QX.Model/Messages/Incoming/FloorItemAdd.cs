using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FloorItemAdd(FloorItem Item) : IParserComposer<FloorItemAdd>
{
    public static FloorItemAdd Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FloorItemAdd ParseFlash(in PacketReader p) => ParseItem(in p, 46);

    private static FloorItemAdd ParseItem(in PacketReader p, int minimum_size)
    {
        RoomPlacementWire.RequireMinimum(in p, minimum_size, nameof(FloorItemAdd));
        var item = p.Parse<FloorItem>();
        item.OwnerName = p.ReadString();
        RoomPlacementWire.RequireEmpty(in p, nameof(FloorItemAdd));
        return new FloorItemAdd(item);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FloorItemAdd value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateFloorItem(value.Item, true, in p);
        value.ComposeItem(in p);
    }

    private void ComposeItem(in PacketWriter p)
    {
        p.Compose(Item);
        p.WriteString(Item.OwnerName);
    }
}
