using System.Collections.ObjectModel;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Notifications;

public sealed class ToastQueue<TToast> : IDisposable where TToast : class
{
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly int _capacity;
    readonly TimeSpan _lifetime;
    readonly ObservableCollection<TToast> _toasts = [];
    readonly Dictionary<TToast, ITimer> _timers = new(ReferenceEqualityComparer.Instance);

    public ToastQueue(IUiDispatcher dispatcher, TimeProvider time, int capacity, TimeSpan lifetime)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        _capacity = capacity;
        _lifetime = lifetime;
        Toasts = new ReadOnlyObservableCollection<TToast>(_toasts);
    }

    public ReadOnlyObservableCollection<TToast> Toasts { get; }

    public void Show(TToast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        _toasts.Add(toast);
        while (_toasts.Count > _capacity)
            Remove(_toasts[0]);
        _timers[toast] = _time.CreateTimer(Expire, toast, _lifetime, Timeout.InfiniteTimeSpan);
    }

    public void Remove(TToast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        if (_timers.Remove(toast, out ITimer? timer))
            timer.Dispose();
        _toasts.Remove(toast);
    }

    public void Clear()
    {
        foreach (TToast toast in _toasts.ToArray())
            Remove(toast);
    }

    public void Dispose() => Clear();

    void Expire(object? state)
    {
        if (state is TToast toast)
            _dispatcher.Post(() => Remove(toast));
    }
}
