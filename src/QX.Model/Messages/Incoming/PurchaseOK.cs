using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the catalog offer a completed purchase was made from.</summary>
public sealed record PurchaseOffer : IParserComposer<PurchaseOffer>
{
    private string _localization_id = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());
    private IReadOnlyList<Id>? _room_items;
    private IReadOnlyList<Id>? _wall_items;

    /// <summary>Initializes a new instance of the <see cref="PurchaseOffer"/> class.</summary>
    /// <param name="offerId">The ID of the catalog offer.</param>
    /// <param name="localizationId">The localization key of the offer's name.</param>
    /// <param name="isRent">Whether the offer is a rental.</param>
    /// <param name="priceInCredits">The price in credits.</param>
    /// <param name="priceInActivityPoints">The price in activity points.</param>
    /// <param name="activityPointType">The activity point type the activity point price is paid in.</param>
    /// <param name="giftable">Whether the offer can be bought as a gift.</param>
    /// <param name="products">The products the offer contains.</param>
    /// <param name="clubLevel">The club level required to buy the offer.</param>
    /// <param name="bundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
    /// <param name="roomItems">The IDs of the floor items the hotel created for the purchase, or <see langword="null"/> when the message carries no purchase results.</param>
    /// <param name="wallItems">The IDs of the wall items the hotel created for the purchase, or <see langword="null"/> when the message carries no purchase results.</param>
    public PurchaseOffer(
        int offerId,
        string localizationId,
        bool isRent,
        int priceInCredits,
        int priceInActivityPoints,
        int activityPointType,
        bool giftable,
        IReadOnlyList<CatalogProduct> products,
        int clubLevel,
        bool bundlePurchaseAllowed,
        IReadOnlyList<Id>? roomItems = null,
        IReadOnlyList<Id>? wallItems = null)
    {
        OfferId = offerId;
        LocalizationId = localizationId;
        IsRent = isRent;
        PriceInCredits = priceInCredits;
        PriceInActivityPoints = priceInActivityPoints;
        ActivityPointType = activityPointType;
        Giftable = giftable;
        Products = products;
        ClubLevel = clubLevel;
        BundlePurchaseAllowed = bundlePurchaseAllowed;
        RoomItems = roomItems;
        WallItems = wallItems;
    }

    /// <summary>Gets the ID of the catalog offer.</summary>
    public int OfferId { get; init; }

    /// <summary>Gets the localization key of the offer's name.</summary>
    public string LocalizationId
    {
        get => _localization_id;
        init => _localization_id = CatalogWire.RequireReference(value, nameof(LocalizationId));
    }

    /// <summary>Gets whether the offer is a rental.</summary>
    public bool IsRent { get; init; }

    /// <summary>Gets the price in credits.</summary>
    public int PriceInCredits { get; init; }

    /// <summary>Gets the price in activity points of the type given by <see cref="ActivityPointType"/>.</summary>
    public int PriceInActivityPoints { get; init; }

    /// <summary>Gets the activity point type the activity point price is paid in.</summary>
    public int ActivityPointType { get; init; }

    /// <summary>Gets whether the offer can be bought as a gift.</summary>
    public bool Giftable { get; init; }

    /// <summary>Gets the products the offer contains, at most 65535.</summary>
    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPurchaseWire.MaximumProducts,
            nameof(Products));
    }

    /// <summary>Gets the club level required to buy the offer.</summary>
    public int ClubLevel { get; init; }

    /// <summary>Gets whether several of the offer can be bought at once.</summary>
    public bool BundlePurchaseAllowed { get; init; }

    /// <summary>
    /// Gets the IDs of the floor items the hotel created for the purchase, at most 65535, or
    /// <see langword="null"/> when the message carries no purchase results.
    /// </summary>
    public IReadOnlyList<Id>? RoomItems
    {
        get => _room_items;
        init => _room_items = CatalogPurchaseWire.FreezeItemIds(value, nameof(RoomItems));
    }

    /// <summary>
    /// Gets the IDs of the wall items the hotel created for the purchase, at most 65535, or
    /// <see langword="null"/> when the message carries no purchase results.
    /// </summary>
    public IReadOnlyList<Id>? WallItems
    {
        get => _wall_items;
        init => _wall_items = CatalogPurchaseWire.FreezeItemIds(value, nameof(WallItems));
    }

    /// <summary>Parses the offer from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PurchaseOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseOffer ParseFlash(in PacketReader p) =>
        CatalogPurchaseWire.ParseOffer(in p);

    /// <summary>Composes the offer into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseOffer value, in PacketWriter p) =>
        CatalogPurchaseWire.ComposeOffer(value, in p);

    /// <summary>Deconstructs the offer into its values.</summary>
    /// <param name="offerId">The ID of the catalog offer.</param>
    /// <param name="localizationId">The localization key of the offer's name.</param>
    /// <param name="isRent">Whether the offer is a rental.</param>
    /// <param name="priceInCredits">The price in credits.</param>
    /// <param name="priceInActivityPoints">The price in activity points.</param>
    /// <param name="activityPointType">The activity point type the activity point price is paid in.</param>
    /// <param name="giftable">Whether the offer can be bought as a gift.</param>
    /// <param name="products">The products the offer contains.</param>
    /// <param name="clubLevel">The club level required to buy the offer.</param>
    /// <param name="bundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
    public void Deconstruct(
        out int offerId,
        out string localizationId,
        out bool isRent,
        out int priceInCredits,
        out int priceInActivityPoints,
        out int activityPointType,
        out bool giftable,
        out IReadOnlyList<CatalogProduct> products,
        out int clubLevel,
        out bool bundlePurchaseAllowed)
    {
        offerId = OfferId;
        localizationId = LocalizationId;
        isRent = IsRent;
        priceInCredits = PriceInCredits;
        priceInActivityPoints = PriceInActivityPoints;
        activityPointType = ActivityPointType;
        giftable = Giftable;
        products = Products;
        clubLevel = ClubLevel;
        bundlePurchaseAllowed = BundlePurchaseAllowed;
    }

    /// <summary>Deconstructs the offer into its values, including the purchase results.</summary>
    /// <param name="offerId">The ID of the catalog offer.</param>
    /// <param name="localizationId">The localization key of the offer's name.</param>
    /// <param name="isRent">Whether the offer is a rental.</param>
    /// <param name="priceInCredits">The price in credits.</param>
    /// <param name="priceInActivityPoints">The price in activity points.</param>
    /// <param name="activityPointType">The activity point type the activity point price is paid in.</param>
    /// <param name="giftable">Whether the offer can be bought as a gift.</param>
    /// <param name="products">The products the offer contains.</param>
    /// <param name="clubLevel">The club level required to buy the offer.</param>
    /// <param name="bundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
    /// <param name="roomItems">The IDs of the floor items the hotel created for the purchase.</param>
    /// <param name="wallItems">The IDs of the wall items the hotel created for the purchase.</param>
    public void Deconstruct(
        out int offerId,
        out string localizationId,
        out bool isRent,
        out int priceInCredits,
        out int priceInActivityPoints,
        out int activityPointType,
        out bool giftable,
        out IReadOnlyList<CatalogProduct> products,
        out int clubLevel,
        out bool bundlePurchaseAllowed,
        out IReadOnlyList<Id>? roomItems,
        out IReadOnlyList<Id>? wallItems)
    {
        Deconstruct(
            out offerId,
            out localizationId,
            out isRent,
            out priceInCredits,
            out priceInActivityPoints,
            out activityPointType,
            out giftable,
            out products,
            out clubLevel,
            out bundlePurchaseAllowed);
        roomItems = RoomItems;
        wallItems = WallItems;
    }
}

/// <summary>Represents the <c>PurchaseOk</c> message, received when the server accepts a catalog purchase.</summary>
/// <remarks>
/// The hotel appends purchase results to the offer: the IDs of the floor items and of the wall items it created
/// for the purchase, a gift box included. The Flash client does not read them, and when a hotel leaves them out,
/// <see cref="PurchaseOffer.RoomItems"/> and <see cref="PurchaseOffer.WallItems"/> are <see langword="null"/>.
/// </remarks>
public sealed record PurchaseOK : IParserComposer<PurchaseOK>
{
    private PurchaseOffer _offer = null!;

    /// <summary>Initializes a new instance of the <see cref="PurchaseOK"/> class.</summary>
    /// <param name="offer">The offer that was bought.</param>
    public PurchaseOK(PurchaseOffer offer)
    {
        Offer = offer;
    }

    /// <summary>Gets the offer that was bought.</summary>
    public PurchaseOffer Offer
    {
        get => _offer;
        init => _offer = CatalogWire.RequireReference(value, nameof(Offer));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PurchaseOK Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseOK ParseFlash(in PacketReader p) =>
        new(CatalogPurchaseWire.ParseOffer(in p));

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseOK value, in PacketWriter p) =>
        CatalogPurchaseWire.ComposeOffer(value.Offer, in p);

    /// <summary>Deconstructs the message into its values.</summary>
    /// <param name="offer">The offer that was bought.</param>
    public void Deconstruct(out PurchaseOffer offer)
    {
        offer = Offer;
    }
}

internal static class CatalogPurchaseWire
{
    internal const int MaximumProducts = ushort.MaxValue;
    internal const int MaximumItemIds = ushort.MaxValue;
    private const int MaximumStrings = MaximumProducts * 2 + 1;
    private const int MaximumStringBytes = 16 * 1024 * 1024;
    private const int MinimumProductBytes = CatalogWire.StringMinimumBytes * 2;
    private const int FlashOfferTailBytes = sizeof(int) + sizeof(byte);
    private const int FlashIdBytes = sizeof(int);

    public static IReadOnlyList<Id>? FreezeItemIds(IReadOnlyList<Id>? values, string name) =>
        values is null
            ? null
            : CatalogWire.FreezeValues(values, MaximumItemIds, name);

    public static PurchaseOffer ParseOffer(in PacketReader p)
    {
        var strings = NewStringBudget();
        int offer_id = p.ReadInt();
        int count_width = CatalogWire.CountWidth;
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

        Id[]? room_items = null;
        Id[]? wall_items = null;
        if (p.Available > 0)
        {
            room_items = ReadItemIds(in p, count_width, nameof(PurchaseOffer.RoomItems));
            wall_items = ReadItemIds(in p, 0, nameof(PurchaseOffer.WallItems));
        }

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
            bundle_purchase_allowed,
            room_items,
            wall_items);
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
        if (prepared.RoomItems is not null)
        {
            p.WriteIdArray(prepared.RoomItems);
            p.WriteIdArray(prepared.WallItems!);
        }
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

        if (value.RoomItems is null && value.WallItems is null)
            return new PreparedPurchaseOffer(products, null, null);
        if (value.RoomItems is null || value.WallItems is null)
            throw new InvalidDataException("Purchase results require the room items and the wall items together.");
        Id[] room_items = SnapshotItemIds(value.RoomItems, nameof(PurchaseOffer.RoomItems));
        Id[] wall_items = SnapshotItemIds(value.WallItems, nameof(PurchaseOffer.WallItems));
        return new PreparedPurchaseOffer(products, room_items, wall_items);
    }

    private static Id[] ReadItemIds(in PacketReader p, int trailing_bytes, string name)
    {
        int count = CatalogWire.ReadCount(
            in p,
            FlashIdBytes,
            trailing_bytes,
            MaximumItemIds,
            name);
        var values = new Id[count];
        for (int index = 0; index < values.Length; index++)
            values[index] = p.ReadId();
        return values;
    }

    private static Id[] SnapshotItemIds(IReadOnlyList<Id> values, string name)
    {
        Id[] snapshot = CatalogWire.SnapshotValues(values, MaximumItemIds, name);
        foreach (Id value in snapshot)
            RequireFlashId(value);
        return snapshot;
    }

    private static void RequireFlashId(Id value) =>
        _ = checked((int)(long)value);

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
            RequireBadgeFields(value);
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

    private static void RequireBadgeFields(CatalogProduct value)
    {
        if (value.FurniClassId != 0 ||
            value.ProductCount != 1 ||
            value.UniqueLimitedItem ||
            value.UniqueLimitedItemSeriesSize != 0 ||
            value.UniqueLimitedItemsLeft != 0)
        {
            throw new InvalidDataException(
                "Flash badge products contain fields absent from the wire layout.");
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
        CatalogProduct[] Products,
        Id[]? RoomItems,
        Id[]? WallItems);
}
