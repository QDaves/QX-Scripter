using Qx;
using Qx.Diagnostics;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;
using System.Runtime.ExceptionServices;

namespace Qx.Game;

public abstract class GameStateManager : IDisposable
{
    private delegate void PacketComposer(in PacketWriter writer);
    private delegate T PacketParser<T>(in PacketReader reader);

    private readonly object _lifecycle_sync = new();
    private readonly object _profile_sync = new();
    private readonly List<IDisposable> _subscriptions = [];
    private Task _profile_callbacks = Task.CompletedTask;
    private CallbackGeneration? _callbacks;
    private CallbackGeneration? _closing_callbacks;
    private OperationGeneration? _operations;
    private long _attachment_generation;
    private long _state_generation;
    private bool _attached;
    private bool _disposed;

    protected IInterceptor Interceptor { get; private set; } = null!;

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

    protected void OnIncoming<T>(string name, Action<T> handler) where T : IParserComposer<T>
        => OnIncoming<T>(
            ClientType.None,
            name,
            (message, _) => handler(message));

    protected void OnIncoming<T>(MessageKey key, Action<T> handler) where T : IParserComposer<T>
        => OnIncoming<T>(key, (message, _) => handler(message));

    protected void OnIncoming<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T> =>
        OnIncoming(contract, (message, _) => handler(message));

    protected void OnIncoming<T>(MessageContract<T> contract, Action<T, long> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => ParseOrDefer(
                        contract.Key.Value,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler,
                        contract.Parse))));
    }

    protected void OnIncoming<T>(
        ClientType client,
        MessageContract<T> contract,
        Action<T> handler) where T : IParserComposer<T> =>
        OnIncoming(client, contract, (message, _) => handler(message));

    protected void OnIncoming<T>(
        ClientType client,
        MessageContract<T> contract,
        Action<T, long> handler) where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (!contract.Supports(client))
            throw new UnsupportedClientException(client);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
        {
            IDisposable Bind() => interceptor.Intercept(contract.Key, intercept =>
            {
                InvokeCallback(generation, state_generation =>
                {
                    if (intercept.Packet.Client != client)
                        return;
                    ParseOrDefer(
                        contract.Key.Value,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler,
                        contract.Parse);
                });
            });

            return new ClientScopedSubscription(interceptor, client, Bind);
        });
    }

    protected void OnIncoming<T>(MessageKey key, Action<T, long> handler) where T : IParserComposer<T>
    {
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => ParseOrDefer<T>(
                        key.Value,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler))));
    }

    protected void OnIncoming<T>(
        ClientType client,
        MessageKey key,
        Action<T> handler) where T : IParserComposer<T> =>
        OnIncoming<T>(client, key, (message, _) => handler(message));

    protected void OnIncoming<T>(
        ClientType client,
        MessageKey key,
        Action<T, long> handler) where T : IParserComposer<T>
    {
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
        {
            IDisposable Bind() => interceptor.Intercept(key, intercept =>
            {
                InvokeCallback(generation, state_generation =>
                {
                    if (intercept.Packet.Client != client)
                        return;
                    ParseOrDefer<T>(
                        key.Value,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler);
                });
            });

            return new ClientScopedSubscription(interceptor, client, Bind);
        });
    }

    protected void OnIncoming<T>(
        string name,
        Action<T, long> handler) where T : IParserComposer<T> =>
        OnIncoming<T>(ClientType.None, name, handler);

    protected void OnIncoming<T>(
        ClientType client,
        string name,
        Action<T> handler) where T : IParserComposer<T> =>
        OnIncoming<T>(
            client,
            name,
            (message, _) => handler(message));

    protected void OnIncoming<T>(
        ClientType client,
        string name,
        Action<T, long> handler) where T : IParserComposer<T>
    {
        var identifier = new Identifier(client, Direction.In, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
        {
            IDisposable Bind() => interceptor.Intercept(identifier, intercept =>
            {
                InvokeCallback(generation, state_generation =>
                {
                    if (client is not ClientType.None &&
                        intercept.Packet.Client != client)
                    {
                        return;
                    }
                    ParseOrDefer<T>(
                        name,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler);
                });
            });

            return client is ClientType.None
                ? Bind()
                : new ClientScopedSubscription(interceptor, client, Bind);
        });
    }

    protected void OnIncoming(string name, Action handler)
    {
        var identifier = new Identifier(ClientType.None, Direction.In, name);
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

    protected void OnOutgoing<T>(string name, Action<T> handler) where T : IParserComposer<T>
    {
        var identifier = new Identifier(ClientType.None, Direction.Out, name);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(identifier, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => ParseOrDefer<T>(
                        name,
                        generation,
                        state_generation,
                        intercept.Packet,
                        (message, _) => handler(message)))));
    }

    protected void OnOutgoing(string name, Action handler)
    {
        var identifier = new Identifier(ClientType.None, Direction.Out, name);
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

    protected void Send(string name, params object[] values)
        => Send(default, name, values);

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
            if (!TryGetHeader(interceptor.Messages, Direction.Out, key, name, out Header header))
                throw new InvalidOperationException($"Unknown outgoing message '{RouteName(key, name)}'.");

            ClientType client = ResolveClient(interceptor, expected_session);

            using var packet = new Packet(header, client);
            packet.Context = new ParserContext(
                interceptor.Messages,
                interceptor.Messages.GetWireProfile(client));
            PacketWriter writer = packet.Writer();
            if (client is ClientType.Unity)
            {
                if (!interceptor.Messages.TryGetOutgoingSchemas(client, header, out IReadOnlyList<OutgoingMessageSchema> schemas))
                    throw new NotSupportedException($"Unity request '{RouteName(key, name)}' requires a verified wire schema.");
                if (!OutgoingSchemaWriter.TryWrite(in writer, schemas, values))
                    throw new NotSupportedException($"Unity request '{RouteName(key, name)}' contains an unsupported verified wire type.");
            }
            else
            {
                writer.WriteValues(values);
            }
            interceptor.Send(packet, expected_session, expected_catalog);
        }
        finally
        {
            operations.Leave();
        }
    }

    /// <summary>
    /// Writes a message the client will take for one the hotel sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to <see cref="SendMessage{T}(string,T)"/>, which speaks to the hotel. This one speaks to
    /// the client, and is what makes it possible to take something off the screen that is still in
    /// the room: the hotel is never told, so nothing is really removed and the state we mirror is
    /// left alone.
    /// </para>
    /// <para>
    /// No verified-schema check here. That check exists because the hotel refuses a request it
    /// cannot read; the client is on the other side of the same wire and reads what a hotel would
    /// have sent, so the model's own composer is the whole contract.
    /// </para>
    /// </remarks>
    protected void SendToClient<T>(string name, T message) where T : IComposer
        => SendToClient(default, name, message);

    protected void SendToClient<T>(MessageKey key, T message) where T : IComposer
        => SendToClient(key, null, message);

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
            if (!TryGetHeader(interceptor.Messages, Direction.In, key, name, out Header header))
                throw new InvalidOperationException($"Unknown incoming message '{RouteName(key, name)}'.");

            ClientType client = ResolveClient(interceptor, expected_session);

            using var packet = new Packet(header, client);
            packet.Context = new ParserContext(
                interceptor.Messages,
                interceptor.Messages.GetWireProfile(client));
            PacketWriter writer = packet.Writer();
            compose(in writer);
            interceptor.Send(packet, expected_session, expected_catalog);
        }
        finally
        {
            operations.Leave();
        }
    }

    protected void SendMessage<T>(string name, T message) where T : IComposer
        => SendMessage(default, name, message);

    protected void SendMessage<T>(MessageKey key, T message) where T : IComposer
        => SendMessage(key, null, message);

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
            false,
            null,
            default,
            null);
    }

    protected void OnOutgoing<T>(MessageContract<T> contract, Action<T, long> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => ParseOrDefer(
                        contract.Key.Value,
                        generation,
                        state_generation,
                        intercept.Packet,
                        handler,
                        contract.Parse))));
    }

    protected void SendMessage<T>(
        MessageContract<T> contract,
        T message,
        Session expected_session,
        CancellationToken cancellation_token = default,
        Action? dispatch_guard = null)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(expected_session);
        SendMessageCore(
            contract.Key,
            null,
            message,
            (in PacketWriter writer) => contract.Compose(message, in writer),
            contract,
            false,
            expected_session,
            cancellation_token,
            dispatch_guard);
    }

    protected void OnOutgoing<T>(MessageContract<T> contract, Action<T> handler)
        where T : IParserComposer<T>
    {
        ArgumentNullException.ThrowIfNull(contract);
        IInterceptor interceptor = Interceptor;
        Subscribe(generation =>
            interceptor.Intercept(contract.Key, intercept =>
                InvokeCallback(
                    generation,
                    state_generation => ParseOrDefer(
                        contract.Key.Value,
                        generation,
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
            true,
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
        bool require_unity_schema,
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
            ClientType client = ResolveClient(interceptor, expected_session);
            Packet packet;
            if (contract?.AllowsSchemaSelectedHeader(client) is true)
            {
                packet = ComposeSchemaSelectedPacket(
                    interceptor.Messages,
                    client,
                    key,
                    name,
                    message,
                    compose,
                    contract,
                    cancellation_token);
            }
            else
            {
                if (!TryGetHeader(interceptor.Messages, Direction.Out, key, name, out Header header))
                    throw new InvalidOperationException($"Unknown outgoing message '{RouteName(key, name)}'.");

                IReadOnlyList<OutgoingMessageSchema>? schemas = null;
                if (client is ClientType.Unity)
                {
                    bool has_schema = interceptor.Messages.TryGetOutgoingSchemas(
                        ClientType.Unity,
                        header,
                        out schemas);
                    if (!has_schema && require_unity_schema)
                    {
                        throw new NotSupportedException(
                            $"Unity request '{RouteName(key, name)}' requires a verified wire schema.");
                    }
                    if (!has_schema)
                        schemas = null;
                }

                packet = ComposePacket(
                    interceptor.Messages,
                    client,
                    header,
                    compose);
                if (schemas is not null && !MatchesSchema(
                        interceptor.Messages,
                        header,
                        key,
                        name,
                        message,
                        packet,
                        schemas))
                {
                    packet.Dispose();
                    throw new NotSupportedException($"Unity request '{RouteName(key, name)}' does not match its verified wire schema.");
                }
            }
            using (packet)
            {
                cancellation_token.ThrowIfCancellationRequested();
                interceptor.Send(packet, expected_session, expected_catalog, dispatch_guard);
            }
        }
        finally
        {
            operations.Leave();
        }
    }

    private static Packet ComposeSchemaSelectedPacket(
        MessageManager messages,
        ClientType client,
        MessageKey key,
        string? name,
        IComposer message,
        PacketComposer compose,
        IMessageContract contract,
        CancellationToken cancellation_token)
    {
        if (key.IsEmpty ||
            !messages.TryGetHeaders(client, key, out IReadOnlyList<Header> headers))
        {
            throw new InvalidOperationException(
                $"Unknown outgoing message '{RouteName(key, name)}'.");
        }

        Packet? selected = null;
        try
        {
            foreach (Header header in headers.Distinct())
            {
                cancellation_token.ThrowIfCancellationRequested();
                if (header.Direction is not Direction.Out)
                    continue;
                MessageDialectCapability capability = contract.Capability(client, messages, header);
                if (!capability.Available ||
                    !messages.TryGetOutgoingSchemas(
                        client,
                        header,
                        out IReadOnlyList<OutgoingMessageSchema> schemas) ||
                    schemas.Count == 0)
                {
                    continue;
                }

                Packet candidate;
                try
                {
                    candidate = ComposePacket(messages, client, header, compose);
                }
                catch (Exception error) when (error is InvalidDataException or NotSupportedException)
                {
                    continue;
                }

                bool matches;
                try
                {
                    matches = MatchesSchema(messages, header, key, name, message, candidate, schemas);
                }
                catch
                {
                    candidate.Dispose();
                    throw;
                }
                if (!matches)
                {
                    candidate.Dispose();
                    continue;
                }
                if (selected is not null)
                {
                    candidate.Dispose();
                    throw new NotSupportedException(
                        $"Request '{RouteName(key, name)}' matches more than one verified outgoing header.");
                }
                selected = candidate;
            }

            cancellation_token.ThrowIfCancellationRequested();
            Packet result = selected ?? throw new NotSupportedException(
                $"Request '{RouteName(key, name)}' has no uniquely matching verified outgoing header.");
            selected = null;
            return result;
        }
        finally
        {
            selected?.Dispose();
        }
    }

    private static Packet ComposePacket(
        MessageManager messages,
        ClientType client,
        Header header,
        PacketComposer compose)
    {
        var packet = new Packet(header, client)
        {
            Context = new ParserContext(messages, messages.GetWireProfile(client))
        };
        try
        {
            PacketWriter writer = packet.Writer();
            compose(in writer);
            return packet;
        }
        catch
        {
            packet.Dispose();
            throw;
        }
    }

    private static bool MatchesSchema(
        MessageManager messages,
        Header header,
        MessageKey key,
        string? name,
        IComposer message,
        Packet packet,
        IReadOnlyList<OutgoingMessageSchema> schemas)
    {
        string resolved_name = ResolveName(messages, header, key, name);
        return UnityComplexComposerMatcher.RequiresExactMatch(resolved_name, message)
            ? UnityComplexComposerMatcher.TryMatch(
                resolved_name,
                message,
                packet,
                schemas)
            : OutgoingSchemaMatcher.TryMatch(packet, schemas, out _);
    }

    private static bool TryGetHeader(
        MessageManager messages,
        Direction direction,
        MessageKey key,
        string? name,
        out Header header)
    {
        bool found = key.IsEmpty
            ? messages.TryGetHeader(new Identifier(ClientType.None, direction, name!), out header)
            : messages.TryGetHeader(key, out header);
        return found && header.Direction == direction;
    }

    private static string ResolveName(
        MessageManager messages,
        Header header,
        MessageKey key,
        string? name) =>
        name ??
        (messages.TryGetIdentifier(header, out Identifier identifier)
            ? identifier.Name
            : key.Value);

    private static string RouteName(MessageKey key, string? name) => name ?? key.Value;

    protected ClientType CurrentClient
    {
        get
        {
            IInterceptor interceptor;
            lock (_lifecycle_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!_attached)
                    throw new InvalidOperationException("The state manager is not attached.");
                interceptor = Interceptor;
            }
            return ResolveClient(interceptor, interceptor.Session);
        }
    }

    protected long CurrentStateGeneration
    {
        get
        {
            lock (_lifecycle_sync)
                return _state_generation;
        }
    }

    protected Session? CurrentSession
    {
        get
        {
            lock (_lifecycle_sync)
                return _attached && !_disposed ? Interceptor.Session : null;
        }
    }

    protected bool ApplyIfCurrent(
        long state_generation,
        Session session,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(action);
        lock (_lifecycle_sync)
        {
            if (!_attached ||
                _disposed ||
                _state_generation != state_generation ||
                !ReferenceEquals(Interceptor.Session, session))
            {
                return false;
            }
            action();
            return true;
        }
    }

    protected abstract void OnAttach();

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

    private void ParseOrDefer<T>(
        string name,
        long attachment_generation,
        long state_generation,
        Packet packet,
        Action<T, long> handler,
        PacketParser<T>? parser = null) where T : IParserComposer<T>
    {
        try
        {
            handler(
                parser is null
                    ? Parse<T>(name, packet)
                    : Parse(name, packet, parser),
                state_generation);
        }
        catch (WireProfilePendingException) when (packet.Client is ClientType.Unity)
        {
            QueueProfileCallback(
                name,
                attachment_generation,
                state_generation,
                packet,
                (deferred, generation) => handler(
                    parser is null
                        ? Parse<T>(name, deferred)
                        : Parse(name, deferred, parser),
                    generation));
        }
    }

    private void QueueProfileCallback(
        string name,
        long attachment_generation,
        long state_generation,
        Packet packet,
        Action<Packet, long> callback)
    {
        Packet deferred = packet.Copy();
        lock (_profile_sync)
        {
            _profile_callbacks = ReplayProfileCallbackAsync(
                _profile_callbacks,
                name,
                attachment_generation,
                state_generation,
                deferred,
                callback);
        }
    }

    private async Task ReplayProfileCallbackAsync(
        Task previous,
        string name,
        long attachment_generation,
        long state_generation,
        Packet packet,
        Action<Packet, long> callback)
    {
        try
        {
            try
            {
                await previous.ConfigureAwait(false);
            }
            catch
            {
            }

            IInterceptor interceptor = Interceptor;
            await interceptor.WaitForCatalogBuildAsync().ConfigureAwait(false);
            packet.Context = new ParserContext(
                interceptor.Messages,
                interceptor.Messages.GetWireProfile(packet.Client));
            InvokeCallback(attachment_generation, current_generation =>
            {
                if (current_generation == state_generation)
                    callback(packet, current_generation);
            });
        }
        catch (Exception error)
        {
            Diag.Error($"Deferred handler for '{name}' failed: {error}", "game");
        }
        finally
        {
            packet.Dispose();
        }
    }

    private static void EnsureEmpty(string name, IPacket packet)
    {
        PacketReader reader = packet.Reader();
        if (reader.Available != 0)
            throw new InvalidOperationException($"Message '{name}' contains {reader.Available} unexpected bytes.");
    }

    public virtual void Dispose()
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

    private static ClientType ResolveClient(
        IInterceptor interceptor,
        Session? expected_session)
    {
        ClientType client =
            expected_session?.Client ??
            interceptor.Messages.ActiveClient;
        return client is ClientType.None ? ClientType.Flash : client;
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

    private sealed class ClientScopedSubscription : IDisposable
    {
        private readonly object _sync = new();
        private readonly IInterceptor _interceptor;
        private readonly ClientType _client;
        private readonly Func<IDisposable> _bind;
        private IDisposable? _active;
        private ClientType _desired_client;
        private long _event_revision;
        private bool _reconciling;
        private bool _disposed;

        public ClientScopedSubscription(
            IInterceptor interceptor,
            ClientType client,
            Func<IDisposable> bind)
        {
            _interceptor = interceptor;
            _client = client;
            _bind = bind;
            _interceptor.Connected += Connected;
            _interceptor.Disconnected += Disconnected;
            try
            {
                long revision;
                lock (_sync)
                    revision = _event_revision;
                ClientType active_client =
                    _interceptor.Session?.Client ?? _interceptor.Messages.ActiveClient;
                SetInitialClient(active_client, revision);
            }
            catch
            {
                _interceptor.Connected -= Connected;
                _interceptor.Disconnected -= Disconnected;
                throw;
            }
        }

        private void Connected(Session session) => SetClient(session.Client);

        private void Disconnected() => SetClient(ClientType.None);

        private void SetClient(ClientType client)
        {
            bool reconcile;
            lock (_sync)
            {
                if (_disposed)
                    return;
                _event_revision++;
                _desired_client = client;
                reconcile = StartReconcile();
            }
            if (reconcile)
                Reconcile();
        }

        private void SetInitialClient(ClientType client, long revision)
        {
            bool reconcile;
            lock (_sync)
            {
                if (_disposed || revision != _event_revision)
                    return;
                _desired_client = client;
                reconcile = StartReconcile();
            }
            if (reconcile)
                Reconcile();
        }

        private bool StartReconcile()
        {
            if (_reconciling)
                return false;
            _reconciling = true;
            return true;
        }

        private void Reconcile()
        {
            while (true)
            {
                IDisposable? removed = null;
                bool bind = false;
                lock (_sync)
                {
                    if (_disposed)
                    {
                        _reconciling = false;
                        return;
                    }
                    if (_desired_client != _client)
                    {
                        if (_active is null)
                        {
                            _reconciling = false;
                            return;
                        }
                        removed = _active;
                        _active = null;
                    }
                    else if (_active is null)
                    {
                        bind = true;
                    }
                    else
                    {
                        _reconciling = false;
                        return;
                    }
                }

                if (removed is not null)
                {
                    try
                    {
                        removed.Dispose();
                    }
                    catch
                    {
                        EndReconcile();
                        throw;
                    }
                    continue;
                }

                if (!bind)
                    continue;

                IDisposable active;
                try
                {
                    active = _bind();
                }
                catch
                {
                    EndReconcile();
                    throw;
                }

                bool keep;
                lock (_sync)
                {
                    keep = !_disposed && _desired_client == _client && _active is null;
                    if (keep)
                        _active = active;
                }
                if (!keep)
                {
                    try
                    {
                        active.Dispose();
                    }
                    catch
                    {
                        EndReconcile();
                        throw;
                    }
                }
            }
        }

        private void EndReconcile()
        {
            lock (_sync)
                _reconciling = false;
        }

        public void Dispose()
        {
            IDisposable? active;
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                active = _active;
                _active = null;
            }
            _interceptor.Connected -= Connected;
            _interceptor.Disconnected -= Disconnected;
            active?.Dispose();
        }
    }
}
