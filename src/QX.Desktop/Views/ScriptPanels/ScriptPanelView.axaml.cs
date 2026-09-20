using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Qx.Presentation.ViewModels.ScriptPanels;

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
            previous.Panel.IsShown = false;
        }
        _panel = DataContext as ScriptPanelViewModel;
        if (_panel is { } panel)
            panel.PropertyChanged += OnPanelChanged;
        ApplyShown();
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
            panel.Panel.IsShown = false;
        }
        _panel = null;
    }

    void OnPanelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ScriptPanelViewModel.IsPanelMode))
            ApplyShown();
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
