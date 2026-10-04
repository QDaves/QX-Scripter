using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Sent when the user opens a trade with another user in the room.</summary>
/// <remarks>Sent as the Flash <c>OpenTrading</c> message. Parsing and composing throw <see cref="InvalidDataException"/> when <paramref name="UserIndex"/> is negative.</remarks>
/// <param name="UserIndex">The room index of the user to trade with.</param>
public sealed record OpenTradeRequest(int UserIndex) : IParserComposer<OpenTradeRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static OpenTradeRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static OpenTradeRequest ParseFlash(in PacketReader p)
    {
        var value = new OpenTradeRequest(p.ReadInt());
        RequireUserIndex(value.UserIndex);
        TradeWire.RequireEmpty(in p, nameof(OpenTradeRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(OpenTradeRequest value, in PacketWriter p)
    {
        RequireUserIndex(value.UserIndex);
        p.WriteInt(value.UserIndex);
    }

    private static void RequireUserIndex(int user_index)
    {
        if (user_index < 0)
            throw new InvalidDataException("Trade target user indexes cannot be negative.");
    }
}

/// <summary>Sent when the user offers inventory items in the open trade.</summary>
/// <remarks>Sent as the Flash <c>AddItemsToTrade</c> message. The ids are written as a count followed by one 32 bit integer each, and composing throws <see cref="InvalidDataException"/> when an id is zero, repeated or does not fit in 32 bits.</remarks>
public sealed record AddTradeItemsRequest : IParserComposer<AddTradeItemsRequest>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="AddTradeItemsRequest"/> record.</summary>
    /// <param name="itemIds">The ids of the inventory items to offer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="itemIds"/> is <see langword="null"/>.</exception>
    public AddTradeItemsRequest(IReadOnlyList<Id> itemIds) => ItemIds = itemIds;

    /// <summary>Gets the ids of the inventory items to offer.</summary>
    /// <remarks>The list is copied when set.</remarks>
    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = TradeWire.FreezeValues(value, nameof(ItemIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AddTradeItemsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AddTradeItemsRequest ParseFlash(in PacketReader p)
    {
        int count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available,
            sizeof(int),
            nameof(ItemIds));
        var item_ids = new Id[count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = p.ReadInt();
        TradeWire.RequireDistinctIds(item_ids, nameof(ItemIds));
        TradeWire.RequireEmpty(in p, nameof(AddTradeItemsRequest));
        return new AddTradeItemsRequest(item_ids);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AddTradeItemsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.ItemIds);
        TradeWire.RequireDistinctIds(value.ItemIds, nameof(ItemIds));
        var item_ids = new int[value.ItemIds.Count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = TradeWire.FlashId(value.ItemIds[index], nameof(ItemIds));
        p.WriteInt(item_ids.Length);
        foreach (int item_id in item_ids)
            p.WriteInt(item_id);
    }
}

/// <summary>Sent when the user takes an item back out of the open trade.</summary>
/// <remarks>Sent as the Flash <c>RemoveItemFromTrade</c> message. Parsing and composing throw <see cref="InvalidDataException"/> when <paramref name="ItemId"/> is zero or does not fit in 32 bits.</remarks>
/// <param name="ItemId">The id of the offered item.</param>
public sealed record RemoveTradeItemRequest(Id ItemId) : IParserComposer<RemoveTradeItemRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RemoveTradeItemRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RemoveTradeItemRequest ParseFlash(in PacketReader p)
    {
        var value = new RemoveTradeItemRequest(p.ReadInt());
        TradeWire.RequireNonZeroFlashId(value.ItemId, nameof(ItemId));
        TradeWire.RequireEmpty(in p, nameof(RemoveTradeItemRequest));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RemoveTradeItemRequest value, in PacketWriter p)
    {
        TradeWire.RequireNonZeroFlashId(value.ItemId, nameof(ItemId));
        p.WriteInt(TradeWire.FlashId(value.ItemId, nameof(ItemId)));
    }
}

/// <summary>Sent when the user accepts the current offers in the open trade.</summary>
/// <remarks>Sent as the Flash <c>AcceptTrading</c> message, which carries no fields.</remarks>
public sealed record AcceptTradeRequest : IParserComposer<AcceptTradeRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AcceptTradeRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AcceptTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(AcceptTradeRequest));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AcceptTradeRequest value, in PacketWriter p) { }
}

/// <summary>Sent when the user withdraws the acceptance of the open trade.</summary>
/// <remarks>Sent as the Flash <c>UnacceptTrading</c> message, which carries no fields.</remarks>
public sealed record UnacceptTradeRequest : IParserComposer<UnacceptTradeRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UnacceptTradeRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UnacceptTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(UnacceptTradeRequest));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UnacceptTradeRequest value, in PacketWriter p) { }
}

/// <summary>Sent when the user gives the final confirmation of the open trade after both sides accepted.</summary>
/// <remarks>Sent as the Flash <c>ConfirmAcceptTrading</c> message, which carries no fields.</remarks>
public sealed record ConfirmTradeRequest : IParserComposer<ConfirmTradeRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ConfirmTradeRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ConfirmTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(ConfirmTradeRequest));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ConfirmTradeRequest value, in PacketWriter p) { }
}

/// <summary>Sent when the user closes the open trade.</summary>
/// <remarks>Sent as the Flash <c>CloseTrading</c> message, which carries no fields.</remarks>
public sealed record CloseTradeRequest : IParserComposer<CloseTradeRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CloseTradeRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CloseTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(CloseTradeRequest));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CloseTradeRequest value, in PacketWriter p) { }
}

/// <summary>Requests the NFT assets the user can offer in a trade.</summary>
/// <remarks>Sent as the Flash <c>GetNftTradeInventory</c> message, which carries no fields.</remarks>
public sealed record GetNftTradeInventoryRequest : IParserComposer<GetNftTradeInventoryRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetNftTradeInventoryRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetNftTradeInventoryRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(GetNftTradeInventoryRequest));
        return new();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetNftTradeInventoryRequest value, in PacketWriter p) { }
}
