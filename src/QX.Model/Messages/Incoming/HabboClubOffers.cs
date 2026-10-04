using Qx.Messages;
using Qx.Model;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents one club membership offer the hotel sells.</summary>
/// <param name="OfferId">The identifier of the offer.</param>
/// <param name="ProductCode">The product code of the offer.</param>
/// <param name="PriceCredits">The price in credits.</param>
/// <param name="PriceActivityPoints">The price in activity points.</param>
/// <param name="PriceActivityPointType">The activity point type of <paramref name="PriceActivityPoints"/>.</param>
/// <param name="IsVip">Whether the offer is a VIP membership.</param>
/// <param name="Months">The number of months of membership the offer grants.</param>
/// <param name="ExtraDays">The number of extra days of membership the offer grants.</param>
/// <param name="IsGiftable">Whether the offer can be bought as a gift.</param>
/// <param name="DaysLeftAfterPurchase">
/// The number of membership days the account would have left after buying the offer.
/// </param>
/// <param name="Year">The year of the expiry date the account would reach after buying the offer.</param>
/// <param name="Month">The month of the expiry date the account would reach after buying the offer.</param>
/// <param name="Day">The day of the expiry date the account would reach after buying the offer.</param>
public sealed record HabboClubOffer(
    int OfferId,
    string ProductCode,
    int PriceCredits,
    int PriceActivityPoints,
    int PriceActivityPointType,
    bool IsVip,
    int Months,
    int ExtraDays,
    bool IsGiftable,
    int DaysLeftAfterPurchase,
    int Year,
    int Month,
    int Day) : IParserComposer<HabboClubOffer>
{
    internal bool ReservedWireFlag { get; init; }

    /// <summary>Parses a club offer from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>An unnamed flag after the product code is kept internally so that composing writes it back.</remarks>
    public static HabboClubOffer Parse(in PacketReader p)
    {
        var strings = new CatalogStringBudget(1, SubscriptionAdjunctWire.MaximumStringBytes);
        return Parse(in p, 0, ref strings);
    }

    internal static HabboClubOffer Parse(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogStringBudget strings)
    {
        int offerId = p.ReadInt();
        string productCode = strings.Read(
            in p,
            nameof(ProductCode),
            checked(SubscriptionAdjunctWire.MinimumOfferTailSize + trailing_bytes));
        bool reserved_wire_flag = p.ReadBool();
        int priceCredits = p.ReadInt();
        int priceActivityPoints = p.ReadInt();
        int priceActivityPointType = p.ReadInt();
        bool isVip = p.ReadBool();
        int months = p.ReadInt();
        int extraDays = p.ReadInt();
        bool isGiftable = p.ReadBool();
        int daysLeftAfterPurchase = p.ReadInt();
        int year = p.ReadInt();
        int month = p.ReadInt();
        int day = p.ReadInt();
        return new HabboClubOffer(
            offerId,
            productCode,
            priceCredits,
            priceActivityPoints,
            priceActivityPointType,
            isVip,
            months,
            extraDays,
            isGiftable,
            daysLeftAfterPurchase,
            year,
            month,
            day)
        {
            ReservedWireFlag = reserved_wire_flag
        };
    }

    /// <summary>Composes the club offer into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p)
    {
        var strings = new CatalogStringBudget(1, SubscriptionAdjunctWire.MaximumStringBytes);
        strings.Require(ProductCode, nameof(ProductCode), in p);
        p.WriteInt(OfferId);
        p.WriteString(ProductCode);
        p.WriteBool(ReservedWireFlag);
        p.WriteInt(PriceCredits);
        p.WriteInt(PriceActivityPoints);
        p.WriteInt(PriceActivityPointType);
        p.WriteBool(IsVip);
        p.WriteInt(Months);
        p.WriteInt(ExtraDays);
        p.WriteBool(IsGiftable);
        p.WriteInt(DaysLeftAfterPurchase);
        p.WriteInt(Year);
        p.WriteInt(Month);
        p.WriteInt(Day);
    }
}

/// <summary>Represents the <c>HabboClubOffers</c> message, received with the club membership offers.</summary>
public sealed record HabboClubOffers : IParserComposer<HabboClubOffers>
{
    private IReadOnlyList<HabboClubOffer> _offers =
        Array.AsReadOnly(Array.Empty<HabboClubOffer>());

    /// <summary>Initializes a new instance of the <see cref="HabboClubOffers"/> record.</summary>
    /// <param name="offers">The offers, copied into a read only list.</param>
    /// <param name="daysLeft">The number of membership days the account has left.</param>
    public HabboClubOffers(IReadOnlyList<HabboClubOffer> offers, int daysLeft)
    {
        Offers = offers;
        DaysLeft = daysLeft;
    }

    /// <summary>Gets the offers, in the order the hotel sent them, as a read only copy.</summary>
    public IReadOnlyList<HabboClubOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            SubscriptionAdjunctWire.MaximumOfferCount,
            nameof(Offers));
    }

    /// <summary>Gets the number of membership days the account has left.</summary>
    public int DaysLeft { get; init; }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static HabboClubOffers Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static HabboClubOffers ParseFlash(in PacketReader p)
        => ParseSnapshot(in p);

    private static HabboClubOffers ParseSnapshot(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireMinimum(
            in p,
            CatalogWire.CountWidth + sizeof(int),
            nameof(HabboClubOffers));
        int count = CatalogWire.ReadCount(
            in p,
            SubscriptionAdjunctWire.MinimumOfferSize,
            sizeof(int),
            SubscriptionAdjunctWire.MaximumOfferCount,
            nameof(Offers));
        var strings = new CatalogStringBudget(
            SubscriptionAdjunctWire.MaximumOfferCount,
            SubscriptionAdjunctWire.MaximumStringBytes);
        var offers = new HabboClubOffer[count];
        for (int i = 0; i < count; i++)
        {
            int trailing_bytes = checked(
                sizeof(int) +
                (count - i - 1) * SubscriptionAdjunctWire.MinimumOfferSize);
            offers[i] = HabboClubOffer.Parse(in p, trailing_bytes, ref strings);
        }
        int days_left = p.ReadInt();
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(HabboClubOffers));
        return new HabboClubOffers(offers, days_left);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(HabboClubOffers value, in PacketWriter p) =>
        ComposeSnapshot(value, in p);

    private static void ComposeSnapshot(
        HabboClubOffers value,
        in PacketWriter p)
    {
        HabboClubOffer[] offers = CatalogWire.SnapshotReferences(
            value.Offers,
            SubscriptionAdjunctWire.MaximumOfferCount,
            nameof(Offers));
        var strings = new CatalogStringBudget(
            SubscriptionAdjunctWire.MaximumOfferCount,
            SubscriptionAdjunctWire.MaximumStringBytes);
        foreach (HabboClubOffer offer in offers)
            strings.Require(
                offer.ProductCode,
                nameof(HabboClubOffer.ProductCode),
                in p);

        CatalogWire.WriteCount(offers.Length, in p);
        foreach (HabboClubOffer offer in offers)
            p.Compose(offer);
        p.WriteInt(value.DaysLeft);
    }

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="offers">The offers.</param>
    /// <param name="daysLeft">The number of membership days the account has left.</param>
    public void Deconstruct(
        out IReadOnlyList<HabboClubOffer> offers,
        out int daysLeft)
    {
        offers = Offers;
        daysLeft = DaysLeft;
    }
}
