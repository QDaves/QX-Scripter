using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>Represents the gift chosen at one step of the new user gift offer.</summary>
/// <param name="DayIndex">The day index of the step.</param>
/// <param name="StepIndex">The step index.</param>
/// <param name="GiftIndex">The zero based index of the chosen option within the step.</param>
public readonly record struct NuxGiftSelection(
    int DayIndex,
    int StepIndex,
    int GiftIndex) : IParserComposer<NuxGiftSelection>
{
    /// <summary>Parses a new user gift selection from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGiftSelection Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGiftSelection ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the new user gift selection into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGiftSelection value, in PacketWriter p) =>
        GiftWire.WriteSelection(value, in p);
}

/// <summary>
/// Represents the <c>NewUserExperienceGetGifts</c> message, sent to claim gifts from the new user gift offer.
/// </summary>
/// <remarks>
/// The wire count covers every value, three per selection, so parsing fails when it is not a multiple of
/// three.
/// </remarks>
public sealed record NuxGetGifts : IParserComposer<NuxGetGifts>
{
    private IReadOnlyList<NuxGiftSelection> _selections =
        Array.AsReadOnly(Array.Empty<NuxGiftSelection>());

    /// <summary>Initializes a new instance of the <see cref="NuxGetGifts"/> record.</summary>
    /// <param name="selections">The chosen gifts, one per step.</param>
    public NuxGetGifts(IReadOnlyList<NuxGiftSelection> selections) => Selections = selections;

    /// <summary>Gets the chosen gifts, as a read only copy.</summary>
    /// <remarks>The list may hold at most 21845 entries.</remarks>
    public IReadOnlyList<NuxGiftSelection> Selections
    {
        get => _selections;
        init => _selections = CatalogWire.FreezeValues(
            value,
            GiftWire.MaximumNuxSelections,
            nameof(Selections));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NuxGetGifts Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NuxGetGifts ParseFlash(in PacketReader p) => GiftWire.ParseNuxGetGifts(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NuxGetGifts value, in PacketWriter p) =>
        GiftWire.ComposeNuxGetGifts(value, in p);

    /// <summary>Deconstructs the request into its selections.</summary>
    /// <param name="selections">The chosen gifts, one per step.</param>
    public void Deconstruct(out IReadOnlyList<NuxGiftSelection> selections) =>
        selections = Selections;
}

/// <summary>Represents the <c>PresentOpen</c> message, sent to open a present in the room.</summary>
/// <param name="FurniId">
/// The room item identifier of the present. Flash sends it as a 32 bit integer, and composing throws
/// when it does not fit.
/// </param>
public sealed record PresentOpen(Id FurniId) : IParserComposer<PresentOpen>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PresentOpen Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PresentOpen ParseFlash(in PacketReader p) =>
        GiftWire.ParsePresentOpen(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PresentOpen value, in PacketWriter p) =>
        GiftWire.ComposePresentOpen(value, in p);
}

/// <summary>
/// Represents the <c>PurchaseFromCatalogAsGift</c> message, sent to buy a catalog offer as a gift for another user.
/// </summary>
/// <param name="PageId">The catalog page identifier.</param>
/// <param name="OfferId">The catalog offer identifier.</param>
/// <param name="ExtraData">The extra data the offer expects, sent unchanged.</param>
/// <param name="ReceiverName">The name of the user who receives the gift.</param>
/// <param name="GiftMessage">The message attached to the gift.</param>
/// <param name="SpriteId">The sprite identifier of the gift box furni.</param>
/// <param name="BoxType">The gift box type, or 0 with the default box.</param>
/// <param name="RibbonType">The ribbon type, or 0 with the default box.</param>
/// <param name="ShowPurchaserName">Whether the receiver sees who sent the gift.</param>
public sealed record PurchaseFromCatalogAsGift(
    int PageId,
    int OfferId,
    string ExtraData,
    string ReceiverName,
    string GiftMessage,
    int SpriteId,
    int BoxType,
    int RibbonType,
    bool ShowPurchaserName) : IParserComposer<PurchaseFromCatalogAsGift>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PurchaseFromCatalogAsGift Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PurchaseFromCatalogAsGift ParseFlash(in PacketReader p) =>
        GiftWire.ParsePurchase(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PurchaseFromCatalogAsGift value, in PacketWriter p) =>
        GiftWire.ComposePurchase(value, in p);
}

/// <summary>
/// Represents the <c>GetGiftWrappingConfiguration</c> message, sent to request the gift wrapping options.
/// </summary>
public sealed record GetGiftWrappingConfiguration : IParserComposer<GetGiftWrappingConfiguration>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetGiftWrappingConfiguration Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetGiftWrappingConfiguration ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<GetGiftWrappingConfiguration>(
            in p,
            static () => new GetGiftWrappingConfiguration());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetGiftWrappingConfiguration value, in PacketWriter p) { }
}

/// <summary>Represents the <c>GetClubGift</c> message, sent to request the club gifts that can be selected.</summary>
public sealed record GetClubGift : IParserComposer<GetClubGift>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetClubGift Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetClubGift ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<GetClubGift>(in p, static () => new GetClubGift());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetClubGift value, in PacketWriter p) { }
}

/// <summary>Represents the <c>SelectClubGift</c> message, sent to select a club gift.</summary>
/// <param name="ProductCode">The product code of the club gift to select.</param>
public sealed record SelectClubGift(string ProductCode) : IParserComposer<SelectClubGift>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SelectClubGift Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SelectClubGift ParseFlash(in PacketReader p) => GiftWire.ParseSelectClubGift(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SelectClubGift value, in PacketWriter p) =>
        GiftWire.ComposeSelectClubGift(value, in p);
}

/// <summary>
/// Represents the <c>GetIsOfferGiftable</c> message, sent to request whether a catalog offer can be sent as a gift.
/// </summary>
/// <param name="OfferId">The identifier of the catalog offer.</param>
public sealed record GetIsOfferGiftable(int OfferId) : IParserComposer<GetIsOfferGiftable>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetIsOfferGiftable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static GetIsOfferGiftable ParseFlash(in PacketReader p) =>
        GiftWire.ParseOfferGiftabilityRequest(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(GetIsOfferGiftable value, in PacketWriter p) =>
        p.WriteInt(value.OfferId);
}

/// <summary>
/// Represents the <c>NewUserExperienceScriptProceed</c> message, sent to advance the new user flow to its next step.
/// </summary>
public sealed record AdvanceNewUserFlowRequest : IParserComposer<AdvanceNewUserFlowRequest>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AdvanceNewUserFlowRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AdvanceNewUserFlowRequest ParseFlash(in PacketReader p) =>
        GiftWire.ParseEmpty<AdvanceNewUserFlowRequest>(
            in p,
            static () => new AdvanceNewUserFlowRequest());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AdvanceNewUserFlowRequest value, in PacketWriter p) { }
}
