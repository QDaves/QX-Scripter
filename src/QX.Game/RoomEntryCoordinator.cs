using Qx.Interception;
using Qx.Model.Messages.Incoming;

namespace Qx.Game;

public enum RoomEntryStatus
{
    Success,
    Denied,
    NotFound,
    ConnectionError
}

public sealed record RoomEntryResult(
    Id RoomId,
    RoomEntryStatus Status,
    RoomConnectionFailure? Failure = null,
    RoomExitState? Exit = null)
{
    public bool IsSuccess => Status is RoomEntryStatus.Success;
}

public sealed class RoomEntryTimeoutException(Id room_id, int timeout_ms)
    : TimeoutException($"Room entry for '{room_id}' timed out after {timeout_ms} ms.")
{
    public Id RoomId { get; } = room_id;
    public int TimeoutMs { get; } = timeout_ms;
}

public sealed class RoomEntryReplacedException(Id room_id, Id replacement_room_id)
    : InvalidOperationException(
        $"Room entry for '{room_id}' was replaced by a request for '{replacement_room_id}'.")
{
    public Id RoomId { get; } = room_id;
    public Id ReplacementRoomId { get; } = replacement_room_id;
}

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

    public Task<RoomEntryResult> EnsureAsync(
        Id room_id,
        Action send,
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual((long)room_id, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout_ms, 0);
        cancellation_token.ThrowIfCancellationRequested();

        var attempt = new RoomEntryAttempt(room_id);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellation_token.ThrowIfCancellationRequested();

            RoomEntryAttempt? previous = _active;
            _active = attempt;
            previous?.Completion.TrySetException(
                new RoomEntryReplacedException(previous.RoomId, room_id));

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

        return AwaitResult(attempt, timeout_ms, cancellation_token);
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

    public void Dispose()
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
