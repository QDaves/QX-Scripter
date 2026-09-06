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
    ClientType Client,
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
        string normalized_path,
        string source_path,
        HeaderCatalogKey key,
        HeaderCatalogSnapshot catalog,
        HeaderCatalogCacheState cache_state,
        string content_sha256,
        DateTimeOffset prepared_at)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        NormalizedPath = Path.GetFullPath(normalized_path);
        SourcePath = Path.GetFullPath(source_path);
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        CacheState = cache_state;
        ContentSha256 = HeaderCatalogKey.NormalizeHash(content_sha256, nameof(content_sha256));
        PreparedAt = prepared_at;
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
