namespace Qx.Messages;

public interface IMessageManager
{
    ClientType ActiveClient => ClientType.None;
    bool TryGetHeader(Identifier identifier, out Header header);
    bool TryGetHeaders(Identifier identifier, out IReadOnlyList<Header> headers);
    bool TryGetIdentifier(Header header, out Identifier identifier);
}
