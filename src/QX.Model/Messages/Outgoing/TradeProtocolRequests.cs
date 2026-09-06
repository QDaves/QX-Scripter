using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record OpenTradeRequest(int UserIndex) : IParserComposer<OpenTradeRequest>
{
    public static OpenTradeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OpenTradeRequest ParseFlash(in PacketReader p)
    {
        var value = new OpenTradeRequest(p.ReadInt());
        RequireUserIndex(value.UserIndex);
        TradeWire.RequireEmpty(in p, nameof(OpenTradeRequest));
        return value;
    }

    private static OpenTradeRequest ParseUnity(in PacketReader p)
    {
        var value = new OpenTradeRequest(p.ReadInt());
        RequireUserIndex(value.UserIndex);
        TradeWire.RequireEmpty(in p, nameof(OpenTradeRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OpenTradeRequest value, in PacketWriter p)
    {
        RequireUserIndex(value.UserIndex);
        p.WriteInt(value.UserIndex);
    }

    private static void ComposeUnity(OpenTradeRequest value, in PacketWriter p)
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

public sealed record AddTradeItemsRequest : IParserComposer<AddTradeItemsRequest>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    public AddTradeItemsRequest(IReadOnlyList<Id> item_ids) => ItemIds = item_ids;

    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = TradeWire.FreezeValues(value, nameof(ItemIds));
    }

    public static AddTradeItemsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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
        TradeWire.RequireDistinctIds(item_ids, true, nameof(ItemIds));
        TradeWire.RequireEmpty(in p, nameof(AddTradeItemsRequest));
        return new AddTradeItemsRequest(item_ids);
    }

    private static AddTradeItemsRequest ParseUnity(in PacketReader p)
    {
        int count = TradeWire.RequireCount(
            unchecked((ushort)p.ReadShort()),
            p.Available,
            sizeof(long),
            nameof(ItemIds));
        var item_ids = new Id[count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = p.ReadLong();
        TradeWire.RequireDistinctIds(item_ids, false, nameof(ItemIds));
        TradeWire.RequireEmpty(in p, nameof(AddTradeItemsRequest));
        return new AddTradeItemsRequest(item_ids);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AddTradeItemsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.ItemIds);
        TradeWire.RequireDistinctIds(value.ItemIds, true, nameof(ItemIds));
        var item_ids = new int[value.ItemIds.Count];
        for (int index = 0; index < item_ids.Length; index++)
            item_ids[index] = TradeWire.FlashId(value.ItemIds[index], nameof(ItemIds));
        p.WriteInt(item_ids.Length);
        foreach (int item_id in item_ids)
            p.WriteInt(item_id);
    }

    private static void ComposeUnity(AddTradeItemsRequest value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.ItemIds);
        TradeWire.RequireUnityCount(value.ItemIds.Count, nameof(ItemIds));
        TradeWire.RequireDistinctIds(value.ItemIds, false, nameof(ItemIds));
        p.WriteShort((short)(ushort)value.ItemIds.Count);
        foreach (Id item_id in value.ItemIds)
            p.WriteLong(item_id);
    }
}

public sealed record RemoveTradeItemRequest(Id ItemId) : IParserComposer<RemoveTradeItemRequest>
{
    public static RemoveTradeItemRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemoveTradeItemRequest ParseFlash(in PacketReader p)
    {
        var value = new RemoveTradeItemRequest(p.ReadInt());
        TradeWire.RequireNonZeroFlashId(value.ItemId, nameof(ItemId));
        TradeWire.RequireEmpty(in p, nameof(RemoveTradeItemRequest));
        return value;
    }

    private static RemoveTradeItemRequest ParseUnity(in PacketReader p)
    {
        var value = new RemoveTradeItemRequest(p.ReadLong());
        TradeWire.RequireNonZeroId(value.ItemId, nameof(ItemId));
        TradeWire.RequireEmpty(in p, nameof(RemoveTradeItemRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemoveTradeItemRequest value, in PacketWriter p)
    {
        TradeWire.RequireNonZeroFlashId(value.ItemId, nameof(ItemId));
        p.WriteInt(TradeWire.FlashId(value.ItemId, nameof(ItemId)));
    }

    private static void ComposeUnity(RemoveTradeItemRequest value, in PacketWriter p)
    {
        TradeWire.RequireNonZeroId(value.ItemId, nameof(ItemId));
        p.WriteLong(value.ItemId);
    }
}

public sealed record AcceptTradeRequest : IParserComposer<AcceptTradeRequest>
{
    public static AcceptTradeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AcceptTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(AcceptTradeRequest));
        return new();
    }

    private static AcceptTradeRequest ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(AcceptTradeRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AcceptTradeRequest value, in PacketWriter p) { }

    private static void ComposeUnity(AcceptTradeRequest value, in PacketWriter p) { }
}

public sealed record UnacceptTradeRequest : IParserComposer<UnacceptTradeRequest>
{
    public static UnacceptTradeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UnacceptTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(UnacceptTradeRequest));
        return new();
    }

    private static UnacceptTradeRequest ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(UnacceptTradeRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UnacceptTradeRequest value, in PacketWriter p) { }

    private static void ComposeUnity(UnacceptTradeRequest value, in PacketWriter p) { }
}

public sealed record ConfirmTradeRequest : IParserComposer<ConfirmTradeRequest>
{
    public static ConfirmTradeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ConfirmTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(ConfirmTradeRequest));
        return new();
    }

    private static ConfirmTradeRequest ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(ConfirmTradeRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ConfirmTradeRequest value, in PacketWriter p) { }

    private static void ComposeUnity(ConfirmTradeRequest value, in PacketWriter p) { }
}

public sealed record CloseTradeRequest : IParserComposer<CloseTradeRequest>
{
    public static CloseTradeRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CloseTradeRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(CloseTradeRequest));
        return new();
    }

    private static CloseTradeRequest ParseUnity(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(CloseTradeRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CloseTradeRequest value, in PacketWriter p) { }

    private static void ComposeUnity(CloseTradeRequest value, in PacketWriter p) { }
}

public sealed record GetNftTradeInventoryRequest : IParserComposer<GetNftTradeInventoryRequest>
{
    public static GetNftTradeInventoryRequest Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static GetNftTradeInventoryRequest ParseFlash(in PacketReader p)
    {
        TradeWire.RequireEmpty(in p, nameof(GetNftTradeInventoryRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(GetNftTradeInventoryRequest value, in PacketWriter p) { }
}
