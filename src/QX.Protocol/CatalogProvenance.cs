namespace Qx.Protocol;

/// <summary>Specifies where a message catalog came from.</summary>
public enum CatalogOrigin
{
    /// <summary>The catalog was extracted from the files of the client build.</summary>
    ClientExtraction,
    /// <summary>The catalog came from the G-Earth connection handshake.</summary>
    GEarthHandshake,
    /// <summary>The catalog is a reference catalog embedded in the application.</summary>
    EmbeddedReference,
    /// <summary>No catalog is available.</summary>
    Unavailable
}

/// <summary>Represents the origin, source and client build of a message catalog.</summary>
public sealed record CatalogProvenance
{
    /// <summary>Initializes a new instance of the <see cref="CatalogProvenance"/> record with validated and trimmed values.</summary>
    /// <param name="origin">The origin of the catalog.</param>
    /// <param name="source">The source of the catalog, such as a file path or <c>G-Earth</c>, at most 1024 characters.</param>
    /// <param name="clientVersion">The client build version, at most 256 characters, or <see langword="null"/> when not known.</param>
    /// <param name="sourceSha256">The SHA-256 hash of the source as 64 hexadecimal characters, or <see langword="null"/> when not known.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="origin"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> or <paramref name="clientVersion"/> is blank, too long or contains control characters, or <paramref name="sourceSha256"/> is not 64 hexadecimal characters.</exception>
    public CatalogProvenance(
        CatalogOrigin origin,
        string source,
        string? clientVersion = null,
        string? sourceSha256 = null)
    {
        if (!Enum.IsDefined(origin))
            throw new ArgumentOutOfRangeException(nameof(origin));
        string normalized_source = Normalize(source, nameof(source), 1024);
        string? normalized_version = clientVersion is null
            ? null
            : Normalize(clientVersion, nameof(clientVersion), 256);
        if (sourceSha256 is not null &&
            (sourceSha256.Length != 64 || sourceSha256.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("The catalog source hash is invalid.", nameof(sourceSha256));
        }
        Origin = origin;
        Source = normalized_source;
        ClientVersion = normalized_version;
        SourceSha256 = sourceSha256?.ToUpperInvariant();
    }

    /// <summary>Gets the origin of the catalog.</summary>
    public CatalogOrigin Origin { get; }
    /// <summary>Gets the trimmed source of the catalog, such as a file path or <c>G-Earth</c>.</summary>
    public string Source { get; }
    /// <summary>Gets the trimmed client build version, or <see langword="null"/> when not known.</summary>
    public string? ClientVersion { get; }
    /// <summary>Gets the SHA-256 hash of the source in upper-case hexadecimal, or <see langword="null"/> when not known.</summary>
    public string? SourceSha256 { get; }

    static string Normalize(string value, string parameter_name, int maximum_length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter_name);
        string normalized = value.Trim();
        if (normalized.Length > maximum_length || normalized.Any(char.IsControl))
            throw new ArgumentException("The catalog provenance value is invalid.", parameter_name);
        return normalized;
    }
}
