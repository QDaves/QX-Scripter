using Qx.Game;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Room access: the correlated entry helper and the events that report how far the local user got
/// trying to enter a room (connecting, ringing a doorbell, waiting in a queue, admitted, denied,
/// not found, or refused outright).
/// <para>
/// The current access state is a live view on <see cref="Room"/>, updated as the entry handshake
/// progresses: <see cref="RoomManager.AccessState"/>, <see cref="RoomManager.QueuePosition"/>,
/// <see cref="RoomManager.ConnectionFailure"/> and the members next to them. Reading it never
/// sends anything.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Requests entry into a room and waits for the handshake to reach a conclusion.
    /// </summary>
    /// <param name="roomId">The room to enter. Must be positive.</param>
    /// <param name="password">The door password; empty for rooms that need none.</param>
    /// <param name="timeoutMs">
    /// The timeout in milliseconds for the handshake to conclude. Doorbell and queue waits count
    /// against this budget, so a busy room usually needs more than the default.
    /// </param>
    /// <param name="cancellationToken">
    /// An extra token to abandon the wait with. The script's own stop token always applies as well.
    /// </param>
    /// <returns>
    /// The outcome (success, denied, not found, or connection error) with the room id, the failure
    /// detail for a refused connection, and the exit when the room was left before entry completed.
    /// A connection error is also reported when the hotel connection closes during the attempt.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="password"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="roomId"/> is not positive, or <paramref name="timeoutMs"/> is not positive.
    /// </exception>
    /// <exception cref="Qx.Game.RoomEntryTimeoutException">
    /// Thrown when the handshake did not conclude in time. This is a <see cref="TimeoutException"/>.
    /// </exception>
    /// <exception cref="Qx.Game.RoomEntryReplacedException">
    /// Thrown when another room entry was started before this one finished. Only one entry attempt is tracked
    /// at a time.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the script was stopped, or <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is the awaited form of <see cref="EnterRoom(Id, string)"/>. The entry request is sent on
    /// every call, also when the local user is already in that room; compare <see cref="RoomId"/>
    /// first when that is not wanted.
    /// </para>
    /// <para>
    /// Only failures the server reports as an access result are returned. A wrong password is not
    /// one of them: the server answers with a generic error and does not let the user in, so the
    /// call ends in a timeout.
    /// </para>
    /// </remarks>
    public async Task<RoomEntryResult> EnterRoomAsync(
        Id roomId,
        string password = "",
        int timeoutMs = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        CancellationToken script_token = Ct;
        using var operation_lifetime = new CancellationTokenSource();
        using IDisposable tracked_lifetime = Track(new Unsubscriber(operation_lifetime.Cancel));
        using CancellationTokenSource linked = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(
                script_token,
                cancellationToken,
                operation_lifetime.Token)
            : CancellationTokenSource.CreateLinkedTokenSource(
                script_token,
                operation_lifetime.Token);
        try
        {
            return await Game.RoomEntries
                .EnsureAsync(
                    roomId,
                    () => EnterRoom(roomId, password),
                    timeoutMs,
                    linked.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (script_token.IsCancellationRequested)
        {
            throw new OperationCanceledException(script_token);
        }
    }

    /// <summary>
    /// Registers a handler that runs on every room access state change.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the transition, which carries the old and new state and room id,
    /// plus the failure detail when the new state is a connection error.
    /// </param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomAccessChanged(Action<RoomAccessTransition> handler)
        => Subscribe(
            handler,
            value => Room.AccessStateChanged += value,
            value => Room.AccessStateChanged -= value);

    /// <summary>
    /// Registers a handler that runs each time the server sends a door queue update.
    /// </summary>
    /// <remarks>
    /// The server sends one whenever the local user's place in the door queue moves.
    /// </remarks>
    /// <param name="handler">The handler to call with the queue status, including every queue set.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomQueueUpdated(Action<RoomQueueStatus> handler)
        => Subscribe(
            handler,
            value => Room.QueueUpdated += value,
            value => Room.QueueUpdated -= value);

    /// <summary>
    /// Registers a handler that runs when the server refuses a room connection outright.
    /// </summary>
    /// <param name="handler">
    /// The handler to call with the refusal, which carries the raw reason code and, for a queue
    /// error, the queue name. Its kind maps 1 to full, 3 to queue error, 4 to banned and 5 to blocked.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomConnectionFailed(Action<CanNotConnect> handler)
        => Subscribe(
            handler,
            value => Room.ConnectionFailed += value,
            value => Room.ConnectionFailed -= value);

    /// <summary>
    /// Registers a handler that runs when a doorbell rings.
    /// </summary>
    /// <remarks>
    /// This covers both directions: an empty user name means the local user is the one waiting
    /// outside, a non-empty one names a visitor waiting at the door of the room the local user is in.
    /// </remarks>
    /// <param name="handler">The handler to call with the doorbell message.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnDoorbell(Action<Doorbell> handler)
        => Subscribe(
            handler,
            value => Room.DoorbellRang += value,
            value => Room.DoorbellRang -= value);

    /// <summary>
    /// Registers a handler that runs when the server grants access through a room door.
    /// </summary>
    /// <remarks>
    /// This covers both directions: an empty user name (<see cref="FlatAccessible.IsSelf"/>) means
    /// the local user was let in, a non-empty one names a visitor let into the room the local user
    /// is in.
    /// </remarks>
    /// <param name="handler">The handler to call with the room id and user name.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomAccessGranted(Action<FlatAccessible> handler)
    => Subscribe(
        handler,
        value => Room.AccessGranted += value,
        value => Room.AccessGranted -= value);

    /// <summary>
    /// Registers a handler that runs when the server denies access through a room door.
    /// </summary>
    /// <remarks>
    /// This covers both directions: an empty user name (<see cref="FlatAccessDenied.IsSelf"/>)
    /// means the local user was turned away, a non-empty one names a visitor who was turned away
    /// from the room the local user is in.
    /// </remarks>
    /// <param name="handler">The handler to call with the room id and user name.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnRoomAccessDenied(Action<FlatAccessDenied> handler)
    => Subscribe(
        handler,
        value => Room.AccessDenied += value,
        value => Room.AccessDenied -= value);
}
