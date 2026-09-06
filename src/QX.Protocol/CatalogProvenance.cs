using Qx;

namespace Qx.Protocol;

public enum CatalogOrigin
{
    ClientExtraction,
    GEarthHandshake,
    Sulek,
    EmbeddedReference,
    Unavailable
}

public sealed record CatalogProvenance
{
    public CatalogProvenance(
        CatalogOrigin origin,
        ClientType client,
        string source,
        string? client_version = null,
        string? source_sha256 = null)
    {
        if (!Enum.IsDefined(origin))
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (!ClientTypes.IsSupported(client))
            throw new ArgumentOutOfRangeException(nameof(client));
        string normalized_source = Normalize(source, nameof(source), 1024);
        string? normalized_version = client_version is null
            ? null
            : Normalize(client_version, nameof(client_version), 256);
        if (source_sha256 is not null &&
            (source_sha256.Length != 64 || source_sha256.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("The catalog source hash is invalid.", nameof(source_sha256));
        }
        Origin = origin;
        Client = client;
        Source = normalized_source;
        ClientVersion = normalized_version;
        SourceSha256 = source_sha256?.ToUpperInvariant();
    }

    public CatalogOrigin Origin { get; }
    public ClientType Client { get; }
    public string Source { get; }
    public string? ClientVersion { get; }
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
