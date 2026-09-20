using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

public interface IMessageContract
{
    MessageKey Key { get; }

    Type MessageType { get; }

    bool Supports(ClientType client);

    MessageCapability Capability(ClientType client, MessageManager messages, Header header);

    object Parse(in PacketReader reader);

    void Compose(object message, in PacketWriter writer);
}

public sealed class MessageContract<T> : IMessageContract where T : IParserComposer<T>
{
    private readonly MessageCodec<T> _codec;

    public MessageContract(MessageKey key, MessageCodec<T> codec)
    {
        if (key.IsEmpty)
            throw new ArgumentException("A message contract requires a key.", nameof(key));

        ArgumentNullException.ThrowIfNull(codec);
        Key = key;
        _codec = codec;
    }

    public MessageKey Key { get; }

    public Type MessageType => typeof(T);

    public bool Supports(ClientType client) => client is ClientType.Flash;

    public MessageCapability Capability(
        ClientType client,
        MessageManager messages,
        Header header)
    {
        if (!Supports(client))
            throw new UnsupportedClientException(client);
        return _codec.Capability(messages, header);
    }

    public T Parse(in PacketReader reader) => _codec.Parse(in reader);

    public void Compose(T message, in PacketWriter writer) =>
        _codec.Compose(message, in writer);

    object IMessageContract.Parse(in PacketReader reader) => Parse(in reader)!;

    void IMessageContract.Compose(object message, in PacketWriter writer)
    {
        if (message is not T typed_message)
        {
            throw new ArgumentException(
                $"Message contract '{Key}' requires a value of type '{typeof(T).FullName}'.",
                nameof(message));
        }

        Compose(typed_message, in writer);
    }

}
