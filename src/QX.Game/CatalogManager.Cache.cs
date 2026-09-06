using Qx.Model.Messages.Incoming;

namespace Qx.Game;

public sealed partial class CatalogManager
{
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromMinutes(5);

    public Task<CatalogIndex> GetIndexAsync(
        string catalogType = "NORMAL",
        TimeSpan? maxAge = null,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        BrowseOperations().GetIndexAsync(
            catalogType,
            maxAge,
            timeoutMs,
            cancellationToken);

    public Task<CatalogPage> GetPageAsync(
        int pageId,
        string catalogType = "NORMAL",
        TimeSpan? maxAge = null,
        int offerId = -1,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default) =>
        BrowseOperations().GetPageAsync(
            pageId,
            catalogType,
            maxAge,
            offerId,
            timeoutMs,
            cancellationToken);

    public Task<CatalogLoadReport> LoadAllPagesAsync(
        string catalogType = "NORMAL",
        bool onlyVisible = true,
        int delayMs = 0,
        TimeSpan? maxAge = null,
        int timeoutMs = 15000,
        IProgress<(int Loaded, int Total)>? progress = null,
        CancellationToken cancellationToken = default) =>
        BrowseOperations().LoadAllPagesAsync(
            catalogType,
            onlyVisible,
            delayMs,
            maxAge,
            timeoutMs,
            progress,
            cancellationToken);

    public IReadOnlyList<CatalogPage> CachedPages(string catalogType = "NORMAL") =>
        BrowseOperations().CachedPages(catalogType);

    public IReadOnlyList<CatalogOfferMatch> CachedOffers(string catalogType = "NORMAL") =>
        BrowseOperations().CachedOffers(catalogType);

    public CatalogCacheState CacheState(string catalogType = "NORMAL") =>
        BrowseOperations().CacheState(catalogType);

    public IReadOnlyList<CatalogOfferMatch> FindOffers(
        string text,
        string catalogType = "NORMAL",
        Func<CatalogProduct, string?>? describe = null) =>
        BrowseOperations().FindOffers(text, catalogType, describe);

    public void ClearCache(string? catalogType = null) =>
        BrowseOperations().ClearCache(catalogType);
}

public sealed record CatalogLoadReport(int Loaded, int AlreadyCached, int Refused, int Total)
{
    public int Available => Loaded + AlreadyCached;
}
