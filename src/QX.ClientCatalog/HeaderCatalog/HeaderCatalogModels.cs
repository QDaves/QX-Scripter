using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Qx.Messages;

namespace Qx.ClientCatalog;

public sealed record HeaderCatalogKey
{
    public HeaderCatalogKey(
        string sourceSha256,
        string nameDatabaseSha256,
        string extractorRevision,
        HeaderCatalogProvenance provenance)
    {
        SourceSha256 = NormalizeHash(sourceSha256, nameof(sourceSha256));
        NameDatabaseSha256 = NormalizeHash(nameDatabaseSha256, nameof(nameDatabaseSha256));
        ExtractorRevision = NormalizeText(extractorRevision, nameof(extractorRevision), 256);
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Fingerprint = CreateFingerprint();
    }

    public string SourceSha256 { get; }
    public string NameDatabaseSha256 { get; }
    public string ExtractorRevision { get; }
    public HeaderCatalogProvenance Provenance { get; }
    public string Fingerprint { get; }

    string CreateFingerprint()
    {
        string identity = string.Join(
            '\n',
            HeaderCatalogStore.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            HeaderCatalogStore.ClientName,
            SourceSha256,
            NameDatabaseSha256,
            ExtractorRevision,
            Provenance.ClientVersion,
            Provenance.Source,
            Provenance.SourceRevision is null
                ? "0"
                : $"1:{Provenance.SourceRevision}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    internal static string NormalizeHash(string value, string parameter_name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter_name);
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A SHA-256 value must contain exactly 64 hexadecimal characters.", parameter_name);
        return normalized;
    }

    internal static string NormalizeText(string value, string parameter_name, int maximum_length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter_name);
        string normalized = value.Trim();
        if (normalized.Length > maximum_length || normalized.Any(char.IsControl))
            throw new ArgumentException("The value contains unsupported characters or exceeds its size limit.", parameter_name);
        return normalized;
    }
}

public sealed record HeaderCatalogProvenance
{
    public HeaderCatalogProvenance(
        string clientVersion,
        string source,
        string? sourceRevision = null)
    {
        ClientVersion = HeaderCatalogKey.NormalizeText(clientVersion, nameof(clientVersion), 256);
        Source = HeaderCatalogKey.NormalizeText(source, nameof(source), 1024);
        SourceRevision = sourceRevision is null
            ? null
            : HeaderCatalogKey.NormalizeText(sourceRevision, nameof(sourceRevision), 512);
    }

    public string ClientVersion { get; }
    public string Source { get; }
    public string? SourceRevision { get; }
}

public sealed record HeaderCatalogEntry
{
    public HeaderCatalogEntry(
        MessageDirection direction,
        ushort headerId,
        string? name,
        IEnumerable<string>? aliases = null)
    {
        if (direction is not (MessageDirection.In or MessageDirection.Out))
            throw new ArgumentOutOfRangeException(nameof(direction));
        Direction = direction;
        HeaderId = headerId;
        Name = name is null
            ? null
            : HeaderCatalogKey.NormalizeText(name, nameof(name), 512);
        string[] source_aliases = (aliases ?? []).ToArray();
        for (int index = 0; index < source_aliases.Length; index++)
        {
            if (source_aliases[index] is null)
                throw new ArgumentException($"Header alias at index {index} is null.", nameof(aliases));
        }
        string[] normalized_aliases = source_aliases
            .Select(alias => HeaderCatalogKey.NormalizeText(alias, nameof(aliases), 512))
            .Where(alias => !alias.Equals(Name, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized_aliases.Length > 64)
            throw new ArgumentException("A header cannot contain more than 64 aliases.", nameof(aliases));
        Aliases = Array.AsReadOnly(normalized_aliases);
    }

    public MessageDirection Direction { get; }
    public ushort HeaderId { get; }
    public string? Name { get; }
    public ReadOnlyCollection<string> Aliases { get; }
}

public sealed record HeaderCatalogSnapshot
{
    public HeaderCatalogSnapshot(
        HeaderCatalogProvenance provenance,
        IEnumerable<HeaderCatalogEntry> entries,
        IEnumerable<string>? clientBuildIds = null,
        FlashMarketplaceWireLayout flashMarketplaceLayout =
            FlashMarketplaceWireLayout.Unknown)
    {
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        ArgumentNullException.ThrowIfNull(entries);
        HeaderCatalogEntry[] source_entries = entries.ToArray();
        if (source_entries.Length == 0)
            throw new ArgumentException("The header catalog cannot be empty.", nameof(entries));
        for (int index = 0; index < source_entries.Length; index++)
        {
            if (source_entries[index] is null)
                throw new ArgumentException($"Header catalog entry at index {index} is null.", nameof(entries));
        }
        HeaderCatalogEntry[] normalized_entries = source_entries
            .OrderBy(entry => entry.Direction == MessageDirection.In ? 0 : 1)
            .ThenBy(entry => entry.HeaderId)
            .ToArray();
        if (normalized_entries.Length > 131072)
            throw new ArgumentException("The header catalog exceeds its entry limit.", nameof(entries));
        if (normalized_entries
            .GroupBy(entry => (entry.Direction, entry.HeaderId))
            .Any(group => group.Count() != 1))
        {
            throw new ArgumentException("The header catalog contains duplicate direction and ID pairs.", nameof(entries));
        }
        string[] source_build_ids = (clientBuildIds ?? []).ToArray();
        for (int index = 0; index < source_build_ids.Length; index++)
        {
            if (source_build_ids[index] is null)
                throw new ArgumentException($"Client build ID at index {index} is null.", nameof(clientBuildIds));
        }
        string[] normalized_build_ids = source_build_ids
            .Select(value => HeaderCatalogKey.NormalizeText(value, nameof(clientBuildIds), 256))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized_build_ids.Length > 32)
            throw new ArgumentException("The header catalog contains too many client build IDs.", nameof(clientBuildIds));
        if (!Enum.IsDefined(flashMarketplaceLayout))
            throw new ArgumentOutOfRangeException(nameof(flashMarketplaceLayout));
        Entries = Array.AsReadOnly(normalized_entries);
        ClientBuildIds = Array.AsReadOnly(normalized_build_ids);
        FlashMarketplaceLayout = flashMarketplaceLayout;
    }

    public HeaderCatalogProvenance Provenance { get; }
    public ReadOnlyCollection<HeaderCatalogEntry> Entries { get; }
    public ReadOnlyCollection<string> ClientBuildIds { get; }
    public FlashMarketplaceWireLayout FlashMarketplaceLayout { get; }
}

public enum HeaderCatalogCacheState
{
    Hit,
    Created,
    Rebuilt
}

public sealed record HeaderCatalogCacheResult(
    HeaderCatalogSnapshot Catalog,
    HeaderCatalogCacheState State,
    string ContentSha256);
