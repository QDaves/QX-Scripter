using System.Windows.Threading;
using Microsoft.VisualStudio.Threading;

namespace Qx.Ui;

internal sealed class UiTaskScope
{
    private static readonly object SharedSync = new();
    private static Dispatcher? _shared_dispatcher;
    private static JoinableTaskContext? _shared_context;
    private readonly Dispatcher _dispatcher;
    private readonly CancellationToken _lifetime_token;
    private readonly string _error_category;

    public UiTaskScope(
        Dispatcher dispatcher,
        string error_category,
        CancellationToken lifetime_token = default)
    {
        _dispatcher = dispatcher;
        _lifetime_token = lifetime_token;
        _error_category = error_category;
        Factory = SharedFactory(dispatcher);
    }

    public JoinableTaskFactory Factory { get; }

    public static JoinableTaskFactory ApplicationFactory => SharedFactory(
        System.Windows.Application.Current?.Dispatcher ??
            throw new InvalidOperationException("The UI task context requires an active WPF application."));

    public bool IsOnMainThread => Factory.Context.IsOnMainThread;

    public void Observe(Func<Task> task_factory)
    {
        Factory.RunAsync(async () =>
        {
            try
            {
                await task_factory();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Error(error.ToString(), _error_category);
            }
        }).Task.Forget();
    }

    public void OnUi(Action work) => Observe(() => SwitchAsync(work));

    public void OnUi(Func<Task> work) => Observe(() => SwitchAsync(work));

    public void Post(Action work, DispatcherPriority priority) =>
        Observe(() => PostAsync(work, priority));

    public async Task<T> SwitchAsync<T>(
        Func<T> work,
        CancellationToken cancellation_token = default)
    {
        using CancellationTokenSource? linked = Link(cancellation_token, out CancellationToken effective_token);
        await Factory.SwitchToMainThreadAsync(effective_token);
        return work();
    }

    public async Task SwitchAsync(
        Action work,
        CancellationToken cancellation_token = default)
    {
        using CancellationTokenSource? linked = Link(cancellation_token, out CancellationToken effective_token);
        await Factory.SwitchToMainThreadAsync(effective_token);
        work();
    }

    public async Task SwitchAsync(
        Func<Task> work,
        CancellationToken cancellation_token = default)
    {
        using CancellationTokenSource? linked = Link(cancellation_token, out CancellationToken effective_token);
        await Factory.SwitchToMainThreadAsync(effective_token);
        await work();
    }

    private async Task PostAsync(Action work, DispatcherPriority priority)
    {
        CancellationToken cancellation_token = _lifetime_token;
        cancellation_token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler shutdown = (_, _) => completion.TrySetCanceled();
        var context = new DispatcherSynchronizationContext(_dispatcher, priority);
        _dispatcher.ShutdownStarted += shutdown;
        try
        {
            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                completion.TrySetCanceled();
            }
            else
            {
                context.Post(_ =>
                {
                    if (cancellation_token.IsCancellationRequested ||
                        _dispatcher.HasShutdownStarted ||
                        _dispatcher.HasShutdownFinished)
                    {
                        completion.TrySetCanceled(cancellation_token);
                        return;
                    }

                    try
                    {
                        work();
                        completion.TrySetResult();
                    }
                    catch (Exception error)
                    {
                        completion.TrySetException(error);
                    }
                }, null);
            }
        }
        catch (Exception error)
        {
            completion.TrySetException(error);
        }
        try
        {
            await completion.Task.WaitAsync(cancellation_token);
        }
        finally
        {
            _dispatcher.ShutdownStarted -= shutdown;
        }
    }

    private CancellationTokenSource? Link(
        CancellationToken cancellation_token,
        out CancellationToken effective_token)
    {
        if (!_lifetime_token.CanBeCanceled)
        {
            effective_token = cancellation_token;
            return null;
        }

        if (!cancellation_token.CanBeCanceled || cancellation_token == _lifetime_token)
        {
            effective_token = _lifetime_token;
            return null;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime_token,
            cancellation_token);
        effective_token = linked.Token;
        return linked;
    }

    private static JoinableTaskFactory SharedFactory(Dispatcher dispatcher)
    {
        lock (SharedSync)
        {
            if (_shared_context is null)
            {
                if (!dispatcher.CheckAccess())
                    throw new InvalidOperationException("The UI task context must be created on its dispatcher thread.");
                _shared_dispatcher = dispatcher;
                _shared_context = new JoinableTaskContext(
                    dispatcher.Thread,
                    new DispatcherSynchronizationContext(dispatcher));
            }
            else if (!ReferenceEquals(_shared_dispatcher, dispatcher))
            {
                throw new InvalidOperationException("All UI task scopes must use the application dispatcher.");
            }

            return _shared_context.Factory;
        }
    }
}
