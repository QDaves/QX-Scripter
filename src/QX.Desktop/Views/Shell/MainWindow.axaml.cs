using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Qx.Desktop.Composition;
using Qx.Desktop.Controls;
using Qx.Desktop.Services;
using Qx.Presentation.Input;
using Qx.Presentation.Navigation;
using Qx.Presentation.Services.Lifecycle;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Shell;
using Qx.Presentation.ViewModels.Editor;
using BitmapCache = Qx.Desktop.Services.BitmapCache;

namespace Qx.Desktop.Views.Shell;

public sealed partial class MainWindow : Window
{
    ShellCloseCoordinator? _close;
    ICommandPalette? _palette;
    ShellWindow? _host;
    INavigationService? _navigation;
    IPageProvider? _pages;

    public MainWindow()
    {
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://QX/Assets/qx.ico")));
    }

    public void Use(ShellViewModel shell, ViewRegistry views, BitmapCache images, ShellWindow host, ShellCloseCoordinator close, IPageProvider pages)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(pages);
        _close = close;
        _palette = shell.Palette;
        _host = host;
        _pages = pages;
        _navigation = shell.Navigation;
        _navigation.Navigated += OnNavigated;
        if (_navigation.IsStarted)
            OnNavigated(_navigation.Current);
        DataContext = shell;
        Pages.Views = views;
        Dialogs.Views = views;
        RemoteImage.SetImages(this, images);
        Chrome.Reserve(host.Insets);
        host.InsetsChanged += OnInsetsChanged;
        Closing += OnClosing;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
        PaletteLayer.AddHandler(PointerPressedEvent, OnPaletteLayerPressed, RoutingStrategies.Bubble);
    }

    void OnInsetsChanged()
    {
        if (_host is { } host)
            Chrome.Reserve(host.Insets);
    }

    void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_close is not { } close || close.CanCloseWindowNow)
            return;
        args.Cancel = true;
        close.RequestCloseAsync(CloseReason.User).Observe("app");
    }

    void OnClosed(object? sender, EventArgs args)
    {
        if (_navigation is { } navigation)
            navigation.Navigated -= OnNavigated;
        Strip.Release();
        _navigation = null;
        _pages = null;
        if (_host is { } host)
            host.InsetsChanged -= OnInsetsChanged;
        PaletteLayer.RemoveHandler(PointerPressedEvent, OnPaletteLayerPressed);
        Closing -= OnClosing;
        Closed -= OnClosed;
        Deactivated -= OnDeactivated;
        _host = null;
    }

    void OnDeactivated(object? sender, EventArgs args) => _palette?.Dismiss();

    void OnNavigated(PageKey page)
    {
        Strip.IsVisible = page is PageKey.Library or PageKey.Editor;
        if (Strip.IsVisible && Strip.DataContext is not WorkspaceViewModel && _pages is { } pages)
            Strip.DataContext = pages.Get(PageKey.Editor);
    }

    void OnPaletteLayerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_palette is not { IsOpen: true } palette)
            return;
        if (args.Source is Visual source && Palette.IsVisualAncestorOf(source))
            return;
        palette.Dismiss();
        args.Handled = true;
    }
}
