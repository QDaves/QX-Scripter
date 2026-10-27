using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NftMarketplaceMakeOfferResult(int Result)
    : IParserComposer<NftMarketplaceMakeOfferResult>
{
    public static NftMarketplaceMakeOfferResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceMakeOfferResult Read(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceMakeOfferResult value, in PacketWriter p) =>
        p.WriteInt(value.Result);
}

public sealed record NftMarketplaceBuyOfferResult(string Result, long OfferId, int Price)
    : IParserComposer<NftMarketplaceBuyOfferResult>
{
    public static NftMarketplaceBuyOfferResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceBuyOfferResult Read(in PacketReader p) =>
        new(p.ReadString(), p.ReadLong(), p.ReadInt());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceBuyOfferResult value, in PacketWriter p)
    {
        MarketplaceWire.RequireString(value.Result, nameof(Result), in p);
        p.WriteString(value.Result);
        p.WriteLong(value.OfferId);
        p.WriteInt(value.Price);
    }
}

public sealed record NftMarketplaceItemStats : IParserComposer<NftMarketplaceItemStats>
{
    private IReadOnlyList<MarketplaceTradeInfo> _history = Array.Empty<MarketplaceTradeInfo>();

    public int AverageSalePrice { get; init; }
    public int OfferCount { get; init; }
    public int HistoryLengthDays { get; init; }
    public IReadOnlyList<MarketplaceTradeInfo> History
    {
        get => _history;
        init => _history = MarketplaceWire.FreezeValues(value, nameof(History));
    }
    public int LowestPrice { get; init; }
    public int SuggestedPrice { get; init; }

    public static NftMarketplaceItemStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceItemStats Read(in PacketReader p)
    {
        int average = p.ReadInt();
        int offers = p.ReadInt();
        int days = p.ReadInt();
        int count = MarketplaceWire.ReadFlashCount(in p, nameof(History));
        var rows = new MarketplaceTradeInfo[count];
        for (int i = 0; i < count; i++)
            rows[i] = new(p.ReadInt(), p.ReadInt(), p.ReadInt());
        return new()
        {
            AverageSalePrice = average,
            OfferCount = offers,
            HistoryLengthDays = days,
            History = rows,
            LowestPrice = p.ReadInt(),
            SuggestedPrice = p.ReadInt()
        };
    }

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceItemStats value, in PacketWriter p)
    {
        p.WriteInt(value.AverageSalePrice);
        p.WriteInt(value.OfferCount);
        p.WriteInt(value.HistoryLengthDays);
        p.WriteInt(value.History.Count);
        foreach (var row in value.History)
        {
            p.WriteInt(row.DayOffset);
            p.WriteInt(row.AverageSalePrice);
            p.WriteInt(row.SoldAmount);
        }
        p.WriteInt(value.LowestPrice);
        p.WriteInt(value.SuggestedPrice);
    }
}
