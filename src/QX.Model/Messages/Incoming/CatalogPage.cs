using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Contains the images and texts shown on a catalog page.</summary>
public sealed record CatalogPageLocalization : IParserComposer<CatalogPageLocalization>
{
    private IReadOnlyList<string> _images = Array.AsReadOnly(Array.Empty<string>());
    private IReadOnlyList<string> _texts = Array.AsReadOnly(Array.Empty<string>());

    /// <summary>Initializes a new instance of the <see cref="CatalogPageLocalization"/> record.</summary>
    /// <param name="images">The image names of the page, copied into a read only list.</param>
    /// <param name="texts">The texts of the page, copied into a read only list.</param>
    public CatalogPageLocalization(IReadOnlyList<string> images, IReadOnlyList<string> texts)
    {
        Images = images;
        Texts = texts;
    }

    /// <summary>Gets the image names of the page, as a read only copy.</summary>
    public IReadOnlyList<string> Images
    {
        get => _images;
        init => _images = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumLocalizationEntries,
            nameof(Images));
    }

    /// <summary>Gets the texts of the page, as a read only copy.</summary>
    public IReadOnlyList<string> Texts
    {
        get => _texts;
        init => _texts = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumLocalizationEntries,
            nameof(Texts));
    }

    /// <summary>Parses the page localization from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogPageLocalization Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogPageLocalization ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneLocalization(in p);

    /// <summary>Composes the page localization into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogPageLocalization value, in PacketWriter p) =>
        CatalogPageWire.ComposeLocalization(value, in p);

    /// <summary>Deconstructs the localization into its images and texts.</summary>
    /// <param name="images">The image names of the page.</param>
    /// <param name="texts">The texts of the page.</param>
    public void Deconstruct(out IReadOnlyList<string> images, out IReadOnlyList<string> texts)
    {
        images = Images;
        texts = Texts;
    }
}

/// <summary>Represents an offer on a catalog page.</summary>
public sealed record CatalogPageOffer : IParserComposer<CatalogPageOffer>
{
    private string _localization_id = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());
    private string _preview_image = "";

    /// <summary>Initializes a new instance of the <see cref="CatalogPageOffer"/> record.</summary>
    /// <param name="offerId">The identifier of the offer.</param>
    /// <param name="localizationId">The localization key of the offer.</param>
    /// <param name="isRent">Whether the offer is a rental.</param>
    /// <param name="priceInCredits">The price in credits.</param>
    /// <param name="priceInActivityPoints">The price in activity points.</param>
    /// <param name="activityPointType">The activity point type of <paramref name="priceInActivityPoints"/>.</param>
    /// <param name="priceInSilver">The price in silver.</param>
    /// <param name="giftable">Whether the offer can be bought as a gift.</param>
    /// <param name="products">The products the offer gives, copied into a read only list.</param>
    /// <param name="clubLevel">The club level the offer requires.</param>
    /// <param name="bundlePurchaseAllowed">Whether the offer can be bought in bulk.</param>
    /// <param name="isPet">Whether the offer is a pet.</param>
    /// <param name="previewImage">The preview image of the offer.</param>
    public CatalogPageOffer(
        int offerId,
        string localizationId,
        bool isRent,
        int priceInCredits,
        int priceInActivityPoints,
        int activityPointType,
        int priceInSilver,
        bool giftable,
        IReadOnlyList<CatalogProduct> products,
        int clubLevel,
        bool bundlePurchaseAllowed,
        bool isPet,
        string previewImage)
    {
        OfferId = offerId;
        LocalizationId = localizationId;
        IsRent = isRent;
        PriceInCredits = priceInCredits;
        PriceInActivityPoints = priceInActivityPoints;
        ActivityPointType = activityPointType;
        PriceInSilver = priceInSilver;
        Giftable = giftable;
        Products = products;
        ClubLevel = clubLevel;
        BundlePurchaseAllowed = bundlePurchaseAllowed;
        IsPet = isPet;
        PreviewImage = previewImage;
    }

    /// <summary>Gets the identifier of the offer.</summary>
    public int OfferId { get; init; }

    /// <summary>Gets the localization key of the offer.</summary>
    public string LocalizationId
    {
        get => _localization_id;
        init => _localization_id = CatalogWire.RequireReference(value, nameof(LocalizationId));
    }

    /// <summary>Gets whether the offer is a rental.</summary>
    public bool IsRent { get; init; }

    /// <summary>Gets the price in credits.</summary>
    public int PriceInCredits { get; init; }

    /// <summary>Gets the price in activity points.</summary>
    public int PriceInActivityPoints { get; init; }

    /// <summary>Gets the activity point type of <see cref="PriceInActivityPoints"/>.</summary>
    public int ActivityPointType { get; init; }

    /// <summary>Gets the price in silver.</summary>
    public int PriceInSilver { get; init; }

    /// <summary>Gets whether the offer can be bought as a gift.</summary>
    public bool Giftable { get; init; }

    /// <summary>Gets the products the offer gives, as a read only copy.</summary>
    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(Products));
    }

    /// <summary>Gets the club level the offer requires.</summary>
    public int ClubLevel { get; init; }

    /// <summary>Gets whether the offer can be bought in bulk.</summary>
    public bool BundlePurchaseAllowed { get; init; }

    /// <summary>Gets whether the offer is a pet.</summary>
    public bool IsPet { get; init; }

    /// <summary>Gets the preview image of the offer.</summary>
    public string PreviewImage
    {
        get => _preview_image;
        init => _preview_image = CatalogWire.RequireReference(value, nameof(PreviewImage));
    }

    /// <summary>Parses a catalog offer from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogPageOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogPageOffer ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneOffer(in p);

    /// <summary>Composes the catalog offer into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogPageOffer value, in PacketWriter p) =>
        CatalogPageWire.ComposeOffer(value, false, in p);

    /// <summary>Deconstructs the offer into its parts.</summary>
    /// <param name="offerId">The identifier of the offer.</param>
    /// <param name="localizationId">The localization key of the offer.</param>
    /// <param name="isRent">Whether the offer is a rental.</param>
    /// <param name="priceInCredits">The price in credits.</param>
    /// <param name="priceInActivityPoints">The price in activity points.</param>
    /// <param name="activityPointType">The activity point type of the activity point price.</param>
    /// <param name="priceInSilver">The price in silver.</param>
    /// <param name="giftable">Whether the offer can be bought as a gift.</param>
    /// <param name="products">The products the offer gives.</param>
    /// <param name="clubLevel">The club level the offer requires.</param>
    /// <param name="bundlePurchaseAllowed">Whether the offer can be bought in bulk.</param>
    /// <param name="isPet">Whether the offer is a pet.</param>
    /// <param name="previewImage">The preview image of the offer.</param>
    public void Deconstruct(
        out int offerId,
        out string localizationId,
        out bool isRent,
        out int priceInCredits,
        out int priceInActivityPoints,
        out int activityPointType,
        out int priceInSilver,
        out bool giftable,
        out IReadOnlyList<CatalogProduct> products,
        out int clubLevel,
        out bool bundlePurchaseAllowed,
        out bool isPet,
        out string previewImage)
    {
        offerId = OfferId;
        localizationId = LocalizationId;
        isRent = IsRent;
        priceInCredits = PriceInCredits;
        priceInActivityPoints = PriceInActivityPoints;
        activityPointType = ActivityPointType;
        priceInSilver = PriceInSilver;
        giftable = Giftable;
        products = Products;
        clubLevel = ClubLevel;
        bundlePurchaseAllowed = BundlePurchaseAllowed;
        isPet = IsPet;
        previewImage = PreviewImage;
    }
}

/// <summary>Represents a promoted item on the catalog front page.</summary>
/// <param name="Position">The position of the item on the front page.</param>
/// <param name="ItemName">The name of the item.</param>
/// <param name="ItemPromoImage">The promotional image of the item.</param>
/// <param name="Type">
/// The link type of the item: 0 for a catalog page location, 1 for a product offer, or 2 for a product code.
/// </param>
/// <param name="CataloguePageLocation">
/// The catalog page location, or empty when <paramref name="Type"/> is not 0.
/// </param>
/// <param name="ProductOfferId">
/// The identifier of the product offer, or 0 when <paramref name="Type"/> is not 1.
/// </param>
/// <param name="ProductCode">The product code, or empty when <paramref name="Type"/> is not 2.</param>
/// <param name="ExpirationSeconds">The number of seconds until the item expires.</param>
public sealed record CatalogFrontPageItem(
    int Position,
    string ItemName,
    string ItemPromoImage,
    int Type,
    string CataloguePageLocation,
    int ProductOfferId,
    string ProductCode,
    int ExpirationSeconds) : IParserComposer<CatalogFrontPageItem>
{
    /// <summary>Parses a front page item from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogFrontPageItem Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogFrontPageItem ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseFrontPageItem(in p);

    /// <summary>Composes the front page item into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogFrontPageItem value, in PacketWriter p) =>
        CatalogPageWire.ComposeFrontPageItem(value, in p);
}

/// <summary>Represents the <c>CatalogPage</c> message, received with the contents of a catalog page.</summary>
public sealed record CatalogPage : IParserComposer<CatalogPage>
{
    private string _catalog_type = "";
    private string _layout_code = "";
    private CatalogPageLocalization _localization = null!;
    private IReadOnlyList<CatalogPageOffer> _offers = Array.AsReadOnly(Array.Empty<CatalogPageOffer>());
    private IReadOnlyList<CatalogFrontPageItem>? _front_page_items;

    /// <summary>Initializes a new instance of the <see cref="CatalogPage"/> record.</summary>
    /// <param name="pageId">The identifier of the page.</param>
    /// <param name="catalogType">The type of the catalog, such as <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</param>
    /// <param name="layoutCode">The layout code of the page.</param>
    /// <param name="localization">The images and texts of the page.</param>
    /// <param name="offers">The offers on the page, copied into a read only list.</param>
    /// <param name="offerId">The identifier of the offer selected on the page.</param>
    /// <param name="acceptSeasonCurrencyAsCredits">
    /// Whether the page accepts seasonal currency in place of credits.
    /// </param>
    /// <param name="frontPageItems">
    /// The front page items, or <see langword="null"/> when the message does not carry them.
    /// </param>
    public CatalogPage(
        int pageId,
        string catalogType,
        string layoutCode,
        CatalogPageLocalization localization,
        IReadOnlyList<CatalogPageOffer> offers,
        int offerId,
        bool acceptSeasonCurrencyAsCredits,
        IReadOnlyList<CatalogFrontPageItem>? frontPageItems)
    {
        PageId = pageId;
        CatalogType = catalogType;
        LayoutCode = layoutCode;
        Localization = localization;
        Offers = offers;
        OfferId = offerId;
        AcceptSeasonCurrencyAsCredits = acceptSeasonCurrencyAsCredits;
        FrontPageItems = frontPageItems;
    }

    /// <summary>Gets the identifier of the page.</summary>
    public int PageId { get; init; }

    /// <summary>Gets the type of the catalog, such as <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</summary>
    public string CatalogType
    {
        get => _catalog_type;
        init => _catalog_type = CatalogWire.RequireReference(value, nameof(CatalogType));
    }

    /// <summary>Gets the layout code of the page.</summary>
    public string LayoutCode
    {
        get => _layout_code;
        init => _layout_code = CatalogWire.RequireReference(value, nameof(LayoutCode));
    }

    /// <summary>Gets the images and texts of the page.</summary>
    public CatalogPageLocalization Localization
    {
        get => _localization;
        init => _localization = CatalogWire.RequireReference(value, nameof(Localization));
    }

    /// <summary>Gets the offers on the page, as a read only copy.</summary>
    public IReadOnlyList<CatalogPageOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumOffers,
            nameof(Offers));
    }

    /// <summary>Gets the identifier of the offer selected on the page.</summary>
    public int OfferId { get; init; }

    /// <summary>Gets whether the page accepts seasonal currency in place of credits.</summary>
    public bool AcceptSeasonCurrencyAsCredits { get; init; }

    /// <summary>
    /// Gets the front page items, or <see langword="null"/> when the message does not carry them.
    /// </summary>
    public IReadOnlyList<CatalogFrontPageItem>? FrontPageItems
    {
        get => _front_page_items;
        init => _front_page_items = CatalogWire.FreezeOptionalReferences(
            value,
            CatalogPageWire.MaximumFrontPageItems,
            nameof(FrontPageItems));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogPage Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogPage ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParsePage(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogPage value, in PacketWriter p) =>
        CatalogPageWire.ComposePage(value, in p);

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="pageId">The identifier of the page.</param>
    /// <param name="catalogType">The type of the catalog.</param>
    /// <param name="layoutCode">The layout code of the page.</param>
    /// <param name="localization">The images and texts of the page.</param>
    /// <param name="offers">The offers on the page.</param>
    /// <param name="offerId">The identifier of the offer selected on the page.</param>
    /// <param name="acceptSeasonCurrencyAsCredits">
    /// Whether the page accepts seasonal currency in place of credits.
    /// </param>
    /// <param name="frontPageItems">The front page items, or <see langword="null"/>.</param>
    public void Deconstruct(
        out int pageId,
        out string catalogType,
        out string layoutCode,
        out CatalogPageLocalization localization,
        out IReadOnlyList<CatalogPageOffer> offers,
        out int offerId,
        out bool acceptSeasonCurrencyAsCredits,
        out IReadOnlyList<CatalogFrontPageItem>? frontPageItems)
    {
        pageId = PageId;
        catalogType = CatalogType;
        layoutCode = LayoutCode;
        localization = Localization;
        offers = Offers;
        offerId = OfferId;
        acceptSeasonCurrencyAsCredits = AcceptSeasonCurrencyAsCredits;
        frontPageItems = FrontPageItems;
    }
}

internal static class CatalogPageWire
{
    internal const int MaximumOffers = 4_096;
    internal const int MaximumLocalizationEntries = 16_384;
    internal const int MaximumProducts = 65_535;
    internal const int MaximumProductReferences = 65_535;
    internal const int MaximumFrontPageItems = 4_096;
    internal const int MaximumStrings = 196_608;
    internal const int MaximumStringBytes = 16 * 1024 * 1024;

    private const int FrontPageItemMinimumBytes =
        sizeof(int) + CatalogWire.StringMinimumBytes * 2 + sizeof(int) +
        CatalogWire.StringMinimumBytes + sizeof(int);

    public static CatalogPageLocalization ParseStandaloneLocalization(in PacketReader p)
    {
        var budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        return ParseLocalization(in p, 0, ref budget, ref strings);
    }

    public static CatalogPageOffer ParseStandaloneOffer(in PacketReader p)
    {
        var budget = new CatalogPageBudget();
        budget.TakeOffers(1);
        var strings = NewStringBudget();
        return ParseOffer(in p, 0, ref budget, ref strings);
    }

    public static CatalogFrontPageItem ParseFrontPageItem(in PacketReader p)
    {
        var budget = new CatalogPageBudget();
        budget.TakeFrontPageItems(1);
        var strings = NewStringBudget();
        return ParseFrontPageItem(in p, ref strings);
    }

    public static CatalogPage ParsePage(in PacketReader p)
    {
        var budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        int page_id = p.ReadInt();
        string catalog_type = strings.Read(in p, nameof(CatalogPage.CatalogType));
        string layout_code = strings.Read(in p, nameof(CatalogPage.LayoutCode));
        int count_width = CatalogWire.CountWidth;
        int trailing_after_localization = count_width + sizeof(int) + sizeof(byte) +
            (0);
        CatalogPageLocalization localization = ParseLocalization(
            in p,
            trailing_after_localization,
            ref budget,
            ref strings);

        int offer_tail = sizeof(int) + sizeof(byte) + (0);
        int offer_count = CatalogWire.ReadCount(
            in p,
            MinimumOfferBytes(),
            offer_tail,
            MaximumOffers,
            nameof(CatalogPage.Offers));
        budget.TakeOffers(offer_count);
        var offers = new CatalogPageOffer[offer_count];
        int minimum_offer_bytes = MinimumOfferBytes();
        for (int index = 0; index < offers.Length; index++)
        {
            int sibling_bytes = checked((offers.Length - index - 1) * minimum_offer_bytes);
            offers[index] = ParseOffer(
                in p,
                checked(offer_tail + sibling_bytes),
                ref budget,
                ref strings);
        }

        int offer_id = p.ReadInt();
        bool accept_season_currency_as_credits = p.ReadBool();
        CatalogFrontPageItem[]? front_page_items = null;
        if (p.Available > 0)
        {
            int item_count = CatalogWire.ReadCount(
                in p,
                FrontPageItemMinimumBytes,
                0,
                MaximumFrontPageItems,
                nameof(CatalogPage.FrontPageItems));
            budget.TakeFrontPageItems(item_count);
            front_page_items = new CatalogFrontPageItem[item_count];
            for (int index = 0; index < front_page_items.Length; index++)
                front_page_items[index] = ParseFrontPageItem(in p, ref strings);
        }

        CatalogWire.RequireEmpty(in p, nameof(CatalogPage));
        return new CatalogPage(
            page_id,
            catalog_type,
            layout_code,
            localization,
            offers,
            offer_id,
            accept_season_currency_as_credits,
            front_page_items);
    }

    public static void ComposeLocalization(CatalogPageLocalization value, in PacketWriter p)
    {
        var budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        CatalogPageLocalization prepared = PrepareLocalization(value, ref budget, ref strings, in p);
        WriteLocalization(prepared, in p);
    }

    public static void ComposeOffer(
        CatalogPageOffer value,
        bool strict_page_fields,
        in PacketWriter p)
    {
        var budget = new CatalogPageBudget();
        budget.TakeOffers(1);
        var strings = NewStringBudget();
        CatalogPageOffer prepared = PrepareOffer(
            value,
            strict_page_fields,
            ref budget,
            ref strings,
            in p);
        WriteOffer(prepared, in p);
    }

    public static void ComposeFrontPageItem(CatalogFrontPageItem value, in PacketWriter p)
    {
        var strings = NewStringBudget();
        CatalogFrontPageItem prepared = PrepareFrontPageItem(value, ref strings, in p);
        WriteFrontPageItem(prepared, in p);
    }

    public static void ComposePage(CatalogPage value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        strings.Require(value.CatalogType, nameof(CatalogPage.CatalogType), in p);
        strings.Require(value.LayoutCode, nameof(CatalogPage.LayoutCode), in p);
        CatalogPageLocalization localization = PrepareLocalization(
            value.Localization,
            ref budget,
            ref strings,
            in p);

        int offer_count = CatalogWire.RequireListCount(
            value.Offers,
            MaximumOffers,
            nameof(CatalogPage.Offers));
        budget.TakeOffers(offer_count);
        CatalogPageOffer[] source_offers = CatalogWire.SnapshotReferences(
            value.Offers,
            MaximumOffers,
            nameof(CatalogPage.Offers));
        var offers = new CatalogPageOffer[source_offers.Length];
        for (int index = 0; index < offers.Length; index++)
        {
            offers[index] = PrepareOffer(
                source_offers[index],
                true,
                ref budget,
                ref strings,
                in p);
        }

        CatalogFrontPageItem[]? front_page_items = null;
        if (value.FrontPageItems is not null)
        {
            int item_count = CatalogWire.RequireListCount(
                value.FrontPageItems,
                MaximumFrontPageItems,
                nameof(CatalogPage.FrontPageItems));
            budget.TakeFrontPageItems(item_count);
            CatalogFrontPageItem[] source_items = CatalogWire.SnapshotReferences(
                value.FrontPageItems,
                MaximumFrontPageItems,
                nameof(CatalogPage.FrontPageItems));
            front_page_items = new CatalogFrontPageItem[source_items.Length];
            for (int index = 0; index < front_page_items.Length; index++)
                front_page_items[index] = PrepareFrontPageItem(source_items[index], ref strings, in p);
        }

        p.WriteInt(value.PageId);
        p.WriteString(value.CatalogType);
        p.WriteString(value.LayoutCode);
        WriteLocalization(localization, in p);
        CatalogWire.WriteCount(offers.Length, in p);
        foreach (CatalogPageOffer offer in offers)
            WriteOffer(offer, in p);
        p.WriteInt(value.OfferId);
        p.WriteBool(value.AcceptSeasonCurrencyAsCredits);
        if (front_page_items is not null)
        {
            CatalogWire.WriteCount(front_page_items.Length, in p);
            foreach (CatalogFrontPageItem item in front_page_items)
                WriteFrontPageItem(item, in p);
        }
    }

    private static CatalogPageLocalization ParseLocalization(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings)
    {
        int count_width = CatalogWire.CountWidth;
        int image_count = CatalogWire.ReadCount(
            in p,
            CatalogWire.StringMinimumBytes,
            checked(trailing_bytes + count_width),
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Images));
        budget.TakeLocalizationEntries(image_count);
        var images = new string[image_count];
        for (int index = 0; index < images.Length; index++)
            images[index] = strings.Read(in p, nameof(CatalogPageLocalization.Images));

        int text_count = CatalogWire.ReadCount(
            in p,
            CatalogWire.StringMinimumBytes,
            trailing_bytes,
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Texts));
        budget.TakeLocalizationEntries(text_count);
        var texts = new string[text_count];
        for (int index = 0; index < texts.Length; index++)
            texts[index] = strings.Read(in p, nameof(CatalogPageLocalization.Texts));
        return new CatalogPageLocalization(images, texts);
    }

    internal static CatalogPageOffer ParseOffer(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings)
    {
        int offer_id = p.ReadInt();
        int count_width = CatalogWire.CountWidth;
        int fields_after_localization = sizeof(byte) + sizeof(int) * 4 + sizeof(byte) +
            (count_width + FlashOfferTailBytes);
        string localization_id = strings.Read(
            in p,
            nameof(CatalogPageOffer.LocalizationId),
            checked(trailing_bytes + fields_after_localization));
        bool is_rent = p.ReadBool();
        int price_in_credits = p.ReadInt();
        int price_in_activity_points = p.ReadInt();
        int activity_point_type = p.ReadInt();
        int price_in_silver = p.ReadInt();
        bool giftable = p.ReadBool();

        CatalogProduct[] products;
        int club_level;
        {
            int product_count = CatalogWire.ReadCount(
                in p,
                FlashProductMinimumBytes,
                checked(trailing_bytes + FlashOfferTailBytes),
                MaximumProducts,
                nameof(CatalogPageOffer.Products));
            budget.TakeProducts(product_count);
            products = new CatalogProduct[product_count];
            for (int index = 0; index < products.Length; index++)
            {
                int sibling_bytes = checked(
                    (products.Length - index - 1) * FlashProductMinimumBytes);
                products[index] = ParseFlashProduct(
                    in p,
                    checked(trailing_bytes + FlashOfferTailBytes + sibling_bytes),
                    ref strings);
            }
            club_level = p.ReadInt();
        }

        bool bundle_purchase_allowed = p.ReadBool();
        bool is_pet = p.ReadBool();
        string preview_image = strings.Read(
            in p,
            nameof(CatalogPageOffer.PreviewImage),
            trailing_bytes);
        return new CatalogPageOffer(
            offer_id,
            localization_id,
            is_rent,
            price_in_credits,
            price_in_activity_points,
            activity_point_type,
            price_in_silver,
            giftable,
            products,
            club_level,
            bundle_purchase_allowed,
            is_pet,
            preview_image);
    }

    private static CatalogFrontPageItem ParseFrontPageItem(
        in PacketReader p,
        ref CatalogStringBudget strings)
    {
        int position = p.ReadInt();
        string item_name = strings.Read(in p, nameof(CatalogFrontPageItem.ItemName));
        string item_promo_image = strings.Read(in p, nameof(CatalogFrontPageItem.ItemPromoImage));
        int type = p.ReadInt();
        string page_location = "";
        int product_offer_id = 0;
        string product_code = "";
        switch (type)
        {
            case 0:
                page_location = strings.Read(in p, nameof(CatalogFrontPageItem.CataloguePageLocation));
                break;
            case 1:
                product_offer_id = p.ReadInt();
                break;
            case 2:
                product_code = strings.Read(in p, nameof(CatalogFrontPageItem.ProductCode));
                break;
            default:
                throw new InvalidDataException($"Unsupported catalog front-page item type {type}.");
        }
        int expiration_seconds = p.ReadInt();
        return new CatalogFrontPageItem(
            position,
            item_name,
            item_promo_image,
            type,
            page_location,
            product_offer_id,
            product_code,
            expiration_seconds);
    }

    internal static CatalogProduct ParseFlashProduct(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogStringBudget strings)
    {
        string product_type = strings.Read(
            in p,
            nameof(CatalogProduct.ProductType),
            checked(trailing_bytes + CatalogWire.StringMinimumBytes));
        if (product_type == CatalogProduct.TypeBadge)
        {
            return new CatalogProduct(
                product_type,
                0,
                strings.Read(in p, nameof(CatalogProduct.ExtraParam), trailing_bytes),
                1,
                false,
                0,
                0);
        }

        RequireAvailable(
            in p,
            checked(trailing_bytes + sizeof(int) * 2 + CatalogWire.StringMinimumBytes + sizeof(byte)),
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
            RequireAvailable(
                in p,
                checked(trailing_bytes + sizeof(int) * 2),
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

    private static CatalogPageLocalization PrepareLocalization(
        CatalogPageLocalization value,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int image_count = CatalogWire.RequireListCount(
            value.Images,
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Images));
        int text_count = CatalogWire.RequireListCount(
            value.Texts,
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Texts));
        budget.TakeLocalizationEntries(checked(image_count + text_count));
        string[] images = CatalogWire.SnapshotReferences(
            value.Images,
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Images));
        string[] texts = CatalogWire.SnapshotReferences(
            value.Texts,
            MaximumLocalizationEntries,
            nameof(CatalogPageLocalization.Texts));
        foreach (string image in images)
            strings.Require(image, nameof(CatalogPageLocalization.Images), in p);
        foreach (string text in texts)
            strings.Require(text, nameof(CatalogPageLocalization.Texts), in p);
        return new CatalogPageLocalization(images, texts);
    }

    internal static CatalogPageOffer PrepareOffer(
        CatalogPageOffer value,
        bool strict_page_fields,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.LocalizationId, nameof(CatalogPageOffer.LocalizationId), in p);
        strings.Require(value.PreviewImage, nameof(CatalogPageOffer.PreviewImage), in p);

        CatalogProduct[] products;
        {
            int product_count = CatalogWire.RequireListCount(
                value.Products,
                MaximumProducts,
                nameof(CatalogPageOffer.Products));
            budget.TakeProducts(product_count);
            products = CatalogWire.SnapshotReferences(
                value.Products,
                MaximumProducts,
                nameof(CatalogPageOffer.Products));
            for (int index = 0; index < products.Length; index++)
                PrepareFlashProduct(products[index], strict_page_fields, ref strings, in p);
        }

        return new CatalogPageOffer(
            value.OfferId,
            value.LocalizationId,
            value.IsRent,
            value.PriceInCredits,
            value.PriceInActivityPoints,
            value.ActivityPointType,
            value.PriceInSilver,
            value.Giftable,
            products,
            value.ClubLevel,
            value.BundlePurchaseAllowed,
            value.IsPet,
            value.PreviewImage);
    }

    private static CatalogFrontPageItem PrepareFrontPageItem(
        CatalogFrontPageItem value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ItemName, nameof(CatalogFrontPageItem.ItemName), in p);
        strings.Require(value.ItemPromoImage, nameof(CatalogFrontPageItem.ItemPromoImage), in p);
        CatalogWire.RequireString(
            value.CataloguePageLocation,
            nameof(CatalogFrontPageItem.CataloguePageLocation),
            in p);
        CatalogWire.RequireString(value.ProductCode, nameof(CatalogFrontPageItem.ProductCode), in p);
        switch (value.Type)
        {
            case 0:
                if (value.ProductOfferId != 0 || value.ProductCode.Length != 0)
                    throw new InvalidDataException("Catalog page-location items contain conflicting union fields.");
                strings.Require(
                    value.CataloguePageLocation,
                    nameof(CatalogFrontPageItem.CataloguePageLocation),
                    in p);
                break;
            case 1:
                if (value.CataloguePageLocation.Length != 0 || value.ProductCode.Length != 0)
                    throw new InvalidDataException("Catalog offer items contain conflicting union fields.");
                break;
            case 2:
                if (value.CataloguePageLocation.Length != 0 || value.ProductOfferId != 0)
                    throw new InvalidDataException("Catalog product-code items contain conflicting union fields.");
                strings.Require(value.ProductCode, nameof(CatalogFrontPageItem.ProductCode), in p);
                break;
            default:
                throw new InvalidDataException($"Unsupported catalog front-page item type {value.Type}.");
        }
        return value;
    }

    internal static void PrepareFlashProduct(
        CatalogProduct value,
        bool strict_page_fields,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ProductType, nameof(CatalogProduct.ProductType), in p);
        strings.Require(value.ExtraParam, nameof(CatalogProduct.ExtraParam), in p);
        RequireLimitedFields(
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft,
            nameof(CatalogProduct));
        if (value.ProductType == CatalogProduct.TypeBadge &&
            (value.FurniClassId != 0 || value.ProductCount != 1 || value.UniqueLimitedItem ||
             value.UniqueLimitedItemSeriesSize != 0 || value.UniqueLimitedItemsLeft != 0))
        {
            throw new InvalidDataException("Flash badge products contain fields absent from the wire layout.");
        }
    }

    private static void WriteLocalization(CatalogPageLocalization value, in PacketWriter p)
    {
        CatalogWire.WriteCount(value.Images.Count, in p);
        foreach (string image in value.Images)
            p.WriteString(image);
        CatalogWire.WriteCount(value.Texts.Count, in p);
        foreach (string text in value.Texts)
            p.WriteString(text);
    }

    internal static void WriteOffer(CatalogPageOffer value, in PacketWriter p)
    {
        p.WriteInt(value.OfferId);
        p.WriteString(value.LocalizationId);
        p.WriteBool(value.IsRent);
        p.WriteInt(value.PriceInCredits);
        p.WriteInt(value.PriceInActivityPoints);
        p.WriteInt(value.ActivityPointType);
        p.WriteInt(value.PriceInSilver);
        p.WriteBool(value.Giftable);
        {
            CatalogWire.WriteCount(value.Products.Count, in p);
            foreach (CatalogProduct product in value.Products)
                WriteFlashProduct(product, in p);
            p.WriteInt(value.ClubLevel);
        }
        p.WriteBool(value.BundlePurchaseAllowed);
        p.WriteBool(value.IsPet);
        p.WriteString(value.PreviewImage);
    }

    private static void WriteFrontPageItem(CatalogFrontPageItem value, in PacketWriter p)
    {
        p.WriteInt(value.Position);
        p.WriteString(value.ItemName);
        p.WriteString(value.ItemPromoImage);
        p.WriteInt(value.Type);
        switch (value.Type)
        {
            case 0:
                p.WriteString(value.CataloguePageLocation);
                break;
            case 1:
                p.WriteInt(value.ProductOfferId);
                break;
            case 2:
                p.WriteString(value.ProductCode);
                break;
        }
        p.WriteInt(value.ExpirationSeconds);
    }

    internal static void WriteFlashProduct(CatalogProduct value, in PacketWriter p)
    {
        p.WriteString(value.ProductType);
        if (value.ProductType == CatalogProduct.TypeBadge)
        {
            p.WriteString(value.ExtraParam);
            return;
        }
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

    private static void RequireLimitedFields(
        bool limited,
        int series_size,
        int items_left,
        string name)
    {
        if (!limited && (series_size != 0 || items_left != 0))
            throw new InvalidDataException($"{name} contains inactive limited-item fields.");
    }

    private static void RequireAvailable(in PacketReader p, int required, string name)
    {
        if (p.Available < required)
            throw new InvalidDataException($"{name} exceeds the remaining payload capacity.");
    }

    internal static CatalogStringBudget NewStringBudget() =>
        new(MaximumStrings, MaximumStringBytes);

    internal static int MinimumOfferBytes() => sizeof(int) + CatalogWire.StringMinimumBytes + sizeof(byte) + sizeof(int) * 4 +
          sizeof(byte) + sizeof(int) + sizeof(int) + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes;

    internal const int FlashProductMinimumBytes = CatalogWire.StringMinimumBytes * 2;
    internal const int ProductReferenceMinimumBytes = sizeof(short) + CatalogWire.StringMinimumBytes;
    private const int FlashOfferTailBytes =
        sizeof(int) + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes;
}

internal struct CatalogPageBudget
{
    private int _offers;
    private int _localization_entries;
    private int _products;
    private int _product_references;
    private int _front_page_items;

    public void TakeOffers(int count) =>
        Take(ref _offers, count, CatalogPageWire.MaximumOffers, "Catalog offers");

    public void TakeLocalizationEntries(int count) =>
        Take(
            ref _localization_entries,
            count,
            CatalogPageWire.MaximumLocalizationEntries,
            "Catalog localization entries");

    public void TakeProducts(int count) =>
        Take(ref _products, count, CatalogPageWire.MaximumProducts, "Catalog products");

    public void TakeProductReferences(int count) =>
        Take(
            ref _product_references,
            count,
            CatalogPageWire.MaximumProductReferences,
            "Catalog product references");

    public void TakeFrontPageItems(int count) =>
        Take(
            ref _front_page_items,
            count,
            CatalogPageWire.MaximumFrontPageItems,
            "Catalog front-page items");

    private static void Take(ref int current, int count, int maximum, string name)
    {
        CatalogWire.RequireCount(count, maximum, name);
        if (count > maximum - current)
            throw new InvalidDataException($"{name} exceed the global limit {maximum}.");
        current += count;
    }
}
