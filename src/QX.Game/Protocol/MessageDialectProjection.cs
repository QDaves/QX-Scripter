using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

public delegate T MessageDialectParser<T>(in PacketReader reader);

public delegate void MessageDialectComposer<T>(T message, in PacketWriter writer);

public delegate MessageDialectCapability MessageDialectCapabilityProbe(
    MessageManager messages,
    Header header);

public readonly record struct MessageDialectCapability(
    string? Name,
    bool Available,
    string? Reason)
{
    public static MessageDialectCapability Ready(string? name = null) => new(name, true, null);

    public static MessageDialectCapability Missing(string name, string reason) => new(name, false, reason);
}

public sealed class MessageDialectProjection<T> where T : IParserComposer<T>
{
    private readonly MessageDialectParser<T> _parser;
    private readonly MessageDialectComposer<T> _composer;
    private readonly MessageDialectCapabilityProbe? _capability;

    public MessageDialectProjection(
        ClientType client,
        MessageDialectParser<T> parser,
        MessageDialectComposer<T> composer,
        MessageDialectCapabilityProbe? capability = null,
        bool allows_schema_selected_header = false)
    {
        if (client is not (ClientType.Flash or ClientType.Unity))
            throw new ArgumentOutOfRangeException(nameof(client), client, "A message projection requires Flash or Unity.");

        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(composer);

        Client = client;
        _parser = parser;
        _composer = composer;
        _capability = capability;
        AllowsSchemaSelectedHeader = allows_schema_selected_header;
    }

    public ClientType Client { get; }

    public bool AllowsSchemaSelectedHeader { get; }

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

    public MessageDialectCapability Capability(MessageManager messages, Header header)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return _capability?.Invoke(messages, header) ?? MessageDialectCapability.Ready();
    }

    public static MessageDialectProjection<T> FromModel(
        ClientType client,
        MessageDialectCapabilityProbe? capability = null,
        bool allows_schema_selected_header = false) =>
        new(
            client,
            static (in PacketReader reader) => T.Parse(in reader),
            static (T message, in PacketWriter writer) => message.Compose(in writer),
            capability,
            allows_schema_selected_header);

    private void RequireClient(ClientType client)
    {
        if (client != Client)
            throw new UnsupportedClientException(client);
    }
}
