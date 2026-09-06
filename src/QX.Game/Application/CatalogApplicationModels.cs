using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

public sealed record CatalogStateRequest(string CatalogType = "NORMAL");

public sealed record CatalogIndexGetRequest(
    string CatalogType = "NORMAL",
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogPageGetRequest(
    int PageId,
    int OfferId = -1,
    string CatalogType = "NORMAL",
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogLoadRequest(
    string CatalogType = "NORMAL",
    bool OnlyVisible = true,
    int DelayMilliseconds = 0,
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 15000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogPagesRequest(
    string CatalogType = "NORMAL",
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogOfferSearchRequest(
    string Text = "",
    string CatalogType = "NORMAL",
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogCacheClearRequest(
    string? CatalogType = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    string CatalogType,
    bool IndexLoaded,
    DateTimeOffset? IndexReceivedAtUtc,
    long? IndexAgeMilliseconds,
    int CachedPages,
    int CachedOffers,
    CatalogPublished? LastPublication,
    DateTimeOffset? LastPublishedAtUtc);

public sealed record CatalogNodeView(
    int PageId,
    int? ParentPageId,
    int Depth,
    bool Visible,
    int Icon,
    string PageName,
    string Localization,
    int OfferCount,
    int ChildCount);

public sealed record CatalogIndexView(
    ClientType Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    bool FromCache,
    string CatalogType,
    bool NewAdditionsAvailable,
    int TotalNodes,
    bool NodesTruncated,
    IReadOnlyList<CatalogNodeView> Nodes);

public sealed record CatalogProductView(
    string ProductType,
    int FurniClassId,
    string ExtraParam,
    int ProductCount,
    bool UniqueLimitedItem,
    int UniqueLimitedItemSeriesSize,
    int UniqueLimitedItemsLeft,
    short? UnityProductType);

public sealed record CatalogOfferView(
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
    int TotalProducts,
    bool ProductsTruncated,
    IReadOnlyList<CatalogProductView> Products);

public sealed record CatalogFrontPageItemView(
    int Position,
    string ItemName,
    string ItemPromoImage,
    int Type,
    string CataloguePageLocation,
    int ProductOfferId,
    string ProductCode,
    int ExpirationSeconds);

public sealed record CatalogPageView(
    ClientType Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    bool FromCache,
    int PageId,
    string CatalogType,
    string LayoutCode,
    int SelectedOfferId,
    bool AcceptSeasonCurrencyAsCredits,
    int TotalImages,
    bool ImagesTruncated,
    IReadOnlyList<string> Images,
    int TotalTexts,
    bool TextsTruncated,
    IReadOnlyList<string> Texts,
    int TotalOffers,
    bool OffersTruncated,
    IReadOnlyList<CatalogOfferView> Offers,
    int TotalFrontPageItems,
    bool FrontPageItemsTruncated,
    IReadOnlyList<CatalogFrontPageItemView> FrontPageItems);

public sealed record CatalogLoadView(
    ClientType Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset CompletedAtUtc,
    string CatalogType,
    bool OnlyVisible,
    int Loaded,
    int AlreadyCached,
    int Refused,
    int Total,
    int Available);

public sealed record CatalogPageSummaryView(
    int PageId,
    string CatalogType,
    string LayoutCode,
    int SelectedOfferId,
    int OfferCount,
    int ProductCount,
    bool AcceptSeasonCurrencyAsCredits);

public sealed record CatalogPageListView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long CatalogGeneration,
    long StateRevision,
    long SnapshotRevision,
    string CatalogType,
    int TotalPages,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CatalogPageSummaryView> Pages);

public sealed record CatalogOfferSearchMatchView(
    int PageId,
    string PageName,
    string PageLocalization,
    bool PageVisible,
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
    int ProductCount,
    string? FirstProductType,
    int? FirstFurniClassId,
    string? FirstExtraParam);

public sealed record CatalogOfferSearchPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long CatalogGeneration,
    long StateRevision,
    long SnapshotRevision,
    string Text,
    string CatalogType,
    int TotalOffers,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CatalogOfferSearchMatchView> Offers);

public sealed record CatalogCacheClearView(
    ClientType? Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ClearedAtUtc,
    string? CatalogType);

public sealed record CatalogPublishedEvent(
    ClientType Client,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    CatalogPublished Publication);

public sealed record CatalogPurchaseStateRequest;

public sealed record CatalogPurchaseSendRequest(
    int PageId,
    int OfferId,
    string ExtraData = "",
    int Quantity = 1,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

public sealed record CatalogPurchaseDispatchReceipt(
    ClientType Client,
    long SessionGeneration,
    long CatalogGeneration,
    int PageId,
    int OfferId,
    int Quantity,
    int MessagesDispatched,
    DateTimeOffset DispatchedAtUtc);

public enum CatalogPurchaseOutcomeKind
{
    Accepted,
    Failed,
    Forbidden
}

public sealed record CatalogPurchaseOfferView(
    int OfferId,
    string LocalizationId,
    bool IsRent,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    bool Giftable,
    int ClubLevel,
    bool BundlePurchaseAllowed,
    int TotalProducts,
    bool ProductsTruncated,
    IReadOnlyList<CatalogProductView> Products,
    Id? GiftTo,
    int TotalRoomItems,
    bool RoomItemsTruncated,
    IReadOnlyList<Id> RoomItems,
    int TotalWallItems,
    bool WallItemsTruncated,
    IReadOnlyList<Id> WallItems);

public sealed record CatalogPurchaseOutcomeView(
    CatalogPurchaseOutcomeKind Kind,
    CatalogPurchaseOfferView? Offer,
    int ErrorCode);

public sealed record CatalogPurchaseStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    CatalogPurchaseOutcomeView? LastOutcome,
    DateTimeOffset? LastOutcomeAtUtc);

public sealed record CatalogPurchaseOutcomeEvent(
    ClientType Client,
    long SessionGeneration,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    CatalogPurchaseOutcomeView Outcome);

internal interface ICatalogBrowseOperations
{
    Task<CatalogIndex> GetIndexAsync(
        string catalog_type,
        TimeSpan? max_age,
        int timeout_ms,
        CancellationToken cancellation_token);

    Task<CatalogPage> GetPageAsync(
        int page_id,
        string catalog_type,
        TimeSpan? max_age,
        int offer_id,
        int timeout_ms,
        CancellationToken cancellation_token);

    Task<CatalogLoadReport> LoadAllPagesAsync(
        string catalog_type,
        bool only_visible,
        int delay_ms,
        TimeSpan? max_age,
        int timeout_ms,
        IProgress<(int Loaded, int Total)>? progress,
        CancellationToken cancellation_token);

    IReadOnlyList<CatalogPage> CachedPages(string catalog_type);

    IReadOnlyList<CatalogOfferMatch> CachedOffers(string catalog_type);

    CatalogCacheState CacheState(string catalog_type);

    IReadOnlyList<CatalogOfferMatch> FindOffers(
        string text,
        string catalog_type,
        Func<CatalogProduct, string?>? describe);

    void ClearCache(string? catalog_type);
}

internal interface ICatalogPurchaseOperations
{
    Task<CatalogPurchaseOutcome> PurchaseAsync(
        PurchaseFromCatalogRequest request,
        int timeout_ms,
        CancellationToken cancellation_token);

    Task<CatalogPurchaseOutcome> DispatchCompatibility(
        Action send,
        int timeout_ms,
        CancellationToken cancellation_token);
}
