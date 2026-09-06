using System.Collections.Concurrent;
using Qx;
using Qx.Messages;

namespace Qx.Protocol;

public sealed class MessageManager : IMessageManager, ISemanticMessageResolver
{
    private const int CompatibleReferenceParts = 20;
    private const int CompatibleReferenceRequiredParts = 19;
    private readonly MessageMap _map;
    private readonly MessageRegistry _registry;
    private readonly ConcurrentDictionary<ClientType, MessageCatalog> _catalogs = new();
    private readonly ConcurrentDictionary<ClientType, MessageCatalog> _fallback_catalogs = new();
    private readonly ConcurrentDictionary<ClientType, MessageCatalog> _default_versioned_catalogs = new();
    private readonly ConcurrentDictionary<
        (ClientType Client, string CatalogFingerprint, string SchemaFingerprint),
        MessageCatalog> _versioned_catalogs = new();
    private readonly ConcurrentDictionary<ClientType, ClientBuildIdentity> _catalog_builds = new();
    private readonly object _session_catalog_sync = new();
    private SessionCatalogState? _session_catalog;
    private long _session_catalog_generation;
    private ClientType _active_client;

    public MessageManager(MessageMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _map = map;
        _registry = map.Registry;
    }

    public static MessageManager CreateWithEmbeddedMap() => new(MessagesIniParser.ParseEmbedded());

    public ClientType ActiveClient
    {
        get => Volatile.Read(ref _session_catalog)?.Binding.Client ?? _active_client;
        set
        {
            if (value != ClientType.None)
                RequireSupportedClient(value);
            lock (_session_catalog_sync)
            {
                if (_session_catalog is { } session && session.Binding.Client != value)
                    throw new InvalidOperationException("The active client is fixed for the bound session.");
                _active_client = value;
            }
        }
    }

    public MessageMap Map => _map;

    public MessageRegistry Registry => _registry;

    public SessionCatalogBinding? ActiveCatalogBinding =>
        Volatile.Read(ref _session_catalog)?.Binding;

    public SessionCatalogLease BindSessionCatalog(SessionCatalogBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        lock (_session_catalog_sync)
        {
            long generation = ++_session_catalog_generation;
            Volatile.Write(ref _session_catalog, new SessionCatalogState(generation, binding));
            return new SessionCatalogLease(generation);
        }
    }

    public bool TryReplaceSessionCatalog(
        SessionCatalogLease expected,
        SessionCatalogBinding binding,
        out SessionCatalogLease replacement)
    {
        ArgumentNullException.ThrowIfNull(binding);
        lock (_session_catalog_sync)
        {
            SessionCatalogState? current = _session_catalog;
            if (expected.IsEmpty ||
                current is null ||
                current.Generation != expected.Value ||
                current.Binding.Client != binding.Client)
            {
                replacement = default;
                return false;
            }

            long generation = ++_session_catalog_generation;
            Volatile.Write(ref _session_catalog, new SessionCatalogState(generation, binding));
            replacement = new SessionCatalogLease(generation);
            return true;
        }
    }

    public bool ClearSessionCatalog(SessionCatalogLease lease)
    {
        lock (_session_catalog_sync)
        {
            SessionCatalogState? current = _session_catalog;
            if (lease.IsEmpty || current is null || current.Generation != lease.Value)
                return false;
            Volatile.Write(ref _session_catalog, null);
            return true;
        }
    }

    public void LoadCatalog(ClientType client, MessagesJson json)
    {
        RequireSupportedClient(client);
        _catalogs[client] = MessageCatalog.FromJson(json);
    }

    public void LoadCatalog(ClientType client, MessageCatalog catalog)
    {
        RequireSupportedClient(client);
        ArgumentNullException.ThrowIfNull(catalog);
        _catalogs[client] = catalog;
    }

    public bool ClearCatalog(ClientType client)
    {
        RequireSupportedClient(client);
        return _catalogs.TryRemove(client, out _);
    }

    public void LoadFallbackCatalog(ClientType client, MessageCatalog catalog)
    {
        RequireSupportedClient(client);
        ArgumentNullException.ThrowIfNull(catalog);
        _fallback_catalogs[client] = catalog;
    }

    public void LoadVerifiedFallbackCatalog(
        ClientType client,
        MessageCatalog catalog,
        bool preferred = true)
    {
        RequireSupportedClient(client);
        ArgumentNullException.ThrowIfNull(catalog);
        MessageCatalog registered = catalog;
        if (catalog.BuildFingerprint is { } fingerprint)
        {
            registered = _versioned_catalogs.AddOrUpdate(
                (client, fingerprint, catalog.SchemaFingerprint ?? ""),
                catalog,
                (_, _) => catalog);
        }
        if (preferred || catalog.BuildFingerprint is null)
            _default_versioned_catalogs[client] = registered;
    }

    public bool HasCatalogBuild(ClientType client, string fingerprint)
    {
        if (!IsSupportedClient(client))
            return false;
        if (ActiveCatalogBinding is { } binding && binding.Client == client)
            return binding.Catalog?.MatchesBuildFingerprint(fingerprint) is true;
        return _versioned_catalogs.Keys.Any(key =>
            key.Client == client &&
            key.CatalogFingerprint.Equals(fingerprint.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void BindCatalogBuild(ClientType client, ClientBuildIdentity? identity)
    {
        RequireSupportedClient(client);
        if (identity is null || string.IsNullOrWhiteSpace(identity.CatalogFingerprint))
        {
            _catalog_builds.TryRemove(client, out _);
            return;
        }
        _catalog_builds[client] = identity with
        {
            CatalogFingerprint = identity.CatalogFingerprint.Trim().ToUpperInvariant(),
            SchemaFingerprint = string.IsNullOrWhiteSpace(identity.SchemaFingerprint)
                ? null
                : identity.SchemaFingerprint.Trim().ToUpperInvariant()
        };
    }

    public bool HasCatalog(ClientType client)
    {
        if (!IsSupportedClient(client))
            return false;
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Client == client)
                return session.Binding.Catalog?.HeaderCount > 0;
        }
        return _catalogs.ContainsKey(client) ||
            _fallback_catalogs.ContainsKey(client) ||
            _default_versioned_catalogs.ContainsKey(client) ||
            _versioned_catalogs.Keys.Any(key => key.Client == client);
    }

    public MessageWireProfile GetWireProfile(ClientType client)
    {
        if (!IsSupportedClient(client))
            return default;
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Client == client)
            {
                MessageWireProfile profile = session.Binding.Catalog?.WireProfile ?? default;
                if (TryGetSessionMetadataCatalog(session.Binding, out MessageCatalog? enriched) &&
                    enriched.WireProfile.IsAnalyzed)
                {
                    return MergeWireProfiles(enriched.WireProfile, profile);
                }
                return profile;
            }
        }
        if (TryGetBoundVersionedCatalog(client, out MessageCatalog? bound, out _))
            return bound.WireProfile;
        if (_catalogs.TryGetValue(client, out MessageCatalog? catalog) && catalog.WireProfile.IsAnalyzed)
            return catalog.WireProfile;
        if (TryGetUsableVersionedCatalog(client, out MessageCatalog? versioned) && versioned.WireProfile.IsAnalyzed)
            return versioned.WireProfile;
        if (_fallback_catalogs.TryGetValue(client, out MessageCatalog? fallback) && fallback.WireProfile.IsAnalyzed)
            return fallback.WireProfile;
        return default;
    }

    static MessageWireProfile MergeWireProfiles(
        MessageWireProfile preferred,
        MessageWireProfile fallback)
    {
        if (!preferred.IsAnalyzed)
            return fallback;
        if (!fallback.IsAnalyzed)
            return preferred;
        return new MessageWireProfile(
            preferred.WiredContextLayout is MessageWiredContextLayout.Unknown
                ? fallback.WiredContextLayout
                : preferred.WiredContextLayout,
            preferred.WiredConditionHasSeparateInvert ?? fallback.WiredConditionHasSeparateInvert,
            UnityAvatarStatusHasTargetId:
                preferred.UnityAvatarStatusHasTargetId ?? fallback.UnityAvatarStatusHasTargetId,
            UnityUpdateAvatarHasBadgeRank:
                preferred.UnityUpdateAvatarHasBadgeRank ?? fallback.UnityUpdateAvatarHasBadgeRank,
            UnityInventoryItemHasExtendedMetadata:
                preferred.UnityInventoryItemHasExtendedMetadata ?? fallback.UnityInventoryItemHasExtendedMetadata,
            FlashGuestRoomResultLayout:
                preferred.FlashGuestRoomResultLayout ?? fallback.FlashGuestRoomResultLayout,
            UnityGuestRoomResultHasExtendedData:
                preferred.UnityGuestRoomResultHasExtendedData ?? fallback.UnityGuestRoomResultHasExtendedData,
            UnityCraftingProductHasProductCode:
                preferred.UnityCraftingProductHasProductCode ?? fallback.UnityCraftingProductHasProductCode,
            UnityMarketplaceBuyLayout:
                preferred.UnityMarketplaceBuyLayout is MarketplaceBuyWireLayout.Unknown
                    ? fallback.UnityMarketplaceBuyLayout
                    : preferred.UnityMarketplaceBuyLayout,
            UnityMarketplaceBuyHeaderId:
                preferred.UnityMarketplaceBuyHeaderId ?? fallback.UnityMarketplaceBuyHeaderId,
            FlashMarketplaceLayout:
                preferred.FlashMarketplaceLayout is FlashMarketplaceWireLayout.Unknown
                    ? fallback.FlashMarketplaceLayout
                    : preferred.FlashMarketplaceLayout,
            UnityConsoleMessageLayout:
                preferred.UnityConsoleMessageLayout is ConsoleMessageWireLayout.Unknown
                    ? fallback.UnityConsoleMessageLayout
                    : preferred.UnityConsoleMessageLayout,
            UnityRoomSettingsLayout:
                preferred.UnityRoomSettingsLayout is UnityRoomSettingsWireLayout.Unknown
                    ? fallback.UnityRoomSettingsLayout
                    : preferred.UnityRoomSettingsLayout);
    }

    public bool HasMessage(ClientType client, Direction direction, string name) =>
        IsSupportedClient(client) && TryGetIds(client, direction, name, out _);

    public bool HasMessage(MessageKey key) => HasMessage(ActiveClient, key);

    public bool IsKnown(MessageKey key) => _registry.TryGet(key, out _);

    public bool IsApplicable(MessageKey key) =>
        IsSupportedClient(ActiveClient) &&
        _registry.TryGet(key, out MessageDescriptor descriptor) &&
        descriptor.NamesFor(ActiveClient).Count != 0;

    public bool HasMessage(ClientType client, MessageKey key) =>
        TryGetHeaders(client, key, out _);

    public bool TryGetHeader(MessageKey key, out Header header) =>
        TryGetHeader(ActiveClient, key, out header);

    public bool TryGetHeader(ClientType client, MessageKey key, out Header header)
    {
        if (TryGetHeaders(client, key, out IReadOnlyList<Header> headers) && headers.Count == 1)
        {
            header = headers[^1];
            return true;
        }
        header = default;
        return false;
    }

    public bool TryGetHeaders(MessageKey key, out IReadOnlyList<Header> headers) =>
        TryGetHeaders(ActiveClient, key, out headers);

    public bool TryGetHeaders(
        ClientType client,
        MessageKey key,
        out IReadOnlyList<Header> headers)
    {
        headers = [];
        if (!IsSupportedClient(client) ||
            !_registry.TryGet(key, out MessageDescriptor descriptor) ||
            !descriptor.HasExplicitKey ||
            !HasCatalog(client))
        {
            return false;
        }

        IReadOnlyList<string> names = descriptor.NamesFor(client);
        var values = new List<short>();
        foreach (string name in names)
        {
            if (!TryGetIds(client, descriptor.Direction, name, out IReadOnlyList<short> found))
                continue;
            foreach (short value in found)
                if (!values.Contains(value))
                    values.Add(value);
        }

        var primary_values = new List<short>();
        var fallback_values = new List<short>();
        foreach (short value in values)
        {
            if (!TryGetName(client, descriptor.Direction, value, out string primary_name))
            {
                fallback_values.Add(value);
                continue;
            }
            if (names.Any(name => name.Equals(primary_name, StringComparison.OrdinalIgnoreCase)))
            {
                primary_values.Add(value);
                continue;
            }
            if (!_registry.TryGet(
                    client,
                    descriptor.Direction,
                    primary_name,
                    out MessageDescriptor primary_descriptor) ||
                !primary_descriptor.HasExplicitKey ||
                primary_descriptor.Key == key)
            {
                fallback_values.Add(value);
            }
        }
        IReadOnlyList<short> resolved = primary_values.Count == 0
            ? fallback_values
            : primary_values;
        headers = resolved.Select(value => new Header(descriptor.Direction, value)).ToArray();
        return headers.Count > 0;
    }

    public bool TryGetHeader(Identifier id, out Header header)
    {
        if (TryGetHeaders(id, out IReadOnlyList<Header> headers))
        {
            header = headers[^1];
            return true;
        }
        header = default;
        return false;
    }

    public bool TryGetHeaders(Identifier id, out IReadOnlyList<Header> headers)
    {
        headers = [];

        ClientType target = ActiveClient == ClientType.None ? id.Client : ActiveClient;
        if (!IsSupportedClient(target) || !HasCatalog(target))
            return false;

        var values = new List<short>();
        foreach (string name in ResolveNames(id, target))
        {
            if (!TryGetIds(target, id.Direction, name, out IReadOnlyList<short> found))
                continue;
            foreach (short value in found)
                if (!values.Contains(value))
                    values.Add(value);
        }
        headers = values.Select(value => new Header(id.Direction, value)).ToArray();
        return headers.Count > 0;
    }

    public bool TryGetIdentifier(Header header, out Identifier id)
    {
        id = Identifier.Unknown;
        if (TryGetName(ActiveClient, header.Direction, header.Value, out string name))
        {
            id = new Identifier(ActiveClient, header.Direction, name);
            return true;
        }
        return false;
    }

    public bool TryGetOutgoingSchemas(
        Identifier identifier,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        ClientType target = ActiveClient == ClientType.None ? identifier.Client : ActiveClient;
        return TryGetOutgoingSchemas(target, identifier, out schemas);
    }

    public bool TryGetOutgoingSchemas(
        ClientType client,
        string name,
        out IReadOnlyList<OutgoingMessageSchema> schemas) =>
        TryGetOutgoingSchemas(
            client,
            new Identifier(ClientType.None, Direction.Out, name),
            out schemas);

    public bool TryGetOutgoingSchemas(
        ClientType client,
        Identifier identifier,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        schemas = [];
        if (!IsSupportedClient(client) || identifier.Direction != Direction.Out || !HasCatalog(client))
            return false;

        var resolved = new List<OutgoingMessageSchema>();
        var headers = new HashSet<short>();
        foreach (string name in ResolveNames(identifier, client))
        {
            if (!TryGetIds(client, Direction.Out, name, out IReadOnlyList<short> ids))
                continue;
            foreach (short id in ids)
            {
                if (!headers.Add(id) ||
                    !TryGetOutgoingSchemas(client, new Header(Direction.Out, id), out IReadOnlyList<OutgoingMessageSchema> found))
                    continue;
                resolved.AddRange(found);
            }
        }

        schemas = resolved;
        return resolved.Count > 0;
    }

    public bool TryGetOutgoingSchemas(
        Header header,
        out IReadOnlyList<OutgoingMessageSchema> schemas) =>
        TryGetOutgoingSchemas(ActiveClient, header, out schemas);

    public bool TryGetOutgoingSchemas(
        ClientType client,
        Header header,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        schemas = [];
        if (!IsSupportedClient(client) || header.Direction != Direction.Out)
            return false;
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Client == client)
            {
                if (session.Binding.Catalog is not { } session_catalog)
                    return false;
                if (session_catalog.TryGetOutgoingSchemas(header.Value, out schemas))
                    return true;
                return TryGetSessionMetadataCatalog(session.Binding, out MessageCatalog? enriched) &&
                    enriched.TryGetOutgoingSchemas(header.Value, out schemas);
            }
        }
        if (client == ProtocolClients.Unity)
            return TryGetUnityOutgoingSchemas(header, out schemas);
        if (TryGetBoundVersionedCatalog(client, out MessageCatalog? bound, out ClientBuildIdentity? identity))
        {
            if (bound.MatchesSchemaFingerprint(identity.SchemaFingerprint) &&
                bound.TryGetOutgoingSchemas(header.Value, out schemas))
                return true;
            if (!bound.TryGetName(header.Direction, header.Value, out string build_name))
                return false;
            return _fallback_catalogs.TryGetValue(client, out MessageCatalog? stable_for_build) &&
                TryGetCompatibleSchemas(client, stable_for_build, header, build_name, out schemas);
        }
        if (_catalogs.TryGetValue(client, out MessageCatalog? catalog))
        {
            if (catalog.TryGetOutgoingSchemas(header.Value, out schemas))
                return true;
            if (TryGetCoveredFallbackSchemas(_fallback_catalogs, client, catalog, header, out schemas) ||
                TryGetCoveredFallbackSchemas(_default_versioned_catalogs, client, catalog, header, out schemas))
                return true;
            if (!catalog.TryGetName(header.Direction, header.Value, out string live_name))
                return false;
            return (_fallback_catalogs.TryGetValue(client, out MessageCatalog? compatible_stable) &&
                    TryGetCompatibleSchemas(client, compatible_stable, header, live_name, out schemas)) ||
                (_default_versioned_catalogs.TryGetValue(client, out MessageCatalog? compatible_versioned) &&
                    TryGetCompatibleSchemas(client, compatible_versioned, header, live_name, out schemas));
        }
        if (TryGetUsableVersionedCatalog(client, out MessageCatalog? versioned) &&
            versioned.TryGetOutgoingSchemas(header.Value, out schemas))
            return true;
        return _fallback_catalogs.TryGetValue(client, out MessageCatalog? stable) &&
            stable.TryGetOutgoingSchemas(header.Value, out schemas);
    }

    bool TryGetUnityOutgoingSchemas(
        Header header,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        schemas = [];
        if (_catalog_builds.ContainsKey(ProtocolClients.Unity))
        {
            return TryGetBoundVersionedCatalog(
                    ProtocolClients.Unity,
                    out MessageCatalog? catalog,
                    out ClientBuildIdentity? identity) &&
                catalog.MatchesBuildFingerprint(identity.CatalogFingerprint) &&
                catalog.MatchesSchemaFingerprint(identity.SchemaFingerprint) &&
                catalog.TryGetOutgoingSchemas(header.Value, out schemas);
        }
        return _catalogs.TryGetValue(ProtocolClients.Unity, out MessageCatalog? runtime) &&
            runtime.TryGetOutgoingSchemas(header.Value, out schemas);
    }

    bool TryGetId(ClientType client, Direction direction, string name, out short id)
    {
        if (TryGetIds(client, direction, name, out IReadOnlyList<short> ids))
        {
            id = ids[^1];
            return true;
        }
        id = default;
        return false;
    }

    bool TryGetIds(ClientType client, Direction direction, string name, out IReadOnlyList<short> ids)
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Client == client)
            {
                if (session.Binding.Catalog is { } session_catalog)
                    return session_catalog.TryGetIds(direction, name, out ids);
                ids = [];
                return false;
            }
        }
        var values = new List<short>();
        if (TryGetBoundVersionedCatalog(client, out MessageCatalog? bound, out _))
        {
            if (bound.TryGetIds(direction, name, out IReadOnlyList<short> build_ids))
                values.AddRange(build_ids);
            AppendCompatibleFallbackIds(_fallback_catalogs, client, direction, name, bound, values);
            AppendCompatibleFallbackIds(_catalogs, client, direction, name, bound, values);
        }
        else if (_catalogs.TryGetValue(client, out MessageCatalog? catalog))
        {
            if (catalog.TryGetIds(direction, name, out IReadOnlyList<short> catalog_ids))
                values.AddRange(catalog_ids);
            AppendCompatibleFallbackIds(_fallback_catalogs, client, direction, name, catalog, values);
            AppendCompatibleFallbackIds(_default_versioned_catalogs, client, direction, name, catalog, values);
        }
        else
        {
            AppendStandaloneFallbackIds(_fallback_catalogs, client, direction, name, values);
            if (TryGetUsableVersionedCatalog(client, out MessageCatalog? versioned))
                AppendStandaloneIds(versioned, direction, name, values);
        }
        ids = values;
        return values.Count > 0;
    }

    bool TryGetName(ClientType client, Direction direction, short id, out string name)
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Client == client)
            {
                if (session.Binding.Catalog is { } session_catalog)
                    return session_catalog.TryGetName(direction, id, out name);
                name = "";
                return false;
            }
        }
        if (TryGetBoundVersionedCatalog(client, out MessageCatalog? bound, out _))
            return bound.TryGetName(direction, id, out name!);
        if (_catalogs.TryGetValue(client, out MessageCatalog? catalog))
        {
            if (catalog.TryGetName(direction, id, out name))
                return true;
            if (TryGetCoveredFallbackName(_fallback_catalogs, client, catalog, direction, id, out name) ||
                TryGetCoveredFallbackName(_default_versioned_catalogs, client, catalog, direction, id, out name))
                return true;
            return false;
        }
        if (TryGetUsableVersionedCatalog(client, out MessageCatalog? versioned) &&
            versioned.TryGetName(direction, id, out name))
            return true;
        if (_fallback_catalogs.TryGetValue(client, out MessageCatalog? stable) &&
            stable.TryGetName(direction, id, out name))
            return true;
        name = "";
        return false;
    }

    bool TryGetCompatibleSchemas(
        ClientType client,
        MessageCatalog fallback,
        Header header,
        string live_name,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        schemas = [];
        if (fallback.TryGetName(header.Direction, header.Value, out string fallback_name) &&
            !NamesMatch(client, header.Direction, live_name, fallback_name))
            return false;
        return fallback.TryGetOutgoingSchemas(header.Value, out schemas);
    }

    void AppendCompatibleFallbackIds(
        ConcurrentDictionary<ClientType, MessageCatalog> catalogs,
        ClientType client,
        Direction direction,
        string name,
        MessageCatalog live,
        List<short> values)
    {
        if (!catalogs.TryGetValue(client, out MessageCatalog? fallback))
            return;
        if (!fallback.TryGetIds(direction, name, out IReadOnlyList<short> fallback_ids))
            return;
        bool exact_catalog = IsExactFallback(fallback, live);
        bool compatible_reference = IsCompatibleReference(client, fallback, live);
        foreach (short fallback_id in fallback_ids)
        {
            if (!live.TryGetName(direction, fallback_id, out string live_name))
            {
                if ((exact_catalog || compatible_reference) && !values.Contains(fallback_id))
                    values.Add(fallback_id);
                continue;
            }
            if (values.Contains(fallback_id))
                continue;
            if (!NamesMatch(client, direction, name, live_name) &&
                !CatalogBindsName(fallback, direction, fallback_id, live_name) &&
                (!fallback.TryGetName(direction, fallback_id, out string fallback_name) ||
                 !NamesMatch(client, direction, fallback_name, live_name)))
                continue;
            values.Add(fallback_id);
        }
    }

    static bool CatalogBindsName(
        MessageCatalog catalog,
        Direction direction,
        short id,
        string name) =>
        catalog.TryGetIds(direction, name, out IReadOnlyList<short> ids) && ids.Contains(id);

    static bool TryGetCoveredFallbackName(
        ConcurrentDictionary<ClientType, MessageCatalog> catalogs,
        ClientType client,
        MessageCatalog live,
        Direction direction,
        short id,
        out string name)
    {
        if (catalogs.TryGetValue(client, out MessageCatalog? fallback) &&
            (IsExactFallback(fallback, live) || IsCompatibleReference(client, fallback, live)) &&
            fallback.TryGetName(direction, id, out name))
            return true;
        name = "";
        return false;
    }

    static bool TryGetCoveredFallbackSchemas(
        ConcurrentDictionary<ClientType, MessageCatalog> catalogs,
        ClientType client,
        MessageCatalog live,
        Header header,
        out IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        if (catalogs.TryGetValue(client, out MessageCatalog? fallback) &&
            !live.TryGetName(header.Direction, header.Value, out _) &&
            IsExactFallback(fallback, live) &&
            fallback.TryGetOutgoingSchemas(header.Value, out schemas))
            return true;
        schemas = [];
        return false;
    }

    static bool IsExactFallback(MessageCatalog fallback, MessageCatalog live) =>
        live.HeaderCount >= 64 &&
        fallback.CoversHeaders(live) &&
        fallback.MatchingHeaders(live) * 4 >= live.HeaderCount * 3;

    static bool IsCompatibleReference(
        ClientType client,
        MessageCatalog fallback,
        MessageCatalog live)
    {
        if (client is not (ProtocolClients.Unity or ProtocolClients.Flash) ||
            fallback.BuildFingerprint is not null ||
            live.HeaderCount < 64 ||
            fallback.HeaderCount < 64)
        {
            return false;
        }

        int matching = fallback.MatchingHeaders(live);
        return matching * CompatibleReferenceParts >=
                live.HeaderCount * CompatibleReferenceRequiredParts &&
            matching * CompatibleReferenceParts >=
                fallback.HeaderCount * CompatibleReferenceRequiredParts;
    }

    static void AppendStandaloneFallbackIds(
        ConcurrentDictionary<ClientType, MessageCatalog> catalogs,
        ClientType client,
        Direction direction,
        string name,
        List<short> values)
    {
        if (!catalogs.TryGetValue(client, out MessageCatalog? fallback) ||
            !fallback.TryGetIds(direction, name, out IReadOnlyList<short> fallback_ids))
            return;
        foreach (short fallback_id in fallback_ids)
            if (!values.Contains(fallback_id))
                values.Add(fallback_id);
    }

    static void AppendStandaloneIds(
        MessageCatalog catalog,
        Direction direction,
        string name,
        List<short> values)
    {
        if (!catalog.TryGetIds(direction, name, out IReadOnlyList<short> ids))
            return;
        foreach (short id in ids)
            if (!values.Contains(id))
                values.Add(id);
    }

    bool TryGetBoundVersionedCatalog(
        ClientType client,
        out MessageCatalog catalog,
        out ClientBuildIdentity identity)
    {
        catalog = null!;
        identity = null!;
        if (!_catalog_builds.TryGetValue(client, out ClientBuildIdentity? found_identity))
        {
            return false;
        }
        MessageCatalog? found_catalog = null;
        if (found_identity.SchemaFingerprint is { } schema_fingerprint)
        {
            _versioned_catalogs.TryGetValue(
                (client, found_identity.CatalogFingerprint, schema_fingerprint),
                out found_catalog);
        }
        if (found_catalog is null)
        {
            _versioned_catalogs.TryGetValue(
                (client, found_identity.CatalogFingerprint, ""),
                out found_catalog);
        }
        if (found_catalog is null)
            return false;
        catalog = found_catalog;
        identity = found_identity;
        return true;
    }

    bool TryGetUsableVersionedCatalog(ClientType client, out MessageCatalog catalog)
    {
        if (TryGetBoundVersionedCatalog(client, out catalog, out _))
            return true;
        if (!_default_versioned_catalogs.TryGetValue(client, out MessageCatalog? fallback))
        {
            catalog = null!;
            return false;
        }
        if (ActiveClient == client && fallback.BuildFingerprint is not null)
        {
            catalog = null!;
            return false;
        }
        catalog = fallback;
        return true;
    }

    bool TryGetSessionMetadataCatalog(
        SessionCatalogBinding binding,
        out MessageCatalog catalog)
    {
        catalog = null!;
        if (binding.Provenance.Origin != CatalogOrigin.ClientExtraction ||
            binding.Provenance.SourceSha256 is not { } source_fingerprint ||
            binding.Catalog is not { } session_catalog ||
            !session_catalog.MatchesBuildFingerprint(source_fingerprint) ||
            !_catalog_builds.TryGetValue(binding.Client, out ClientBuildIdentity? identity) ||
            identity.SchemaFingerprint is null ||
            !identity.CatalogFingerprint.Equals(source_fingerprint, StringComparison.OrdinalIgnoreCase) ||
            !TryGetBoundVersionedCatalog(binding.Client, out MessageCatalog? candidate, out _) ||
            !candidate.MatchesBuildFingerprint(source_fingerprint) ||
            !candidate.MatchesSchemaFingerprint(identity.SchemaFingerprint) ||
            !session_catalog.HasExactHeaders(candidate))
        {
            return false;
        }
        catalog = candidate;
        return true;
    }

    bool NamesMatch(ClientType client, Direction direction, string first, string second)
    {
        if (first.Equals(second, StringComparison.OrdinalIgnoreCase))
            return true;
        return _map.AreEquivalent(client, direction, first, second);
    }

    IReadOnlyList<string> ResolveNames(Identifier identifier, ClientType target)
    {
        if (identifier.Client == ClientType.None)
        {
            IReadOnlyList<string> equivalents =
                _map.EquivalentNames(target, identifier.Direction, identifier.Name);
            return equivalents.Count == 0 ? [identifier.Name] : equivalents;
        }
        if (identifier.Client == target)
        {
            IReadOnlyList<string> equivalents =
                _map.EquivalentNames(target, identifier.Direction, identifier.Name);
            return equivalents.Count == 0 ? [identifier.Name] : equivalents;
        }
        IReadOnlyList<string> translated = _map.EquivalentNames(
            identifier.Client,
            target,
            identifier.Direction,
            identifier.Name);
        if (translated.Count > 0)
            return translated;
        IReadOnlyList<string> source_names = _map.EquivalentNames(
            target,
            identifier.Client,
            identifier.Direction,
            identifier.Name);
        return source_names.Count == 0
            ? []
            : _map.EquivalentNames(target, identifier.Direction, identifier.Name);
    }

    static bool IsSupportedClient(ClientType client) =>
        ClientTypes.IsSupported(client);

    static void RequireSupportedClient(ClientType client)
    {
        if (!IsSupportedClient(client))
            throw new ArgumentOutOfRangeException(nameof(client), client, "Only Flash and Unity catalogs are supported.");
    }

    sealed record SessionCatalogState(long Generation, SessionCatalogBinding Binding);
}
