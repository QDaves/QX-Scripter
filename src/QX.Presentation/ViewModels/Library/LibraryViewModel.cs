using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Library;

public sealed record LibrarySortOption(LibrarySort Sort, string Label);

public sealed partial class LibraryViewModel : PageViewModel
{
    public const string Subheading = "Open, organise and manage your saved scripts.";
    public const string CommunityUrl = "http://qxscripter.xyz/";
    public const string CommunityTitle = "Community scripts";
    public const string NewScriptText = "New script";
    public const string EmptyTitle = "No saved scripts yet";
    public const string EmptyMessage = "Create your first script to add it here.";
    public const string NoMatchTitle = "No matching scripts";

    public static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan ClockPeriod = TimeSpan.FromSeconds(30);

    readonly IScriptLibrary _library;
    readonly IScriptFileService _files;
    readonly IScriptFileCommands _commands;
    readonly IScriptRunRegistry _runs;
    readonly IDialogService _dialogs;
    readonly ILauncherService _launcher;
    readonly IUiDispatcher _dispatcher;
    readonly TimeProvider _time;
    readonly Dictionary<string, LibraryScriptRow> _scripts;
    readonly Dictionary<string, LibraryGroupRow> _groups = new(StringComparer.CurrentCultureIgnoreCase);
    readonly HashSet<string> _search_folds = new(StringComparer.CurrentCultureIgnoreCase);
    readonly SerialOperation _loading = new();
    readonly Debouncer _rescan;
    LibraryGroupRow? _uncategorised;
    IDisposable? _watch;
    ITimer? _clock;

    public LibraryViewModel(
        IScriptLibrary library,
        IScriptFileService files,
        IScriptFileCommands commands,
        IScriptRunRegistry runs,
        IDialogService dialogs,
        ILauncherService launcher,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _scripts = new Dictionary<string, LibraryScriptRow>(PathComparison.Comparer);
        _rescan = Own(new Debouncer(dispatcher, time, WatchDelay, Rescan));
        Subtitle = Subheading;
        Sorts = [new LibrarySortOption(LibrarySort.Modified, "Last edited"), new LibrarySortOption(LibrarySort.LastRun, "Last run"), new LibrarySortOption(LibrarySort.Name, "Name")];
        SelectedSort = OptionFor(_library.Sort);
        ViewIndex = _library.View == LibraryView.Grid ? 1 : 0;
        _library.Changed += OnLibraryChanged;
        _runs.Changed += OnRunsChanged;
        Own(() =>
        {
            _library.Changed -= OnLibraryChanged;
            _runs.Changed -= OnRunsChanged;
        });
        Apply();
    }

    public ObservableCollection<LibraryRow> Rows { get; } = [];

    public SelectionList<LibraryScriptRow> Selection { get; } = new();

    public IReadOnlyList<LibrarySortOption> Sorts { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortsByName), nameof(SortsByLastRun), nameof(SortsByEdited), nameof(SortIcon))]
    public partial LibrarySortOption SelectedSort { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGrid))]
    public partial int ViewIndex { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial bool SearchFocusRequested { get; set; }

    public bool IsGrid => ViewIndex == 1;

    public bool IsSearching => SearchText.Trim().Length > 0;

    public bool SortsByName => SelectedSort.Sort == LibrarySort.Name;

    public bool SortsByLastRun => SelectedSort.Sort == LibrarySort.LastRun;

    public bool SortsByEdited => SelectedSort.Sort == LibrarySort.Modified;

    public IconKind SortIcon => SortsByName ? IconKind.ArrowUp : IconKind.ArrowDown;

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        SearchText = "";
        return true;
    }

    public LibraryScriptRow? SelectFirstResult()
    {
        if ((Selection.First ?? Rows.OfType<LibraryScriptRow>().FirstOrDefault()) is not { } wanted)
            return null;
        Selection.Select([wanted]);
        return wanted;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        SearchText = "";
        _watch ??= _files.Watch(_rescan.Trigger);
        _clock ??= _time.CreateTimer(static state => ((LibraryViewModel)state!).Tick(), this, ClockPeriod, ClockPeriod);
        SearchFocusRequested = true;
        await ReloadAsync(cancellation_token);
    }

    protected override void OnDeactivated()
    {
        _watch?.Dispose();
        _watch = null;
        _clock?.Dispose();
        _clock = null;
        _rescan.Cancel();
    }

    [RelayCommand]
    void NewScript() => _commands.NewDocument();

    [RelayCommand]
    void SortBy(LibrarySort sort) => SelectedSort = OptionFor(sort);

    [RelayCommand]
    async Task OpenAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { } script)
            return;
        await _commands.OpenAsync(script.Path, cancellation_token);
    }

    [RelayCommand]
    async Task RenameAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { } script)
            return;
        if (await _commands.RenameFileAsync(script.Path, cancellation_token))
            await ReloadAsync(cancellation_token);
    }

    [RelayCommand]
    async Task DuplicateAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { } script)
            return;
        if (await _commands.DuplicateAsync(script.Path, cancellation_token))
            await ReloadAsync(cancellation_token);
    }

    [RelayCommand]
    async Task RevealAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { } script)
            return;
        await _commands.RevealAsync(script.Path, cancellation_token);
    }

    [RelayCommand]
    async Task DeleteAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { CanDelete: true } script)
            return;
        if (await _commands.DeleteAsync(script.Path, cancellation_token))
            await ReloadAsync(cancellation_token);
    }

    [RelayCommand]
    async Task CategoryAsync(LibraryScriptRow? row, CancellationToken cancellation_token)
    {
        if (Target(row) is not { } script)
            return;
        var dialog = new CategoryDialogViewModel(script.Name, _library.Get(script.Name), _library.Categories);
        ScriptMeta? meta = await _dialogs.ShowAsync(dialog, cancellation_token);
        if (meta is null || !_scripts.ContainsKey(script.Path))
            return;
        _library.Set(script.Name, meta);
        Selection.Select([script]);
    }

    [RelayCommand]
    void ToggleGroup(LibraryGroupRow? group)
    {
        if (group is null)
            return;
        bool expanded = !group.IsExpanded;
        group.IsExpanded = expanded;
        if (IsSearching)
        {
            if (expanded)
                _search_folds.Remove(group.Name);
            else
                _search_folds.Add(group.Name);
        }
        else
        {
            _library.SetCollapsed(group.Name, !expanded);
        }
        Apply();
    }

    [RelayCommand]
    async Task RenameCategoryAsync(LibraryGroupRow? group, CancellationToken cancellation_token)
    {
        if (group is not { CanEdit: true })
            return;
        var request = new PromptRequest("Rename category", group.Name, "Rename", "Category name", IconKind.Category);
        string? typed = await _dialogs.PromptAsync(request, cancellation_token);
        if (typed is null)
            return;
        string wanted = typed.Trim();
        if (wanted.Length == 0 || string.Equals(wanted, group.Name, StringComparison.Ordinal))
            return;
        _library.RenameCategory(group.Name, wanted);
    }

    [RelayCommand]
    async Task ClearCategoryAsync(LibraryGroupRow? group, CancellationToken cancellation_token)
    {
        if (group is not { CanEdit: true })
            return;
        int count = _scripts.Values.Count(row => row.HasCategory && string.Equals(row.CategoryName, group.Name, StringComparison.CurrentCultureIgnoreCase));
        if (count == 0)
            return;
        string scripts = count == 1 ? "1 script" : $"{count} scripts";
        bool confirmed = await _dialogs.ConfirmAsync(
            "Clear category",
            $"Take {scripts} out of “{group.Name}”? The scripts stay.",
            "Clear",
            cancellation_token: cancellation_token);
        if (!confirmed)
            return;
        _library.RemoveCategory(group.Name);
    }

    [RelayCommand]
    async Task CommunityAsync(CancellationToken cancellation_token)
    {
        if (await _launcher.OpenUriAsync(new Uri(CommunityUrl), cancellation_token))
            return;
        Diag.Error($"Could not open the community scripts website: {CommunityUrl}", "library");
        await _dialogs.AlertAsync(CommunityTitle, $"Could not open your browser. Visit {CommunityUrl} to find community scripts.", cancellation_token);
    }

    partial void OnSearchTextChanged(string value)
    {
        if (value.Trim().Length == 0)
            _search_folds.Clear();
        Apply();
    }

    partial void OnSelectedSortChanged(LibrarySortOption value)
    {
        if (_library.Sort == value.Sort)
            return;
        _library.Sort = value.Sort;
        Apply();
    }

    partial void OnViewIndexChanged(int value)
    {
        LibraryView wanted = value == 1 ? LibraryView.Grid : LibraryView.List;
        if (_library.View != wanted)
            _library.View = wanted;
        Apply();
    }

    LibrarySortOption OptionFor(LibrarySort sort) => Sorts.FirstOrDefault(option => option.Sort == sort) ?? Sorts[0];

    LibraryScriptRow? Target(LibraryScriptRow? row) => row ?? Selection.First;

    void Rescan() => ReloadAsync(ActivationToken).Observe("library");

    void Tick() => _dispatcher.Post(RefreshTexts, UiPriority.Background);

    void OnLibraryChanged()
    {
        SelectedSort = OptionFor(_library.Sort);
        ViewIndex = _library.View == LibraryView.Grid ? 1 : 0;
        RefreshRows();
        Apply();
    }

    void OnRunsChanged() => RefreshRows();

    async Task ReloadAsync(CancellationToken cancellation_token)
    {
        OperationLease lease = await _loading.StartAsync(cancellation_token);
        try
        {
            IReadOnlyList<ScriptFileEntry> entries = await _files.ListAsync(lease.Token);
            if (!_loading.IsCurrent(lease))
                return;
            Load(entries);
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Diag.Warn($"The script folder could not be read: {error.Message}", "library");
        }
        finally
        {
            _loading.Complete(lease);
        }
    }

    void Load(IReadOnlyList<ScriptFileEntry> entries)
    {
        DateTimeOffset now = _time.GetUtcNow();
        var found = new HashSet<string>(PathComparison.Comparer);
        foreach (ScriptFileEntry entry in entries)
        {
            string full = PathComparison.Full(entry.Path);
            found.Add(full);
            if (!_scripts.TryGetValue(full, out LibraryScriptRow? row))
            {
                row = new LibraryScriptRow(full, entry.Name);
                _scripts[full] = row;
            }
            Sync(row, entry.EditedAt, now);
        }
        foreach (string gone in _scripts.Keys.Where(path => !found.Contains(path)).ToArray())
            _scripts.Remove(gone);
        Apply();
    }

    void RefreshRows()
    {
        DateTimeOffset now = _time.GetUtcNow();
        foreach (LibraryScriptRow row in _scripts.Values)
            Sync(row, row.EditedAt, now);
    }

    void RefreshTexts()
    {
        DateTimeOffset now = _time.GetUtcNow();
        foreach (LibraryScriptRow row in _scripts.Values)
            row.Refresh(now);
    }

    void Sync(LibraryScriptRow row, DateTimeOffset edited_at, DateTimeOffset now)
    {
        bool working = _runs.WorkingPaths.Contains(row.Path);
        bool armed = !working && _runs.LivePaths.Contains(row.Path);
        row.Apply(edited_at, _library.Get(row.Name).Category, _library.LastRunOf(row.Name), working, armed, now);
    }

    void Apply()
    {
        string query = SearchText.Trim();
        bool searching = query.Length > 0;
        List<LibraryScriptRow> visible = [.. _scripts.Values.Where(row => Keep(row, query))];
        visible.Sort(Compare);
        List<LibraryRow> next = Flatten(visible, searching);
        string? selected = Selection.First?.Path;
        CollectionSync.Sync(Rows, next, KeyOf, KeyOf, static row => row, static (row, item) => _ = item);
        if (selected is not null &&
            Selection.First is null &&
            Rows.OfType<LibraryScriptRow>().FirstOrDefault(row => PathComparison.Same(row.Path, selected)) is { } again)
        {
            Selection.Select([again]);
        }
        CountText = searching
            ? $"{visible.Count} of {_scripts.Count}"
            : _scripts.Count == 1 ? "1 script" : $"{_scripts.Count} scripts";
        if (_scripts.Count == 0)
            State.ShowEmpty(IconKind.FileText, EmptyTitle, EmptyMessage, NewScriptCommand, NewScriptText);
        else if (visible.Count == 0)
            State.ShowEmpty(IconKind.Search, NoMatchTitle, $"Try another search for “{query}”.");
        else
            State.ShowReady();
    }

    List<LibraryRow> Flatten(List<LibraryScriptRow> visible, bool searching)
    {
        var next = new List<LibraryRow>(visible.Count + 4);
        bool grouped = visible.Any(row => row.HasCategory);
        foreach (LibraryScriptRow row in visible)
            row.ShowCategory = !grouped && row.HasCategory;
        if (!grouped)
        {
            next.AddRange(visible);
            return next;
        }
        int index = 0;
        while (index < visible.Count)
        {
            LibraryScriptRow first = visible[index];
            int end = index;
            while (end < visible.Count && SameGroup(first, visible[end]))
                end++;
            LibraryGroupRow group = GroupFor(first.CategoryName, !first.HasCategory);
            group.Count = end - index;
            group.IsExpanded = searching ? !_search_folds.Contains(group.Name) : !_library.IsCollapsed(group.Name);
            next.Add(group);
            if (group.IsExpanded)
                next.AddRange(visible.GetRange(index, end - index));
            index = end;
        }
        return next;
    }

    LibraryGroupRow GroupFor(string name, bool invented)
    {
        if (invented)
            return _uncategorised ??= new LibraryGroupRow(name, invented: true);
        if (!_groups.TryGetValue(name, out LibraryGroupRow? group) || !string.Equals(group.Name, name, StringComparison.Ordinal))
        {
            group = new LibraryGroupRow(name, invented: false);
            _groups[name] = group;
        }
        return group;
    }

    int Compare(LibraryScriptRow left, LibraryScriptRow right)
    {
        if (left.HasCategory != right.HasCategory)
            return left.HasCategory ? -1 : 1;
        int category = string.Compare(left.CategoryName, right.CategoryName, StringComparison.CurrentCultureIgnoreCase);
        if (category != 0)
            return category;
        int inside = _library.Sort switch
        {
            LibrarySort.Name => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase),
            LibrarySort.LastRun => CompareRuns(left, right),
            _ => right.EditedAt.CompareTo(left.EditedAt)
        };
        if (inside != 0)
            return inside;
        int name = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
        return name != 0 ? name : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
    }

    static int CompareRuns(LibraryScriptRow left, LibraryScriptRow right)
    {
        int run = (right.LastRun?.At ?? DateTimeOffset.MinValue).CompareTo(left.LastRun?.At ?? DateTimeOffset.MinValue);
        return run != 0 ? run : right.EditedAt.CompareTo(left.EditedAt);
    }

    static bool SameGroup(LibraryScriptRow first, LibraryScriptRow other) =>
        first.HasCategory == other.HasCategory &&
        string.Equals(first.CategoryName, other.CategoryName, StringComparison.CurrentCultureIgnoreCase);

    static string KeyOf(LibraryRow row) => row switch
    {
        LibraryGroupRow group => group.IsInvented ? "\0" : "#" + group.Name,
        LibraryScriptRow script => "!" + script.Path,
        _ => "?"
    };

    static bool Keep(LibraryScriptRow row, string query) =>
        query.Length == 0 ||
        row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        (row.HasCategory && row.CategoryName.Contains(query, StringComparison.OrdinalIgnoreCase));
}
