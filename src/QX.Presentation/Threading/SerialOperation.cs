namespace Qx.Presentation.Threading;

public sealed class SerialOperation
{
    readonly Lock _gate = new();
    OperationLease? _current;

    public async Task<OperationLease> StartAsync(CancellationToken lifetime)
    {
        var next = new OperationLease(CancellationTokenSource.CreateLinkedTokenSource(lifetime));
        OperationLease? previous;
        lock (_gate)
        {
            previous = _current;
            _current = next;
        }
        if (previous is not null)
            await previous.StopAsync().ConfigureAwait(false);
        return next;
    }

    public bool IsCurrent(OperationLease lease)
    {
        lock (_gate)
            return ReferenceEquals(_current, lease);
    }

    public void Complete(OperationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        lock (_gate)
        {
            if (ReferenceEquals(_current, lease))
                _current = null;
        }
        lease.Complete();
    }

    public Task StopAsync()
    {
        OperationLease? current;
        lock (_gate)
        {
            current = _current;
            _current = null;
        }
        return current?.StopAsync() ?? Task.CompletedTask;
    }
}
