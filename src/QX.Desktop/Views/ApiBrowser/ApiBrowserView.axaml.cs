using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Qx.Presentation.ViewModels.ApiBrowser;
using Qx.Scripting;

namespace Qx.Desktop.Views.ApiBrowser;

public sealed partial class ApiBrowserView : UserControl
{
    ApiBrowserViewModel? _browser;

    public ApiBrowserView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Subscribe();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        QueryBox.AddHandler(KeyDownEvent, OnSearchKey, RoutingStrategies.Tunnel);
        ResultsList.KeyDown += OnResultKey;
        ResultsList.DoubleTapped += OnResultTapped;
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        QueryBox.RemoveHandler(KeyDownEvent, OnSearchKey);
        ResultsList.KeyDown -= OnResultKey;
        ResultsList.DoubleTapped -= OnResultTapped;
        if (_browser is { } previous)
            previous.PropertyChanged -= OnBrowserChanged;
        _browser = null;
        base.OnDetachedFromVisualTree(e);
    }

    void Subscribe()
    {
        if (_browser is { } previous)
            previous.PropertyChanged -= OnBrowserChanged;
        _browser = DataContext as ApiBrowserViewModel;
        if (_browser is { } next)
            next.PropertyChanged += OnBrowserChanged;
    }

    void OnBrowserChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApiBrowserViewModel.FocusRequest))
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsEffectivelyVisible)
                    return;
                QueryBox.Focus();
                QueryBox.SelectAll();
            }, DispatcherPriority.Loaded);
        else if (e.PropertyName == nameof(ApiBrowserViewModel.Selected) && _browser?.Selected is { } member)
            ResultsList.ScrollIntoView(member);
    }

    void OnSearchKey(object? sender, KeyEventArgs e)
    {
        if (_browser is not { } browser)
            return;
        switch (e.Key)
        {
            case Key.Up: browser.Move(-1); break;
            case Key.Down: browser.Move(1); break;
            case Key.Enter: browser.InsertCommand.Execute(null); break;
            case Key.Escape: browser.Escape(); break;
            default: return;
        }
        e.Handled = true;
    }

    void OnResultKey(object? sender, KeyEventArgs e)
    {
        if (_browser is not { } browser)
            return;
        if (e.Key == Key.Enter)
            browser.InsertCommand.Execute(null);
        else if (e.Key == Key.Escape)
            browser.CloseCommand.Execute(null);
        else
            return;
        e.Handled = true;
    }

    void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (_browser is not { } browser ||
            (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true)?.DataContext is not ScriptApiMember member)
            return;
        browser.Selected = member;
        browser.InsertCommand.Execute(null);
        e.Handled = true;
    }
}
