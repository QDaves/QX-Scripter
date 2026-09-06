using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CatalogPageLocalization : IParserComposer<CatalogPageLocalization>
{
    private IReadOnlyList<string> _images = Array.AsReadOnly(Array.Empty<string>());
    private IReadOnlyList<string> _texts = Array.AsReadOnly(Array.Empty<string>());

    public CatalogPageLocalization(IReadOnlyList<string> Images, IReadOnlyList<string> Texts)
    {
        this.Images = Images;
        this.Texts = Texts;
    }

    public IReadOnlyList<string> Images
    {
        get => _images;
        init => _images = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumLocalizationEntries,
            nameof(Images));
    }

    public IReadOnlyList<string> Texts
    {
        get => _texts;
        init => _texts = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumLocalizationEntries,
            nameof(Texts));
    }

    public static CatalogPageLocalization Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPageLocalization ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneLocalization(in p);

    private static CatalogPageLocalization ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneLocalization(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPageLocalization value, in PacketWriter p) =>
        CatalogPageWire.ComposeLocalization(value, in p);

    private static void ComposeUnity(CatalogPageLocalization value, in PacketWriter p) =>
        CatalogPageWire.ComposeLocalization(value, in p);

    public void Deconstruct(out IReadOnlyList<string> Images, out IReadOnlyList<string> Texts)
    {
        Images = this.Images;
        Texts = this.Texts;
    }
}

public sealed record CatalogPageProductReference(short ProductType, string Identifier)
    : IParserComposer<CatalogPageProductReference>
{
    public static CatalogPageProductReference Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPageProductReference ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseProductReference(in p);

    private static CatalogPageProductReference ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParseProductReference(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPageProductReference value, in PacketWriter p) =>
        CatalogPageWire.ComposeProductReference(value, in p);

    private static void ComposeUnity(CatalogPageProductReference value, in PacketWriter p) =>
        CatalogPageWire.ComposeProductReference(value, in p);
}

public sealed record CatalogPageProduct(
    short ProductType,
    int FurniClassId,
    string ExtraParam,
    int ProductCount,
    bool UniqueLimitedItem,
    int UniqueLimitedItemSeriesSize,
    int UniqueLimitedItemsLeft) : IParserComposer<CatalogPageProduct>
{
    public static CatalogPageProduct Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPageProduct ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParsePageProduct(in p);

    private static CatalogPageProduct ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParsePageProduct(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPageProduct value, in PacketWriter p) =>
        CatalogPageWire.ComposePageProduct(value, in p);

    private static void ComposeUnity(CatalogPageProduct value, in PacketWriter p) =>
        CatalogPageWire.ComposePageProduct(value, in p);
}

public sealed record CatalogPageOffer : IParserComposer<CatalogPageOffer>
{
    private string _localization_id = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());
    private string _preview_image = "";
    private IReadOnlyList<CatalogPageProductReference>? _unity_product_references;
    private IReadOnlyList<CatalogPageProduct>? _unity_products;

    public CatalogPageOffer(
        int OfferId,
        string LocalizationId,
        bool IsRent,
        int PriceInCredits,
        int PriceInActivityPoints,
        int ActivityPointType,
        int PriceInSilver,
        bool Giftable,
        IReadOnlyList<CatalogProduct> Products,
        int ClubLevel,
        bool BundlePurchaseAllowed,
        bool IsPet,
        string PreviewImage,
        IReadOnlyList<CatalogPageProductReference>? UnityProductReferences = null,
        IReadOnlyList<CatalogPageProduct>? UnityProducts = null)
    {
        this.OfferId = OfferId;
        this.LocalizationId = LocalizationId;
        this.IsRent = IsRent;
        this.PriceInCredits = PriceInCredits;
        this.PriceInActivityPoints = PriceInActivityPoints;
        this.ActivityPointType = ActivityPointType;
        this.PriceInSilver = PriceInSilver;
        this.Giftable = Giftable;
        this.Products = Products;
        this.ClubLevel = ClubLevel;
        this.BundlePurchaseAllowed = BundlePurchaseAllowed;
        this.IsPet = IsPet;
        this.PreviewImage = PreviewImage;
        this.UnityProductReferences = UnityProductReferences;
        this.UnityProducts = UnityProducts;
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

    public int PriceInSilver { get; init; }

    public bool Giftable { get; init; }

    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(Products));
    }

    public int ClubLevel { get; init; }

    public bool BundlePurchaseAllowed { get; init; }

    public bool IsPet { get; init; }

    public string PreviewImage
    {
        get => _preview_image;
        init => _preview_image = CatalogWire.RequireReference(value, nameof(PreviewImage));
    }

    public IReadOnlyList<CatalogPageProductReference>? UnityProductReferences
    {
        get => _unity_product_references;
        init => _unity_product_references = CatalogWire.FreezeOptionalReferences(
            value,
            CatalogPageWire.MaximumProductReferences,
            nameof(UnityProductReferences));
    }

    public IReadOnlyList<CatalogPageProduct>? UnityProducts
    {
        get => _unity_products;
        init => _unity_products = CatalogWire.FreezeOptionalReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(UnityProducts));
    }

    public static CatalogPageOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPageOffer ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneOffer(in p, true);

    private static CatalogPageOffer ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParseStandaloneOffer(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPageOffer value, in PacketWriter p) =>
        CatalogPageWire.ComposeOffer(value, true, false, in p);

    private static void ComposeUnity(CatalogPageOffer value, in PacketWriter p) =>
        CatalogPageWire.ComposeOffer(value, false, false, in p);

    public void Deconstruct(
        out int OfferId,
        out string LocalizationId,
        out bool IsRent,
        out int PriceInCredits,
        out int PriceInActivityPoints,
        out int ActivityPointType,
        out int PriceInSilver,
        out bool Giftable,
        out IReadOnlyList<CatalogProduct> Products,
        out int ClubLevel,
        out bool BundlePurchaseAllowed,
        out bool IsPet,
        out string PreviewImage,
        out IReadOnlyList<CatalogPageProductReference>? UnityProductReferences,
        out IReadOnlyList<CatalogPageProduct>? UnityProducts)
    {
        OfferId = this.OfferId;
        LocalizationId = this.LocalizationId;
        IsRent = this.IsRent;
        PriceInCredits = this.PriceInCredits;
        PriceInActivityPoints = this.PriceInActivityPoints;
        ActivityPointType = this.ActivityPointType;
        PriceInSilver = this.PriceInSilver;
        Giftable = this.Giftable;
        Products = this.Products;
        ClubLevel = this.ClubLevel;
        BundlePurchaseAllowed = this.BundlePurchaseAllowed;
        IsPet = this.IsPet;
        PreviewImage = this.PreviewImage;
        UnityProductReferences = this.UnityProductReferences;
        UnityProducts = this.UnityProducts;
    }
}

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
    public static CatalogFrontPageItem Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogFrontPageItem ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParseFrontPageItem(in p);

    private static CatalogFrontPageItem ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParseFrontPageItem(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogFrontPageItem value, in PacketWriter p) =>
        CatalogPageWire.ComposeFrontPageItem(value, in p);

    private static void ComposeUnity(CatalogFrontPageItem value, in PacketWriter p) =>
        CatalogPageWire.ComposeFrontPageItem(value, in p);
}

public sealed record CatalogPage : IParserComposer<CatalogPage>
{
    private string _catalog_type = "";
    private string _layout_code = "";
    private CatalogPageLocalization _localization = null!;
    private IReadOnlyList<CatalogPageOffer> _offers = Array.AsReadOnly(Array.Empty<CatalogPageOffer>());
    private IReadOnlyList<CatalogFrontPageItem>? _front_page_items;

    public CatalogPage(
        int PageId,
        string CatalogType,
        string LayoutCode,
        CatalogPageLocalization Localization,
        IReadOnlyList<CatalogPageOffer> Offers,
        int OfferId,
        bool AcceptSeasonCurrencyAsCredits,
        IReadOnlyList<CatalogFrontPageItem>? FrontPageItems)
    {
        this.PageId = PageId;
        this.CatalogType = CatalogType;
        this.LayoutCode = LayoutCode;
        this.Localization = Localization;
        this.Offers = Offers;
        this.OfferId = OfferId;
        this.AcceptSeasonCurrencyAsCredits = AcceptSeasonCurrencyAsCredits;
        this.FrontPageItems = FrontPageItems;
    }

    public int PageId { get; init; }

    public string CatalogType
    {
        get => _catalog_type;
        init => _catalog_type = CatalogWire.RequireReference(value, nameof(CatalogType));
    }

    public string LayoutCode
    {
        get => _layout_code;
        init => _layout_code = CatalogWire.RequireReference(value, nameof(LayoutCode));
    }

    public CatalogPageLocalization Localization
    {
        get => _localization;
        init => _localization = CatalogWire.RequireReference(value, nameof(Localization));
    }

    public IReadOnlyList<CatalogPageOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumOffers,
            nameof(Offers));
    }

    public int OfferId { get; init; }

    public bool AcceptSeasonCurrencyAsCredits { get; init; }

    public IReadOnlyList<CatalogFrontPageItem>? FrontPageItems
    {
        get => _front_page_items;
        init => _front_page_items = CatalogWire.FreezeOptionalReferences(
            value,
            CatalogPageWire.MaximumFrontPageItems,
            nameof(FrontPageItems));
    }

    public static CatalogPage Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogPage ParseFlash(in PacketReader p) =>
        CatalogPageWire.ParsePage(in p, true);

    private static CatalogPage ParseUnity(in PacketReader p) =>
        CatalogPageWire.ParsePage(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogPage value, in PacketWriter p) =>
        CatalogPageWire.ComposePage(value, true, in p);

    private static void ComposeUnity(CatalogPage value, in PacketWriter p) =>
        CatalogPageWire.ComposePage(value, false, in p);

    public void Deconstruct(
        out int PageId,
        out string CatalogType,
        out string LayoutCode,
        out CatalogPageLocalization Localization,
        out IReadOnlyList<CatalogPageOffer> Offers,
        out int OfferId,
        out bool AcceptSeasonCurrencyAsCredits,
        out IReadOnlyList<CatalogFrontPageItem>? FrontPageItems)
    {
        PageId = this.PageId;
        CatalogType = this.CatalogType;
        LayoutCode = this.LayoutCode;
        Localization = this.Localization;
        Offers = this.Offers;
        OfferId = this.OfferId;
        AcceptSeasonCurrencyAsCredits = this.AcceptSeasonCurrencyAsCredits;
        FrontPageItems = this.FrontPageItems;
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

    public static CatalogPageProductReference ParseProductReference(in PacketReader p)
    {
        var strings = NewStringBudget();
        return new CatalogPageProductReference(
            p.ReadShort(),
            strings.Read(in p, nameof(CatalogPageProductReference.Identifier)));
    }

    public static CatalogPageProduct ParsePageProduct(in PacketReader p)
    {
        var strings = NewStringBudget();
        return ParseNativeProduct(in p, 0, ref strings);
    }

    public static CatalogPageOffer ParseStandaloneOffer(in PacketReader p, bool flash)
    {
        var budget = new CatalogPageBudget();
        budget.TakeOffers(1);
        var strings = NewStringBudget();
        return ParseOffer(in p, flash, 0, ref budget, ref strings);
    }

    public static CatalogFrontPageItem ParseFrontPageItem(in PacketReader p)
    {
        var budget = new CatalogPageBudget();
        budget.TakeFrontPageItems(1);
        var strings = NewStringBudget();
        return ParseFrontPageItem(in p, ref strings);
    }

    public static CatalogPage ParsePage(in PacketReader p, bool flash)
    {
        var budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        int page_id = p.ReadInt();
        string catalog_type = strings.Read(in p, nameof(CatalogPage.CatalogType));
        string layout_code = strings.Read(in p, nameof(CatalogPage.LayoutCode));
        int count_width = CatalogWire.CountWidth(p.Client);
        int trailing_after_localization = count_width + sizeof(int) + sizeof(byte) +
            (flash ? 0 : count_width);
        CatalogPageLocalization localization = ParseLocalization(
            in p,
            trailing_after_localization,
            ref budget,
            ref strings);

        int offer_tail = sizeof(int) + sizeof(byte) + (flash ? 0 : count_width);
        int offer_count = CatalogWire.ReadCount(
            in p,
            MinimumOfferBytes(flash),
            offer_tail,
            MaximumOffers,
            nameof(CatalogPage.Offers));
        budget.TakeOffers(offer_count);
        var offers = new CatalogPageOffer[offer_count];
        int minimum_offer_bytes = MinimumOfferBytes(flash);
        for (int index = 0; index < offers.Length; index++)
        {
            int sibling_bytes = checked((offers.Length - index - 1) * minimum_offer_bytes);
            offers[index] = ParseOffer(
                in p,
                flash,
                checked(offer_tail + sibling_bytes),
                ref budget,
                ref strings);
        }

        int offer_id = p.ReadInt();
        bool accept_season_currency_as_credits = p.ReadBool();
        CatalogFrontPageItem[]? front_page_items = null;
        if (!flash || p.Available > 0)
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

    public static void ComposeProductReference(CatalogPageProductReference value, in PacketWriter p)
    {
        var strings = NewStringBudget();
        CatalogPageProductReference prepared = PrepareProductReference(value, ref strings, in p);
        WriteProductReference(prepared, in p);
    }

    public static void ComposePageProduct(CatalogPageProduct value, in PacketWriter p)
    {
        var strings = NewStringBudget();
        CatalogPageProduct prepared = PrepareNativeProduct(value, ref strings, in p);
        WriteNativeProduct(prepared, in p);
    }

    public static void ComposeOffer(
        CatalogPageOffer value,
        bool flash,
        bool strict_page_fields,
        in PacketWriter p)
    {
        var budget = new CatalogPageBudget();
        budget.TakeOffers(1);
        var strings = NewStringBudget();
        CatalogPageOffer prepared = PrepareOffer(
            value,
            flash,
            strict_page_fields,
            ref budget,
            ref strings,
            in p);
        WriteOffer(prepared, flash, in p);
    }

    public static void ComposeFrontPageItem(CatalogFrontPageItem value, in PacketWriter p)
    {
        var strings = NewStringBudget();
        CatalogFrontPageItem prepared = PrepareFrontPageItem(value, ref strings, in p);
        WriteFrontPageItem(prepared, in p);
    }

    public static void ComposePage(CatalogPage value, bool flash, in PacketWriter p)
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
                flash,
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
        else if (!flash)
        {
            throw new InvalidDataException("Unity catalog pages require a front-page item collection.");
        }

        p.WriteInt(value.PageId);
        p.WriteString(value.CatalogType);
        p.WriteString(value.LayoutCode);
        WriteLocalization(localization, in p);
        CatalogWire.WriteCount(offers.Length, in p);
        foreach (CatalogPageOffer offer in offers)
            WriteOffer(offer, flash, in p);
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
        int count_width = CatalogWire.CountWidth(p.Client);
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
        bool flash,
        int trailing_bytes,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings)
    {
        int offer_id = p.ReadInt();
        int count_width = CatalogWire.CountWidth(p.Client);
        int fields_after_localization = sizeof(byte) + sizeof(int) * 4 + sizeof(byte) +
            (flash ? count_width + FlashOfferTailBytes : count_width * 2 + UnityOfferTailBytes);
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
        CatalogPageProductReference[]? unity_product_references = null;
        CatalogPageProduct[]? unity_products = null;
        int club_level;
        if (flash)
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
        else
        {
            int reference_count = CatalogWire.ReadCount(
                in p,
                ProductReferenceMinimumBytes,
                checked(trailing_bytes + UnityOfferTailBytes + count_width),
                MaximumProductReferences,
                nameof(CatalogPageOffer.UnityProductReferences));
            budget.TakeProductReferences(reference_count);
            unity_product_references = new CatalogPageProductReference[reference_count];
            for (int index = 0; index < unity_product_references.Length; index++)
            {
                int sibling_bytes = checked(
                    (unity_product_references.Length - index - 1) * ProductReferenceMinimumBytes);
                unity_product_references[index] = new CatalogPageProductReference(
                    p.ReadShort(),
                    strings.Read(
                        in p,
                        nameof(CatalogPageProductReference.Identifier),
                        checked(trailing_bytes + UnityOfferTailBytes + count_width + sibling_bytes)));
            }

            int product_count = CatalogWire.ReadCount(
                in p,
                UnityProductMinimumBytes,
                checked(trailing_bytes + UnityOfferTailBytes),
                MaximumProducts,
                nameof(CatalogPageOffer.UnityProducts));
            budget.TakeProducts(product_count);
            unity_products = new CatalogPageProduct[product_count];
            products = new CatalogProduct[product_count];
            for (int index = 0; index < unity_products.Length; index++)
            {
                int sibling_bytes = checked(
                    (unity_products.Length - index - 1) * UnityProductMinimumBytes);
                CatalogPageProduct product = ParseNativeProduct(
                    in p,
                    checked(trailing_bytes + UnityOfferTailBytes + sibling_bytes),
                    ref strings);
                unity_products[index] = product;
                products[index] = ProjectProduct(product);
            }
            club_level = p.ReadShort();
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
            preview_image,
            unity_product_references,
            unity_products);
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

    internal static CatalogPageProduct ParseNativeProduct(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogStringBudget strings)
    {
        short product_type = p.ReadShort();
        int furni_class_id = p.ReadInt();
        string extra_param = strings.Read(
            in p,
            nameof(CatalogPageProduct.ExtraParam),
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
                nameof(CatalogPageProduct));
            series_size = p.ReadInt();
            items_left = p.ReadInt();
        }
        return new CatalogPageProduct(
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

    private static CatalogPageProductReference PrepareProductReference(
        CatalogPageProductReference value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.Identifier, nameof(CatalogPageProductReference.Identifier), in p);
        return value;
    }

    internal static CatalogPageProduct PrepareNativeProduct(
        CatalogPageProduct value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ExtraParam, nameof(CatalogPageProduct.ExtraParam), in p);
        RequireLimitedFields(
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft,
            nameof(CatalogPageProduct));
        return value;
    }

    internal static CatalogPageOffer PrepareOffer(
        CatalogPageOffer value,
        bool flash,
        bool strict_page_fields,
        ref CatalogPageBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.LocalizationId, nameof(CatalogPageOffer.LocalizationId), in p);
        strings.Require(value.PreviewImage, nameof(CatalogPageOffer.PreviewImage), in p);

        CatalogProduct[] products;
        CatalogPageProductReference[]? references = null;
        CatalogPageProduct[]? native_products = null;
        if (flash)
        {
            if (strict_page_fields &&
                (value.UnityProductReferences is not null || value.UnityProducts is not null))
            {
                throw new InvalidDataException(
                    "Flash catalog pages cannot carry Unity product collections.");
            }
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
        else
        {
            IReadOnlyList<CatalogPageProductReference> source_references =
                value.UnityProductReferences ?? Array.Empty<CatalogPageProductReference>();
            int reference_count = CatalogWire.RequireListCount(
                source_references,
                MaximumProductReferences,
                nameof(CatalogPageOffer.UnityProductReferences));
            budget.TakeProductReferences(reference_count);
            references = CatalogWire.SnapshotReferences(
                source_references,
                MaximumProductReferences,
                nameof(CatalogPageOffer.UnityProductReferences));
            for (int index = 0; index < references.Length; index++)
                references[index] = PrepareProductReference(references[index], ref strings, in p);

            if (value.UnityProducts is null)
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
                native_products = new CatalogPageProduct[products.Length];
                for (int index = 0; index < products.Length; index++)
                {
                    native_products[index] = ConvertProduct(products[index], ref strings, in p);
                }
            }
            else
            {
                int native_count = CatalogWire.RequireListCount(
                    value.UnityProducts,
                    MaximumProducts,
                    nameof(CatalogPageOffer.UnityProducts));
                int projected_count = CatalogWire.RequireListCount(
                    value.Products,
                    MaximumProducts,
                    nameof(CatalogPageOffer.Products));
                if (native_count != projected_count)
                {
                    throw new InvalidDataException(
                        "Unity catalog product collections must have matching counts.");
                }
                budget.TakeProducts(native_count);
                native_products = CatalogWire.SnapshotReferences(
                    value.UnityProducts,
                    MaximumProducts,
                    nameof(CatalogPageOffer.UnityProducts));
                products = CatalogWire.SnapshotReferences(
                    value.Products,
                    MaximumProducts,
                    nameof(CatalogPageOffer.Products));
                for (int index = 0; index < native_products.Length; index++)
                {
                    native_products[index] = PrepareNativeProduct(native_products[index], ref strings, in p);
                    RequireProjection(products[index], native_products[index], in p);
                }
            }

            if (value.ClubLevel < short.MinValue || value.ClubLevel > short.MaxValue)
                throw new InvalidDataException("Unity catalog club level does not fit the Int16 wire format.");
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
            value.PreviewImage,
            references,
            native_products);
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
        if (strict_page_fields && value.UnityProductType is not null)
            throw new InvalidDataException("Flash catalog products cannot carry a Unity product type.");
        if (value.ProductType == CatalogProduct.TypeBadge &&
            (value.FurniClassId != 0 || value.ProductCount != 1 || value.UniqueLimitedItem ||
             value.UniqueLimitedItemSeriesSize != 0 || value.UniqueLimitedItemsLeft != 0))
        {
            throw new InvalidDataException("Flash badge products contain fields absent from the wire layout.");
        }
    }

    internal static CatalogPageProduct ConvertProduct(
        CatalogProduct value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CatalogWire.RequireString(value.ProductType, nameof(CatalogProduct.ProductType), in p);
        strings.Require(value.ExtraParam, nameof(CatalogProduct.ExtraParam), in p);
        RequireLimitedFields(
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft,
            nameof(CatalogProduct));
        short product_type = ParseUnityType(value.ProductType);
        if (value.UnityProductType is short native_type && native_type != product_type)
            throw new InvalidDataException("Catalog product type conflicts with its Unity product type.");
        return new CatalogPageProduct(
            value.UnityProductType ?? product_type,
            value.FurniClassId,
            value.ExtraParam,
            value.ProductCount,
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft);
    }

    internal static void RequireProjection(
        CatalogProduct value,
        CatalogPageProduct native,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CatalogWire.RequireString(value.ProductType, nameof(CatalogProduct.ProductType), in p);
        CatalogWire.RequireString(value.ExtraParam, nameof(CatalogProduct.ExtraParam), in p);
        RequireLimitedFields(
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft,
            nameof(CatalogProduct));
        short product_type = ParseUnityType(value.ProductType);
        if (value.UnityProductType is short explicit_type && explicit_type != product_type)
            throw new InvalidDataException("Catalog product type conflicts with its Unity product type.");
        if (product_type != native.ProductType ||
            value.FurniClassId != native.FurniClassId ||
            !string.Equals(value.ExtraParam, native.ExtraParam, StringComparison.Ordinal) ||
            value.ProductCount != native.ProductCount ||
            value.UniqueLimitedItem != native.UniqueLimitedItem ||
            value.UniqueLimitedItemSeriesSize != native.UniqueLimitedItemSeriesSize ||
            value.UniqueLimitedItemsLeft != native.UniqueLimitedItemsLeft)
        {
            throw new InvalidDataException("Unity catalog product projection conflicts with its native product.");
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

    private static void WriteProductReference(CatalogPageProductReference value, in PacketWriter p)
    {
        p.WriteShort(value.ProductType);
        p.WriteString(value.Identifier);
    }

    internal static void WriteNativeProduct(CatalogPageProduct value, in PacketWriter p)
    {
        p.WriteShort(value.ProductType);
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

    internal static void WriteOffer(CatalogPageOffer value, bool flash, in PacketWriter p)
    {
        p.WriteInt(value.OfferId);
        p.WriteString(value.LocalizationId);
        p.WriteBool(value.IsRent);
        p.WriteInt(value.PriceInCredits);
        p.WriteInt(value.PriceInActivityPoints);
        p.WriteInt(value.ActivityPointType);
        p.WriteInt(value.PriceInSilver);
        p.WriteBool(value.Giftable);
        if (flash)
        {
            CatalogWire.WriteCount(value.Products.Count, in p);
            foreach (CatalogProduct product in value.Products)
                WriteFlashProduct(product, in p);
            p.WriteInt(value.ClubLevel);
        }
        else
        {
            IReadOnlyList<CatalogPageProductReference> references =
                value.UnityProductReferences ?? Array.Empty<CatalogPageProductReference>();
            IReadOnlyList<CatalogPageProduct> products =
                value.UnityProducts ?? throw new InvalidDataException("Prepared Unity products are missing.");
            CatalogWire.WriteCount(references.Count, in p);
            foreach (CatalogPageProductReference reference in references)
                WriteProductReference(reference, in p);
            CatalogWire.WriteCount(products.Count, in p);
            foreach (CatalogPageProduct product in products)
                WriteNativeProduct(product, in p);
            p.WriteShort((short)value.ClubLevel);
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

    internal static CatalogProduct ProjectProduct(CatalogPageProduct value) =>
        new(
            $"unity:{value.ProductType}",
            value.FurniClassId,
            value.ExtraParam,
            value.ProductCount,
            value.UniqueLimitedItem,
            value.UniqueLimitedItemSeriesSize,
            value.UniqueLimitedItemsLeft,
            value.ProductType);

    private static short ParseUnityType(string value)
    {
        if (value.StartsWith("unity:", StringComparison.Ordinal) &&
            short.TryParse(value.AsSpan(6), out short product_type))
        {
            return product_type;
        }
        if (value.Equals(CatalogProduct.TypeItem, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (value.Equals(CatalogProduct.TypeStuff, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (value.Equals(CatalogProduct.TypeEffect, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (value.Equals(CatalogProduct.TypeBadge, StringComparison.OrdinalIgnoreCase))
            return 4;
        throw new InvalidDataException($"Unknown Unity catalog product type: {value}.");
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

    internal static int MinimumOfferBytes(bool flash) => flash
        ? sizeof(int) + CatalogWire.StringMinimumBytes + sizeof(byte) + sizeof(int) * 4 +
          sizeof(byte) + sizeof(int) + sizeof(int) + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes
        : sizeof(int) + CatalogWire.StringMinimumBytes + sizeof(byte) + sizeof(int) * 4 +
          sizeof(byte) + sizeof(short) * 3 + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes;

    internal const int FlashProductMinimumBytes = CatalogWire.StringMinimumBytes * 2;
    internal const int ProductReferenceMinimumBytes = sizeof(short) + CatalogWire.StringMinimumBytes;
    internal const int UnityProductMinimumBytes =
        sizeof(short) + sizeof(int) + CatalogWire.StringMinimumBytes + sizeof(int) + sizeof(byte);
    private const int FlashOfferTailBytes =
        sizeof(int) + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes;
    private const int UnityOfferTailBytes =
        sizeof(short) + sizeof(byte) * 2 + CatalogWire.StringMinimumBytes;
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
