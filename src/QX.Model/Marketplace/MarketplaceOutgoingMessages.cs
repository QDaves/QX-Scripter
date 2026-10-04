using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents a request for the hotel's marketplace settings.</summary>
/// <remarks>Only the modern Flash marketplace layout has the message. The hotel answers with <see cref="MarketplaceConfiguration"/>.</remarks>
public sealed record GetMarketplaceConfiguration
    : IParserComposer<GetMarketplaceConfiguration>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetMarketplaceConfiguration Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceConfiguration ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceConfiguration));
        return new GetMarketplaceConfiguration();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        GetMarketplaceConfiguration value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

/// <summary>Represents a request that asks whether the local user may list a marketplace offer.</summary>
/// <remarks>Only the modern Flash marketplace layout has the message. The hotel answers with <see cref="MarketplaceCanMakeOfferResult"/>.</remarks>
public sealed record GetMarketplaceCanMakeOffer
    : IParserComposer<GetMarketplaceCanMakeOffer>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetMarketplaceCanMakeOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetMarketplaceCanMakeOffer ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(GetMarketplaceCanMakeOffer));
        return new GetMarketplaceCanMakeOffer();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        GetMarketplaceCanMakeOffer value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

/// <summary>Represents a request to buy a batch of marketplace listing tokens.</summary>
/// <remarks>Only the modern Flash marketplace layout has the message.</remarks>
public sealed record BuyMarketplaceTokens
    : IParserComposer<BuyMarketplaceTokens>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BuyMarketplaceTokens Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuyMarketplaceTokens ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(BuyMarketplaceTokens));
        return new BuyMarketplaceTokens();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        BuyMarketplaceTokens value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

/// <summary>Represents a request to list inventory items on the marketplace at one price each.</summary>
/// <remarks>
/// Sent as the Flash <c>MakeOffer</c> message.
/// The legacy Flash marketplace layout carries exactly one item id; the modern layout carries a list.
/// The hotel answers with <see cref="MarketplaceMakeOfferResult"/>.
/// </remarks>
public sealed record MakeMarketplaceOffer
    : IParserComposer<MakeMarketplaceOffer>
{
    private IReadOnlyList<Id> _item_ids = Array.Empty<Id>();

    /// <summary>Initializes a new instance of the <see cref="MakeMarketplaceOffer"/> record.</summary>
    /// <param name="price">The price per item in credits.</param>
    /// <param name="furniCategory">The category of the items, <see cref="MarketplaceFurniCategory.Floor"/> or <see cref="MarketplaceFurniCategory.Wall"/>.</param>
    /// <param name="itemIds">The inventory item ids to list.</param>
    public MakeMarketplaceOffer(
        int price,
        MarketplaceFurniCategory furniCategory,
        IReadOnlyList<Id> itemIds)
    {
        Price = price;
        FurniCategory = furniCategory;
        ItemIds = itemIds;
    }

    /// <summary>Gets the price per item in credits.</summary>
    public int Price { get; init; }
    /// <summary>Gets the category of the items, <see cref="MarketplaceFurniCategory.Floor"/> or <see cref="MarketplaceFurniCategory.Wall"/>.</summary>
    public MarketplaceFurniCategory FurniCategory { get; init; }

    /// <summary>Gets the inventory item ids to list, as a read only copy.</summary>
    public IReadOnlyList<Id> ItemIds
    {
        get => _item_ids;
        init => _item_ids = MarketplaceWire.FreezeValues(value, nameof(ItemIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Represents a request for the marketplace statistics of one furni kind.</summary>
/// <remarks>The hotel answers with <see cref="MarketplaceItemStats"/>.</remarks>
/// <param name="FurniCategory">The marketplace category of the furni.</param>
/// <param name="FurniTypeId">The furni type id.</param>
/// <param name="ExtraData">The extra data string, only sent by the modern Flash layout and only when not empty. The legacy layout requires an empty string.</param>
public sealed record GetMarketplaceItemStats(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    string ExtraData) : IParserComposer<GetMarketplaceItemStats>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Represents a marketplace offer search.</summary>
/// <remarks>Sent as the Flash <c>GetMarketplaceOffers</c> message. The hotel answers with <see cref="MarketplaceOffers"/>.</remarks>
/// <param name="MinimumPrice">The lowest price to include, in credits, or -1 for no lower bound.</param>
/// <param name="MaximumPrice">The highest price to include, in credits, or -1 for no upper bound.</param>
/// <param name="SearchQuery">The search text.</param>
/// <param name="SortOrder">The order of the results.</param>
/// <param name="CombineUniqueOffers">
/// Whether unique offers are grouped, required by the modern Flash layout and <see langword="null"/> on the legacy layout.
/// </param>
public sealed record SearchMarketplaceOffers(
    int MinimumPrice,
    int MaximumPrice,
    string SearchQuery,
    MarketplaceSortOrder SortOrder,
    bool? CombineUniqueOffers) : IParserComposer<SearchMarketplaceOffers>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Represents a request for the local user's own marketplace offers.</summary>
/// <remarks>The hotel answers with <see cref="MarketplaceOwnOffers"/>.</remarks>
/// <param name="Category">The slice of offers to list, required by the modern Flash layout and <see langword="null"/> on the legacy layout.</param>
public sealed record GetMarketplaceOwnOffers(
    MarketplaceOwnOffersCategory? Category)
    : IParserComposer<GetMarketplaceOwnOffers>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
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

/// <summary>Represents a request to buy a marketplace offer.</summary>
/// <remarks>
/// Flash buys by offer id, so the only concrete form is <see cref="BuyMarketplaceOffer"/>. The hotel
/// answers with <see cref="MarketplaceBuyResult"/>.
/// </remarks>
public abstract record MarketplaceBuyOfferRequest
    : IParserComposer<MarketplaceBuyOfferRequest>
{
    /// <summary>Parses the message from a packet as a <see cref="BuyMarketplaceOffer"/>.</summary>
    /// <param name="p">The packet reader.</param>
    public static MarketplaceBuyOfferRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static MarketplaceBuyOfferRequest ParseFlash(in PacketReader p) =>
        new BuyMarketplaceOffer(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">Thrown when the value is not a <see cref="BuyMarketplaceOffer"/>.</exception>
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

/// <summary>Represents a request to buy a marketplace offer by its id.</summary>
/// <param name="OfferId">The id of the offer to buy.</param>
public sealed record BuyMarketplaceOffer(Id OfferId)
    : MarketplaceBuyOfferRequest, IParserComposer<BuyMarketplaceOffer>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static new BuyMarketplaceOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuyMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());
}

/// <summary>Represents a request to cancel one of the local user's marketplace offers.</summary>
/// <remarks>The hotel answers with <see cref="MarketplaceCancelOfferResult"/>.</remarks>
/// <param name="OfferId">The id of the offer to cancel.</param>
public sealed record CancelMarketplaceOffer(Id OfferId)
    : IParserComposer<CancelMarketplaceOffer>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CancelMarketplaceOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelMarketplaceOffer ParseFlash(in PacketReader p) =>
        new(p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CancelMarketplaceOffer value, in PacketWriter p)
    {
        int offer_id = MarketplaceWire.FlashId(value.OfferId);
        p.WriteInt(offer_id);
    }
}

/// <summary>Represents a request to collect the credits from sold marketplace offers.</summary>
public sealed record RedeemMarketplaceOfferCredits
    : IParserComposer<RedeemMarketplaceOfferCredits>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static RedeemMarketplaceOfferCredits Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RedeemMarketplaceOfferCredits ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireEmpty(in p, nameof(RedeemMarketplaceOfferCredits));
        return new RedeemMarketplaceOfferCredits();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        RedeemMarketplaceOfferCredits value,
        in PacketWriter p)
    {
    }
}

/// <summary>Represents a request to cancel every open marketplace offer of the local user.</summary>
/// <remarks>Only the modern Flash marketplace layout has the message. The hotel answers with <see cref="MarketplaceCancelAllOffersResult"/>.</remarks>
public sealed record CancelAllMarketplaceOffers
    : IParserComposer<CancelAllMarketplaceOffers>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CancelAllMarketplaceOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CancelAllMarketplaceOffers ParseFlash(in PacketReader p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireEmpty(in p, nameof(CancelAllMarketplaceOffers));
        return new CancelAllMarketplaceOffers();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(
        CancelAllMarketplaceOffers value,
        in PacketWriter p) => MarketplaceWire.RequireModernFlash(in p);
}

/// <summary>Represents a request to clear the local user's sold or expired marketplace offer history.</summary>
/// <remarks>Only the modern Flash marketplace layout has the message. The hotel answers with <see cref="MarketplaceClearOwnHistoryResult"/>.</remarks>
/// <param name="Category">The history to clear, <see cref="MarketplaceOwnOffersCategory.Sold"/> or <see cref="MarketplaceOwnOffersCategory.Expired"/>.</param>
public sealed record ClearMarketplaceOwnHistory(
    MarketplaceOwnOffersCategory Category)
    : IParserComposer<ClearMarketplaceOwnHistory>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClearMarketplaceOwnHistory value, in PacketWriter p)
    {
        MarketplaceWire.RequireModernFlash(in p);
        MarketplaceWire.RequireHistoryCategory(value.Category);
        MarketplaceWire.WriteOwnOffersCategory(in p, value.Category);
    }
}
