using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record GiftWrappingConfiguration : IParserComposer<GiftWrappingConfiguration>
{
    private IReadOnlyList<int> _stuff_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _box_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _ribbon_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _default_stuff_types = Array.AsReadOnly(Array.Empty<int>());

    public GiftWrappingConfiguration(
        bool IsWrappingEnabled,
        int WrappingPrice,
        IReadOnlyList<int> StuffTypes,
        IReadOnlyList<int> BoxTypes,
        IReadOnlyList<int> RibbonTypes,
        IReadOnlyList<int> DefaultStuffTypes)
    {
        this.IsWrappingEnabled = IsWrappingEnabled;
        this.WrappingPrice = WrappingPrice;
        this.StuffTypes = StuffTypes;
        this.BoxTypes = BoxTypes;
        this.RibbonTypes = RibbonTypes;
        this.DefaultStuffTypes = DefaultStuffTypes;
    }

    public bool IsWrappingEnabled { get; init; }

    public int WrappingPrice { get; init; }

    public IReadOnlyList<int> StuffTypes
    {
        get => _stuff_types;
        init => _stuff_types = GiftWire.FreezeValues(value, nameof(StuffTypes));
    }

    public IReadOnlyList<int> BoxTypes
    {
        get => _box_types;
        init => _box_types = GiftWire.FreezeValues(value, nameof(BoxTypes));
    }

    public IReadOnlyList<int> RibbonTypes
    {
        get => _ribbon_types;
        init => _ribbon_types = GiftWire.FreezeValues(value, nameof(RibbonTypes));
    }

    public IReadOnlyList<int> DefaultStuffTypes
    {
        get => _default_stuff_types;
        init => _default_stuff_types = GiftWire.FreezeValues(value, nameof(DefaultStuffTypes));
    }

    public static GiftWrappingConfiguration Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GiftWrappingConfiguration ParseFlash(in PacketReader p) =>
        GiftWire.ParseWrappingConfiguration(in p);

    private static GiftWrappingConfiguration ParseUnity(in PacketReader p) =>
        GiftWire.ParseWrappingConfiguration(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GiftWrappingConfiguration value, in PacketWriter p) =>
        GiftWire.ComposeWrappingConfiguration(value, in p);

    private static void ComposeUnity(GiftWrappingConfiguration value, in PacketWriter p) =>
        GiftWire.ComposeWrappingConfiguration(value, in p);

    public void Deconstruct(
        out bool IsWrappingEnabled,
        out int WrappingPrice,
        out IReadOnlyList<int> StuffTypes,
        out IReadOnlyList<int> BoxTypes,
        out IReadOnlyList<int> RibbonTypes,
        out IReadOnlyList<int> DefaultStuffTypes)
    {
        IsWrappingEnabled = this.IsWrappingEnabled;
        WrappingPrice = this.WrappingPrice;
        StuffTypes = this.StuffTypes;
        BoxTypes = this.BoxTypes;
        RibbonTypes = this.RibbonTypes;
        DefaultStuffTypes = this.DefaultStuffTypes;
    }
}

public sealed record PresentOpened(
    string ItemType,
    int ClassId,
    string ProductCode,
    Id PlacedItemId,
    string PlacedItemType,
    bool PlacedInRoom,
    string PetFigureString) : IParserComposer<PresentOpened>
{
    public static PresentOpened Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PresentOpened ParseFlash(in PacketReader p) =>
        GiftWire.ParsePresentOpened(in p, true);

    private static PresentOpened ParseUnity(in PacketReader p) =>
        GiftWire.ParsePresentOpened(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PresentOpened value, in PacketWriter p) =>
        GiftWire.ComposePresentOpened(value, true, in p);

    private static void ComposeUnity(PresentOpened value, in PacketWriter p) =>
        GiftWire.ComposePresentOpened(value, false, in p);
}

public sealed record ClubGiftEligibility(
    int OfferId,
    bool? IsVip,
    int DaysRequired,
    bool IsSelectable) : IParserComposer<ClubGiftEligibility>
{
    public static ClubGiftEligibility Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ClubGiftEligibility ParseFlash(in PacketReader p) =>
        GiftWire.ParseEligibility(in p, true);

    private static ClubGiftEligibility ParseUnity(in PacketReader p) =>
        GiftWire.ParseEligibility(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ClubGiftEligibility value, in PacketWriter p) =>
        GiftWire.ComposeEligibility(value, true, in p);

    private static void ComposeUnity(ClubGiftEligibility value, in PacketWriter p) =>
        GiftWire.ComposeEligibility(value, false, in p);
}

public sealed record ClubGiftInfo : IParserComposer<ClubGiftInfo>
{
    private IReadOnlyList<CatalogPageOffer> _offers = Array.AsReadOnly(Array.Empty<CatalogPageOffer>());
    private IReadOnlyList<ClubGiftEligibility> _gift_eligibility =
        Array.AsReadOnly(Array.Empty<ClubGiftEligibility>());

    public ClubGiftInfo(
        int DaysUntilNextGift,
        int GiftsAvailable,
        IReadOnlyList<CatalogPageOffer> Offers,
        IReadOnlyList<ClubGiftEligibility> GiftEligibility)
    {
        this.DaysUntilNextGift = DaysUntilNextGift;
        this.GiftsAvailable = GiftsAvailable;
        this.Offers = Offers;
        this.GiftEligibility = GiftEligibility;
    }

    public int DaysUntilNextGift { get; init; }

    public int GiftsAvailable { get; init; }

    public IReadOnlyList<CatalogPageOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumOffers,
            nameof(Offers));
    }

    public IReadOnlyList<ClubGiftEligibility> GiftEligibility
    {
        get => _gift_eligibility;
        init => _gift_eligibility = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumCollectionCount,
            nameof(GiftEligibility));
    }

    public static ClubGiftInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ClubGiftInfo ParseFlash(in PacketReader p) =>
        GiftWire.ParseClubGiftInfo(in p, true);

    private static ClubGiftInfo ParseUnity(in PacketReader p) =>
        GiftWire.ParseClubGiftInfo(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ClubGiftInfo value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftInfo(value, true, in p);

    private static void ComposeUnity(ClubGiftInfo value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftInfo(value, false, in p);

    public void Deconstruct(
        out int DaysUntilNextGift,
        out int GiftsAvailable,
        out IReadOnlyList<CatalogPageOffer> Offers,
        out IReadOnlyList<ClubGiftEligibility> GiftEligibility)
    {
        DaysUntilNextGift = this.DaysUntilNextGift;
        GiftsAvailable = this.GiftsAvailable;
        Offers = this.Offers;
        GiftEligibility = this.GiftEligibility;
    }
}

public sealed record ClubGiftSelected : IParserComposer<ClubGiftSelected>
{
    private string _product_code = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());
    private IReadOnlyList<CatalogPageProduct>? _unity_products;

    public ClubGiftSelected(
        string ProductCode,
        IReadOnlyList<CatalogProduct> Products,
        IReadOnlyList<CatalogPageProduct>? UnityProducts = null)
    {
        this.ProductCode = ProductCode;
        this.Products = Products;
        this.UnityProducts = UnityProducts;
    }

    public string ProductCode
    {
        get => _product_code;
        init => _product_code = CatalogWire.RequireReference(value, nameof(ProductCode));
    }

    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(Products));
    }

    public IReadOnlyList<CatalogPageProduct>? UnityProducts
    {
        get => _unity_products;
        init => _unity_products = CatalogWire.FreezeOptionalReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(UnityProducts));
    }

    public static ClubGiftSelected Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ClubGiftSelected ParseFlash(in PacketReader p) =>
        GiftWire.ParseClubGiftSelected(in p, true);

    private static ClubGiftSelected ParseUnity(in PacketReader p) =>
        GiftWire.ParseClubGiftSelected(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ClubGiftSelected value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftSelected(value, true, in p);

    private static void ComposeUnity(ClubGiftSelected value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftSelected(value, false, in p);

    public void Deconstruct(
        out string ProductCode,
        out IReadOnlyList<CatalogProduct> Products,
        out IReadOnlyList<CatalogPageProduct>? UnityProducts)
    {
        ProductCode = this.ProductCode;
        Products = this.Products;
        UnityProducts = this.UnityProducts;
    }
}

public sealed record GiftReceiverNotFound : IParserComposer<GiftReceiverNotFound>
{
    public static GiftReceiverNotFound Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GiftReceiverNotFound ParseFlash(in PacketReader p)
    {
        CatalogWire.RequireEmpty(in p, nameof(GiftReceiverNotFound));
        return new GiftReceiverNotFound();
    }

    private static GiftReceiverNotFound ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<GiftReceiverNotFound>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GiftReceiverNotFound value, in PacketWriter p) { }

    private static void ComposeUnity(GiftReceiverNotFound value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);
}

public sealed record ClubGiftNotification(int NumGifts) : IParserComposer<ClubGiftNotification>
{
    public static ClubGiftNotification Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ClubGiftNotification ParseFlash(in PacketReader p)
    {
        var value = new ClubGiftNotification(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(ClubGiftNotification));
        return value;
    }

    private static ClubGiftNotification ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<ClubGiftNotification>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ClubGiftNotification value, in PacketWriter p) =>
        p.WriteInt(value.NumGifts);

    private static void ComposeUnity(ClubGiftNotification value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);
}

public sealed record IsOfferGiftable(
    int OfferId,
    bool IsGiftable) : IParserComposer<IsOfferGiftable>
{
    public static IsOfferGiftable Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static IsOfferGiftable ParseFlash(in PacketReader p)
    {
        var value = new IsOfferGiftable(p.ReadInt(), p.ReadBool());
        CatalogWire.RequireEmpty(in p, nameof(IsOfferGiftable));
        return value;
    }

    private static IsOfferGiftable ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<IsOfferGiftable>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(IsOfferGiftable value, in PacketWriter p)
    {
        p.WriteInt(value.OfferId);
        p.WriteBool(value.IsGiftable);
    }

    private static void ComposeUnity(IsOfferGiftable value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);
}

public sealed record NuxGiftProduct(
    string ProductCode,
    string? LocalizationKey) : IParserComposer<NuxGiftProduct>
{
    public static NuxGiftProduct Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGiftProduct ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxProduct(in p);

    private static NuxGiftProduct ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<NuxGiftProduct>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGiftProduct value, in PacketWriter p) =>
        GiftWire.ComposeNuxProduct(value, in p);

    private static void ComposeUnity(NuxGiftProduct value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);
}

public sealed record NuxGiftOption : IParserComposer<NuxGiftOption>
{
    private IReadOnlyList<NuxGiftProduct> _products = Array.AsReadOnly(Array.Empty<NuxGiftProduct>());

    public NuxGiftOption(string? ThumbnailUrl, IReadOnlyList<NuxGiftProduct> Products)
    {
        this.ThumbnailUrl = ThumbnailUrl;
        this.Products = Products;
    }

    public string? ThumbnailUrl { get; init; }

    public IReadOnlyList<NuxGiftProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxProducts,
            nameof(Products));
    }

    public static NuxGiftOption Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGiftOption ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxOption(in p);

    private static NuxGiftOption ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<NuxGiftOption>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGiftOption value, in PacketWriter p) =>
        GiftWire.ComposeNuxOption(value, in p);

    private static void ComposeUnity(NuxGiftOption value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);

    public void Deconstruct(out string? ThumbnailUrl, out IReadOnlyList<NuxGiftProduct> Products)
    {
        ThumbnailUrl = this.ThumbnailUrl;
        Products = this.Products;
    }
}

public sealed record NuxGiftStep : IParserComposer<NuxGiftStep>
{
    private IReadOnlyList<NuxGiftOption> _options = Array.AsReadOnly(Array.Empty<NuxGiftOption>());

    public NuxGiftStep(int DayIndex, int StepIndex, IReadOnlyList<NuxGiftOption> Options)
    {
        this.DayIndex = DayIndex;
        this.StepIndex = StepIndex;
        this.Options = Options;
    }

    public int DayIndex { get; init; }

    public int StepIndex { get; init; }

    public IReadOnlyList<NuxGiftOption> Options
    {
        get => _options;
        init => _options = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxOptions,
            nameof(Options));
    }

    public static NuxGiftStep Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGiftStep ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxStep(in p);

    private static NuxGiftStep ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<NuxGiftStep>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGiftStep value, in PacketWriter p) =>
        GiftWire.ComposeNuxStep(value, in p);

    private static void ComposeUnity(NuxGiftStep value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);

    public void Deconstruct(
        out int DayIndex,
        out int StepIndex,
        out IReadOnlyList<NuxGiftOption> Options)
    {
        DayIndex = this.DayIndex;
        StepIndex = this.StepIndex;
        Options = this.Options;
    }
}

public sealed record NuxGiftOffer : IParserComposer<NuxGiftOffer>
{
    private IReadOnlyList<NuxGiftStep> _steps = Array.AsReadOnly(Array.Empty<NuxGiftStep>());

    public NuxGiftOffer(IReadOnlyList<NuxGiftStep> Steps) => this.Steps = Steps;

    public IReadOnlyList<NuxGiftStep> Steps
    {
        get => _steps;
        init => _steps = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxSteps,
            nameof(Steps));
    }

    public static NuxGiftOffer Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGiftOffer ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxOffer(in p);

    private static NuxGiftOffer ParseUnity(in PacketReader p) =>
        GiftWire.UnsupportedUnity<NuxGiftOffer>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGiftOffer value, in PacketWriter p) =>
        GiftWire.ComposeNuxOffer(value, in p);

    private static void ComposeUnity(NuxGiftOffer value, in PacketWriter p) =>
        GiftWire.UnsupportedUnity(p.Client);

    public void Deconstruct(out IReadOnlyList<NuxGiftStep> Steps) => Steps = this.Steps;
}

public readonly record struct NuxGiftSelection(
    int DayIndex,
    int StepIndex,
    int GiftIndex) : IParserComposer<NuxGiftSelection>
{
    public static NuxGiftSelection Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGiftSelection ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    private static NuxGiftSelection ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGiftSelection value, in PacketWriter p) =>
        GiftWire.WriteSelection(value, in p);

    private static void ComposeUnity(NuxGiftSelection value, in PacketWriter p) =>
        GiftWire.WriteSelection(value, in p);
}

public sealed record NuxNotComplete : IParserComposer<NuxNotComplete>
{
    public static NuxNotComplete Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxNotComplete ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<NuxNotComplete>(in p, static () => new NuxNotComplete());

    private static NuxNotComplete ParseUnity(in PacketReader p) =>
        GiftWire.ParseEmpty<NuxNotComplete>(in p, static () => new NuxNotComplete());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxNotComplete value, in PacketWriter p) { }

    private static void ComposeUnity(NuxNotComplete value, in PacketWriter p) { }
}

public sealed record NuxGetGifts : IParserComposer<NuxGetGifts>
{
    private IReadOnlyList<NuxGiftSelection> _selections =
        Array.AsReadOnly(Array.Empty<NuxGiftSelection>());

    public NuxGetGifts(IReadOnlyList<NuxGiftSelection> Selections) => this.Selections = Selections;

    public IReadOnlyList<NuxGiftSelection> Selections
    {
        get => _selections;
        init => _selections = CatalogWire.FreezeValues(
            value,
            GiftWire.MaximumNuxSelections,
            nameof(Selections));
    }

    public static NuxGetGifts Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static NuxGetGifts ParseFlash(in PacketReader p) => GiftWire.ParseNuxGetGifts(in p);

    private static NuxGetGifts ParseUnity(in PacketReader p) => GiftWire.ParseNuxGetGifts(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(NuxGetGifts value, in PacketWriter p) =>
        GiftWire.ComposeNuxGetGifts(value, in p);

    private static void ComposeUnity(NuxGetGifts value, in PacketWriter p) =>
        GiftWire.ComposeNuxGetGifts(value, in p);

    public void Deconstruct(out IReadOnlyList<NuxGiftSelection> Selections) =>
        Selections = this.Selections;
}

public sealed record PresentOpen(Id FurniId) : IParserComposer<PresentOpen>
{
    public static PresentOpen Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PresentOpen ParseFlash(in PacketReader p) =>
        GiftWire.ParsePresentOpen(in p, true);

    private static PresentOpen ParseUnity(in PacketReader p) =>
        GiftWire.ParsePresentOpen(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PresentOpen value, in PacketWriter p) =>
        GiftWire.ComposePresentOpen(value, true, in p);

    private static void ComposeUnity(PresentOpen value, in PacketWriter p) =>
        GiftWire.ComposePresentOpen(value, false, in p);
}

public sealed record PurchaseFromCatalogAsGift(
    int PageId,
    int OfferId,
    string ExtraData,
    string ReceiverName,
    string GiftMessage,
    int BoxType,
    int RibbonType,
    int Color,
    bool IsIncognito,
    int? Quantity = null) : IParserComposer<PurchaseFromCatalogAsGift>
{
    public static PurchaseFromCatalogAsGift Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PurchaseFromCatalogAsGift ParseFlash(in PacketReader p) =>
        GiftWire.ParsePurchase(in p, true);

    private static PurchaseFromCatalogAsGift ParseUnity(in PacketReader p) =>
        GiftWire.ParsePurchase(in p, false);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PurchaseFromCatalogAsGift value, in PacketWriter p) =>
        GiftWire.ComposePurchase(value, true, in p);

    private static void ComposeUnity(PurchaseFromCatalogAsGift value, in PacketWriter p) =>
        GiftWire.ComposePurchase(value, false, in p);
}

public sealed record GetGiftWrappingConfiguration : IParserComposer<GetGiftWrappingConfiguration>
{
    public static GetGiftWrappingConfiguration Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetGiftWrappingConfiguration ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<GetGiftWrappingConfiguration>(
            in p,
            static () => new GetGiftWrappingConfiguration());

    private static GetGiftWrappingConfiguration ParseUnity(in PacketReader p) =>
        GiftWire.ParseEmpty<GetGiftWrappingConfiguration>(
            in p,
            static () => new GetGiftWrappingConfiguration());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetGiftWrappingConfiguration value, in PacketWriter p) { }

    private static void ComposeUnity(GetGiftWrappingConfiguration value, in PacketWriter p) { }
}

public sealed record GetClubGift : IParserComposer<GetClubGift>
{
    public static GetClubGift Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetClubGift ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<GetClubGift>(in p, static () => new GetClubGift());

    private static GetClubGift ParseUnity(in PacketReader p) =>
        GiftWire.ParseEmpty<GetClubGift>(in p, static () => new GetClubGift());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetClubGift value, in PacketWriter p) { }

    private static void ComposeUnity(GetClubGift value, in PacketWriter p) { }
}

public sealed record SelectClubGift(string ProductCode) : IParserComposer<SelectClubGift>
{
    public static SelectClubGift Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SelectClubGift ParseFlash(in PacketReader p) => GiftWire.ParseSelectClubGift(in p);

    private static SelectClubGift ParseUnity(in PacketReader p) => GiftWire.ParseSelectClubGift(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SelectClubGift value, in PacketWriter p) =>
        GiftWire.ComposeSelectClubGift(value, in p);

    private static void ComposeUnity(SelectClubGift value, in PacketWriter p) =>
        GiftWire.ComposeSelectClubGift(value, in p);
}

public sealed record GetIsOfferGiftable(int OfferId) : IParserComposer<GetIsOfferGiftable>
{
    public static GetIsOfferGiftable Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static GetIsOfferGiftable ParseFlash(in PacketReader p) =>
        GiftWire.ParseOfferGiftabilityRequest(in p);

    private static GetIsOfferGiftable ParseUnity(in PacketReader p) =>
        GiftWire.ParseOfferGiftabilityRequest(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(GetIsOfferGiftable value, in PacketWriter p) =>
        p.WriteInt(value.OfferId);

    private static void ComposeUnity(GetIsOfferGiftable value, in PacketWriter p) =>
        p.WriteInt(value.OfferId);
}

public sealed record AdvanceNewUserFlowRequest : IParserComposer<AdvanceNewUserFlowRequest>
{
    public static AdvanceNewUserFlowRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AdvanceNewUserFlowRequest ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<AdvanceNewUserFlowRequest>(
            in p,
            static () => new AdvanceNewUserFlowRequest());

    private static AdvanceNewUserFlowRequest ParseUnity(in PacketReader p) =>
        GiftWire.ParseEmpty<AdvanceNewUserFlowRequest>(
            in p,
            static () => new AdvanceNewUserFlowRequest());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AdvanceNewUserFlowRequest value, in PacketWriter p) { }

    private static void ComposeUnity(AdvanceNewUserFlowRequest value, in PacketWriter p) { }
}

internal static class GiftWire
{
    internal const int MaximumCollectionCount = ushort.MaxValue;
    internal const int MaximumNuxSteps = CatalogPageWire.MaximumOffers;
    internal const int MaximumNuxOptions = ushort.MaxValue;
    internal const int MaximumNuxProducts = ushort.MaxValue;
    internal const int MaximumNuxSelections = ushort.MaxValue / 3;

    private const int FlashEligibilityBytes = sizeof(int) + sizeof(byte) + sizeof(int) + sizeof(byte);
    private const int UnityEligibilityBytes = sizeof(int) + sizeof(int) + sizeof(byte);
    private const int NuxProductMinimumBytes = CatalogWire.StringMinimumBytes * 2;
    private const int NuxOptionMinimumBytes = CatalogWire.StringMinimumBytes + sizeof(int);
    private const int NuxStepMinimumBytes = sizeof(int) * 3;

    public static IReadOnlyList<int> FreezeValues(IReadOnlyList<int> values, string name) =>
        CatalogWire.FreezeValues(values, MaximumCollectionCount, name);

    public static GiftWrappingConfiguration ParseWrappingConfiguration(in PacketReader p)
    {
        bool enabled = p.ReadBool();
        int price = p.ReadInt();
        int count_width = CatalogWire.CountWidth(p.Client);
        int[] stuff_types = ReadIntValues(
            in p,
            checked(count_width * 3),
            nameof(GiftWrappingConfiguration.StuffTypes));
        int[] box_types = ReadIntValues(
            in p,
            checked(count_width * 2),
            nameof(GiftWrappingConfiguration.BoxTypes));
        int[] ribbon_types = ReadIntValues(
            in p,
            count_width,
            nameof(GiftWrappingConfiguration.RibbonTypes));
        int[] default_stuff_types = ReadIntValues(
            in p,
            0,
            nameof(GiftWrappingConfiguration.DefaultStuffTypes));
        CatalogWire.RequireEmpty(in p, nameof(GiftWrappingConfiguration));
        return new GiftWrappingConfiguration(
            enabled,
            price,
            stuff_types,
            box_types,
            ribbon_types,
            default_stuff_types);
    }

    public static void ComposeWrappingConfiguration(
        GiftWrappingConfiguration value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int[] stuff_types = SnapshotValues(value.StuffTypes, nameof(value.StuffTypes));
        int[] box_types = SnapshotValues(value.BoxTypes, nameof(value.BoxTypes));
        int[] ribbon_types = SnapshotValues(value.RibbonTypes, nameof(value.RibbonTypes));
        int[] default_stuff_types = SnapshotValues(
            value.DefaultStuffTypes,
            nameof(value.DefaultStuffTypes));

        p.WriteBool(value.IsWrappingEnabled);
        p.WriteInt(value.WrappingPrice);
        WriteIntValues(stuff_types, in p);
        WriteIntValues(box_types, in p);
        WriteIntValues(ribbon_types, in p);
        WriteIntValues(default_stuff_types, in p);
    }

    public static PresentOpened ParsePresentOpened(in PacketReader p, bool flash)
    {
        var strings = NewStringBudget();
        int id_width = flash ? sizeof(int) : sizeof(long);
        string item_type = strings.Read(
            in p,
            nameof(PresentOpened.ItemType),
            checked(sizeof(int) + CatalogWire.StringMinimumBytes + id_width +
                CatalogWire.StringMinimumBytes + sizeof(byte) + CatalogWire.StringMinimumBytes));
        int class_id = p.ReadInt();
        string product_code = strings.Read(
            in p,
            nameof(PresentOpened.ProductCode),
            checked(id_width + CatalogWire.StringMinimumBytes + sizeof(byte) +
                CatalogWire.StringMinimumBytes));
        Id placed_item_id = flash ? ReadFlashId(in p) : ReadUnityId(in p);
        string placed_item_type = strings.Read(
            in p,
            nameof(PresentOpened.PlacedItemType),
            checked(sizeof(byte) + CatalogWire.StringMinimumBytes));
        bool placed_in_room = p.ReadBool();
        string pet_figure = strings.Read(in p, nameof(PresentOpened.PetFigureString));
        CatalogWire.RequireEmpty(in p, nameof(PresentOpened));
        return new PresentOpened(
            item_type,
            class_id,
            product_code,
            placed_item_id,
            placed_item_type,
            placed_in_room,
            pet_figure);
    }

    public static void ComposePresentOpened(PresentOpened value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = NewStringBudget();
        strings.Require(value.ItemType, nameof(value.ItemType), in p);
        strings.Require(value.ProductCode, nameof(value.ProductCode), in p);
        strings.Require(value.PlacedItemType, nameof(value.PlacedItemType), in p);
        strings.Require(value.PetFigureString, nameof(value.PetFigureString), in p);
        if (flash)
            RequireFlashId(value.PlacedItemId);

        p.WriteString(value.ItemType);
        p.WriteInt(value.ClassId);
        p.WriteString(value.ProductCode);
        if (flash)
            WriteFlashId(in p, value.PlacedItemId);
        else
            WriteUnityId(in p, value.PlacedItemId);
        p.WriteString(value.PlacedItemType);
        p.WriteBool(value.PlacedInRoom);
        p.WriteString(value.PetFigureString);
    }

    public static ClubGiftEligibility ParseEligibility(in PacketReader p, bool flash) => flash
        ? new ClubGiftEligibility(p.ReadInt(), p.ReadBool(), p.ReadInt(), p.ReadBool())
        : new ClubGiftEligibility(p.ReadInt(), null, p.ReadInt(), p.ReadBool());

    public static void ComposeEligibility(ClubGiftEligibility value, bool flash, in PacketWriter p)
    {
        PrepareEligibility(value, flash);
        WriteEligibility(value, flash, in p);
    }

    public static ClubGiftInfo ParseClubGiftInfo(in PacketReader p, bool flash)
    {
        int days_until_next_gift = p.ReadInt();
        int gifts_available = p.ReadInt();
        int count_width = CatalogWire.CountWidth(p.Client);
        int minimum_offer_bytes = CatalogPageWire.MinimumOfferBytes(flash);
        var catalog_budget = new CatalogPageBudget();
        var strings = NewStringBudget();
        int offer_count = CatalogWire.ReadCount(
            in p,
            minimum_offer_bytes,
            count_width,
            CatalogPageWire.MaximumOffers,
            nameof(ClubGiftInfo.Offers));
        catalog_budget.TakeOffers(offer_count);
        var offers = new CatalogPageOffer[offer_count];
        for (int index = 0; index < offers.Length; index++)
        {
            int sibling_bytes = checked((offers.Length - index - 1) * minimum_offer_bytes);
            offers[index] = CatalogPageWire.ParseOffer(
                in p,
                flash,
                checked(count_width + sibling_bytes),
                ref catalog_budget,
                ref strings);
        }

        int eligibility_bytes = flash ? FlashEligibilityBytes : UnityEligibilityBytes;
        int eligibility_count = CatalogWire.ReadCount(
            in p,
            eligibility_bytes,
            0,
            MaximumCollectionCount,
            nameof(ClubGiftInfo.GiftEligibility));
        var eligibility = new ClubGiftEligibility[eligibility_count];
        for (int index = 0; index < eligibility.Length; index++)
            eligibility[index] = ParseEligibility(in p, flash);
        CatalogWire.RequireEmpty(in p, nameof(ClubGiftInfo));
        return new ClubGiftInfo(days_until_next_gift, gifts_available, offers, eligibility);
    }

    public static void ComposeClubGiftInfo(ClubGiftInfo value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CatalogPageOffer[] offers = CatalogWire.SnapshotReferences(
            value.Offers,
            CatalogPageWire.MaximumOffers,
            nameof(value.Offers));
        ClubGiftEligibility[] eligibility = CatalogWire.SnapshotReferences(
            value.GiftEligibility,
            MaximumCollectionCount,
            nameof(value.GiftEligibility));
        var catalog_budget = new CatalogPageBudget();
        catalog_budget.TakeOffers(offers.Length);
        var strings = NewStringBudget();
        for (int index = 0; index < offers.Length; index++)
        {
            offers[index] = CatalogPageWire.PrepareOffer(
                offers[index],
                flash,
                true,
                ref catalog_budget,
                ref strings,
                in p);
        }
        foreach (ClubGiftEligibility item in eligibility)
            PrepareEligibility(item, flash);

        p.WriteInt(value.DaysUntilNextGift);
        p.WriteInt(value.GiftsAvailable);
        CatalogWire.WriteCount(offers.Length, in p);
        foreach (CatalogPageOffer offer in offers)
            CatalogPageWire.WriteOffer(offer, flash, in p);
        CatalogWire.WriteCount(eligibility.Length, in p);
        foreach (ClubGiftEligibility item in eligibility)
            WriteEligibility(item, flash, in p);
    }

    public static ClubGiftSelected ParseClubGiftSelected(in PacketReader p, bool flash)
    {
        var strings = NewStringBudget();
        int count_width = CatalogWire.CountWidth(p.Client);
        string product_code = strings.Read(
            in p,
            nameof(ClubGiftSelected.ProductCode),
            count_width);
        int minimum_product_bytes = flash
            ? CatalogPageWire.FlashProductMinimumBytes
            : CatalogPageWire.UnityProductMinimumBytes;
        int product_count = CatalogWire.ReadCount(
            in p,
            minimum_product_bytes,
            0,
            CatalogPageWire.MaximumProducts,
            nameof(ClubGiftSelected.Products));
        var catalog_budget = new CatalogPageBudget();
        catalog_budget.TakeProducts(product_count);
        var products = new CatalogProduct[product_count];
        CatalogPageProduct[]? unity_products = flash ? null : new CatalogPageProduct[product_count];
        for (int index = 0; index < products.Length; index++)
        {
            int sibling_bytes = checked((products.Length - index - 1) * minimum_product_bytes);
            if (flash)
            {
                products[index] = CatalogPageWire.ParseFlashProduct(
                    in p,
                    sibling_bytes,
                    ref strings);
            }
            else
            {
                CatalogPageProduct product = CatalogPageWire.ParseNativeProduct(
                    in p,
                    sibling_bytes,
                    ref strings);
                unity_products![index] = product;
                products[index] = CatalogPageWire.ProjectProduct(product);
            }
        }
        CatalogWire.RequireEmpty(in p, nameof(ClubGiftSelected));
        return new ClubGiftSelected(product_code, products, unity_products);
    }

    public static void ComposeClubGiftSelected(
        ClubGiftSelected value,
        bool flash,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = NewStringBudget();
        strings.Require(value.ProductCode, nameof(value.ProductCode), in p);
        CatalogProduct[] products = CatalogWire.SnapshotReferences(
            value.Products,
            CatalogPageWire.MaximumProducts,
            nameof(value.Products));
        var catalog_budget = new CatalogPageBudget();
        catalog_budget.TakeProducts(products.Length);
        CatalogPageProduct[]? unity_products = null;
        if (flash)
        {
            if (value.UnityProducts is not null)
                throw new InvalidDataException("Flash club gift selections cannot carry Unity products.");
            foreach (CatalogProduct product in products)
                CatalogPageWire.PrepareFlashProduct(product, true, ref strings, in p);
        }
        else if (value.UnityProducts is null)
        {
            unity_products = new CatalogPageProduct[products.Length];
            for (int index = 0; index < products.Length; index++)
                unity_products[index] = CatalogPageWire.ConvertProduct(products[index], ref strings, in p);
        }
        else
        {
            unity_products = CatalogWire.SnapshotReferences(
                value.UnityProducts,
                CatalogPageWire.MaximumProducts,
                nameof(value.UnityProducts));
            if (unity_products.Length != products.Length)
                throw new InvalidDataException("Unity club gift product collections must have matching counts.");
            for (int index = 0; index < unity_products.Length; index++)
            {
                unity_products[index] = CatalogPageWire.PrepareNativeProduct(
                    unity_products[index],
                    ref strings,
                    in p);
                CatalogPageWire.RequireProjection(products[index], unity_products[index], in p);
            }
        }

        p.WriteString(value.ProductCode);
        CatalogWire.WriteCount(products.Length, in p);
        if (flash)
        {
            foreach (CatalogProduct product in products)
                CatalogPageWire.WriteFlashProduct(product, in p);
        }
        else
        {
            foreach (CatalogPageProduct product in unity_products!)
                CatalogPageWire.WriteNativeProduct(product, in p);
        }
    }

    public static NuxGiftProduct ParseNuxProduct(in PacketReader p)
    {
        var strings = NewStringBudget();
        return ParseNuxProduct(in p, 0, ref strings);
    }

    public static void ComposeNuxProduct(NuxGiftProduct value, in PacketWriter p)
    {
        var strings = NewStringBudget();
        PrepareNuxProduct(value, ref strings, in p);
        WriteNuxProduct(value, in p);
    }

    public static NuxGiftOption ParseNuxOption(in PacketReader p)
    {
        var budget = new GiftBudget();
        budget.TakeOptions(1);
        var strings = NewStringBudget();
        return ParseNuxOption(in p, 0, ref budget, ref strings);
    }

    public static void ComposeNuxOption(NuxGiftOption value, in PacketWriter p)
    {
        var budget = new GiftBudget();
        budget.TakeOptions(1);
        var strings = NewStringBudget();
        PrepareNuxOption(value, ref budget, ref strings, in p);
        WriteNuxOption(value, in p);
    }

    public static NuxGiftStep ParseNuxStep(in PacketReader p)
    {
        var budget = new GiftBudget();
        budget.TakeSteps(1);
        var strings = NewStringBudget();
        return ParseNuxStep(in p, 0, ref budget, ref strings);
    }

    public static void ComposeNuxStep(NuxGiftStep value, in PacketWriter p)
    {
        var budget = new GiftBudget();
        budget.TakeSteps(1);
        var strings = NewStringBudget();
        PrepareNuxStep(value, ref budget, ref strings, in p);
        WriteNuxStep(value, in p);
    }

    public static NuxGiftOffer ParseNuxOffer(in PacketReader p)
    {
        var budget = new GiftBudget();
        var strings = NewStringBudget();
        int step_count = CatalogWire.ReadCount(
            in p,
            NuxStepMinimumBytes,
            0,
            MaximumNuxSteps,
            nameof(NuxGiftOffer.Steps));
        budget.TakeSteps(step_count);
        var steps = new NuxGiftStep[step_count];
        for (int index = 0; index < steps.Length; index++)
        {
            int sibling_bytes = checked((steps.Length - index - 1) * NuxStepMinimumBytes);
            steps[index] = ParseNuxStep(in p, sibling_bytes, ref budget, ref strings);
        }
        CatalogWire.RequireEmpty(in p, nameof(NuxGiftOffer));
        return new NuxGiftOffer(steps);
    }

    public static void ComposeNuxOffer(NuxGiftOffer value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int step_count = CatalogWire.RequireListCount(
            value.Steps,
            MaximumNuxSteps,
            nameof(value.Steps));
        var budget = new GiftBudget();
        budget.TakeSteps(step_count);
        var strings = NewStringBudget();
        foreach (NuxGiftStep step in value.Steps)
            PrepareNuxStep(step, ref budget, ref strings, in p);

        CatalogWire.WriteCount(step_count, in p);
        foreach (NuxGiftStep step in value.Steps)
            WriteNuxStep(step, in p);
    }

    public static NuxGetGifts ParseNuxGetGifts(in PacketReader p)
    {
        int value_count = CatalogWire.ReadCount(
            in p,
            sizeof(int),
            0,
            MaximumCollectionCount,
            nameof(NuxGetGifts.Selections));
        if (value_count % 3 != 0)
            throw new InvalidDataException("NUX gift selections must contain complete day, step and gift triples.");
        var selections = new NuxGiftSelection[value_count / 3];
        for (int index = 0; index < selections.Length; index++)
            selections[index] = new NuxGiftSelection(p.ReadInt(), p.ReadInt(), p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(NuxGetGifts));
        return new NuxGetGifts(selections);
    }

    public static void ComposeNuxGetGifts(NuxGetGifts value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        NuxGiftSelection[] selections = CatalogWire.SnapshotValues(
            value.Selections,
            MaximumNuxSelections,
            nameof(value.Selections));
        int value_count = checked(selections.Length * 3);
        CatalogWire.RequireCount(value_count, MaximumCollectionCount, nameof(value.Selections));

        CatalogWire.WriteCount(value_count, in p);
        foreach (NuxGiftSelection selection in selections)
            WriteSelection(selection, in p);
    }

    public static PresentOpen ParsePresentOpen(in PacketReader p, bool flash)
    {
        var value = new PresentOpen(flash ? ReadFlashId(in p) : ReadUnityId(in p));
        CatalogWire.RequireEmpty(in p, nameof(PresentOpen));
        return value;
    }

    public static void ComposePresentOpen(PresentOpen value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash)
            RequireFlashId(value.FurniId);
        if (flash)
            WriteFlashId(in p, value.FurniId);
        else
            WriteUnityId(in p, value.FurniId);
    }

    public static PurchaseFromCatalogAsGift ParsePurchase(in PacketReader p, bool flash)
    {
        var strings = NewStringBudget();
        int quantity_bytes = flash ? 0 : sizeof(int);
        int page_id = p.ReadInt();
        int offer_id = p.ReadInt();
        string extra_data = strings.Read(
            in p,
            nameof(PurchaseFromCatalogAsGift.ExtraData),
            checked(CatalogWire.StringMinimumBytes * 2 + sizeof(int) * 3 + sizeof(byte) +
                quantity_bytes));
        string receiver_name = strings.Read(
            in p,
            nameof(PurchaseFromCatalogAsGift.ReceiverName),
            checked(CatalogWire.StringMinimumBytes + sizeof(int) * 3 + sizeof(byte) +
                quantity_bytes));
        string gift_message = strings.Read(
            in p,
            nameof(PurchaseFromCatalogAsGift.GiftMessage),
            checked(sizeof(int) * 3 + sizeof(byte) + quantity_bytes));
        var value = new PurchaseFromCatalogAsGift(
            page_id,
            offer_id,
            extra_data,
            receiver_name,
            gift_message,
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadBool(),
            flash ? null : p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(PurchaseFromCatalogAsGift));
        return value;
    }

    public static void ComposePurchase(
        PurchaseFromCatalogAsGift value,
        bool flash,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash && value.Quantity is not null)
            throw new InvalidDataException("Flash gift purchases cannot represent a quantity.");
        if (!flash && value.Quantity is null)
            throw new InvalidDataException("Unity gift purchases require a quantity.");
        var strings = NewStringBudget();
        strings.Require(value.ExtraData, nameof(value.ExtraData), in p);
        strings.Require(value.ReceiverName, nameof(value.ReceiverName), in p);
        strings.Require(value.GiftMessage, nameof(value.GiftMessage), in p);

        p.WriteInt(value.PageId);
        p.WriteInt(value.OfferId);
        p.WriteString(value.ExtraData);
        p.WriteString(value.ReceiverName);
        p.WriteString(value.GiftMessage);
        p.WriteInt(value.BoxType);
        p.WriteInt(value.RibbonType);
        p.WriteInt(value.Color);
        p.WriteBool(value.IsIncognito);
        if (!flash)
            p.WriteInt(value.Quantity!.Value);
    }

    public static SelectClubGift ParseSelectClubGift(in PacketReader p)
    {
        var strings = NewStringBudget();
        string product_code = strings.Read(in p, nameof(SelectClubGift.ProductCode));
        CatalogWire.RequireEmpty(in p, nameof(SelectClubGift));
        return new SelectClubGift(product_code);
    }

    public static void ComposeSelectClubGift(SelectClubGift value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = NewStringBudget();
        strings.Require(value.ProductCode, nameof(value.ProductCode), in p);
        p.WriteString(value.ProductCode);
    }

    public static GetIsOfferGiftable ParseOfferGiftabilityRequest(in PacketReader p)
    {
        var value = new GetIsOfferGiftable(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(GetIsOfferGiftable));
        return value;
    }

    public static T ParseEmpty<T>(in PacketReader p, Func<T> factory)
    {
        CatalogWire.RequireEmpty(in p, typeof(T).Name);
        return factory();
    }

    public static void WriteSelection(NuxGiftSelection value, in PacketWriter p)
    {
        p.WriteInt(value.DayIndex);
        p.WriteInt(value.StepIndex);
        p.WriteInt(value.GiftIndex);
    }

    public static T UnsupportedUnity<T>(ClientType client) =>
        throw new UnsupportedClientException(client);

    public static void UnsupportedUnity(ClientType client) =>
        throw new UnsupportedClientException(client);

    private static int[] ReadIntValues(in PacketReader p, int trailing_bytes, string name)
    {
        int count = CatalogWire.ReadCount(
            in p,
            sizeof(int),
            trailing_bytes,
            MaximumCollectionCount,
            name);
        var values = new int[count];
        for (int index = 0; index < values.Length; index++)
            values[index] = p.ReadInt();
        return values;
    }

    private static int[] SnapshotValues(IReadOnlyList<int> values, string name) =>
        CatalogWire.SnapshotValues(values, MaximumCollectionCount, name);

    private static void WriteIntValues(IReadOnlyList<int> values, in PacketWriter p)
    {
        CatalogWire.WriteCount(values.Count, in p);
        foreach (int value in values)
            p.WriteInt(value);
    }

    private static void PrepareEligibility(ClubGiftEligibility value, bool flash)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash && value.IsVip is null)
            throw new InvalidDataException("Flash club gift eligibility requires the VIP flag.");
        if (!flash && value.IsVip is not null)
            throw new InvalidDataException("Unity club gift eligibility cannot represent the Flash VIP flag.");
    }

    private static void WriteEligibility(
        ClubGiftEligibility value,
        bool flash,
        in PacketWriter p)
    {
        p.WriteInt(value.OfferId);
        if (flash)
            p.WriteBool(value.IsVip!.Value);
        p.WriteInt(value.DaysRequired);
        p.WriteBool(value.IsSelectable);
    }

    private static NuxGiftStep ParseNuxStep(
        in PacketReader p,
        int trailing_bytes,
        ref GiftBudget budget,
        ref CatalogStringBudget strings)
    {
        int day_index = p.ReadInt();
        int step_index = p.ReadInt();
        int option_count = CatalogWire.ReadCount(
            in p,
            NuxOptionMinimumBytes,
            trailing_bytes,
            MaximumNuxOptions,
            nameof(NuxGiftStep.Options));
        budget.TakeOptions(option_count);
        var options = new NuxGiftOption[option_count];
        for (int index = 0; index < options.Length; index++)
        {
            int sibling_bytes = checked((options.Length - index - 1) * NuxOptionMinimumBytes);
            options[index] = ParseNuxOption(
                in p,
                checked(trailing_bytes + sibling_bytes),
                ref budget,
                ref strings);
        }
        return new NuxGiftStep(day_index, step_index, options);
    }

    private static NuxGiftOption ParseNuxOption(
        in PacketReader p,
        int trailing_bytes,
        ref GiftBudget budget,
        ref CatalogStringBudget strings)
    {
        int count_width = CatalogWire.CountWidth(p.Client);
        string thumbnail = strings.Read(
            in p,
            nameof(NuxGiftOption.ThumbnailUrl),
            checked(trailing_bytes + count_width));
        int product_count = CatalogWire.ReadCount(
            in p,
            NuxProductMinimumBytes,
            trailing_bytes,
            MaximumNuxProducts,
            nameof(NuxGiftOption.Products));
        budget.TakeProducts(product_count);
        var products = new NuxGiftProduct[product_count];
        for (int index = 0; index < products.Length; index++)
        {
            int sibling_bytes = checked((products.Length - index - 1) * NuxProductMinimumBytes);
            products[index] = ParseNuxProduct(
                in p,
                checked(trailing_bytes + sibling_bytes),
                ref strings);
        }
        return new NuxGiftOption(thumbnail.Length == 0 ? null : thumbnail, products);
    }

    private static NuxGiftProduct ParseNuxProduct(
        in PacketReader p,
        int trailing_bytes,
        ref CatalogStringBudget strings)
    {
        string product_code = strings.Read(
            in p,
            nameof(NuxGiftProduct.ProductCode),
            checked(trailing_bytes + CatalogWire.StringMinimumBytes));
        string localization_key = strings.Read(
            in p,
            nameof(NuxGiftProduct.LocalizationKey),
            trailing_bytes);
        return new NuxGiftProduct(
            product_code,
            localization_key.Length == 0 ? null : localization_key);
    }

    private static void PrepareNuxStep(
        NuxGiftStep value,
        ref GiftBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int option_count = CatalogWire.RequireListCount(
            value.Options,
            MaximumNuxOptions,
            nameof(value.Options));
        budget.TakeOptions(option_count);
        foreach (NuxGiftOption option in value.Options)
            PrepareNuxOption(option, ref budget, ref strings, in p);
    }

    private static void PrepareNuxOption(
        NuxGiftOption value,
        ref GiftBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ThumbnailUrl ?? "", nameof(value.ThumbnailUrl), in p);
        int product_count = CatalogWire.RequireListCount(
            value.Products,
            MaximumNuxProducts,
            nameof(value.Products));
        budget.TakeProducts(product_count);
        foreach (NuxGiftProduct product in value.Products)
            PrepareNuxProduct(product, ref strings, in p);
    }

    private static void PrepareNuxProduct(
        NuxGiftProduct value,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.ProductCode, nameof(value.ProductCode), in p);
        strings.Require(value.LocalizationKey ?? "", nameof(value.LocalizationKey), in p);
    }

    private static void WriteNuxStep(NuxGiftStep value, in PacketWriter p)
    {
        p.WriteInt(value.DayIndex);
        p.WriteInt(value.StepIndex);
        CatalogWire.WriteCount(value.Options.Count, in p);
        foreach (NuxGiftOption option in value.Options)
            WriteNuxOption(option, in p);
    }

    private static void WriteNuxOption(NuxGiftOption value, in PacketWriter p)
    {
        p.WriteString(value.ThumbnailUrl ?? "");
        CatalogWire.WriteCount(value.Products.Count, in p);
        foreach (NuxGiftProduct product in value.Products)
            WriteNuxProduct(product, in p);
    }

    private static void WriteNuxProduct(NuxGiftProduct value, in PacketWriter p)
    {
        p.WriteString(value.ProductCode);
        p.WriteString(value.LocalizationKey ?? "");
    }

    private static CatalogStringBudget NewStringBudget() =>
        CatalogPageWire.NewStringBudget();

    private static Id ReadFlashId(in PacketReader p) => p.ReadInt();

    private static Id ReadUnityId(in PacketReader p) => p.ReadLong();

    private static void RequireFlashId(Id value)
    {
        long id = value;
        if (id is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException(
                "Flash cannot represent a gift furni identifier outside the signed 32-bit range.");
    }

    private static void WriteFlashId(in PacketWriter p, Id value) => p.WriteInt((int)(long)value);

    private static void WriteUnityId(in PacketWriter p, Id value) => p.WriteLong(value);
}

internal struct GiftBudget
{
    private int _steps;
    private int _options;
    private int _products;

    public void TakeSteps(int count) =>
        Take(ref _steps, count, GiftWire.MaximumNuxSteps, "NUX gift steps");

    public void TakeOptions(int count) =>
        Take(ref _options, count, GiftWire.MaximumNuxOptions, "NUX gift options");

    public void TakeProducts(int count) =>
        Take(ref _products, count, GiftWire.MaximumNuxProducts, "NUX gift products");

    private static void Take(ref int current, int count, int maximum, string name)
    {
        CatalogWire.RequireCount(count, maximum, name);
        if (count > maximum - current)
            throw new InvalidDataException($"{name} exceed the global limit {maximum}.");
        current += count;
    }
}
