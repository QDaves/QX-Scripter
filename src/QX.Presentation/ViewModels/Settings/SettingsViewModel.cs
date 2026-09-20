using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Mcp;
using Qx.Presentation.Input;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Runtime;
using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Services.Status;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.Settings;

public sealed partial class SettingsViewModel : PageViewModel
{
    readonly ISettingsStore _settings;
    readonly IThemeService _theme;
    readonly McpServer _mcp;
    readonly IAppPaths _paths;
    readonly IScriptFileService _files;
    readonly ILauncherService _launcher;
    readonly IShellWindow _window;
    readonly ISessionStatusService _status;
    readonly EditorPreferences _editor;
    bool _syncing;

    public SettingsViewModel(
        ISettingsStore settings,
        IThemeService theme,
        DesktopRuntime runtime,
        IAppPaths paths,
        IScriptFileService files,
        ILauncherService launcher,
        IClipboardService clipboard,
        IShellWindow window,
        ISessionStatusService status,
        PanicKey panic,
        IGestureFormatter gestures,
        EditorPreferences editor,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        ArgumentNullException.ThrowIfNull(runtime);
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        ArgumentNullException.ThrowIfNull(clipboard);
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        ArgumentNullException.ThrowIfNull(panic);
        ArgumentNullException.ThrowIfNull(gestures);
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        _mcp = runtime.Mcp;

        Subtitle = "Appearance, storage and connections.";
        Notices = Own(new NoticeLine(dispatcher, time));
        McpCopy = Own(new CopyAction(
            clipboard,
            dispatcher,
            time,
            () => SettingsMcpText.CopyUrl(_mcp.IsRunning, _mcp.ClientUrl),
            reason => Notices.Show(NoticeSeverity.Error, reason)));
        ShortcutGroups = BuildShortcuts(panic, gestures);

        _syncing = true;
        RestoreSession = settings.Current.RestoreSession;
        KeepOnTop = settings.Current.Topmost;
        McpConfig config = _mcp.Config;
        AllowExecute = config.AllowExecute;
        AllowFileWrite = config.AllowFileWrite;
        AllowEditor = config.AllowEditor;
        _syncing = false;

        RefreshMcp();
        RefreshConnection();

        theme.EffectiveChanged += OnThemeChanged;
        status.Changed += OnStatusChanged;
        settings.Changed += OnSettingsChanged;
        editor.PropertyChanged += OnEditorChanged;
        Own(() =>
        {
            theme.EffectiveChanged -= OnThemeChanged;
            status.Changed -= OnStatusChanged;
            settings.Changed -= OnSettingsChanged;
            editor.PropertyChanged -= OnEditorChanged;
        });

        State.ShowReady();
    }

    public NoticeLine Notices { get; }

    public CopyAction McpCopy { get; }

    public IReadOnlyList<ShortcutGroup> ShortcutGroups { get; }

    public string ScriptsFolder => _files.ScriptsDirectory;

    public string EditorFontSizeText => $"{_editor.FontSize.ToString("0", CultureInfo.InvariantCulture)} px";

    public int SelectedThemeIndex
    {
        get => (int)_theme.Mode;
        set
        {
            if (value is >= 0 and <= 2 && (ThemeMode)value != _theme.Mode)
                _theme.Change((ThemeMode)value);
        }
    }

    [ObservableProperty]
    public partial bool RestoreSession { get; set; }

    [ObservableProperty]
    public partial bool KeepOnTop { get; set; }

    [ObservableProperty]
    public partial bool IsMcpRunning { get; private set; }

    [ObservableProperty]
    public partial string McpUrlDisplay { get; private set; } = SettingsMcpText.NotRunning;

    [ObservableProperty]
    public partial string McpHint { get; private set; } = SettingsMcpText.StoppedHint;

    [ObservableProperty]
    public partial bool AllowExecute { get; set; }

    [ObservableProperty]
    public partial bool AllowFileWrite { get; set; }

    [ObservableProperty]
    public partial bool AllowEditor { get; set; }

    [ObservableProperty]
    public partial string? CapabilityHint { get; private set; }

    [ObservableProperty]
    public partial bool GEarthConnected { get; private set; }

    [ObservableProperty]
    public partial string GEarthPort { get; private set; } = "";

    [ObservableProperty]
    public partial string ClientText { get; private set; } = "not connected";

    [RelayCommand(CanExecute = nameof(CanIncreaseFont))]
    void IncreaseFont() => _editor.ZoomIn();

    bool CanIncreaseFont() => _editor.FontSize < EditorPreferences.MaximumFontSize;

    [RelayCommand(CanExecute = nameof(CanDecreaseFont))]
    void DecreaseFont() => _editor.ZoomOut();

    bool CanDecreaseFont() => _editor.FontSize > EditorPreferences.MinimumFontSize;

    [RelayCommand]
    async Task OpenScriptsFolderAsync(CancellationToken cancellation_token)
    {
        Notices.Clear();
        bool opened = await _launcher.OpenFolderAsync(_files.ScriptsDirectory, cancellation_token);
        if (!opened)
            Notices.Show(NoticeSeverity.Error, "Could not open the scripts folder.");
    }

    partial void OnRestoreSessionChanged(bool value)
    {
        if (_syncing)
            return;
        _settings.Update(document => document.RestoreSession == value ? document : document with { RestoreSession = value });
    }

    partial void OnKeepOnTopChanged(bool value)
    {
        if (_syncing)
            return;
        _window.Topmost = value;
        _settings.Update(document => document.Topmost == value ? document : document with { Topmost = value });
    }

    partial void OnAllowExecuteChanged(bool value) => SaveCapabilities();

    partial void OnAllowFileWriteChanged(bool value) => SaveCapabilities();

    partial void OnAllowEditorChanged(bool value) => SaveCapabilities();

    void SaveCapabilities()
    {
        if (_syncing)
            return;
        McpConfig updated = _mcp.Config with { AllowExecute = AllowExecute, AllowFileWrite = AllowFileWrite, AllowEditor = AllowEditor };
        _mcp.Config = updated;
        try
        {
            updated.Save(_paths.McpConfigFile);
            CapabilityHint = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            CapabilityHint = $"Applied, but could not be saved for next time: {error.Message}";
        }
    }

    void RefreshMcp()
    {
        bool running = _mcp.IsRunning;
        (string display, string hint, bool _) = SettingsMcpText.Describe(running, _mcp.ClientUrl);
        IsMcpRunning = running;
        McpUrlDisplay = display;
        McpHint = hint;
    }

    void RefreshConnection()
    {
        SessionStatus snapshot = _status.Current;
        GEarthConnected = snapshot.IsGEarthConnected;
        GEarthPort = snapshot.GEarthPort.ToString(CultureInfo.InvariantCulture);
        ClientText = snapshot.ClientName.Length > 0 ? snapshot.ClientName : "not connected";
    }

    void OnThemeChanged(bool dark) => OnPropertyChanged(nameof(SelectedThemeIndex));

    void OnStatusChanged(SessionStatus snapshot)
    {
        RefreshMcp();
        RefreshConnection();
    }

    void OnSettingsChanged(SettingsDocument document)
    {
        _syncing = true;
        RestoreSession = document.RestoreSession;
        KeepOnTop = document.Topmost;
        _syncing = false;
    }

    void OnEditorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not (nameof(EditorPreferences.FontSize) or null))
            return;
        OnPropertyChanged(nameof(EditorFontSizeText));
        IncreaseFontCommand.NotifyCanExecuteChanged();
        DecreaseFontCommand.NotifyCanExecuteChanged();
    }

    static IReadOnlyList<ShortcutGroup> BuildShortcuts(PanicKey panic, IGestureFormatter gestures)
    {
        var order = new List<CommandGroup>();
        var rows = new Dictionary<CommandGroup, List<ShortcutRow>>();
        foreach (CommandSpec spec in ShellCommandCatalog.All)
        {
            bool is_panic = string.Equals(spec.Id, ShellCommandCatalog.RunStopAll, StringComparison.Ordinal);
            if (!is_panic && spec.Chords.Count == 0)
                continue;
            IReadOnlyList<string> keys;
            string? note;
            if (is_panic)
            {
                keys = panic.IsRegistered ? GestureText.Parts(panic.Gesture) : [];
                note = panic.IsRegistered ? null : "not available on this system";
            }
            else
            {
                keys = GestureText.Parts(gestures.Describe(spec.Chords[0]));
                note = null;
            }
            if (!rows.TryGetValue(spec.Group, out List<ShortcutRow>? group))
            {
                group = [];
                rows[spec.Group] = group;
                order.Add(spec.Group);
            }
            group.Add(new ShortcutRow(spec.Title, keys, note));
        }
        return [.. order.Select(group => new ShortcutGroup(group.ToString(), rows[group]))];
    }
}
