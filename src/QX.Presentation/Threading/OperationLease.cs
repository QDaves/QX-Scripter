namespace Qx.Presentation.Threading;

public sealed class OperationLease
{
    readonly CancellationTokenSource _source;
    readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal OperationLease(CancellationTokenSource source)
    {
        _source = source;
        Token = source.Token;
    }

    public CancellationToken Token { get; }

    public Task Completion => _completion.Task;

    internal async Task StopAsync()
    {
        try
        {
            await _source.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        await Completion.WaitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    internal void Complete()
    {
        if (_completion.TrySetResult())
            _source.Dispose();
    }
}
