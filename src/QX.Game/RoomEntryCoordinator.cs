using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

/// <summary>
/// Specifies the outcome of a room entry attempt.
/// </summary>
public enum RoomEntryStatus
{
    /// <summary>An entry into a room that is ready.</summary>
    Success,
    /// <summary>An entry the server denied access for.</summary>
    Denied,
    /// <summary>An entry into a room the server could not find.</summary>
    NotFound,
    /// <summary>An entry that ended because the room connection failed, the room was left or the hotel connection closed.</summary>
    ConnectionError
}

/// <summary>
/// Represents the result of a room entry attempt.
/// </summary>
/// <param name="RoomId">The id of the room the entry was for.</param>
/// <param name="Status">The outcome of the entry attempt.</param>
/// <param name="Failure">The failure the server reported when it could not connect to the room, or <see langword="null"/>.</param>
/// <param name="Exit">The exit that ended the entry attempt when the room was left before entry completed, or <see langword="null"/>.</param>
public sealed record RoomEntryResult(
    Id RoomId,
    RoomEntryStatus Status,
    RoomConnectionFailure? Failure = null,
    RoomExitState? Exit = null)
{
    /// <summary>Gets whether the entry succeeded.</summary>
    public bool IsSuccess => Status is RoomEntryStatus.Success;
}

/// <summary>
/// Thrown when a room entry does not complete within its timeout.
/// </summary>
/// <param name="roomId">The id of the room the entry was for.</param>
/// <param name="timeoutMs">The timeout that elapsed, in milliseconds.</param>
public sealed class RoomEntryTimeoutException(Id roomId, int timeoutMs)
    : TimeoutException($"Room entry for '{roomId}' timed out after {timeoutMs} ms.")
{
    /// <summary>Gets the id of the room the entry was for.</summary>
    public Id RoomId { get; } = roomId;
    /// <summary>Gets the timeout that elapsed, in milliseconds.</summary>
    public int TimeoutMs { get; } = timeoutMs;
}

/// <summary>
/// Thrown when a room entry is replaced by a newer entry request.
/// </summary>
/// <param name="roomId">The id of the room the replaced entry was for.</param>
/// <param name="replacementRoomId">The id of the room the newer entry is for.</param>
public sealed class RoomEntryReplacedException(Id roomId, Id replacementRoomId)
    : InvalidOperationException(
        $"Room entry for '{roomId}' was replaced by a request for '{replacementRoomId}'.")
{
    /// <summary>Gets the id of the room the replaced entry was for.</summary>
    public Id RoomId { get; } = roomId;
    /// <summary>Gets the id of the room the newer entry is for.</summary>
    public Id ReplacementRoomId { get; } = replacementRoomId;
}

/// <summary>
/// Provides room entry requests that wait for the entry result.
/// </summary>
/// <remarks>
/// Only one entry attempt is active at a time. The result is taken from the room manager's ready,
/// entered, access state, connection failure and exit events, and from the hotel connection closing.
/// All members are safe to call from any thread.
/// </remarks>
public sealed class RoomEntryCoordinator : IDisposable
{
    private readonly RoomManager _room;
    private readonly object _sync = new();
    private RoomEntryAttempt? _active;
    private IInterceptor? _interceptor;
    private bool _disposed;

    internal RoomEntryCoordinator(RoomManager room)
    {
        ArgumentNullException.ThrowIfNull(room);
        _room = room;
        _room.Ready += RoomProgressed;
        _room.Entered += RoomProgressed;
        _room.AccessStateChanged += AccessStateChanged;
        _room.ConnectionFailed += ConnectionFailed;
        _room.Exited += RoomExited;
    }

    internal void Attach(IInterceptor interceptor)
    {
        ArgumentNullException.ThrowIfNull(interceptor);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_interceptor is not null)
                throw new InvalidOperationException("The room entry coordinator is already attached.");
            _interceptor = interceptor;
            _interceptor.Disconnected += Disconnected;
        }
    }

    /// <summary>Sends a room entry request and waits for the entry result.</summary>
    /// <param name="roomId">The id of the room to enter.</param>
    /// <param name="send">The action that sends the entry request, called once while the attempt is registered.</param>
    /// <param name="timeoutMs">The time to wait for the result, in milliseconds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes with the entry result.</returns>
    /// <remarks>
    /// A pending attempt started earlier fails with <see cref="RoomEntryReplacedException"/>. The
    /// attempt succeeds once the room with <paramref name="roomId"/> is entered and ready.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="send"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="roomId"/> or <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the coordinator is disposed.</exception>
    /// <exception cref="RoomEntryTimeoutException">Thrown when no result arrives within the timeout.</exception>
    /// <exception cref="RoomEntryReplacedException">Thrown when a newer entry request replaces the attempt.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public Task<RoomEntryResult> EnsureAsync(
        Id roomId,
        Action send,
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual((long)roomId, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeoutMs, 0);
        cancellationToken.ThrowIfCancellationRequested();

        var attempt = new RoomEntryAttempt(roomId);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();

            RoomEntryAttempt? previous = _active;
            _active = attempt;
            previous?.Completion.TrySetException(
                new RoomEntryReplacedException(previous.RoomId, roomId));

            try
            {
                send();
            }
            catch
            {
                if (ReferenceEquals(_active, attempt))
                    _active = null;
                throw;
            }
        }

        return AwaitResult(attempt, timeoutMs, cancellationToken);
    }

    private async Task<RoomEntryResult> AwaitResult(
        RoomEntryAttempt attempt,
        int timeout_ms,
        CancellationToken cancellation_token)
    {
        try
        {
            return await attempt.Completion.Task
                .WaitAsync(TimeSpan.FromMilliseconds(timeout_ms), cancellation_token)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new RoomEntryTimeoutException(attempt.RoomId, timeout_ms);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_active, attempt))
                    _active = null;
            }
        }
    }

    private void RoomProgressed()
    {
        RoomEntryAttempt? attempt = ActiveAttempt();
        if (attempt is null)
            return;

        bool entered = _room.Capture(room =>
            room.RoomId == attempt.RoomId &&
            room.IsInRoom &&
            room.IsReady);
        if (entered)
            Complete(attempt, new RoomEntryResult(attempt.RoomId, RoomEntryStatus.Success));
    }

    private void AccessStateChanged(RoomAccessTransition transition)
    {
        RoomEntryStatus? status = transition.CurrentState switch
        {
            RoomAccessState.Denied => RoomEntryStatus.Denied,
            RoomAccessState.NotFound => RoomEntryStatus.NotFound,
            _ => null
        };
        if (status is null)
            return;

        RoomEntryAttempt? attempt = ActiveAttempt();
        if (attempt is null || transition.CurrentRoomId != attempt.RoomId)
            return;

        Complete(attempt, new RoomEntryResult(attempt.RoomId, status.Value));
    }

    private void ConnectionFailed(CanNotConnect message)
    {
        RoomEntryAttempt? attempt = ActiveAttempt();
        if (attempt is null)
            return;

        var failure = new RoomConnectionFailure(
            message.Kind,
            message.ReasonCode,
            message.Parameter);
        Complete(
            attempt,
            new RoomEntryResult(
                attempt.RoomId,
                RoomEntryStatus.ConnectionError,
                failure));
    }

    private void RoomExited(RoomExitState exit)
    {
        if (exit.Source is RoomExitSource.AccessFailure or RoomExitSource.RoomTransition)
            return;

        RoomEntryAttempt? attempt = ActiveAttempt();
        if (attempt is null || exit.RoomId != attempt.RoomId)
            return;

        Complete(
            attempt,
            new RoomEntryResult(
                attempt.RoomId,
                RoomEntryStatus.ConnectionError,
                Exit: exit));
    }

    private void Disconnected()
    {
        RoomEntryAttempt? attempt = ActiveAttempt();
        if (attempt is null)
            return;
        Complete(
            attempt,
            new RoomEntryResult(
                attempt.RoomId,
                RoomEntryStatus.ConnectionError));
    }

    private RoomEntryAttempt? ActiveAttempt()
    {
        lock (_sync)
            return _active;
    }

    private void Complete(RoomEntryAttempt attempt, RoomEntryResult result)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_active, attempt))
                return;
            _active = null;
            attempt.Completion.TrySetResult(result);
        }
    }

    /// <summary>Stops tracking room events and fails the pending entry attempt with <see cref="ObjectDisposedException"/>.</summary>
    void IDisposable.Dispose() => Close();

    internal void Close()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _room.Ready -= RoomProgressed;
            _room.Entered -= RoomProgressed;
            _room.AccessStateChanged -= AccessStateChanged;
            _room.ConnectionFailed -= ConnectionFailed;
            _room.Exited -= RoomExited;
            if (_interceptor is not null)
            {
                _interceptor.Disconnected -= Disconnected;
                _interceptor = null;
            }
            _active?.Completion.TrySetException(new ObjectDisposedException(nameof(RoomEntryCoordinator)));
            _active = null;
        }
    }

    private sealed class RoomEntryAttempt(Id room_id)
    {
        public Id RoomId { get; } = room_id;
        public TaskCompletionSource<RoomEntryResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
