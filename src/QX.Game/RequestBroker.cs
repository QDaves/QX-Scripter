using System.Collections.Concurrent;
using System.Diagnostics;
using Qx;
using Qx.Game.Protocol;
using Qx.Interception;
using Qx.Messages;
using Qx.Protocol;

namespace Qx.Game;

internal interface IRequestClock
{
    long Timestamp();
    TimeSpan ElapsedSince(long timestamp);
    Task Delay(TimeSpan delay, CancellationToken cancellation_token);
}

internal sealed class SystemRequestClock : IRequestClock
{
    public static SystemRequestClock Instance { get; } = new();

    public long Timestamp() => Stopwatch.GetTimestamp();

    public TimeSpan ElapsedSince(long timestamp) => Stopwatch.GetElapsedTime(timestamp);

    public Task Delay(TimeSpan delay, CancellationToken cancellation_token) =>
        Task.Delay(delay, cancellation_token);
}

public sealed class RequestBroker : GameStateManager
{
    private readonly IRequestClock _clock;
    private readonly ConcurrentDictionary<WireKey, SemaphoreSlim> _response_locks = [];
    private readonly ConcurrentDictionary<WireKey, SemaphoreSlim> _outgoing_locks = [];
    private readonly ConcurrentDictionary<WireKey, long> _last_request_ticks = [];
    private readonly object _connection_sync = new();
    private CancellationTokenSource _connection_closed = new();
    private bool _connection_unavailable;
    private int _dispose_state;
    private TimeSpan _minimum_request_interval = TimeSpan.FromMilliseconds(40);
    private TimeSpan _retry_delay = TimeSpan.FromMilliseconds(150);

    public RequestBroker() : this(SystemRequestClock.Instance)
    {
    }

    internal RequestBroker(IRequestClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public TimeSpan MinimumRequestInterval
    {
        get => _minimum_request_interval;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            _minimum_request_interval = value;
        }
    }

    public TimeSpan RetryDelay
    {
        get => _retry_delay;
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));
            _retry_delay = value;
        }
    }

    protected override void OnAttach()
    {
        lock (_connection_sync)
            _connection_unavailable = !Interceptor.IsConnected;
        OnConnected(_ =>
        {
            lock (_connection_sync)
                _connection_unavailable = false;
        });
    }

    public Task<TResponse> RequestAsync<TResponse>(
        string outName,
        object[] request,
        string inName,
        Func<TResponse, bool>? match = null,
        int timeoutMs = 10000,
        bool block = true,
        CancellationToken cancellationToken = default) where TResponse : IParserComposer<TResponse> =>
        RequestAsync(
            outName,
            request,
            inName,
            match,
            timeoutMs,
            block,
            cancellationToken,
            1);

    public Task<TResponse> RequestAsync<TResponse>(
        MessageKey outgoingKey,
        object[] request,
        MessageKey incomingKey,
        Func<TResponse, bool>? match = null,
        int timeoutMs = 10000,
        bool block = true,
        CancellationToken cancellationToken = default) where TResponse : IParserComposer<TResponse> =>
        RequestAsync(
            outgoingKey,
            request,
            incomingKey,
            match,
            timeoutMs,
            block,
            cancellationToken,
            1);

    public Task<TResponse> RequestAsync<TResponse>(
        string outName,
        object[] request,
        string inName,
        Func<TResponse, bool>? match,
        int timeoutMs,
        bool block,
        CancellationToken cancellationToken,
        int maxAttempts) where TResponse : IParserComposer<TResponse> =>
        AwaitResponse<TResponse>(
            outName,
            inName,
            _ => SendRequest(outName, request),
            match,
            timeoutMs,
            block,
            cancellationToken,
            maxAttempts);

    public Task<TResponse> RequestAsync<TResponse>(
        MessageKey outgoingKey,
        object[] request,
        MessageKey incomingKey,
        Func<TResponse, bool>? match,
        int timeoutMs,
        bool block,
        CancellationToken cancellationToken,
        int maxAttempts) where TResponse : IParserComposer<TResponse> =>
        AwaitResponse<TResponse>(
            outgoingKey.Value,
            incomingKey.Value,
            _ => SendRequest(outgoingKey, request),
            match,
            timeoutMs,
            block,
            cancellationToken,
            maxAttempts,
            outgoingKey,
            incomingKey);

    public Task<TResponse> RequestAsync<TResponse>(
        string outName,
        IComposer request,
        string inName,
        Func<TResponse, bool>? match = null,
        int timeoutMs = 10000,
        bool block = true,
        CancellationToken cancellationToken = default) where TResponse : IParserComposer<TResponse> =>
        RequestAsync(
            outName,
            request,
            inName,
            match,
            timeoutMs,
            block,
            cancellationToken,
            1);

    public Task<TResponse> RequestAsync<TResponse>(
        MessageKey outgoingKey,
        IComposer request,
        MessageKey incomingKey,
        Func<TResponse, bool>? match = null,
        int timeoutMs = 10000,
        bool block = true,
        CancellationToken cancellationToken = default) where TResponse : IParserComposer<TResponse> =>
        RequestAsync(
            outgoingKey,
            request,
            incomingKey,
            match,
            timeoutMs,
            block,
            cancellationToken,
            1);

    public Task<TResponse> RequestAsync<TResponse>(
        string outName,
        IComposer request,
        string inName,
        Func<TResponse, bool>? match,
        int timeoutMs,
        bool block,
        CancellationToken cancellationToken,
        int maxAttempts) where TResponse : IParserComposer<TResponse> =>
        AwaitResponse<TResponse>(
            outName,
            inName,
            _ => SendComposer(outName, request),
            match,
            timeoutMs,
            block,
            cancellationToken,
            maxAttempts);

    public Task<TResponse> RequestAsync<TResponse>(
        MessageKey outgoingKey,
        IComposer request,
        MessageKey incomingKey,
        Func<TResponse, bool>? match,
        int timeoutMs,
        bool block,
        CancellationToken cancellationToken,
        int maxAttempts) where TResponse : IParserComposer<TResponse> =>
        AwaitResponse<TResponse>(
            outgoingKey.Value,
            incomingKey.Value,
            _ => SendComposer(outgoingKey, request),
            match,
            timeoutMs,
            block,
            cancellationToken,
            maxAttempts,
            outgoingKey,
            incomingKey);

    public Task<TResponse> RequestAsync<TRequest, TResponse>(
        MessageContract<TRequest> outgoingContract,
        TRequest request,
        MessageContract<TResponse> incomingContract,
        Func<TResponse, bool>? match = null,
        int timeoutMs = 10000,
        bool block = true,
        CancellationToken cancellationToken = default,
        int maxAttempts = 1)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        ArgumentNullException.ThrowIfNull(outgoingContract);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(incomingContract);
        return AwaitResponse(
            outgoingContract.Key.Value,
            incomingContract.Key.Value,
            _ => SendMessage(outgoingContract, request),
            match,
            timeoutMs,
            block,
            cancellationToken,
            maxAttempts,
            outgoingContract.Key,
            incomingContract.Key,
            incomingContract);
    }

    internal Task<TResponse> RequestAsync<TRequest, TResponse>(
        MessageContract<TRequest> outgoing_contract,
        TRequest request,
        MessageContract<TResponse> incoming_contract,
        Session expected_session,
        Func<TResponse, bool>? match = null,
        int timeout_ms = 10000,
        bool block = true,
        CancellationToken cancellation_token = default,
        int max_attempts = 1,
        Action? dispatch_guard = null,
        Action? attempt_start = null)
        where TRequest : IParserComposer<TRequest>
        where TResponse : IParserComposer<TResponse>
    {
        ArgumentNullException.ThrowIfNull(outgoing_contract);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(incoming_contract);
        ArgumentNullException.ThrowIfNull(expected_session);
        return AwaitResponse(
            outgoing_contract.Key.Value,
            incoming_contract.Key.Value,
            token => SendMessage(
                outgoing_contract,
                request,
                expected_session,
                token,
                dispatch_guard),
            match,
            timeout_ms,
            block,
            cancellation_token,
            max_attempts,
            outgoing_contract.Key,
            incoming_contract.Key,
            incoming_contract,
            attempt_start);
    }

    private async Task<TResponse> AwaitResponse<TResponse>(
        string out_name,
        string in_name,
        Action<CancellationToken> send,
        Func<TResponse, bool>? match,
        int timeout_ms,
        bool block,
        CancellationToken cancellation_token,
        int max_attempts,
        MessageKey outgoing_message_key = default,
        MessageKey incoming_message_key = default,
        MessageContract<TResponse>? response_contract = null,
        Action? attempt_start = null)
        where TResponse : IParserComposer<TResponse>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(out_name);
        ArgumentException.ThrowIfNullOrWhiteSpace(in_name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_ms, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(max_attempts, 1);

        WireKey outgoing_key = outgoing_message_key.IsEmpty
            ? ResolveWireKey(Direction.Out, out_name)
            : ResolveWireKey(Direction.Out, outgoing_message_key);
        WireKey incoming_key = incoming_message_key.IsEmpty
            ? ResolveWireKey(Direction.In, in_name)
            : ResolveWireKey(Direction.In, incoming_message_key);
        long started = _clock.Timestamp();
        CancellationToken connection_closed;
        lock (_connection_sync)
        {
            if (_connection_unavailable)
                throw new RequestDisconnectedException(out_name, in_name);
            connection_closed = _connection_closed.Token;
        }

        using var timeout_cancellation = new CancellationTokenSource(timeout_ms);
        using CancellationTokenSource request_cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellation_token,
                connection_closed,
                timeout_cancellation.Token);
        CancellationToken request_token = request_cancellation.Token;
        SemaphoreSlim response_lock = _response_locks.GetOrAdd(
            incoming_key,
            static _ => new SemaphoreSlim(1, 1));
        try
        {
            await response_lock.WaitAsync(request_token);
        }
        catch (OperationCanceledException) when (
            connection_closed.IsCancellationRequested &&
            !cancellation_token.IsCancellationRequested)
        {
            throw new RequestDisconnectedException(out_name, in_name);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (timeout_cancellation.IsCancellationRequested)
        {
            throw new RequestTimeoutException(out_name, in_name, timeout_ms);
        }

        try
        {
            for (int attempt = 1; ; attempt++)
            {
                request_token.ThrowIfCancellationRequested();
                try
                {
                    attempt_start?.Invoke();
                }
                catch (Exception) when (
                    connection_closed.IsCancellationRequested &&
                    !cancellation_token.IsCancellationRequested)
                {
                    throw new RequestDisconnectedException(out_name, in_name);
                }
                catch (Exception) when (cancellation_token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellation_token);
                }
                catch (Exception) when (timeout_cancellation.IsCancellationRequested)
                {
                    throw new RequestTimeoutException(out_name, in_name, timeout_ms);
                }
                SemaphoreSlim outgoing_lock = _outgoing_locks.GetOrAdd(
                    outgoing_key,
                    static _ => new SemaphoreSlim(1, 1));
                await outgoing_lock.WaitAsync(request_token);
                var reservation = new OutgoingReservation(outgoing_lock);
                Task<TResponse> response;
                try
                {
                    await WaitForRequestInterval(outgoing_key, request_token);
                    int attempt_timeout_ms = AttemptTimeout(
                        started,
                        timeout_ms,
                        attempt,
                        max_attempts);
                    request_token.ThrowIfCancellationRequested();
                    response = AwaitSingleResponse<TResponse>(
                        out_name,
                        in_name,
                        outgoing_key,
                        incoming_key.Header,
                        send,
                        match,
                        attempt_timeout_ms,
                        block,
                        connection_closed,
                        request_token,
                        reservation,
                        response_contract);
                }
                catch
                {
                    reservation.Dispose();
                    throw;
                }

                try
                {
                    return await response;
                }
                catch (RequestTimeoutException) when (attempt < max_attempts)
                {
                    await _clock.Delay(RetryDelay * attempt, request_token);
                }
                catch (RequestTimeoutException)
                {
                    throw new RequestTimeoutException(out_name, in_name, timeout_ms);
                }
            }
        }
        catch (OperationCanceledException) when (
            connection_closed.IsCancellationRequested &&
            !cancellation_token.IsCancellationRequested)
        {
            throw new RequestDisconnectedException(out_name, in_name);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (timeout_cancellation.IsCancellationRequested)
        {
            throw new RequestTimeoutException(out_name, in_name, timeout_ms);
        }
        finally
        {
            response_lock.Release();
        }
    }

    private async Task<TResponse> AwaitSingleResponse<TResponse>(
        string out_name,
        string in_name,
        WireKey outgoing_key,
        Header incoming_header,
        Action<CancellationToken> send,
        Func<TResponse, bool>? match,
        int timeout_ms,
        bool block,
        CancellationToken connection_closed,
        CancellationToken cancellation_token,
        OutgoingReservation reservation,
        MessageContract<TResponse>? response_contract)
        where TResponse : IParserComposer<TResponse>
    {
        using IDisposable outgoing_reservation = reservation;
        var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable subscription = Interceptor.Intercept(incoming_header, intercept =>
        {
            TResponse response;
            try
            {
                using IPacket packet = intercept.Packet.Copy();
                PacketReader reader = packet.Reader();
                response = response_contract is null
                    ? reader.Parse<TResponse>()
                    : response_contract.Parse(in reader);
                if (reader.Available != 0)
                {
                    completion.TrySetException(new ResponseParseException(
                        in_name,
                        typeof(TResponse).Name,
                        $"{reader.Available} trailing bytes remain."));
                    return;
                }
            }
            catch (Exception error)
            {
                completion.TrySetException(new ResponseParseException(
                    in_name,
                    typeof(TResponse).Name,
                    error.Message,
                    error));
                return;
            }

            bool matched;
            try
            {
                matched = match?.Invoke(response) ?? true;
            }
            catch (Exception error)
            {
                completion.TrySetException(new ResponseMatchException(
                    in_name,
                    typeof(TResponse).Name,
                    error));
                return;
            }

            if (!matched)
                return;
            if (completion.TrySetResult(response) && block)
                intercept.Block();
        });

        using var timeout = new CancellationTokenSource(timeout_ms);
        using CancellationTokenRegistration cancellation_registration = cancellation_token.Register(
            () => completion.TrySetCanceled(cancellation_token));
        using CancellationTokenRegistration timeout_registration = timeout.Token.Register(
            () => completion.TrySetException(new RequestTimeoutException(out_name, in_name, timeout_ms)));
        using CancellationTokenRegistration disconnect_registration = connection_closed.Register(
            () => completion.TrySetException(new RequestDisconnectedException(out_name, in_name)));

        lock (_connection_sync)
        {
            if (_connection_unavailable ||
                connection_closed.IsCancellationRequested ||
                _connection_closed.Token != connection_closed)
            {
                throw new RequestDisconnectedException(out_name, in_name);
            }
            cancellation_token.ThrowIfCancellationRequested();
            send(cancellation_token);
            _last_request_ticks[outgoing_key] = _clock.Timestamp();
        }
        reservation.Dispose();
        return await completion.Task;
    }

    private async Task WaitForRequestInterval(WireKey outgoing_key, CancellationToken cancellation_token)
    {
        if (MinimumRequestInterval <= TimeSpan.Zero ||
            !_last_request_ticks.TryGetValue(outgoing_key, out long previous))
        {
            return;
        }

        TimeSpan elapsed = _clock.ElapsedSince(previous);
        TimeSpan remaining = MinimumRequestInterval - elapsed;
        if (remaining > TimeSpan.Zero)
            await _clock.Delay(remaining, cancellation_token);
    }

    private int AttemptTimeout(
        long started,
        int timeout_ms,
        int attempt,
        int max_attempts)
    {
        double remaining_ms = timeout_ms - _clock.ElapsedSince(started).TotalMilliseconds;
        int attempts_left = max_attempts - attempt + 1;
        if (remaining_ms <= 1 || attempts_left <= 1)
            return Math.Max(1, (int)Math.Ceiling(remaining_ms));
        return Math.Max(1, (int)Math.Ceiling(remaining_ms / attempts_left));
    }

    public void SendComposer(string name, IComposer composer) =>
        SendMessage(name, composer);

    public void SendComposer(MessageKey key, IComposer composer) =>
        SendMessage(key, composer);

    public void SendComposer<T>(MessageContract<T> contract, T composer)
        where T : IParserComposer<T> =>
        SendMessage(contract, composer);

    /// <summary>
    /// Sends one outgoing message that nothing is waiting on an answer to.
    /// </summary>
    /// <remarks>
    /// The same write the requests use, without the wait — so anything outside the game layer that
    /// needs to say something to the hotel goes through the one path that knows how each client
    /// wants it written, rather than building a packet of its own.
    /// </remarks>
    public void SendToServer(string name, params object[] values) => Send(name, values);

    private void SendRequest(string name, object[] values) =>
        Send(name, values);

    private void SendRequest(MessageKey key, object[] values) =>
        Send(key, values);

    private WireKey ResolveWireKey(Direction direction, string name)
    {
        var identifier = new Identifier(ClientType.None, direction, name);
        if (!Interceptor.Messages.TryGetHeader(identifier, out Header header))
            throw new InvalidOperationException($"Unknown {direction.ToString().ToLowerInvariant()} message '{name}'.");
        ClientType client = Interceptor.Session?.Client ?? Interceptor.Messages.ActiveClient;
        if (client is ClientType.None)
            client = ClientType.Flash;
        return new WireKey(client, header);
    }

    private WireKey ResolveWireKey(Direction direction, MessageKey key)
    {
        if (key.IsEmpty ||
            !Interceptor.Messages.TryGetHeader(key, out Header header) ||
            header.Direction != direction)
        {
            throw new InvalidOperationException(
                $"Unknown {direction.ToString().ToLowerInvariant()} message '{key.Value}'.");
        }
        ClientType client = Interceptor.Session?.Client ?? Interceptor.Messages.ActiveClient;
        if (client is ClientType.None)
            client = ClientType.Flash;
        return new WireKey(client, header);
    }

    protected override void Reset()
    {
        CancellationTokenSource previous;
        lock (_connection_sync)
        {
            previous = _connection_closed;
            _connection_closed = new CancellationTokenSource();
            _connection_unavailable = true;
        }
        previous.Cancel();
        previous.Dispose();
        _last_request_ticks.Clear();
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _dispose_state, 1) != 0)
            return;
        base.Dispose();
        lock (_connection_sync)
            _connection_closed.Dispose();
        _response_locks.Clear();
        _outgoing_locks.Clear();
    }

    private readonly record struct WireKey(ClientType Client, Header Header);

    private sealed class OutgoingReservation(SemaphoreSlim outgoing_lock) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                outgoing_lock.Release();
        }
    }
}
