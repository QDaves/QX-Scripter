namespace Qx.Presentation.Mvvm;

public interface ISelectionTarget
{
    void Replace(IEnumerable<object> selected);

    event Action<IReadOnlyList<object>>? SelectRequested;
}
