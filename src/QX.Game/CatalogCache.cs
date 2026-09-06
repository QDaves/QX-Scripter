using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

public sealed record CatalogCacheState(
    string CatalogType,
    TimeSpan? IndexAge,
    int PageCount,
    int OfferCount);

public sealed record CatalogOfferMatch(
    CatalogPageOffer Offer,
    CatalogPage Page,
    CatalogNode? Node);

internal sealed record CatalogManagerState(
    Session? Session,
    long SessionGeneration,
    long CatalogGeneration,
    long Revision,
    CatalogPublished? LastPublication,
    DateTimeOffset? LastPublishedAtUtc);

internal readonly record struct CatalogManagerScope(
    Session Session,
    long SessionGeneration,
    long CatalogGeneration);

internal enum CatalogCommitStatus
{
    Committed,
    SessionChanged,
    CatalogChanged,
    Superseded
}

internal enum CatalogInvalidationKind
{
    SessionChanged,
    Reset,
    Published,
    Cleared,
    IndexRefreshed
}

internal sealed record CatalogInvalidationUpdate(
    CatalogInvalidationKind Kind,
    CatalogManagerState State,
    string? CatalogType,
    DateTimeOffset ChangedAtUtc,
    CatalogPublished? Publication);

internal readonly record struct CatalogCachedIndex(
    CatalogIndex Value,
    long Version,
    DateTimeOffset ReceivedAtUtc,
    TimeSpan Age);

internal readonly record struct CatalogCachedPage(
    CatalogPage Value,
    long Version,
    DateTimeOffset ReceivedAtUtc,
    TimeSpan Age);

internal sealed record CatalogCacheSnapshot(
    CatalogManagerState State,
    string CatalogType,
    CatalogCachedIndex? Index,
    IReadOnlyList<CatalogPage> Pages);

internal sealed class CatalogCache
{
    internal const int MaximumIndexDepth = 64;
    internal const int MaximumIndexNodes = 4096;
    internal const int MaximumOfferIdsPerNode = 4096;
    internal const int MaximumIndexOfferIds = 65536;
    internal const int MaximumReferencedPages = 512;
    internal const int MaximumPagesPerType = 512;
    internal const int MaximumOffersPerPage = 2048;
    internal const int MaximumOffersPerType = 32768;
    internal const int MaximumProductsPerOffer = 256;
    internal const int MaximumProductsPerPage = 16384;
    internal const int MaximumLocalizationsPerPage = 256;
    internal const int MaximumUnityProductsPerOffer = 256;
    internal const int MaximumFrontPageItems = 256;

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeProvider time_provider;

    internal CatalogCache(TimeProvider time_provider)
    {
        ArgumentNullException.ThrowIfNull(time_provider);
        this.time_provider = time_provider;
    }

    internal CatalogCachedIndex? Index(string catalog_type, TimeSpan max_age)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry) || entry.Index is null)
            return null;
        CatalogIndexEntry value = entry.Index;
        TimeSpan age = time_provider.GetElapsedTime(value.Timestamp);
        return Fresh(age, max_age)
            ? new CatalogCachedIndex(value.Value, value.Version, value.ReceivedAtUtc, age)
            : null;
    }

    internal CatalogCachedIndex? HeldIndex(string catalog_type)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry) || entry.Index is null)
            return null;
        CatalogIndexEntry value = entry.Index;
        return new CatalogCachedIndex(
            value.Value,
            value.Version,
            value.ReceivedAtUtc,
            time_provider.GetElapsedTime(value.Timestamp));
    }

    internal CatalogCachedPage? Page(string catalog_type, int page_id, TimeSpan max_age)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry) ||
            !entry.Pages.TryGetValue(page_id, out CatalogPageEntry? value))
        {
            return null;
        }
        TimeSpan age = time_provider.GetElapsedTime(value.Timestamp);
        return Fresh(age, max_age)
            ? new CatalogCachedPage(value.Value, value.Version, value.ReceivedAtUtc, age)
            : null;
    }

    internal long PageVersion(string catalog_type, int page_id) =>
        entries.TryGetValue(catalog_type, out Entry? entry) &&
        entry.Pages.TryGetValue(page_id, out CatalogPageEntry? page)
            ? page.Version
            : 0;

    internal void StoreIndex(
        string catalog_type,
        CatalogIndex value,
        long version,
        long timestamp,
        DateTimeOffset received_at_utc)
    {
        Entry entry = Get(catalog_type);
        entry.Pages.Clear();
        entry.Index = new CatalogIndexEntry(value, version, timestamp, received_at_utc);
    }

    internal void StorePage(
        string catalog_type,
        CatalogPage value,
        long version,
        long timestamp,
        DateTimeOffset received_at_utc)
    {
        Entry entry = Get(catalog_type);
        bool replacing = entry.Pages.TryGetValue(value.PageId, out CatalogPageEntry? previous);
        if (!replacing && entry.Pages.Count >= MaximumPagesPerType)
            throw new InvalidDataException($"Catalog page count exceeds the limit {MaximumPagesPerType}.");
        int held_offers = entry.Pages.Values.Sum(page => page.Value.Offers.Count);
        int next_offers = checked(held_offers - (previous?.Value.Offers.Count ?? 0) + value.Offers.Count);
        if (next_offers > MaximumOffersPerType)
            throw new InvalidDataException($"Cached catalog offer count exceeds the limit {MaximumOffersPerType}.");
        entry.Pages[value.PageId] = new CatalogPageEntry(
            value,
            version,
            timestamp,
            received_at_utc);
    }

    internal IReadOnlyList<CatalogPage> Pages(string catalog_type)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry))
            return Array.AsReadOnly(Array.Empty<CatalogPage>());
        CatalogPage[] pages = entry.Pages.Values
            .Select(value => value.Value)
            .OrderBy(value => value.PageId)
            .ToArray();
        return Array.AsReadOnly(pages);
    }

    internal CatalogCacheState State(string catalog_type)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry))
            return new CatalogCacheState(catalog_type, null, 0, 0);
        TimeSpan? age = entry.Index is null
            ? null
            : time_provider.GetElapsedTime(entry.Index.Timestamp);
        return new CatalogCacheState(
            catalog_type,
            age,
            entry.Pages.Count,
            entry.Pages.Values.Sum(value => value.Value.Offers.Count));
    }

    internal void Clear(string? catalog_type)
    {
        if (catalog_type is null)
            entries.Clear();
        else
            entries.Remove(catalog_type);
    }

    internal static CatalogIndex FreezeIndex(CatalogIndex source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int nodes = 0;
        int offer_ids = 0;
        var page_ids = new HashSet<int>();
        CatalogNode root = FreezeNode(
            source.Root,
            1,
            ref nodes,
            ref offer_ids,
            page_ids);
        return new CatalogIndex(root, source.NewAdditionsAvailable, source.CatalogType);
    }

    internal static CatalogPage FreezePage(CatalogPage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Localization);
        if (source.PageId < 0)
            throw new InvalidDataException("Catalog page identifiers cannot be negative.");
        if (source.Localization.Images.Count > MaximumLocalizationsPerPage ||
            source.Localization.Texts.Count > MaximumLocalizationsPerPage)
        {
            throw new InvalidDataException(
                $"Catalog page localization count exceeds the limit {MaximumLocalizationsPerPage}.");
        }
        if (source.Offers.Count > MaximumOffersPerPage)
            throw new InvalidDataException($"Catalog page offer count exceeds the limit {MaximumOffersPerPage}.");
        if ((source.FrontPageItems?.Count ?? 0) > MaximumFrontPageItems)
            throw new InvalidDataException($"Catalog front-page item count exceeds the limit {MaximumFrontPageItems}.");

        string[] images = source.Localization.Images.Select(Required).ToArray();
        string[] texts = source.Localization.Texts.Select(Required).ToArray();
        var offers = new CatalogPageOffer[source.Offers.Count];
        int products = 0;
        for (int index = 0; index < offers.Length; index++)
            offers[index] = FreezeOffer(source.Offers[index], ref products);
        if (products > MaximumProductsPerPage)
            throw new InvalidDataException($"Catalog page product count exceeds the limit {MaximumProductsPerPage}.");

        CatalogFrontPageItem[]? front_page_items = source.FrontPageItems is null
            ? null
            : source.FrontPageItems.Select(FreezeFrontPageItem).ToArray();
        return new CatalogPage(
            source.PageId,
            Required(source.CatalogType),
            Required(source.LayoutCode),
            new CatalogPageLocalization(
                Array.AsReadOnly(images),
                Array.AsReadOnly(texts)),
            Array.AsReadOnly(offers),
            source.OfferId,
            source.AcceptSeasonCurrencyAsCredits,
            front_page_items is null ? null : Array.AsReadOnly(front_page_items));
    }

    private static CatalogNode FreezeNode(
        CatalogNode source,
        int depth,
        ref int nodes,
        ref int offer_ids,
        HashSet<int> page_ids)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (depth > MaximumIndexDepth)
            throw new InvalidDataException($"Catalog index depth exceeds the limit {MaximumIndexDepth}.");
        if (++nodes > MaximumIndexNodes)
            throw new InvalidDataException($"Catalog index node count exceeds the limit {MaximumIndexNodes}.");
        if (source.OfferIds.Count > MaximumOfferIdsPerNode)
            throw new InvalidDataException($"Catalog node offer count exceeds the limit {MaximumOfferIdsPerNode}.");
        offer_ids = checked(offer_ids + source.OfferIds.Count);
        if (offer_ids > MaximumIndexOfferIds)
            throw new InvalidDataException($"Catalog index offer count exceeds the limit {MaximumIndexOfferIds}.");
        if (source.PageId >= 0 && !page_ids.Add(source.PageId))
            throw new InvalidDataException($"Catalog page identifier {source.PageId} occurs more than once in the index.");
        if (page_ids.Count > MaximumReferencedPages)
            throw new InvalidDataException($"Catalog index page count exceeds the limit {MaximumReferencedPages}.");

        int[] node_offer_ids = source.OfferIds.ToArray();
        var children = new CatalogNode[source.Children.Count];
        for (int index = 0; index < children.Length; index++)
        {
            children[index] = FreezeNode(
                source.Children[index],
                depth + 1,
                ref nodes,
                ref offer_ids,
                page_ids);
        }
        return new CatalogNode(
            source.Visible,
            source.Icon,
            source.PageId,
            Required(source.PageName),
            Required(source.Localization),
            Array.AsReadOnly(node_offer_ids),
            Array.AsReadOnly(children));
    }

    private static CatalogPageOffer FreezeOffer(CatalogPageOffer source, ref int product_total)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Products.Count > MaximumProductsPerOffer)
            throw new InvalidDataException($"Catalog offer product count exceeds the limit {MaximumProductsPerOffer}.");
        if ((source.UnityProductReferences?.Count ?? 0) > MaximumUnityProductsPerOffer ||
            (source.UnityProducts?.Count ?? 0) > MaximumUnityProductsPerOffer)
        {
            throw new InvalidDataException(
                $"Catalog Unity product count exceeds the limit {MaximumUnityProductsPerOffer}.");
        }
        product_total = checked(product_total + source.Products.Count);
        CatalogProduct[] products = source.Products.Select(FreezeProduct).ToArray();
        CatalogPageProductReference[]? references = source.UnityProductReferences is null
            ? null
            : source.UnityProductReferences.Select(value => new CatalogPageProductReference(
                value.ProductType,
                Required(value.Identifier))).ToArray();
        CatalogPageProduct[]? unity_products = source.UnityProducts is null
            ? null
            : source.UnityProducts.Select(value => new CatalogPageProduct(
                value.ProductType,
                value.FurniClassId,
                Required(value.ExtraParam),
                value.ProductCount,
                value.UniqueLimitedItem,
                value.UniqueLimitedItemSeriesSize,
                value.UniqueLimitedItemsLeft)).ToArray();
        return new CatalogPageOffer(
            source.OfferId,
            Required(source.LocalizationId),
            source.IsRent,
            source.PriceInCredits,
            source.PriceInActivityPoints,
            source.ActivityPointType,
            source.PriceInSilver,
            source.Giftable,
            Array.AsReadOnly(products),
            source.ClubLevel,
            source.BundlePurchaseAllowed,
            source.IsPet,
            Required(source.PreviewImage),
            references is null ? null : Array.AsReadOnly(references),
            unity_products is null ? null : Array.AsReadOnly(unity_products));
    }

    private static CatalogProduct FreezeProduct(CatalogProduct source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new CatalogProduct(
            Required(source.ProductType),
            source.FurniClassId,
            Required(source.ExtraParam),
            source.ProductCount,
            source.UniqueLimitedItem,
            source.UniqueLimitedItemSeriesSize,
            source.UniqueLimitedItemsLeft,
            source.UnityProductType);
    }

    private static CatalogFrontPageItem FreezeFrontPageItem(CatalogFrontPageItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new CatalogFrontPageItem(
            source.Position,
            Required(source.ItemName),
            Required(source.ItemPromoImage),
            source.Type,
            Required(source.CataloguePageLocation),
            source.ProductOfferId,
            Required(source.ProductCode),
            source.ExpirationSeconds);
    }

    private static string Required(string value) =>
        value ?? throw new InvalidDataException("Catalog strings cannot be null.");

    private static bool Fresh(TimeSpan age, TimeSpan max_age)
    {
        if (max_age == Timeout.InfiniteTimeSpan)
            return true;
        if (max_age <= TimeSpan.Zero)
            return false;
        return age <= max_age;
    }

    private Entry Get(string catalog_type)
    {
        if (!entries.TryGetValue(catalog_type, out Entry? entry))
            entries.Add(catalog_type, entry = new Entry());
        return entry;
    }

    private sealed class Entry
    {
        public CatalogIndexEntry? Index { get; set; }
        public Dictionary<int, CatalogPageEntry> Pages { get; } = [];
    }

    private sealed record CatalogIndexEntry(
        CatalogIndex Value,
        long Version,
        long Timestamp,
        DateTimeOffset ReceivedAtUtc);

    private sealed record CatalogPageEntry(
        CatalogPage Value,
        long Version,
        long Timestamp,
        DateTimeOffset ReceivedAtUtc);
}
