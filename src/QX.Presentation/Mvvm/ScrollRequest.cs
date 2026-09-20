namespace Qx.Presentation.Mvvm;

public sealed class ScrollRequest
{
    public event Action? ResetRequested;

    public event Action<object>? IntoViewRequested;

    public event Action? EndRequested;

    public void Reset() => ResetRequested?.Invoke();

    public void IntoView(object item)
    {
        ArgumentNullException.ThrowIfNull(item);
        IntoViewRequested?.Invoke(item);
    }

    public void End() => EndRequested?.Invoke();
}
