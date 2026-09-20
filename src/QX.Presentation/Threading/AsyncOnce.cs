namespace Qx.Presentation.Threading;

public sealed class AsyncOnce<T>
{
    readonly Func<Task<T>> _factory;
    readonly Lock _gate = new();
    Task<T>? _task;

    public AsyncOnce(Func<Task<T>> factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public bool IsReady => Volatile.Read(ref _task) is { IsCompletedSuccessfully: true };

    public Task<T> GetAsync(CancellationToken cancellation_token = default)
    {
        Task<T> task;
        lock (_gate)
            task = _task ??= Task.Run(_factory);
        return task.WaitAsync(cancellation_token);
    }
}
