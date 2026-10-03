using Qx.Messages;

namespace Qx.Protocol;

/// <summary>Defines a read-only resolver that maps message names, keys and headers to the catalog of the connected client build.</summary>
/// <remarks>
/// While a session catalog is bound, lookups use only that catalog. Until one is bound, they use the default
/// catalog of the host.
/// </remarks>
public interface IMessageResolver : IMessageManager
{
    /// <summary>Gets the message registry.</summary>
    MessageRegistry Registry { get; }

    /// <summary>Gets the catalog binding of the active session, or <see langword="null"/> when none is bound.</summary>
    SessionCatalogBinding? ActiveCatalogBinding { get; }

    /// <summary>Gets whether a catalog is available.</summary>
    /// <remarks>
    /// While a session catalog is bound, this is whether that catalog has headers. Otherwise it is whether the
    /// host registered a default catalog.
    /// </remarks>
    bool HasCatalog();

    /// <summary>Gets the wire profile of the client build in use.</summary>
    /// <returns>The wire profile, or the <see langword="default"/> profile, which is not analyzed, when none is available.</returns>
    MessageWireProfile GetWireProfile();

    /// <summary>Gets the parser context of the active session.</summary>
    /// <returns>A new parser context that carries this resolver and the wire profile from <see cref="GetWireProfile"/>.</returns>
    ParserContext GetParserContext();

    /// <summary>Creates an empty packet for the active session.</summary>
    /// <param name="header">The header of the packet.</param>
    /// <returns>The new packet, which carries <see cref="GetParserContext"/>. Dispose it to return its buffer memory to the pool.</returns>
    Packet CreatePacket(Header header);

    /// <summary>Gets whether a message key resolves to at least one header.</summary>
    /// <param name="key">The message key.</param>
    bool HasMessage(MessageKey key);

    /// <summary>Gets whether a message key is declared in the message registry.</summary>
    /// <param name="key">The message key.</param>
    bool IsKnown(MessageKey key);

    /// <summary>Tries to get the single header that a message key resolves to.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="header">The header, or the default header when the key does not resolve to exactly one header.</param>
    /// <returns><see langword="true"/> if the key resolves to exactly one header; otherwise, <see langword="false"/>.</returns>
    bool TryGetHeader(MessageKey key, out Header header);

    /// <summary>Tries to get every header that a message key resolves to.</summary>
    /// <param name="key">The message key.</param>
    /// <param name="headers">The headers, or an empty list when none can be resolved.</param>
    /// <returns><see langword="true"/> if at least one header was resolved; otherwise, <see langword="false"/>.</returns>
    bool TryGetHeaders(MessageKey key, out IReadOnlyList<Header> headers);
}
