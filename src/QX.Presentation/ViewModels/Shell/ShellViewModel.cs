using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Lifecycle;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Shell;

public sealed partial class ShellViewModel : ViewModelBase
{
    public const string PaletteHint = "Search commands, scripts and pages";

    readonly INavigationService _navigation;
    readonly ICommandRegistry _registry;
    readonly IThemeService _theme;
    readonly ISettingsStore _settings;
    readonly IShellWindow _window;
    readonly IDialogService _dialogs;
    readonly ShellCloseCoordinator _close;
    readonly RailItemViewModel[] _rail;

    public ShellViewModel(
        INavigationService navigation,
        ICommandRegistry registry,
        IGestureFormatter gestures,
        IThemeService theme,
        ISettingsStore settings,
        IShellWindow window,
        IDialogService dialogs,
        INotificationService notifications,
        ICommandPalette palette,
        StatusBarViewModel status,
        ShellCloseCoordinator close)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentNullException.ThrowIfNull(gestures);
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        _close = close ?? throw new ArgumentNullException(nameof(close));
        WorkspaceItems = Items(PageGroup.Workspace, gestures);
        GameItems = Items(PageGroup.Game, gestures);
        SupportItems = Items(PageGroup.Support, gestures);
        _rail = [.. WorkspaceItems, .. GameItems, .. SupportItems];
        NewScriptTip = Tip(PageCatalog.NewScriptRailTip, PageCatalog.NewScriptCommandId, gestures);
        PaletteKeys = ShellCommandCatalog.Find(ShellCommandCatalog.PaletteOpen) is { Chords.Count: > 0 } opener
            ? GestureText.Parts(gestures.Describe(opener.Chords[0]))
            : [];
        IsDark = theme.IsDark;
        IsTopmost = settings.Current.Topmost;
        DismissToastCommand = new RelayCommand<Toast>(toast =>
        {
            if (toast is not null)
                Notifications.Dismiss(toast);
        });
        theme.EffectiveChanged += OnThemeChanged;
        navigation.Navigated += OnNavigated;
        if (dialogs is INotifyPropertyChanged dialog_changes)
            dialog_changes.PropertyChanged += OnDialogsChanged;
        Own(() =>
        {
            theme.EffectiveChanged -= OnThemeChanged;
            navigation.Navigated -= OnNavigated;
            if (dialogs is INotifyPropertyChanged releasing)
                releasing.PropertyChanged -= OnDialogsChanged;
        });
        SyncNavigation();
    }

    public INavigationService Navigation => _navigation;

    public IDialogService Dialogs => _dialogs;

    public INotificationService Notifications { get; }

    public ICommandPalette Palette { get; }

    public StatusBarViewModel Status { get; }

    public IReadOnlyList<RailItemViewModel> WorkspaceItems { get; }

    public IReadOnlyList<RailItemViewModel> GameItems { get; }

    public IReadOnlyList<RailItemViewModel> SupportItems { get; }

    public string NewScriptTip { get; }

    public IReadOnlyList<string> PaletteKeys { get; }

    public IRelayCommand<Toast> DismissToastCommand { get; }

    public string PaletteText => PaletteHint;

    [ObservableProperty]
    public partial string PageTitle { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon), nameof(ThemeTip))]
    public partial bool IsDark { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TopmostIcon), nameof(TopmostTip))]
    public partial bool IsTopmost { get; set; }

    public IconKind ThemeIcon => IsDark ? IconKind.ThemeLight : IconKind.ThemeDark;

    public string ThemeTip => IsDark ? "Switch to the light theme" : "Switch to the dark theme";

    public IconKind TopmostIcon => IsTopmost ? IconKind.PinOff : IconKind.Pin;

    public string TopmostTip => IsTopmost ? "Stop keeping the window on top" : "Keep the window on top";

    [RelayCommand]
    void ToggleTheme() => _theme.Change(_theme.IsDark ? ThemeMode.Light : ThemeMode.Dark);

    [RelayCommand]
    void ToggleTopmost() => IsTopmost = !IsTopmost;

    [RelayCommand]
    void OpenPalette() => Palette.Open();

    bool CanNewScript() => Find(PageCatalog.NewScriptCommandId) is { } command && command.IsAvailable() && command.Command.CanExecute(null);

    [RelayCommand(CanExecute = nameof(CanNewScript))]
    void NewScript() => Find(PageCatalog.NewScriptCommandId)?.Command.Execute(null);

    partial void OnIsTopmostChanged(bool value)
    {
        _window.Topmost = value;
        _settings.Update(document => document.Topmost == value ? document : document with { Topmost = value });
    }

    AppCommand? Find(string id) => _registry.Commands.FirstOrDefault(command => string.Equals(command.Id, id, StringComparison.Ordinal));

    RailItemViewModel[] Items(PageGroup group, IGestureFormatter gestures) =>
        [.. PageCatalog.InGroup(group).Select(page => new RailItemViewModel(page.Key, page.Icon, Tip(page.RailTip, page.CommandId, gestures), new RelayCommand(() => _navigation.Toggle(page.Key))))];

    static string Tip(string rail_tip, string command_id, IGestureFormatter gestures) =>
        ShellCommandCatalog.Find(command_id) is { Chords.Count: > 0 } spec ? $"{rail_tip} ({gestures.Describe(spec.Chords[0])})" : rail_tip;

    void OnThemeChanged(bool dark) => IsDark = dark;

    void OnNavigated(PageKey key) => SyncNavigation();

    void SyncNavigation()
    {
        PageKey current = _navigation.Current;
        bool started = _navigation.IsStarted;
        foreach (RailItemViewModel item in _rail)
            item.IsSelected = started && item.Key == current;
        PageTitle = _navigation.CurrentPage?.Title ?? "";
    }

    void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IDialogService.Current) || _dialogs.Current is null || _close.IsClosing)
            return;
        if (!_window.IsVisible || _window.IsMinimized)
            _window.ShowAndActivate();
    }
}
