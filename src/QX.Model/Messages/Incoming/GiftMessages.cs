using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents the <c>GiftWrappingConfiguration</c> message, received with the gift wrapping options.
/// </summary>
public sealed record GiftWrappingConfiguration : IParserComposer<GiftWrappingConfiguration>
{
    private IReadOnlyList<int> _stuff_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _box_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _ribbon_types = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<int> _default_stuff_types = Array.AsReadOnly(Array.Empty<int>());

    /// <summary>Initializes a new instance of the <see cref="GiftWrappingConfiguration"/> record.</summary>
    /// <param name="isWrappingEnabled">Whether gift wrapping is enabled.</param>
    /// <param name="wrappingPrice">The price of gift wrapping.</param>
    /// <param name="stuffTypes">The gift furni sprite identifiers offered as wrapping.</param>
    /// <param name="boxTypes">The box types offered for wrapping.</param>
    /// <param name="ribbonTypes">The ribbon types offered for wrapping.</param>
    /// <param name="defaultStuffTypes">The default gift furni sprite identifiers.</param>
    public GiftWrappingConfiguration(
        bool isWrappingEnabled,
        int wrappingPrice,
        IReadOnlyList<int> stuffTypes,
        IReadOnlyList<int> boxTypes,
        IReadOnlyList<int> ribbonTypes,
        IReadOnlyList<int> defaultStuffTypes)
    {
        IsWrappingEnabled = isWrappingEnabled;
        WrappingPrice = wrappingPrice;
        StuffTypes = stuffTypes;
        BoxTypes = boxTypes;
        RibbonTypes = ribbonTypes;
        DefaultStuffTypes = defaultStuffTypes;
    }

    /// <summary>Gets whether gift wrapping is enabled.</summary>
    public bool IsWrappingEnabled { get; init; }

    /// <summary>Gets the price of gift wrapping.</summary>
    public int WrappingPrice { get; init; }

    /// <summary>
    /// Gets the gift furni sprite identifiers offered as wrapping, as a read only copy.
    /// </summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<int> StuffTypes
    {
        get => _stuff_types;
        init => _stuff_types = GiftWire.FreezeValues(value, nameof(StuffTypes));
    }

    /// <summary>Gets the box types offered for wrapping, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<int> BoxTypes
    {
        get => _box_types;
        init => _box_types = GiftWire.FreezeValues(value, nameof(BoxTypes));
    }

    /// <summary>Gets the ribbon types offered for wrapping, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<int> RibbonTypes
    {
        get => _ribbon_types;
        init => _ribbon_types = GiftWire.FreezeValues(value, nameof(RibbonTypes));
    }

    /// <summary>Gets the default gift furni sprite identifiers, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<int> DefaultStuffTypes
    {
        get => _default_stuff_types;
        init => _default_stuff_types = GiftWire.FreezeValues(value, nameof(DefaultStuffTypes));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GiftWrappingConfiguration Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GiftWrappingConfiguration ParseFlash(in PacketReader p) =>
        GiftWire.ParseWrappingConfiguration(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GiftWrappingConfiguration value, in PacketWriter p) =>
        GiftWire.ComposeWrappingConfiguration(value, in p);

    /// <summary>Deconstructs the configuration into its values.</summary>
    /// <param name="isWrappingEnabled">Whether gift wrapping is enabled.</param>
    /// <param name="wrappingPrice">The price of gift wrapping.</param>
    /// <param name="stuffTypes">The gift furni sprite identifiers offered as wrapping.</param>
    /// <param name="boxTypes">The box types offered for wrapping.</param>
    /// <param name="ribbonTypes">The ribbon types offered for wrapping.</param>
    /// <param name="defaultStuffTypes">The default gift furni sprite identifiers.</param>
    public void Deconstruct(
        out bool isWrappingEnabled,
        out int wrappingPrice,
        out IReadOnlyList<int> stuffTypes,
        out IReadOnlyList<int> boxTypes,
        out IReadOnlyList<int> ribbonTypes,
        out IReadOnlyList<int> defaultStuffTypes)
    {
        isWrappingEnabled = IsWrappingEnabled;
        wrappingPrice = WrappingPrice;
        stuffTypes = StuffTypes;
        boxTypes = BoxTypes;
        ribbonTypes = RibbonTypes;
        defaultStuffTypes = DefaultStuffTypes;
    }
}

/// <summary>
/// Represents the <c>PresentOpened</c> message, received when a present has been opened and its contents
/// are revealed.
/// </summary>
/// <param name="ItemType">The type of the item inside the present.</param>
/// <param name="ClassId">The class identifier of the item inside the present.</param>
/// <param name="ProductCode">The product code of the present's contents.</param>
/// <param name="PlacedItemId">The identifier of the item that was placed, sent by Flash as a 32 bit integer.</param>
/// <param name="PlacedItemType">The type of the item that was placed.</param>
/// <param name="PlacedInRoom">Whether the item was placed straight into the room.</param>
/// <param name="PetFigureString">The pet figure when the present held a pet.</param>
public sealed record PresentOpened(
    string ItemType,
    int ClassId,
    string ProductCode,
    Id PlacedItemId,
    string PlacedItemType,
    bool PlacedInRoom,
    string PetFigureString) : IParserComposer<PresentOpened>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PresentOpened Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PresentOpened ParseFlash(in PacketReader p) =>
        GiftWire.ParsePresentOpened(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PresentOpened value, in PacketWriter p) =>
        GiftWire.ComposePresentOpened(value, in p);
}

/// <summary>Represents the eligibility of a club gift offer.</summary>
/// <param name="OfferId">The identifier of the catalog offer the entry applies to.</param>
/// <param name="IsVip">Whether the gift is a VIP club gift.</param>
/// <param name="DaysRequired">The number of club days the gift requires.</param>
/// <param name="IsSelectable">Whether the gift can be selected.</param>
public sealed record ClubGiftEligibility(
    int OfferId,
    bool IsVip,
    int DaysRequired,
    bool IsSelectable) : IParserComposer<ClubGiftEligibility>
{
    /// <summary>Parses a club gift eligibility entry from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ClubGiftEligibility Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClubGiftEligibility ParseFlash(in PacketReader p) =>
        GiftWire.ParseEligibility(in p);

    /// <summary>Composes the club gift eligibility entry into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClubGiftEligibility value, in PacketWriter p) =>
        GiftWire.ComposeEligibility(value, in p);
}

/// <summary>Represents the <c>ClubGiftInfo</c> message, received with the club gifts that can be selected.</summary>
public sealed record ClubGiftInfo : IParserComposer<ClubGiftInfo>
{
    private IReadOnlyList<CatalogPageOffer> _offers = Array.AsReadOnly(Array.Empty<CatalogPageOffer>());
    private IReadOnlyList<ClubGiftEligibility> _gift_eligibility =
        Array.AsReadOnly(Array.Empty<ClubGiftEligibility>());

    /// <summary>Initializes a new instance of the <see cref="ClubGiftInfo"/> record.</summary>
    /// <param name="daysUntilNextGift">The number of days until the next club gift.</param>
    /// <param name="giftsAvailable">The number of club gifts that can be selected now.</param>
    /// <param name="offers">The catalog offers that can be chosen as a club gift.</param>
    /// <param name="giftEligibility">The eligibility of each club gift offer.</param>
    public ClubGiftInfo(
        int daysUntilNextGift,
        int giftsAvailable,
        IReadOnlyList<CatalogPageOffer> offers,
        IReadOnlyList<ClubGiftEligibility> giftEligibility)
    {
        DaysUntilNextGift = daysUntilNextGift;
        GiftsAvailable = giftsAvailable;
        Offers = offers;
        GiftEligibility = giftEligibility;
    }

    /// <summary>Gets the number of days until the next club gift.</summary>
    public int DaysUntilNextGift { get; init; }

    /// <summary>Gets the number of club gifts that can be selected now.</summary>
    public int GiftsAvailable { get; init; }

    /// <summary>
    /// Gets the catalog offers that can be chosen as a club gift, as a read only copy.
    /// </summary>
    /// <remarks>The list may hold at most 4096 entries.</remarks>
    public IReadOnlyList<CatalogPageOffer> Offers
    {
        get => _offers;
        init => _offers = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumOffers,
            nameof(Offers));
    }

    /// <summary>Gets the eligibility of each club gift offer, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<ClubGiftEligibility> GiftEligibility
    {
        get => _gift_eligibility;
        init => _gift_eligibility = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumCollectionCount,
            nameof(GiftEligibility));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ClubGiftInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClubGiftInfo ParseFlash(in PacketReader p) =>
        GiftWire.ParseClubGiftInfo(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClubGiftInfo value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftInfo(value, in p);

    /// <summary>Deconstructs the club gift information into its values.</summary>
    /// <param name="daysUntilNextGift">The number of days until the next club gift.</param>
    /// <param name="giftsAvailable">The number of club gifts that can be selected now.</param>
    /// <param name="offers">The catalog offers that can be chosen as a club gift.</param>
    /// <param name="giftEligibility">The eligibility of each club gift offer.</param>
    public void Deconstruct(
        out int daysUntilNextGift,
        out int giftsAvailable,
        out IReadOnlyList<CatalogPageOffer> offers,
        out IReadOnlyList<ClubGiftEligibility> giftEligibility)
    {
        daysUntilNextGift = DaysUntilNextGift;
        giftsAvailable = GiftsAvailable;
        offers = Offers;
        giftEligibility = GiftEligibility;
    }
}

/// <summary>
/// Represents the <c>ClubGiftSelected</c> message, received when the server confirms a selected club gift.
/// </summary>
public sealed record ClubGiftSelected : IParserComposer<ClubGiftSelected>
{
    private string _product_code = "";
    private IReadOnlyList<CatalogProduct> _products = Array.AsReadOnly(Array.Empty<CatalogProduct>());

    /// <summary>Initializes a new instance of the <see cref="ClubGiftSelected"/> record.</summary>
    /// <param name="productCode">The product code of the selected club gift.</param>
    /// <param name="products">The products the club gift granted.</param>
    public ClubGiftSelected(
        string productCode,
        IReadOnlyList<CatalogProduct> products)
    {
        ProductCode = productCode;
        Products = products;
    }

    /// <summary>Gets the product code of the selected club gift.</summary>
    public string ProductCode
    {
        get => _product_code;
        init => _product_code = CatalogWire.RequireReference(value, nameof(ProductCode));
    }

    /// <summary>Gets the products the club gift granted, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<CatalogProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            CatalogPageWire.MaximumProducts,
            nameof(Products));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ClubGiftSelected Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClubGiftSelected ParseFlash(in PacketReader p) =>
        GiftWire.ParseClubGiftSelected(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClubGiftSelected value, in PacketWriter p) =>
        GiftWire.ComposeClubGiftSelected(value, in p);

    /// <summary>Deconstructs the selection into its product code and products.</summary>
    /// <param name="productCode">The product code of the selected club gift.</param>
    /// <param name="products">The products the club gift granted.</param>
    public void Deconstruct(
        out string productCode,
        out IReadOnlyList<CatalogProduct> products)
    {
        productCode = ProductCode;
        products = Products;
    }
}

/// <summary>
/// Represents the <c>GiftReceiverNotFound</c> message, received when the receiver of a gift purchase does not exist.
/// </summary>
public sealed record GiftReceiverNotFound : IParserComposer<GiftReceiverNotFound>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GiftReceiverNotFound Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GiftReceiverNotFound ParseFlash(in PacketReader p)
    {
        CatalogWire.RequireEmpty(in p, nameof(GiftReceiverNotFound));
        return new GiftReceiverNotFound();
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GiftReceiverNotFound value, in PacketWriter p) { }
}

/// <summary>Represents the <c>ClubGiftNotification</c> message, received when club gifts can be selected.</summary>
/// <param name="NumGifts">The number of club gifts that can be selected.</param>
public sealed record ClubGiftNotification(int NumGifts) : IParserComposer<ClubGiftNotification>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ClubGiftNotification Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ClubGiftNotification ParseFlash(in PacketReader p)
    {
        var value = new ClubGiftNotification(p.ReadInt());
        CatalogWire.RequireEmpty(in p, nameof(ClubGiftNotification));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ClubGiftNotification value, in PacketWriter p) =>
        p.WriteInt(value.NumGifts);
}

/// <summary>
/// Represents the <c>IsOfferGiftable</c> message, received with whether a catalog offer can be sent as a gift.
/// </summary>
/// <param name="OfferId">The identifier of the catalog offer.</param>
/// <param name="IsGiftable">Whether the offer can be sent as a gift.</param>
public sealed record IsOfferGiftable(
    int OfferId,
    bool IsGiftable) : IParserComposer<IsOfferGiftable>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static IsOfferGiftable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IsOfferGiftable ParseFlash(in PacketReader p)
    {
        var value = new IsOfferGiftable(p.ReadInt(), p.ReadBool());
        CatalogWire.RequireEmpty(in p, nameof(IsOfferGiftable));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IsOfferGiftable value, in PacketWriter p)
    {
        p.WriteInt(value.OfferId);
        p.WriteBool(value.IsGiftable);
    }
}

/// <summary>Represents a product inside a new user gift option.</summary>
/// <param name="ProductCode">The product code.</param>
/// <param name="LocalizationKey">
/// The localization key of the product, or <see langword="null"/> when the server sends an empty string.
/// </param>
public sealed record NuxGiftProduct(
    string ProductCode,
    string? LocalizationKey) : IParserComposer<NuxGiftProduct>
{
    /// <summary>Parses a new user gift product from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGiftProduct Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGiftProduct ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxProduct(in p);

    /// <summary>Composes the new user gift product into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGiftProduct value, in PacketWriter p) =>
        GiftWire.ComposeNuxProduct(value, in p);
}

/// <summary>Represents one choice in a step of the new user gift offer.</summary>
public sealed record NuxGiftOption : IParserComposer<NuxGiftOption>
{
    private IReadOnlyList<NuxGiftProduct> _products = Array.AsReadOnly(Array.Empty<NuxGiftProduct>());

    /// <summary>Initializes a new instance of the <see cref="NuxGiftOption"/> record.</summary>
    /// <param name="thumbnailUrl">The thumbnail image URL, or <see langword="null"/> when there is none.</param>
    /// <param name="products">The products the option grants.</param>
    public NuxGiftOption(string? thumbnailUrl, IReadOnlyList<NuxGiftProduct> products)
    {
        ThumbnailUrl = thumbnailUrl;
        Products = products;
    }

    /// <summary>
    /// Gets the thumbnail image URL, or <see langword="null"/> when the server sends an empty string.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Gets the products the option grants, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<NuxGiftProduct> Products
    {
        get => _products;
        init => _products = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxProducts,
            nameof(Products));
    }

    /// <summary>Parses a new user gift option from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGiftOption Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGiftOption ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxOption(in p);

    /// <summary>Composes the new user gift option into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGiftOption value, in PacketWriter p) =>
        GiftWire.ComposeNuxOption(value, in p);

    /// <summary>Deconstructs the option into its thumbnail and products.</summary>
    /// <param name="thumbnailUrl">The thumbnail image URL, or <see langword="null"/> when there is none.</param>
    /// <param name="products">The products the option grants.</param>
    public void Deconstruct(out string? thumbnailUrl, out IReadOnlyList<NuxGiftProduct> products)
    {
        thumbnailUrl = ThumbnailUrl;
        products = Products;
    }
}

/// <summary>Represents one step of the new user gift offer, with the options to choose from.</summary>
public sealed record NuxGiftStep : IParserComposer<NuxGiftStep>
{
    private IReadOnlyList<NuxGiftOption> _options = Array.AsReadOnly(Array.Empty<NuxGiftOption>());

    /// <summary>Initializes a new instance of the <see cref="NuxGiftStep"/> record.</summary>
    /// <param name="dayIndex">The day index of the step.</param>
    /// <param name="stepIndex">The step index.</param>
    /// <param name="options">The options to choose from.</param>
    public NuxGiftStep(int dayIndex, int stepIndex, IReadOnlyList<NuxGiftOption> options)
    {
        DayIndex = dayIndex;
        StepIndex = stepIndex;
        Options = options;
    }

    /// <summary>Gets the day index of the step.</summary>
    public int DayIndex { get; init; }

    /// <summary>Gets the step index.</summary>
    public int StepIndex { get; init; }

    /// <summary>Gets the options to choose from, as a read only copy.</summary>
    /// <remarks>The list may hold at most 65535 entries.</remarks>
    public IReadOnlyList<NuxGiftOption> Options
    {
        get => _options;
        init => _options = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxOptions,
            nameof(Options));
    }

    /// <summary>Parses a new user gift step from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGiftStep Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGiftStep ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxStep(in p);

    /// <summary>Composes the new user gift step into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGiftStep value, in PacketWriter p) =>
        GiftWire.ComposeNuxStep(value, in p);

    /// <summary>Deconstructs the step into its indexes and options.</summary>
    /// <param name="dayIndex">The day index of the step.</param>
    /// <param name="stepIndex">The step index.</param>
    /// <param name="options">The options to choose from.</param>
    public void Deconstruct(
        out int dayIndex,
        out int stepIndex,
        out IReadOnlyList<NuxGiftOption> options)
    {
        dayIndex = DayIndex;
        stepIndex = StepIndex;
        options = Options;
    }
}

/// <summary>
/// Represents the <c>NewUserExperienceGiftOffer</c> message, received with the gifts offered to a new user.
/// </summary>
public sealed record NuxGiftOffer : IParserComposer<NuxGiftOffer>
{
    private IReadOnlyList<NuxGiftStep> _steps = Array.AsReadOnly(Array.Empty<NuxGiftStep>());

    /// <summary>Initializes a new instance of the <see cref="NuxGiftOffer"/> record.</summary>
    /// <param name="steps">The steps of the offer.</param>
    public NuxGiftOffer(IReadOnlyList<NuxGiftStep> steps) => Steps = steps;

    /// <summary>Gets the steps of the offer, as a read only copy.</summary>
    /// <remarks>The list may hold at most 4096 entries.</remarks>
    public IReadOnlyList<NuxGiftStep> Steps
    {
        get => _steps;
        init => _steps = CatalogWire.FreezeReferences(
            value,
            GiftWire.MaximumNuxSteps,
            nameof(Steps));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGiftOffer Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGiftOffer ParseFlash(in PacketReader p) =>
        GiftWire.ParseNuxOffer(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGiftOffer value, in PacketWriter p) =>
        GiftWire.ComposeNuxOffer(value, in p);

    /// <summary>Deconstructs the offer into its steps.</summary>
    /// <param name="steps">The steps of the offer.</param>
    public void Deconstruct(out IReadOnlyList<NuxGiftStep> steps) => steps = Steps;
}

/// <summary>
/// Represents the <c>NewUserExperienceNotComplete</c> message, received when the account has not finished
/// the new user flow.
/// </summary>
public sealed record NuxNotComplete : IParserComposer<NuxNotComplete>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxNotComplete Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxNotComplete ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<NuxNotComplete>(in p, static () => new NuxNotComplete());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxNotComplete value, in PacketWriter p) { }
}
