using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using RoslynPad.Editor;
using Qx.Game;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Mcp;
using Qx.Protocol;
using Qx.Scripting;

namespace Qx.Ui;

public partial class MainWindow : Window, Qx.Mcp.IEditorBridge
{
    private readonly ObservableCollection<ScriptTab> _tabs = [];
    private readonly InterceptFailureLog _intercept_failures = new();
    private MessageManager _messages = null!;
    private GameState _game = null!;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _scriptsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QX Scripter", "scripts");
    private readonly ScriptLibrary _library = new();
    private readonly DraftStore _drafts = new();
    private readonly Dictionary<ScriptTab, PanelRun> _panelRuns = [];
    private readonly UiTaskScope _ui_tasks;
    private readonly GEarthOptions _gearth_options;
    private readonly bool _launched_by_gearth;

    private GEarthExtension _extension = null!;
    private ScriptExecutionService _script_execution = null!;
    private RuntimeHost? _runtime;
    private McpServer? _mcp;
    private QxRoslynHost? _roslynHost;
    private StatusBarModel? _status;
    private int _gearthPort = 9092;
    private bool _closingConfirmed;
    private bool _close_with_gearth;
    private WindowState _hosted_restore_state = WindowState.Normal;
    private bool _closed;
    private ScriptTab? _homeReturnTab;
    private DispatcherTimer? _copyConfirmation;
    private int _errorIndex;
    private DispatcherTimer? _draftTimer;
    private ICollectionView? _outputView;
    private ScrollViewer? _outputScroller;
    private PanicHotkey? _panic;
    private HwndSource? _window_source;

    /// <summary>How many console lines the filter is showing, or -1 when no filter is set.</summary>
    /// <remarks>
    /// Carried rather than derived: counting the buffer per line is what the view's own count
    /// would cost, and the console is written to far more often than the filter changes.
    /// </remarks>
    private int _outputMatches = -1;

    /// <summary>Lines that arrived while the console was scrolled away from the bottom.</summary>
    private int _outputPending;

    /// <summary>
    /// Whether console lines wrap, bound by every line's template.
    /// </summary>
    /// <remarks>
    /// A dependency property rather than a field so the setting reaches containers the virtualizing
    /// panel has not created yet — a wrapped console that is scrolled after the toggle would
    /// otherwise realise its remaining lines unwrapped.
    /// </remarks>
    public static readonly DependencyProperty OutputWrappingProperty =
        DependencyProperty.Register(
            nameof(OutputWrapping),
            typeof(TextWrapping),
            typeof(MainWindow),
            new PropertyMetadata(TextWrapping.NoWrap));

    public TextWrapping OutputWrapping
    {
        get => (TextWrapping)GetValue(OutputWrappingProperty);
        set => SetValue(OutputWrappingProperty, value);
    }

    private static readonly Brush LightEditorBg = Brush(0xF4, 0xF7, 0xFB);
    private static readonly Brush LightEditorFg = Brush(0x24, 0x28, 0x33);
    private static readonly Brush LightLineNumbers = Brush(0x8C, 0x96, 0xA5);
    private static readonly Brush DarkEditorBg = Brush(0x1F, 0x1D, 0x29);
    private static readonly Brush DarkEditorFg = Brush(0xE5, 0xE1, 0xEC);
    private static readonly Brush DarkLineNumbers = Brush(0x7F, 0x78, 0x8E);

    private static readonly Regex NameDirective =
        new(@"^///\s*@name[^\S\n]+(?<name>\S.*?)[^\S\n]*$", RegexOptions.Multiline | RegexOptions.Compiled);

    private ScriptTab? Active => TabList.SelectedItem as ScriptTab;

    public MainWindow()
        : this(new GEarthOptions
        {
            Title = "QX Scripter",
            Author = "QDave",
            Description = "C# scripting console for Habbo",
            OnClickUsed = true,
            Port = 9092,
            SearchPorts = true
        }, false)
    {
    }

    internal MainWindow(GEarthOptions gearth_options, bool launched_by_gearth)
    {
        _gearth_options = gearth_options ?? throw new ArgumentNullException(nameof(gearth_options));
        _launched_by_gearth = launched_by_gearth;
        InitializeComponent();
        _ui_tasks = new UiTaskScope(Dispatcher, "host", _cts.Token);
        Qx.Diagnostics.Diag.Emitted += OnDiagnostic;
        Directory.CreateDirectory(_scriptsDir);
        TabList.ItemsSource = _tabs;
        ApiLibrary.Picked += InsertFromApi;
        ApiLibrary.Closed += () => ToggleApiBrowser(this, new RoutedEventArgs());
        Panel.RunRequested += OnPanelRun;
        Panel.OutputCleared += OnPanelOutputCleared;
        Home.Library = _library;
        Home.OpenRequested += OpenScriptFile;
        Home.NewRequested += () => NewTab(this, new RoutedEventArgs());
        Home.DeleteRequested += DeleteScriptFile;
        Home.RenameRequested += RenameScriptFile;
        Home.DuplicateRequested += DuplicateScriptFile;
        Home.RevealRequested += RevealScriptFile;
        Home.DismissRequested += LeaveHome;
        RefreshThemeButton();
        ApplyOutputWrap();
        RestoreWindowPlacement();
        _hosted_restore_state = WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        Topmost = App.Settings.Topmost;
        RefreshTopmostButton();
        ShowHome();
        // Before Loaded: a session restored into a maximized window is sized from this message, and
        // that happens while the handle is being made — by Loaded the wrong size is already set.
        SourceInitialized += (_, _) => HookWindowMessages();
        WindowCorners.Round(this);
        Loaded += OnLoaded;
        Activated += (_, _) => TryShowUpdateNotice();
        PreviewKeyDown += OnPreviewKeyDown;
        StateChanged += (_, _) => RefreshWindowState();
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QX Scripter", "logs", "qx.log");
    private static readonly object LogSync = new();
    private static int _logging_started;

    /// <summary>
    /// Mirrors warnings and errors to a file. Release builds compile out the trace and debug
    /// calls but keep info upwards, and without a sink they go nowhere - which leaves no trace of
    /// a failure that only reproduces against a live hotel.
    /// </summary>
    private static void StartLogging()
    {
        Qx.Diagnostics.Diag.Enabled = true;
        Qx.Diagnostics.Diag.MinLevel = Qx.Diagnostics.DiagLevel.Info;
        // Loaded can be raised more than once for a window; subscribing again would duplicate
        // every line in the log.
        if (Interlocked.Exchange(ref _logging_started, 1) == 1)
            return;

        try
        {
            string? dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            // Keep one previous run for comparison rather than growing without bound.
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 4 * 1024 * 1024)
                File.Move(LogPath, LogPath + ".1", true);
        }
        catch
        {
            return;
        }

        Qx.Diagnostics.Diag.Emitted += (level, message, category) =>
        {
            string tag = category is null ? "" : category + " ";
            try
            {
                lock (LogSync)
                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {tag}{message}{Environment.NewLine}");
            }
            catch
            {
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartLogging();
        RegisterPanicHotkey();
        string? editorError = null;
        try
        {
            _roslynHost = new QxRoslynHost();
        }
        catch (Exception ex)
        {
            _roslynHost = null;
            editorError = ex.Message;
        }

        RestoreSession();
        // After the session, so restored drafts land beside the saved scripts rather than being
        // pushed aside by them, and end up as the tabs in front.
        StartDrafts();

        if (Active is { } tab)
            ShowEditor(tab);

        _gearthPort = _gearth_options.Port;
        _runtime = new RuntimeHost(new RuntimeHostOptions
        {
            GEarth = _gearth_options,
            ScriptsDirectory = _scriptsDir,
            ReconnectTransport = !_launched_by_gearth
        }, this, () => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
        _messages = _runtime.Messages;
        _extension = _runtime.Extension;
        _game = _runtime.Game;
        _script_execution = _runtime.ScriptExecution;
        _mcp = _runtime.Mcp;

        _status = new StatusBarModel(
            _game,
            _extension,
            _runtime.Application,
            _ui_tasks.Factory,
            _cts.Token);
        StatusRoot.DataContext = _status;

        // The rail's counts come from the same model as the bar's, so they cannot disagree.
        RoomButtons.DataContext = _status;
        GivePagesState();

        GiveToolPagesState();

        if (editorError is not null)
            Qx.Diagnostics.Diag.Warn(
                $"Editor code-intelligence unavailable ({editorError}); scripts still run.",
                "editor");

        _extension.Connected += session =>
        {
            _status?.Refresh();
            // Pushed onto the window so it reaches every control by inheritance. Anything marked
            // with a client it does not work on disables itself from here.
            HabboImages.WebHost = Qx.Game.GameData.WebHostFor(session.Host);
            Dispatch(() => ClientCapability.SetClient(this, session.Client));
        };
        _extension.Disconnected += () =>
        {
            _status?.Refresh();
            Dispatch(() => ClientCapability.SetClient(this, ClientType.None));
        };
        _extension.InterceptorConnected += () => _status?.Refresh();
        _extension.InterceptorDisconnected += OnGEarthLost;
        _extension.InterceptFailed += ReportInterceptFailure;
        _extension.Activated += () => Dispatch(() =>
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = _hosted_restore_state;
            Activate();
        });

        WatchRuntime();
        StartUpdateCheck();
        if (_launched_by_gearth)
            Observe(CloseIfGEarthUnavailableAsync);
        Qx.Diagnostics.Diag.Info("Application initialized", "app");
    }

    private const int WmMouseActivate = 0x0021;

    /// <summary>Discard the click and do not activate — the Win32 answer for "eat this press".</summary>
    private const int MaNoActivateAndEat = 4;

    /// <summary>
    /// Eats the click that dismisses the command palette.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows asks an inactive window what to do with a press <em>before</em> delivering it, which
    /// is the only point where the press can still be stopped. Answering
    /// <see cref="MaNoActivateAndEat"/> discards it outright.
    /// </para>
    /// <para>
    /// The previous attempt closed the palette on deactivation and tried to recognise the press
    /// afterwards. That raced: activation and the press are separate messages, so the press
    /// sometimes arrived first and landed in the editor before anything knew a palette had been
    /// open. Clicking away moved the caret, or worse, pressed whatever was under the pointer.
    /// </para>
    /// </remarks>
    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        // Windows announces how big maximized will be rather than asking, and for a borderless
        // window its answer is the whole monitor — which puts the status bar under the taskbar.
        if (message == WorkAreaMaximize.WmGetMinMaxInfo)
        {
            WorkAreaMaximize.Apply(hwnd, l);
            handled = true;
            return IntPtr.Zero;
        }

        if (message != WmMouseActivate || !CommandPalette.IsOpen)
            return IntPtr.Zero;

        CommandPalette.DismissOpen();
        handled = true;

        // Not activated by the click, so the owner is asked for afterwards rather than during, when
        // the palette it belongs to is still closing.
        _ui_tasks.Post(() =>
        {
            Activate();
        }, DispatcherPriority.Input);
        return new IntPtr(MaNoActivateAndEat);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            ToggleRun(this, e);
        }
        else if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ShowCommandPalette();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SaveScript(this, e);
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            SaveScriptAs(this, e);
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            NewTab(this, e);
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            OpenScript(this, e);
        }
        else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            if (Active is { } tab)
                CloseTab(tab);
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ReopenClosedTab();
        }
        else if (e.Key == Key.J && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ToggleOutput(this, e);
        }
        else if (e.Key == Key.D1 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SelectCodeMode(this, e);
        }
        else if (e.Key == Key.D2 && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SelectPanelMode(this, e);
        }
        else if (e.Key == Key.Tab && Keyboard.Modifiers is ModifierKeys.Control or (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            SelectAdjacentTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        }
        // Only when the search box has not already claimed it: inside the browser, Escape clears
        // a search first and leaves on the second press.
        else if (e.Key == Key.Escape && Room.Visibility == Visibility.Visible && !Room.IsSearching)
        {
            e.Handled = true;
            LeaveRoom();
        }
        else if (e.Key == Key.F1)
        {
            e.Handled = true;
            GoTo(NavPage.Settings);
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            ToggleApiBrowser(this, e);
        }
        else if (e.Key == Key.F8)
        {
            e.Handled = true;
            GoToError(this, e);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control &&
                 e.Key is Key.OemPlus or Key.Add or Key.OemMinus or Key.Subtract or Key.D0 or Key.NumPad0)
        {
            e.Handled = true;
            ZoomEditor(e.Key switch
            {
                Key.OemPlus or Key.Add => App.Settings.EditorFontSize + 1,
                Key.OemMinus or Key.Subtract => App.Settings.EditorFontSize - 1,
                _ => UiSettings.DefaultEditorFontSize
            });
        }
    }

    /// <summary>
    /// Opens or closes the API library beside the editor.
    /// </summary>
    /// <remarks>
    /// The editor keeps the space it had when the library is away, so the split only costs
    /// anything while it is actually being read. The width is remembered for the session, because
    /// dragging the splitter back to where it was on every open would be tedious.
    /// </remarks>
    private void ToggleApiBrowser(object sender, RoutedEventArgs e)
    {
        bool opening = ApiPane.Visibility != Visibility.Visible;
        ApiPane.Visibility = opening ? Visibility.Visible : Visibility.Collapsed;
        ApiSplitter.Visibility = ApiPane.Visibility;

        // The library carries its own close, so the button that opened it would only sit on top of
        // the code saying something that is already on screen.
        ApiButton.Visibility = opening ? Visibility.Collapsed : Visibility.Visible;
        ApiColumn.Width = opening ? new GridLength(_api_width, GridUnitType.Pixel) : new GridLength(0);
        ApiColumn.MinWidth = opening ? 320 : 0;

        if (opening)
        {
            ApiLibrary.TakeFocus();
            return;
        }

        _api_width = Math.Max(320, ApiColumn.ActualWidth);
        Active?.Editor?.Focus();
    }

    private double _api_width = 470;

    /// <summary>
    /// Writes a picked member where the caret is.
    /// </summary>
    /// <remarks>
    /// Into the document rather than onto the clipboard, and the caret is left where the arguments
    /// go, so a call can be finished without reaching for the mouse again. The library stays open:
    /// building a line usually takes more than one member.
    /// </remarks>
    private void InsertFromApi(string text, int caretOffset)
    {
        if (Active?.Editor is not { } editor)
            return;

        int at = editor.CaretOffset;
        editor.Document.Insert(at, text);
        editor.CaretOffset = at + Math.Clamp(caretOffset, 0, text.Length);
        editor.TextArea.Focus();
    }

    /// <summary>
    /// Walks the errors a run reported. Compile diagnostics and runtime exceptions both carry a
    /// line, which until now was only readable as text in the output.
    /// </summary>
    private void GoToError(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;

        ScriptExecutionError[] located = tab.Errors.Where(error => error.Line is > 0).ToArray();
        if (located.Length == 0)
            return;

        ScriptExecutionError target = located[_errorIndex % located.Length];
        _errorIndex = (_errorIndex + 1) % located.Length;

        if (tab.PanelMode)
            ShowCode();
        if (tab.Editor is not { } editor || !tab.EditorInitialized)
            return;

        int line = Math.Clamp(target.Line!.Value, 1, Math.Max(1, editor.Document.LineCount));
        editor.TextArea.Caret.Line = line;
        editor.TextArea.Caret.Column = Math.Max(1, target.Column ?? 1);
        editor.ScrollToLine(line);
        editor.TextArea.Focus();
    }

    private void RefreshErrorAction(ScriptTab? tab) =>
        GoToErrorButton.Visibility = tab is not null && tab.Errors.Any(error => error.Line is > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

    /// <summary>Ctrl and the wheel sizes the editor text, as it does in every other editor.</summary>
    private void OnEditorMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;
        ZoomEditor(App.Settings.EditorFontSize + (e.Delta > 0 ? 1 : -1));
    }

    private void ZoomEditor(double size)
    {
        App.Settings.EditorFontSize = size;
        ApplyEditorFontSize();
    }

    private void ApplyEditorFontSize()
    {
        double size = App.Settings.EditorFontSize;
        foreach (ScriptTab tab in _tabs)
        {
            if (tab.Editor is not null)
                tab.Editor.FontSize = size;
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        // Anything that is not a dropped script file is left alone — a tab being dragged along the
        // strip to reorder it also raises this, and claiming it would refuse the drop.
        if (ScriptPathsIn(e.Data).Count == 0)
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        IReadOnlyList<string> paths = ScriptPathsIn(e.Data);
        if (paths.Count == 0)
            return;

        e.Handled = true;
        foreach (string path in paths)
            OpenScriptFile(path);
    }

    private static IReadOnlyList<string> ScriptPathsIn(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(file =>
                file.EndsWith(ScriptName.Extension, StringComparison.OrdinalIgnoreCase) && File.Exists(file)).ToArray()
            : [];

    private ScriptTab AddTab(string name, string code, string? path)
    {
        var tab = new ScriptTab { Name = name, FilePath = path, Code = code };

        // Before it is added, because adding it selects it and the selection is what decides which
        // view the tab opens in and what the panel is restored from.
        if (path is not null)
            RestorePanelMemory(tab, path);
        tab.PropertyChanged += OnTabStateChanged;
        _tabs.Add(tab);
        HideHome();
        TabList.SelectedItem = tab;

        // Opening a script is a move to the editor like any other, so it goes through the one place
        // that decides what is in front. Left out, the page in front and the button lit for it would
        // disagree: the editor would be on screen while the rail still pointed at the room.
        GoTo(NavPage.Editor);
        return tab;
    }

    /// <summary>
    /// Reopens a script the way it was left: in the panel, holding what was typed into it.
    /// </summary>
    /// <remarks>
    /// The mode is only honoured while the script still declares a panel. A script whose directives
    /// were deleted between sessions would otherwise open into an empty panel with no way back but
    /// the toggle, which is disabled for exactly that script.
    /// </remarks>
    /// <param name="tab">The tab being opened.</param>
    /// <param name="path">The file it was opened from.</param>
    private static void RestorePanelMemory(ScriptTab tab, string path)
    {
        if (App.Settings.PanelFor(path) is not { } memory)
            return;

        if (memory.Values is { Count: > 0 })
            tab.PanelState.SaveValues(memory.Values);
        tab.PanelMode = memory.Panel && UiSpec.Parse(tab.Code).HasUi;
    }

    /// <summary>
    /// Writes a tab's view mode and panel values to settings, keyed by the file behind it.
    /// </summary>
    /// <param name="tab">The tab to remember. One with no file has nothing to key on.</param>
    private static void RememberPanel(ScriptTab tab)
    {
        if (tab.FilePath is { } path)
            App.Settings.RememberPanel(path, tab.PanelMode, tab.PanelState.Values);
    }

    private void ShowHome()
    {
        _homeReturnTab = Active;
        SaveVisiblePanelState();
        TabList.SelectedItem = null;
        GoTo(NavPage.Library);
    }

    /// <summary>
    /// Puts the room browser over the editor, the way the library goes over it.
    /// </summary>
    /// <remarks>
    /// The console is left where it was rather than hidden. A script running against the room is
    /// the usual reason to be looking at the room, and losing its output the moment you check what
    /// is in there would be the wrong trade.
    /// </remarks>
    private void ShowRoom(RoomSection section)
    {
        GoTo(NavPage.Room);
        Room.Open(section);
    }

    private void LeaveRoom() => GoTo(NavPage.Editor);

    /// <summary>Shows the room buttons only while there is a room behind them.</summary>
    private void HideHome()
    {
        WorkspaceActions.Visibility = Visibility.Visible;
        LibraryButton.IsChecked = false;
        if (Home.Visibility != Visibility.Visible)
        {
            ApplyConsoleVisibility();
            return;
        }
        Home.Visibility = Visibility.Collapsed;
        CodeArea.Visibility = Visibility.Visible;
        ApplyConsoleVisibility();
    }

    /// <summary>What the library marks as running: only tabs that are actually doing something.</summary>
    private HashSet<string> RunningFilePaths() =>
        _tabs.Where(t => t.IsWorking && t.FilePath is not null)
            .Select(t => t.FilePath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What still has a run behind it, armed panels included.
    /// </summary>
    /// <remarks>
    /// Deleting the file under a live run is refused whether or not the run is busy, which is not
    /// the same question as what the library shows a spinner for.
    /// </remarks>
    private HashSet<string> LiveFilePaths() =>
        _tabs.Where(t => t.IsAlive && t.FilePath is not null)
            .Select(t => t.FilePath!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void TabSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.OfType<ScriptTab>().FirstOrDefault() is { OutputCollapsed: false } previous &&
            OutputShell.Visibility == Visibility.Visible &&
            OutputRow.ActualHeight > 32)
            previous.OutputHeight = OutputRow.ActualHeight;

        RefreshTitle();

        if (Active is not { } tab)
            return;
        HideHome();
        ShowEditor(tab);
        if (tab.PanelMode)
            ShowPanel(tab);
        else
            ShowCode();
        ApplyOutputState(tab);
        RefreshErrorAction(tab);
        _errorIndex = 0;
    }

    private void SelectAdjacentTab(int offset)
    {
        if (_tabs.Count < 2)
            return;
        int current = Math.Max(0, TabList.SelectedIndex);
        TabList.SelectedIndex = (current + offset + _tabs.Count) % _tabs.Count;
    }

    private void OnTabStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ScriptTab tab)
            return;

        Dispatch(() =>
        {
            if (e.PropertyName == nameof(ScriptTab.IsWorking) && Home.Visibility == Visibility.Visible)
                Home.RefreshRunning(RunningFilePaths());

            // The status bar's count is over every tab, not the selected one, so it is refreshed
            // before the early return below.
            if (e.PropertyName == nameof(ScriptTab.IsAlive))
                RefreshRunningCount();

            if (tab != Active)
                return;

            RefreshTitle();
            RefreshErrorAction(tab);
            if (IsVisiblePanel(tab))
                Panel.SetBusy(PanelBusy(tab));
            if (tab.IsFaulted)
            {
                tab.OutputCollapsed = false;
                ApplyOutputState(tab);
            }
        });
    }

    /// <summary>Names the open script in the taskbar and in Alt+Tab, unsaved ones with a dot.</summary>
    private void RefreshTitle() =>
        Title = Active is { } tab
            ? $"{(tab.IsModified ? "● " : "")}{tab.Name} - QX Scripter"
            : "QX Scripter";

    private void RefreshViewAvailability(ScriptTab tab)
    {
        bool hasUi = UiSpec.Parse(tab.CurrentCode).HasUi;
        PanelToggle.IsEnabled = hasUi;
        PanelToggle.ToolTip = hasUi
            ? "UI view (Ctrl+2)"
            : "No UI directives in this script";

        if (!hasUi && tab.PanelMode)
        {
            tab.PanelMode = false;
            ShowCode();
            RememberPanel(tab);
        }
    }

    private void SelectCodeMode(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;
        tab.PanelMode = false;
        ShowCode();
        RememberPanel(tab);
    }

    private void SelectPanelMode(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;

        UiSpec spec = UiSpec.Parse(tab.CurrentCode);
        if (!spec.HasUi)
        {
            CodeToggle.IsChecked = true;
            PanelToggle.IsChecked = false;
            return;
        }

        tab.PanelMode = true;
        ShowPanel(tab);
        RememberPanel(tab);
    }

    private ScriptTab? _panelTab;
    private string? _panelDirectives;

    private void ShowPanel(ScriptTab tab)
    {
        SaveVisiblePanelState();

        // Rebuilt whenever the directives themselves changed, so editing the panel and switching
        // back shows the edit. Comparing the directive lines rather than the whole file keeps a
        // rebuild off the path when only the script body was touched, which would otherwise throw
        // away what the user had typed into the controls.
        string directives = PanelDirectives(tab.CurrentCode);
        if (_panelTab != tab || _panelDirectives != directives || Panel.Visibility != Visibility.Visible)
        {
            Panel.Build(UiSpec.Parse(tab.CurrentCode));
            Panel.Restore(tab.PanelState.Values, tab.PanelState.OutputValues());
            _panelTab = tab;
            _panelDirectives = directives;
        }
        CodeArea.Visibility = Visibility.Collapsed;
        Panel.Visibility = Visibility.Visible;
        Panel.SetBusy(PanelBusy(tab));
        CodeToggle.IsChecked = false;
        PanelToggle.IsChecked = true;
        ApplyConsoleVisibility();
    }

    private void ShowCode()
    {
        SaveVisiblePanelState();
        Panel.Visibility = Visibility.Collapsed;
        CodeArea.Visibility = Visibility.Visible;
        CodeToggle.IsChecked = true;
        PanelToggle.IsChecked = false;
        ApplyConsoleVisibility();
    }

    /// <summary>
    /// The script's <c>//@ui:</c> lines, joined, for telling one panel layout from another.
    /// </summary>
    /// <param name="code">The script.</param>
    private static string PanelDirectives(string code)
    {
        var lines = new List<string>();
        foreach (string line in code.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) &&
                trimmed.Contains("@ui:", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(trimmed);
            }
        }
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Works out which output box a script meant.
    /// </summary>
    /// <remarks>
    /// An empty name falls back to the first box the panel declared, so a one-box panel can write
    /// with <c>Ui.Log("", …)</c>. A name that was written but matches nothing resolves to nothing:
    /// sending it to the first box put a mistyped box name's lines somewhere plausible but wrong,
    /// and a mistyped <c>Ui.Clear</c> emptied a box the script never meant to touch. Every other
    /// writer already ignores a name it does not know, so this now agrees with them.
    /// </remarks>
    /// <param name="box">The name the script used.</param>
    /// <param name="known">The declared box names.</param>
    /// <param name="fallback">The first declared box, or null when the panel declares none.</param>
    /// <returns>The box to write to, or an empty string when there is nowhere to put it.</returns>
    private static string ResolveOutput(
        string box,
        IReadOnlyDictionary<string, string> known,
        string? fallback)
    {
        if (box.Length == 0)
            return fallback ?? "";
        return known.TryGetValue(box, out string? name) ? name : "";
    }

    /// <summary>
    /// Hides the console while a panel is shown.
    /// </summary>
    /// <remarks>
    /// A panel says where its output goes; the console underneath it did not belong to it and only
    /// competed with it for room. It comes back with the code view, which is where it belongs.
    /// </remarks>
    private void ApplyConsoleVisibility()
    {
        bool panelShown = Panel.Visibility == Visibility.Visible;
        bool home = Home.Visibility == Visibility.Visible;
        Visibility wanted = panelShown || home ? Visibility.Collapsed : Visibility.Visible;
        OutputShell.Visibility = wanted;
        OutputSplitter.Visibility = wanted;
    }

    private bool IsVisiblePanel(ScriptTab tab) =>
        ReferenceEquals(tab, Active) &&
        ReferenceEquals(tab, _panelTab) &&
        tab.PanelMode &&
        Panel.Visibility == Visibility.Visible;

    /// <summary>
    /// Whether the panel's own buttons have to be off.
    /// </summary>
    /// <remarks>
    /// Only while a run is on its way up, when there is nothing behind the buttons yet. A run that
    /// stayed alive to answer them is the opposite case: disabling them there would leave the panel
    /// it just drew dead in the moment it became usable.
    /// </remarks>
    private bool PanelBusy(ScriptTab tab) => tab.IsAlive && !_panelRuns.ContainsKey(tab);

    private void SaveVisiblePanelState()
    {
        if (_panelTab is not { } tab || Panel.Visibility != Visibility.Visible)
            return;
        tab.PanelState.SaveValues(Panel.Values());
        RememberPanel(tab);
    }

    private void ApplyOutputState(ScriptTab tab)
    {
        // Runs on every tab switch and at the start of every run, both of which happen while a
        // panel may be open. Showing the console unconditionally here undid the panel's claim on
        // the space each time, so the decision is left to the one place that knows.
        ApplyConsoleVisibility();
        if (OutputShell.Visibility != Visibility.Visible)
            return;

        if (tab.OutputCollapsed)
        {
            OutputRow.Height = new GridLength(32);
            OutputBodyRow.Height = new GridLength(0);
            OutputSplitter.Visibility = Visibility.Collapsed;
            OutputToggleIcon.Kind = PackIconKind.ChevronUp;
            OutputToggleButton.ToolTip = "Expand output (Ctrl+J)";
            AutomationProperties.SetName(OutputToggleButton, "Expand output");
            return;
        }

        OutputRow.Height = new GridLength(tab.OutputHeight);
        OutputBodyRow.Height = new GridLength(1, GridUnitType.Star);
        OutputSplitter.Visibility = Visibility.Visible;
        OutputToggleIcon.Kind = PackIconKind.ChevronDown;
        OutputToggleButton.ToolTip = "Collapse output (Ctrl+J)";
        AutomationProperties.SetName(OutputToggleButton, "Collapse output");
    }

    private void ShowEditor(ScriptTab tab)
    {
        if (_roslynHost is null)
            return;
        EnsureEditor(tab);
        EditorHost.Content = tab.Editor;
        RefreshOutput(tab);
        RefreshViewAvailability(tab);
        ApplyOutputState(tab);
    }

    private void EnsureEditor(ScriptTab tab)
    {
        if (tab.Editor is not null)
            return;

        bool dark = App.Theme.IsDark;
        var editor = new RoslynCodeEditor
        {
            FontFamily = new FontFamily("Cascadia Code, Cascadia Mono, Consolas"),
            FontSize = App.Settings.EditorFontSize,
            Background = dark ? DarkEditorBg : LightEditorBg,
            Foreground = dark ? DarkEditorFg : LightEditorFg,
            LineNumbersForeground = dark ? DarkLineNumbers : LightLineNumbers,
            Padding = new Thickness(10, 10, 12, 10),
            ShowLineNumbers = true,
            WordWrap = App.Settings.EditorWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        foreach (var margin in editor.TextArea.LeftMargins.OfType<ICSharpCode.AvalonEdit.Editing.LineNumberMargin>())
            margin.Margin = new Thickness(10, 0, 2, 0);
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Options.AllowScrollBelowDocument = true;
        editor.Options.HighlightCurrentLine = false;
        editor.TextArea.SelectionCornerRadius = 2;
        editor.TextArea.SelectionBorder = null;
        editor.PreviewMouseWheel += OnEditorMouseWheel;

        bool initialized = false;
        editor.Loaded += EditorLoaded;

        void EditorLoaded(object sender, RoutedEventArgs e)
        {
            if (initialized)
                return;
            initialized = true;
            Observe(async () =>
            {
                var colors = dark ? new DarkEditorColors() : new ClassificationHighlightColors();
                try
                {
                    await editor.InitializeAsync(_roslynHost!, colors, _scriptsDir, tab.Code, SourceCodeKind.Script);
                    if (!ReferenceEquals(tab.Editor, editor))
                        return;

                    tab.EditorInitialized = true;
                    RemoveFolding(editor);
                    editor.TextChanged += (_, _) =>
                    {
                        tab.IsModified = true;
                        UpdateTabName(tab);
                        RemoveFolding(editor);
                        if (tab == Active)
                            RefreshViewAvailability(tab);
                    };
                    if (tab == Active)
                        RefreshViewAvailability(tab);
                }
                catch (Exception error)
                {
                    if (ReferenceEquals(tab.Editor, editor))
                        AppendOutput(tab, $"Editor initialization failed: {error.Message}", OutputLevel.Error);
                }
            });
        }

        tab.EditorInitialized = false;
        tab.Editor = editor;
    }

    private static void RemoveFolding(RoslynCodeEditor editor)
    {
        for (int i = editor.TextArea.LeftMargins.Count - 1; i >= 0; i--)
            if (editor.TextArea.LeftMargins[i] is ICSharpCode.AvalonEdit.Folding.FoldingMargin)
                editor.TextArea.LeftMargins.RemoveAt(i);
    }

    private static void UpdateTabName(ScriptTab tab)
    {
        Match match = NameDirective.Match(tab.CurrentCode);
        if (match.Success)
            tab.Name = match.Groups["name"].Value.Trim();
        else if (tab.FilePath is { } path)
            tab.Name = Path.GetFileNameWithoutExtension(path);
    }

    private void NewTab(object sender, RoutedEventArgs e) => AddTab(NextUntitledName(), "", null);

    private void StopOrCloseTab(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ScriptTab tab)
            return;

        if (tab.IsWorking)
            RequestStop(tab);
        else
            CloseTab(tab);
    }

    private void CloseTab(ScriptTab tab)
    {
        if (tab.IsWorking)
        {
            RequestStop(tab);
            return;
        }

        if (!CanDiscard(tab))
            return;
        RemoveTab(tab);
    }

    private void RemoveTab(ScriptTab tab)
    {
        if (!_tabs.Contains(tab))
            return;
        if (tab.Cts is { } cancellation)
            Observe(() => cancellation.CancelAsync());
        if (ReferenceEquals(_panelTab, tab))
        {
            // Closing a tab is the most ordinary way of leaving a panel, so what it held is written
            // out here rather than only when the window closes.
            SaveVisiblePanelState();
            _panelTab = null;
        }
        RememberPanel(tab);
        tab.PropertyChanged -= OnTabStateChanged;
        RememberClosed(tab);
        _tabs.Remove(tab);
        RefreshRunningCount();
        if (_tabs.Count == 0)
            ShowHome();
        else if (Active is null)
            TabList.SelectedIndex = _tabs.Count - 1;
    }

    /// <summary>
    /// Scripts closed this session, most recent last.
    /// </summary>
    /// <remarks>
    /// Paths, not tabs: reopening reads the file again, so what was closed comes back as it is on
    /// disk rather than as a buffer held alive after the user asked for it to go. Only saved
    /// scripts qualify — an unsaved buffer has nothing to reopen from.
    /// </remarks>
    private readonly List<string> _closedScripts = [];

    private const int MaxClosedRemembered = 20;

    private void RememberClosed(ScriptTab tab)
    {
        if (tab.FilePath is not { } path)
            return;

        _closedScripts.RemoveAll(other => string.Equals(other, path, StringComparison.OrdinalIgnoreCase));
        _closedScripts.Add(path);
        if (_closedScripts.Count > MaxClosedRemembered)
            _closedScripts.RemoveAt(0);
    }

    /// <summary>
    /// Reopens the most recently closed script that is still there to reopen.
    /// </summary>
    /// <remarks>
    /// Entries are discarded as they are found wanting rather than checked up front, so a script
    /// deleted or already reopened by hand costs one press and not a dead shortcut.
    /// </remarks>
    private void ReopenClosedTab()
    {
        while (_closedScripts.Count > 0)
        {
            string path = _closedScripts[^1];
            _closedScripts.RemoveAt(_closedScripts.Count - 1);

            if (!File.Exists(path))
                continue;
            if (_tabs.Any(tab => string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            OpenScriptFile(path);
            return;
        }
    }

    private Point _dragStart;
    private ScriptTab? _dragTab;

    private void TabStripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && (e.OriginalSource as FrameworkElement)?.DataContext is ScriptTab middle)
        {
            e.Handled = true;
            CloseTab(middle);
            return;
        }

        if (e.ChangedButton == MouseButton.Left && (e.OriginalSource as FrameworkElement)?.DataContext is ScriptTab tab)
        {
            _dragStart = e.GetPosition(TabList);
            _dragTab = tab;
        }
    }

    private void TabStripMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point pos = e.GetPosition(TabList);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance)
            return;

        ScriptTab dragged = _dragTab;
        _dragTab = null;
        DragDrop.DoDragDrop(TabList, dragged, DragDropEffects.Move);
    }

    private void TabDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ScriptTab)) is not ScriptTab dragged)
            return;
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not ScriptTab target || target == dragged)
            return;

        int from = _tabs.IndexOf(dragged);
        int to = _tabs.IndexOf(target);
        if (from >= 0 && to >= 0)
        {
            _tabs.Move(from, to);
            TabList.SelectedItem = dragged;
        }
    }

    private static ScriptTab? MenuTab(object sender) =>
        ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget is FrameworkElement target
            ? target.DataContext as ScriptTab
            : null;

    private void CtxClose(object sender, RoutedEventArgs e)
    {
        if (MenuTab(sender) is { } tab)
            CloseTab(tab);
    }

    private void CtxCloseOthers(object sender, RoutedEventArgs e)
    {
        if (MenuTab(sender) is not { } keep)
            return;
        ScriptTab[] others = _tabs.Where(tab => tab != keep).ToArray();
        int modified = others.Count(tab => tab.IsModified);
        if (modified > 0 && !ConfirmDialog.Ask(
                this,
                "Close other scripts?",
                modified == 1
                    ? "One script has unsaved changes. Close it and discard those changes?"
                    : $"{modified} scripts have unsaved changes. Close them and discard those changes?",
                "Discard and close"))
            return;

        Observe(() => CloseOtherTabsAsync(keep, others));
    }

    private async Task CloseOtherTabsAsync(ScriptTab keep, ScriptTab[] others)
    {
        foreach (ScriptTab other in others)
        {
            if (other.IsWorking)
            {
                RequestStop(other);
                continue;
            }

            RemoveTab(other);
        }

        foreach (ScriptTab other in others.Where(tab => tab.IsWorking))
        {
            if (other.ExecutionTask is { } execution)
            {
                try
                {
                    await execution;
                }
                catch
                {
                }
            }

            if (!other.IsWorking)
                RemoveTab(other);
        }

        if (_tabs.Contains(keep))
            TabList.SelectedItem = keep;
    }

    private void CtxRename(object sender, RoutedEventArgs e)
    {
        if (MenuTab(sender) is not { } tab)
            return;
        if (RenameDialog.Ask(this, tab.Name) is not { } typed || string.IsNullOrWhiteSpace(typed))
            return;

        // A tab with no file behind it is only a label, so nothing touches the disk.
        if (tab.FilePath is null)
        {
            tab.Name = typed.Trim();
            tab.IsModified = true;
            return;
        }

        RenameScriptFile(tab.FilePath, typed);
    }

    /// <summary>Both only apply to a tab that has a file behind it; an unsaved buffer has nothing to act on.</summary>
    private void CtxDuplicate(object sender, RoutedEventArgs e)
    {
        if (MenuTab(sender) is { FilePath: { } path })
            DuplicateScriptFile(path);
    }

    private void CtxReveal(object sender, RoutedEventArgs e)
    {
        if (MenuTab(sender) is { FilePath: { } path })
            RevealScriptFile(path);
    }

    /// <summary>Asks for a new name for a saved script, from the library rather than from a tab.</summary>
    private void RenameScriptFile(string path)
    {
        if (!File.Exists(path))
            return;
        if (RenameDialog.Ask(this, Path.GetFileNameWithoutExtension(path)) is not { } typed ||
            string.IsNullOrWhiteSpace(typed))
            return;

        RenameScriptFile(path, typed);
    }

    /// <summary>
    /// Renames a script on disk and carries everything keyed to its name across: the library's
    /// group and icon, and any tab holding the file open.
    /// </summary>
    private bool RenameScriptFile(string path, string typed)
    {
        string directory = Path.GetDirectoryName(path)!;
        string nextPath = ScriptName.PathIn(directory, typed);

        if (string.Equals(path, nextPath, StringComparison.Ordinal))
            return false;

        // Differing only in case is still a rename; Windows performs it, but File.Exists sees the
        // source file at the destination, so it must not be mistaken for a collision.
        bool caseOnly = string.Equals(path, nextPath, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && File.Exists(nextPath) && !ConfirmDialog.Ask(
                this,
                "Replace existing script?",
                $"A script named “{Path.GetFileNameWithoutExtension(nextPath)}” already exists.",
                "Replace"))
            return false;

        string previous = Path.GetFileNameWithoutExtension(path);
        try
        {
            if (caseOnly)
                File.Move(path, nextPath);
            else
                File.Move(path, nextPath, true);
        }
        catch (Exception error)
        {
            ConfirmDialog.Alert(this, "Rename failed", error.Message);
            return false;
        }

        string renamed = Path.GetFileNameWithoutExtension(nextPath);
        _library.Rename(previous, renamed);

        if (_tabs.FirstOrDefault(tab => string.Equals(tab.FilePath, path, StringComparison.Ordinal)) is { } open)
        {
            open.FilePath = nextPath;
            open.Name = renamed;
        }

        // The tab strip stays reachable while the library is on screen, so either route can be the
        // one that just moved a file out from under the list.
        if (Home.Visibility == Visibility.Visible)
            Home.Refresh(RunningFilePaths());

        return true;
    }

    private void OpenScript(object sender, RoutedEventArgs e) => GoTo(NavPage.Library);

    private void OpenScriptFile(string path)
    {
        if (!File.Exists(path))
            return;

        ScriptTab? existing = _tabs.FirstOrDefault(t => string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            HideHome();
            TabList.SelectedItem = existing;

            // Through the same door a new tab goes through. Opening one that was already open used
            // to stop here, which left the window still believing it was showing the library — and
            // the library is a page without a tab strip, so the strip stayed hidden and every tab
            // with it. It only happened when the script picked was one already open, which is what
            // made it look intermittent.
            GoTo(NavPage.Editor);
            return;
        }

        ScriptTab tab = AddTab(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path), path);
        tab.IsModified = false;
    }

    /// <summary>Copies a script beside itself, keeping its group and icon on the copy.</summary>
    private void DuplicateScriptFile(string path)
    {
        if (!File.Exists(path))
            return;

        string directory = Path.GetDirectoryName(path)!;
        string original = Path.GetFileNameWithoutExtension(path);
        string copy = NextCopyName(directory, original);

        try
        {
            File.Copy(path, Path.Combine(directory, copy + ScriptName.Extension));
        }
        catch (Exception error)
        {
            ConfirmDialog.Alert(this, "Duplicate failed", error.Message);
            return;
        }

        _library.Set(copy, _library.Get(original));
        Home.Refresh(RunningFilePaths());
    }

    private static string NextCopyName(string directory, string original)
    {
        string candidate = $"{original} copy";
        for (int index = 2; File.Exists(Path.Combine(directory, candidate + ScriptName.Extension)); index++)
            candidate = $"{original} copy {index}";
        return candidate;
    }

    private void RevealScriptFile(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception error)
        {
            ConfirmDialog.Alert(this, "Could not open the folder", error.Message);
        }
    }

    /// <summary>Leaves the library for the script it was opened from, when there is one.</summary>
    private void LeaveHome()
    {
        ScriptTab? target = _homeReturnTab is { } previous && _tabs.Contains(previous)
            ? previous
            : _tabs.LastOrDefault();

        if (target is not null)
            TabList.SelectedItem = target;
    }

    private void DeleteScriptFile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (LiveFilePaths().Contains(path))
        {
            // The library only hides Delete for what it shows as running, and an armed panel is not
            // that. Refusing without a word would look like the button did nothing.
            ConfirmDialog.Alert(
                this,
                "Script is still open",
                $"“{name}” has a run behind it. Stop it or close its tab before deleting the script.");
            return;
        }

        if (!ConfirmDialog.Ask(
                this,
                "Delete script?",
                $"“{name}” will be permanently deleted from the script library.",
                "Delete"))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception error)
        {
            ConfirmDialog.Alert(this, "Delete failed", error.Message);
            return;
        }

        _library.Remove(name);

        // An open tab keeps its buffer, but is no longer backed by a file.
        if (_tabs.FirstOrDefault(tab => string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase)) is { } orphaned)
        {
            orphaned.FilePath = null;
            orphaned.IsModified = true;
        }

        Home.Refresh(RunningFilePaths());
    }

    private void SaveScript(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;

        string path;
        if (tab.FilePath is { } saved)
        {
            path = saved;
        }
        else
        {
            if (AskScriptName(tab.Name) is not { } name)
                return;

            path = Path.Combine(_scriptsDir, name + ScriptName.Extension);
            if (File.Exists(path) && !ConfirmDialog.Ask(
                    this,
                    "Replace existing script?",
                    $"A script named “{Path.GetFileNameWithoutExtension(path)}” already exists.",
                    "Replace"))
                return;
        }

        WriteScript(tab, path);
    }

    /// <summary>Saves the open script under a new name and leaves the tab on the new file.</summary>
    private void SaveScriptAs(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;
        if (AskScriptName(tab.Name) is not { } name)
            return;

        string path = Path.Combine(_scriptsDir, name + ScriptName.Extension);
        if (string.Equals(path, tab.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            WriteScript(tab, path);
            return;
        }

        if (File.Exists(path) && !ConfirmDialog.Ask(
                this,
                "Replace existing script?",
                $"A script named “{Path.GetFileNameWithoutExtension(path)}” already exists.",
                "Replace"))
            return;

        // The copy starts out looking like the script it came from.
        if (tab.FilePath is { } source)
            _library.Set(name, _library.Get(Path.GetFileNameWithoutExtension(source)));

        if (WriteScript(tab, path) && Home.Visibility == Visibility.Visible)
            Home.Refresh(RunningFilePaths());
    }

    private bool WriteScript(ScriptTab tab, string path)
    {
        try
        {
            Directory.CreateDirectory(_scriptsDir);
            File.WriteAllText(path, tab.CurrentCode);
            tab.FilePath = path;
            tab.Name = Path.GetFileNameWithoutExtension(path);
            tab.IsModified = false;
            return true;
        }
        catch (Exception error)
        {
            AppendOutput(tab, $"Save failed: {error.Message}", OutputLevel.Error);
            return false;
        }
    }

    private string? AskScriptName(string current)
    {
        string? name = RenameDialog.Ask(this, current, "Save script", "Save", PackIconKind.ContentSaveOutline);
        return string.IsNullOrWhiteSpace(name) ? null : ScriptName.Normalize(name);
    }

    private string NextUntitledName()
    {
        for (int index = 1; ; index++)
        {
            string name = index == 1 ? "untitled" : $"untitled {index}";
            bool open = _tabs.Any(tab => string.Equals(tab.Name, name, StringComparison.OrdinalIgnoreCase));
            bool saved = File.Exists(ScriptName.PathIn(_scriptsDir, name));
            if (!open && !saved)
                return name;
        }
    }

    private bool CanDiscard(ScriptTab tab) =>
        !tab.IsModified ||
        ConfirmDialog.Ask(
            this,
            "Discard unsaved changes?",
            $"“{tab.Name}” has unsaved changes. Close it and discard those changes?",
            "Discard");

    private void ToggleRun(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;
        if (tab.IsAlive)
            RequestStop(tab);
        else
            StartRun(tab, null, tab.PanelMode);
    }

    /// <summary>
    /// Answers a press on the panel.
    /// </summary>
    /// <remarks>
    /// A run that is still alive answers its own buttons, so pressing one calls what the script
    /// registered for it instead of running the script again. Everything else — a button the script
    /// never claimed, and the stand-in Run button a panel without buttons gets — keeps the older
    /// meaning, which is what scripts written against <c>Ui.Clicked</c> still rely on.
    /// </remarks>
    /// <param name="button">The button pressed, or null for the stand-in Run button.</param>
    private void OnPanelRun(string? button)
    {
        if (Active is not { } tab)
            return;

        if (button is { Length: > 0 } &&
            _panelRuns.TryGetValue(tab, out PanelRun? live) &&
            FirePanelHandler(tab, live, button))
            return;

        // IsAlive rather than IsWorking: an armed panel reads as idle everywhere else, but it is
        // still a run, and starting another over it would leave two runs on one tab.
        if (tab.IsAlive)
            RequestStop(tab);
        else
            StartRun(tab, button, panelMode: true);
    }

    /// <summary>
    /// Empties the saved copy of a box the user cleared from its own toolbar.
    /// </summary>
    /// <remarks>
    /// Clearing only the control left the text saved: switching away and back brought it all back,
    /// and mid-run the next append that tripped truncation rewrote the whole buffer into the box
    /// the user had just emptied.
    /// </remarks>
    /// <param name="box">The box the user cleared.</param>
    private void OnPanelOutputCleared(string box)
    {
        if (Active is { } tab && ReferenceEquals(tab, _panelTab))
            tab.PanelState.SetOutput(box, "");
    }

    private void StartRun(ScriptTab tab, string? panelButton, bool panelMode)
    {
        // Recorded at the start rather than the end, because that is the question the library is
        // asked: a script parked on its panel handlers for an hour has been run, not finished, and
        // a run that never ends would never be recorded at all.
        if (tab.IsSavedToDisk)
            _library.SetLastRun(tab.Name, DateTime.Now);

        tab.ExecutionTask = RunTabCoreAsync(tab, panelButton, panelMode);
    }

    /// <summary>
    /// Starts saving unsaved buffers, and restores any a previous session left behind.
    /// </summary>
    /// <remarks>
    /// Restored before the timer starts, so the first tick cannot write an empty set over drafts
    /// that have not been brought back yet.
    /// </remarks>
    private void StartDrafts()
    {
        foreach (Draft draft in _drafts.Load())
        {
            if (string.IsNullOrWhiteSpace(draft.Code))
                continue;

            ScriptTab restored = AddTab(draft.Name, draft.Code, null);
            // Modified from the moment it comes back: it has never been written anywhere, and the
            // close prompt is what stops it being lost a second time.
            restored.IsModified = true;
        }

        _draftTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _draftTimer.Tick += (_, _) =>
        {
            SaveDrafts();
            // Polled on the same tick rather than pushed from the server: requests arrive off the
            // UI thread, and the bar only needs to be right to within a few seconds.
            _status?.SetMcpActivity(_mcp?.LastRequestUtc);
        };
        _draftTimer.Start();
    }

    private void SaveDrafts()
    {
        Draft[] drafts = _tabs
            .Where(tab => tab.FilePath is null)
            .Select(tab => new Draft(tab.Name, tab.CurrentCode))
            .Where(draft => draft.Code.Trim().Length > 0)
            .ToArray();

        _drafts.Save(drafts);
    }

    private void RequestStop(ScriptTab tab)
    {
        if (!tab.IsAlive)
            return;
        tab.SetRunState(ScriptRunState.Stopping);
        if (tab.Cts is { } cancellation)
            Observe(() => cancellation.CancelAsync());
    }

    /// <summary>
    /// Stops every run in the window, whatever tab is in front.
    /// </summary>
    /// <remarks>
    /// The one action that has to work when QX is not the focused application, so it takes no
    /// argument and asks nothing: a script sending packets to a live account is not something to
    /// put a confirmation in front of.
    /// </remarks>
    /// <returns>How many runs were asked to stop.</returns>
    private int StopAllScripts()
    {
        ScriptTab[] alive = _tabs.Where(tab => tab.IsAlive).ToArray();
        foreach (ScriptTab tab in alive)
            RequestStop(tab);

        if (alive.Length > 0)
            Qx.Diagnostics.Diag.Warn($"Stopped {alive.Length} running script(s).", "scripts");

        RefreshRunningCount();
        return alive.Length;
    }

    private void StopAllScripts(object sender, RoutedEventArgs e) => StopAllScripts();

    private void HookWindowMessages()
    {
        if (PresentationSource.FromVisual(this) is not HwndSource source)
            return;
        _window_source = source;
        source.AddHook(OnWindowMessage);
    }

    private void RegisterPanicHotkey()
    {
        _panic = PanicHotkey.Register(this, () => StopAllScripts());
        if (_panic is not null)
            return;

        Qx.Diagnostics.Diag.Warn(
            $"Panic key {PanicHotkey.Gesture} is held by another application; " +
            "stop from the tab or the status bar instead.",
            "hotkey");
    }

    /// <summary>Keeps the status bar's running count and its stop control in step.</summary>
    private void RefreshRunningCount()
    {
        int running = _tabs.Count(tab => tab.IsAlive);
        _status?.SetRunning(running);
    }

    /// <summary>
    /// Everything the palette offers.
    /// </summary>
    /// <remarks>
    /// Built per press rather than held, so availability is decided against the tab that is in
    /// front now. Anything added to the window belongs here; unlike a toolbar this costs no space,
    /// which is the point of having it.
    /// </remarks>
    private IEnumerable<PaletteCommand> PaletteCommands()
    {
        var empty = new RoutedEventArgs();
        bool HasTab() => Active is not null;

        yield return new("New script", "Script", "Ctrl+N", () => NewTab(this, empty));
        yield return new("Open the script library", "Script", "Ctrl+O", () => OpenScript(this, empty));
        yield return new("Save", "Script", "Ctrl+S", () => SaveScript(this, empty), HasTab);
        yield return new("Save as…", "Script", "Ctrl+Shift+S", () => SaveScriptAs(this, empty), HasTab);
        yield return new("Close the tab", "Script", "Ctrl+W", () =>
        {
            if (Active is { } tab)
                CloseTab(tab);
        }, HasTab);
        yield return new(
            "Reopen the last closed script",
            "Script",
            "Ctrl+Shift+T",
            ReopenClosedTab,
            () => _closedScripts.Count > 0);

        yield return new("Run or stop the script", "Run", "F5", () => ToggleRun(this, empty), HasTab);
        yield return new(
            "Stop every running script",
            "Run",
            PanicHotkey.Gesture,
            () => StopAllScripts(),
            () => _tabs.Any(tab => tab.IsAlive));
        yield return new("Go to the next error", "Run", "F8", () => GoToError(this, empty),
            () => Active is { } tab && tab.Errors.Any(error => error.Line is > 0));

        yield return new("Code view", "View", "Ctrl+1", () => SelectCodeMode(this, empty), HasTab);
        yield return new("UI panel view", "View", "Ctrl+2", () => SelectPanelMode(this, empty), HasTab);
        yield return new("Show or hide output", "View", "Ctrl+J", () => ToggleOutput(this, empty), HasTab);
        yield return new("Script API library", "View", "F2", () => ToggleApiBrowser(this, empty));
        yield return new(
            App.Settings.EditorWrap ? "Stop wrapping editor lines" : "Wrap editor lines",
            "View",
            "",
            ToggleEditorWrap);
        yield return new("Switch theme", "View", "", () => ToggleTheme(this, empty));
        yield return new("Keep the window on top", "View", "", () => ToggleTopmost(this, empty));
        yield return new("Application log", "View", "", () => GoTo(NavPage.Logging));

        yield return new("Copy output", "Output", "", () => CopyOutput(this, empty),
            () => Active is { Output.Count: > 0 });
        yield return new("Clear output", "Output", "", () => ClearOutput(this, empty),
            () => Active is { Output.Count: > 0 });
        yield return new("Copy the room id", "Session", "", () => CopyRoomId(this, empty),
            () => _status is { RoomId: > 0 });

        bool InRoom() => _status?.IsInRoom == true;
        yield return new("Room", "Room", "", () => ShowRoom(RoomSection.Info), InRoom);
        yield return new("People in the room", "Room", "", () => ShowRoom(RoomSection.Users), InRoom);
        yield return new("Furni in the room", "Room", "", () => ShowRoom(RoomSection.Furni), InRoom);

        yield return new("Settings and shortcuts", "Window", "F1", () => GoTo(NavPage.Settings));
        yield return new("About QX Scripter", "Window", "", () => GoTo(NavPage.About));
    }

    private void ShowCommandPalette() => CommandPalette.Show(this, PaletteCommands());

    /// <summary>Puts the current room's id on the clipboard, which is what the name is clicked for.</summary>
    private void CopyRoomId(object sender, RoutedEventArgs e)
    {
        if (_status is not { RoomId: > 0 } status)
            return;
        try
        {
            Clipboard.SetText(status.RoomId.ToString());
            Qx.Diagnostics.Diag.Info($"Copied room id {status.RoomId}.", "ui");
        }
        catch (Exception error)
        {
            Qx.Diagnostics.Diag.Error($"Copy failed: {error.Message}", "ui");
        }
    }

    /// <summary>
    /// Hands a press to the script that is waiting for it.
    /// </summary>
    /// <param name="tab">The tab the panel belongs to.</param>
    /// <param name="run">The run still alive behind the panel.</param>
    /// <param name="button">The button that was pressed.</param>
    /// <returns>
    /// <see langword="false"/> when the script registered nothing for that button, which leaves the
    /// press meaning what it meant before handlers existed.
    /// </returns>
    private bool FirePanelHandler(ScriptTab tab, PanelRun run, string button)
    {
        // What the user typed since the run started is what the handler has to read: the values the
        // run was handed at its first press are already stale by its second.
        run.Sync();

        if (!run.Globals.Ui.HandledButtons.Contains(button, StringComparer.OrdinalIgnoreCase))
            return false;

        JoinableTask work = _ui_tasks.Factory.RunAsync(async () =>
        {
            Task? handlers = run.Globals.Ui.Invoke(button);
            if (handlers is not null)
                await handlers;
        });

        // A press is the one moment an armed panel is genuinely doing something, and the button that
        // was pressed is where that belongs — not in a toolbar the panel does not even show.
        tab.BeginHandler();
        if (IsVisiblePanel(tab))
            Panel.SetButtonBusy(button, true);

        run.Watch(WatchPanelHandlerAsync(tab, run, button, work));
        return true;
    }

    /// <summary>
    /// Waits for one press's handlers and reports what they threw.
    /// </summary>
    /// <remarks>
    /// A handler that throws takes down only itself: the panel is still there and its other buttons
    /// still answer, which is the point of the run outliving its body.
    /// </remarks>
    private async Task WatchPanelHandlerAsync(ScriptTab tab, PanelRun run, string button, JoinableTask work)
    {
        try
        {
            await work;
        }
        catch (Exception error)
        {
            // A button's handlers run together and awaiting the group surfaces only the first of
            // them, so one handler being stopped would hide another one failing.
            IEnumerable<Exception> faults = work.Task.Exception is { } group
                ? group.InnerExceptions
                : new[] { error };

            foreach (Exception fault in faults)
            {
                if (fault is OperationCanceledException)
                    continue;

                // Finish means the script is done with itself, so the stop it causes is a finish.
                if (fault is ScriptFinishedException)
                {
                    run.Finished = true;
                    RequestStop(tab);
                    continue;
                }

                run.Report(fault);
            }
        }
        finally
        {
            // Started on the UI thread and awaited without leaving its context, so this lands back
            // on it — the same reason the run's own body can touch the panel after its await.
            tab.EndHandler();
            if (IsVisiblePanel(tab))
                Panel.SetButtonBusy(button, false);
        }
    }

    /// <summary>
    /// A panel run that outlived its script body, because the script registered click handlers.
    /// </summary>
    private sealed class PanelRun
    {
        private const int HandlerGraceMs = 500;

        private readonly List<Task> _handlers = [];

        /// <summary>What the script ran against, and with it the handlers it registered.</summary>
        public required ScriptGlobals Globals { get; init; }

        /// <summary>Reads the panel's controls into the script's <c>Ui</c> before a handler runs.</summary>
        public required Action Sync { get; init; }

        /// <summary>Reports what a handler threw, the way the run reports a script fault.</summary>
        public required Action<Exception> Report { get; init; }

        /// <summary>Whether a handler called <c>Finish</c>, so its stop is reported as a finish.</summary>
        public bool Finished { get; set; }

        /// <summary>
        /// Keeps a started handler so a stop can wait for it. Presses that are over are dropped as
        /// they are noticed, which holds the list to what is actually still running.
        /// </summary>
        /// <param name="handler">The press being watched.</param>
        public void Watch(Task handler)
        {
            _handlers.RemoveAll(started => started.IsCompleted);
            _handlers.Add(handler);
        }

        /// <summary>
        /// Waits for the handlers still running, so the globals are not disposed under one.
        /// </summary>
        /// <remarks>
        /// Bounded, and by the same grace the background tasks get: a handler that ignores the token
        /// must not be able to hold the tab in "stopping" for as long as it likes.
        /// </remarks>
        public async Task DrainAsync()
        {
            Task[] pending = _handlers.Where(handler => !handler.IsCompleted).ToArray();
            _handlers.Clear();
            if (pending.Length == 0)
                return;

            await Task.WhenAny(Task.WhenAll(pending), Task.Delay(HandlerGraceMs));
        }
    }


    /// <summary>
    /// Asks the user a yes-or-no on the script's behalf and hands back the answer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog is shown for as long as the run is still the tab's own, whatever is on screen: a
    /// question from a live script deserves an answer, and answering it silently because the user
    /// had switched tabs would be a decision made behind their back.
    /// </para>
    /// <para>
    /// Everything else answers no, at once. A run that was replaced or stopped, a window on its way
    /// out, a dialog that refuses to open — none of them can produce an answer, and a script left
    /// awaiting one would hang for as long as the process lives. No is the safe half of a
    /// yes-or-no, which is why it is what nobody-to-ask means.
    /// </para>
    /// </remarks>
    /// <param name="tab">The tab the asking run belongs to.</param>
    /// <param name="run">That run's cancellation source, which identifies it.</param>
    /// <param name="title">The heading the script gave.</param>
    /// <param name="message">What is being confirmed.</param>
    private async Task<bool> AskScriptAsync(
        ScriptTab tab,
        CancellationTokenSource run,
        string title,
        string message)
    {
        try
        {
            return await InvokeUiAsync(
                () => CanAsk(tab, run) && ConfirmDialog.Ask(this, title, message, "Yes"),
                _cts.Token);
        }
        catch (Exception error)
        {
            AppendOutput(tab, $"Confirmation failed: {error.Message}", OutputLevel.Error);
            return false;
        }
    }

    /// <summary>
    /// Asks the user for a value on the script's behalf.
    /// </summary>
    /// <remarks>
    /// Answers null where <see cref="AskScriptAsync"/> answers no, and for the same reasons. Null
    /// is also what a dismissed box gives, so a script that cannot tell the two apart still reads
    /// both as "no value", which is the only sensible thing it could have done with either.
    /// </remarks>
    /// <param name="tab">The tab the asking run belongs to.</param>
    /// <param name="run">That run's cancellation source, which identifies it.</param>
    /// <param name="title">The heading the script gave.</param>
    /// <param name="initial">What the box starts with.</param>
    private async Task<string?> PromptScriptAsync(
        ScriptTab tab,
        CancellationTokenSource run,
        string title,
        string initial)
    {
        try
        {
            return await InvokeUiAsync<string?>(
                () => CanAsk(tab, run)
                    ? RenameDialog.Ask(this, initial, title, "OK", PackIconKind.CommentQuestionOutline)
                    : null,
                _cts.Token);
        }
        catch (Exception error)
        {
            AppendOutput(tab, $"Prompt failed: {error.Message}", OutputLevel.Error);
            return null;
        }
    }

    /// <summary>Whether there is anyone left to answer a script's question.</summary>
    private bool CanAsk(ScriptTab tab, CancellationTokenSource run) =>
        !_closed && IsLoaded && _tabs.Contains(tab) && ReferenceEquals(tab.Cts, run);

    private void SaveDownload(ScriptTab tab, string fileName, string content)
    {
        try
        {
            var dialog = new SaveFileDialog { FileName = fileName };
            if (dialog.ShowDialog(this) == true)
                File.WriteAllText(dialog.FileName, content);
        }
        catch (Exception error)
        {
            AppendOutput(tab, $"Download failed: {error.Message}", OutputLevel.Error);
        }
    }

    private void ReportInterceptFailure(Qx.Interception.Intercept intercept, Exception error)
    {
        if (!_intercept_failures.ShouldReport(intercept.Packet.Header, error))
            return;
        string message = InterceptFailureLog.Format(
            InterceptFailureLog.Describe(intercept.Packet.Header, _messages),
            error);
        Dispatch(() => TryAppendApplicationLog(OutputLevel.Error, message, "intercept"));
    }

    private void OnDiagnostic(Qx.Diagnostics.DiagLevel level, string message, string? category)
    {
        if (_ui_tasks.IsOnMainThread)
            TryAppendApplicationLog(level, message, category);
        else
            Dispatch(() => TryAppendApplicationLog(level, message, category));
    }

    private void TryAppendApplicationLog(
        Qx.Diagnostics.DiagLevel level,
        string message,
        string? category)
    {
        try
        {
            LoggingView.Append(level, message, category);
        }
        catch
        {
        }
    }

    private void TryAppendApplicationLog(OutputLevel level, string message, string? category)
    {
        try
        {
            LoggingView.Append(level, message, category);
        }
        catch
        {
        }
    }

    private void AppendOutput(ScriptTab tab, string message, OutputLevel level = OutputLevel.Info) => Dispatch(() =>
    {
        tab.AppendOutputLine(message, level);
        if (tab != Active)
            return;

        // The filter is a live view over the collection, so the new line is admitted or hidden
        // without anything being rebuilt. Only the count has to be told, and only by one.
        if (OutputFiltering && Matches(message, level))
            SetOutputFilterCount(_outputMatches + 1);

        ScrollOutputToEnd();
    });

    private string OutputFilter => OutputFilterBox?.Text?.Trim() ?? "";

    /// <summary>Whether only warnings and errors are being shown.</summary>
    private bool OutputProblemsOnly => OutputProblemsButton?.IsChecked == true;

    /// <summary>
    /// Whether a line survives both filters.
    /// </summary>
    /// <remarks>
    /// The text and the level narrow the same view rather than replacing one another, so a level
    /// with no matching text shows nothing — which is the answer, not a bug.
    /// </remarks>
    private bool Matches(OutputLine line) => Matches(line.Text, line.Level);

    private bool Matches(string text, OutputLevel level) =>
        (!OutputProblemsOnly || level is not OutputLevel.Info) &&
        (OutputFilter.Length == 0 || text.Contains(OutputFilter, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether anything is narrowing the console, which is when a count is worth showing.</summary>
    private bool OutputFiltering => OutputFilter.Length > 0 || OutputProblemsOnly;

    /// <summary>
    /// Scrolls to the newest line, but only from the bottom.
    /// </summary>
    /// <remarks>
    /// A tab that is being read while its script still writes used to be yanked back down on
    /// every line. Scrolling up is taken as "I am reading this" and left alone until the view is
    /// returned to the bottom.
    /// </remarks>
    private void ScrollOutputToEnd()
    {
        if (OutputScroller is not { } scroller)
            return;

        if (scroller.ScrollableHeight - scroller.VerticalOffset > 24)
        {
            // Held away from the bottom, so the line is not followed — but it did arrive, and the
            // chip is the only thing that says so.
            _outputPending++;
            ShowOutputPending();
            return;
        }

        scroller.ScrollToEnd();
    }

    private ScrollViewer? OutputScroller
    {
        get
        {
            if (_outputScroller is not null)
                return _outputScroller;

            _outputScroller = TreeLookup.FirstChild<ScrollViewer>(OutputBox);
            if (_outputScroller is not null)
                _outputScroller.ScrollChanged += OnOutputScrolled;
            return _outputScroller;
        }
    }

    /// <summary>Clears the pending count once the view is back at the bottom, however it got there.</summary>
    private void OnOutputScrolled(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroller)
            return;
        if (scroller.ScrollableHeight - scroller.VerticalOffset > 24)
            return;

        _outputPending = 0;
        OutputLatestButton.Visibility = Visibility.Collapsed;
    }

    private void ShowOutputPending()
    {
        OutputLatestText.Text = _outputPending == 1 ? "1 new line" : $"{_outputPending} new lines";
        OutputLatestButton.Visibility = Visibility.Visible;
    }

    private void ScrollOutputToLatest(object sender, RoutedEventArgs e)
    {
        _outputPending = 0;
        OutputLatestButton.Visibility = Visibility.Collapsed;
        OutputScroller?.ScrollToEnd();
    }

    /// <summary>A copy that says nothing looks like a copy that failed.</summary>
    private void ConfirmCopy()
    {
        _copyConfirmation?.Stop();
        CopyOutputIcon.Kind = PackIconKind.Check;
        CopyOutputIcon.SetResourceReference(ForegroundProperty, "QxSuccessBrush");
        CopyOutputButton.ToolTip = "Copied";

        _copyConfirmation ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        _copyConfirmation.Tick -= EndCopyConfirmation;
        _copyConfirmation.Tick += EndCopyConfirmation;
        _copyConfirmation.Start();
    }

    private void EndCopyConfirmation(object? sender, EventArgs e)
    {
        _copyConfirmation?.Stop();
        CopyOutputIcon.Kind = PackIconKind.ContentCopy;
        CopyOutputIcon.ClearValue(ForegroundProperty);
        CopyOutputButton.ToolTip = "Copy output";
    }

    /// <summary>Points the console at a tab's lines and applies the current filter to them.</summary>
    private void RefreshOutput(ScriptTab tab)
    {
        ICollectionView view = CollectionViewSource.GetDefaultView(tab.Output);
        view.Filter = item => item is OutputLine line && Matches(line);
        _outputView = view;

        if (!ReferenceEquals(OutputBox.ItemsSource, view))
            OutputBox.ItemsSource = view;

        RecountOutputMatches();
        ScrollOutputToEnd();
    }

    /// <summary>
    /// Recounts the filtered lines, which is the one place the whole buffer is walked.
    /// </summary>
    /// <remarks>
    /// Only on a filter change or a tab switch — never per line, which is what the count costs if
    /// it is derived rather than carried.
    /// </remarks>
    private void RecountOutputMatches()
    {
        if (!OutputFiltering)
        {
            SetOutputFilterCount(-1);
            return;
        }

        int matches = 0;
        if (_outputView is not null)
        {
            foreach (object _ in _outputView)
                matches++;
        }
        SetOutputFilterCount(matches);
    }

    /// <summary>Shows the match count, or nothing at all when no filter is set.</summary>
    /// <param name="matches">The number of matching lines, or -1 for "no filter".</param>
    private void SetOutputFilterCount(int matches)
    {
        _outputMatches = matches;
        OutputFilterCount.Text = matches switch
        {
            < 0 => "",
            0 => "no matches",
            _ => $"{matches} matching"
        };
    }

    private void OnOutputFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (Active is null || _outputView is null)
            return;
        _outputView.Refresh();
        RecountOutputMatches();
        ScrollOutputToEnd();
    }

    /// <summary>Ctrl+C over the console copies the selected lines rather than the whole buffer.</summary>
    private void OnOutputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || Keyboard.Modifiers != ModifierKeys.Control)
            return;
        if (OutputBox.SelectedItems.Count == 0)
            return;

        string text = string.Join(
            Environment.NewLine,
            OutputBox.SelectedItems.OfType<OutputLine>().Select(line => line.Text));
        try
        {
            Clipboard.SetText(text);
            ConfirmCopy();
        }
        catch (Exception)
        {
            // A clipboard another process is holding is not worth a line of output here: the
            // toolbar copy reports it, and this one is a shortcut over a view that still shows
            // exactly what failed to copy.
        }
        e.Handled = true;
    }

    private void OnOutputFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || OutputFilterBox.Text.Length == 0)
            return;

        OutputFilterBox.Clear();
        e.Handled = true;
    }

    /// <summary>Narrows the console to warnings and errors, or opens it back up.</summary>
    private void ToggleOutputProblems(object sender, RoutedEventArgs e)
    {
        bool only = OutputProblemsOnly;
        OutputProblemsIcon.Kind = only ? PackIconKind.AlertCircle : PackIconKind.AlertCircleOutline;
        if (only)
            OutputProblemsIcon.SetResourceReference(ForegroundProperty, "QxWarningBrush");
        else
            OutputProblemsIcon.ClearValue(ForegroundProperty);

        OutputProblemsButton.ToolTip = only ? "Show every line" : "Show only warnings and errors";
        AutomationProperties.SetName(
            OutputProblemsButton,
            only ? "Show every output line" : "Show only warnings and errors");

        _outputView?.Refresh();
        RecountOutputMatches();
        ScrollOutputToEnd();
    }

    /// <summary>
    /// Wraps or unwraps every editor, now and later.
    /// </summary>
    /// <remarks>
    /// Applied to the editors that exist and stored for the ones that do not: an editor is built
    /// the first time its tab is shown, and would otherwise come up unwrapped in a window the user
    /// had already set to wrap.
    /// </remarks>
    private void ToggleEditorWrap()
    {
        bool wrap = !App.Settings.EditorWrap;
        App.Settings.EditorWrap = wrap;

        foreach (ScriptTab tab in _tabs)
        {
            if (tab.Editor is { } editor)
                editor.WordWrap = wrap;
        }
    }

    private void ToggleOutputWrap(object sender, RoutedEventArgs e)
    {
        App.Settings.OutputWrap = OutputWrapButton.IsChecked == true;
        ApplyOutputWrap();
    }

    private void ApplyOutputWrap()
    {
        bool wrap = App.Settings.OutputWrap;
        OutputWrapButton.IsChecked = wrap;
        OutputWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ScrollViewer.SetHorizontalScrollBarVisibility(
            OutputBox,
            wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        OutputWrapIcon.Kind = wrap ? PackIconKind.Wrap : PackIconKind.WrapDisabled;
        OutputWrapButton.ToolTip = wrap ? "Stop wrapping long lines" : "Wrap long lines";
        AutomationProperties.SetName(OutputWrapButton, wrap ? "Stop wrapping output lines" : "Wrap output lines");
    }

    private void ClearOutput(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;
        tab.Output.Clear();
        SetOutputFilterCount(OutputFilter.Length == 0 ? -1 : 0);
    }

    private void CopyOutput(object sender, RoutedEventArgs e)
    {
        if (Active is not { Output.Count: > 0 } tab)
            return;
        try
        {
            Clipboard.SetText(tab.OutputText());
            ConfirmCopy();
        }
        catch (Exception error)
        {
            AppendOutput(tab, $"Copy failed: {error.Message}", OutputLevel.Error);
        }
    }

    private void ToggleOutput(object sender, RoutedEventArgs e)
    {
        if (Active is not { } tab)
            return;
        if (!tab.OutputCollapsed && OutputRow.ActualHeight > 32)
            tab.OutputHeight = OutputRow.ActualHeight;
        tab.OutputCollapsed = !tab.OutputCollapsed;
        ApplyOutputState(tab);
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        App.Theme.Toggle();
        RefreshThemeButton();
        RebuildEditors();
        Home.RefreshTheme();
    }

    private void ToggleTopmost(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        App.Settings.Topmost = Topmost;
        RefreshTopmostButton();
    }

    private void RefreshTopmostButton()
    {
        TopmostIcon.Kind = Topmost ? PackIconKind.Pin : PackIconKind.PinOutline;
        if (Topmost)
            TopmostIcon.SetResourceReference(ForegroundProperty, "QxAccentBrush");
        else
            TopmostIcon.ClearValue(ForegroundProperty);
        TopmostButton.ToolTip = Topmost ? "Stop keeping on top" : "Keep on top";
        AutomationProperties.SetName(TopmostButton, Topmost ? "Stop keeping window on top" : "Keep window on top");
    }

    private void RefreshThemeButton()
    {
        bool dark = App.Theme.IsDark;
        ThemeIcon.Kind = dark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
        ThemeButton.ToolTip = dark ? "Use light theme" : "Use dark theme";
        AutomationProperties.SetName(ThemeButton, dark ? "Use light theme" : "Use dark theme");
    }

    private void RebuildEditors()
    {
        foreach (ScriptTab tab in _tabs)
        {
            if (tab.Editor is not null)
            {
                if (tab.EditorInitialized)
                    tab.Code = tab.Editor.Text;
                tab.Editor = null;
                tab.EditorInitialized = false;
            }
        }

        EditorHost.Content = null;
        if (Active is { } active)
            ShowEditor(active);
    }

    private void WatchRuntime()
    {
        RuntimeHost runtime = _runtime!;
        McpServer mcp = _mcp!;
        _ui_tasks.Factory.RunAsync(async () =>
        {
            try
            {
                await runtime.StartAsync(_cts.Token);
                RuntimeServiceStatus mcp_status = runtime.Status.Mcp;
                if (mcp_status.Phase == RuntimeServicePhase.Running)
                {
                    _status?.SetMcp(true, mcp.Port);
                }
                else
                {
                    string failure = mcp_status.Error?.Message ?? $"MCP server failed to start on port {mcp.Port}.";
                    _status?.SetMcp(false, mcp.Port, failure);
                }
                _status?.Refresh();
                Dispatch(SettingsView.Refresh);
                runtime.TransportTask.ContinueWith(
                    completed => Qx.Diagnostics.Diag.Error(completed.Exception?.ToString() ?? "Transport failed.", "host"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default).Forget();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Qx.Diagnostics.Diag.Error(error.ToString(), "host");
            }
        }).Task.Forget();
    }

    private void OnGEarthLost()
    {
        if (_cts.IsCancellationRequested || !_launched_by_gearth)
            return;

        Observe(() => CloseAfterDisconnectAsync());
    }

    private async Task CloseAfterDisconnectAsync()
    {
        _status?.Refresh();
        await InvokeUiAsync(() =>
        {
            _close_with_gearth = true;
            _closingConfirmed = true;
            Close();
            return true;
        }, _cts.Token);
    }

    private async Task CloseIfGEarthUnavailableAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
        if (_extension.IsInterceptorConnected)
            return;
        await CloseAfterDisconnectAsync();
    }

    private ScriptTab? TabByName(string name) =>
        _tabs.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    Task<string> Qx.Mcp.IEditorBridge.ListTabsAsync(CancellationToken cancellationToken) => InvokeUiAsync(() =>
        _tabs.Count == 0
            ? "no open tabs"
            : string.Join("\n", _tabs.Select(t =>
                $"{(t == Active ? "* " : "  ")}{t.Name} [{(t.IsArmedIdle ? "armed" : t.RunState.ToString().ToLowerInvariant())}]{(t.IsModified ? " ●" : "")}")),
        cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.GetActiveTabAsync(CancellationToken cancellationToken) => InvokeUiAsync(() =>
        Active is { } tab ? $"{tab.Name}\n----\n{tab.CurrentCode}" : "no active tab",
        cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.OpenTabAsync(string name, CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        string path = Path.Combine(_scriptsDir, name + ".csx");
        if (!File.Exists(path))
            return $"no saved script named '{name}'";
        OpenScriptFile(path);
        return $"opened '{name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.CreateTabAsync(
        string name,
        string code,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        AddTab(string.IsNullOrWhiteSpace(name) ? "untitled" : name, code, null);
        return $"created tab '{name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.EditActiveTabAsync(
        string code,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        if (Active is not { } tab)
            return "no active tab";
        if (tab.Editor is not null)
            tab.Editor.Text = code;
        else
            tab.Code = code;
        return "updated active tab";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.SelectTabAsync(string name, CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        if (TabByName(name) is not { } tab)
            return $"no tab named '{name}'";
        HideHome();
        TabList.SelectedItem = tab;
        return $"selected '{name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.CloseTabAsync(string name, CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        if (TabByName(name) is not { } tab)
            return $"no tab named '{name}'";
        CloseTab(tab);
        if (!_tabs.Contains(tab))
            return $"closed '{name}'";
        return tab.IsWorking ? $"stopping '{name}'" : $"close cancelled for '{name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.RunActiveTabAsync(
        string name,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        ScriptTab? tab = string.IsNullOrWhiteSpace(name) ? Active : TabByName(name);
        if (tab is null)
            return string.IsNullOrWhiteSpace(name) ? "no active tab" : $"no tab named '{name}'";
        if (tab.IsAlive)
            return tab.IsArmedIdle ? "panel already running; press its buttons or stop it" : "already running";
        StartRun(tab, null, tab.PanelMode);
        return $"running '{tab.Name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.StopActiveTabAsync(
        string name,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        ScriptTab? tab = string.IsNullOrWhiteSpace(name) ? Active : TabByName(name);
        if (tab is null)
            return string.IsNullOrWhiteSpace(name) ? "no active tab" : $"no tab named '{name}'";
        if (!tab.IsAlive)
            return "not running";
        RequestStop(tab);
        return $"stopping '{tab.Name}'";
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.GetTabOutputAsync(
        string name,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        ScriptTab? tab = string.IsNullOrWhiteSpace(name) ? Active : TabByName(name);
        if (tab is null)
            return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";
        return tab.Output.Count == 0 ? "(no output)" : tab.OutputText();
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.GetTabStatusAsync(
        string name,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        ScriptTab? tab = string.IsNullOrWhiteSpace(name) ? Active : TabByName(name);
        if (tab is null)
            return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";

        return JsonSerializer.Serialize(new
        {
            name = tab.Name,
            state = tab.RunState.ToString().ToLowerInvariant(),
            running = tab.IsRunning,
            working = tab.IsWorking,
            // A panel waiting for a press: its run is up, and it is doing nothing.
            armed = tab.IsArmedIdle,
            handlers = tab.BusyHandlers,
            faulted = tab.IsFaulted,
            runtimeMs = tab.RuntimeMs,
            startedAt = tab.StartedAt,
            finishedAt = tab.FinishedAt,
            // Still characters, so the MCP shape does not change under a caller. The console is
            // lines now, but this is asked for occasionally by a tool and never per line.
            outputLength = tab.OutputText().Length,
            errorCount = tab.Errors.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }, cancellationToken);

    Task<string> Qx.Mcp.IEditorBridge.GetTabErrorsAsync(
        string name,
        CancellationToken cancellationToken) => InvokeUiAsync(() =>
    {
        ScriptTab? tab = string.IsNullOrWhiteSpace(name) ? Active : TabByName(name);
        if (tab is null)
            return string.IsNullOrWhiteSpace(name) ? "no tab" : $"no tab named '{name}'";
        return tab.Errors.Count == 0
            ? "no errors"
            : JsonSerializer.Serialize(tab.Errors, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }, cancellationToken);

    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        RefreshWindowState();
    }

    /// <summary>
    /// Reopens where the window was left. With nothing saved the size is taken from the work area
    /// rather than a fixed default, which on a small screen used to be taller than the desktop.
    /// </summary>
    private void RestoreWindowPlacement()
    {
        if (App.Settings.Window is { } saved && IsReachable(saved))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = Math.Max(MinWidth, saved.Width);
            Height = Math.Max(MinHeight, saved.Height);
            Left = saved.Left;
            Top = saved.Top;
            if (saved.Maximized)
                WindowState = System.Windows.WindowState.Maximized;
            return;
        }

        Rect work = SystemParameters.WorkArea;
        Width = Math.Clamp(work.Width * 0.80, MinWidth, 1400);
        Height = Math.Clamp(work.Height * 0.86, MinHeight, 950);
    }

    private void SaveWindowPlacement()
    {
        // A window that never made it to the screen has nothing worth remembering.
        if (!IsLoaded)
            return;

        bool maximized = WindowState == System.Windows.WindowState.Maximized ||
            (_launched_by_gearth &&
             WindowState == System.Windows.WindowState.Minimized &&
             _hosted_restore_state == System.Windows.WindowState.Maximized);
        Rect bounds = WindowState == System.Windows.WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        // Left and Top are NaN until the window has been placed on screen at least once.
        if (bounds.IsEmpty ||
            !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top) ||
            !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
            bounds.Width < 1 || bounds.Height < 1)
            return;

        App.Settings.Window = new WindowPlacement
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = maximized
        };
    }

    /// <summary>Guards against a monitor that is no longer attached, or a window dragged off-screen.</summary>
    private static bool IsReachable(WindowPlacement placement)
    {
        if (placement.Width < 1 || placement.Height < 1)
            return false;

        var desktop = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        Rect visible = Rect.Intersect(new Rect(placement.Left, placement.Top, placement.Width, placement.Height), desktop);
        return !visible.IsEmpty && visible.Width >= 160 && visible.Height >= 40;
    }

    private void RefreshWindowState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaxIcon.Kind = maximized ? PackIconKind.WindowRestore : PackIconKind.WindowMaximize;
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
        AutomationProperties.SetName(MaximizeButton, maximized ? "Restore window" : "Maximize window");
    }

    /// <summary>
    /// Reopens the scripts that were open at close. Only files are restored — an unsaved buffer
    /// has nothing on disk to reopen, so it is not pretended to survive.
    /// </summary>
    private void RestoreSession()
    {
        if (!App.Settings.RestoreSession || App.Settings.Session is not { Open.Count: > 0 } session)
            return;

        foreach (string path in session.Open)
            OpenScriptFile(path);

        if (session.Active is { } active &&
            _tabs.FirstOrDefault(tab => string.Equals(tab.FilePath, active, StringComparison.OrdinalIgnoreCase)) is { } selected)
            TabList.SelectedItem = selected;
    }

    private void SaveSession()
    {
        if (!App.Settings.RestoreSession)
            return;

        App.Settings.Session = new SessionState
        {
            Open = _tabs.Where(tab => tab.FilePath is not null).Select(tab => tab.FilePath!).ToArray(),
            Active = Active?.FilePath
        };
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_launched_by_gearth && !_close_with_gearth)
        {
            e.Cancel = true;
            HideForGEarth();
            return;
        }

        SaveWindowPlacement();
        SaveSession();
        SaveVisiblePanelState();
        foreach (ScriptTab tab in _tabs)
            RememberPanel(tab);

        if (_closingConfirmed)
            return;

        int modified = _tabs.Count(tab => tab.IsModified);
        if (modified == 0)
            return;

        bool discard = ConfirmDialog.Ask(
            this,
            "Close QX Scripter?",
            modified == 1
                ? "One script has unsaved changes. Close QX Scripter and discard them?"
                : $"{modified} scripts have unsaved changes. Close QX Scripter and discard them?",
            "Discard and close");
        if (!discard)
        {
            e.Cancel = true;
            return;
        }

        _closingConfirmed = true;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Nothing can be asked of the user from here on: a modal owned by a closed window throws,
        // and a script still on its way down must get its refusal rather than that exception.
        _closed = true;
        Qx.Diagnostics.Diag.Emitted -= OnDiagnostic;
        _draftTimer?.Stop();
        // A close that got this far was agreed to — every unsaved buffer was either saved or
        // knowingly discarded, so keeping drafts would restore what the user just declined.
        if (_close_with_gearth)
            SaveDrafts();
        else
            _drafts.Clear();
        // Released explicitly: a system-wide combination outlives the window that claimed it, and
        // a QX that was restarted after a crash would find its own key already taken.
        _panic?.Dispose();
        _panic = null;
        _window_source?.RemoveHook(OnWindowMessage);
        _window_source = null;
        RuntimeHost? runtime = _runtime;
        _runtime = null;
        _cts.Cancel();
        if (runtime is not null)
            Observe(() => runtime.DisposeAsync().AsTask());
    }

    private void CloseWindow(object sender, RoutedEventArgs e) => Close();

    internal void HideForGEarth()
    {
        if (WindowState != WindowState.Minimized)
            _hosted_restore_state = WindowState;
        WindowState = WindowState.Minimized;
        ShowInTaskbar = false;
        Hide();
    }

    private void Dispatch(Action action) => _ui_tasks.OnUi(action);

    private Task<T> InvokeUiAsync<T>(Func<T> action, CancellationToken cancellationToken) =>
        _ui_tasks.SwitchAsync(action, cancellationToken);

    private void Observe(Func<Task> task_factory) => _ui_tasks.Observe(task_factory);
}
