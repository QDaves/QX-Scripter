using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FurniList : IParserComposer<FurniList>
{
    private IReadOnlyList<InventoryItem> _items = Array.Empty<InventoryItem>();

    public FurniList(int total, int index, IReadOnlyList<InventoryItem> items)
    {
        Total = total;
        Index = index;
        Items = items;
    }

    public int Total { get; init; }

    public int Index { get; init; }

    public IReadOnlyList<InventoryItem> Items
    {
        get => _items;
        init => _items = InventoryWire.FreezeReferences(value, nameof(Items));
    }

    public static FurniList Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FurniList ParseFlash(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();
        InventoryWire.RequireFragment(total, index, nameof(FurniList));
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available,
            35,
            nameof(Items));
        var items = new InventoryItem[count];
        for (int item_index = 0; item_index < items.Length; item_index++)
            items[item_index] = p.Parse<InventoryItem>();
        InventoryWire.RequireEmpty(in p, nameof(FurniList));
        return new FurniList(total, index, items);
    }

    private static FurniList ParseUnity(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();
        InventoryWire.RequireFragment(total, index, nameof(FurniList));
        int count = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available,
            56,
            nameof(Items));
        var items = new InventoryItem[count];
        for (int item_index = 0; item_index < items.Length; item_index++)
            items[item_index] = p.Parse<InventoryItem>();
        InventoryWire.RequireEmpty(in p, nameof(FurniList));
        return new FurniList(total, index, items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FurniList value, in PacketWriter p)
    {
        InventoryWire.RequireFragment(value.Total, value.Index, nameof(FurniList));
        foreach (InventoryItem item in value.Items)
            item.ValidateFlash(in p);
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);
        p.WriteInt(value.Items.Count);
        foreach (InventoryItem item in value.Items)
            p.Compose(item);
    }

    private static void ComposeUnity(FurniList value, in PacketWriter p)
    {
        InventoryWire.RequireFragment(value.Total, value.Index, nameof(FurniList));
        InventoryWire.RequireUnityCount(value.Items.Count, nameof(Items));
        foreach (InventoryItem item in value.Items)
            item.ValidateUnity(in p);
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);
        p.WriteLength((Length)value.Items.Count);
        foreach (InventoryItem item in value.Items)
            p.Compose(item);
    }
}

public sealed record FurniListAddOrUpdate : IParserComposer<FurniListAddOrUpdate>
{
    private IReadOnlyList<InventoryItem> _items = Array.Empty<InventoryItem>();

    public FurniListAddOrUpdate(IReadOnlyList<InventoryItem> items) => Items = items;

    public IReadOnlyList<InventoryItem> Items
    {
        get => _items;
        init => _items = InventoryWire.FreezeReferences(value, nameof(Items));
    }

    public static FurniListAddOrUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FurniListAddOrUpdate ParseFlash(in PacketReader p)
    {
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available,
            35,
            nameof(Items));
        var items = new InventoryItem[count];
        for (int item_index = 0; item_index < items.Length; item_index++)
            items[item_index] = p.Parse<InventoryItem>();
        InventoryWire.RequireEmpty(in p, nameof(FurniListAddOrUpdate));
        return new FurniListAddOrUpdate(items);
    }

    private static FurniListAddOrUpdate ParseUnity(in PacketReader p)
    {
        int count = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available,
            56,
            nameof(Items));
        var items = new InventoryItem[count];
        for (int item_index = 0; item_index < items.Length; item_index++)
            items[item_index] = p.Parse<InventoryItem>();
        InventoryWire.RequireEmpty(in p, nameof(FurniListAddOrUpdate));
        return new FurniListAddOrUpdate(items);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FurniListAddOrUpdate value, in PacketWriter p)
    {
        foreach (InventoryItem item in value.Items)
            item.ValidateFlash(in p);
        p.WriteInt(value.Items.Count);
        foreach (InventoryItem item in value.Items)
            p.Compose(item);
    }

    private static void ComposeUnity(FurniListAddOrUpdate value, in PacketWriter p)
    {
        InventoryWire.RequireUnityCount(value.Items.Count, nameof(Items));
        foreach (InventoryItem item in value.Items)
            item.ValidateUnity(in p);
        p.WriteLength((Length)value.Items.Count);
        foreach (InventoryItem item in value.Items)
            p.Compose(item);
    }
}

public sealed record FurniListRemove(Id ItemId) : IParserComposer<FurniListRemove>
{
    public static FurniListRemove Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FurniListRemove ParseFlash(in PacketReader p)
    {
        var value = new FurniListRemove(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(FurniListRemove));
        return value;
    }

    private static FurniListRemove ParseUnity(in PacketReader p)
    {
        var value = new FurniListRemove(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(FurniListRemove));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FurniListRemove value, in PacketWriter p) =>
        p.WriteInt(InventoryWire.Int32Id(value.ItemId));

    private static void ComposeUnity(FurniListRemove value, in PacketWriter p) =>
        p.WriteInt(InventoryWire.Int32Id(value.ItemId));
}

public sealed record FurniListRemoveMultiple : IParserComposer<FurniListRemoveMultiple>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    public FurniListRemoveMultiple(IReadOnlyList<Id> item_ids) => ItemIds = item_ids;

    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = InventoryWire.FreezeValues(value, nameof(ItemIds));
    }

    public static FurniListRemoveMultiple Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static FurniListRemoveMultiple ParseFlash(in PacketReader p)
    {
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available,
            sizeof(int),
            nameof(ItemIds));
        var item_ids = new Id[count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = p.ReadInt();
        InventoryWire.RequireEmpty(in p, nameof(FurniListRemoveMultiple));
        return new FurniListRemoveMultiple(item_ids);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(FurniListRemoveMultiple value, in PacketWriter p)
    {
        var item_ids = new int[value.ItemIds.Count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = InventoryWire.Int32Id(value.ItemIds[index]);
        p.WriteInt(item_ids.Length);
        foreach (int item_id in item_ids)
            p.WriteInt(item_id);
    }
}

public sealed record PostItPlaced(Id ItemId, int ItemsLeft) : IParserComposer<PostItPlaced>
{
    public static PostItPlaced Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PostItPlaced ParseFlash(in PacketReader p)
    {
        var value = new PostItPlaced(p.ReadInt(), p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(PostItPlaced));
        return value;
    }

    private static PostItPlaced ParseUnity(in PacketReader p)
    {
        var value = new PostItPlaced(p.ReadLong(), p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(PostItPlaced));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PostItPlaced value, in PacketWriter p)
    {
        int item_id = InventoryWire.Int32Id(value.ItemId);
        p.WriteInt(item_id);
        p.WriteInt(value.ItemsLeft);
    }

    private static void ComposeUnity(PostItPlaced value, in PacketWriter p)
    {
        p.WriteLong(value.ItemId);
        p.WriteInt(value.ItemsLeft);
    }
}

public sealed record FurniListInvalidate : IParserComposer<FurniListInvalidate>
{
    public static FurniListInvalidate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FurniListInvalidate ParseFlash(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(FurniListInvalidate));
        return new();
    }

    private static FurniListInvalidate ParseUnity(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(FurniListInvalidate));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FurniListInvalidate value, in PacketWriter p) { }

    private static void ComposeUnity(FurniListInvalidate value, in PacketWriter p) { }
}
