using Qx;

namespace Qx.Protocol;

public sealed record CatalogSupplement
{
    public CatalogSupplement(CatalogProvenance provenance, int alias_count)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.Origin is not (CatalogOrigin.GEarthHandshake or CatalogOrigin.Sulek))
            throw new ArgumentException("A catalog supplement requires fallback provenance.", nameof(provenance));
        if (alias_count <= 0)
            throw new ArgumentOutOfRangeException(nameof(alias_count));
        Provenance = provenance;
        AliasCount = alias_count;
    }

    public CatalogProvenance Provenance { get; }
    public int AliasCount { get; }
}

public sealed record SessionCatalogBinding
{
    public SessionCatalogBinding(
        ClientType client,
        MessageCatalog? catalog,
        CatalogProvenance provenance,
        ClientBuildIdentity? build = null,
        CatalogSupplement? supplement = null)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (!ClientTypes.IsSupported(client) ||
            provenance.Client != client)
            throw new ArgumentException("The catalog provenance does not match the bound client.", nameof(provenance));
        if ((provenance.Origin == CatalogOrigin.Unavailable) != (catalog is null))
            throw new ArgumentException("Only an unavailable binding can omit its catalog.", nameof(catalog));
        if (provenance.Origin == CatalogOrigin.ClientExtraction)
        {
            if (provenance.SourceSha256 is null ||
                catalog is null ||
                !catalog.MatchesBuildFingerprint(provenance.SourceSha256))
            {
                throw new ArgumentException("An extracted catalog must match its source hash.", nameof(catalog));
            }
        }
        if (build is not null &&
            (catalog is null ||
             !catalog.MatchesBuildFingerprint(build.CatalogFingerprint) ||
             build.SchemaFingerprint is not null &&
             !catalog.MatchesSchemaFingerprint(build.SchemaFingerprint)))
        {
            throw new ArgumentException("The catalog does not match its build identity.", nameof(build));
        }
        if (supplement is not null &&
            (catalog is null ||
             supplement.Provenance.Client != client ||
             provenance.ClientVersion is null ||
             !string.Equals(
                 supplement.Provenance.ClientVersion,
                 provenance.ClientVersion,
                 StringComparison.Ordinal)))
        {
            throw new ArgumentException("The catalog supplement does not match the bound client.", nameof(supplement));
        }
        Client = client;
        Catalog = catalog?.Snapshot();
        Provenance = provenance;
        Build = build;
        Supplement = supplement;
    }

    public ClientType Client { get; }
    public MessageCatalog? Catalog { get; }
    public CatalogProvenance Provenance { get; }
    public ClientBuildIdentity? Build { get; }
    public CatalogSupplement? Supplement { get; }
}

public readonly record struct SessionCatalogLease(long Value)
{
    public bool IsEmpty => Value == 0;
}
