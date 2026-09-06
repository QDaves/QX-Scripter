using Qx.Game;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Room access: how far the local user got trying to enter a room — connecting, ringing a
/// doorbell, waiting in a queue, admitted, denied, not found, or refused outright — plus the
/// correlated entry helper and the access events.
/// <para>
/// All of the state below is a live view of the room tracker, updated as the entry handshake
/// progresses. Reading it never sends anything.
/// </para>
/// <para>
/// The doorbell and access grant/deny messages are decoded on Flash only; the queue, the
/// connection-failure message and the access state machine itself work on both clients.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Where the local user stands in the room entry handshake: <c>Idle</c>, <c>Connecting</c>,
    /// <c>RingingDoorbell</c>, <c>Queued</c>, <c>Accessible</c>, or one of the terminal failures
    /// <c>Denied</c>, <c>NotFound</c> and <c>ConnectionError</c>.
    /// </summary>
    public RoomAccessState RoomAccessState => Room.AccessState;

    /// <summary>
    /// The room the current access state refers to, or <see langword="null"/> when the state is
    /// idle or the server never named a room. This is not necessarily the room the user is in — it
    /// is the room being entered.
    /// </summary>
    public Id? RoomAccessRoomId => Room.AccessRoomId;

    /// <summary>
    /// Whether the local user is waiting at a locked door for someone inside to let them in.
    /// </summary>
    public bool IsRingingDoorbell => Room.IsRingingDoorbell;

    /// <summary>Whether the local user is currently sitting in a room's door queue.</summary>
    public bool IsInRoomQueue => Room.IsInQueue;

    /// <summary>
    /// The local user's place in the queue they are waiting in, or <see langword="null"/> when
    /// they are not queued. The value comes from the first queue set the server reports.
    /// </summary>
    public int? RoomQueuePosition => Room.QueuePosition;

    /// <summary>
    /// The full queue status as last reported: the room and every queue set with its target
    /// (spectator or visitor) and position. <see langword="null"/> until a queue message arrives,
    /// and cleared as soon as the access state leaves the queued state.
    /// </summary>
    public RoomQueueStatus? CurrentRoomQueue => Room.QueueStatus;

    /// <summary>
    /// Why the last room entry attempt was refused outright — room full, queue error, banned or
    /// blocked — together with the raw reason code. <see langword="null"/> when the current attempt
    /// did not fail that way.
    /// </summary>
    public RoomConnectionFailure? LastRoomConnectionFailure => Room.ConnectionFailure;

    /// <summary>
    /// Requests entry into a room and waits for the handshake to reach a conclusion, instead of
    /// firing the entry message and hoping.
    /// </summary>
    /// <param name="room_id">The room to enter. Must be positive.</param>
    /// <param name="password">The door password; empty for rooms that need none.</param>
    /// <param name="timeout_ms">
    /// How long to wait for the handshake to conclude, in milliseconds. Doorbell and queue waits
    /// count against this budget, so a busy room usually needs more than the default.
    /// </param>
    /// <param name="cancellation_token">
    /// An extra token to abandon the wait with. The script's own stop token always applies as well.
    /// </param>
    /// <returns>
    /// The outcome: success, denied, not found, or connection error, with the room id and, for a
    /// connection error, the failure detail.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="password"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="room_id"/> is not positive, or <paramref name="timeout_ms"/> is not positive.
    /// </exception>
    /// <exception cref="Qx.Game.RoomEntryTimeoutException">
    /// The handshake did not conclude in time. This is a <see cref="TimeoutException"/>.
    /// </exception>
    /// <exception cref="Qx.Game.RoomEntryReplacedException">
    /// Another room entry was started before this one finished. Only one entry attempt is tracked
    /// at a time.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The script was stopped, or <paramref name="cancellation_token"/> was cancelled.
    /// </exception>
    /// <remarks>
    /// Only failures the server reports are returned as a result. A wrong password is not one of
    /// them: the server simply does not let the user in, so the call ends in a timeout.
    /// </remarks>
    public async Task<RoomEntryResult> EnsureEnterRoom(
        Id room_id,
        string password = "",
        int timeout_ms = 10000,
        CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        CancellationToken script_token = Ct;
        using var operation_lifetime = new CancellationTokenSource();
        using IDisposable tracked_lifetime = Track(new Unsubscriber(operation_lifetime.Cancel));
        using CancellationTokenSource linked = cancellation_token.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(
                script_token,
                cancellation_token,
                operation_lifetime.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(
                script_token,
                operation_lifetime.Token);
        try
        {
            return await Game.RoomEntries
                .EnsureAsync(
                    room_id,
                    () => EnterRoom(room_id, password),
                    timeout_ms,
                    linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellation_token);
        }
        catch (OperationCanceledException) when (script_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(script_token);
        }
    }

    /// <summary>
    /// Raised on every room access state change, carrying both the old and the new state and room
    /// id, plus the failure detail when the new state is a connection error.
    /// </summary>
    /// <param name="handler">Receives the transition.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomAccessChanged(Action<RoomAccessTransition> handler)
        => Subscribe(
            handler,
            value => Room.AccessStateChanged += value,
            value => Room.AccessStateChanged -= value);

    /// <summary>
    /// Raised each time the server sends a queue update, which it does whenever the local user's
    /// place in the door queue moves.
    /// </summary>
    /// <param name="handler">Receives the queue status, including every queue set.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomQueueUpdated(Action<RoomQueueStatus> handler)
        => Subscribe(
            handler,
            value => Room.QueueUpdated += value,
            value => Room.QueueUpdated -= value);

    /// <summary>
    /// Raised when the server refuses a room connection outright, carrying the raw reason code and,
    /// for a queue error, the queue name.
    /// </summary>
    /// <param name="handler">Receives the refusal. Its kind maps 1 to full, 3 to queue error, 4 to banned and 5 to blocked.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnRoomConnectionFailed(Action<CanNotConnect> handler)
        => Subscribe(
            handler,
            value => Room.ConnectionFailed += value,
            value => Room.ConnectionFailed -= value);

    /// <summary>
    /// Raised when a doorbell rings. This covers both directions: an empty user name means the
    /// local user is the one waiting outside, a non-empty one names a visitor waiting at the door
    /// of the room the local user is in.
    /// </summary>
    /// <param name="handler">Receives the doorbell message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnDoorbell(Action<Doorbell> handler)
        => Subscribe(
            handler,
            value => Room.DoorbellRang += value,
            value => Room.DoorbellRang -= value);

    /// <summary>
    /// Raised when someone at a doorbell is let in. An empty user name means it was the local
    /// user; a name means another visitor was admitted to the room the local user is in.
    /// </summary>
    /// <param name="handler">Receives the grant.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>Flash only; the message has no Unity layout.</remarks>
    public IDisposable OnRoomAccessGranted(Action<FlatAccessible> handler)
        => Subscribe(
            handler,
            value => Room.AccessGranted += value,
            value => Room.AccessGranted -= value);

    /// <summary>
    /// Raised when someone at a doorbell is turned away. An empty or absent user name means it was
    /// the local user, which also drives the access state to denied.
    /// </summary>
    /// <param name="handler">Receives the denial.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>Flash only; the message has no Unity layout.</remarks>
    public IDisposable OnRoomAccessDenied(Action<FlatAccessDenied> handler)
        => Subscribe(
            handler,
            value => Room.AccessDenied += value,
            value => Room.AccessDenied -= value);
}
