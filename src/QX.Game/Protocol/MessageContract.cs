using System.Collections.ObjectModel;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

public interface IMessageContract
{
    MessageKey Key { get; }

    Type MessageType { get; }

    IReadOnlyList<ClientType> Clients { get; }

    bool Supports(ClientType client);

    bool AllowsSchemaSelectedHeader(ClientType client);

    MessageDialectCapability Capability(ClientType client, MessageManager messages, Header header);

    object Parse(in PacketReader reader);

    void Compose(object message, in PacketWriter writer);
}

public sealed class MessageContract<T> : IMessageContract where T : IParserComposer<T>
{
    private readonly IReadOnlyDictionary<ClientType, MessageDialectProjection<T>> _by_client;

    public MessageContract(MessageKey key, params MessageDialectProjection<T>[] projections)
    {
        if (key.IsEmpty)
            throw new ArgumentException("A message contract requires a key.", nameof(key));

        ArgumentNullException.ThrowIfNull(projections);
        if (projections.Length == 0)
            throw new ArgumentException("A message contract requires at least one dialect projection.", nameof(projections));

        var by_client = new Dictionary<ClientType, MessageDialectProjection<T>>();
        foreach (MessageDialectProjection<T> projection in projections)
        {
            ArgumentNullException.ThrowIfNull(projection);
            if (!by_client.TryAdd(projection.Client, projection))
                throw new InvalidDataException($"Message contract '{key}' declares {projection.Client} more than once.");
        }

        Key = key;
        Projections = Array.AsReadOnly(
        [
            .. by_client.Values.OrderBy(projection => projection.Client is ClientType.Flash ? 0 : 1)
        ]);
        Clients = Array.AsReadOnly(Projections.Select(projection => projection.Client).ToArray());
        _by_client = new ReadOnlyDictionary<ClientType, MessageDialectProjection<T>>(by_client);
    }

    public MessageKey Key { get; }

    public Type MessageType => typeof(T);

    public IReadOnlyList<ClientType> Clients { get; }

    public IReadOnlyList<MessageDialectProjection<T>> Projections { get; }

    public bool Supports(ClientType client) => _by_client.ContainsKey(client);

    public bool AllowsSchemaSelectedHeader(ClientType client) =>
        _by_client.TryGetValue(client, out MessageDialectProjection<T>? projection) &&
        projection.AllowsSchemaSelectedHeader;

    public MessageDialectCapability Capability(
        ClientType client,
        MessageManager messages,
        Header header) => ProjectionFor(client).Capability(messages, header);

    public bool TryGetProjection(
        ClientType client,
        out MessageDialectProjection<T> projection) =>
        _by_client.TryGetValue(client, out projection!);

    public T Parse(in PacketReader reader) => ProjectionFor(reader.Client).Parse(in reader);

    public void Compose(T message, in PacketWriter writer) =>
        ProjectionFor(writer.Client).Compose(message, in writer);

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

    private MessageDialectProjection<T> ProjectionFor(ClientType client)
    {
        if (_by_client.TryGetValue(client, out MessageDialectProjection<T>? projection))
            return projection;
        throw new UnsupportedClientException(client);
    }
}
