namespace Qx.Scripting;

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

    public static void ThrowIfCancellationRequested() =>
        Current.Value.ThrowIfCancellationRequested();

    public static Task Delay(int millisecondsDelay) =>
        Task.Delay(millisecondsDelay, Current.Value);

    public static Task Delay(int millisecondsDelay, CancellationToken cancellationToken) =>
        Delay(millisecondsDelay, cancellationToken, Current.Value);

    public static Task Delay(TimeSpan delay) =>
        Task.Delay(delay, Current.Value);

    public static Task Delay(TimeSpan delay, CancellationToken cancellationToken) =>
        Delay(delay, cancellationToken, Current.Value);

    public static Task Delay(TimeSpan delay, TimeProvider timeProvider) =>
        Task.Delay(delay, timeProvider, Current.Value);

    public static Task Delay(
        TimeSpan delay,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        Delay(delay, timeProvider, cancellationToken, Current.Value);

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
