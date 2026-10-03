namespace Qx.Protocol;

/// <summary>Represents the message names that a session catalog took from a fallback catalog of the same client build.</summary>
public sealed record CatalogSupplement
{
    /// <summary>Initializes a new instance of the <see cref="CatalogSupplement"/> record.</summary>
    /// <param name="provenance">The provenance of the fallback catalog, which must come from <see cref="CatalogOrigin.GEarthHandshake"/>.</param>
    /// <param name="aliasCount">The number of message names taken from the fallback catalog, greater than 0.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provenance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="provenance"/> has another origin.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="aliasCount"/> is 0 or less.</exception>
    public CatalogSupplement(CatalogProvenance provenance, int aliasCount)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.Origin is not CatalogOrigin.GEarthHandshake)
            throw new ArgumentException("A catalog supplement requires fallback provenance.", nameof(provenance));
        if (aliasCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(aliasCount));
        Provenance = provenance;
        AliasCount = aliasCount;
    }

    /// <summary>Gets the provenance of the fallback catalog.</summary>
    public CatalogProvenance Provenance { get; }
    /// <summary>Gets the number of message names taken from the fallback catalog.</summary>
    public int AliasCount { get; }
}

/// <summary>Represents the message catalog bound to a hotel session, with its provenance and supplement.</summary>
public sealed record SessionCatalogBinding
{
    /// <summary>Initializes a new instance of the <see cref="SessionCatalogBinding"/> record with a read-only snapshot of the catalog.</summary>
    /// <remarks>
    /// An extracted catalog must match the source hash of its provenance, and a supplement must have the same
    /// client version as the provenance.
    /// </remarks>
    /// <param name="catalog">The message catalog, which is <see langword="null"/> only when the origin is <see cref="CatalogOrigin.Unavailable"/>.</param>
    /// <param name="provenance">The provenance of the catalog.</param>
    /// <param name="supplement">The names added to the catalog from a fallback catalog, or <see langword="null"/> when there are none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provenance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the catalog, provenance or supplement do not match each other.</exception>
    public SessionCatalogBinding(
        MessageCatalog? catalog,
        CatalogProvenance provenance,
        CatalogSupplement? supplement = null)
    {
        ArgumentNullException.ThrowIfNull(provenance);
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
        if (supplement is not null &&
            (catalog is null ||
             provenance.ClientVersion is null ||
             !string.Equals(
                 supplement.Provenance.ClientVersion,
                 provenance.ClientVersion,
                 StringComparison.Ordinal)))
        {
            throw new ArgumentException("The catalog supplement does not match the bound client.", nameof(supplement));
        }
        Catalog = catalog?.Snapshot();
        Provenance = provenance;
        Supplement = supplement;
    }

    /// <summary>Gets a read-only snapshot of the message catalog, or <see langword="null"/> when no catalog is available.</summary>
    public MessageCatalog? Catalog { get; }
    /// <summary>Gets the provenance of the catalog.</summary>
    public CatalogProvenance Provenance { get; }
    /// <summary>Gets the names added to the catalog from a fallback catalog, or <see langword="null"/> when there are none.</summary>
    public CatalogSupplement? Supplement { get; }
}

/// <summary>Represents the lease on a bound session catalog, used to clear that binding later.</summary>
/// <param name="Value">The generation number of the binding, or 0 for an empty lease.</param>
public readonly record struct SessionCatalogLease(long Value)
{
    /// <summary>Gets whether the lease is empty, which is the case for the <see langword="default"/> value.</summary>
    public bool IsEmpty => Value == 0;
}
