using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

public sealed record GetMarketplaceConfiguration
    : IParserComposer<GetMarketplaceConfiguration>
{
    public static GetMarketplaceConfiguration Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetMarketplaceConfiguration ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceConfiguration));
        return new GetMarketplaceConfiguration();
    }

    private static GetMarketplaceConfiguration ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceConfiguration));
        return new GetMarketplaceConfiguration();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        GetMarketplaceConfiguration value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);

    private static void ComposeUnity(
        GetMarketplaceConfiguration value,
        in PacketWriter p)
    {
    }
}

public sealed record GetMarketplaceCanMakeOffer
    : IParserComposer<GetMarketplaceCanMakeOffer>
{
    public static GetMarketplaceCanMakeOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetMarketplaceCanMakeOffer ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceCanMakeOffer));
        return new GetMarketplaceCanMakeOffer();
    }

    private static GetMarketplaceCanMakeOffer ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceCanMakeOffer));
        return new GetMarketplaceCanMakeOffer();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        GetMarketplaceCanMakeOffer value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);

    private static void ComposeUnity(
        GetMarketplaceCanMakeOffer value,
        in PacketWriter p)
    {
    }
}

public sealed record BuyMarketplaceTokens
    : IParserComposer<BuyMarketplaceTokens>
{
    public static BuyMarketplaceTokens Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuyMarketplaceTokens ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(BuyMarketplaceTokens));
        return new BuyMarketplaceTokens();
    }

    private static BuyMarketplaceTokens ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(BuyMarketplaceTokens));
        return new BuyMarketplaceTokens();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        BuyMarketplaceTokens value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);

    private static void ComposeUnity(
        BuyMarketplaceTokens value,
        in PacketWriter p)
    {
    }
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static MakeMarketplaceOffer ParseUnity(in PacketReader p)
    {
        int price = p.ReadInt();
        MarketplaceFurniCategory category =
            MarketplaceWire.ReadSellableCategory(in p);
        int count = p.ReadLength();
        var item_ids = new Id[count];
        for (int i = 0; i < count; i++)
            item_ids[i] = p.ReadLong();
        return new MakeMarketplaceOffer(price, category, item_ids);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(MakeMarketplaceOffer value, in PacketWriter p)
    {
        MarketplaceWire.RequireSellableCategory(value.FurniCategory);
        MarketplaceWire.RequireUnityCount(value.ItemIds.Count, nameof(ItemIds));

        p.WriteInt(value.Price);
        MarketplaceWire.WriteCategory(in p, value.FurniCategory);
        p.WriteLength((Length)value.ItemIds.Count);
        foreach (Id item_id in value.ItemIds)
            p.WriteLong(item_id);
    }
}

public sealed record GetMarketplaceItemStats(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    string ExtraData) : IParserComposer<GetMarketplaceItemStats>
{
    public static GetMarketplaceItemStats Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static GetMarketplaceItemStats ParseUnity(in PacketReader p)
    {
        MarketplaceFurniCategory category = MarketplaceWire.ReadCategory(in p);
        int furni_type_id = p.ReadInt();
        string extra_data = p.ReadString();
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceItemStats));
        return new GetMarketplaceItemStats(category, furni_type_id, extra_data);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(GetMarketplaceItemStats value, in PacketWriter p)
    {
        MarketplaceWire.RequireCategory(value.FurniCategory);
        MarketplaceWire.RequireString(value.ExtraData, nameof(ExtraData), in p);
        MarketplaceWire.WriteCategory(in p, value.FurniCategory);
        p.WriteInt(value.FurniTypeId);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static SearchMarketplaceOffers ParseUnity(in PacketReader p)
    {
        var result = new SearchMarketplaceOffers(
            p.ReadInt(),
            p.ReadInt(),
            p.ReadString(),
            MarketplaceWire.ReadSortOrder(in p),
            null);
        MarketplaceWire.RequireEmpty(in p, nameof(SearchMarketplaceOffers));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(SearchMarketplaceOffers value, in PacketWriter p)
    {
        MarketplaceWire.RequireString(value.SearchQuery, nameof(SearchQuery), in p);
        MarketplaceWire.RequireSortOrder(value.SortOrder);
        if (value.CombineUniqueOffers is not null)
        {
            throw new InvalidDataException(
                "Unity marketplace searches cannot represent the unique-offer grouping flag.");
        }
        WriteSearch(value, in p);
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetMarketplaceOwnOffers ParseFlash(in PacketReader p)
    {
        MarketplaceOwnOffersCategory? category =
            MarketplaceWire.FlashLayout(in p) is FlashMarketplaceWireLayout.Modern
                ? MarketplaceWire.ReadOwnOffersCategory(in p)
                : null;
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceOwnOffers));
        return new GetMarketplaceOwnOffers(category);
    }

    private static GetMarketplaceOwnOffers ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceOwnOffers));
        return new GetMarketplaceOwnOffers((MarketplaceOwnOffersCategory?)null);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(GetMarketplaceOwnOffers value, in PacketWriter p)
    {
        if (value.Category is not null)
        {
            throw new InvalidDataException(
                "Unity marketplace own-offer requests do not carry a category.");
        }
    }
}

public abstract record MarketplaceBuyOfferRequest
    : IParserComposer<MarketplaceBuyOfferRequest>
{
    public static MarketplaceBuyOfferRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MarketplaceBuyOfferRequest ParseFlash(in PacketReader p) =>
        new BuyMarketplaceOffer(p.ReadInt());

    private static MarketplaceBuyOfferRequest ParseUnity(in PacketReader p) =>
        MarketplaceWire.UnityBuyLayout(in p) switch
        {
            MarketplaceBuyWireLayout.OfferId =>
                new BuyMarketplaceOffer(p.ReadLong()),
            MarketplaceBuyWireLayout.FurniDetails =>
                new BuyMarketplaceOfferByDetails(
                    MarketplaceWire.ReadCategory(in p),
                    p.ReadInt(),
                    p.ReadInt(),
                    p.ReadString()),
            _ => throw new NotSupportedException(
                "The active Unity session has no compatible marketplace purchase wire layout.")
        };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(
        MarketplaceBuyOfferRequest value,
        in PacketWriter p)
    {
        MarketplaceBuyWireLayout layout = MarketplaceWire.UnityBuyLayout(in p);
        if (layout is MarketplaceBuyWireLayout.OfferId)
        {
            if (value is not BuyMarketplaceOffer by_offer_id)
            {
                throw new InvalidDataException(
                    "The active Unity build purchases marketplace offers by offer ID.");
            }
            p.WriteLong(by_offer_id.OfferId);
            return;
        }

        if (value is not BuyMarketplaceOfferByDetails by_details)
        {
            throw new InvalidDataException(
                "The active Unity build purchases marketplace offers by furniture details.");
        }
        MarketplaceWire.RequireCategory(by_details.FurniCategory);
        MarketplaceWire.RequireString(by_details.ExtraData, nameof(by_details.ExtraData), in p);
        MarketplaceWire.WriteCategory(in p, by_details.FurniCategory);
        p.WriteInt(by_details.FurniTypeId);
        p.WriteInt(by_details.Price);
        p.WriteString(by_details.ExtraData);
    }
}

public sealed record BuyMarketplaceOffer(Id OfferId)
    : MarketplaceBuyOfferRequest, IParserComposer<BuyMarketplaceOffer>
{
    public static new BuyMarketplaceOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuyMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static BuyMarketplaceOffer ParseUnity(in PacketReader p)
    {
        if (MarketplaceWire.UnityBuyLayout(in p) is not MarketplaceBuyWireLayout.OfferId)
        {
            throw new NotSupportedException(
                "The active Unity build does not purchase marketplace offers by offer ID.");
        }
        return new BuyMarketplaceOffer(p.ReadLong());
    }
}

public sealed record BuyMarketplaceOfferByDetails(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    int Price,
    string ExtraData)
    : MarketplaceBuyOfferRequest,
      IParserComposer<BuyMarketplaceOfferByDetails>
{
    public static new BuyMarketplaceOfferByDetails Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuyMarketplaceOfferByDetails ParseFlash(in PacketReader p) =>
        throw new NotSupportedException(
            "Flash marketplace purchases require an offer ID.");

    private static BuyMarketplaceOfferByDetails ParseUnity(in PacketReader p)
    {
        if (MarketplaceWire.UnityBuyLayout(in p) is not MarketplaceBuyWireLayout.FurniDetails)
        {
            throw new NotSupportedException(
                "The active Unity build does not purchase marketplace offers by furniture details.");
        }
        return new BuyMarketplaceOfferByDetails(
            MarketplaceWire.ReadCategory(in p),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadString());
    }
}

public sealed record CancelMarketplaceOffer(Id OfferId)
    : IParserComposer<CancelMarketplaceOffer>
{
    public static CancelMarketplaceOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CancelMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    private static CancelMarketplaceOffer ParseUnity(in PacketReader p) =>
        new(p.ReadLong());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CancelMarketplaceOffer value, in PacketWriter p)
    {
        int offer_id = MarketplaceWire.FlashId(value.OfferId);
        p.WriteInt(offer_id);
    }

    private static void ComposeUnity(CancelMarketplaceOffer value, in PacketWriter p) =>
        p.WriteLong(value.OfferId);
}

public sealed record RedeemMarketplaceOfferCredits
    : IParserComposer<RedeemMarketplaceOfferCredits>
{
    public static RedeemMarketplaceOfferCredits Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RedeemMarketplaceOfferCredits ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(RedeemMarketplaceOfferCredits));
        return new RedeemMarketplaceOfferCredits();
    }

    private static RedeemMarketplaceOfferCredits ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(RedeemMarketplaceOfferCredits));
        return new RedeemMarketplaceOfferCredits();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        RedeemMarketplaceOfferCredits value,
        in PacketWriter p)
    {
    }

    private static void ComposeUnity(
        RedeemMarketplaceOfferCredits value,
        in PacketWriter p)
    {
    }
}

public sealed record CancelAllMarketplaceOffers
    : IParserComposer<CancelAllMarketplaceOffers>
{
    public static CancelAllMarketplaceOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CancelAllMarketplaceOffers ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(CancelAllMarketplaceOffers));
        return new CancelAllMarketplaceOffers();
    }

    private static CancelAllMarketplaceOffers ParseUnity(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(CancelAllMarketplaceOffers));
        return new CancelAllMarketplaceOffers();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(
        CancelAllMarketplaceOffers value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);

    private static void ComposeUnity(
        CancelAllMarketplaceOffers value,
        in PacketWriter p)
    {
    }
}

public sealed record ClearMarketplaceOwnHistory(
    MarketplaceOwnOffersCategory Category)
    : IParserComposer<ClearMarketplaceOwnHistory>
{
    public static ClearMarketplaceOwnHistory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ClearMarketplaceOwnHistory ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceOwnOffersCategory category =
            MarketplaceWire.ReadOwnOffersCategory(in p);
        MarketplaceWire.RequireHistoryCategory(category);
        return new ClearMarketplaceOwnHistory(category);
    }

    private static ClearMarketplaceOwnHistory ParseUnity(in PacketReader p) =>
        throw new NotSupportedException(
            "Marketplace history clearing is only verified for Flash.");

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ClearMarketplaceOwnHistory value, in PacketWriter p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireHistoryCategory(value.Category);
        MarketplaceWire.WriteOwnOffersCategory(in p, value.Category);
    }

    private static void ComposeUnity(ClearMarketplaceOwnHistory value, in PacketWriter p) =>
        throw new NotSupportedException(
            "Marketplace history clearing is only verified for Flash.");
}
