using CommunityToolkit.Mvvm.ComponentModel;

namespace Qx.Presentation.Mvvm;

public abstract class ViewModelBase : ObservableObject, IDisposable
{
    readonly List<IDisposable> _owned = [];
    bool _disposed;

    protected bool IsDisposed => _disposed;

    protected T Own<T>(T disposable) where T : IDisposable
    {
        ArgumentNullException.ThrowIfNull(disposable);
        if (_disposed)
        {
            disposable.Dispose();
            return disposable;
        }
        _owned.Add(disposable);
        return disposable;
    }

    protected void Own(Action release) => Own(new Release(release));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        for (int index = _owned.Count - 1; index >= 0; index--)
            _owned[index].Dispose();
        _owned.Clear();
        OnDisposed();
    }

    protected virtual void OnDisposed()
    {
    }

    sealed class Release(Action release) : IDisposable
    {
        Action? _release = release ?? throw new ArgumentNullException(nameof(release));

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
