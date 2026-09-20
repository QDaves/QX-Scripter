using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

public delegate T MessageParser<T>(in PacketReader reader);

public delegate void MessageComposer<T>(T message, in PacketWriter writer);

public delegate MessageCapability MessageCapabilityProbe(
    MessageManager messages,
    Header header);

public readonly record struct MessageCapability(
    string? Name,
    bool Available,
    string? Reason)
{
    public static MessageCapability Ready(string? name = null) => new(name, true, null);

    public static MessageCapability Missing(string name, string reason) => new(name, false, reason);
}

public sealed class MessageCodec<T> where T : IParserComposer<T>
{
    private readonly MessageParser<T> _parser;
    private readonly MessageComposer<T> _composer;
    private readonly MessageCapabilityProbe? _capability;

    public MessageCodec(
        MessageParser<T> parser,
        MessageComposer<T> composer,
        MessageCapabilityProbe? capability = null)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(composer);

        _parser = parser;
        _composer = composer;
        _capability = capability;
    }

    public T Parse(in PacketReader reader)
    {
        RequireClient(reader.Client);
        return _parser(in reader);
    }

    public void Compose(T message, in PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(message);
        RequireClient(writer.Client);
        _composer(message, in writer);
    }

    public MessageCapability Capability(MessageManager messages, Header header)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return _capability?.Invoke(messages, header) ?? MessageCapability.Ready();
    }

    public static MessageCodec<T> FromModel(
        MessageCapabilityProbe? capability = null) =>
        new(
            static (in PacketReader reader) => T.Parse(in reader),
            static (T message, in PacketWriter writer) => message.Compose(in writer),
            capability);

    private static void RequireClient(ClientType client)
    {
        if (client is not ClientType.Flash)
            throw new UnsupportedClientException(client);
    }
}
