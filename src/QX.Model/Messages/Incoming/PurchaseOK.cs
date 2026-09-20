using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PurchaseOffer : IParserComposer<PurchaseOffer>
{
    private string _localization_id = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());

    public PurchaseOffer(
        int OfferId,
        string LocalizationId,
        bool IsRent,
        int PriceInCredits,
        int PriceInActivityPoints,
        int ActivityPointType,
        bool Giftable,
        IReadOnlyList<CatalogProduct> Products,
        int ClubLevel,
        bool BundlePurchaseAllowed)
    {
        this.OfferId = OfferId;
        this.LocalizationId = LocalizationId;
        this.IsRent = IsRent;
        this.PriceInCredits = PriceInCredits;
        this.PriceInActivityPoints = PriceInActivityPoints;
        this.ActivityPointType = ActivityPointType;
        this.Giftable = Giftable;
        this.Products = Products;
        this.ClubLevel = ClubLevel;
        this.BundlePurchaseAllowed = BundlePurchaseAllowed;
    }

    public int OfferId { get; init; }

    public string LocalizationId
    {
        get => _localization_id;
        init => _localization_id = CatalogWire.RequireReference(value, nameof(LocalizationId));
    }

    public bool IsRent { get; init; }

    public int PriceInCredits { get; init; }

    public int PriceInActivityPoints { get; init; }

    public int ActivityPointType { get; init; }

    public bool Giftable { get; init; }

    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPurchaseWire.MaximumProducts,
            nameof(Products));
    }

    public int ClubLevel { get; init; }

    public bool BundlePurchaseAllowed { get; init; }

    public static PurchaseOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseOffer ParseFlash(in PacketReader p) =>
        CatalogPurchaseWire.ParseOffer(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseOffer value, in PacketWriter p) =>
        CatalogPurchaseWire.ComposeOffer(value, in p);

    public void Deconstruct(
        out int OfferId,
        out string LocalizationId,
        out bool IsRent,
        out int PriceInCredits,
        out int PriceInActivityPoints,
        out int ActivityPointType,
        out bool Giftable,
        out IReadOnlyList<CatalogProduct> Products,
        out int ClubLevel,
        out bool BundlePurchaseAllowed)
    {
        OfferId = this.OfferId;
        LocalizationId = this.LocalizationId;
        IsRent = this.IsRent;
        PriceInCredits = this.PriceInCredits;
        PriceInActivityPoints = this.PriceInActivityPoints;
        ActivityPointType = this.ActivityPointType;
        Giftable = this.Giftable;
        Products = this.Products;
        ClubLevel = this.ClubLevel;
        BundlePurchaseAllowed = this.BundlePurchaseAllowed;
    }
}

public sealed record PurchaseOK : IParserComposer<PurchaseOK>
{
    private PurchaseOffer _offer = null!;

    public PurchaseOK(PurchaseOffer Offer)
    {
        this.Offer = Offer;
    }

    public PurchaseOffer Offer
    {
        get => _offer;
        init => _offer = CatalogWire.RequireReference(value, nameof(Offer));
    }

    public static PurchaseOK Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseOK ParseFlash(in PacketReader p) =>
        new(CatalogPurchaseWire.ParseFlashAcceptedOffer(in p));

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseOK value, in PacketWriter p) =>
        CatalogPurchaseWire.ComposeOffer(value.Offer, in p);

    public void Deconstruct(out PurchaseOffer Offer)
    {
        Offer = this.Offer;
    }
}

internal static class CatalogPurchaseWire
{
    internal const int MaximumProducts = ushort.MaxValue;
    private const int MaximumStrings = MaximumProducts * 2 + 1;
    private const int MaximumStringBytes = 16 * 1024 * 1024;
    private const int MinimumProductBytes = CatalogWire.StringMinimumBytes * 2;
    private const int FlashAcceptedExtensionBytes = sizeof(int) * 3;
    private const int FlashOfferTailBytes = sizeof(int) + sizeof(byte);

    public static PurchaseOffer ParseOffer(in PacketReader p) =>
        ParseOffer(in p, true);

    public static PurchaseOffer ParseFlashAcceptedOffer(in PacketReader p)
    {
        PurchaseOffer offer = ParseOffer(in p, false);
        if (p.Available is not (0 or FlashAcceptedExtensionBytes))
        {
            throw new InvalidDataException(
                $"PurchaseOK contains {p.Available} unexpected bytes.");
        }
        return offer;
    }

    private static PurchaseOffer ParseOffer(in PacketReader p, bool require_empty)
    {
        var strings = NewStringBudget();
        int offer_id = p.ReadInt();
        int count_width = CatalogWire.CountWidth(p.Client);
        int offer_tail = FlashOfferTailBytes;
        int trailing_after_localization =
            sizeof(byte) + sizeof(int) * 3 + sizeof(byte) + count_width + offer_tail;
        RequireRemaining(
            in p,
            CatalogWire.StringMinimumBytes,
            trailing_after_localization,
            nameof(PurchaseOffer.LocalizationId));
        string localization_id = strings.Read(
            in p,
            nameof(PurchaseOffer.LocalizationId),
            trailing_after_localization);
        bool is_rent = p.ReadBool();
        int price_in_credits = p.ReadInt();
        int price_in_activity_points = p.ReadInt();
        int activity_point_type = p.ReadInt();
        bool giftable = p.ReadBool();

        int product_count = CatalogWire.ReadCount(
            in p,
            MinimumProductBytes,
            offer_tail,
            MaximumProducts,
            nameof(PurchaseOffer.Products));
        var products = new CatalogProduct[product_count];
        for (int index = 0; index < products.Length; index++)
        {
            int sibling_bytes = checked((products.Length - index - 1) * MinimumProductBytes);
            products[index] = ParseProduct(
                in p,
                checked(offer_tail + sibling_bytes),
                ref strings);
        }

        int club_level = p.ReadInt();
        bool bundle_purchase_allowed = p.ReadBool();

        if (require_empty)
            CatalogWire.RequireEmpty(in p, nameof(PurchaseOffer));
        return new PurchaseOffer(
            offer_id,
            localization_id,
            is_rent,
            price_in_credits,
            price_in_activity_points,
            activity_point_type,
            giftable,
            products,
            club_level,
            bundle_purchase_allowed);
    }

    public static void ComposeOffer(PurchaseOffer value, in PacketWriter p)
    {
        PreparedPurchaseOffer prepared = PrepareOffer(value, in p);
        p.WriteInt(value.OfferId);
        p.WriteString(value.LocalizationId);
        p.WriteBool(value.IsRent);
        p.WriteInt(value.PriceInCredits);
        p.WriteInt(value.PriceInActivityPoints);
        p.WriteInt(value.ActivityPointType);
        p.WriteBool(value.Giftable);
        CatalogWire.WriteCount(prepared.Products.Length, in p);
        for (int index = 0; index < prepared.Products.Length; index++)
        {
            WriteFlashProduct(prepared.Products[index], in p);
        }
        p.WriteInt(value.ClubLevel);
        p.WriteBool(value.BundlePurchaseAllowed);
    }

    private static CatalogProduct ParseProduct(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogStringBudget strings)
    {
        string product_type;
        {
            RequireRemaining(
                in p,
                CatalogWire.StringMinimumBytes,
                checked(trailing_bytes + CatalogWire.StringMinimumBytes),
                nameof(CatalogProduct.ProductType));
            product_type = strings.Read(
                in p,
                nameof(CatalogProduct.ProductType),
                checked(trailing_bytes + CatalogWire.StringMinimumBytes));
        }

        bool is_badge = product_type == CatalogProduct.TypeBadge;
        if (is_badge)
        {
            RequireRemaining(
                in p,
                CatalogWire.StringMinimumBytes,
                trailing_bytes,
                nameof(CatalogProduct.ExtraParam));
            return new CatalogProduct(
                product_type,
                0,
                strings.Read(in p, nameof(CatalogProduct.ExtraParam), trailing_bytes),
                1,
                false,
                0,
                0);
        }

        RequireRemaining(
            in p,
            sizeof(int) + CatalogWire.StringMinimumBytes + sizeof(int) + sizeof(byte),
            trailing_bytes,
            nameof(CatalogProduct));
        int furni_class_id = p.ReadInt();
        string extra_param = strings.Read(
            in p,
            nameof(CatalogProduct.ExtraParam),
            checked(trailing_bytes + sizeof(int) + sizeof(byte)));
        int product_count = p.ReadInt();
        bool unique_limited_item = p.ReadBool();
        int series_size = 0;
        int items_left = 0;
        if (unique_limited_item)
        {
            RequireRemaining(
                in p,
                sizeof(int) * 2,
                trailing_bytes,
                nameof(CatalogProduct));
            series_size = p.ReadInt();
            items_left = p.ReadInt();
        }
        return new CatalogProduct(
            product_type,
            furni_class_id,
            extra_param,
            product_count,
            unique_limited_item,
            series_size,
            items_left);
    }

    private static PreparedPurchaseOffer PrepareOffer(
        PurchaseOffer value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = NewStringBudget();
        strings.Require(value.LocalizationId, nameof(PurchaseOffer.LocalizationId), in p);
        CatalogProduct[] products = CatalogWire.SnapshotReferences(
            value.Products,
            MaximumProducts,
            nameof(PurchaseOffer.Products));
        for (int index = 0; index < products.Length; index++)
        {
            PrepareFlashProduct(products[index], ref strings, in p);
        }

        return new PreparedPurchaseOffer(products);
    }

    private static void PrepareFlashProduct(
        CatalogProduct value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ProductType, nameof(CatalogProduct.ProductType), in p);
        strings.Require(value.ExtraParam, nameof(CatalogProduct.ExtraParam), in p);
        RequireLimitedFields(value);
        if (value.ProductType == CatalogProduct.TypeBadge)
            RequireBadgeFields(value, "Flash");
    }

    private static void WriteFlashProduct(CatalogProduct value, in PacketWriter p)
    {
        p.WriteString(value.ProductType);
        if (value.ProductType == CatalogProduct.TypeBadge)
        {
            p.WriteString(value.ExtraParam);
            return;
        }
        WriteProductBody(value, in p);
    }

    private static void WriteProductBody(CatalogProduct value, in PacketWriter p)
    {
        p.WriteInt(value.FurniClassId);
        p.WriteString(value.ExtraParam);
        p.WriteInt(value.ProductCount);
        p.WriteBool(value.UniqueLimitedItem);
        if (value.UniqueLimitedItem)
        {
            p.WriteInt(value.UniqueLimitedItemSeriesSize);
            p.WriteInt(value.UniqueLimitedItemsLeft);
        }
    }

    private static void RequireLimitedFields(CatalogProduct value)
    {
        if (!value.UniqueLimitedItem &&
            (value.UniqueLimitedItemSeriesSize != 0 || value.UniqueLimitedItemsLeft != 0))
        {
            throw new InvalidDataException("Catalog product contains inactive limited-item fields.");
        }
    }

    private static void RequireBadgeFields(CatalogProduct value, string client)
    {
        if (value.FurniClassId != 0 ||
            value.ProductCount != 1 ||
            value.UniqueLimitedItem ||
            value.UniqueLimitedItemSeriesSize != 0 ||
            value.UniqueLimitedItemsLeft != 0)
        {
            throw new InvalidDataException(
                $"{client} badge products contain fields absent from the wire layout.");
        }
    }

    private static void RequireRemaining(
        in PacketReader p,
        int minimum_bytes,
        int trailing_bytes,
        string name)
    {
        if (p.Available < checked(minimum_bytes + trailing_bytes))
            throw new InvalidDataException($"{name} exceeds the remaining payload capacity.");
    }

    private static CatalogStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);

    private sealed record PreparedPurchaseOffer(
        CatalogProduct[] Products);
}
