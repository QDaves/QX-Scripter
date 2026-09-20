using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Qx.Desktop.Controls;

[PseudoClasses(":compiling", ":running", ":armed", ":failed", ":modified", ":active", ":tab-hover")]
public sealed class TabHeader : TemplatedControl
{
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<TabHeader, string?>(nameof(FileName));

    public static readonly StyledProperty<TabHeaderState> StateProperty =
        AvaloniaProperty.Register<TabHeader, TabHeaderState>(nameof(State));

    public static readonly StyledProperty<bool> IsModifiedProperty =
        AvaloniaProperty.Register<TabHeader, bool>(nameof(IsModified));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<TabHeader, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsTabHoveredProperty =
        AvaloniaProperty.Register<TabHeader, bool>(nameof(IsTabHovered));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<TabHeader, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<object?> CloseCommandParameterProperty =
        AvaloniaProperty.Register<TabHeader, object?>(nameof(CloseCommandParameter));

    public string? FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public TabHeaderState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public bool IsModified
    {
        get => GetValue(IsModifiedProperty);
        set => SetValue(IsModifiedProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public bool IsTabHovered
    {
        get => GetValue(IsTabHoveredProperty);
        set => SetValue(IsTabHoveredProperty, value);
    }

    public object? CloseCommandParameter
    {
        get => GetValue(CloseCommandParameterProperty);
        set => SetValue(CloseCommandParameterProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            PseudoClasses.Set(":compiling", State == TabHeaderState.Compiling);
            PseudoClasses.Set(":running", State == TabHeaderState.Running);
            PseudoClasses.Set(":armed", State == TabHeaderState.Armed);
            PseudoClasses.Set(":failed", State == TabHeaderState.Failed);
        }
        else if (change.Property == IsModifiedProperty)
        {
            PseudoClasses.Set(":modified", IsModified);
        }
        else if (change.Property == IsActiveProperty)
        {
            PseudoClasses.Set(":active", IsActive);
        }
        else if (change.Property == IsTabHoveredProperty)
        {
            PseudoClasses.Set(":tab-hover", IsTabHovered);
        }
    }
}
