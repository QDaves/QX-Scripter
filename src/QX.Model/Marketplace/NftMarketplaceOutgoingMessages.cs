using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record NftMarketplaceGetItemStats(string ProductCode)
    : IParserComposer<NftMarketplaceGetItemStats>
{
    public static NftMarketplaceGetItemStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceGetItemStats Read(in PacketReader p) =>
        new(p.ReadString());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceGetItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequireString(value.ProductCode, nameof(ProductCode), in p);
        p.WriteString(value.ProductCode);
    }
}

public sealed record NftMarketplaceBuyOffer(string ProductCode, int ExpectedPrice)
    : IParserComposer<NftMarketplaceBuyOffer>
{
    public static NftMarketplaceBuyOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceBuyOffer Read(in PacketReader p) =>
        new(p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceBuyOffer value, in PacketWriter p)
    {
        MarketplaceWire.RequireString(value.ProductCode, nameof(ProductCode), in p);
        p.WriteString(value.ProductCode);
        p.WriteInt(value.ExpectedPrice);
    }
}

public sealed record NftMarketplaceMakeOffer(
    MarketplaceFurniCategory FurniCategory, Id ItemId, int Price)
    : IParserComposer<NftMarketplaceMakeOffer>
{
    public static NftMarketplaceMakeOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, Read);

    private static NftMarketplaceMakeOffer Read(in PacketReader p) =>
        new(MarketplaceWire.ReadSellableCategory(in p), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) => FlashWire.Compose(this, in p, Write);

    private static void Write(NftMarketplaceMakeOffer value, in PacketWriter p)
    {
        MarketplaceWire.RequireSellableCategory(value.FurniCategory);
        int item_id = MarketplaceWire.FlashId(value.ItemId);
        MarketplaceWire.WriteCategory(in p, value.FurniCategory);
        p.WriteInt(item_id);
        p.WriteInt(value.Price);
    }
}
