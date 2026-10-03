using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>FurniList</c> message, received with one fragment of the user's furni inventory.</summary>
/// <remarks>
/// The server splits a large inventory into several fragments. The inventory is complete once every
/// fragment from 0 to <see cref="Total"/> minus one has been received.
/// </remarks>
public sealed record FurniList : IParserComposer<FurniList>
{
    private IReadOnlyList<InventoryItem> _items = Array.Empty<InventoryItem>();

    /// <summary>Initializes a new instance of the <see cref="FurniList"/> class.</summary>
    /// <param name="total">The total number of fragments.</param>
    /// <param name="index">The zero based index of this fragment.</param>
    /// <param name="items">The items in this fragment.</param>
    public FurniList(int total, int index, IReadOnlyList<InventoryItem> items)
    {
        Total = total;
        Index = index;
        Items = items;
    }

    /// <summary>Gets the total number of fragments the inventory is split into.</summary>
    public int Total { get; init; }

    /// <summary>Gets the zero based index of this fragment.</summary>
    public int Index { get; init; }

    /// <summary>Gets the items in this fragment.</summary>
    public IReadOnlyList<InventoryItem> Items
    {
        get => _items;
        init => _items = InventoryWire.FreezeReferences(value, nameof(Items));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FurniList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Represents the <c>FurniListAddOrUpdate</c> message, received when furni is added to or updated in the user's inventory.</summary>
public sealed record FurniListAddOrUpdate : IParserComposer<FurniListAddOrUpdate>
{
    private IReadOnlyList<InventoryItem> _items = Array.Empty<InventoryItem>();

    /// <summary>Initializes a new instance of the <see cref="FurniListAddOrUpdate"/> class.</summary>
    /// <param name="items">The added or updated items.</param>
    public FurniListAddOrUpdate(IReadOnlyList<InventoryItem> items) => Items = items;

    /// <summary>Gets the added or updated items.</summary>
    public IReadOnlyList<InventoryItem> Items
    {
        get => _items;
        init => _items = InventoryWire.FreezeReferences(value, nameof(Items));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FurniListAddOrUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FurniListAddOrUpdate value, in PacketWriter p)
    {
        foreach (InventoryItem item in value.Items)
            item.ValidateFlash(in p);
        p.WriteInt(value.Items.Count);
        foreach (InventoryItem item in value.Items)
            p.Compose(item);
    }
}

/// <summary>Represents the <c>FurniListRemove</c> message, received when an item is removed from the user's furni inventory.</summary>
/// <param name="ItemId">The inventory item ID of the removed item, as in <see cref="InventoryItem.ItemId"/>.</param>
public sealed record FurniListRemove(Id ItemId) : IParserComposer<FurniListRemove>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FurniListRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FurniListRemove ParseFlash(in PacketReader p)
    {
        var value = new FurniListRemove(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(FurniListRemove));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FurniListRemove value, in PacketWriter p) =>
        p.WriteInt(InventoryWire.Int32Id(value.ItemId));
}

/// <summary>Represents the <c>FurniListRemoveMultiple</c> message, received when several items are removed from the user's furni inventory.</summary>
public sealed record FurniListRemoveMultiple : IParserComposer<FurniListRemoveMultiple>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="FurniListRemoveMultiple"/> class.</summary>
    /// <param name="itemIds">The inventory item IDs of the removed items.</param>
    public FurniListRemoveMultiple(IReadOnlyList<Id> itemIds) => ItemIds = itemIds;

    /// <summary>Gets the inventory item IDs of the removed items, as in <see cref="InventoryItem.ItemId"/>.</summary>
    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = InventoryWire.FreezeValues(value, nameof(ItemIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FurniListRemoveMultiple Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

/// <summary>Represents the <c>PostItPlaced</c> message, received when the user places a post-it from a pad in their inventory.</summary>
/// <param name="ItemId">The inventory item ID of the post-it pad, as in <see cref="InventoryItem.ItemId"/>.</param>
/// <param name="ItemsLeft">The number of post-its left on the pad.</param>
public sealed record PostItPlaced(Id ItemId, int ItemsLeft) : IParserComposer<PostItPlaced>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PostItPlaced Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PostItPlaced ParseFlash(in PacketReader p)
    {
        var value = new PostItPlaced(p.ReadInt(), p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(PostItPlaced));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PostItPlaced value, in PacketWriter p)
    {
        int item_id = InventoryWire.Int32Id(value.ItemId);
        p.WriteInt(item_id);
        p.WriteInt(value.ItemsLeft);
    }
}

/// <summary>Represents the <c>FurniListInvalidate</c> message, received when the user's furni inventory is out of date and must be requested again.</summary>
public sealed record FurniListInvalidate : IParserComposer<FurniListInvalidate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FurniListInvalidate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FurniListInvalidate ParseFlash(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(FurniListInvalidate));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FurniListInvalidate value, in PacketWriter p) { }
}
