namespace Qx.Presentation.Mvvm;

public interface IVisibleItemsSink
{
    void VisibleChanged(IReadOnlyList<object> visible);
}
