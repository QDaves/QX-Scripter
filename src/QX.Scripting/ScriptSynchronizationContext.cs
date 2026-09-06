namespace Qx.Scripting;

internal sealed class ScriptSynchronizationContext(
    SynchronizationContext? inner,
    CancellationToken cancellation_token,
    Action<Exception> report_error,
    Action complete) : SynchronizationContext
{
    private int _dispatching = 1;
    private int _operations;
    private int _posts;
    private int _completed;

    public override void OperationStarted() => Interlocked.Increment(ref _operations);

    public override void OperationCompleted()
    {
        if (Interlocked.Decrement(ref _operations) < 0)
            throw new InvalidOperationException("The script synchronization operation count is invalid.");
        TryComplete();
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Interlocked.Increment(ref _posts);

        void Invoke(object? value)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            using IDisposable scope = ScriptExecutionContext.Enter(cancellation_token);
            try
            {
                callback(value);
            }
            catch (Exception error)
            {
                Report(error);
            }
            finally
            {
                SetSynchronizationContext(previous);
                Interlocked.Decrement(ref _posts);
                TryComplete();
            }
        }

        try
        {
            if (inner is null)
            {
                if (!ThreadPool.QueueUserWorkItem(Invoke, state, false))
                    throw new InvalidOperationException("The script continuation could not be queued.");
            }
            else
                inner.Post(Invoke, state);
        }
        catch (Exception error)
        {
            Report(error);
            Invoke(state);
        }
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        ArgumentNullException.ThrowIfNull(callback);

        void Invoke(object? value)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            using IDisposable scope = ScriptExecutionContext.Enter(cancellation_token);
            try
            {
                callback(value);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }

        if (inner is null)
            Invoke(state);
        else
            inner.Send(Invoke, state);
    }

    public override SynchronizationContext CreateCopy() => this;

    public void CompleteDispatch()
    {
        Volatile.Write(ref _dispatching, 0);
        TryComplete();
    }

    private void TryComplete()
    {
        if (Volatile.Read(ref _dispatching) != 0 ||
            Volatile.Read(ref _operations) != 0 ||
            Volatile.Read(ref _posts) != 0 ||
            Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }
        complete();
    }

    private void Report(Exception error)
    {
        try
        {
            report_error(error);
        }
        catch
        {
        }
    }
}
