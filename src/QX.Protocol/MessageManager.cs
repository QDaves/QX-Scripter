using Qx.Messages;

namespace Qx.Protocol;

/// <summary>Represents the message manager that resolves message names, keys and headers against the catalogs of the connected client build.</summary>
/// <remarks>
/// While a session catalog is bound, name and header lookups use only that catalog. Until one is bound, they
/// use the default catalog registered with <see cref="LoadVerifiedFallbackCatalog"/>.
/// </remarks>
public sealed class MessageManager : IMessageResolver
{
    private readonly MessageRegistry _registry;
    private MessageCatalog? _default_catalog;
    private readonly object _session_catalog_sync = new();
    private SessionCatalogState? _session_catalog;
    private long _session_catalog_generation;

    /// <summary>Initializes a new instance of the <see cref="MessageManager"/> class over a message registry.</summary>
    /// <param name="registry">The message registry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is <see langword="null"/>.</exception>
    public MessageManager(MessageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>Creates a message manager over the embedded <c>messages.ini</c> registry.</summary>
    /// <returns>The new message manager, with no catalogs loaded.</returns>
    public static MessageManager CreateWithEmbeddedMap() => new(MessagesIniParser.ParseEmbeddedRegistry());

    /// <summary>Gets the message registry.</summary>
    public MessageRegistry Registry => _registry;

    /// <summary>Gets the catalog binding of the active session, or <see langword="null"/> when none is bound.</summary>
    public SessionCatalogBinding? ActiveCatalogBinding =>
        Volatile.Read(ref _session_catalog)?.Binding;

    /// <summary>Binds a catalog to the active session, replacing any current binding.</summary>
    /// <param name="binding">The session catalog binding.</param>
    /// <returns>The lease that clears this binding later.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binding"/> is <see langword="null"/>.</exception>
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

    /// <summary>Removes the session catalog binding when a lease is still current.</summary>
    /// <param name="lease">The lease of the binding to remove.</param>
    /// <returns><see langword="true"/> if the binding was removed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>Registers a verified catalog of a client build as the default catalog.</summary>
    /// <remarks>
    /// The catalog replaces the current default catalog, which name and header lookups use while no session
    /// catalog is bound.
    /// </remarks>
    /// <param name="catalog">The catalog.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is <see langword="null"/>.</exception>
    public void LoadVerifiedFallbackCatalog(MessageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Volatile.Write(ref _default_catalog, catalog);
    }

    /// <summary>Gets whether a catalog is available.</summary>
    /// <remarks>
    /// While a session catalog is bound, this is whether that catalog has headers. Otherwise it is whether a
    /// default catalog was registered with <see cref="LoadVerifiedFallbackCatalog"/>.
    /// </remarks>
    public bool HasCatalog()
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
            return session.Binding.Catalog?.HeaderCount > 0;
        return Volatile.Read(ref _default_catalog) is not null;
    }

    /// <summary>Gets the wire profile of the client build in use.</summary>
    /// <remarks>
    /// While a session catalog is bound, the profile of that catalog is used. Otherwise the profile of the default
    /// catalog is used.
    /// </remarks>
    /// <returns>The wire profile, or the <see langword="default"/> profile, which is not analyzed, when none is available.</returns>
    public MessageWireProfile GetWireProfile()
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
            return session.Binding.Catalog?.WireProfile ?? default;
        return Volatile.Read(ref _default_catalog)?.WireProfile ?? default;
    }

    /// <summary>Gets the parser context of the active session.</summary>
    /// <remarks>
    /// The context carries this manager and the wire profile from <see cref="GetWireProfile"/>, so parsers and
    /// composers whose layout differs between client builds use the layout of the connected build.
    /// </remarks>
    /// <returns>A new parser context.</returns>
    public ParserContext GetParserContext() => new(this, GetWireProfile());

    /// <summary>Creates an empty packet for the active session.</summary>
    /// <remarks>
    /// The packet carries <see cref="GetParserContext"/>, so every value and composer that depends on the client
    /// build can be written to it.
    /// </remarks>
    /// <param name="header">The header of the packet.</param>
    /// <returns>The new packet. Dispose it to return its buffer memory to the pool.</returns>
    public Packet CreatePacket(Header header) => new(header) { Context = GetParserContext() };

    /// <summary>Gets whether a message key resolves to at least one header.</summary>
    /// <param name="key">The message key.</param>
    public bool HasMessage(MessageKey key) => TryGetHeaders(key, out _);

    /// <summary>Gets whether a message key is declared in the message registry.</summary>
    /// <param name="key">The message key.</param>
    public bool IsKnown(MessageKey key) => _registry.TryGet(key, out _);

    /// <summary>Tries to get the single header that a message key resolves to.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="header">The header, or the default header when the key does not resolve to exactly one header.</param>
    /// <returns><see langword="true"/> if the key resolves to exactly one header; otherwise, <see langword="false"/>.</returns>
    public bool TryGetHeader(MessageKey key, out Header header)
    {
        if (TryGetHeaders(key, out IReadOnlyList<Header> headers) && headers.Count == 1)
        {
            header = headers[^1];
            return true;
        }
        header = default;
        return false;
    }

    /// <summary>Tries to get every header that a message key resolves to.</summary>
    /// <remarks>
    /// Only keys declared with <c>k:</c> in the registry resolve. Header IDs whose catalog name is one of the
    /// key's names are preferred, and IDs whose catalog name belongs to another declared key are left out.
    /// </remarks>
    /// <param name="key">The message key.</param>
    /// <param name="headers">The headers, or an empty list when none can be resolved.</param>
    /// <returns><see langword="true"/> if at least one header was resolved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetHeaders(MessageKey key, out IReadOnlyList<Header> headers)
    {
        headers = [];
        if (!_registry.TryGet(key, out MessageDescriptor descriptor) ||
            !descriptor.HasExplicitKey ||
            !HasCatalog())
        {
            return false;
        }

        IReadOnlyList<string> names = descriptor.Names;
        var values = new List<short>();
        foreach (string name in names)
        {
            if (!TryGetIds(descriptor.Direction, name, out IReadOnlyList<short> found))
                continue;
            foreach (short value in found)
                if (!values.Contains(value))
                    values.Add(value);
        }

        var primary_values = new List<short>();
        var fallback_values = new List<short>();
        foreach (short value in values)
        {
            if (!TryGetName(descriptor.Direction, value, out string primary_name))
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

    /// <summary>Tries to get the header of a message identifier.</summary>
    /// <remarks>When the identifier resolves to several headers, the last one is returned.</remarks>
    /// <param name="identifier">The message identifier.</param>
    /// <param name="header">The header, or the default header when none can be resolved.</param>
    /// <returns><see langword="true"/> if at least one header was resolved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetHeader(Identifier identifier, out Header header)
    {
        if (TryGetHeaders(identifier, out IReadOnlyList<Header> headers))
        {
            header = headers[^1];
            return true;
        }
        header = default;
        return false;
    }

    /// <summary>Tries to get every header that a message identifier resolves to, including the headers of equivalent names.</summary>
    /// <param name="identifier">The message identifier.</param>
    /// <param name="headers">The headers, or an empty list when none can be resolved.</param>
    /// <returns><see langword="true"/> if at least one header was resolved; otherwise, <see langword="false"/>.</returns>
    public bool TryGetHeaders(Identifier identifier, out IReadOnlyList<Header> headers)
    {
        headers = [];
        if (!HasCatalog())
            return false;

        var values = new List<short>();
        foreach (string name in ResolveNames(identifier))
        {
            if (!TryGetIds(identifier.Direction, name, out IReadOnlyList<short> found))
                continue;
            foreach (short value in found)
                if (!values.Contains(value))
                    values.Add(value);
        }
        headers = values.Select(value => new Header(identifier.Direction, value)).ToArray();
        return headers.Count > 0;
    }

    /// <summary>Tries to get the identifier of a header.</summary>
    /// <param name="header">The header to look up.</param>
    /// <param name="identifier">The identifier with the primary name of the header, or <see cref="Identifier.Unknown"/> when the header is not known.</param>
    /// <returns><see langword="true"/> if the header is known; otherwise, <see langword="false"/>.</returns>
    public bool TryGetIdentifier(Header header, out Identifier identifier)
    {
        identifier = Identifier.Unknown;
        if (TryGetName(header.Direction, header.Value, out string name))
        {
            identifier = new Identifier(header.Direction, name);
            return true;
        }
        return false;
    }

    bool TryGetIds(MessageDirection direction, string name, out IReadOnlyList<short> ids)
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Catalog is { } session_catalog)
                return session_catalog.TryGetIds(direction, name, out ids);
            ids = [];
            return false;
        }
        if (Volatile.Read(ref _default_catalog) is { } default_catalog &&
            default_catalog.TryGetIds(direction, name, out ids))
            return true;
        ids = [];
        return false;
    }

    bool TryGetName(MessageDirection direction, short id, out string name)
    {
        if (Volatile.Read(ref _session_catalog) is { } session)
        {
            if (session.Binding.Catalog is { } session_catalog)
                return session_catalog.TryGetName(direction, id, out name);
            name = "";
            return false;
        }
        if (Volatile.Read(ref _default_catalog) is { } default_catalog &&
            default_catalog.TryGetName(direction, id, out name))
            return true;
        name = "";
        return false;
    }

    IReadOnlyList<string> ResolveNames(Identifier identifier) =>
        _registry.TryGet(identifier.Direction, identifier.Name, out MessageDescriptor descriptor)
            ? descriptor.Names
            : [identifier.Name];

    sealed record SessionCatalogState(long Generation, SessionCatalogBinding Binding);
}
