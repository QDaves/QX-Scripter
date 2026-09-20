namespace Qx.Presentation.Threading;

public sealed class ThrottledSignal : IDisposable
{
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly TimeSpan _interval;
    readonly Action _apply;
    readonly Lock _gate = new();
    ITimer? _timer;
    long _last_applied;
    bool _scheduled;
    bool _disposed;

    public ThrottledSignal(IUiDispatcher dispatcher, TimeProvider time, TimeSpan interval, Action apply)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        _interval = interval;
    }

    public void Raise()
    {
        lock (_gate)
        {
            if (_disposed || _scheduled)
                return;
            _scheduled = true;
            TimeSpan since = _last_applied == 0 ? _interval : _time.GetElapsedTime(_last_applied);
            if (since >= _interval)
            {
                _dispatcher.Post(Apply, UiPriority.Background);
                return;
            }
            _timer ??= _time.CreateTimer(static state => ((ThrottledSignal)state!).Elapse(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_interval - since, Timeout.InfiniteTimeSpan);
        }
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

    void Elapse() => _dispatcher.Post(Apply, UiPriority.Background);

    void Apply()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _scheduled = false;
            _last_applied = _time.GetTimestamp();
        }
        _apply();
    }
}
