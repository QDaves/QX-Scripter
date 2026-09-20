namespace Qx.Presentation.Threading;

public sealed class CoalescingSignal
{
    readonly IUiDispatcher _dispatcher;
    readonly Action _apply;
    readonly UiPriority _priority;
    int _pending;

    public CoalescingSignal(IUiDispatcher dispatcher, Action apply, UiPriority priority = UiPriority.Background)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _priority = priority;
    }

    public void Raise()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 0)
            _dispatcher.Post(Drain, _priority);
    }

    void Drain()
    {
        Interlocked.Exchange(ref _pending, 0);
        _apply();
    }
}
