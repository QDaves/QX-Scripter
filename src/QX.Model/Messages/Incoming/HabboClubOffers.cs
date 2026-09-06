using Qx.Messages;
using Qx.Model;

namespace Qx.Model.Messages.Incoming;

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

public sealed record HabboClubOffers : IParserComposer<HabboClubOffers>
{
    private IReadOnlyList<HabboClubOffer> _offers =
        Array.AsReadOnly(Array.Empty<HabboClubOffer>());

    public HabboClubOffers(IReadOnlyList<HabboClubOffer> Offers, int DaysLeft)
    {
        this.Offers = Offers;
        this.DaysLeft = DaysLeft;
    }

    public IReadOnlyList<HabboClubOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            SubscriptionAdjunctWire.MaximumOfferCount,
            nameof(Offers));
    }

    public int DaysLeft { get; init; }

    public static HabboClubOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HabboClubOffers ParseFlash(in PacketReader p)
        => ParseSnapshot(in p);

    private static HabboClubOffers ParseUnity(in PacketReader p)
        => ParseSnapshot(in p);

    private static HabboClubOffers ParseSnapshot(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireMinimum(
            in p,
            CatalogWire.CountWidth(p.Client) + sizeof(int),
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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HabboClubOffers value, in PacketWriter p) =>
        ComposeSnapshot(value, in p);

    private static void ComposeUnity(HabboClubOffers value, in PacketWriter p) =>
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

    public void Deconstruct(
        out IReadOnlyList<HabboClubOffer> Offers,
        out int DaysLeft)
    {
        Offers = this.Offers;
        DaysLeft = this.DaysLeft;
    }
}

public sealed record GetClubOffers(int OfferType) : IParserComposer<GetClubOffers>
{
    public static GetClubOffers Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetClubOffers ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static GetClubOffers ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static GetClubOffers ParseRequest(in PacketReader p)
    {
        SubscriptionAdjunctWire.RequireSize(in p, sizeof(int), nameof(GetClubOffers));
        var value = new GetClubOffers(p.ReadInt());
        SubscriptionAdjunctWire.RequireEmpty(in p, nameof(GetClubOffers));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetClubOffers value, in PacketWriter p) =>
        p.WriteInt(value.OfferType);

    private static void ComposeUnity(GetClubOffers value, in PacketWriter p) =>
        p.WriteInt(value.OfferType);
}

internal static class SubscriptionAdjunctWire
{
    public const int MaximumOfferCount = ushort.MaxValue;
    public const int MaximumStringBytes = 8 * 1024 * 1024;
    public const int MinimumOfferSize = 45;
    public const int MinimumOfferTailSize = 39;

    public static void RequireEmpty(in PacketReader p, string name)
    {
        if (p.Available != 0)
            throw new InvalidDataException($"{name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireSize(in PacketReader p, int expected, string name)
    {
        if (p.Available != expected)
        {
            throw new InvalidDataException(
                $"{name} requires exactly {expected} bytes, received {p.Available}.");
        }
    }

    public static void RequireMinimum(in PacketReader p, int minimum, string name)
    {
        if (p.Available < minimum)
        {
            throw new InvalidDataException(
                $"{name} requires at least {minimum} bytes, received {p.Available}.");
        }
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
