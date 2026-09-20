using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace Qx.Desktop.Controls;

[PseudoClasses(":empty")]
public sealed class CommandPalette : TemplatedControl
{
    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<CommandPalette, string?>(nameof(Query), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<CommandPalette, string?>(nameof(Placeholder), "Search commands");

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CommandPalette, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<CommandPalette, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CommandPalette, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<CommandPalette, bool>(nameof(IsEmpty));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<CommandPalette, string?>(nameof(EmptyText), "No matching command");

    TextBox? _query;

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    public string? EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public void FocusQuery()
    {
        ApplyTemplate();
        _query?.Focus();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _query = e.NameScope.Find<TextBox>("PART_Query");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEmptyProperty)
            PseudoClasses.Set(":empty", IsEmpty);
    }
}
