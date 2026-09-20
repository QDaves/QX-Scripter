using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record GetMarketplaceConfiguration
    : IParserComposer<GetMarketplaceConfiguration>
{
    public static GetMarketplaceConfiguration Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceConfiguration ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceConfiguration));
        return new GetMarketplaceConfiguration();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        GetMarketplaceConfiguration value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

public sealed record GetMarketplaceCanMakeOffer
    : IParserComposer<GetMarketplaceCanMakeOffer>
{
    public static GetMarketplaceCanMakeOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceCanMakeOffer ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceCanMakeOffer));
        return new GetMarketplaceCanMakeOffer();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        GetMarketplaceCanMakeOffer value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

public sealed record BuyMarketplaceTokens
    : IParserComposer<BuyMarketplaceTokens>
{
    public static BuyMarketplaceTokens Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuyMarketplaceTokens ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(BuyMarketplaceTokens));
        return new BuyMarketplaceTokens();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        BuyMarketplaceTokens value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

public sealed record MakeMarketplaceOffer
    : IParserComposer<MakeMarketplaceOffer>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    public MakeMarketplaceOffer(
        int price,
        MarketplaceFurniCategory furni_category,
        IReadOnlyList<Id> item_ids)
    {
        Price = price;
        FurniCategory = furni_category;
        ItemIds = item_ids;
    }

    public int Price { get; init; }
    public MarketplaceFurniCategory FurniCategory { get; init; }

    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = MarketplaceWire.FreezeValues(value, nameof(ItemIds));
    }

    public static MakeMarketplaceOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MakeMarketplaceOffer ParseFlash(in PacketReader p)
    {
        int price = p.ReadInt();
        MarketplaceFurniCategory category =
            MarketplaceWire.ReadSellableCategory(in p);
        IReadOnlyList<Id> item_ids;
        if (MarketplaceWire.FlashLayout(in p) is FlashMarketplaceWireLayout.Legacy)
        {
            item_ids = [p.ReadInt()];
        }
        else
        {
            int count = MarketplaceWire.ReadFlashCount(in p, nameof(ItemIds));
            var values = new Id[count];
            for (int i = 0; i < count; i++)
                values[i] = p.ReadInt();
            item_ids = values;
        }
        return new MakeMarketplaceOffer(price, category, item_ids);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(MakeMarketplaceOffer value, in PacketWriter p)
    {
        MarketplaceWire.RequireSellableCategory(value.FurniCategory);
        FlashMarketplaceWireLayout layout = MarketplaceWire.FlashLayout(in p);
        if (layout is FlashMarketplaceWireLayout.Legacy && value.ItemIds.Count != 1)
        {
            throw new InvalidDataException(
                "Legacy Flash marketplace offers require exactly one item.");
        }

        var item_ids = new int[value.ItemIds.Count];
        for (int i = 0; i < item_ids.Length; i++)
            item_ids[i] = MarketplaceWire.FlashId(value.ItemIds[i]);

        p.WriteInt(value.Price);
        MarketplaceWire.WriteCategory(in p, value.FurniCategory);
        if (layout is FlashMarketplaceWireLayout.Legacy)
        {
            p.WriteInt(item_ids[0]);
            return;
        }

        p.WriteInt(item_ids.Length);
        foreach (int item_id in item_ids)
            p.WriteInt(item_id);
    }
}

public sealed record GetMarketplaceItemStats(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    string ExtraData) : IParserComposer<GetMarketplaceItemStats>
{
    public static GetMarketplaceItemStats Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceItemStats ParseFlash(in PacketReader p)
    {
        MarketplaceFurniCategory category = MarketplaceWire.ReadCategory(in p);
        int furni_type_id = p.ReadInt();
        string extra_data =
            MarketplaceWire.FlashLayout(in p) is FlashMarketplaceWireLayout.Modern &&
            p.Available > 0
                ? p.ReadString()
                : "";
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceItemStats));
        return new GetMarketplaceItemStats(category, furni_type_id, extra_data);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetMarketplaceItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequireCategory(value.FurniCategory);
        MarketplaceWire.RequireString(value.ExtraData, nameof(ExtraData), in p);
        FlashMarketplaceWireLayout layout = MarketplaceWire.FlashLayout(in p);
        if (layout is FlashMarketplaceWireLayout.Legacy && value.ExtraData.Length > 0)
        {
            throw new InvalidDataException(
                "Legacy Flash marketplace item-stats requests cannot represent extra data.");
        }

        MarketplaceWire.WriteCategory(in p, value.FurniCategory);
        p.WriteInt(value.FurniTypeId);
        if (layout is FlashMarketplaceWireLayout.Modern && value.ExtraData.Length > 0)
            p.WriteString(value.ExtraData);
    }
}

public sealed record SearchMarketplaceOffers(
    int MinimumPrice,
    int MaximumPrice,
    string SearchQuery,
    MarketplaceSortOrder SortOrder,
    bool? CombineUniqueOffers) : IParserComposer<SearchMarketplaceOffers>
{
    public static SearchMarketplaceOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SearchMarketplaceOffers ParseFlash(in PacketReader p)
    {
        int minimum_price = p.ReadInt();
        int maximum_price = p.ReadInt();
        string search_query = p.ReadString();
        MarketplaceSortOrder sort_order = MarketplaceWire.ReadSortOrder(in p);
        bool? combine_unique_offers =
            MarketplaceWire.FlashLayout(in p) is FlashMarketplaceWireLayout.Modern
                ? p.ReadBool()
                : null;
        MarketplaceWire.RequireEmpty(in p, nameof(SearchMarketplaceOffers));
        return new SearchMarketplaceOffers(
            minimum_price,
            maximum_price,
            search_query,
            sort_order,
            combine_unique_offers);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SearchMarketplaceOffers value, in PacketWriter p)
    {
        MarketplaceWire.RequireString(value.SearchQuery, nameof(SearchQuery), in p);
        MarketplaceWire.RequireSortOrder(value.SortOrder);
        FlashMarketplaceWireLayout layout = MarketplaceWire.FlashLayout(in p);
        if (layout is FlashMarketplaceWireLayout.Modern &&
            value.CombineUniqueOffers is null)
        {
            throw new InvalidDataException(
                "Modern Flash marketplace searches require the unique-offer grouping flag.");
        }
        if (layout is FlashMarketplaceWireLayout.Legacy &&
            value.CombineUniqueOffers is not null)
        {
            throw new InvalidDataException(
                "Legacy Flash marketplace searches cannot represent the unique-offer grouping flag.");
        }

        WriteSearch(value, in p);
        if (layout is FlashMarketplaceWireLayout.Modern)
            p.WriteBool(value.CombineUniqueOffers!.Value);
    }

    private static void WriteSearch(SearchMarketplaceOffers value, in PacketWriter p)
    {
        p.WriteInt(value.MinimumPrice);
        p.WriteInt(value.MaximumPrice);
        p.WriteString(value.SearchQuery);
        MarketplaceWire.WriteSortOrder(in p, value.SortOrder);
    }
}

public sealed record GetMarketplaceOwnOffers(
    MarketplaceOwnOffersCategory? Category)
    : IParserComposer<GetMarketplaceOwnOffers>
{
    public static GetMarketplaceOwnOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceOwnOffers ParseFlash(in PacketReader p)
    {
        MarketplaceOwnOffersCategory? category =
            MarketplaceWire.FlashLayout(in p) is FlashMarketplaceWireLayout.Modern
                ? MarketplaceWire.ReadOwnOffersCategory(in p)
                : null;
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceOwnOffers));
        return new GetMarketplaceOwnOffers(category);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetMarketplaceOwnOffers value, in PacketWriter p)
    {
        FlashMarketplaceWireLayout layout = MarketplaceWire.FlashLayout(in p);
        if (layout is FlashMarketplaceWireLayout.Modern)
        {
            MarketplaceOwnOffersCategory category = value.Category ??
                throw new InvalidDataException(
                    "Modern Flash marketplace own-offer requests require a category.");
            MarketplaceWire.RequireOwnOffersCategory(category);
            MarketplaceWire.WriteOwnOffersCategory(in p, category);
            return;
        }

        if (value.Category is not null)
        {
            throw new InvalidDataException(
                "Legacy Flash marketplace own-offer requests do not carry a category.");
        }
    }
}

public abstract record MarketplaceBuyOfferRequest
    : IParserComposer<MarketplaceBuyOfferRequest>
{
    public static MarketplaceBuyOfferRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceBuyOfferRequest ParseFlash(in PacketReader p) =>
        new BuyMarketplaceOffer(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        MarketplaceBuyOfferRequest value,
        in PacketWriter p)
    {
        if (value is not BuyMarketplaceOffer by_offer_id)
        {
            throw new InvalidDataException(
                "Flash marketplace purchases require an offer ID.");
        }
        int offer_id = MarketplaceWire.FlashId(by_offer_id.OfferId);
        p.WriteInt(offer_id);
    }
}

public sealed record BuyMarketplaceOffer(Id OfferId)
    : MarketplaceBuyOfferRequest, IParserComposer<BuyMarketplaceOffer>
{
    public static new BuyMarketplaceOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuyMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());
}

public sealed record CancelMarketplaceOffer(Id OfferId)
    : IParserComposer<CancelMarketplaceOffer>
{
    public static CancelMarketplaceOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CancelMarketplaceOffer value, in PacketWriter p)
    {
        int offer_id = MarketplaceWire.FlashId(value.OfferId);
        p.WriteInt(offer_id);
    }
}

public sealed record RedeemMarketplaceOfferCredits
    : IParserComposer<RedeemMarketplaceOfferCredits>
{
    public static RedeemMarketplaceOfferCredits Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RedeemMarketplaceOfferCredits ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(RedeemMarketplaceOfferCredits));
        return new RedeemMarketplaceOfferCredits();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        RedeemMarketplaceOfferCredits value,
        in PacketWriter p)
    {
    }
}

public sealed record CancelAllMarketplaceOffers
    : IParserComposer<CancelAllMarketplaceOffers>
{
    public static CancelAllMarketplaceOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelAllMarketplaceOffers ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(CancelAllMarketplaceOffers));
        return new CancelAllMarketplaceOffers();
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        CancelAllMarketplaceOffers value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

public sealed record ClearMarketplaceOwnHistory(
    MarketplaceOwnOffersCategory Category)
    : IParserComposer<ClearMarketplaceOwnHistory>
{
    public static ClearMarketplaceOwnHistory Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClearMarketplaceOwnHistory ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceOwnOffersCategory category =
            MarketplaceWire.ReadOwnOffersCategory(in p);
        MarketplaceWire.RequireHistoryCategory(category);
        return new ClearMarketplaceOwnHistory(category);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClearMarketplaceOwnHistory value, in PacketWriter p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireHistoryCategory(value.Category);
        MarketplaceWire.WriteOwnOffersCategory(in p, value.Category);
    }
}
