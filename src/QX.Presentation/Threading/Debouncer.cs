namespace Qx.Presentation.Threading;

public sealed class Debouncer : IDisposable
{
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly TimeSpan _delay;
    readonly Action _apply;
    readonly Lock _gate = new();
    ITimer? _timer;
    long _armed;
    bool _disposed;

    public Debouncer(IUiDispatcher dispatcher, TimeProvider time, TimeSpan delay, Action apply)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        _delay = delay;
    }

    public void Trigger()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _armed++;
            _timer ??= _time.CreateTimer(static state => ((Debouncer)state!).Elapse(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _armed++;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _armed++;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        _apply();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }

    void Elapse()
    {
        long armed;
        lock (_gate)
        {
            if (_disposed)
                return;
            armed = _armed;
        }
        _dispatcher.Post(() => Apply(armed), UiPriority.Background);
    }

    void Apply(long armed)
    {
        lock (_gate)
        {
            if (_disposed || armed != _armed)
                return;
        }
        _apply();
    }
}
