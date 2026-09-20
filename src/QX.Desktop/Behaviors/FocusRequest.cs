using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

namespace Qx.Desktop.Behaviors;

public static class FocusRequest
{
    public static readonly AttachedProperty<bool> IsRequestedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsRequested", typeof(FocusRequest), defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<bool> OnAttachedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("OnAttached", typeof(FocusRequest));

    public static readonly AttachedProperty<bool> KeyboardScrollProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("KeyboardScroll", typeof(FocusRequest));

    static FocusRequest()
    {
        IsRequestedProperty.Changed.AddClassHandler<Control>(OnIsRequestedChanged);
        OnAttachedProperty.Changed.AddClassHandler<Control>(OnOnAttachedChanged);
        KeyboardScrollProperty.Changed.AddClassHandler<ScrollViewer>(OnKeyboardScrollChanged);
    }

    public static bool GetIsRequested(Control control) => control.GetValue(IsRequestedProperty);

    public static void SetIsRequested(Control control, bool value) => control.SetValue(IsRequestedProperty, value);

    public static bool GetOnAttached(Control control) => control.GetValue(OnAttachedProperty);

    public static void SetOnAttached(Control control, bool value) => control.SetValue(OnAttachedProperty, value);

    public static bool GetKeyboardScroll(ScrollViewer viewer) => viewer.GetValue(KeyboardScrollProperty);

    public static void SetKeyboardScroll(ScrollViewer viewer, bool value) => viewer.SetValue(KeyboardScrollProperty, value);

    static void OnKeyboardScrollChanged(ScrollViewer viewer, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
            viewer.GotFocus += OnScrollFocus;
        else
            viewer.GotFocus -= OnScrollFocus;
    }

    static void OnScrollFocus(object? sender, FocusChangedEventArgs args)
    {
        if (sender is ScrollViewer { BringIntoViewOnFocusChange: false } &&
            args.NavigationMethod != NavigationMethod.Pointer && args.Source is Control control && control != sender)
            control.BringIntoView();
    }

    static void OnIsRequestedChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is not true)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            control.Focus();
            control.SetCurrentValue(IsRequestedProperty, false);
        }, DispatcherPriority.Input);
    }

    static void OnOnAttachedChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
            control.AttachedToVisualTree += OnAttached;
        else
            control.AttachedToVisualTree -= OnAttached;
    }

    static void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
    {
        if (sender is Control control)
            Dispatcher.UIThread.Post(() => control.Focus(), DispatcherPriority.Input);
    }
}
