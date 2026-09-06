using Qx.Messages;

namespace Qx.Model;

public sealed class TradeOffer : IParserComposer<TradeOffer>
{
    private IReadOnlyList<TradeItem> _items = Array.Empty<TradeItem>();

    public Id UserId { get; set; }

    public IReadOnlyList<TradeItem> Items
    {
        get => _items;
        set => _items = TradeWire.FreezeReferences(value, nameof(Items));
    }

    public int FurniCount { get; set; }

    public int CreditCount { get; set; }

    public TradeOffer() { }

    public static TradeOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static TradeOffer ParseFlash(in PacketReader p)
    {
        Id user_id = p.ReadInt();
        TradeWire.RequirePositiveId(user_id, nameof(UserId));
        int item_count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available - sizeof(int) * 2,
            TradeWire.FlashTradeItemMinimumBytes,
            nameof(Items));
        var items = new TradeItem[item_count];
        for (int index = 0; index < items.Length; index++)
            items[index] = p.Parse<TradeItem>();
        int furni_count = p.ReadInt();
        int credit_count = p.ReadInt();
        TradeWire.RequireNonNegative(furni_count, nameof(FurniCount));
        TradeWire.RequireNonNegative(credit_count, nameof(CreditCount));
        RequireDistinctItemIds(items, nameof(Items));
        return new TradeOffer
        {
            UserId = user_id,
            Items = items,
            FurniCount = furni_count,
            CreditCount = credit_count
        };
    }

    private static TradeOffer ParseUnity(in PacketReader p)
    {
        Id user_id = p.ReadLong();
        TradeWire.RequirePositiveId(user_id, nameof(UserId));
        int item_count = TradeWire.RequireCount(
            p.ReadInt(),
            p.Available - sizeof(int) * 2,
            TradeWire.UnityTradeItemMinimumBytes,
            nameof(Items));
        var items = new TradeItem[item_count];
        for (int index = 0; index < items.Length; index++)
            items[index] = p.Parse<TradeItem>();
        int furni_count = p.ReadInt();
        int credit_count = p.ReadInt();
        TradeWire.RequireNonNegative(furni_count, nameof(FurniCount));
        TradeWire.RequireNonNegative(credit_count, nameof(CreditCount));
        RequireDistinctItemIds(items, nameof(Items));
        return new TradeOffer
        {
            UserId = user_id,
            Items = items,
            FurniCount = furni_count,
            CreditCount = credit_count
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(TradeOffer value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteInt(TradeWire.FlashId(value.UserId, nameof(UserId)));
        p.WriteInt(value.Items.Count);
        foreach (TradeItem item in value.Items)
            p.Compose(item);
        p.WriteInt(value.FurniCount);
        p.WriteInt(value.CreditCount);
    }

    private static void ComposeUnity(TradeOffer value, in PacketWriter p)
    {
        value.ValidateUnity(in p);
        p.WriteLong(value.UserId);
        p.WriteInt(value.Items.Count);
        foreach (TradeItem item in value.Items)
            p.Compose(item);
        p.WriteInt(value.FurniCount);
        p.WriteInt(value.CreditCount);
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        TradeWire.RequirePositiveFlashId(UserId, nameof(UserId));
        TradeWire.RequireNonNegative(FurniCount, nameof(FurniCount));
        TradeWire.RequireNonNegative(CreditCount, nameof(CreditCount));
        ArgumentNullException.ThrowIfNull(Items);
        RequireDistinctItemIds(Items, nameof(Items));
        foreach (TradeItem item in Items)
            item.ValidateFlash(in p);
    }

    internal void ValidateUnity(in PacketWriter p)
    {
        TradeWire.RequirePositiveId(UserId, nameof(UserId));
        TradeWire.RequireNonNegative(FurniCount, nameof(FurniCount));
        TradeWire.RequireNonNegative(CreditCount, nameof(CreditCount));
        ArgumentNullException.ThrowIfNull(Items);
        RequireDistinctItemIds(Items, nameof(Items));
        foreach (TradeItem item in Items)
            item.ValidateUnity(in p);
    }

    private static void RequireDistinctItemIds(IReadOnlyList<TradeItem> items, string name)
    {
        var seen = new HashSet<long>();
        foreach (TradeItem item in items)
        {
            ArgumentNullException.ThrowIfNull(item, name);
            long item_id = item.ItemId;
            if (!seen.Add(item_id))
                throw new InvalidDataException($"{name} contains duplicate item ID {item_id}.");
        }
    }
}
