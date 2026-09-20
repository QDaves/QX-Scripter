using Avalonia.Threading;
using Qx.Diagnostics;
using Qx.Presentation.Threading;

namespace Qx.Desktop.Services;

internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action work, UiPriority priority = UiPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(work);
        Dispatcher.UIThread.Post(() => Run(work), Map(priority));
    }

    public void Post(Func<Task> work, UiPriority priority = UiPriority.Normal)
    {
        ArgumentNullException.ThrowIfNull(work);
        Dispatcher.UIThread.Post(() => StartAsync(work).Observe("ui"), Map(priority));
    }

    public Task InvokeAsync(Action work, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(work, DispatcherPriority.Normal, cancellation_token).GetTask();
        cancellation_token.ThrowIfCancellationRequested();
        work();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(work, DispatcherPriority.Normal, cancellation_token).GetTask();
        cancellation_token.ThrowIfCancellationRequested();
        return Task.FromResult(work());
    }

    public Task InvokeAsync(Func<Task> work, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(work, DispatcherPriority.Normal, cancellation_token).GetTask().Unwrap();
        cancellation_token.ThrowIfCancellationRequested();
        return work();
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> work, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(work, DispatcherPriority.Normal, cancellation_token).GetTask().Unwrap();
        cancellation_token.ThrowIfCancellationRequested();
        return work();
    }

    static DispatcherPriority Map(UiPriority priority) => priority switch
    {
        UiPriority.Background => DispatcherPriority.Background,
        UiPriority.Input => DispatcherPriority.Input,
        _ => DispatcherPriority.Normal
    };

    static void Run(Action work)
    {
        try
        {
            work();
        }
        catch (Exception error)
        {
            Diag.Error(error.ToString(), "ui");
        }
    }

    static Task StartAsync(Func<Task> work)
    {
        try
        {
            return work();
        }
        catch (Exception error)
        {
            return Task.FromException(error);
        }
    }
}
