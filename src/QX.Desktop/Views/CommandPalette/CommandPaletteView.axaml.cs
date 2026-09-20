using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Presentation.ViewModels.CommandPalette;

namespace Qx.Desktop.Views.CommandPalette;

public sealed partial class CommandPaletteView : UserControl
{
    CommandPaletteViewModel? _palette;
    IInputElement? _restore;

    public CommandPaletteView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AddHandler(KeyDownEvent, OnPaletteKey, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnRowReleased, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        RemoveHandler(KeyDownEvent, OnPaletteKey);
        RemoveHandler(PointerReleasedEvent, OnRowReleased);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_palette is { } previous)
            previous.PropertyChanged -= OnPaletteChanged;
        _palette = DataContext as CommandPaletteViewModel;
        if (_palette is { } next)
            next.PropertyChanged += OnPaletteChanged;
        if (_palette is { IsOpen: true })
            Enter();
    }

    void OnPaletteChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(CommandPaletteViewModel.IsOpen))
            return;
        if (_palette is { IsOpen: true })
            Enter();
        else
            Leave();
    }

    void OnPaletteKey(object? sender, KeyEventArgs args)
    {
        if (_palette is not { IsOpen: true } palette)
            return;
        switch (args.Key)
        {
            case Key.Down:
                palette.MoveSelectionCommand.Execute(1);
                break;
            case Key.Up:
                palette.MoveSelectionCommand.Execute(-1);
                break;
            case Key.Enter:
                palette.InvokeSelectedCommand.Execute(null);
                break;
            case Key.Escape:
                palette.Dismiss();
                break;
            default:
                return;
        }
        args.Handled = true;
    }

    void OnRowReleased(object? sender, PointerReleasedEventArgs args)
    {
        if (_palette is not { IsOpen: true } palette || args.InitialPressMouseButton != MouseButton.Left)
            return;
        if (Container(args.Source as Visual)?.Content is not PaletteEntry entry)
            return;
        palette.Selected = entry;
        palette.InvokeSelectedCommand.Execute(null);
        args.Handled = true;
    }

    void Enter()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager is { } manager && manager.GetFocusedElement() is { } focused && !IsInside(focused))
            _restore = focused;
        Dispatcher.UIThread.Post(FocusQuery, DispatcherPriority.Loaded);
    }

    void Leave()
    {
        IInputElement? back = _restore;
        _restore = null;
        if (TopLevel.GetTopLevel(this)?.FocusManager is not { } manager)
            return;
        if (manager.GetFocusedElement() is { } focused && !IsInside(focused))
            return;
        if (back is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control && control.IsAttachedToVisualTree())
            manager.Focus(control);
        else
            manager.Focus(null);
    }

    void FocusQuery()
    {
        if (_palette is not { IsOpen: true } || !this.IsAttachedToVisualTree())
            return;
        Card.FocusQuery();
    }

    bool IsInside(IInputElement element) => element is Visual visual && this.IsVisualAncestorOf(visual);

    static ListBoxItem? Container(Visual? source)
    {
        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ListBoxItem container)
                return container;
        }
        return null;
    }
}
