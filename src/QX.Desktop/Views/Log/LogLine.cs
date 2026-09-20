using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Diagnostics;

namespace Qx.Desktop.Views.Log;

[PseudoClasses(":warning", ":error", ":wrap")]
public sealed class LogLine : TemplatedControl
{
    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<LogLine, string?>(nameof(Time));

    public static readonly StyledProperty<DiagLevel> LevelProperty =
        AvaloniaProperty.Register<LogLine, DiagLevel>(nameof(Level));

    public static readonly StyledProperty<string?> LevelNameProperty =
        AvaloniaProperty.Register<LogLine, string?>(nameof(LevelName));

    public static readonly StyledProperty<string?> CategoryProperty =
        AvaloniaProperty.Register<LogLine, string?>(nameof(Category));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<LogLine, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsWrappedProperty =
        AvaloniaProperty.Register<LogLine, bool>(nameof(IsWrapped));

    public string? Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public DiagLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public string? LevelName
    {
        get => GetValue(LevelNameProperty);
        set => SetValue(LevelNameProperty, value);
    }

    public string? Category
    {
        get => GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsWrapped
    {
        get => GetValue(IsWrappedProperty);
        set => SetValue(IsWrappedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsWrappedProperty)
        {
            PseudoClasses.Set(":wrap", IsWrapped);
            return;
        }
        if (change.Property != LevelProperty)
            return;
        PseudoClasses.Set(":warning", Level == DiagLevel.Warn);
        PseudoClasses.Set(":error", Level >= DiagLevel.Error);
    }
}
