namespace Qx.Presentation.Threading;

public sealed class AppLifetime : IDisposable
{
    readonly CancellationTokenSource _source = new();

    public CancellationToken Token => _source.Token;

    public bool IsStopping => _source.IsCancellationRequested;

    public void Cancel() => _source.Cancel();

    public void Dispose() => _source.Dispose();
}
