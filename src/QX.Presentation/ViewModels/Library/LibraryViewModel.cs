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
    public const string Subheading = "Your scripts, ready when you need them.";
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
    readonly Dictionary<string, LibraryFilterOption> _filters = new(StringComparer.CurrentCultureIgnoreCase);
    readonly LibraryFilterOption _all = new("all", "All scripts", null);
    readonly LibraryFilterOption _active = new("active", "Active scripts", null);
    readonly LibraryFilterOption _ungrouped = new("ungrouped", "Uncategorised", null);
    readonly SerialOperation _loading = new();
    readonly Debouncer _rescan;
    IDisposable? _watch;
    ITimer? _clock;
    bool _applying;

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
        SelectedFilter = _all;
        _library.Changed += OnLibraryChanged;
        _runs.Changed += OnRunsChanged;
        Own(() =>
        {
            _library.Changed -= OnLibraryChanged;
            _runs.Changed -= OnRunsChanged;
        });
        Apply();
    }

    public ObservableCollection<LibraryScriptRow> Rows { get; } = [];

    public SelectionList<LibraryScriptRow> Selection { get; } = new();

    public IReadOnlyList<LibrarySortOption> Sorts { get; }

    [ObservableProperty]
    public partial LibrarySortOption SelectedSort { get; set; }

    public ObservableCollection<LibraryFilterOption> Filters { get; } = [];

    public IReadOnlyList<LibraryFilterOption> FixedFilters => [_all, _active, _ungrouped];

    public ObservableCollection<LibraryFilterOption> CategoryFilters { get; } = [];

    public bool HasCategories => CategoryFilters.Count > 0;

    public LibraryFilterOption? SelectedFixedFilter
    {
        get => SelectedFilter is { CanEdit: false } filter ? filter : null;
        set { if (value is not null) SelectedFilter = value; }
    }

    public LibraryFilterOption? SelectedCategory
    {
        get => SelectedFilter is { CanEdit: true } filter ? filter : null;
        set { if (value is not null) SelectedFilter = value; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterTitle))]
    public partial LibraryFilterOption? SelectedFilter { get; set; }

    [ObservableProperty]
    public partial bool HasResults { get; private set; }

    [ObservableProperty]
    public partial string EmptyHeading { get; private set; } = EmptyTitle;

    [ObservableProperty]
    public partial string EmptyDetail { get; private set; } = EmptyMessage;

    public string FilterTitle => SelectedFilter?.Name ?? _all.Name;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial bool SearchFocusRequested { get; set; }

    public override bool TryClearSearch()
    {
        if (SearchText.Length == 0)
            return false;
        SearchText = "";
        return true;
    }

    public LibraryScriptRow? SelectFirstResult()
    {
        if ((Selection.First ?? Rows.FirstOrDefault()) is not { } wanted)
            return null;
        Selection.Select([wanted]);
        return wanted;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellationToken)
    {
        SearchText = "";
        _watch ??= _files.Watch(_rescan.Trigger);
        _clock ??= _time.CreateTimer(static state => ((LibraryViewModel)state!).Tick(), this, ClockPeriod, ClockPeriod);
        SearchFocusRequested = true;
        await ReloadAsync(cancellationToken);
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
        ScriptMeta current = script.DeclaredGroup is { } declared ? new ScriptMeta { Category = declared } : _library.Get(script.Name);
        var dialog = new CategoryDialogViewModel(script.Name, current, Categories());
        ScriptMeta? meta = await _dialogs.ShowAsync(dialog, cancellation_token);
        if (meta is null || !_scripts.ContainsKey(script.Path))
            return;
        if (script.DeclaredGroup is not null)
            await _commands.SetGroupAsync(script.Path, meta.Category, cancellation_token);
        else
            _library.Set(script.Name, meta);
        await ReloadAsync(cancellation_token);
        Selection.Select([script]);
    }

    [RelayCommand]
    void ClearFilters()
    {
        SearchText = "";
        SelectedFilter = _all;
    }

    [RelayCommand]
    async Task OpenFolderAsync(CancellationToken cancellation_token)
    {
        if (!await _launcher.OpenFolderAsync(_files.ScriptsDirectory, cancellation_token))
            await _dialogs.AlertAsync("Could not open the folder", "The scripts folder could not be opened.", cancellation_token);
    }

    [RelayCommand]
    async Task RenameCategoryAsync(LibraryFilterOption? group, CancellationToken cancellation_token)
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
        await RegroupDeclaredAsync(group.Name, wanted, cancellation_token);
    }

    [RelayCommand]
    async Task ClearCategoryAsync(LibraryFilterOption? group, CancellationToken cancellation_token)
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
            cancellationToken: cancellation_token);
        if (!confirmed)
            return;
        _library.RemoveCategory(group.Name);
        await RegroupDeclaredAsync(group.Name, null, cancellation_token);
    }

    async Task RegroupDeclaredAsync(string from, string? to, CancellationToken cancellation_token)
    {
        LibraryScriptRow[] declared =
        [
            .. _scripts.Values.Where(row => string.Equals(row.DeclaredGroup, from, StringComparison.CurrentCultureIgnoreCase))
        ];
        foreach (LibraryScriptRow row in declared)
            await _commands.SetGroupAsync(row.Path, to, cancellation_token);
        if (declared.Length > 0)
            await ReloadAsync(cancellation_token);
    }

    IReadOnlyList<string> Categories() =>
    [
        .. _library.Categories
            .Concat(_scripts.Values.Where(row => row.HasCategory).Select(row => row.CategoryName))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
    ];

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
        Apply();
    }

    partial void OnSelectedSortChanged(LibrarySortOption value)
    {
        if (_library.Sort == value.Sort)
            return;
        _library.Sort = value.Sort;
        Apply();
    }

    partial void OnSelectedFilterChanged(LibraryFilterOption? value)
    {
        OnPropertyChanged(nameof(SelectedFixedFilter));
        OnPropertyChanged(nameof(SelectedCategory));
        Apply();
    }

    LibrarySortOption OptionFor(LibrarySort sort) => Sorts.FirstOrDefault(option => option.Sort == sort) ?? Sorts[0];

    LibraryScriptRow? Target(LibraryScriptRow? row) => row ?? Selection.First;

    void Rescan() => ReloadAsync(ActivationToken).Observe("library");

    void Tick() => _dispatcher.Post(RefreshTexts, UiPriority.Background);

    void OnLibraryChanged()
    {
        SelectedSort = OptionFor(_library.Sort);
        RefreshRows();
        Apply();
    }

    void OnRunsChanged()
    {
        RefreshRows();
        Apply();
    }

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
            row.DeclaredGroup = entry.Header.Group;
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
        row.Apply(edited_at, row.DeclaredGroup ?? _library.Get(row.Name).Category, _library.LastRunOf(row.Name), working, armed, now);
    }

    void Apply()
    {
        if (_applying)
            return;
        _applying = true;
        try
        {
            string query = SearchText.Trim();
            bool searching = query.Length > 0;
            update_filters();
            List<LibraryScriptRow> visible = [.. _scripts.Values.Where(row => matches_filter(row) && Keep(row, query))];
            visible.Sort(Compare);
            string? selected = Selection.First?.Path;
            CollectionSync.Sync(Rows, visible, static row => row.Path, static row => row.Path, static row => row, static (row, item) => _ = item);
            Selection.Prune(visible);
            if (selected is not null &&
                Selection.First is null &&
                Rows.FirstOrDefault(row => PathComparison.Same(row.Path, selected)) is { } again)
            {
                Selection.Select([again]);
            }
            CountText = visible.Count == 1 ? "1 script" : $"{visible.Count} scripts";
            HasResults = visible.Count > 0;
            EmptyHeading = _scripts.Count == 0 ? EmptyTitle : NoMatchTitle;
            EmptyDetail = _scripts.Count == 0 ? EmptyMessage
                : searching ? $"No scripts match “{query}”. Try another search or category."
                : "There are no scripts in this view. Choose another category or create a script.";
            State.ShowReady();
        }
        finally
        {
            _applying = false;
        }
    }

    void update_filters()
    {
        string selected_key = SelectedFilter?.Key ?? _all.Key;
        _all.Count = _scripts.Count;
        _active.Count = _scripts.Values.Count(row => row.IsRunning || row.IsReady);
        _ungrouped.Count = _scripts.Values.Count(row => !row.HasCategory);
        var next = new List<LibraryFilterOption> { _all, _active, _ungrouped };
        foreach (var category in _scripts.Values.Where(row => row.HasCategory)
            .GroupBy(row => row.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            string key = "category:" + category.Key;
            if (!_filters.TryGetValue(key, out var filter) || filter.Name != category.Key)
                _filters[key] = filter = new LibraryFilterOption(key, category.Key, category.Key);
            filter.Count = category.Count();
            next.Add(filter);
        }
        CollectionSync.Sync(Filters, next, static filter => filter.Key, static filter => filter.Key,
            static filter => filter, static (filter, item) => _ = item);
        CollectionSync.Sync(CategoryFilters, next.Where(filter => filter.CanEdit).ToList(), static filter => filter.Key, static filter => filter.Key,
            static filter => filter, static (filter, item) => _ = item);
        OnPropertyChanged(nameof(HasCategories));
        SelectedFilter = Filters.FirstOrDefault(filter => string.Equals(filter.Key, selected_key, StringComparison.CurrentCultureIgnoreCase)) ?? _all;
    }

    bool matches_filter(LibraryScriptRow row) => SelectedFilter?.Key switch
    {
        "active" => row.IsRunning || row.IsReady,
        "ungrouped" => !row.HasCategory,
        _ when SelectedFilter?.Category is { } category => string.Equals(row.CategoryName, category, StringComparison.CurrentCultureIgnoreCase),
        _ => true
    };

    int Compare(LibraryScriptRow left, LibraryScriptRow right)
    {
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

    static bool Keep(LibraryScriptRow row, string query) =>
        query.Length == 0 ||
        row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        (row.HasCategory && row.CategoryName.Contains(query, StringComparison.OrdinalIgnoreCase));
}
