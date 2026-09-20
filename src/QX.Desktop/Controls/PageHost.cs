using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Qx.Desktop.Controls;

public sealed class PageHost : Panel
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageHost, object?>(nameof(Page));

    public static readonly StyledProperty<IDataTemplate?> ViewsProperty =
        AvaloniaProperty.Register<PageHost, IDataTemplate?>(nameof(Views));

    readonly Dictionary<object, Control> _views = new(ReferenceEqualityComparer.Instance);

    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public IDataTemplate? Views
    {
        get => GetValue(ViewsProperty);
        set => SetValue(ViewsProperty, value);
    }

    public int CachedViewCount => _views.Count;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty || change.Property == ViewsProperty)
            Show(Page);
    }

    void Show(object? page)
    {
        if (page is not null && Views is { } views && !_views.ContainsKey(page))
        {
            Control view = views.Build(page) ?? throw new InvalidOperationException($"No view for {page.GetType().Name}.");
            _views[page] = view;
            Children.Add(view);
        }
        foreach ((object key, Control view) in _views)
            view.IsVisible = ReferenceEquals(key, page);
    }
}
