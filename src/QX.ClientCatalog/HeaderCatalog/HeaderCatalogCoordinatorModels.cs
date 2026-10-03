using Qx.ClientCatalog.InstalledClients;

namespace Qx.ClientCatalog;

public enum HeaderCatalogPreparationStage
{
    Discovered,
    Hashing,
    CacheLookup,
    Extracting,
    Ready,
    Failed
}

public sealed record HeaderCatalogPreparationStatus(
    InstalledClientCandidate Candidate,
    string NormalizedPath,
    HeaderCatalogPreparationStage Stage,
    DateTimeOffset ChangedAt,
    string? SourceSha256 = null,
    HeaderCatalogCacheState? CacheState = null,
    Exception? Error = null);

public sealed class HeaderCatalogPreparationChangedEventArgs : EventArgs
{
    public HeaderCatalogPreparationChangedEventArgs(HeaderCatalogPreparationStatus status)
    {
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }

    public HeaderCatalogPreparationStatus Status { get; }
}

public sealed record PreparedHeaderCatalog
{
    public PreparedHeaderCatalog(
        InstalledClientCandidate candidate,
        string normalizedPath,
        string sourcePath,
        HeaderCatalogKey key,
        HeaderCatalogSnapshot catalog,
        HeaderCatalogCacheState cacheState,
        string contentSha256,
        DateTimeOffset preparedAt)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        NormalizedPath = Path.GetFullPath(normalizedPath);
        SourcePath = Path.GetFullPath(sourcePath);
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        CacheState = cacheState;
        ContentSha256 = HeaderCatalogKey.NormalizeHash(contentSha256, nameof(contentSha256));
        PreparedAt = preparedAt;
    }

    public InstalledClientCandidate Candidate { get; }
    public string NormalizedPath { get; }
    public string SourcePath { get; }
    public HeaderCatalogKey Key { get; }
    public HeaderCatalogSnapshot Catalog { get; }
    public HeaderCatalogCacheState CacheState { get; }
    public string ContentSha256 { get; }
    public DateTimeOffset PreparedAt { get; }
}
