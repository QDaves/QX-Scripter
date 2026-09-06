using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FloorItemUpdate(FloorItem Item) : IParserComposer<FloorItemUpdate>
{
    public static FloorItemUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItemUpdate ParseFlash(in PacketReader p) => ParseItem(in p, 44);

    private static FloorItemUpdate ParseUnity(in PacketReader p) => ParseItem(in p, 60);

    private static FloorItemUpdate ParseItem(in PacketReader p, int minimum_size)
    {
        RoomPlacementWire.RequireMinimum(in p, minimum_size, nameof(FloorItemUpdate));
        var result = new FloorItemUpdate(p.Parse<FloorItem>());
        RoomPlacementWire.RequireEmpty(in p, nameof(FloorItemUpdate));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItemUpdate value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateFloorItem(value.Item, false, in p);
        p.Compose(value.Item);
    }

    private static void ComposeUnity(FloorItemUpdate value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateFloorItem(value.Item, false, in p);
        p.Compose(value.Item);
    }
}

public sealed record WallItemUpdate(WallItem Item) : IParserComposer<WallItemUpdate>
{
    public static WallItemUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WallItemUpdate ParseFlash(in PacketReader p) => ParseItem(in p, 22);

    private static WallItemUpdate ParseUnity(in PacketReader p) => ParseItem(in p, 32);

    private static WallItemUpdate ParseItem(in PacketReader p, int minimum_size)
    {
        RoomPlacementWire.RequireMinimum(in p, minimum_size, nameof(WallItemUpdate));
        var result = new WallItemUpdate(p.Parse<WallItem>());
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemUpdate));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WallItemUpdate value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateWallItem(value.Item, false, in p);
        p.Compose(value.Item);
    }

    private static void ComposeUnity(WallItemUpdate value, in PacketWriter p)
    {
        RoomPlacementWire.ValidateWallItem(value.Item, false, in p);
        p.Compose(value.Item);
    }
}

public sealed record FloorItemDataUpdate(Id Id, ItemData Data) : IParserComposer<FloorItemDataUpdate>
{
    public static FloorItemDataUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItemDataUpdate ParseFlash(in PacketReader p)
    {
        Id id = long.TryParse(p.ReadString(), out long value) ? value : 0;
        return ParseItem(in p, id);
    }

    private static FloorItemDataUpdate ParseUnity(in PacketReader p) =>
        ParseItem(in p, p.ReadId());

    private static FloorItemDataUpdate ParseItem(in PacketReader p, Id id) =>
        new(id, p.Parse<ItemData>());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItemDataUpdate value, in PacketWriter p)
    {
        p.WriteString(value.Id.ToString());
        p.Compose(value.Data);
    }

    private static void ComposeUnity(FloorItemDataUpdate value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.Compose(value.Data);
    }
}

public readonly record struct FloorDataEntry(Id Id, ItemData Data);

public sealed record FloorItemsDataUpdate(IReadOnlyList<FloorDataEntry> Items) : IParserComposer<FloorItemsDataUpdate>
{
    public static FloorItemsDataUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItemsDataUpdate ParseFlash(in PacketReader p) => ParseItems(in p);

    private static FloorItemsDataUpdate ParseUnity(in PacketReader p) => ParseItems(in p);

    private static FloorItemsDataUpdate ParseItems(in PacketReader p)
    {
        int count = p.ReadLength();
        var items = new FloorDataEntry[count];
        for (int i = 0; i < count; i++)
            items[i] = new FloorDataEntry(p.ReadId(), p.Parse<ItemData>());
        return new FloorItemsDataUpdate(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItemsDataUpdate value, in PacketWriter p) =>
        value.ComposeItems(in p);

    private static void ComposeUnity(FloorItemsDataUpdate value, in PacketWriter p) =>
        value.ComposeItems(in p);

    private void ComposeItems(in PacketWriter p)
    {
        p.WriteLength((Length)Items.Count);
        foreach (FloorDataEntry item in Items)
        {
            p.WriteId(item.Id);
            p.Compose(item.Data);
        }
    }
}
