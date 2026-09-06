namespace Qx.Scripting;

public partial class ScriptGlobals
{
    private Action Guarded(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        CancellationToken cancellation_token = Ct;
        return () => RunGuarded(handler, cancellation_token);
    }

    private Action<T> Guarded<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        CancellationToken cancellation_token = Ct;
        return value => RunGuarded(() => handler(value), cancellation_token);
    }

    private Action<T1, T2> Guarded<T1, T2>(Action<T1, T2> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        CancellationToken cancellation_token = Ct;
        return (first, second) => RunGuarded(() => handler(first, second), cancellation_token);
    }

    private Action<T1, T2, T3> Guarded<T1, T2, T3>(Action<T1, T2, T3> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        CancellationToken cancellation_token = Ct;
        return (first, second, third) =>
            RunGuarded(() => handler(first, second, third), cancellation_token);
    }

    private Action<T1, T2, T3, T4, T5> Guarded<T1, T2, T3, T4, T5>(
        Action<T1, T2, T3, T4, T5> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        CancellationToken cancellation_token = Ct;
        return (first, second, third, fourth, fifth) =>
            RunGuarded(() => handler(first, second, third, fourth, fifth), cancellation_token);
    }

    private IDisposable Subscribe(Action handler, Action<Action> add, Action<Action> remove)
    {
        Action guarded = Guarded(handler);
        add(guarded);
        return Track(new Unsubscriber(() => remove(guarded)));
    }

    private IDisposable Subscribe<T>(
        Action<T> handler,
        Action<Action<T>> add,
        Action<Action<T>> remove)
    {
        Action<T> guarded = Guarded(handler);
        add(guarded);
        return Track(new Unsubscriber(() => remove(guarded)));
    }

    private IDisposable Subscribe<T1, T2>(
        Action<T1, T2> handler,
        Action<Action<T1, T2>> add,
        Action<Action<T1, T2>> remove)
    {
        Action<T1, T2> guarded = Guarded(handler);
        add(guarded);
        return Track(new Unsubscriber(() => remove(guarded)));
    }

    private IDisposable Subscribe<T1, T2, T3>(
        Action<T1, T2, T3> handler,
        Action<Action<T1, T2, T3>> add,
        Action<Action<T1, T2, T3>> remove)
    {
        Action<T1, T2, T3> guarded = Guarded(handler);
        add(guarded);
        return Track(new Unsubscriber(() => remove(guarded)));
    }

    private IDisposable Subscribe<T1, T2, T3, T4, T5>(
        Action<T1, T2, T3, T4, T5> handler,
        Action<Action<T1, T2, T3, T4, T5>> add,
        Action<Action<T1, T2, T3, T4, T5>> remove)
    {
        Action<T1, T2, T3, T4, T5> guarded = Guarded(handler);
        add(guarded);
        return Track(new Unsubscriber(() => remove(guarded)));
    }

    private void RunGuarded(Action handler, CancellationToken cancellation_token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!TryTrackBackgroundTask(completion.Task, cancellation_token))
            return;

        var context = new ScriptSynchronizationContext(
            SynchronizationContext.Current,
            cancellation_token,
            error => FinishGuarded(error, cancellation_token),
            () => completion.TrySetResult());
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        using IDisposable scope = ScriptExecutionContext.Enter(cancellation_token);
        try
        {
            cancellation_token.ThrowIfCancellationRequested();
            handler();
        }
        catch (Exception error)
        {
            FinishGuarded(error, cancellation_token);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
            context.CompleteDispatch();
        }
    }

    private void FinishGuarded(Exception error, CancellationToken cancellation_token)
    {
        if (error is ScriptFinishedException)
        {
            if (!cancellation_token.IsCancellationRequested)
                ReportBackgroundFinished();
            return;
        }
        if (error is OperationCanceledException && cancellation_token.IsCancellationRequested)
            return;
        ReportBackgroundError(error);
    }
}
