namespace Qx.Presentation.Mvvm;

public sealed class SortRefreshRequest
{
    public event Action? Requested;

    public void Request() => Requested?.Invoke();
}
