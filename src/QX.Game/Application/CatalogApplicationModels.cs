using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Game.Application;

/// <summary>Represents a request for the cache state of one catalog type.</summary>
/// <remarks>Used by the <c>catalog.state</c> query.</remarks>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
public sealed record CatalogStateRequest(string CatalogType = "NORMAL");

/// <summary>Represents a request for the catalog index.</summary>
/// <remarks>
/// Used by the <c>catalog.index.get</c> operation. A cached index that is not older than
/// <paramref name="MaxAgeMilliseconds"/> is returned without a request. Concurrent callers for the
/// same catalog type share one request, and receiving a new index clears the cached pages of that
/// catalog type.
/// </remarks>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
/// <param name="MaxAgeMilliseconds">The maximum age of a cached index, in milliseconds. 0 always requests a new index and -1 accepts any cached index.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogIndexGetRequest(
    string CatalogType = "NORMAL",
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents a request for one catalog page.</summary>
/// <remarks>
/// Used by the <c>catalog.page.get</c> operation. A cached page that is not older than
/// <paramref name="MaxAgeMilliseconds"/> is returned without a request, and concurrent callers for
/// the same page share one request.
/// </remarks>
/// <param name="PageId">The id of the catalog page, which must not be negative.</param>
/// <param name="OfferId">The offer to select on the page, or -1 for none. The selected offer is always included in the result with all its products.</param>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
/// <param name="MaxAgeMilliseconds">The maximum age of a cached page, in milliseconds. 0 always requests the page and -1 accepts any cached page.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogPageGetRequest(
    int PageId,
    int OfferId = -1,
    string CatalogType = "NORMAL",
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents a request to load every page listed in the catalog index into the cache.</summary>
/// <remarks>
/// Used by the <c>catalog.pages.load</c> operation. The index is read first, then every page node
/// that has offers is loaded in index order. Pages still fresh in the cache are not requested again,
/// and a page request that times out is counted as refused while the walk continues. Concurrent
/// calls with the same arguments share one walk, and the call itself has no overall timeout.
/// </remarks>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
/// <param name="OnlyVisible">Whether to skip hidden index nodes and everything below them.</param>
/// <param name="DelayMilliseconds">The pause after each page request, in milliseconds, which must not be negative.</param>
/// <param name="MaxAgeMilliseconds">The maximum age of a cached index or page, in milliseconds. 0 always requests a new one and -1 accepts any cached one.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the index and for each page response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogLoadRequest(
    string CatalogType = "NORMAL",
    bool OnlyVisible = true,
    int DelayMilliseconds = 0,
    long MaxAgeMilliseconds = 300000,
    int TimeoutMilliseconds = 15000,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents a request for a page of the cached catalog page summaries.</summary>
/// <remarks>
/// Used by the <c>catalog.pages.list</c> query, which reads only the cache. The pages are ordered by
/// page id. The application keeps the 16 most recent catalog snapshots, and every snapshot is
/// dropped when the catalog generation changes. A continuation page must use the same catalog type
/// as the first page.
/// </remarks>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
/// <param name="Offset">The zero-based index of the first page summary to return.</param>
/// <param name="Limit">The maximum number of page summaries to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current cache. Required when <paramref name="Offset"/> is greater than 0.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogPagesRequest(
    string CatalogType = "NORMAL",
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents a search of the offers on the cached catalog pages.</summary>
/// <remarks>
/// Used by the <c>catalog.offers.search</c> query, which reads only the cache. An offer matches when
/// the text is found, ignoring case, in its localization id, the page name or caption, or the type or
/// extra parameter of one of its products, or when the text equals the furniture class id of one of
/// its products. The matches are ordered by page id, then offer id. A continuation page must use the
/// same text and catalog type as the first page.
/// </remarks>
/// <param name="Text">The text to search for, at most 1024 UTF-8 bytes, or an empty string to list every cached offer.</param>
/// <param name="CatalogType">The catalog type, <c>NORMAL</c> or <c>BUILDERS_CLUB</c>, ignoring case.</param>
/// <param name="Offset">The zero-based index of the first match to return.</param>
/// <param name="Limit">The maximum number of matches to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current cache. Required when <paramref name="Offset"/> is greater than 0.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogOfferSearchRequest(
    string Text = "",
    string CatalogType = "NORMAL",
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents a request to clear the catalog cache.</summary>
/// <remarks>Used by the <c>catalog.cache.clear</c> operation. Clearing the cache advances the catalog generation.</remarks>
/// <param name="CatalogType">The catalog type to clear, <c>NORMAL</c> or <c>BUILDERS_CLUB</c> ignoring case, or <see langword="null"/> to clear every type.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogCacheClearRequest(
    string? CatalogType = null,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents the cache state of one catalog type.</summary>
/// <remarks>Returned by the <c>catalog.state</c> query.</remarks>
/// <param name="Connected">Whether the catalog state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the catalog state belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation, which increases whenever a new index is received or the cache is invalidated.</param>
/// <param name="Revision">The catalog cache revision, which increases whenever an index or page is cached or the cache is invalidated.</param>
/// <param name="CatalogType">The catalog type the state was read for.</param>
/// <param name="IndexLoaded">Whether the index of the catalog type is cached.</param>
/// <param name="IndexReceivedAtUtc">The UTC time the cached index was received, or <see langword="null"/> when no index is cached.</param>
/// <param name="IndexAgeMilliseconds">The age of the cached index in milliseconds, rounded up, or <see langword="null"/> when no index is cached.</param>
/// <param name="CachedPages">The number of cached pages of the catalog type.</param>
/// <param name="CachedOffers">The number of offers across the cached pages.</param>
/// <param name="LastPublication">The last catalog publication the hotel announced, or <see langword="null"/> when there is none.</param>
/// <param name="LastPublishedAtUtc">The UTC time of the last catalog publication, or <see langword="null"/> when there is none.</param>
public sealed record CatalogStateView(
    bool Connected,
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

/// <summary>Represents one node of the catalog index tree.</summary>
/// <param name="PageId">The id of the catalog page.</param>
/// <param name="ParentPageId">The page id of the parent node, or <see langword="null"/> for the root or when the parent has a negative page id.</param>
/// <param name="Depth">The depth of the node, 0 for the root.</param>
/// <param name="Visible">Whether the node is visible in the catalog.</param>
/// <param name="Icon">The icon id of the node.</param>
/// <param name="PageName">The internal name of the page.</param>
/// <param name="Localization">The localized caption of the page.</param>
/// <param name="OfferCount">The number of offers on the page.</param>
/// <param name="ChildCount">The number of child nodes.</param>
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

/// <summary>Represents the catalog index as a flat list of nodes.</summary>
/// <remarks>
/// Returned by the <c>catalog.index.get</c> operation. The nodes are listed depth first, each parent
/// before its children, and at most 500 nodes are returned.
/// </remarks>
/// <param name="SessionGeneration">The generation of the hotel session the index belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation the index belongs to.</param>
/// <param name="Revision">The catalog cache revision when the index was read or stored.</param>
/// <param name="ReceivedAtUtc">The UTC time the index was received from the server.</param>
/// <param name="FromCache">Whether the index was read from the cache rather than requested.</param>
/// <param name="CatalogType">The catalog type of the index.</param>
/// <param name="NewAdditionsAvailable">Whether the catalog has new additions.</param>
/// <param name="TotalNodes">The total number of nodes in the index tree.</param>
/// <param name="NodesTruncated">Whether the tree has more nodes than <paramref name="Nodes"/> holds.</param>
/// <param name="Nodes">The nodes of the index tree, depth first.</param>
public sealed record CatalogIndexView(
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

/// <summary>Represents a product included in a catalog offer.</summary>
/// <param name="ProductType">The product type code, such as <c>s</c> for floor furniture or <c>b</c> for a badge.</param>
/// <param name="FurniClassId">The furniture class id of the product, or 0 for a badge.</param>
/// <param name="ExtraParam">The extra parameter of the product, which holds the badge code for a badge.</param>
/// <param name="ProductCount">The number of items the product gives.</param>
/// <param name="UniqueLimitedItem">Whether the product is a limited edition item.</param>
/// <param name="UniqueLimitedItemSeriesSize">The total number of items in the limited edition series, or 0 when the product is not limited.</param>
/// <param name="UniqueLimitedItemsLeft">The number of limited edition items left, or 0 when the product is not limited.</param>
public sealed record CatalogProductView(
    string ProductType,
    int FurniClassId,
    string ExtraParam,
    int ProductCount,
    bool UniqueLimitedItem,
    int UniqueLimitedItemSeriesSize,
    int UniqueLimitedItemsLeft);

/// <summary>Represents an offer on a catalog page.</summary>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="LocalizationId">The localization key of the offer name.</param>
/// <param name="IsRent">Whether the offer is a rental.</param>
/// <param name="PriceInCredits">The price in credits.</param>
/// <param name="PriceInActivityPoints">The price in activity points.</param>
/// <param name="ActivityPointType">The activity point type the activity point price is paid in.</param>
/// <param name="PriceInSilver">The price in silver.</param>
/// <param name="Giftable">Whether the offer can be bought as a gift.</param>
/// <param name="ClubLevel">The club level required to buy the offer.</param>
/// <param name="BundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
/// <param name="IsPet">Whether the offer is a pet.</param>
/// <param name="PreviewImage">The preview image of the offer.</param>
/// <param name="TotalProducts">The number of products the offer contains.</param>
/// <param name="ProductsTruncated">Whether the offer has more products than <paramref name="Products"/> holds.</param>
/// <param name="Products">The products of the offer that fit in the result.</param>
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

/// <summary>Represents a promoted item on the catalog front page.</summary>
/// <param name="Position">The position of the item on the front page.</param>
/// <param name="ItemName">The name of the item.</param>
/// <param name="ItemPromoImage">The promotional image of the item.</param>
/// <param name="Type">The link type of the item, 0 for a catalog page location, 1 for a product offer and 2 for a product code.</param>
/// <param name="CataloguePageLocation">The catalog page location, or an empty string when <paramref name="Type"/> is not 0.</param>
/// <param name="ProductOfferId">The id of the product offer, or 0 when <paramref name="Type"/> is not 1.</param>
/// <param name="ProductCode">The product code, or an empty string when <paramref name="Type"/> is not 2.</param>
/// <param name="ExpirationSeconds">The number of seconds until the item expires.</param>
public sealed record CatalogFrontPageItemView(
    int Position,
    string ItemName,
    string ItemPromoImage,
    int Type,
    string CataloguePageLocation,
    int ProductOfferId,
    string ProductCode,
    int ExpirationSeconds);

/// <summary>Represents the contents of one catalog page.</summary>
/// <remarks>
/// Returned by the <c>catalog.page.get</c> operation. At most 500 offers, images, texts and front
/// page items are returned, and at most 500 products across all returned offers. The offer requested
/// with the page is always included with all its products.
/// </remarks>
/// <param name="SessionGeneration">The generation of the hotel session the page belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation the page belongs to.</param>
/// <param name="Revision">The catalog cache revision when the page was read or stored.</param>
/// <param name="ReceivedAtUtc">The UTC time the page was received from the server.</param>
/// <param name="FromCache">Whether the page was read from the cache rather than requested.</param>
/// <param name="PageId">The id of the page.</param>
/// <param name="CatalogType">The catalog type of the page.</param>
/// <param name="LayoutCode">The layout code of the page.</param>
/// <param name="SelectedOfferId">The id of the offer selected on the page.</param>
/// <param name="AcceptSeasonCurrencyAsCredits">Whether the page accepts seasonal currency in place of credits.</param>
/// <param name="TotalImages">The number of images on the page.</param>
/// <param name="ImagesTruncated">Whether the page has more images than <paramref name="Images"/> holds.</param>
/// <param name="Images">The images of the page.</param>
/// <param name="TotalTexts">The number of texts on the page.</param>
/// <param name="TextsTruncated">Whether the page has more texts than <paramref name="Texts"/> holds.</param>
/// <param name="Texts">The texts of the page.</param>
/// <param name="TotalOffers">The number of offers on the page.</param>
/// <param name="OffersTruncated">Whether the page has more offers than <paramref name="Offers"/> holds.</param>
/// <param name="Offers">The offers of the page, in page order.</param>
/// <param name="TotalFrontPageItems">The number of front page items, or 0 when the page carries none.</param>
/// <param name="FrontPageItemsTruncated">Whether the page has more front page items than <paramref name="FrontPageItems"/> holds.</param>
/// <param name="FrontPageItems">The front page items of the page.</param>
public sealed record CatalogPageView(
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

/// <summary>Represents the result of loading every catalog page into the cache.</summary>
/// <remarks>Returned by the <c>catalog.pages.load</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the load ran in.</param>
/// <param name="CatalogGeneration">The catalog generation the pages were loaded for.</param>
/// <param name="Revision">The catalog cache revision after the load.</param>
/// <param name="CompletedAtUtc">The UTC time the load completed.</param>
/// <param name="CatalogType">The catalog type that was loaded.</param>
/// <param name="OnlyVisible">Whether hidden index nodes were skipped.</param>
/// <param name="Loaded">The number of pages requested and received.</param>
/// <param name="AlreadyCached">The number of pages that were fresh in the cache.</param>
/// <param name="Refused">The number of pages whose request timed out.</param>
/// <param name="Total">The number of pages the index listed for loading.</param>
/// <param name="Available">The number of pages that were loaded or already cached, the sum of <paramref name="Loaded"/> and <paramref name="AlreadyCached"/>.</param>
public sealed record CatalogLoadView(
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

/// <summary>Represents a summary of one cached catalog page.</summary>
/// <param name="PageId">The id of the page.</param>
/// <param name="CatalogType">The catalog type of the page.</param>
/// <param name="LayoutCode">The layout code of the page.</param>
/// <param name="SelectedOfferId">The id of the offer selected on the page.</param>
/// <param name="OfferCount">The number of offers on the page.</param>
/// <param name="ProductCount">The total number of products across the offers of the page.</param>
/// <param name="AcceptSeasonCurrencyAsCredits">Whether the page accepts seasonal currency in place of credits.</param>
public sealed record CatalogPageSummaryView(
    int PageId,
    string CatalogType,
    string LayoutCode,
    int SelectedOfferId,
    int OfferCount,
    int ProductCount,
    bool AcceptSeasonCurrencyAsCredits);

/// <summary>Represents a page of cached catalog page summaries read from one snapshot.</summary>
/// <remarks>Returned by the <c>catalog.pages.list</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation the snapshot belongs to.</param>
/// <param name="StateRevision">The catalog cache revision when the snapshot was captured.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="CatalogType">The catalog type the snapshot was read for.</param>
/// <param name="TotalPages">The number of cached pages in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first page summary in the result.</param>
/// <param name="NextOffset">The offset of the next result page, or <see langword="null"/> when there are no more page summaries.</param>
/// <param name="Pages">The page summaries in the result, ordered by page id.</param>
public sealed record CatalogPageListView(
    bool Connected,
    long SessionGeneration,
    long CatalogGeneration,
    long StateRevision,
    long SnapshotRevision,
    string CatalogType,
    int TotalPages,
    int Offset,
    int? NextOffset,
    IReadOnlyList<CatalogPageSummaryView> Pages);

/// <summary>Represents a cached catalog offer that matched a search.</summary>
/// <param name="PageId">The id of the page that holds the offer.</param>
/// <param name="PageName">The internal name of the page, or an empty string when the page is not in the cached index.</param>
/// <param name="PageLocalization">The localized caption of the page, or an empty string when the page is not in the cached index.</param>
/// <param name="PageVisible">Whether the page is visible in the catalog, or <see langword="false"/> when the page is not in the cached index.</param>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="LocalizationId">The localization key of the offer name.</param>
/// <param name="IsRent">Whether the offer is a rental.</param>
/// <param name="PriceInCredits">The price in credits.</param>
/// <param name="PriceInActivityPoints">The price in activity points.</param>
/// <param name="ActivityPointType">The activity point type the activity point price is paid in.</param>
/// <param name="PriceInSilver">The price in silver.</param>
/// <param name="Giftable">Whether the offer can be bought as a gift.</param>
/// <param name="ClubLevel">The club level required to buy the offer.</param>
/// <param name="BundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
/// <param name="IsPet">Whether the offer is a pet.</param>
/// <param name="ProductCount">The number of products the offer contains.</param>
/// <param name="FirstProductType">The product type code of the first product, or <see langword="null"/> when the offer has no products.</param>
/// <param name="FirstFurniClassId">The furniture class id of the first product, or <see langword="null"/> when the offer has no products.</param>
/// <param name="FirstExtraParam">The extra parameter of the first product, or <see langword="null"/> when the offer has no products.</param>
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

/// <summary>Represents a page of cached catalog offers that matched a search, read from one snapshot.</summary>
/// <remarks>Returned by the <c>catalog.offers.search</c> query.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation the snapshot belongs to.</param>
/// <param name="StateRevision">The catalog cache revision when the snapshot was captured.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Text">The text that was searched for.</param>
/// <param name="CatalogType">The catalog type that was searched.</param>
/// <param name="TotalOffers">The number of matching offers in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first match in the result.</param>
/// <param name="NextOffset">The offset of the next result page, or <see langword="null"/> when there are no more matches.</param>
/// <param name="Offers">The matching offers in the result.</param>
public sealed record CatalogOfferSearchPage(
    bool Connected,
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

/// <summary>Represents the result of clearing the catalog cache.</summary>
/// <remarks>Returned by the <c>catalog.cache.clear</c> operation.</remarks>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the catalog state belongs to.</param>
/// <param name="CatalogGeneration">The catalog generation after the cache was cleared.</param>
/// <param name="Revision">The catalog cache revision after the cache was cleared.</param>
/// <param name="ClearedAtUtc">The UTC time the cache was cleared.</param>
/// <param name="CatalogType">The catalog type that was cleared, or <see langword="null"/> when every type was cleared.</param>
public sealed record CatalogCacheClearView(
    bool Connected,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ClearedAtUtc,
    string? CatalogType);

/// <summary>Represents a catalog publication announced by the hotel.</summary>
/// <remarks>
/// Published by the <c>catalog.published</c> event after the catalog cache has been invalidated, so
/// the reported catalog generation already reflects the publication.
/// </remarks>
/// <param name="SessionGeneration">The generation of the hotel session the publication was received in.</param>
/// <param name="CatalogGeneration">The catalog generation after the cache was invalidated.</param>
/// <param name="Revision">The catalog cache revision after the cache was invalidated.</param>
/// <param name="ReceivedAtUtc">The UTC time the publication was applied.</param>
/// <param name="Publication">The publication message the hotel sent.</param>
public sealed record CatalogPublishedEvent(
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    CatalogPublished Publication);

/// <summary>Represents a request for the latest catalog purchase outcome.</summary>
/// <remarks>Used by the <c>catalog.purchase.state</c> query.</remarks>
public sealed record CatalogPurchaseStateRequest;

/// <summary>Represents a request to buy a catalog offer.</summary>
/// <remarks>
/// Used by the <c>catalog.purchase.send</c> operation. The operation sends one purchase and returns
/// without waiting. The hotel outcome is published later by the <c>catalog.purchase.outcome</c>
/// event and is not matched to the purchase that caused it.
/// </remarks>
/// <param name="PageId">The id of the catalog page, which must not be negative.</param>
/// <param name="OfferId">The id of the catalog offer, which must not be negative.</param>
/// <param name="ExtraData">The offer selection data, at most 65535 UTF-8 bytes.</param>
/// <param name="Quantity">The number of the offer to buy, at least 1.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call requires, or <see langword="null"/> to skip the check.</param>
/// <param name="ExpectedCatalogGeneration">The catalog generation the call requires, or <see langword="null"/> to skip the check.</param>
public sealed record CatalogPurchaseSendRequest(
    int PageId,
    int OfferId,
    string ExtraData = "",
    int Quantity = 1,
    long? ExpectedSessionGeneration = null,
    long? ExpectedCatalogGeneration = null);

/// <summary>Represents the receipt for a catalog purchase that was sent.</summary>
/// <remarks>Returned by the <c>catalog.purchase.send</c> operation.</remarks>
/// <param name="SessionGeneration">The generation of the hotel session the purchase was sent in.</param>
/// <param name="CatalogGeneration">The catalog generation the purchase was sent in.</param>
/// <param name="PageId">The id of the catalog page.</param>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="Quantity">The number of the offer that was requested.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
/// <param name="DispatchedAtUtc">The UTC time the purchase was sent.</param>
public sealed record CatalogPurchaseDispatchReceipt(
    long SessionGeneration,
    long CatalogGeneration,
    int PageId,
    int OfferId,
    int Quantity,
    int MessagesDispatched,
    DateTimeOffset DispatchedAtUtc);

/// <summary>Specifies the kind of a catalog purchase outcome.</summary>
public enum CatalogPurchaseOutcomeKind
{
    /// <summary>The hotel accepted the purchase and reported the purchased offer.</summary>
    Accepted,
    /// <summary>The hotel reported that the purchase failed.</summary>
    Failed,
    /// <summary>The hotel reported that the purchase is not allowed.</summary>
    Forbidden
}

/// <summary>Represents the offer the hotel reported for an accepted purchase.</summary>
/// <param name="OfferId">The id of the catalog offer.</param>
/// <param name="LocalizationId">The localization key of the offer name.</param>
/// <param name="IsRent">Whether the offer is a rental.</param>
/// <param name="PriceInCredits">The price in credits.</param>
/// <param name="PriceInActivityPoints">The price in activity points.</param>
/// <param name="ActivityPointType">The activity point type the activity point price is paid in.</param>
/// <param name="Giftable">Whether the offer can be bought as a gift.</param>
/// <param name="ClubLevel">The club level required to buy the offer.</param>
/// <param name="BundlePurchaseAllowed">Whether several of the offer can be bought at once.</param>
/// <param name="TotalProducts">The number of products the offer contains.</param>
/// <param name="ProductsTruncated">Whether the offer has more products than <paramref name="Products"/> holds.</param>
/// <param name="Products">The products of the offer, at most 256.</param>
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
    IReadOnlyList<CatalogProductView> Products);

/// <summary>Represents a catalog purchase outcome reported by the hotel.</summary>
/// <param name="Kind">The kind of outcome.</param>
/// <param name="Offer">The purchased offer for <see cref="CatalogPurchaseOutcomeKind.Accepted"/>; otherwise, <see langword="null"/>.</param>
/// <param name="ErrorCode">The error code the hotel sent, or 0 for <see cref="CatalogPurchaseOutcomeKind.Accepted"/>.</param>
public sealed record CatalogPurchaseOutcomeView(
    CatalogPurchaseOutcomeKind Kind,
    CatalogPurchaseOfferView? Offer,
    int ErrorCode);

/// <summary>Represents the latest catalog purchase outcome of the session.</summary>
/// <remarks>Returned by the <c>catalog.purchase.state</c> query.</remarks>
/// <param name="Connected">Whether the purchase state belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the purchase state belongs to.</param>
/// <param name="Revision">The purchase state revision, which increases with each outcome and when the state is cleared.</param>
/// <param name="LastOutcome">The latest outcome, or <see langword="null"/> when none has been received or <paramref name="Connected"/> is <see langword="false"/>.</param>
/// <param name="LastOutcomeAtUtc">The UTC time the latest outcome was received, or <see langword="null"/> when there is none.</param>
public sealed record CatalogPurchaseStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    CatalogPurchaseOutcomeView? LastOutcome,
    DateTimeOffset? LastOutcomeAtUtc);

/// <summary>Represents a catalog purchase outcome published as it is received.</summary>
/// <remarks>
/// Published by the <c>catalog.purchase.outcome</c> event. The outcome is not matched to the purchase
/// that caused it.
/// </remarks>
/// <param name="SessionGeneration">The generation of the hotel session the outcome was received in.</param>
/// <param name="Revision">The purchase state revision after the outcome.</param>
/// <param name="ReceivedAtUtc">The UTC time the outcome was received.</param>
/// <param name="Outcome">The outcome the hotel reported.</param>
public sealed record CatalogPurchaseOutcomeEvent(
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
    void Purchase(
        PurchaseFromCatalogRequest request,
        CancellationToken cancellation_token);

    void DispatchCompatibility(
        Action send,
        CancellationToken cancellation_token);
}
