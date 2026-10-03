using System.Buffers.Binary;
using System.Net.Sockets;
using Qx;
using Qx.Diagnostics;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Interception.GEarth;

/// <summary>Represents a G-Earth extension that intercepts the hotel traffic G-Earth relays.</summary>
/// <remarks>
/// The extension connects to G-Earth on <c>127.0.0.1</c> and answers every intercepted packet, passing it
/// through unchanged when it cannot be parsed or a callback fails. An event subscriber that throws is logged
/// and skipped, so the other subscribers still run and the connection stays up.
/// </remarks>
public class GEarthExtension : IInterceptor, IDisposable
{
    private const string Category = "gearth";
    private const int MaxControlFrameLength = 16 * 1024 * 1024;

    private readonly GEarthOptions _options;
    private readonly InterceptDispatcher _dispatcher = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _catalog_sync = new();

    private TcpClient? _client;
    private NetworkStream? _stream;
    private SessionCatalogLease _session_catalog_lease;
    private int _connected_port;
    private int _activations;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="GEarthExtension"/> class.</summary>
    /// <param name="options">The connection settings and the info reported to G-Earth.</param>
    /// <param name="messages">The message manager to use, or <see langword="null"/> to create one from the embedded message map.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the port is outside 1 to 65535, the port search count is below 1 or the handshake timeout is not positive.
    /// </exception>
    public GEarthExtension(GEarthOptions options, MessageManager? messages = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        if (_options.Port is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "The G-Earth port is invalid.");
        if (_options.PortSearchCount < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "The G-Earth port search count is invalid.");
        if (_options.HandshakeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The G-Earth handshake timeout is invalid.");
        Messages = messages ?? MessageManager.CreateWithEmbeddedMap();
        _dispatcher.CallbackFailed += (intercept, error) =>
            Publish(InterceptFailed, intercept, error, nameof(InterceptFailed));
    }

    /// <summary>Gets the message manager that maps message names to headers for the session.</summary>
    public MessageManager Messages { get; }
    IMessageResolver IInterceptor.Messages => Messages;
    /// <summary>Gets or sets the selector that picks the message catalog when a session starts.</summary>
    /// <remarks>
    /// When no selector is set, or it returns <see langword="null"/> or throws, the catalog G-Earth sent in its
    /// handshake is used, if there was one.
    /// </remarks>
    public ISessionCatalogSelector? SessionCatalogSelector { get; set; }
    /// <summary>Gets or sets the readiness source that is awaited before a session's catalog is selected.</summary>
    public IMessageCatalogReadiness? CatalogReadiness { get; set; }
    /// <inheritdoc/>
    public Session? Session { get; private set; }
    /// <inheritdoc/>
    public bool IsConnected => Session is not null;
    /// <summary>Gets whether the connection to G-Earth is established and past the handshake.</summary>
    public bool IsInterceptorConnected { get; private set; }
    /// <summary>Gets how many times the user has activated the extension in G-Earth.</summary>
    public int Activations => Volatile.Read(ref _activations);
    /// <summary>Gets the port of the connected G-Earth instance, or 0 when not connected.</summary>
    public int ConnectedPort => Volatile.Read(ref _connected_port);

    /// <inheritdoc/>
    public event Action<Session>? Connected;
    /// <inheritdoc/>
    public event Action? Disconnected;
    /// <summary>Occurs when G-Earth sends the init frame to the extension.</summary>
    public event Action? Initialized;
    /// <summary>Occurs when the user activates the extension in G-Earth.</summary>
    public event Action? Activated;
    /// <summary>Occurs when the connection to G-Earth is established and the handshake succeeded.</summary>
    public event Action? InterceptorConnected;
    /// <summary>Occurs when an established connection to G-Earth closes.</summary>
    public event Action? InterceptorDisconnected;
    /// <inheritdoc/>
    public event Action<Intercept>? Intercepted;

    /// <summary>Occurs when an intercept callback throws.</summary>
    /// <remarks>
    /// The failure is isolated, so the remaining callbacks registered for the same message still run.
    /// </remarks>
    public event Action<Intercept, Exception>? InterceptFailed;

    /// <summary>Gets the message identifiers that could not be resolved against the loaded catalog.</summary>
    /// <remarks>Callbacks registered under these identifiers are bound to nothing and never run.</remarks>
    public IReadOnlyList<Identifier> UnresolvedInterceptors => _dispatcher.UnresolvedIdentifiers;

    /// <summary>Gets the semantic message keys that could not be resolved against the loaded catalog.</summary>
    /// <remarks>Callbacks registered under these keys are bound to nothing and never run.</remarks>
    public IReadOnlyList<MessageKey> UnresolvedSemanticInterceptors => _dispatcher.UnresolvedKeys;

    /// <inheritdoc/>
    public IDisposable Intercept(Header header, Action<Intercept> callback) => _dispatcher.Add(header, callback);
    /// <summary>Registers a callback for packets of a named message.</summary>
    /// <remarks>The identifier is resolved again whenever the session catalog changes.</remarks>
    /// <param name="identifier">The message to intercept.</param>
    /// <param name="callback">The callback that receives each matching packet.</param>
    /// <returns>A handle that removes the callback when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> has no message name or no direction.</exception>
    public IDisposable Intercept(Identifier identifier, Action<Intercept> callback) => _dispatcher.Add(identifier, callback, Messages);
    /// <summary>Registers a callback for packets of a semantic message.</summary>
    /// <remarks>The key is resolved again whenever the session catalog changes.</remarks>
    /// <param name="key">The semantic message key to intercept.</param>
    /// <param name="callback">The callback that receives each matching packet.</param>
    /// <returns>A handle that removes the callback when disposed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty.</exception>
    public IDisposable Intercept(MessageKey key, Action<Intercept> callback) => _dispatcher.Add(key, callback, Messages);

    /// <summary>Resolves every identifier and semantic key registration again against <see cref="Messages"/>.</summary>
    public void RebindInterceptors() => _dispatcher.Rebind(Messages, HasActiveCatalog());

    private bool HasActiveCatalog() =>
        Messages.ActiveCatalogBinding is not null && Messages.HasCatalog();

    /// <summary>Waits until <see cref="CatalogReadiness"/> reports that the message catalog is ready.</summary>
    /// <remarks>Completes immediately when <see cref="CatalogReadiness"/> is <see langword="null"/>.</remarks>
    /// <param name="cancellationToken">A token that cancels the wait.</param>
    /// <returns>A task that completes when the catalog is ready.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task WaitForCatalogBuildAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CatalogReadiness is not { } readiness)
            return;
        await readiness.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Connects to G-Earth and runs the extension until the connection closes.</summary>
    /// <remarks>
    /// G-Earth must send its first control frame within <see cref="GEarthOptions.HandshakeTimeout"/>. When
    /// <see cref="GEarthOptions.SearchPorts"/> is set, the ports from <see cref="GEarthOptions.Port"/> onwards
    /// are tried in order until one answers.
    /// </remarks>
    /// <param name="cancellationToken">A token that stops the extension.</param>
    /// <returns>A task that completes when G-Earth closes the connection.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the extension is disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the extension is already running.</exception>
    /// <exception cref="IOException">Thrown when port search is on and no G-Earth instance answered.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_options.SearchPorts)
        {
            await RunOnPortAsync(_options.Port, cancellationToken).ConfigureAwait(false);
            return;
        }

        Exception? last_error = null;
        int last_port = (int)Math.Min(
            ushort.MaxValue,
            (long)_options.Port + _options.PortSearchCount - 1);
        for (int port = _options.Port; port <= last_port; port++)
        {
            try
            {
                await RunOnPortAsync(port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                last_error = error;
            }
        }

        throw new IOException(
            $"No G-Earth instance responded on ports {_options.Port}-{last_port}.",
            last_error);
    }

    private async Task RunOnPortAsync(int port, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        NetworkStream? stream = null;
        bool connected = false;
        try
        {
            await client.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
            client.NoDelay = true;
            stream = client.GetStream();
            _writeLock.Wait(cancellationToken);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_stream is not null)
                    throw new InvalidOperationException("The G-Earth extension is already running.");
                _client = client;
                _stream = stream;
            }
            finally
            {
                _writeLock.Release();
            }

            var validated = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Task read_loop = ReadLoopAsync(stream, validated, cancellationToken);
            try
            {
                await validated.Task
                    .WaitAsync(_options.HandshakeTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                stream.Dispose();
                client.Dispose();
                try
                {
                    await read_loop.ConfigureAwait(false);
                }
                catch
                {
                }
                throw;
            }

            Diag.Info($"Connected to G-Earth on port {port}", Category);
            Volatile.Write(ref _connected_port, port);
            IsInterceptorConnected = true;
            connected = true;
            Publish(InterceptorConnected, nameof(InterceptorConnected));
            await read_loop.ConfigureAwait(false);
            Diag.Info("Read loop ended; G-Earth closed the connection", Category);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            if (connected)
                Diag.Error($"Read loop ended unexpectedly: {error}", Category);
            throw;
        }
        finally
        {
            try
            {
                if (connected)
                    EndSession();
            }
            finally
            {
                if (connected)
                {
                    IsInterceptorConnected = false;
                    Volatile.Write(ref _connected_port, 0);
                }
                _writeLock.Wait();
                try
                {
                    if (ReferenceEquals(_stream, stream))
                        _stream = null;
                    if (ReferenceEquals(_client, client))
                        _client = null;
                }
                finally
                {
                    _writeLock.Release();
                }
                stream?.Dispose();
                client.Dispose();
                if (connected)
                    Publish(InterceptorDisconnected, nameof(InterceptorDisconnected));
            }
        }
    }

    private async Task ReadLoopAsync(
        NetworkStream stream,
        TaskCompletionSource validated,
        CancellationToken cancellationToken)
    {
        byte[] lengthBuffer = new byte[4];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false))
                    break;

                int length = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                if (length is < 2 or > MaxControlFrameLength)
                    throw new InvalidDataException("The G-Earth control frame length is invalid.");

                byte[] frame = new byte[length];
                if (!await ReadExactAsync(stream, frame, cancellationToken).ConfigureAwait(false))
                    break;

                short header = BinaryPrimitives.ReadInt16BigEndian(frame);
                if (!IsGEarthControlHeader(header))
                    throw new InvalidDataException("The endpoint did not send a G-Earth control frame.");
                validated.TrySetResult();
                try
                {
                    await HandleFrameAsync(header, frame.AsMemory(2), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception error)
                {
                    Diag.Error($"Control frame {header} failed: {error}", Category);
                }
            }
        }
        catch (IOException error) when (
            validated.Task.IsCompletedSuccessfully &&
            error.InnerException is SocketException
            {
                SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted
            })
        {
            return;
        }
        finally
        {
            if (!validated.Task.IsCompleted)
                validated.TrySetException(new InvalidDataException("The endpoint closed before the G-Earth handshake."));
        }
    }

    private static bool IsGEarthControlHeader(short header) => header is
        GControl.Outgoing.OnDoubleClick or
        GControl.Outgoing.InfoRequest or
        GControl.Outgoing.PacketIntercept or
        GControl.Outgoing.FlagsCheck or
        GControl.Outgoing.ConnectionStart or
        GControl.Outgoing.ConnectionEnd or
        GControl.Outgoing.Init or
        GControl.Outgoing.UpdateHostInfo or
        GControl.Outgoing.PacketToStringResponse or
        GControl.Outgoing.StringToPacketResponse or
        GControl.Outgoing.FreeFlow;

    private Task HandleFrameAsync(short header, ReadOnlyMemory<byte> body, CancellationToken cancellation_token)
    {
        switch (header)
        {
            case GControl.Outgoing.InfoRequest:
                SendExtensionInfo();
                return Task.CompletedTask;
            case GControl.Outgoing.Init:
                Diag.Trace("Init received", Category);
                Publish(Initialized, nameof(Initialized));
                return Task.CompletedTask;
            case GControl.Outgoing.ConnectionStart:
                return StartConnectionAsync(body, cancellation_token);
            case GControl.Outgoing.ConnectionEnd:
                EndSession();
                return Task.CompletedTask;
            case GControl.Outgoing.PacketIntercept:
                HandleIntercept(body.Span);
                return Task.CompletedTask;
            case GControl.Outgoing.OnDoubleClick:
                Interlocked.Increment(ref _activations);
                Publish(Activated, nameof(Activated));
                return Task.CompletedTask;
            default:
                Diag.Trace($"Unhandled control header {header}", Category);
                return Task.CompletedTask;
        }
    }

    private void SendExtensionInfo()
    {
        var writer = new GControlWriter(GControl.Incoming.ExtensionInfo);
        writer.WriteString(_options.Title);
        writer.WriteString(_options.Author);
        writer.WriteString(_options.Version);
        writer.WriteString(_options.Description);
        writer.WriteBool(_options.OnClickUsed);
        writer.WriteBool(!string.IsNullOrEmpty(_options.File));
        writer.WriteString(_options.File);
        writer.WriteString(_options.Cookie);
        writer.WriteBool(_options.CanLeave);
        writer.WriteBool(_options.CanDelete);
        SendFrame(writer);
        Diag.Trace("Sent ExtensionInfo", Category);
    }

    private Task StartConnectionAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellation_token)
    {
        ConnectionStartInfo start;
        try
        {
            start = ParseConnectionStart(body.Span);
        }
        catch (Exception error)
        {
            Diag.Error($"Unable to read the connection start frame: {error.Message}", Category);
            EndSession();
            return Task.CompletedTask;
        }

        if (!string.Equals(start.ClientType, "FLASH", StringComparison.OrdinalIgnoreCase))
        {
            Diag.Warn("Rejected a connection start for an unsupported client type.", Category);
            EndSession();
            return Task.CompletedTask;
        }

        return StartConnectionCoreAsync(start, cancellation_token);
    }

    private async Task StartConnectionCoreAsync(
        ConnectionStartInfo start,
        CancellationToken cancellation_token)
    {
        Exception? preparation_error = null;
        if (CatalogReadiness is { } readiness)
        {
            try
            {
                await readiness.WaitUntilReadyAsync(cancellation_token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                preparation_error = error;
            }
        }

        var session = new Session(start.Host, start.Port, start.HotelVersion, start.ClientIdentifier);
        SessionCatalogBinding fallback = CreateFallbackBinding(session, start.Catalog);
        SessionCatalogBinding binding;

        lock (_catalog_sync)
        {
            binding = SelectCatalog(session, fallback);
            if (!_session_catalog_lease.IsEmpty)
                Messages.ClearSessionCatalog(_session_catalog_lease);
            _session_catalog_lease = Messages.BindSessionCatalog(binding);
            _dispatcher.Rebind(Messages, HasActiveCatalog());
            Session = session;
        }

        if (binding.Provenance.Origin == CatalogOrigin.ClientExtraction && binding.Catalog is { } extracted)
        {
            Diag.Info(
                $"Activated catalog for build {binding.Provenance.ClientVersion} with {extracted.HeaderCount} headers",
                "protocol");
            if (binding.Supplement is { } supplement)
            {
                Diag.Info(
                    $"Supplemented {supplement.AliasCount} unresolved semantic aliases from the exact-session {supplement.Provenance.Source} catalog",
                    "protocol");
            }
        }
        else
        {
            string reason = preparation_error is null
                ? "No matching QX catalog was available after preparation"
                : $"QX catalog preparation failed: {preparation_error.Message}";
            if (binding.Provenance.Origin == CatalogOrigin.GEarthHandshake && binding.Catalog is { } catalog)
            {
                Diag.Warn($"{reason}; using the G-Earth catalog for this session.", "protocol");
                Diag.Info($"Loaded {catalog.Count} messages from G-Earth", Category);
            }
            else
            {
                Diag.Error(
                    $"{reason}; no fallback catalog is available, so packets will pass through unparsed for this session.",
                    "protocol");
            }
        }
        Diag.Info($"Session started: {start.ClientType} {start.HotelVersion}", Category);
        Publish(Connected, session, nameof(Connected));
    }

    private SessionCatalogBinding SelectCatalog(
        Session session,
        SessionCatalogBinding fallback,
        SessionCatalogSelectionIntent intent = SessionCatalogSelectionIntent.SessionStart)
    {
        SessionCatalogBinding? selected;
        try
        {
            selected = SessionCatalogSelector?.Select(new SessionCatalogRequest(
                session.HotelVersion,
                session.ClientIdentifier,
                fallback,
                intent));
        }
        catch (Exception error)
        {
            Diag.Warn($"Unable to select the session catalog: {error.Message}", Category);
            return fallback;
        }
        return selected ?? fallback;
    }

    private static SessionCatalogBinding CreateFallbackBinding(Session session, MessageCatalog? catalog) =>
        catalog is null
            ? new SessionCatalogBinding(
                null,
                new CatalogProvenance(
                    CatalogOrigin.Unavailable,
                    "G-Earth",
                    session.HotelVersion))
            : new SessionCatalogBinding(
                catalog,
                new CatalogProvenance(
                    CatalogOrigin.GEarthHandshake,
                    "G-Earth",
                    session.HotelVersion));

    private static ConnectionStartInfo ParseConnectionStart(ReadOnlySpan<byte> body)
    {
        var reader = new GControlReader(body);
        string host = reader.ReadString();
        int port = reader.ReadInt();
        string hotel_version = reader.ReadString();
        string client_identifier = reader.ReadString();
        string client_type = reader.ReadString();
        MessageCatalog? catalog = null;
        if (reader.Available >= 4)
        {
            var loaded = new MessageCatalog();
            int count = reader.ReadInt();
            if (count < 0 || count > reader.Available / 13)
                throw new InvalidDataException("G-Earth returned an invalid message catalog size.");
            for (int i = 0; i < count; i++)
            {
                int headerId = reader.ReadInt();
                _ = reader.ReadString();
                string name = reader.ReadString();
                _ = reader.ReadString();
                bool isOutgoing = reader.ReadBool();
                _ = reader.ReadString();
                MessageDirection direction = isOutgoing ? MessageDirection.Out : MessageDirection.In;
                if (name != "NULL")
                    loaded.Add(direction, headerId, name);
            }
            if (count > 0)
                catalog = loaded;
        }
        return new ConnectionStartInfo(
            host,
            port,
            hotel_version,
            client_identifier,
            client_type,
            catalog);
    }

    private sealed record ConnectionStartInfo(
        string Host,
        int Port,
        string HotelVersion,
        string ClientIdentifier,
        string ClientType,
        MessageCatalog? Catalog);

    private void EndSession()
    {
        lock (_catalog_sync)
        {
            if (Session is null)
                return;
            if (!_session_catalog_lease.IsEmpty)
                Messages.ClearSessionCatalog(_session_catalog_lease);
            _session_catalog_lease = default;
            Session = null;
            _dispatcher.Rebind(Messages, false);
        }
        Publish(Disconnected, nameof(Disconnected));
    }

    private static void Publish(Action? subscribers, string event_name) =>
        PublishSubscribers(subscribers, default(ValueTuple), static (subscriber, _) => subscriber(), event_name);

    private static void Publish<T>(Action<T>? subscribers, T value, string event_name) =>
        PublishSubscribers(subscribers, value, static (subscriber, argument) => subscriber(argument), event_name);

    private static void Publish<T1, T2>(Action<T1, T2>? subscribers, T1 first, T2 second, string event_name) =>
        PublishSubscribers(
            subscribers,
            (first, second),
            static (subscriber, arguments) => subscriber(arguments.first, arguments.second),
            event_name);

    private static void PublishSubscribers<TSubscriber, TState>(
        TSubscriber? subscribers,
        TState state,
        Action<TSubscriber, TState> invoke,
        string event_name)
        where TSubscriber : Delegate
    {
        if (subscribers is null)
            return;

        foreach (TSubscriber subscriber in subscribers.GetInvocationList())
        {
            try
            {
                invoke(subscriber, state);
            }
            catch (Exception error)
            {
                ReportSubscriberError(event_name, error);
            }
        }
    }

    private static void ReportSubscriberError(string event_name, Exception error)
    {
        try
        {
            Diag.Error($"{event_name} subscriber threw: {error}", Category);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Answers one intercepted packet. G-Earth holds the hotel connection open until every
    /// extension replies, so this must always produce a reply: a packet that cannot be parsed or
    /// dispatched is echoed back untouched rather than being allowed to fail the frame.
    /// </summary>
    private void HandleIntercept(ReadOnlySpan<byte> body)
    {
        string messageString;
        int formatId;
        try
        {
            var reader = new GControlReader(body);
            messageString = reader.ReadLongString();
            formatId = reader.IsEof ? 0 : reader.ReadInt();
        }
        catch (Exception error)
        {
            // The frame itself is unreadable, so there is nothing to echo back.
            Diag.Error($"Unreadable intercept frame: {error.Message}", Category);
            return;
        }

        bool replied = false;
        try
        {
            replied = ForwardIntercept(messageString, formatId);
        }
        catch (Exception error)
        {
            Diag.Error($"Intercept handling failed, passing the packet through: {error}", Category);
        }

        if (!replied)
            EchoIntercept(messageString, formatId);
    }

    /// <summary>
    /// Sends an intercepted packet back exactly as it arrived, which is how a packet that could
    /// not be understood is passed through without stalling the connection.
    /// </summary>
    private void EchoIntercept(string messageString, int formatId)
    {
        try
        {
            var writer = new GControlWriter(GControl.Incoming.ManipulatedPacket);
            writer.WriteLongString(messageString);
            writer.WriteInt(formatId);
            SendFrame(writer);
        }
        catch (Exception error)
        {
            Diag.Error($"Unable to answer an intercepted packet: {error.Message}", Category);
        }
    }

    /// <returns>Whether the packet was answered; a false result still needs an echoed reply.</returns>
    private bool ForwardIntercept(string messageString, int formatId)
    {
        lock (_catalog_sync)
            return ForwardBoundIntercept(messageString, formatId);
    }

    private bool ForwardBoundIntercept(string messageString, int formatId)
    {
        HMessage message = HMessage.Parse(messageString);
        message.Packet.Context = Messages.GetParserContext();
        Packet originalPacket = message.Packet;
        Packet? serializedPacket = null;
        bool replied = false;

        try
        {
            byte[] before = originalPacket.Buffer.Span.ToArray();
            var intercept = new Intercept { Packet = originalPacket, Sequence = message.Index };

            try
            {
                Publish(Intercepted, intercept, nameof(Intercepted));
                _dispatcher.Dispatch(intercept);
            }
            catch (Exception error)
            {
                Diag.Error($"Interceptor dispatch failed: {error}", Category);
            }

            serializedPacket = intercept.Packet;
            message.IsBlocked = intercept.IsBlocked;
            message.Packet = serializedPacket;
            message.IsEdited = !before.AsSpan().SequenceEqual(serializedPacket.Buffer.Span);

            var writer = new GControlWriter(GControl.Incoming.ManipulatedPacket);
            writer.WriteLongString(message.Stringify());
            writer.WriteInt(formatId);
            SendFrame(writer);
            replied = true;
        }
        finally
        {
            if (serializedPacket is not null && !ReferenceEquals(serializedPacket, originalPacket))
                serializedPacket.Dispose();
            originalPacket.Dispose();
        }

        return replied;
    }

    /// <summary>Sends a packet to the client or the server within the current session.</summary>
    /// <param name="packet">The packet to send.</param>
    /// <exception cref="InvalidOperationException">Thrown when no session is active.</exception>
    public void Send(IPacket packet)
    {
        Session? expected_session;
        lock (_catalog_sync)
            expected_session = Session;
        Send(packet, expected_session);
    }

    /// <inheritdoc/>
    public InterceptorSessionCatalog CaptureSessionCatalog()
    {
        lock (_catalog_sync)
            return new InterceptorSessionCatalog(Session, Messages.ActiveCatalogBinding);
    }

    /// <summary>Sends a packet only when <paramref name="expectedSession"/> is still the active session.</summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="expectedSession">The session the packet belongs to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="expectedSession"/> is <see langword="null"/> or not the active session.
    /// </exception>
    public void Send(IPacket packet, Session? expectedSession)
    {
        Send(packet, expectedSession, null, false, null);
    }

    /// <inheritdoc/>
    public void Send(
        IPacket packet,
        Session? expectedSession,
        SessionCatalogBinding? expectedCatalog)
    {
        Send(packet, expectedSession, expectedCatalog, true, null);
    }

    /// <inheritdoc/>
    public void Send(
        IPacket packet,
        Session? expectedSession,
        SessionCatalogBinding? expectedCatalog,
        Action? dispatchGuard)
    {
        Send(packet, expectedSession, expectedCatalog, true, dispatchGuard);
    }

    private void Send(
        IPacket packet,
        Session? expected_session,
        SessionCatalogBinding? expected_catalog,
        bool require_catalog,
        Action? dispatch_guard)
    {
        lock (_catalog_sync)
        {
            if (expected_session is null ||
                !ReferenceEquals(Session, expected_session))
            {
                throw new InvalidOperationException(
                    "The connection session changed before the packet could be sent.");
            }
            if (require_catalog &&
                !ReferenceEquals(Messages.ActiveCatalogBinding, expected_catalog))
            {
                throw new InvalidOperationException(
                    "The session catalog changed before the packet could be sent.");
            }
            dispatch_guard?.Invoke();

            byte side = packet.Header.Direction == MessageDirection.In ? (byte)0 : (byte)1;
            byte[] raw = EvaWire.FromPacket(packet);

            var writer = new GControlWriter(GControl.Incoming.SendMessage);
            writer.WriteByte(side);
            writer.WriteInt(raw.Length);
            writer.WriteBytes(raw);
            writer.WriteInt(0);
            SendFrame(writer);
        }
    }

    private void SendFrame(GControlWriter writer)
    {
        byte[] frame = writer.ToFrame();
        _writeLock.Wait();
        try
        {
            _stream!.Write(frame, 0, frame.Length);
            _stream.Flush();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task<bool> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    /// <summary>Closes the connection to G-Earth, which ends <see cref="RunAsync"/>.</summary>
    public void Dispose()
    {
        _writeLock.Wait();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            _stream?.Dispose();
            _client?.Dispose();
        }
        finally
        {
            _writeLock.Release();
        }
        GC.SuppressFinalize(this);
    }
}
