using Qx.Messages;

namespace Qx.Protocol;

public interface ISemanticMessageResolver
{
    bool IsKnown(MessageKey key);

    bool IsApplicable(MessageKey key);

    bool TryGetHeader(MessageKey key, out Header header);

    bool TryGetHeaders(MessageKey key, out IReadOnlyList<Header> headers);
}
