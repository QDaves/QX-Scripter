using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using Qx.Scripting;

namespace Qx.Ui;

public partial class HomeView : UserControl
{
    private const string Uncategorised = "Uncategorised";
    private readonly UiTaskScope _ui_tasks;

    public sealed record ScriptFile(string Name, string Path, string Modified, bool IsRunning, ScriptMeta Meta)
    {
        public DateTime LastWrite { get; init; }

        /// <summary>When this script last started running, or null if it never has.</summary>
        public DateTime? LastRun { get; init; }

        /// <summary>How the last run ended, or null while it is running or never ran.</summary>
        public string? LastOutcome { get; init; }

        public bool LastRunFailed =>
            !IsRunning && string.Equals(LastOutcome, nameof(ScriptRunState.Faulted), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The one time a row shows.
        /// </summary>
        /// <remarks>
        /// The last run wins over the last edit when there is one. For a library of things that are
        /// run rather than read, "ran 20m ago" answers the question someone is actually asking of
        /// the list; the edit time is still a hover away.
        /// </remarks>
        public string Detail => IsRunning ? "running" : LastRun switch
        {
            { } run when LastRunFailed => $"failed {Ago(run)}",
            { } run => $"ran {Ago(run)}",
            _ => $"edited {Modified}"
        };

        public string DetailTooltip => IsRunning
            ? $"Running; edited {Modified}"
            : LastRun is { } run
            ? $"{OutcomeText} {Ago(run)} · edited {Modified}"
            : $"Edited {Modified}, never run";

        private string OutcomeText => LastOutcome switch
        {
            nameof(ScriptRunState.Faulted) => "Failed",
            nameof(ScriptRunState.Stopped) => "Stopped",
            nameof(ScriptRunState.Finished) => "Finished",
            // Started but never reached an end, which is what a run cut short by closing QX looks
            // like — and what a bot still going looked like the last time anything was written.
            _ => "Last run"
        };

        public bool HasCategory => !string.IsNullOrWhiteSpace(Meta.Category);
        public string CategoryName => HasCategory ? Meta.Category!.Trim() : Uncategorised;

        /// <summary>A running script keeps its file; deleting it out from under the run is not offered.</summary>
        public bool CanDelete => !IsRunning;

        public string DeleteAccessibilityText => $"Delete {Name}";

        public string AccessibilityText
        {
            get
            {
                var parts = new List<string> { Name };
                if (HasCategory)
                    parts.Add($"category {CategoryName}");
                if (IsRunning)
                    parts.Add("running");
                parts.Add($"modified {Modified}");
                return string.Join(", ", parts);
            }
        }
    }

    private List<ScriptFile> _all = [];
    private string _scriptsDir = "";
    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _rescan;

    public event Action<string>? OpenRequested;
    public event Action? NewRequested;
    public event Action<string>? DeleteRequested;
    public event Action<string>? RenameRequested;
    public event Action<string>? DuplicateRequested;
    public event Action<string>? RevealRequested;

    /// <summary>Raised when the library should step aside, if there is anything to step back to.</summary>
    public event Action? DismissRequested;

    private ScriptLibrary? _library;

    /// <summary>Set by the host before the first <see cref="Reload"/>.</summary>
    public ScriptLibrary Library
    {
        get => _library ??= new ScriptLibrary();
        set => _library = value;
    }

    public HomeView()
    {
        _ui_tasks = new UiTaskScope(Dispatcher, "library");
        InitializeComponent();
        Loaded += (_, _) => ApplyView();
    }

    public void Reload(string scriptsDir, IReadOnlySet<string> runningPaths)
    {
        _scriptsDir = scriptsDir;
        _all = LoadScripts(scriptsDir, runningPaths);
        SearchBox.Clear();
        ApplyView();
        Apply("");
        Watch(scriptsDir);
    }

    /// <summary>
    /// Watches the scripts folder so files written by anything else show up.
    /// </summary>
    /// <remarks>
    /// The library used to refresh only when it was the one doing the writing, so a script saved
    /// over MCP — or edited in another editor — was invisible until something unrelated happened
    /// to reload it. Changes are coalesced onto a timer because a single save raises several
    /// events, and each one would otherwise rebuild the list.
    /// </remarks>
    private void Watch(string scriptsDir)
    {
        _watcher?.Dispose();
        _watcher = null;

        if (!Directory.Exists(scriptsDir))
            return;

        _rescan ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _rescan.Tick -= OnRescanTick;
        _rescan.Tick += OnRescanTick;

        var watcher = new FileSystemWatcher(scriptsDir, "*" + ScriptName.Extension)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        // Marshalled by hand rather than through SynchronizingObject: these arrive on a watcher
        // thread, and everything they lead to touches the list.
        void Bump(object? _, FileSystemEventArgs __) =>
            _ui_tasks.Post(RestartRescan, DispatcherPriority.Background);

        watcher.Created += Bump;
        watcher.Deleted += Bump;
        watcher.Changed += Bump;
        watcher.Renamed += (_, _) =>
            _ui_tasks.Post(RestartRescan, DispatcherPriority.Background);
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;
    }

    private void RestartRescan()
    {
        _rescan?.Stop();
        _rescan?.Start();
    }

    private void OnRescanTick(object? sender, EventArgs e)
    {
        _rescan?.Stop();

        // Only while the library is the thing on screen. Rebuilding it behind a tab the user is
        // typing in costs work nobody asked for and can move a selection they cannot see.
        if (Visibility != Visibility.Visible || _scriptsDir.Length == 0)
            return;

        Refresh(RunningPaths());
    }

    /// <summary>Reloads from disk without disturbing the current search.</summary>
    public void Refresh(IReadOnlySet<string> runningPaths)
    {
        _all = LoadScripts(_scriptsDir, runningPaths);
        Apply(SearchBox.Text);
    }

    public void RefreshTheme()
    {
        if (_all.Count > 0)
            Apply(SearchBox.Text);
    }

    public void RefreshRunning(IReadOnlySet<string> runningPaths)
    {
        _all = _all
            .Select(file => file with { IsRunning = runningPaths.Contains(file.Path) })
            .ToList();
        Apply(SearchBox.Text);
    }

    private List<ScriptFile> LoadScripts(string scriptsDir, IReadOnlySet<string> runningPaths)
    {
        if (!Directory.Exists(scriptsDir))
            return [];

        IEnumerable<ScriptFile> files = Directory.GetFiles(scriptsDir, "*.csx")
            .Select(f => new FileInfo(f))
            .Select(f => new ScriptFile(
                Path.GetFileNameWithoutExtension(f.Name),
                f.FullName,
                Ago(f.LastWriteTime),
                runningPaths.Contains(f.FullName),
                Library.Get(Path.GetFileNameWithoutExtension(f.Name)))
            {
                LastWrite = f.LastWriteTime,
                LastRun = Library.LastRun(Path.GetFileNameWithoutExtension(f.Name)),
                LastOutcome = Library.LastOutcome(Path.GetFileNameWithoutExtension(f.Name))
            });

        return Sorted(files).ToList();
    }

    /// <summary>
    /// Named categories first, alphabetically; ungrouped last; the chosen order inside a category.
    /// </summary>
    /// <remarks>
    /// The grouping is fixed and only the order within it is chosen. A sort that ran across the
    /// whole list would interleave the categories and leave every header describing nothing.
    /// </remarks>
    private IOrderedEnumerable<ScriptFile> Sorted(IEnumerable<ScriptFile> files)
    {
        IOrderedEnumerable<ScriptFile> grouped = files
            .OrderBy(file => !file.HasCategory)
            .ThenBy(file => file.CategoryName, StringComparer.CurrentCultureIgnoreCase);

        return Library.Sort switch
        {
            LibrarySort.Name => grouped.ThenBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase),

            // Never-run scripts sort as long ago rather than being dropped, so the list still holds
            // everything and they simply collect at the bottom. Ties fall back to the edit time,
            // which is the only other thing known about a script that has never run.
            LibrarySort.LastRun => grouped
                .ThenByDescending(file => file.LastRun ?? DateTime.MinValue)
                .ThenByDescending(file => file.LastWrite),

            _ => grouped.ThenByDescending(file => file.LastWrite)
        };
    }

    private static string SortLabel(LibrarySort sort) => sort switch
    {
        LibrarySort.Name => "Name",
        LibrarySort.LastRun => "Last run",
        _ => "Last edited"
    };

    internal static string Ago(DateTime time)
    {
        TimeSpan span = DateTime.Now - time;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return time.ToString("d MMM yyyy");
    }

    private void Apply(string filter)
    {
        string query = filter.Trim();
        List<ScriptFile> items = query.Length == 0
            ? _all
            : _all.Where(s =>
                s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (s.HasCategory && s.CategoryName.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

        // Rebuilding the view drops the selection, so the entry the user was on is put back.
        string? selected = Selected?.Path;

        var view = new ListCollectionView(items);
        if (items.Any(item => item.HasCategory))
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ScriptFile.CategoryName)));
        List.ItemsSource = view;

        if (selected is not null)
            List.SelectedItem = items.FirstOrDefault(item => item.Path == selected);

        List.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyLibrary.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptySearch.Visibility = _all.Count > 0 && items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ResultCount.Text = query.Length == 0
            ? CountText(_all.Count)
            : $"{items.Count} of {_all.Count}";
        EmptySearchHint.Text = query.Length == 0
            ? ""
            : $"Try another search for “{query}”.";
    }

    /// <summary>
    /// Whether a search is narrowing the list, in which case no category stays folded.
    /// </summary>
    /// <remarks>
    /// A match hidden inside a collapsed category reads as no match at all — the count says the
    /// script is there and nothing on screen shows it. Folds are remembered rather than cleared,
    /// so they come back when the search does.
    /// </remarks>
    private bool Searching => SearchBox.Text.Trim().Length > 0;

    private static string? CategoryNameOf(object? source) =>
        (source as FrameworkElement)?.DataContext is CollectionViewGroup { Name: string name }
            ? name
            : null;

    /// <summary>Opens or folds a category as it is realised, since the view is rebuilt constantly.</summary>
    private void OnCategoryLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton head || CategoryNameOf(sender) is not { } category)
            return;

        head.IsChecked = Searching || !Library.IsCollapsed(category);

    }

    private void OnCategoryMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || CategoryNameOf(menu.PlacementTarget) is not { } category)
            return;

        menu.Items.Clear();

        // Uncategorised is a bucket the library invents for scripts with no category, not a category anyone
        // named, so there is nothing here to rename, colour or take apart.
        if (string.Equals(category, Uncategorised, StringComparison.Ordinal))
        {
            menu.Items.Add(new MenuItem { Header = "No category to change", IsEnabled = false });
            return;
        }

        var rename = new MenuItem
        {
            Header = "Rename category…",
            Icon = new PackIcon { Kind = PackIconKind.RenameBox, Width = 16, Height = 16 }
        };
        rename.Click += (_, _) => RenameCategory(category);
        menu.Items.Add(rename);

        var uncategorise = new MenuItem
        {
            Header = "Clear category",
            Icon = new PackIcon { Kind = PackIconKind.CloseBoxMultipleOutline, Width = 16, Height = 16 }
        };
        uncategorise.Click += (_, _) => ClearCategory(category);
        menu.Items.Add(uncategorise);

    }

    private void RenameCategory(string category)
    {
        if (Window.GetWindow(this) is not { } owner)
            return;

        string? renamed = RenameDialog.Ask(owner, category, "Rename category", "Rename", PackIconKind.RenameBox);
        if (string.IsNullOrWhiteSpace(renamed) || string.Equals(renamed.Trim(), category, StringComparison.Ordinal))
            return;

        if (Library.RenameCategory(category, renamed) == 0)
            return;

        Refresh(RunningPaths());
    }

    /// <summary>
    /// Empties a category, leaving its scripts in place.
    /// </summary>
    /// <remarks>
    /// Confirmed, because it touches every script in the category and the only way back is to set the
    /// category again on each of them one at a time.
    /// </remarks>
    private void ClearCategory(string category)
    {
        if (Window.GetWindow(this) is not { } owner)
            return;

        int count = _all.Count(entry => string.Equals(entry.CategoryName, category, StringComparison.OrdinalIgnoreCase));
        if (!ConfirmDialog.Ask(
                owner,
                "Clear category",
                $"Take {(count == 1 ? "1 script" : $"{count} scripts")} out of “{category}”? The scripts stay.",
                "Clear"))
        {
            return;
        }

        Library.RemoveCategory(category);
        Refresh(RunningPaths());
    }

    /// <summary>The scripts currently running, which a reload has to be told about.</summary>
    private IReadOnlySet<string> RunningPaths() =>
        _all.Where(entry => entry.IsRunning).Select(entry => entry.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void OnCategoryExpanded(object sender, RoutedEventArgs e) => RememberCategory(sender, collapsed: false);

    private void OnCategoryCollapsed(object sender, RoutedEventArgs e) => RememberCategory(sender, collapsed: true);

    /// <summary>
    /// Records a fold, unless a search is what opened it.
    /// </summary>
    /// <remarks>
    /// Expanding everything for a search would otherwise be written down as the user's own choice,
    /// and clearing the box would leave every category open with no way back to how it was.
    /// </remarks>
    private void RememberCategory(object sender, bool collapsed)
    {
        if (Searching || CategoryNameOf(sender) is not { } category)
            return;

        Library.SetCollapsed(category, collapsed);
    }

    private void ApplyView()
    {
        bool grid = Library.View == LibraryView.Grid;
        ListToggle.IsChecked = !grid;
        GridToggle.IsChecked = grid;
        List.ItemContainerStyle = (Style)FindResource(grid ? "ScriptTileStyle" : "ScriptRowStyle");
        List.ItemsPanel = (ItemsPanelTemplate)FindResource(grid ? "ScriptTilePanel" : "ScriptRowPanel");
        SortLabelText.Text = SortLabel(Library.Sort);
    }

    /// <summary>A plain button rather than a split one, so the whole control opens the choices.</summary>
    private void OnSortClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is not { } menu)
            return;

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnSortMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;

        menu.Items.Clear();
        foreach (LibrarySort sort in new[] { LibrarySort.Modified, LibrarySort.LastRun, LibrarySort.Name })
        {
            var item = new MenuItem
            {
                Header = SortLabel(sort),
                IsCheckable = true,
                IsChecked = Library.Sort == sort
            };
            LibrarySort chosen = sort;
            item.Click += (_, _) => SetSort(chosen);
            menu.Items.Add(item);
        }
    }

    private void SetSort(LibrarySort sort)
    {
        if (Library.Sort == sort)
            return;

        Library.Sort = sort;
        _all = Sorted(_all).ToList();
        SortLabelText.Text = SortLabel(sort);
        Apply(SearchBox.Text);
    }

    private void SetView(LibraryView view)
    {
        Library.View = view;
        ApplyView();
    }

    private void OnSelectListView(object sender, RoutedEventArgs e) => SetView(LibraryView.List);

    private void OnSelectGridView(object sender, RoutedEventArgs e) => SetView(LibraryView.Grid);

    private void OnSearch(object sender, TextChangedEventArgs e) => Apply(SearchBox.Text);

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        // Down out of the search box steps into the results, so the list is reachable without the mouse.
        if (e.Key is Key.Down or Key.Enter && List.Items.Count > 0)
        {
            List.SelectedIndex = Math.Max(0, List.SelectedIndex);
            if (List.ItemContainerGenerator.ContainerFromIndex(List.SelectedIndex) is ListBoxItem item)
                item.Focus();
            else
                List.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
            return;

        // Escape clears a filter first, and only then leaves the library.
        if (SearchBox.Text.Length > 0)
            SearchBox.Clear();
        else
            DismissRequested?.Invoke();

        e.Handled = true;
    }

    /// <summary>Typing filters the library as soon as it opens.</summary>
    public void FocusSearch() =>
        _ui_tasks.Post(() => SearchBox.Focus(), DispatcherPriority.Input);

    private void OnNew(object sender, RoutedEventArgs e) => NewRequested?.Invoke();

    private void OpenCommunityScripts(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            Qx.Diagnostics.Diag.Error($"Could not open the community scripts website: {error.Message}", "library");
            MessageBox.Show(
                $"Could not open your browser. Visit {e.Uri.AbsoluteUri} to find community scripts.",
                "Community scripts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    /// <summary>Only a double-click that landed on an entry opens one; the empty space below does not.</summary>
    private void OnOpen(object sender, MouseButtonEventArgs e)
    {
        if (ContainerFrom(e.OriginalSource)?.DataContext is ScriptFile file)
            OpenRequested?.Invoke(file.Path);
    }

    private void OpenSelected()
    {
        if (Selected is { } file)
            OpenRequested?.Invoke(file.Path);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                OpenSelected();
                e.Handled = true;
                break;
            case Key.Delete when Selected is { CanDelete: true } file:
                DeleteRequested?.Invoke(file.Path);
                e.Handled = true;
                break;
            case Key.F2 when Selected is { } target:
                RenameRequested?.Invoke(target.Path);
                e.Handled = true;
                break;
            case Key.Escape:
                DismissRequested?.Invoke();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Right-click selects the item under the cursor, so the highlight matches the menu.</summary>
    private void OnItemRightClick(object sender, MouseButtonEventArgs e)
    {
        if (ContainerFrom(e.OriginalSource) is { } item)
            item.IsSelected = true;
    }

    private static ListBoxItem? ContainerFrom(object source) => TreeLookup.Ancestor<ListBoxItem>(source);

    /// <summary>
    /// The entry the context menu was opened on, which is not the same thing as the selection: the
    /// grouped view keeps a current item of its own, so a menu driven off SelectedItem acts on the
    /// first row no matter which one was clicked.
    /// </summary>
    private static ScriptFile? MenuTarget(object sender) =>
        ((sender as MenuItem)?.Parent as ContextMenu)?.PlacementTarget is FrameworkElement target
            ? target.DataContext as ScriptFile
            : null;

    private ScriptFile? Selected => List.SelectedItem as ScriptFile;

    private void OnMenuOpen(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { } file)
            OpenRequested?.Invoke(file.Path);
    }

    private void OnMenuRename(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { } file)
            RenameRequested?.Invoke(file.Path);
    }

    private void OnMenuCategory(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { } file)
            ChooseCategory(file);
    }

    private void OnMenuDuplicate(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { } file)
            DuplicateRequested?.Invoke(file.Path);
    }

    private void OnMenuReveal(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { } file)
            RevealRequested?.Invoke(file.Path);
    }

    private void OnMenuDelete(object sender, RoutedEventArgs e)
    {
        if (MenuTarget(sender) is { CanDelete: true } file)
            DeleteRequested?.Invoke(file.Path);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { DataContext: ScriptFile file } && file.CanDelete)
            DeleteRequested?.Invoke(file.Path);
    }

    private void ChooseCategory(ScriptFile file)
    {
        if (Window.GetWindow(this) is not { } owner)
            return;

        ScriptMeta? meta = CategoryDialog.Ask(
            owner,
            file.Name,
            file.Meta,
            Library.Categories);
        if (meta is null)
            return;

        Library.Set(file.Name, meta);

        _all = Sorted(_all.Select(entry => entry.Path == file.Path
            ? entry with { Meta = meta }
            : entry)).ToList();

        Apply(SearchBox.Text);
        List.SelectedItem = _all.FirstOrDefault(entry => entry.Path == file.Path);
    }

    private static string CountText(int count) => count == 1 ? "1 script" : $"{count} scripts";
}
