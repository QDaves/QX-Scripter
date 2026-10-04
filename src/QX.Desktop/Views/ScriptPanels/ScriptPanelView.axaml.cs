using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Qx.Presentation.Services.Panels;
using Qx.Presentation.ViewModels.ScriptPanels;
using Qx.Scripting.Hosting;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed partial class ScriptPanelView : UserControl
{
    ScriptPanelViewModel? _panel;

    public ScriptPanelView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_panel is { } previous)
        {
            previous.PropertyChanged -= OnPanelChanged;
            previous.Panel.PropertyChanged -= OnDocumentChanged;
            previous.Panel.IsShown = false;
        }
        _panel = DataContext as ScriptPanelViewModel;
        if (_panel is { } panel)
        {
            panel.PropertyChanged += OnPanelChanged;
            panel.Panel.PropertyChanged += OnDocumentChanged;
        }
        ApplyShown();
        ApplyLayout();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ApplyShown();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_panel is { } panel)
            panel.Panel.IsShown = false;
    }

    public void Release()
    {
        RemoveHandler(KeyDownEvent, OnKeyDown);
        if (_panel is { } panel)
        {
            panel.PropertyChanged -= OnPanelChanged;
            panel.Panel.PropertyChanged -= OnDocumentChanged;
            panel.Panel.IsShown = false;
        }
        _panel = null;
    }

    void OnPanelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ScriptPanelViewModel.IsPanelMode))
            ApplyShown();
    }

    void OnDocumentChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(PanelDocument.Layout))
            ApplyLayout();
    }

    void ApplyLayout()
    {
        UiLayout? layout = _panel?.Panel.Layout;
        Page.HorizontalAlignment = layout is { Centered: true } ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        Page.MaxWidth = layout?.Width
            ?? (this.TryFindResource("QxPanelMaxWidth", ActualThemeVariant, out object? width) && width is double fallback
                ? fallback
                : double.PositiveInfinity);
    }

    void ApplyShown()
    {
        if (_panel is { } panel)
            panel.Panel.IsShown = IsLoaded && panel.IsPanelMode;
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Handled || _panel is not { } panel)
            return;
        if (e.Source is TextBox { AcceptsReturn: false })
        {
            panel.PressPrimary();
            e.Handled = true;
        }
    }
}
