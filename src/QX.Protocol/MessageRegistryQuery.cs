namespace Qx.Protocol;

public static class MessageRegistryQuery
{
    public static MessageRegistrySnapshot Read(
        MessageManager messages,
        string query,
        string direction,
        string client,
        bool explicit_only,
        bool resolved_only,
        int limit,
        int offset)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 500);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        Direction? direction_filter = ParseDirection(direction);
        ClientType? client_filter = ParseClient(client);
        string search = query.Trim();
        SessionCatalogBinding? binding = messages.ActiveCatalogBinding;
        ClientType active_client = binding?.Client ?? messages.ActiveClient;

        IEnumerable<MessageDescriptor> filtered = messages.Registry.Descriptors;
        if (explicit_only)
            filtered = filtered.Where(descriptor => descriptor.HasExplicitKey);
        if (direction_filter is { } selected_direction)
            filtered = filtered.Where(descriptor => descriptor.Direction == selected_direction);
        if (client_filter is { } selected_client)
            filtered = filtered.Where(descriptor => descriptor.NamesFor(selected_client).Count != 0);
        if (search.Length != 0)
        {
            filtered = filtered.Where(descriptor =>
                descriptor.Key.Value.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                descriptor.Aliases.Any(alias => alias.Name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }
        MessageProjection[] registered = filtered
            .Select(descriptor => Registered(
                descriptor,
                ActiveBinding(binding, descriptor, active_client)))
            .Where(projection => !resolved_only || projection.Entry.Active.Resolved)
            .ToArray();
        MessageProjection[] unmapped = explicit_only
            ? []
            : Unmapped(
                messages.Registry,
                binding,
                active_client,
                direction_filter,
                client_filter,
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
                messages.Registry.AliasCount,
                messages.Registry.Descriptors.Count(descriptor => descriptor.HasExplicitKey)),
            new MessageRegistrySession(
                ClientName(active_client),
                binding?.Catalog is not null,
                binding?.Provenance.Origin.ToString(),
                binding?.Provenance.Source,
                binding?.Provenance.ClientVersion,
                binding?.Catalog?.HeaderCount ?? 0,
                binding?.Provenance.SourceSha256,
                binding?.Build?.CatalogFingerprint ?? binding?.Catalog?.BuildFingerprint,
                binding?.Build?.SchemaFingerprint ?? binding?.Catalog?.SchemaFingerprint),
            new MessageRegistryFilters(
                search,
                direction_filter is null ? "both" : DirectionName(direction_filter.Value),
                client_filter is null ? "all" : ClientName(client_filter.Value),
                explicit_only,
                resolved_only),
            matched.Length,
            offset,
            limit,
            entries);
    }

    private static MessageProjection Registered(
        MessageDescriptor descriptor,
        MessageRegistryActiveBinding active)
    {
        return new MessageProjection(
            descriptor.Direction,
            new MessageRegistryEntry(
                descriptor.Key.Value,
                DirectionName(descriptor.Direction),
                descriptor.HasExplicitKey,
                descriptor.HasExplicitKey ? "semantic" : "legacy",
                new MessageRegistryDialects(
                    ProtocolDialect(descriptor, ProtocolClients.Flash),
                    ProtocolDialect(descriptor, ProtocolClients.Unity)),
                active));
    }

    private static MessageProjection[] Unmapped(
        MessageRegistry registry,
        SessionCatalogBinding? binding,
        ClientType active_client,
        Direction? direction_filter,
        ClientType? client_filter,
        string search)
    {
        if (active_client is not (ProtocolClients.Flash or ProtocolClients.Unity) ||
            binding?.Client != active_client ||
            binding.Catalog is not { } catalog ||
            client_filter is { } selected_client && selected_client != active_client)
        {
            return [];
        }

        var mapped = new HashSet<(Direction Direction, int Id)>();
        foreach (MessageDescriptor descriptor in registry.Descriptors)
        {
            MessageRegistryActiveBinding active = ActiveBinding(binding, descriptor, active_client);
            foreach (int header in active.Headers)
                mapped.Add((descriptor.Direction, header));
        }

        return catalog.Headers
            .Where(header => !mapped.Contains((header.Direction, header.Id)))
            .Where(header => direction_filter is null || header.Direction == direction_filter)
            .Where(header => search.Length == 0 ||
                header.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                header.Id.ToString().Contains(search, StringComparison.Ordinal))
            .Select(header => Unmapped(header, active_client))
            .ToArray();
    }

    private static MessageProjection Unmapped(
        MessageCatalogHeader header,
        ClientType active_client)
    {
        var dialect = new MessageRegistryDialect(true, header.Name, [header.Name]);
        var unavailable = new MessageRegistryDialect(false, null, []);
        int[] headers = [header.Id];
        return new MessageProjection(
            header.Direction,
            new MessageRegistryEntry(
                $"unmapped.{DirectionName(header.Direction)}.{header.Id}",
                DirectionName(header.Direction),
                false,
                "unmapped",
                active_client == ProtocolClients.Flash
                    ? new MessageRegistryDialects(dialect, unavailable)
                    : new MessageRegistryDialects(unavailable, dialect),
                new MessageRegistryActiveBinding(
                    ClientName(active_client),
                    true,
                    true,
                    headers,
                    [new MessageRegistryAliasBinding(header.Name, headers)])));
    }

    private static MessageRegistryActiveBinding ActiveBinding(
        SessionCatalogBinding? binding,
        MessageDescriptor descriptor,
        ClientType active_client)
    {
        IReadOnlyList<string> aliases = active_client == ClientType.None
            ? []
            : descriptor.NamesFor(active_client);
        var evidence = new List<MessageRegistryAliasBinding>();

        foreach (string alias in aliases)
        {
            int[] headers = ResolveAliasHeaders(
                binding,
                active_client,
                descriptor.Direction,
                alias);
            if (headers.Length != 0)
                evidence.Add(new MessageRegistryAliasBinding(alias, headers));
        }

        int[] resolved_headers = evidence
            .SelectMany(item => item.Headers)
            .Distinct()
            .Order()
            .ToArray();
        return new MessageRegistryActiveBinding(
            ClientName(active_client),
            aliases.Count != 0,
            resolved_headers.Length != 0,
            resolved_headers,
            evidence);
    }

    private static int[] ResolveAliasHeaders(
        SessionCatalogBinding? binding,
        ClientType active_client,
        Direction direction,
        string alias)
    {
        if (active_client == ClientType.None ||
            binding?.Client != active_client ||
            binding.Catalog is null ||
            !binding.Catalog.TryGetIds(direction, alias, out IReadOnlyList<short> ids))
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

    private static MessageRegistryDialect ProtocolDialect(
        MessageDescriptor descriptor,
        ClientType client)
    {
        IReadOnlyList<string> aliases = descriptor.NamesFor(client);
        return new MessageRegistryDialect(aliases.Count != 0, descriptor.NameFor(client), aliases);
    }

    private static Direction? ParseDirection(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" or "both" or "all" => null,
        "in" or "incoming" => Direction.In,
        "out" or "outgoing" => Direction.Out,
        _ => throw new ArgumentException("'direction' must be in, out, or both.", nameof(value))
    };

    private static ClientType? ParseClient(string value) => value.Trim().ToLowerInvariant() switch
    {
        "" or "all" => null,
        "flash" => ProtocolClients.Flash,
        "unity" => ProtocolClients.Unity,
        _ => throw new ArgumentException("'client' must be flash, unity, or all.", nameof(value))
    };

    private static string DirectionName(Direction direction) => direction switch
    {
        Direction.In => "in",
        Direction.Out => "out",
        _ => "none"
    };

    private static string ClientName(ClientType client) => client switch
    {
        ProtocolClients.Flash => "flash",
        ProtocolClients.Unity => "unity",
        _ => "none"
    };

    private sealed record MessageProjection(
        Direction Direction,
        MessageRegistryEntry Entry);
}

public sealed record MessageRegistrySnapshot(
    string Query,
    MessageRegistrySummary Registry,
    MessageRegistrySession Session,
    MessageRegistryFilters Filters,
    int Total,
    int Offset,
    int Limit,
    IReadOnlyList<MessageRegistryEntry> Entries);

public sealed record MessageRegistrySummary(
    int Descriptors,
    int Aliases,
    int ExplicitDescriptors);

public sealed record MessageRegistrySession(
    string ActiveClient,
    bool CatalogBound,
    string? CatalogOrigin,
    string? CatalogSource,
    string? ClientVersion,
    int CatalogHeaders,
    string? SourceFingerprint,
    string? CatalogFingerprint,
    string? SchemaFingerprint);

public sealed record MessageRegistryFilters(
    string Query,
    string Direction,
    string Client,
    bool ExplicitOnly,
    bool ResolvedOnly);

public sealed record MessageRegistryEntry(
    string Key,
    string Direction,
    bool Stable,
    string KeyKind,
    MessageRegistryDialects Clients,
    MessageRegistryActiveBinding Active);

public sealed record MessageRegistryDialects(
    MessageRegistryDialect Flash,
    MessageRegistryDialect Unity);

public sealed record MessageRegistryDialect(
    bool Supported,
    string? PrimaryName,
    IReadOnlyList<string> Aliases);

public sealed record MessageRegistryActiveBinding(
    string Client,
    bool Supported,
    bool Resolved,
    IReadOnlyList<int> Headers,
    IReadOnlyList<MessageRegistryAliasBinding> Evidence);

public sealed record MessageRegistryAliasBinding(
    string Name,
    IReadOnlyList<int> Headers);
