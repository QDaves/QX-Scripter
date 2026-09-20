using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Qx.Presentation.Input;
using Qx.Presentation.Visuals;

namespace Qx.Desktop.Controls;

[PseudoClasses(":gesture", ":icon")]
public sealed class CommandRow : TemplatedControl
{
    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(Group));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> GestureProperty =
        AvaloniaProperty.Register<CommandRow, string?>(nameof(Gesture));

    public static readonly StyledProperty<IconKind> IconProperty =
        AvaloniaProperty.Register<CommandRow, IconKind>(nameof(Icon));

    public static readonly DirectProperty<CommandRow, IReadOnlyList<string>> KeysProperty =
        AvaloniaProperty.RegisterDirect<CommandRow, IReadOnlyList<string>>(nameof(Keys), row => row.Keys);

    IReadOnlyList<string> _keys = [];

    public string? Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public IconKind Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IReadOnlyList<string> Keys
    {
        get => _keys;
        private set => SetAndRaise(KeysProperty, ref _keys, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GestureProperty)
        {
            Keys = GestureText.Parts(Gesture ?? "");
            PseudoClasses.Set(":gesture", Keys.Count > 0);
        }
        else if (change.Property == IconProperty)
        {
            PseudoClasses.Set(":icon", Icon != IconKind.None);
        }
    }
}
