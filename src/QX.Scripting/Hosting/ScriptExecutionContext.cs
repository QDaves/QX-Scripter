namespace Qx.Scripting.Hosting;

/// <summary>
/// Provides the cancellation checks and waits that the script compiler inserts into script code.
/// </summary>
/// <remarks>
/// <see cref="ScriptEngine.Prepare(string, string, string?, Qx.Protocol.IMessageResolver?)"/> starts every loop body with a call
/// to <see cref="ThrowIfCancellationRequested"/> and redirects calls to every overload of
/// <c>Task.Delay</c> and <c>Thread.Sleep</c> to the matching members of this class, so they
/// observe the token of the current script run. Outside a run there is no such token and the members behave like their
/// <c>System</c> counterparts, including argument validation. Scripts do not need to call them
/// directly.
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class ScriptExecutionContext
{
    private static readonly AsyncLocal<CancellationToken> Current = new();

    internal static CancellationToken CancellationToken => Current.Value;

    internal static IDisposable Enter(CancellationToken cancellationToken)
    {
        CancellationToken previous = Current.Value;
        Current.Value = cancellationToken;
        return new Scope(previous);
    }

    /// <summary>
    /// Throws an <see cref="OperationCanceledException"/> when the current script run has been
    /// asked to stop.
    /// </summary>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped.</exception>
    public static void ThrowIfCancellationRequested() =>
        Current.Value.ThrowIfCancellationRequested();

    /// <summary>
    /// Waits for the specified number of milliseconds, ending early when the current script run
    /// is stopped.
    /// </summary>
    /// <param name="millisecondsDelay">
    /// The time to wait in milliseconds, or <see cref="Timeout.Infinite"/> to wait until the run
    /// is stopped.
    /// </param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped.</exception>
    public static Task Delay(int millisecondsDelay) =>
        Task.Delay(millisecondsDelay, Current.Value);

    /// <summary>
    /// Waits for the specified number of milliseconds, ending early when the token is canceled
    /// or the current script run is stopped.
    /// </summary>
    /// <param name="millisecondsDelay">
    /// The time to wait in milliseconds, or <see cref="Timeout.Infinite"/> to wait until
    /// cancellation.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token was canceled or the current script run was stopped.</exception>
    public static Task Delay(int millisecondsDelay, CancellationToken cancellationToken) =>
        Delay(millisecondsDelay, cancellationToken, Current.Value);

    /// <summary>
    /// Waits for the specified time, ending early when the current script run is stopped.
    /// </summary>
    /// <param name="delay">The time to wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped.</exception>
    public static Task Delay(TimeSpan delay) =>
        Task.Delay(delay, Current.Value);

    /// <summary>
    /// Waits for the specified time, ending early when the token is canceled or the current
    /// script run is stopped.
    /// </summary>
    /// <param name="delay">The time to wait.</param>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token was canceled or the current script run was stopped.</exception>
    public static Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        Delay(delay, cancellationToken, Current.Value);

    /// <summary>
    /// Waits for the specified time as measured by a time provider, ending early when the current
    /// script run is stopped.
    /// </summary>
    /// <param name="delay">The time to wait.</param>
    /// <param name="timeProvider">The time provider that measures the delay.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped.</exception>
    public static Task Delay(TimeSpan delay, TimeProvider timeProvider) =>
        Task.Delay(delay, timeProvider, Current.Value);

    /// <summary>
    /// Waits for the specified time as measured by a time provider, ending early when the token
    /// is canceled or the current script run is stopped.
    /// </summary>
    /// <param name="delay">The time to wait.</param>
    /// <param name="timeProvider">The time provider that measures the delay.</param>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>A task that completes after the delay.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token was canceled or the current script run was stopped.</exception>
    public static Task Delay(
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        Delay(delay, timeProvider, cancellationToken, Current.Value);

    /// <summary>
    /// Blocks the current thread for the specified number of milliseconds, waking early when the
    /// current script run is stopped.
    /// </summary>
    /// <remarks>Outside a script run it calls <see cref="Thread.Sleep(int)"/>.</remarks>
    /// <param name="millisecondsTimeout">
    /// The time to block in milliseconds, or <see cref="Timeout.Infinite"/> to block until the run
    /// is stopped.
    /// </param>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped before or during the wait.</exception>
    public static void Sleep(int millisecondsTimeout)
    {
        CancellationToken cancellationToken = Current.Value;
        if (!cancellationToken.CanBeCanceled)
        {
            Thread.Sleep(millisecondsTimeout);
            return;
        }
        if (cancellationToken.WaitHandle.WaitOne(millisecondsTimeout))
            cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Blocks the current thread for the specified time, waking early when the current script
    /// run is stopped.
    /// </summary>
    /// <remarks>Outside a script run it calls <see cref="Thread.Sleep(TimeSpan)"/>.</remarks>
    /// <param name="timeout">The time to block.</param>
    /// <exception cref="OperationCanceledException">Thrown when the current script run was stopped before or during the wait.</exception>
    public static void Sleep(TimeSpan timeout)
    {
        CancellationToken cancellationToken = Current.Value;
        if (!cancellationToken.CanBeCanceled)
        {
            Thread.Sleep(timeout);
            return;
        }
        if (cancellationToken.WaitHandle.WaitOne(timeout))
            cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task Delay(
        int millisecondsDelay,
        CancellationToken cancellationToken,
        CancellationToken scriptCancellation)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            scriptCancellation);
        await Task.Delay(millisecondsDelay, linked.Token).ConfigureAwait(false);
    }

    private static async Task Delay(
        TimeSpan delay,
        CancellationToken cancellationToken,
        CancellationToken scriptCancellation)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            scriptCancellation);
        await Task.Delay(delay, linked.Token).ConfigureAwait(false);
    }

    private static async Task Delay(
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        CancellationToken scriptCancellation)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            scriptCancellation);
        await Task.Delay(delay, timeProvider, linked.Token).ConfigureAwait(false);
    }

    private sealed class Scope(CancellationToken previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Current.Value = previous;
        }
    }
}
