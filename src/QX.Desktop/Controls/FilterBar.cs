using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Qx.Desktop.Controls;

[PseudoClasses(":search", ":filters", ":trailing")]
public sealed class FilterBar : TemplatedControl
{
    public static readonly StyledProperty<object?> SearchProperty =
        AvaloniaProperty.Register<FilterBar, object?>(nameof(Search));

    public static readonly StyledProperty<object?> FiltersProperty =
        AvaloniaProperty.Register<FilterBar, object?>(nameof(Filters));

    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<FilterBar, object?>(nameof(Trailing));

    public object? Search
    {
        get => GetValue(SearchProperty);
        set => SetValue(SearchProperty, value);
    }

    public object? Filters
    {
        get => GetValue(FiltersProperty);
        set => SetValue(FiltersProperty, value);
    }

    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SearchProperty)
            PseudoClasses.Set(":search", Search is not null);
        else if (change.Property == FiltersProperty)
            PseudoClasses.Set(":filters", Filters is not null);
        else if (change.Property == TrailingProperty)
            PseudoClasses.Set(":trailing", Trailing is not null);
    }
}
