using System.Diagnostics;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Waits until a condition holds.
    /// </summary>
    /// <remarks>
    /// The condition is checked at once, again after every change to the room state, and at
    /// least every <paramref name="pollMs"/> milliseconds for conditions that depend on time or
    /// on the script's own flags.
    /// </remarks>
    /// <param name="condition">
    /// The condition to wait for. After the first check it runs on a thread pool thread, so it
    /// should only read state. An exception it throws ends the wait and propagates to the caller.
    /// </param>
    /// <param name="timeoutMs">The timeout in milliseconds; -1 waits without a limit.</param>
    /// <param name="pollMs">The longest gap between two checks, in milliseconds. Must be at least 1.</param>
    /// <returns><see langword="true"/> once the condition holds, <see langword="false"/> when the time ran out.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeoutMs"/> is less than -1 or <paramref name="pollMs"/> is less than 1.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 10000, int pollMs = 50)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMs, Timeout.Infinite);
        ArgumentOutOfRangeException.ThrowIfLessThan(pollMs, 1);
        CancellationToken cancellation_token = Ct;
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            cancellation_token.ThrowIfCancellationRequested();
            Task change = Room.NextChange;
            if (condition())
                return true;
            int wait = pollMs;
            if (timeoutMs != Timeout.Infinite)
            {
                long remaining = timeoutMs - (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (remaining <= 0)
                    return false;
                wait = (int)Math.Min(wait, remaining);
            }
            await Task.WhenAny(change, Task.Delay(wait, cancellation_token)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until a room is ready, its floor items are loaded and the local user's avatar is in it.
    /// </summary>
    /// <remarks>
    /// The task completes at once when the current room already meets the condition.
    /// </remarks>
    /// <param name="roomId">The id of the room to wait for; 0 accepts whichever room is entered.</param>
    /// <param name="timeoutMs">The timeout in milliseconds; -1 waits without a limit.</param>
    /// <returns><see langword="true"/> once the room is ready, <see langword="false"/> when the time ran out.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public Task<bool> WaitRoomReady(Id roomId = default, int timeoutMs = 20000) =>
        WaitUntil(
            () => Room.Capture(room =>
                room.IsReady &&
                room.FloorItemsAreLoaded &&
                room.Self is not null &&
                (roomId == 0 || room.RoomId == roomId)),
            timeoutMs);

    /// <summary>
    /// Binds to the room the local user is in right now, so later work can tell whether it is
    /// still the same visit.
    /// </summary>
    /// <returns>A <see cref="RoomScope"/> for the current room session and the local user's avatar index.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no room is loaded or the own avatar is not in it.</exception>
    public RoomScope CaptureRoom() => RoomScope.Capture(Room);
}
