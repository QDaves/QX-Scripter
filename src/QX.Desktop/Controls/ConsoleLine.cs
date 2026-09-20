using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Services.Output;

namespace Qx.Desktop.Controls;

[PseudoClasses(":info", ":warning", ":error", ":wrap")]
public sealed class ConsoleLine : TemplatedControl
{
    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<ConsoleLine, string?>(nameof(Time));

    public static readonly StyledProperty<OutputLevel> LevelProperty =
        AvaloniaProperty.Register<ConsoleLine, OutputLevel>(nameof(Level));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ConsoleLine, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsWrappedProperty =
        AvaloniaProperty.Register<ConsoleLine, bool>(nameof(IsWrapped));

    public static readonly DirectProperty<ConsoleLine, string> LevelTextProperty =
        AvaloniaProperty.RegisterDirect<ConsoleLine, string>(nameof(LevelText), line => line.LevelText);

    string _level_text = "INFO";

    public ConsoleLine()
    {
        PseudoClasses.Set(":info", true);
    }

    public string? Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public OutputLevel Level
    {
        get => GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsWrapped
    {
        get => GetValue(IsWrappedProperty);
        set => SetValue(IsWrappedProperty, value);
    }

    public string LevelText
    {
        get => _level_text;
        private set => SetAndRaise(LevelTextProperty, ref _level_text, value);
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
        PseudoClasses.Set(":info", Level == OutputLevel.Info);
        PseudoClasses.Set(":warning", Level == OutputLevel.Warning);
        PseudoClasses.Set(":error", Level == OutputLevel.Error);
        LevelText = Level switch
        {
            OutputLevel.Warning => "WARN",
            OutputLevel.Error => "ERR",
            _ => "INFO"
        };
    }
}
