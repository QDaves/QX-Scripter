using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Generators;
using Avalonia.Input;
using Avalonia.Layout;
using Qx.Presentation.Mvvm;

namespace Qx.Desktop.Controls;

public sealed class UniformTilePanel : VirtualizingPanel
{
    public const int PoolCapacity = 64;

    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<UniformTilePanel, double>(nameof(ItemWidth), 120);

    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<UniformTilePanel, double>(nameof(ItemHeight), 80);

    public static readonly StyledProperty<double> HeaderHeightProperty =
        AvaloniaProperty.Register<UniformTilePanel, double>(nameof(HeaderHeight), 48);

    public static readonly StyledProperty<int> BufferRowsProperty =
        AvaloniaProperty.Register<UniformTilePanel, int>(nameof(BufferRows), 2);

    readonly Dictionary<int, Control> _realized = [];
    readonly Dictionary<Control, object?> _recycle_keys = new(ReferenceEqualityComparer.Instance);
    readonly Dictionary<object, Stack<Control>> _pool = [];
    readonly List<Rect> _slots = [];
    Rect _viewport;
    double _extent_height;
    int _columns = 1;

    static UniformTilePanel()
    {
        AffectsMeasure<UniformTilePanel>(ItemWidthProperty, ItemHeightProperty, HeaderHeightProperty, BufferRowsProperty);
    }

    public UniformTilePanel()
    {
        EffectiveViewportChanged += OnViewportChanged;
    }

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double HeaderHeight
    {
        get => GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public int BufferRows
    {
        get => GetValue(BufferRowsProperty);
        set => SetValue(BufferRowsProperty, value);
    }

    public int Columns => _columns;

    public int RealizedCount => _realized.Count;

    public int PooledCount => _pool.Values.Sum(pool => pool.Count);

    protected override Size MeasureOverride(Size available_size)
    {
        IReadOnlyList<object?> items = Items;
        double width = double.IsFinite(available_size.Width) ? available_size.Width : Math.Max(_viewport.Width, ItemWidth);
        _columns = Math.Max(1, (int)Math.Floor(width / ItemWidth));
        LayoutSlots(items, width);
        (int first, int last) = VisibleRange();
        foreach (int index in _realized.Keys.Where(index => index < first || index > last).ToArray())
            Unrealize(index);
        for (int index = first; index <= last; index++)
            Realize(index, items);
        foreach ((int index, Control container) in _realized)
            container.Measure(_slots[index].Size);
        return new Size(width, _extent_height);
    }

    protected override Size ArrangeOverride(Size final_size)
    {
        foreach ((int index, Control container) in _realized)
            container.Arrange(_slots[index]);
        return final_size;
    }

    protected override Control? ScrollIntoView(int index)
    {
        IReadOnlyList<object?> items = Items;
        if (index < 0 || index >= items.Count)
            return null;
        if (_slots.Count != items.Count)
            LayoutSlots(items, Math.Max(_viewport.Width, ItemWidth));
        Control container = Realize(index, items);
        container.Measure(_slots[index].Size);
        container.Arrange(_slots[index]);
        container.BringIntoView();
        return container;
    }

    protected override Control? ContainerFromIndex(int index) =>
        _realized.GetValueOrDefault(index);

    protected override int IndexFromContainer(Control container)
    {
        foreach ((int index, Control realized) in _realized)
        {
            if (ReferenceEquals(realized, container))
                return index;
        }
        return -1;
    }

    protected override IEnumerable<Control>? GetRealizedContainers() => _realized.Values.ToArray();

    protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        int count = Items.Count;
        if (count == 0)
            return null;
        int current = from is Control control ? IndexFromContainer(control) : -1;
        if (current < 0 || _slots.Count != count)
            return ScrollIntoView(0);
        Rect slot = _slots[current];
        int target = direction switch
        {
            NavigationDirection.Left => current - 1,
            NavigationDirection.Right => current + 1,
            NavigationDirection.Up => Nearest(current, slot.Top - 1, slot.Center.X, above: true),
            NavigationDirection.Down => Nearest(current, slot.Bottom + 1, slot.Center.X, above: false),
            NavigationDirection.First => 0,
            NavigationDirection.Last => count - 1,
            NavigationDirection.PageUp => Nearest(current, slot.Top - Math.Max(ItemHeight, _viewport.Height), slot.Center.X, above: true),
            NavigationDirection.PageDown => Nearest(current, slot.Bottom + Math.Max(ItemHeight, _viewport.Height), slot.Center.X, above: false),
            _ => current
        };
        return ScrollIntoView(Math.Clamp(target, 0, count - 1));
    }

    protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(items, e);
        int last_realized = _realized.Count == 0 ? -1 : _realized.Keys.Max();
        bool beyond_realized = e.Action switch
        {
            NotifyCollectionChangedAction.Add => e.NewStartingIndex > last_realized,
            NotifyCollectionChangedAction.Remove => e.OldStartingIndex > last_realized,
            _ => false
        };
        if (!beyond_realized)
        {
            foreach (int index in _realized.Keys.ToArray())
                Unrealize(index);
        }
        InvalidateMeasure();
    }

    void LayoutSlots(IReadOnlyList<object?> items, double width)
    {
        _slots.Clear();
        double y = 0;
        int column = 0;
        foreach (object? item in items)
        {
            if (item is ISectionHeader)
            {
                if (column > 0)
                {
                    y += ItemHeight;
                    column = 0;
                }
                _slots.Add(new Rect(0, y, width, HeaderHeight));
                y += HeaderHeight;
                continue;
            }
            _slots.Add(new Rect(column * ItemWidth, y, ItemWidth, ItemHeight));
            if (++column == _columns)
            {
                column = 0;
                y += ItemHeight;
            }
        }
        if (column > 0)
            y += ItemHeight;
        _extent_height = y;
    }

    (int First, int Last) VisibleRange()
    {
        if (_slots.Count == 0)
            return (0, -1);
        double buffer = BufferRows * ItemHeight;
        double top = Math.Max(0, _viewport.Top) - buffer;
        double bottom = (_viewport.Height > 0 ? _viewport.Bottom : Math.Max(0, _viewport.Top) + ItemHeight * 8) + buffer;
        int first = 0;
        while (first < _slots.Count - 1 && _slots[first].Bottom <= top)
            first++;
        int last = first;
        while (last < _slots.Count - 1 && _slots[last + 1].Top < bottom)
            last++;
        return (first, last);
    }

    int Nearest(int current, double y, double x, bool above)
    {
        int best = current;
        double best_distance = double.MaxValue;
        for (int index = 0; index < _slots.Count; index++)
        {
            Rect slot = _slots[index];
            if (above ? slot.Bottom > y + ItemHeight || slot.Top >= _slots[current].Top : slot.Top < y - ItemHeight || slot.Top <= _slots[current].Top)
                continue;
            double distance = Math.Abs(slot.Top - y) * 4 + Math.Abs(slot.Center.X - x);
            if (distance < best_distance)
            {
                best_distance = distance;
                best = index;
            }
        }
        return best;
    }

    Control Realize(int index, IReadOnlyList<object?> items)
    {
        if (_realized.TryGetValue(index, out Control? existing))
            return existing;
        ItemContainerGenerator generator = ItemContainerGenerator ?? throw new InvalidOperationException("The panel is not hosted by an items control.");
        object? item = items[index];
        Control container;
        if (generator.NeedsContainer(item, index, out object? recycle_key))
        {
            if (recycle_key is not null && _pool.TryGetValue(recycle_key, out Stack<Control>? pooled) && pooled.TryPop(out Control? reused))
            {
                container = reused;
                container.IsVisible = true;
            }
            else
            {
                container = generator.CreateContainer(item, index, recycle_key);
                AddInternalChild(container);
            }
            _recycle_keys[container] = recycle_key;
            generator.PrepareItemContainer(container, item, index);
            generator.ItemContainerPrepared(container, item, index);
        }
        else
        {
            container = item as Control ?? throw new InvalidOperationException("An item that is its own container must be a control.");
            _recycle_keys[container] = null;
            AddInternalChild(container);
        }
        _realized[index] = container;
        return container;
    }

    void Unrealize(int index)
    {
        if (!_realized.Remove(index, out Control? container))
            return;
        ItemContainerGenerator?.ClearItemContainer(container);
        object? recycle_key = _recycle_keys.GetValueOrDefault(container);
        if (recycle_key is not null)
        {
            Stack<Control> pooled = _pool.TryGetValue(recycle_key, out Stack<Control>? existing) ? existing : _pool[recycle_key] = new Stack<Control>();
            if (pooled.Count < PoolCapacity)
            {
                container.IsVisible = false;
                pooled.Push(container);
                return;
            }
        }
        _recycle_keys.Remove(container);
        RemoveInternalChild(container);
    }

    void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs args)
    {
        if (args.EffectiveViewport == _viewport)
            return;
        _viewport = args.EffectiveViewport;
        InvalidateMeasure();
    }
}
