using System.Collections.Concurrent;
using Qx.Headers.Flash;
using Qx.Protocol;

namespace Qx.ClientCatalog;

public sealed class PreparedSessionCatalogSelector : ISessionCatalogSelector
{
    readonly Func<ClientType, IReadOnlyList<PreparedHeaderCatalog>> _current_catalogs;
    readonly ConcurrentDictionary<string, MessageCatalog> _converted = new(StringComparer.Ordinal);
    readonly MessageRegistry? _registry;

    public PreparedSessionCatalogSelector(HeaderCatalogCoordinator catalogs)
        : this(
            (catalogs ?? throw new ArgumentNullException(nameof(catalogs))).CurrentCatalogs,
            null)
    {
    }

    public PreparedSessionCatalogSelector(
        HeaderCatalogCoordinator catalogs,
        MessageRegistry registry)
        : this(
            (catalogs ?? throw new ArgumentNullException(nameof(catalogs))).CurrentCatalogs,
            registry)
    {
    }

    internal PreparedSessionCatalogSelector(
        Func<ClientType, IReadOnlyList<PreparedHeaderCatalog>> current_catalogs,
        MessageRegistry? registry)
    {
        _current_catalogs = current_catalogs ?? throw new ArgumentNullException(nameof(current_catalogs));
        _registry = registry;
    }

    public SessionCatalogBinding? Select(SessionCatalogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PreparedHeaderCatalog? selected = request.Client switch
        {
            ClientCatalogClients.Flash => SelectFlash(request.HotelVersion),
            ClientCatalogClients.Unity => SelectUnity(request),
            _ => null
        };
        if (selected is null)
            return null;

        MessageCatalog catalog = Catalog(selected);
        CatalogSupplement? supplement = null;
        if (request.Client == ClientCatalogClients.Flash)
            catalog = EnrichFlashCatalog(request, selected, catalog, out supplement);
        if (request.Intent == SessionCatalogSelectionIntent.CatalogReady &&
            request.Client == ClientCatalogClients.Unity &&
            !CanRefreshUnity(request, selected, catalog))
        {
            return null;
        }
        string client_version = request.Client == ClientCatalogClients.Flash
            ? selected.Catalog.ClientBuildIds[0]
            : selected.Candidate.Version;
        return new SessionCatalogBinding(
            request.Client,
            catalog,
            new CatalogProvenance(
                CatalogOrigin.ClientExtraction,
                request.Client,
                selected.SourcePath,
                client_version,
                selected.Key.SourceSha256),
            supplement: supplement);
    }

    MessageCatalog EnrichFlashCatalog(
        SessionCatalogRequest request,
        PreparedHeaderCatalog selected,
        MessageCatalog catalog,
        out CatalogSupplement? supplement)
    {
        supplement = null;
        SessionCatalogBinding fallback_binding = request.Fallback;
        MessageCatalog? fallback = fallback_binding.Catalog;
        if (_registry is null ||
            fallback_binding.Provenance.Origin != CatalogOrigin.GEarthHandshake ||
            fallback_binding.Client != ClientCatalogClients.Flash ||
            fallback is null ||
            !string.Equals(
                fallback_binding.Provenance.ClientVersion,
                request.HotelVersion,
                StringComparison.Ordinal) ||
            selected.Catalog.ClientBuildIds.Count != 1 ||
            !selected.Catalog.ClientBuildIds[0].Equals(request.HotelVersion, StringComparison.Ordinal) ||
            !CoversFlashBuild(catalog, fallback))
        {
            return catalog;
        }

        var candidates = new List<FlashAlias>();
        foreach (MessageCatalogHeader header in fallback.Headers)
        {
            if (!catalog.TryGetName(
                    header.Direction,
                    unchecked((short)header.Id),
                    out string primary) ||
                !IsObfuscatedFlashName(primary))
            {
                continue;
            }

            string name = FlashHeaderNameResolver.Strip(header.Name);
            if (IsObfuscatedFlashName(name) ||
                !_registry.TryGet(
                    ClientCatalogClients.Flash,
                    header.Direction,
                    name,
                    out MessageDescriptor descriptor) ||
                !descriptor.HasExplicitKey ||
                DescriptorResolved(catalog, descriptor))
            {
                continue;
            }
            candidates.Add(new FlashAlias(descriptor, header.Direction, header.Id, name));
        }

        FlashAlias[] aliases = candidates
            .GroupBy(alias => alias.Descriptor.Key)
            .Where(group => group.Select(alias => alias.Id).Distinct().Count() == 1)
            .Select(group => group.First())
            .ToArray();
        if (aliases.Length == 0)
            return catalog;

        MessageCatalog enriched = ClientCatalogFactory.Create(selected);
        foreach (FlashAlias alias in aliases)
            enriched.AddAlias(alias.Direction, alias.Id, alias.Name);
        supplement = new CatalogSupplement(fallback_binding.Provenance, aliases.Length);
        return enriched;
    }

    static bool DescriptorResolved(
        MessageCatalog catalog,
        MessageDescriptor descriptor) =>
        descriptor.NamesFor(ClientCatalogClients.Flash).Any(name =>
            catalog.TryGetIds(descriptor.Direction, name, out IReadOnlyList<short> ids) &&
            ids.Count != 0);

    static bool CoversFlashBuild(MessageCatalog catalog, MessageCatalog fallback)
    {
        if (catalog.HeaderCount < 64 ||
            fallback.HeaderCount < 64 ||
            fallback.HeaderCount > catalog.HeaderCount)
        {
            return false;
        }

        int matching = fallback.Headers.Count(header =>
            catalog.TryGetName(header.Direction, unchecked((short)header.Id), out _));
        return matching == fallback.HeaderCount &&
            matching * 20 >= catalog.HeaderCount * 19;
    }

    static bool IsObfuscatedFlashName(string name) =>
        string.IsNullOrWhiteSpace(name) ||
        name.StartsWith("_-", StringComparison.Ordinal) ||
        name.StartsWith("§_-", StringComparison.Ordinal);

    readonly record struct FlashAlias(
        MessageDescriptor Descriptor,
        Direction Direction,
        int Id,
        string Name);

    PreparedHeaderCatalog? SelectFlash(string hotel_version)
    {
        if (string.IsNullOrEmpty(hotel_version))
            return null;
        PreparedHeaderCatalog[] matches = DistinctSources(
                _current_catalogs(ClientCatalogClients.Flash),
                true)
            .Where(prepared =>
                prepared.Catalog.ClientBuildIds.Count == 1 &&
                prepared.Catalog.ClientBuildIds[0].Equals(hotel_version, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    PreparedHeaderCatalog? SelectUnity(SessionCatalogRequest request)
    {
        PreparedHeaderCatalog[] candidates = DistinctSources(
            _current_catalogs(ClientCatalogClients.Unity),
            false);
        if (candidates.Length == 0)
            return null;

        string? release = UnityRelease(request.HotelVersion) ?? UnityRelease(request.ClientIdentifier);
        if (release is not null)
        {
            candidates = candidates
                .Where(value => value.Candidate.Version.Equals(release, StringComparison.Ordinal))
                .ToArray();
            return candidates.Length == 1 ? candidates[0] : null;
        }
        if (request.Fallback.Catalog is not { } fallback)
            return null;
        PreparedHeaderCatalog[] compatible = candidates
            .Where(value => IsCompatibleUnityCatalog(fallback, Catalog(value)))
            .OrderByDescending(value => Catalog(value).MatchingHeaders(fallback))
            .ThenByDescending(value => VersionRank(value.Candidate.Version))
            .ThenByDescending(value => value.Candidate.LastModified)
            .ThenBy(value => value.NormalizedPath, PathComparer())
            .ToArray();
        return compatible.FirstOrDefault();
    }

    static bool CanRefreshUnity(
        SessionCatalogRequest request,
        PreparedHeaderCatalog selected,
        MessageCatalog catalog)
    {
        string? release = UnityRelease(request.HotelVersion) ?? UnityRelease(request.ClientIdentifier);
        if (release is not null)
            return selected.Candidate.Version.Equals(release, StringComparison.Ordinal);
        return request.Fallback.Catalog is { } fallback && IsCompatibleUnityCatalog(fallback, catalog);
    }

    static bool IsCompatibleUnityCatalog(MessageCatalog fallback, MessageCatalog candidate)
    {
        if (fallback.HeaderCount < 64 ||
            candidate.HeaderCount < fallback.HeaderCount)
        {
            return false;
        }
        MessageCatalogHeader[] stable = fallback.Headers
            .Where(header => !IsObfuscatedUnityName(header.Name))
            .ToArray();
        if (stable.Length < 64)
            return false;
        int matching = stable.Count(header =>
            candidate.TryGetIds(header.Direction, header.Name, out IReadOnlyList<short> ids) &&
            ids.Contains(unchecked((short)header.Id)));
        return matching * 20 >= stable.Length * 19;
    }

    static bool IsObfuscatedUnityName(string name) =>
        name.Length >= 20 && name.All(character => character is >= 'A' and <= 'D');

    static string? UnityRelease(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string[] parts = value.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !parts[0].StartsWith("UNITY", StringComparison.OrdinalIgnoreCase) ||
            !long.TryParse(parts[1], out _))
        {
            return null;
        }
        return parts[1];
    }

    static PreparedHeaderCatalog[] DistinctSources(
        IReadOnlyList<PreparedHeaderCatalog> catalogs,
        bool require_consistent_build_ids)
    {
        var results = new List<PreparedHeaderCatalog>();
        foreach (IGrouping<string, PreparedHeaderCatalog> group in catalogs
                     .GroupBy(value => value.Key.SourceSha256, StringComparer.Ordinal))
        {
            PreparedHeaderCatalog[] values = group.ToArray();
            if (require_consistent_build_ids && values
                .Skip(1)
                .Any(value => !value.Catalog.ClientBuildIds.SequenceEqual(
                    values[0].Catalog.ClientBuildIds,
                    StringComparer.Ordinal)))
            {
                continue;
            }
            results.Add(values
                .OrderByDescending(value => value.Candidate.LastModified)
                .ThenByDescending(value => VersionRank(value.Candidate.Version))
                .ThenBy(value => value.NormalizedPath, PathComparer())
                .ThenBy(value => value.Key.Fingerprint, StringComparer.Ordinal)
                .First());
        }
        return results.ToArray();
    }

    static long VersionRank(string value) => long.TryParse(value, out long version) ? version : -1;

    static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    MessageCatalog Catalog(PreparedHeaderCatalog prepared) =>
        _converted.GetOrAdd(prepared.Key.Fingerprint, _ => ClientCatalogFactory.Create(prepared));
}
