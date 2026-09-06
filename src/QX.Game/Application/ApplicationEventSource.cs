namespace Qx.Game.Application;

internal sealed class ApplicationEventSource<T>(Action<Exception>? observer_error = null) : IDisposable
{
    private readonly object sync = new();
    private Action<T>? listeners;
    private bool disposed;

    public IDisposable Subscribe(Action<T> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            listeners += listener;
        }
        return new Subscription(this, listener);
    }

    public void Publish(T value)
    {
        Action<T>? snapshot;
        lock (sync)
        {
            if (disposed)
                return;
            snapshot = listeners;
        }
        if (snapshot is null)
            return;
        foreach (Action<T> listener in snapshot.GetInvocationList().Cast<Action<T>>())
        {
            try
            {
                listener(value);
            }
            catch (Exception error)
            {
                observer_error?.Invoke(error);
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            disposed = true;
            listeners = null;
        }
    }

    private void Unsubscribe(Action<T> listener)
    {
        lock (sync)
            listeners -= listener;
    }

    private sealed class Subscription(
        ApplicationEventSource<T> source,
        Action<T> listener) : IDisposable
    {
        private ApplicationEventSource<T>? current_source = source;
        private Action<T>? current_listener = listener;

        public void Dispose()
        {
            ApplicationEventSource<T>? source_value = Interlocked.Exchange(ref current_source, null);
            Action<T>? listener_value = Interlocked.Exchange(ref current_listener, null);
            if (source_value is not null && listener_value is not null)
                source_value.Unsubscribe(listener_value);
        }
    }
}
