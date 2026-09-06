namespace Qx.Game;

internal sealed class SharedLoadOperation<TResult>(TimeProvider time_provider, long epoch)
{
    private long _last_progress = time_provider.GetTimestamp();

    public TaskCompletionSource<TResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public long Epoch { get; } = epoch;
    public int Waiters { get; set; }
    public bool RequestSent { get; set; }
    public bool HasResponse { get; private set; }

    public bool IsExpired(TimeSpan lease) =>
        time_provider.GetElapsedTime(Volatile.Read(ref _last_progress)) >= lease;

    public void Touch()
    {
        HasResponse = true;
        Volatile.Write(ref _last_progress, time_provider.GetTimestamp());
    }
}
