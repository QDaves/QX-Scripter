namespace Qx.Messages;

/// <summary>Defines a mapping between message identifiers and the headers of the connected client build.</summary>
public interface IMessageManager
{
    /// <summary>Tries to get the header of a message.</summary>
    /// <param name="identifier">The message identifier.</param>
    /// <param name="header">The header, or the default header when it cannot be resolved.</param>
    /// <returns><see langword="true"/> if a header was resolved; otherwise, <see langword="false"/>.</returns>
    bool TryGetHeader(Identifier identifier, out Header header);
    /// <summary>Tries to get every header that a message resolves to.</summary>
    /// <param name="identifier">The message identifier.</param>
    /// <param name="headers">The headers, or an empty list when none can be resolved.</param>
    /// <returns><see langword="true"/> if at least one header was resolved; otherwise, <see langword="false"/>.</returns>
    bool TryGetHeaders(Identifier identifier, out IReadOnlyList<Header> headers);
    /// <summary>Tries to get the message identifier of a header.</summary>
    /// <param name="header">The header to look up.</param>
    /// <param name="identifier">The identifier, or <see cref="Identifier.Unknown"/> when the header is not known.</param>
    /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetIdentifier(Header header, out Identifier identifier);
}
