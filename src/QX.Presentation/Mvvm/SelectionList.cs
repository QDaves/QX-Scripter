using CommunityToolkit.Mvvm.ComponentModel;

namespace Qx.Presentation.Mvvm;

public sealed partial class SelectionList<T> : ObservableObject, ISelectionTarget where T : class
{
    readonly List<T> _items = [];

    public IReadOnlyList<T> Items => _items;

    public T? First => _items.Count > 0 ? _items[0] : null;

    public int Count => _items.Count;

    public bool HasAny => _items.Count > 0;

    public bool HasOne => _items.Count == 1;

    public event Action? Changed;

    public event Action<IReadOnlyList<object>>? SelectRequested;

    public void Replace(IEnumerable<object> selected)
    {
        T[] next = [.. selected.OfType<T>()];
        if (next.SequenceEqual(_items, ReferenceEqualityComparer.Instance))
            return;
        _items.Clear();
        _items.AddRange(next);
        OnPropertyChanged(nameof(First));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(HasOne));
        Changed?.Invoke();
    }

    public void Select(IEnumerable<T> items)
    {
        object[] selected = [.. items];
        Replace(selected);
        SelectRequested?.Invoke(selected);
    }

    public void Prune(IReadOnlyCollection<T> alive)
    {
        if (_items.All(alive.Contains))
            return;
        Replace(_items.Where(alive.Contains).ToArray());
    }
}
