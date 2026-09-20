using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Qx.Desktop.Controls;

public sealed class ToastHost : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ToastsProperty =
        AvaloniaProperty.Register<ToastHost, IEnumerable?>(nameof(Toasts));

    public static readonly StyledProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.Register<ToastHost, ICommand?>(nameof(DismissCommand));

    public IEnumerable? Toasts
    {
        get => GetValue(ToastsProperty);
        set => SetValue(ToastsProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }
}
