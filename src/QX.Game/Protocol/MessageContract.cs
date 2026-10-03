using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

/// <summary>Defines the binding between a message key and the model type that parses and composes the message.</summary>
public interface IMessageContract
{
    /// <summary>Gets the key of the message.</summary>
    MessageKey Key { get; }

    /// <summary>Gets the model type the message is parsed into and composed from.</summary>
    Type MessageType { get; }

    /// <summary>Gets the capability of the message for the specified message resolver and header.</summary>
    /// <param name="messages">The message resolver that holds the client's message set and wire profile.</param>
    /// <param name="header">The header the message resolves to.</param>
    /// <returns>The capability of the message for that header.</returns>
    MessageCapability Capability(IMessageResolver messages, Header header);

    /// <summary>Parses the message from a packet into its model.</summary>
    /// <param name="reader">The packet reader.</param>
    /// <returns>The parsed message model.</returns>
    object Parse(in PacketReader reader);

    /// <summary>Writes a message model to a packet.</summary>
    /// <param name="message">The message model, an instance of <see cref="MessageType"/>.</param>
    /// <param name="writer">The packet writer.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="message"/> is not an instance of the contract's model type.</exception>
    void Compose(object message, in PacketWriter writer);
}

/// <summary>Represents the contract for one message and its model type.</summary>
/// <typeparam name="T">The message model type.</typeparam>
public sealed class MessageContract<T> : IMessageContract where T : IParserComposer<T>
{
    private readonly MessageCodec<T> _codec;

    /// <summary>Initializes a new instance of the <see cref="MessageContract{T}"/> class.</summary>
    /// <param name="key">The key of the message.</param>
    /// <param name="codec">The codec that parses, composes and checks the message.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="codec"/> is <see langword="null"/>.</exception>
    public MessageContract(MessageKey key, MessageCodec<T> codec)
    {
        if (key.IsEmpty)
            throw new ArgumentException("A message contract requires a key.", nameof(key));

        ArgumentNullException.ThrowIfNull(codec);
        Key = key;
        _codec = codec;
    }

    /// <inheritdoc/>
    public MessageKey Key { get; }

    /// <summary>Gets the model type the message is parsed into and composed from, which is <typeparamref name="T"/>.</summary>
    public Type MessageType => typeof(T);

    /// <summary>Gets the capability of the message for the specified message resolver and header.</summary>
    /// <param name="messages">The message resolver that holds the client's message set and wire profile.</param>
    /// <param name="header">The header the message resolves to.</param>
    /// <returns>The capability of the message for that header.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messages"/> is <see langword="null"/>.</exception>
    public MessageCapability Capability(IMessageResolver messages, Header header) =>
        _codec.Capability(messages, header);

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="reader">The packet reader.</param>
    /// <returns>The parsed message.</returns>
    public T Parse(in PacketReader reader) => _codec.Parse(in reader);

    /// <summary>Writes the message to a packet.</summary>
    /// <param name="message">The message to write.</param>
    /// <param name="writer">The packet writer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
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
