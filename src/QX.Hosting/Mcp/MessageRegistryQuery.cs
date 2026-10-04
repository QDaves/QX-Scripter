using Qx.Protocol;

namespace Qx.Hosting;

internal static class MessageRegistryQuery
{
    public static MessageRegistrySnapshot Read(
        MessageManager messages,
        string query,
        string direction,
        bool explicit_only,
        bool resolved_only,
        int limit,
        int offset,
        IReadOnlyDictionary<MessageKey, MessageRegistryMembers> members)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        MessageDirection? direction_filter = ParseDirection(direction);
        string search = query.Trim();
        SessionCatalogBinding? binding = messages.ActiveCatalogBinding;

        IEnumerable<MessageDescriptor> filtered = messages.Registry.Descriptors;
        if (explicit_only)
            filtered = filtered.Where(descriptor => descriptor.HasExplicitKey);
        if (direction_filter is { } selected_direction)
            filtered = filtered.Where(descriptor => descriptor.Direction == selected_direction);
        if (search.Length != 0)
        {
            filtered = filtered.Where(descriptor =>
                descriptor.Key.Value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                descriptor.Names.Any(name => name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (members.GetValueOrDefault(descriptor.Key)?.Model is { } model &&
                    model.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }
        MessageProjection[] registered = filtered
            .Select(descriptor => Registered(
                descriptor,
                ActiveBinding(binding, descriptor),
                members.GetValueOrDefault(descriptor.Key)))
            .Where(projection => !resolved_only || projection.Entry.Active.Resolved)
            .ToArray();
        MessageProjection[] unmapped = explicit_only
            ? []
            : Unmapped(
                messages.Registry,
                binding,
                direction_filter,
                search);
        MessageProjection[] matched = registered
            .Concat(unmapped)
            .OrderBy(projection => projection.Direction)
            .ThenBy(projection => projection.Entry.Key, StringComparer.Ordinal)
            .ToArray();
        MessageRegistryEntry[] entries = matched
            .Skip(offset)
            .Take(limit)
            .Select(projection => projection.Entry)
            .ToArray();

        return new MessageRegistrySnapshot(
            "protocol_messages",
            new MessageRegistrySummary(
                messages.Registry.Count,
                messages.Registry.NameCount,
                messages.Registry.Descriptors.Count(descriptor => descriptor.HasExplicitKey)),
            new MessageRegistrySession(
                binding?.Catalog is not null,
                binding?.Provenance.Origin.ToString(),
                binding?.Provenance.Source,
                binding?.Provenance.ClientVersion,
                binding?.Catalog?.HeaderCount ?? 0,
                binding?.Provenance.SourceSha256,
                binding?.Catalog?.BuildFingerprint),
            new MessageRegistryFilters(
                search,
                direction_filter is null ? "both" : DirectionName(direction_filter.Value),
                explicit_only,
                resolved_only),
            matched.Length,
            offset,
            limit,
            entries);
    }

    private static MessageProjection Registered(
        MessageDescriptor descriptor,
        MessageRegistryActiveBinding active,
        MessageRegistryMembers? members)
    {
        return new MessageProjection(
            descriptor.Direction,
            new MessageRegistryEntry(
                descriptor.Key.Value,
                DirectionName(descriptor.Direction),
                descriptor.HasExplicitKey,
                descriptor.HasExplicitKey ? "semantic" : "legacy",
                members?.Model?.FullName?.Replace('+', '.'),
                members?.KeyMember,
                members?.Contract,
                descriptor.Name,
                descriptor.Names,
                active));
    }

    private static MessageProjection[] Unmapped(
        MessageRegistry registry,
        SessionCatalogBinding? binding,
        MessageDirection? direction_filter,
        string search)
    {
        if (binding?.Catalog is not { } catalog)
            return [];

        var mapped = new HashSet<(MessageDirection Direction, int Id)>();
        foreach (MessageDescriptor descriptor in registry.Descriptors)
        {
            MessageRegistryActiveBinding active = ActiveBinding(binding, descriptor);
            foreach (int header in active.Headers)
                mapped.Add((descriptor.Direction, header));
        }

        return catalog.Headers
            .Where(header => !mapped.Contains((header.Direction, header.Id)))
            .Where(header => direction_filter is null || header.Direction == direction_filter)
            .Where(header => search.Length == 0 ||
                header.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                header.Id.ToString().Contains(search, StringComparison.Ordinal))
            .Select(Unmapped)
            .ToArray();
    }

    private static MessageProjection Unmapped(MessageCatalogHeader header)
    {
        int[] headers = [header.Id];
        return new MessageProjection(
            header.Direction,
            new MessageRegistryEntry(
                $"unmapped.{DirectionName(header.Direction)}.{header.Id}",
                DirectionName(header.Direction),
                false,
                "unmapped",
                null,
                null,
                null,
                header.Name,
                [header.Name],
                new MessageRegistryActiveBinding(
                    true,
                    headers,
                    [new MessageRegistryNameBinding(header.Name, headers)])));
    }

    private static MessageRegistryActiveBinding ActiveBinding(
        SessionCatalogBinding? binding,
        MessageDescriptor descriptor)
    {
        var evidence = new List<MessageRegistryNameBinding>();

        foreach (string name in descriptor.Names)
        {
            int[] headers = ResolveNameHeaders(
                binding,
                descriptor.Direction,
                name);
            if (headers.Length != 0)
                evidence.Add(new MessageRegistryNameBinding(name, headers));
        }

        int[] resolved_headers = evidence
            .SelectMany(item => item.Headers)
            .Distinct()
            .Order()
            .ToArray();
        return new MessageRegistryActiveBinding(
            resolved_headers.Length != 0,
            resolved_headers,
            evidence);
    }

    private static int[] ResolveNameHeaders(
        SessionCatalogBinding? binding,
        MessageDirection direction,
        string name)
    {
        if (binding?.Catalog is not { } catalog ||
            !catalog.TryGetIds(direction, name, out IReadOnlyList<short> ids))
        {
            return [];
        }
        return HeaderIds(ids);
    }

    private static int[] HeaderIds(IEnumerable<short> headers) => headers
        .Select(header => (int)unchecked((ushort)header))
        .Distinct()
        .Order()
        .ToArray();

    private static MessageDirection? ParseDirection(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" or "both" or "all" => null,
        "in" or "incoming" => MessageDirection.In,
        "out" or "outgoing" => MessageDirection.Out,
        _ => throw new ArgumentException("'direction' must be in, out, or both.", nameof(value))
    };

    private static string DirectionName(MessageDirection direction) => direction switch
    {
        MessageDirection.In => "in",
        MessageDirection.Out => "out",
        _ => "none"
    };

    private sealed record MessageProjection(
        MessageDirection Direction,
        MessageRegistryEntry Entry);
}

internal sealed record MessageRegistrySnapshot(
    string Query,
    MessageRegistrySummary Registry,
    MessageRegistrySession Session,
    MessageRegistryFilters Filters,
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<MessageRegistryEntry> Entries);

internal sealed record MessageRegistrySummary(
    int Descriptors,
    int Names,
    int ExplicitDescriptors);

internal sealed record MessageRegistrySession(
    bool CatalogBound,
    string? CatalogOrigin,
    string? CatalogSource,
    string? ClientVersion,
    int CatalogHeaders,
    string? SourceFingerprint,
    string? CatalogFingerprint);

internal sealed record MessageRegistryFilters(
    string Query,
    string Direction,
    bool ExplicitOnly,
    bool ResolvedOnly);

internal sealed record MessageRegistryEntry(
    string Key,
    string Direction,
    bool Stable,
    string KeyKind,
    string? Model,
    string? KeyMember,
    string? Contract,
    string Name,
    IReadOnlyList<string> Names,
    MessageRegistryActiveBinding Active);

internal sealed record MessageRegistryMembers(
    string KeyMember,
    Type? Model,
    string? Contract);

internal sealed record MessageRegistryActiveBinding(
    bool Resolved,
    IReadOnlyList<int> Headers,
    IReadOnlyList<MessageRegistryNameBinding> Evidence);

internal sealed record MessageRegistryNameBinding(
    string Name,
    IReadOnlyList<int> Headers);
