using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game.Protocol;

/// <summary>Represents a method that parses a message model from a packet.</summary>
/// <typeparam name="T">The message model type.</typeparam>
/// <param name="reader">The packet reader.</param>
/// <returns>The parsed message.</returns>
public delegate T MessageParser<T>(in PacketReader reader);

/// <summary>Represents a method that writes a message model to a packet.</summary>
/// <typeparam name="T">The message model type.</typeparam>
/// <param name="message">The message to write.</param>
/// <param name="writer">The packet writer.</param>
public delegate void MessageComposer<T>(T message, in PacketWriter writer);

/// <summary>Represents a method that checks whether a message can be used with the active message set.</summary>
/// <param name="messages">The message resolver that holds the client's message set and wire profile.</param>
/// <param name="header">The header the message resolves to.</param>
/// <returns>The capability of the message for that header.</returns>
public delegate MessageCapability MessageCapabilityProbe(
    IMessageResolver messages,
    Header header);

/// <summary>Represents whether a message can be used on the wire and, when it cannot, why.</summary>
/// <param name="Name">The name of the capability, or <see langword="null"/> when the message has no named capability.</param>
/// <param name="Available">Whether the message can be used.</param>
/// <param name="Reason">The reason the message cannot be used, or <see langword="null"/> when it is available.</param>
public readonly record struct MessageCapability(
    string? Name,
    bool Available,
    string? Reason)
{
    /// <summary>Creates a capability that reports the message as available.</summary>
    /// <param name="name">The name of the capability, or <see langword="null"/> for a message without a named capability.</param>
    /// <returns>An available capability with no reason.</returns>
    public static MessageCapability Ready(string? name = null) => new(name, true, null);

    /// <summary>Creates a capability that reports the message as unavailable.</summary>
    /// <param name="name">The name of the capability.</param>
    /// <param name="reason">The reason the message cannot be used.</param>
    /// <returns>An unavailable capability with the specified reason.</returns>
    public static MessageCapability Missing(string name, string reason) => new(name, false, reason);
}

/// <summary>Provides the parser, composer and capability check for one message model.</summary>
/// <typeparam name="T">The message model type.</typeparam>
public sealed class MessageCodec<T> where T : IParserComposer<T>
{
    private readonly MessageParser<T> _parser;
    private readonly MessageComposer<T> _composer;
    private readonly MessageCapabilityProbe? _capability;

    /// <summary>Initializes a new instance of the <see cref="MessageCodec{T}"/> class.</summary>
    /// <param name="parser">The method that parses the message.</param>
    /// <param name="composer">The method that writes the message.</param>
    /// <param name="capability">The method that checks the message's capability, or <see langword="null"/> when the message is always available.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parser"/> or <paramref name="composer"/> is <see langword="null"/>.</exception>
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

    /// <summary>Parses a message from a packet.</summary>
    /// <param name="reader">The packet reader.</param>
    /// <returns>The parsed message.</returns>
    public T Parse(in PacketReader reader) => _parser(in reader);

    /// <summary>Writes a message to a packet.</summary>
    /// <param name="message">The message to write.</param>
    /// <param name="writer">The packet writer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    public void Compose(T message, in PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(message);
        _composer(message, in writer);
    }

    /// <summary>Gets the capability of the message for the specified message resolver and header.</summary>
    /// <param name="messages">The message resolver that holds the client's message set and wire profile.</param>
    /// <param name="header">The header the message resolves to.</param>
    /// <returns>The result of the capability check, or an available capability without a name when the codec has no check.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messages"/> is <see langword="null"/>.</exception>
    public MessageCapability Capability(IMessageResolver messages, Header header)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return _capability?.Invoke(messages, header) ?? MessageCapability.Ready();
    }

    /// <summary>Creates a codec that parses with the model's static <c>Parse</c> method and writes with its <c>Compose</c> method.</summary>
    /// <param name="capability">The method that checks the message's capability, or <see langword="null"/> when the message is always available.</param>
    /// <returns>The new codec.</returns>
    public static MessageCodec<T> FromModel(
        MessageCapabilityProbe? capability = null) =>
        new(
            static (in PacketReader reader) => T.Parse(in reader),
            static (T message, in PacketWriter writer) => message.Compose(in writer),
            capability);
}
