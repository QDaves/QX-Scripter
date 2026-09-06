using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record GiftStateRequest;

public sealed record GiftWrappingSummaryView(
    bool IsWrappingEnabled,
    int WrappingPrice,
    int StuffTypeCount,
    int BoxTypeCount,
    int RibbonTypeCount,
    int DefaultStuffTypeCount);

public sealed record GiftClubInfoSummaryView(
    int DaysUntilNextGift,
    int GiftsAvailable,
    int OfferCount,
    int EligibilityCount,
    int ProductCount,
    int UnityProductReferenceCount,
    int UnityProductCount);

public sealed record GiftClubSelectedSummaryView(
    string ProductCode,
    int ProductCount,
    int UnityProductCount);

public sealed record GiftNewUserOfferSummaryView(
    int StepCount,
    int OptionCount,
    int ProductCount);

public sealed record GiftStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long WrappingRevision,
    long ClubInfoRevision,
    long ClubSelectedRevision,
    long PresentOpenedRevision,
    long ReceiverNotFoundRevision,
    long ClubNotificationRevision,
    long OfferGiftabilityRevision,
    long NewUserOfferRevision,
    long NewUserIncompleteRevision,
    GiftWrappingSummaryView? Wrapping,
    GiftClubInfoSummaryView? ClubInfo,
    GiftClubSelectedSummaryView? LastClubSelected,
    PresentOpened? LastOpenedPresent,
    ClubGiftNotification? LatestNotification,
    GiftNewUserOfferSummaryView? NewUserOffer,
    bool NewUserFlowIsIncomplete,
    IReadOnlyDictionary<int, bool> OfferGiftability);

public enum GiftWrappingCollection
{
    StuffTypes,
    BoxTypes,
    RibbonTypes,
    DefaultStuffTypes
}

public sealed record GiftWrappingPageRequest(
    GiftWrappingCollection Collection = GiftWrappingCollection.StuffTypes,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record GiftWrappingPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long WrappingRevision,
    long SnapshotRevision,
    bool Loaded,
    bool? IsWrappingEnabled,
    int? WrappingPrice,
    GiftWrappingCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<int> Values);

public enum GiftClubInfoCollection
{
    Offers,
    Eligibility,
    Products,
    UnityProductReferences,
    UnityProducts
}

public sealed record GiftClubInfoPageRequest(
    GiftClubInfoCollection Collection = GiftClubInfoCollection.Offers,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record GiftClubOfferView(
    int OfferOrdinal,
    int OfferId,
    string LocalizationId,
    bool IsRent,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    int PriceInSilver,
    bool Giftable,
    int ClubLevel,
    bool BundlePurchaseAllowed,
    bool IsPet,
    string PreviewImage,
    int ProductCount,
    int UnityProductReferenceCount,
    int UnityProductCount);

public sealed record GiftClubEligibilityView(
    int EligibilityOrdinal,
    int OfferId,
    bool? IsVip,
    int DaysRequired,
    bool IsSelectable);

public sealed record GiftClubProductView(
    int OfferOrdinal,
    int ProductOrdinal,
    CatalogProduct Product);

public sealed record GiftClubUnityProductReferenceView(
    int OfferOrdinal,
    int ReferenceOrdinal,
    CatalogPageProductReference ProductReference);

public sealed record GiftClubUnityProductView(
    int OfferOrdinal,
    int ProductOrdinal,
    CatalogPageProduct Product);

public sealed record GiftClubInfoPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long ClubInfoRevision,
    long SnapshotRevision,
    bool Loaded,
    int? DaysUntilNextGift,
    int? GiftsAvailable,
    int TotalOffers,
    int TotalEligibility,
    int TotalProducts,
    int TotalUnityProductReferences,
    int TotalUnityProducts,
    GiftClubInfoCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GiftClubOfferView> Offers,
    IReadOnlyList<GiftClubEligibilityView> Eligibility,
    IReadOnlyList<GiftClubProductView> Products,
    IReadOnlyList<GiftClubUnityProductReferenceView> UnityProductReferences,
    IReadOnlyList<GiftClubUnityProductView> UnityProducts);

public enum GiftClubSelectedCollection
{
    Products,
    UnityProducts
}

public sealed record GiftClubSelectedPageRequest(
    GiftClubSelectedCollection Collection = GiftClubSelectedCollection.Products,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record GiftClubSelectedPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long ClubSelectedRevision,
    long SnapshotRevision,
    bool Loaded,
    string? ProductCode,
    int TotalProducts,
    int TotalUnityProducts,
    GiftClubSelectedCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CatalogProduct> Products,
    IReadOnlyList<CatalogPageProduct> UnityProducts);

public enum GiftNewUserOfferCollection
{
    Steps,
    Options,
    Products
}

public sealed record GiftNewUserOfferPageRequest(
    GiftNewUserOfferCollection Collection = GiftNewUserOfferCollection.Steps,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record GiftNewUserStepView(
    int StepOrdinal,
    int DayIndex,
    int StepIndex,
    int OptionCount);

public sealed record GiftNewUserOptionView(
    int StepOrdinal,
    int OptionOrdinal,
    string? ThumbnailUrl,
    int ProductCount);

public sealed record GiftNewUserProductView(
    int StepOrdinal,
    int OptionOrdinal,
    int ProductOrdinal,
    string ProductCode,
    string? LocalizationKey);

public sealed record GiftNewUserOfferPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long NewUserOfferRevision,
    long SnapshotRevision,
    bool Loaded,
    int TotalSteps,
    int TotalOptions,
    int TotalProducts,
    GiftNewUserOfferCollection Collection,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<GiftNewUserStepView> Steps,
    IReadOnlyList<GiftNewUserOptionView> Options,
    IReadOnlyList<GiftNewUserProductView> Products);

public sealed record GiftRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record GiftRefreshResult(
    ClientType Client,
    long SessionGeneration,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset WrappingObservedAtUtc,
    DateTimeOffset ClubInfoObservedAtUtc,
    long SnapshotRevision,
    long WrappingRevision,
    long ClubInfoRevision,
    GiftWrappingSummaryView Wrapping,
    GiftClubInfoSummaryView ClubInfo,
    GiftClubInfoPage ClubInfoPage);

public sealed record GiftPresentOpenRequest(
    Id FurniId,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record GiftPresentOpenDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    Id FurniId,
    int MessagesDispatched);

public sealed record GiftPurchaseRequest(
    int PageId,
    int OfferId,
    string ExtraData,
    string ReceiverName,
    string GiftMessage,
    int SpriteId,
    int BoxType,
    int RibbonType,
    bool ShowPurchaserName,
    int Quantity = 1,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record GiftPurchaseDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long CatalogGeneration,
    int PageId,
    int OfferId,
    int Quantity,
    bool ShowPurchaserName,
    int MessagesDispatched);

public sealed record GiftClubSelectRequest(
    string ProductCode,
    long? ExpectedSessionGeneration = null,
    long? ExpectedClubInfoRevision = null);

public sealed record GiftClubSelectDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long ClubInfoRevision,
    string ProductCode,
    int MessagesDispatched);

public sealed record GiftOfferGiftabilityRefreshRequest(
    int OfferId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record GiftOfferGiftabilityRefreshResult(
    ClientType Client,
    long SessionGeneration,
    long Revision,
    long OfferGiftabilityRevision,
    DateTimeOffset ObservedAtUtc,
    int OfferId,
    bool IsGiftable);

public sealed record GiftNewUserSelectRequest(
    IReadOnlyList<NuxGiftSelection> Selections,
    long? ExpectedSessionGeneration = null,
    long? ExpectedNewUserOfferRevision = null);

public sealed record GiftNewUserSelectDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    long NewUserOfferRevision,
    int SelectionCount,
    int MessagesDispatched);

public sealed record GiftNewUserAdvanceRequest(
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record GiftNewUserAdvanceDispatchReceipt(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int MessagesDispatched);

public enum GiftChangeKind
{
    Wrapping,
    ClubInfo,
    ClubSelected,
    PresentOpened,
    ReceiverNotFound,
    ClubNotification,
    OfferGiftability,
    NewUserOffer,
    NewUserIncomplete,
    Reset
}

public sealed record GiftChanged(
    GiftChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    GiftWrappingSummaryView? Wrapping,
    GiftClubInfoSummaryView? ClubInfo,
    GiftClubSelectedSummaryView? ClubSelected,
    PresentOpened? PresentOpened,
    bool ReceiverNotFound,
    ClubGiftNotification? ClubNotification,
    IsOfferGiftable? OfferGiftability,
    GiftNewUserOfferSummaryView? NewUserOffer,
    bool NewUserFlowIncomplete);

internal interface IGiftOperations
{
    void RequestWrappingConfiguration();
    void OpenPresent(Id furni_id);
    void Purchase(PurchaseFromCatalogAsGift request);
    void RequestClubGifts();
    void SelectClubGift(string product_code);
    void RequestOfferGiftability(int offer_id);
    void SelectNewUserGifts(IReadOnlyList<NuxGiftSelection> selections);
    void AdvanceNewUserFlow();
}
