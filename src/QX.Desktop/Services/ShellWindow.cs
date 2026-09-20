using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Settings;

namespace Qx.Desktop.Services;

public sealed class ShellWindow : IShellWindow, IDisposable
{
    public const double MacLeadingInset = 72;
    public const int CaptionButtons = 3;

    Window? _window;
    IClassicDesktopStyleApplicationLifetime? _lifetime;
    PixelPlacement _normal;
    WindowState _remembered = WindowState.Normal;
    bool _seeded_state;
    bool _hidden_for_host;
    bool _was_visible;

    public bool IsVisible => _window?.IsVisible == true;

    public bool IsMinimized => _window?.WindowState == WindowState.Minimized;

    public bool IsActive => _window?.IsActive == true;

    public bool Topmost
    {
        get => _window?.Topmost == true;
        set
        {
            if (_window is { } window)
                window.Topmost = value;
        }
    }

    public string Title
    {
        get => _window?.Title ?? "";
        set
        {
            if (_window is { } window)
                window.Title = value;
        }
    }

    public TitleBarInsets Insets { get; private set; } = TitleBarInsets.None;

    public event Action? Activated;

    public event Action? VisibilityChanged;

    public event Action? InsetsChanged;

    public void Attach(Window window, IClassicDesktopStyleApplicationLifetime? lifetime)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _lifetime = lifetime;
        _normal = new PixelPlacement(window.Position.X, window.Position.Y, window.Width, window.Height, false);
        _was_visible = window.IsVisible;
        window.Activated += OnActivated;
        window.PositionChanged += OnPositionChanged;
        window.Resized += OnResized;
        window.PropertyChanged += OnWindowPropertyChanged;
        RefreshInsets();
    }

    public void Dispose() => Detach();

    void Detach()
    {
        if (_window is not { } window)
            return;
        window.Activated -= OnActivated;
        window.PositionChanged -= OnPositionChanged;
        window.Resized -= OnResized;
        window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
    }

    public void ShowAndActivate()
    {
        if (_window is not { } window)
            return;
        window.ShowInTaskbar = true;
        bool restore = !window.IsVisible || window.WindowState == WindowState.Minimized;
        window.Show();
        if (restore)
            window.WindowState = _remembered;
        _hidden_for_host = false;
        window.Activate();
        RaiseVisibility();
    }

    public void PrepareForHost(WindowPlacement? stored)
    {
        if (_window is not { } window)
            return;
        Remember(stored?.Maximized == true);
        if (WindowPlacementMath.Restore(stored, WindowPlacementController.ScreensOf(window)) is { } placement)
            WindowPlacementController.Apply(window, placement with { Maximized = false });
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
    }

    public void Remember(bool maximized)
    {
        _remembered = maximized ? WindowState.Maximized : WindowState.Normal;
        _seeded_state = true;
    }

    public void RestoreState()
    {
        if (_window is { WindowState: WindowState.Normal } window && _remembered == WindowState.Maximized)
            window.WindowState = WindowState.Maximized;
    }

    public void HideForHost()
    {
        if (_window is not { } window)
            return;
        if (_seeded_state)
            _seeded_state = false;
        else if (window.WindowState != WindowState.Minimized)
            _remembered = window.WindowState;
        _hidden_for_host = true;
        window.ShowInTaskbar = false;
        window.Hide();
        RaiseVisibility();
    }

    public void Hide()
    {
        _window?.Hide();
        RaiseVisibility();
    }

    public void Shutdown(int exit_code)
    {
        if (_lifetime is { } lifetime)
        {
            lifetime.Shutdown(exit_code);
            return;
        }
        _window?.Close();
    }

    public WindowPlacement? CapturePlacement()
    {
        if (_window is not { } window)
            return null;
        bool maximized = window.WindowState == WindowState.Maximized || (_hidden_for_host && _remembered == WindowState.Maximized);
        return WindowPlacementMath.Capture(_normal, maximized, WindowPlacementController.ScreensOf(window));
    }

    void OnActivated(object? sender, EventArgs e) => Activated?.Invoke();

    void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (_window is { WindowState: WindowState.Normal })
            _normal = _normal with { X = e.Point.X, Y = e.Point.Y };
    }

    void OnResized(object? sender, WindowResizedEventArgs e)
    {
        if (_window is { WindowState: WindowState.Normal })
            _normal = _normal with { Width = e.ClientSize.Width, Height = e.ClientSize.Height };
    }

    void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == Window.WindowStateProperty)
        {
            RefreshInsets();
            RaiseVisibility();
            return;
        }
        if (change.Property == Window.IsVisibleProperty)
        {
            RaiseVisibility();
            return;
        }
        if (change.Property == Window.IsExtendedIntoWindowDecorationsProperty)
            RefreshInsets();
    }

    void RaiseVisibility()
    {
        bool visible = IsVisible && !IsMinimized;
        if (visible == _was_visible)
            return;
        _was_visible = visible;
        VisibilityChanged?.Invoke();
    }

    void RefreshInsets()
    {
        TitleBarInsets next = Measure();
        if (next == Insets)
            return;
        Insets = next;
        InsetsChanged?.Invoke();
    }

    TitleBarInsets Measure()
    {
        if (_window is not { IsExtendedIntoWindowDecorations: true } window)
            return TitleBarInsets.None;
        if (OperatingSystem.IsMacOS())
            return window.WindowState == WindowState.FullScreen ? TitleBarInsets.None : new TitleBarInsets(MacLeadingInset, 0);
        double button = window.TryFindResource("QxCaptionButtonWidth", out object? resource) && resource is double width ? width : 46;
        return new TitleBarInsets(0, CaptionButtons * button);
    }
}
