using Qx;
using Qx.Diagnostics;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;
using System.Runtime.ExceptionServices;

namespace Qx.Game;

/// <summary>
/// Provides the base for managers that mirror hotel state from intercepted messages.
/// </summary>
/// <remarks>
/// <para>
/// A derived manager registers its message handlers in <see cref="OnAttach"/>, clears its state in
/// <see cref="Reset"/> and releases resources of its own in <see cref="Close"/>. Handlers run on the thread
/// that delivers the intercepted message and stay registered across reconnects until the manager is disposed.
/// </para>
/// <para>
/// While the hotel connection is closing, handlers are skipped and sends throw
/// <see cref="InvalidOperationException"/>. Both resume once the reset has finished.
/// </para>
/// </remarks>
public abstract class GameStateManager : IDisposable
{
    private delegate void PacketComposer(in PacketWriter writer);
    private delegate T PacketParser<T>(in PacketReader reader);

    private readonly object _lifecycle_sync = new();
    private readonly List<IDisposable> _subscriptions = [];
    private CallbackGeneration? _callbacks;
    private CallbackGeneration? _closing_callbacks;
    private OperationGeneration? _operations;
    private long _attachment_generation;
    private long _state_generation;
    private bool _attached;
    private bool _disposed;

    /// <summary>
    /// Gets the interceptor the manager is attached to.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="Attach"/>, and <see langword="null"/> before the first attach.
    /// </remarks>
    protected IInterceptor Interceptor { get; private set; } = null!;

    /// <summary>
    /// Attaches the manager to an interceptor and registers its message handlers.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="OnAttach"/>. If it throws, the attachment is rolled back and the exception is rethrown.
    /// </remarks>
    /// <param name="interceptor">The interceptor to attach to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="interceptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is already attached or its previous attachment is still detaching.</exception>
    public void Attach(IInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        long attachment_generation;
        lock (_lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_attached)
                throw new InvalidOperationException("The state manager is already attached.");
            if (_closing_callbacks is not null)
                throw new InvalidOperationException("The previous attachment is still detaching.");
            _attached = true;
            Interceptor = interceptor;
            attachment_generation = ++_attachment_generation;
            _state_generation++;
            _callbacks = new CallbackGeneration();
            _operations = new OperationGeneration();
        }

        try
        {
            Subscribe(generation =>
            {
                Action disconnected = () => Disconnect(generation);
                interceptor.Disconnected += disconnected;
                return new Unsubscriber(() => interceptor.Disconnected -= disconnected);
            });
            OnAttach();
        }
        catch
        {
            RollbackAttachment(attachment_generation);
            throw;
        }
    }

    /// <summary>
    /// Registers a handler that runs when a hotel session connects.
    /// </summary>
    /// <param name="handler">The handler to run with the new session.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnConnected(Action<Session> handler)
    {
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
        {
            Action<Session> callback = session =>
                InvokeCallback(generation, _ => handler(session));
            interceptor.Connected += callback;
            return new Unsubscriber(() => interceptor.Connected -= callback);
        });
    }

    /// <summary>
    /// Registers a handler for an incoming message, identified by name.
    /// </summary>
    /// <remarks>
    /// A message that leaves unread bytes after parsing throws <see cref="InvalidOperationException"/>
    /// instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="name">The name of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(string name, Action<T> handler) where T : IParserComposer<T>
        => OnIncoming<T>(name, (message, _) => handler(message));

    /// <summary>
    /// Registers a handler for an incoming message, identified by key.
    /// </summary>
    /// <remarks>
    /// A message that leaves unread bytes after parsing throws <see cref="InvalidOperationException"/>
    /// instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="key">The key of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(MessageKey key, Action<T> handler) where T : IParserComposer<T>
        => OnIncoming<T>(key, (message, _) => handler(message));

    /// <summary>
    /// Registers a handler for an incoming message, parsed through its contract.
    /// </summary>
    /// <remarks>
    /// A message that leaves unread bytes after parsing throws <see cref="InvalidOperationException"/>
    /// instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="contract">The contract of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnIncoming(contract, (message, _) => handler(message));

    /// <summary>
    /// Registers a handler for an incoming message, parsed through its contract, that also receives the state generation.
    /// </summary>
    /// <remarks>
    /// The second handler argument is the <see cref="CurrentStateGeneration"/> at the time the message
    /// arrived, for use with <see cref="ApplyIfCurrent"/>. A message that leaves unread bytes after
    /// parsing throws <see cref="InvalidOperationException"/> instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="contract">The contract of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message and the state generation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(MessageContract<T> contract, Action<T, long> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage(
                        contract.Key.Value,
                        state_generation,
                        intercept.Packet,
                        handler,
                        contract.Parse))));
    }

    /// <summary>
    /// Registers a handler for an incoming message, identified by key, that also receives the state generation.
    /// </summary>
    /// <remarks>
    /// The second handler argument is the <see cref="CurrentStateGeneration"/> at the time the message
    /// arrived. A message that leaves unread bytes after parsing throws
    /// <see cref="InvalidOperationException"/> instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="key">The key of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message and the state generation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(MessageKey key, Action<T, long> handler) where T : IParserComposer<T>
    {
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage<T>(
                        key.Value,
                        state_generation,
                        intercept.Packet,
                        handler))));
    }

    /// <summary>
    /// Registers a handler for an incoming message, identified by name, that also receives the state generation.
    /// </summary>
    /// <remarks>
    /// The second handler argument is the <see cref="CurrentStateGeneration"/> at the time the message
    /// arrived. A message that leaves unread bytes after parsing throws
    /// <see cref="InvalidOperationException"/> instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="name">The name of the incoming message.</param>
    /// <param name="handler">The handler to run with the parsed message and the state generation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming<T>(
        string name,
        Action<T, long> handler) where T : IParserComposer<T>
    {
        var identifier = new Identifier(MessageDirection.In, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(identifier, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage<T>(
                        name,
                        state_generation,
                        intercept.Packet,
                        handler))));
    }

    /// <summary>
    /// Registers a handler for an incoming message without a body, identified by name.
    /// </summary>
    /// <remarks>
    /// A message that carries any bytes throws <see cref="InvalidOperationException"/> instead of
    /// reaching the handler.
    /// </remarks>
    /// <param name="name">The name of the incoming message.</param>
    /// <param name="handler">The handler to run when the message arrives.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnIncoming(string name, Action handler)
    {
        var identifier = new Identifier(MessageDirection.In, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(identifier, intercept =>
            {
                InvokeCallback(generation, _ =>
                {
                    EnsureEmpty(name, intercept.Packet);
                    handler();
                });
            }));
    }

    /// <summary>
    /// Registers a handler for an outgoing message, identified by name.
    /// </summary>
    /// <remarks>
    /// A message that leaves unread bytes after parsing throws <see cref="InvalidOperationException"/>
    /// instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="name">The name of the outgoing message.</param>
    /// <param name="handler">The handler to run with the parsed message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnOutgoing<T>(string name, Action<T> handler) where T : IParserComposer<T>
    {
        var identifier = new Identifier(MessageDirection.Out, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(identifier, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage<T>(
                        name,
                        state_generation,
                        intercept.Packet,
                        (message, _) => handler(message)))));
    }

    /// <summary>
    /// Registers a handler for an outgoing message without a body, identified by name.
    /// </summary>
    /// <remarks>
    /// A message that carries any bytes throws <see cref="InvalidOperationException"/> instead of
    /// reaching the handler.
    /// </remarks>
    /// <param name="name">The name of the outgoing message.</param>
    /// <param name="handler">The handler to run when the message is sent.</param>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnOutgoing(string name, Action handler)
    {
        var identifier = new Identifier(MessageDirection.Out, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(identifier, intercept =>
            {
                InvokeCallback(generation, _ =>
                {
                    EnsureEmpty(name, intercept.Packet);
                    handler();
                });
            }));
    }

    /// <summary>
    /// Sends an outgoing message built from values, identified by name.
    /// </summary>
    /// <remarks>
    /// The values are written in order with the wire format of the current client.
    /// </remarks>
    /// <param name="name">The name of the outgoing message.</param>
    /// <param name="values">The values to write to the message, in order.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void Send(string name, params object[] values)
        => Send(default, name, values);

    /// <summary>
    /// Sends an outgoing message built from values, identified by key.
    /// </summary>
    /// <remarks>
    /// The values are written in order with the wire format of the current client.
    /// </remarks>
    /// <param name="key">The key of the outgoing message.</param>
    /// <param name="values">The values to write to the message, in order.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void Send(MessageKey key, params object[] values)
        => Send(key, null, values);

    private void Send(MessageKey key, string? name, object[] values)
    {
        OperationGeneration operations = EnterOperation(
            out IInterceptor interceptor,
            out Session? expected_session);
        try
        {
            InterceptorSessionCatalog session_catalog = interceptor.CaptureSessionCatalog();
            expected_session = session_catalog.Session;
            SessionCatalogBinding? expected_catalog = session_catalog.Catalog;
            if (!TryGetHeader(interceptor.Messages, MessageDirection.Out, key, name, out Header header))
                throw new InvalidOperationException($"Unknown outgoing message '{RouteName(key, name)}'.");

            using Packet packet = interceptor.Messages.CreatePacket(header);
            packet.Writer().WriteValues(values);
            interceptor.Send(packet, expected_session, expected_catalog);
        }
        finally
        {
            operations.Leave();
        }
    }

    /// <summary>
    /// Sends an incoming message, identified by name, to the client as if the hotel had sent it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to <see cref="SendMessage{T}(string,T)"/>, which sends to the hotel. The hotel does
    /// not receive the message and the mirrored state does not change, so it can remove something from
    /// the client's view that is still in the room.
    /// </para>
    /// <para>
    /// The message is written by its own composer without a schema check.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="name">The name of the incoming message.</param>
    /// <param name="message">The message to send to the client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendToClient<T>(string name, T message) where T : IComposer
        => SendToClient(default, name, message);

    /// <summary>
    /// Sends an incoming message, identified by key, to the client as if the hotel had sent it.
    /// </summary>
    /// <remarks>
    /// The hotel does not receive the message and the mirrored state does not change.
    /// </remarks>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="key">The key of the incoming message.</param>
    /// <param name="message">The message to send to the client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendToClient<T>(MessageKey key, T message) where T : IComposer
        => SendToClient(key, null, message);

    /// <summary>
    /// Sends an incoming message through its contract to the client as if the hotel had sent it.
    /// </summary>
    /// <remarks>
    /// The hotel does not receive the message and the mirrored state does not change.
    /// </remarks>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="contract">The contract of the incoming message.</param>
    /// <param name="message">The message to send to the client.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendToClient<T>(MessageContract<T> contract, T message)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);
        SendToClientCore(
            contract.Key,
            null,
            (in PacketWriter writer) => contract.Compose(message, in writer));
    }

    private void SendToClient<T>(MessageKey key, string? name, T message) where T : IComposer
    {
        ArgumentNullException.ThrowIfNull(message);
        SendToClientCore(
            key,
            name,
            (in PacketWriter writer) => message.Compose(in writer));
    }

    private void SendToClientCore(
        MessageKey key,
        string? name,
        PacketComposer compose)
    {
        OperationGeneration operations = EnterOperation(
            out IInterceptor interceptor,
            out Session? expected_session);
        try
        {
            InterceptorSessionCatalog session_catalog = interceptor.CaptureSessionCatalog();
            expected_session = session_catalog.Session;
            SessionCatalogBinding? expected_catalog = session_catalog.Catalog;
            if (!TryGetHeader(interceptor.Messages, MessageDirection.In, key, name, out Header header))
                throw new InvalidOperationException($"Unknown incoming message '{RouteName(key, name)}'.");

            using Packet packet = interceptor.Messages.CreatePacket(header);
            PacketWriter writer = packet.Writer();
            compose(in writer);
            interceptor.Send(packet, expected_session, expected_catalog);
        }
        finally
        {
            operations.Leave();
        }
    }

    /// <summary>
    /// Sends an outgoing message, identified by name, to the hotel.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="name">The name of the outgoing message.</param>
    /// <param name="message">The message to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendMessage<T>(string name, T message) where T : IComposer
        => SendMessage(default, name, message);

    /// <summary>
    /// Sends an outgoing message, identified by key, to the hotel.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="key">The key of the outgoing message.</param>
    /// <param name="message">The message to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendMessage<T>(MessageKey key, T message) where T : IComposer
        => SendMessage(key, null, message);

    /// <summary>
    /// Sends an outgoing message through its contract to the hotel.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="contract">The contract of the outgoing message.</param>
    /// <param name="message">The message to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the message is not known, the manager is not attached, or the hotel connection is closing or not available.</exception>
    protected void SendMessage<T>(MessageContract<T> contract, T message)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);
        SendMessageCore(
            contract.Key,
            null,
            message,
            (in PacketWriter writer) => contract.Compose(message, in writer),
            contract,
            null,
            default,
            null);
    }

    /// <summary>
    /// Registers a handler for an outgoing message, parsed through its contract, that also receives the state generation.
    /// </summary>
    /// <remarks>
    /// The second handler argument is the <see cref="CurrentStateGeneration"/> at the time the message
    /// was sent. A message that leaves unread bytes after parsing throws
    /// <see cref="InvalidOperationException"/> instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="contract">The contract of the outgoing message.</param>
    /// <param name="handler">The handler to run with the parsed message and the state generation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnOutgoing<T>(MessageContract<T> contract, Action<T, long> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage(
                        contract.Key.Value,
                        state_generation,
                        intercept.Packet,
                        handler,
                        contract.Parse))));
    }

    /// <summary>
    /// Sends an outgoing message through its contract to the hotel if the given session is still current.
    /// </summary>
    /// <typeparam name="T">The type of the message.</typeparam>
    /// <param name="contract">The contract of the outgoing message.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="expectedSession">The session the message is meant for.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <param name="dispatchGuard">An action that runs under the interceptor's send lock just before the message is written, and can throw to stop the send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/>, <paramref name="message"/> or <paramref name="expectedSession"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the session is no longer current, the message is not known, the manager is not attached, or the hotel connection is closing.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled before the message is written.</exception>
    protected void SendMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expectedSession,
        CancellationToken cancellationToken = default,
        Action? dispatchGuard = null)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(expectedSession);
        SendMessageCore(
            contract.Key,
            null,
            message,
            (in PacketWriter writer) => contract.Compose(message, in writer),
            contract,
            expectedSession,
            cancellationToken,
            dispatchGuard);
    }

    /// <summary>
    /// Registers a handler for an outgoing message, parsed through its contract.
    /// </summary>
    /// <remarks>
    /// A message that leaves unread bytes after parsing throws <see cref="InvalidOperationException"/>
    /// instead of reaching the handler.
    /// </remarks>
    /// <typeparam name="T">The type the message is parsed as.</typeparam>
    /// <param name="contract">The contract of the outgoing message.</param>
    /// <param name="handler">The handler to run with the parsed message.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the manager is not attached.</exception>
    protected void OnOutgoing<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => PublishMessage(
                        contract.Key.Value,
                        state_generation,
                        intercept.Packet,
                        (message, _) => handler(message),
                        contract.Parse))));
    }

    private void SendMessage<T>(MessageKey key, string? name, T message) where T : IComposer
    {
        ArgumentNullException.ThrowIfNull(message);
        SendMessageCore(
            key,
            name,
            message,
            (in PacketWriter writer) => message.Compose(in writer),
            null,
            null,
            default,
            null);
    }

    private void SendMessageCore(
        MessageKey key,
        string? name,
        IComposer message,
        PacketComposer compose,
        IMessageContract? contract,
        Session? required_session,
        CancellationToken cancellation_token,
        Action? dispatch_guard)
    {
        OperationGeneration operations = EnterOperation(
            out IInterceptor interceptor,
            out Session? expected_session);
        try
        {
            InterceptorSessionCatalog session_catalog = interceptor.CaptureSessionCatalog();
            expected_session = session_catalog.Session;
            SessionCatalogBinding? expected_catalog = session_catalog.Catalog;
            cancellation_token.ThrowIfCancellationRequested();
            if (required_session is not null && !ReferenceEquals(expected_session, required_session))
                throw new InvalidOperationException("The hotel session changed before dispatch.");
            expected_session = required_session ?? expected_session;
            if (!TryGetHeader(interceptor.Messages, MessageDirection.Out, key, name, out Header header))
                throw new InvalidOperationException($"Unknown outgoing message '{RouteName(key, name)}'.");

            using Packet packet = interceptor.Messages.CreatePacket(header);
            PacketWriter writer = packet.Writer();
            compose(in writer);
            cancellation_token.ThrowIfCancellationRequested();
            interceptor.Send(packet, expected_session, expected_catalog, dispatch_guard);
        }
        finally
        {
            operations.Leave();
        }
    }

    private static bool TryGetHeader(
        IMessageResolver messages,
        MessageDirection direction,
        MessageKey key,
        string? name,
        out Header header)
    {
        bool found = key.IsEmpty
            ? messages.TryGetHeader(new Identifier(direction, name!), out header)
            : messages.TryGetHeader(key, out header);
        return found && header.Direction == direction;
    }

    private static string RouteName(MessageKey key, string? name) => name ?? key.Value;

    /// <summary>
    /// Gets the current state generation of the manager.
    /// </summary>
    /// <remarks>
    /// The value increases when the manager is attached, when the hotel connection closes, when an
    /// attach is rolled back and when the manager is disposed.
    /// </remarks>
    protected long CurrentStateGeneration
    {
        get
        {
            lock (_lifecycle_sync)
                return _state_generation;
        }
    }

    /// <summary>
    /// Gets the current hotel session, or <see langword="null"/> when none is connected or the manager is not attached.
    /// </summary>
    protected Session? CurrentSession
    {
        get
        {
            lock (_lifecycle_sync)
                return _attached && !_disposed ? Interceptor.Session : null;
        }
    }

    /// <summary>
    /// Runs an action if the state generation and the hotel session are still current.
    /// </summary>
    /// <remarks>
    /// The action runs under the manager's lifecycle lock, so no disconnect or dispose can start while it runs.
    /// </remarks>
    /// <param name="stateGeneration">The state generation the action belongs to.</param>
    /// <param name="session">The session the action belongs to.</param>
    /// <param name="action">The action to run.</param>
    /// <returns><see langword="true"/> if the action ran; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    protected bool ApplyIfCurrent(
        long stateGeneration,
        Session session,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        lock (_lifecycle_sync)
        {
            if (!_attached ||
                _disposed ||
                _state_generation != stateGeneration ||
                !ReferenceEquals(Interceptor.Session, session))
            {
                return false;
            }
            action();
            return true;
        }
    }

    /// <summary>
    /// Registers the manager's message handlers when it is attached to an interceptor.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="Attach"/> after <see cref="Interceptor"/> is set. If it throws, the attachment is rolled back.
    /// </remarks>
    protected abstract void OnAttach();

    /// <summary>
    /// Clears the manager's state.
    /// </summary>
    /// <remarks>
    /// Called when the hotel connection closes and, if message handlers were still running, again once
    /// they have finished. Also called when the manager is disposed and when an attach is rolled back,
    /// so it can run more than once. The base implementation does nothing.
    /// </remarks>
    protected virtual void Reset()
    {
    }

    private static T Parse<T>(string name, IPacket packet) where T : IParserComposer<T>
    {
        PacketReader reader = packet.Reader();
        T message = reader.Parse<T>();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        return message;
    }

    private static T Parse<T>(string name, IPacket packet, PacketParser<T> parser)
    {
        PacketReader reader = packet.Reader();
        T message = parser(in reader);
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unparsed bytes for model '{typeof(T).Name}'.");
        return message;
    }

    private void PublishMessage<T>(
        string name,
        long state_generation,
        Packet packet,
        Action<T, long> handler,
        PacketParser<T>? parser = null) where T : IParserComposer<T>
    {
        handler(
            parser is null
                ? Parse<T>(name, packet)
                : Parse(name, packet, parser),
            state_generation);
    }

    private static void EnsureEmpty(string name, IPacket packet)
    {
        PacketReader reader = packet.Reader();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unexpected bytes.");
    }

    /// <summary>
    /// Detaches the manager from its interceptor, removes its message handlers and clears its state.
    /// </summary>
    /// <remarks>
    /// Waits for sends and message handlers running on other threads to finish before <see cref="Reset"/> runs. Later
    /// calls have no effect. An error from removing a message handler is thrown after the state is cleared.
    /// </remarks>
    void IDisposable.Dispose() => Close();

    /// <summary>
    /// Releases the manager when it is disposed.
    /// </summary>
    /// <remarks>
    /// Runs on every call to <see cref="IDisposable.Dispose"/>, and the base implementation acts only on the first. A
    /// derived manager overrides it to release resources of its own and calls the base implementation, which detaches
    /// the manager, removes its message handlers and clears its state.
    /// </remarks>
    protected internal virtual void Close()
    {
        CallbackGeneration? callbacks;
        OperationGeneration? operations;
        IDisposable[] subscriptions;
        lock (_lifecycle_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _attached = false;
            _attachment_generation++;
            _state_generation++;
            callbacks = CloseCurrentGeneration();
            operations = _operations;
            _operations = null;
            subscriptions = DrainSubscriptions();
        }

        ClosureMode closure = callbacks?.Close(
            () => CompleteGeneration(callbacks)) ?? ClosureMode.Complete;
        operations?.Close();
        Exception? detach_error = DisposeSubscriptions(subscriptions);
        try
        {
            if (operations is not null &&
                !operations.IsCurrentThreadActive)
            {
                operations.WaitForOperations();
            }
            if (callbacks is null)
                Reset();
            else
                FinishGeneration(callbacks, closure);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
        if (detach_error is not null)
            throw detach_error;
    }

    private void Subscribe(Func<long, IDisposable> subscribe)
    {
        long attachment_generation;
        lock (_lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_attached)
                throw new InvalidOperationException("The state manager is not attached.");
            attachment_generation = _attachment_generation;
        }

        IDisposable subscription = subscribe(attachment_generation);
        bool keep;
        lock (_lifecycle_sync)
        {
            keep = _attached &&
                !_disposed &&
                _attachment_generation == attachment_generation;
            if (keep)
                _subscriptions.Add(subscription);
        }
        if (!keep)
            subscription.Dispose();
    }

    private void InvokeCallback(
        long attachment_generation,
        Action<long> callback)
    {
        CallbackGeneration? callbacks;
        long state_generation;
        lock (_lifecycle_sync)
        {
            callbacks = _attached &&
                !_disposed &&
                _attachment_generation == attachment_generation
                ? _callbacks
                : null;
            state_generation = _state_generation;
        }
        if (callbacks is null || !callbacks.TryEnter())
            return;
        try
        {
            callback(state_generation);
        }
        finally
        {
            callbacks.Leave();
        }
    }

    private void Disconnect(long attachment_generation)
    {
        CallbackGeneration? callbacks;
        OperationGeneration? operations;
        lock (_lifecycle_sync)
        {
            if (!_attached ||
                _disposed ||
                _attachment_generation != attachment_generation ||
                _callbacks is null)
            {
                return;
            }
            callbacks = _callbacks;
            _callbacks = null;
            _closing_callbacks = callbacks;
            operations = _operations;
            _operations = null;
            operations?.Close();
            _state_generation++;
        }

        Action complete_disconnect = () =>
            CompleteDisconnect(
                callbacks,
                attachment_generation);
        ClosureMode closure = callbacks.Close(complete_disconnect);
        if (closure is ClosureMode.Complete)
        {
            complete_disconnect();
        }
        else if (closure is ClosureMode.Wait)
        {
            Exception? reset_error = null;
            try
            {
                Reset();
            }
            catch (Exception error)
            {
                reset_error = error;
            }

            try
            {
                callbacks.CompleteWhenDrained(complete_disconnect);
            }
            catch (Exception error) when (reset_error is not null)
            {
                throw new AggregateException(reset_error, error);
            }

            if (reset_error is not null)
                ExceptionDispatchInfo.Capture(reset_error).Throw();
        }
    }

    private OperationGeneration EnterOperation(
        out IInterceptor interceptor,
        out Session? expected_session)
    {
        CallbackGeneration? callbacks;
        OperationGeneration? operations;
        lock (_lifecycle_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_attached)
                throw new InvalidOperationException("The state manager is not attached.");
            callbacks = _callbacks;
            operations = _operations;
            interceptor = Interceptor;
            expected_session = interceptor.Session;
        }
        if (callbacks is null || !callbacks.TryEnter())
        {
            lock (_lifecycle_sync)
                ObjectDisposedException.ThrowIf(_disposed, this);
            throw new InvalidOperationException("The state manager is resetting after a disconnect.");
        }
        try
        {
            if (operations is not null && operations.TryEnter())
                return operations;
        }
        finally
        {
            callbacks.Leave();
        }
        lock (_lifecycle_sync)
            ObjectDisposedException.ThrowIf(_disposed, this);
        throw new InvalidOperationException("The state manager is not accepting operations.");
    }

    private void RollbackAttachment(long attachment_generation)
    {
        CallbackGeneration? callbacks;
        OperationGeneration? operations;
        IDisposable[] subscriptions;
        long detached_generation;
        lock (_lifecycle_sync)
        {
            if (!_attached ||
                _disposed ||
                _attachment_generation != attachment_generation)
            {
                return;
            }
            _attached = false;
            detached_generation = ++_attachment_generation;
            _state_generation++;
            callbacks = CloseCurrentGeneration();
            operations = _operations;
            _operations = null;
            subscriptions = DrainSubscriptions();
        }

        ClosureMode closure = callbacks?.Close(
            () => CompleteGeneration(callbacks)) ?? ClosureMode.Complete;
        operations?.Close();
        Exception? detach_error = DisposeSubscriptions(subscriptions);
        if (operations is not null &&
            !operations.IsCurrentThreadActive)
        {
            operations.WaitForOperations();
        }
        if (callbacks is null)
            Reset();
        else
            FinishGeneration(callbacks, closure);
        lock (_lifecycle_sync)
        {
            if (!_attached &&
                !_disposed &&
                _attachment_generation == detached_generation)
            {
                Interceptor = null!;
            }
        }
        if (detach_error is not null)
            throw detach_error;
    }

    private CallbackGeneration? CloseCurrentGeneration()
    {
        CallbackGeneration? callbacks = _callbacks ?? _closing_callbacks;
        _callbacks = null;
        if (callbacks is not null)
            _closing_callbacks = callbacks;
        return callbacks;
    }

    private void FinishGeneration(
        CallbackGeneration callbacks,
        ClosureMode closure)
    {
        if (closure is ClosureMode.Deferred)
            return;
        if (closure is ClosureMode.Wait)
            callbacks.WaitForCallbacks();
        CompleteGeneration(callbacks);
    }

    private void CompleteGeneration(CallbackGeneration callbacks)
    {
        while (!callbacks.TryBeginFinalization(true))
        {
            if (!callbacks.IsCurrentThreadActive &&
                !callbacks.IsCurrentThreadFinalizing)
            {
                callbacks.WaitForFinalization();
                if (callbacks.FinalizationError is not null)
                    continue;
            }
            return;
        }

        Exception? reset_error = null;
        try
        {
            Reset();
        }
        catch (Exception error)
        {
            reset_error = error;
            throw;
        }
        finally
        {
            lock (_lifecycle_sync)
            {
                if (ReferenceEquals(_closing_callbacks, callbacks))
                    _closing_callbacks = null;
            }
            callbacks.MarkFinalized(reset_error);
        }
    }

    private void CompleteDisconnect(
        CallbackGeneration callbacks,
        long attachment_generation)
    {
        if (!callbacks.TryBeginFinalization())
        {
            if (!callbacks.IsCurrentThreadActive &&
                !callbacks.IsCurrentThreadFinalizing)
            {
                callbacks.WaitForFinalization();
            }
            return;
        }

        bool reset_completed = false;
        Exception? reset_error = null;
        try
        {
            Reset();
            reset_completed = true;
        }
        catch (Exception error)
        {
            reset_error = error;
            throw;
        }
        finally
        {
            lock (_lifecycle_sync)
            {
                if (ReferenceEquals(_closing_callbacks, callbacks))
                    _closing_callbacks = null;
                if (_attached &&
                    !_disposed &&
                    _attachment_generation == attachment_generation &&
                    reset_completed &&
                    _callbacks is null &&
                    _operations is null)
                {
                    _callbacks = new CallbackGeneration();
                    _operations = new OperationGeneration();
                }
            }
            callbacks.MarkFinalized(reset_error);
        }
    }

    private IDisposable[] DrainSubscriptions()
    {
        IDisposable[] subscriptions = [.. _subscriptions];
        _subscriptions.Clear();
        return subscriptions;
    }

    private static Exception? DisposeSubscriptions(
        IReadOnlyList<IDisposable> subscriptions)
    {
        List<Exception>? errors = null;
        foreach (IDisposable subscription in subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception error)
            {
                (errors ??= []).Add(error);
            }
        }
        return errors?.Count switch
        {
            null => null,
            1 => errors[0],
            _ => new AggregateException(errors)
        };
    }

    private enum ClosureMode
    {
        Complete,
        Wait,
        Deferred
    }

    private sealed class CallbackGeneration
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, int> _threads = [];
        private Action? _drained;
        private int _active;
        private int _finalization;
        private int _finalization_thread_id;
        private Exception? _finalization_error;
        private bool _accepting = true;

        public bool IsCurrentThreadActive
        {
            get
            {
                lock (_sync)
                    return _threads.ContainsKey(Environment.CurrentManagedThreadId);
            }
        }

        public bool IsCurrentThreadFinalizing =>
            Volatile.Read(ref _finalization) == 1 &&
            Volatile.Read(ref _finalization_thread_id) ==
                Environment.CurrentManagedThreadId;

        public Exception? FinalizationError =>
            Volatile.Read(ref _finalization_error);

        public bool TryEnter()
        {
            lock (_sync)
            {
                if (!_accepting)
                    return false;
                _active++;
                int thread_id = Environment.CurrentManagedThreadId;
                _threads[thread_id] = _threads.GetValueOrDefault(thread_id) + 1;
                return true;
            }
        }

        public void Leave()
        {
            Action? drained = null;
            lock (_sync)
            {
                int thread_id = Environment.CurrentManagedThreadId;
                int depth = _threads[thread_id] - 1;
                if (depth == 0)
                    _threads.Remove(thread_id);
                else
                    _threads[thread_id] = depth;
                _active--;
                if (_active == 0)
                {
                    Monitor.PulseAll(_sync);
                    drained = _drained;
                    _drained = null;
                }
            }
            drained?.Invoke();
        }

        public ClosureMode Close(Action drained)
        {
            lock (_sync)
            {
                _accepting = false;
                if (_active == 0)
                    return ClosureMode.Complete;
                if (_threads.ContainsKey(Environment.CurrentManagedThreadId))
                {
                    _drained ??= drained;
                    return ClosureMode.Deferred;
                }
                return ClosureMode.Wait;
            }
        }

        public void WaitForCallbacks()
        {
            lock (_sync)
            {
                while (_active != 0)
                    Monitor.Wait(_sync);
            }
        }

        public void CompleteWhenDrained(Action drained)
        {
            bool complete;
            lock (_sync)
            {
                complete = _active == 0;
                if (!complete)
                    _drained ??= drained;
            }
            if (complete)
                drained();
        }

        public bool TryBeginFinalization(bool retry_failed = false)
        {
            int expected = 0;
            if (retry_failed &&
                Volatile.Read(ref _finalization) == 2 &&
                FinalizationError is not null)
            {
                expected = 2;
            }
            if (Interlocked.CompareExchange(
                ref _finalization,
                1,
                expected) != expected)
            {
                return false;
            }
            Volatile.Write(
                ref _finalization_thread_id,
                Environment.CurrentManagedThreadId);
            return true;
        }

        public void MarkFinalized(Exception? error)
        {
            lock (_sync)
            {
                Volatile.Write(ref _finalization_error, error);
                Volatile.Write(ref _finalization_thread_id, 0);
                Volatile.Write(ref _finalization, 2);
                Monitor.PulseAll(_sync);
            }
        }

        public void WaitForFinalization()
        {
            lock (_sync)
            {
                while (Volatile.Read(ref _finalization) != 2)
                    Monitor.Wait(_sync);
            }
        }
    }

    private sealed class OperationGeneration
    {
        private readonly object _sync = new();
        private readonly Dictionary<int, int> _threads = [];
        private int _active;
        private bool _accepting = true;

        public bool IsCurrentThreadActive
        {
            get
            {
                lock (_sync)
                    return _threads.ContainsKey(Environment.CurrentManagedThreadId);
            }
        }

        public bool TryEnter()
        {
            lock (_sync)
            {
                if (!_accepting)
                    return false;
                _active++;
                int thread_id = Environment.CurrentManagedThreadId;
                _threads[thread_id] =
                    _threads.GetValueOrDefault(thread_id) + 1;
                return true;
            }
        }

        public void Leave()
        {
            lock (_sync)
            {
                int thread_id = Environment.CurrentManagedThreadId;
                int depth = _threads[thread_id] - 1;
                if (depth == 0)
                    _threads.Remove(thread_id);
                else
                    _threads[thread_id] = depth;
                _active--;
                if (_active == 0)
                    Monitor.PulseAll(_sync);
            }
        }

        public void Close()
        {
            lock (_sync)
                _accepting = false;
        }

        public void WaitForOperations()
        {
            lock (_sync)
            {
                while (_active != 0)
                    Monitor.Wait(_sync);
            }
        }
    }

    private sealed class Unsubscriber(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
