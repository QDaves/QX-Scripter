using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Game;
using Qx.Model;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomFurniViewModel : ViewModelBase
{
    static readonly FurniOrder _order = new();

    readonly RoomPageContext _context;
    readonly KeyedRows<string, FurniRowViewModel> _all = new(StringComparer.Ordinal);
    readonly Dictionary<string, FurniStackViewModel> _stacks = new(StringComparer.Ordinal);
    readonly Debouncer _refilter;
    readonly CoalescingSignal _progress_signal;
    readonly CoalescingSignal _items_signal;
    RoomSnapshot _snapshot = RoomSnapshot.Empty;
    object? _latest_progress;
    Area? _area;
    int _rotation_request;
    bool _attached;

    public RoomFurniViewModel(RoomPageContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _refilter = Own(new Debouncer(context.Dispatcher, context.Time, RoomPeopleViewModel.FilterDelay, ApplyFilter));
        _progress_signal = new CoalescingSignal(context.Dispatcher, ApplyProgress);
        _items_signal = new CoalescingSignal(context.Dispatcher, RefreshMenu);
        Selection.Changed += OnSelectionChanged;
        StackSelection.Changed += OnSelectionChanged;
        Rows.Applied += OnRowsApplied;
        Own(() =>
        {
            Selection.Changed -= OnSelectionChanged;
            StackSelection.Changed -= OnSelectionChanged;
            Rows.Applied -= OnRowsApplied;
        });
    }

    public FilteredRows<FurniRowViewModel> Rows { get; } = new();

    public ResettableCollection<FurniStackViewModel> Stacks { get; } = [];

    public SelectionList<FurniRowViewModel> Selection { get; } = new();

    public SelectionList<FurniStackViewModel> StackSelection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public ViewState State { get; } = new();

    public ObservableCollection<RotationOptionViewModel> RotationOptions { get; } = [];

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGrid), nameof(ViewMode))]
    public partial int ViewIndex { get; set; }

    [ObservableProperty]
    public partial OperationProgress? Progress { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArea))]
    public partial string AreaText { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsPicking { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHidden))]
    public partial int HiddenCount { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(HideCommand))]
    public partial bool CanHide { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowAgainCommand))]
    public partial bool CanShow { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool CanToggle { get; private set; }

    [ObservableProperty]
    public partial bool HasSelection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveCommand))]
    public partial bool CanMove { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PickupCommand))]
    public partial bool CanPickup { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EjectCommand))]
    public partial bool CanEject { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyIdsCommand))]
    public partial bool CanCopyIds { get; private set; }

    [ObservableProperty]
    public partial string HideText { get; private set; } = "Hide";

    [ObservableProperty]
    public partial string ShowText { get; private set; } = "Show";

    [ObservableProperty]
    public partial string ToggleText { get; private set; } = "Toggle";

    [ObservableProperty]
    public partial string PickupText { get; private set; } = "Pick up";

    [ObservableProperty]
    public partial string EjectText { get; private set; } = "Eject";

    [ObservableProperty]
    public partial string CopyIdsText { get; private set; } = "Copy id";

    [ObservableProperty]
    public partial string ShowHiddenText { get; private set; } = "";

    public FurniViewMode ViewMode => ViewIndex == 1 ? FurniViewMode.Grid : FurniViewMode.List;

    public bool IsGrid => ViewIndex == 1;

    public bool HasArea => AreaText.Length > 0;

    public bool HasHidden => HiddenCount > 0;

    public void Start()
    {
        if (_attached)
            return;
        _attached = true;
        GameState game = _context.Gateway.Game;
        game.RoomActions.Progressed += OnProgress;
        game.Room.FloorItemUpdated += OnFloorItem;
        game.Room.WallItemUpdated += OnWallItem;
        ApplyProgress();
    }

    public void Stop()
    {
        if (!_attached)
            return;
        _attached = false;
        GameState game = _context.Gateway.Game;
        game.RoomActions.Progressed -= OnProgress;
        game.Room.FloorItemUpdated -= OnFloorItem;
        game.Room.WallItemUpdated -= OnWallItem;
    }

    public void Show(RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        bool moved = snapshot.Identity.Generation != _snapshot.Identity.Generation;
        _snapshot = snapshot;
        if (moved)
        {
            _area = null;
            AreaText = "";
        }
        HiddenCount = snapshot.HiddenCount;
        ShowHiddenText = $"Show {snapshot.HiddenCount}";
        RoomFurniPiece[] scope = Scope(snapshot.Furni);
        _all.Sync(
            scope,
            FurniRowViewModel.KeyOf,
            piece => FurniRowViewModel.From(piece, _context.WebHost),
            (row, piece) => row.Take(piece, _context.WebHost));
        SortRefresh.Request();
        ApplyFilter();
    }

    public bool ClearFilter()
    {
        if (Filter.Length == 0)
            return false;
        Filter = "";
        return true;
    }

    public void Release()
    {
        if (IsPicking)
            _context.Gateway.Game.RoomActions.Cancel();
        ClearArea();
    }

    [RelayCommand]
    public void RefreshMenu()
    {
        IReadOnlyList<Furni> picked = Chosen();
        RoomActions actions = _context.Gateway.Game.RoomActions;
        bool busy = actions.IsBusy;
        bool any_floor = picked.Any(item => item is FloorItem);
        Id? self = _context.Self;
        CanHide = picked.Any(item => !item.IsHidden);
        CanShow = picked.Any(item => item.IsHidden);
        CanToggle = !busy && picked.Count > 0;
        CanMove = !busy && any_floor;
        CanPickup = !busy && self is { } mine && picked.Any(item => item.OwnerId == mine);
        CanEject = !busy && _snapshot.Identity.IsOwner && self is { } owner && picked.Any(item => item.OwnerId != owner);
        CanCopyIds = picked.Count > 0;
        HasSelection = picked.Count > 0;
        int count = picked.Count;
        HideText = count > 1 ? $"Hide {count}" : "Hide";
        ShowText = count > 1 ? $"Show {count}" : "Show";
        ToggleText = count > 1 ? $"Toggle {count}" : "Toggle";
        PickupText = count > 1 ? $"Pick up {count}" : "Pick up";
        EjectText = count > 1 ? $"Eject {count}" : "Eject";
        CopyIdsText = count > 1 ? $"Copy {count} ids" : "Copy id";
        StartRotations(picked, busy);
    }

    [RelayCommand(CanExecute = nameof(CanHide))]
    void Hide()
    {
        RoomActions actions = _context.Gateway.Game.RoomActions;
        try
        {
            foreach (Furni item in Chosen())
                actions.Hide(item);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail("Could not hide that", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanShow))]
    void ShowAgain()
    {
        RoomActions actions = _context.Gateway.Game.RoomActions;
        try
        {
            foreach (Furni item in Chosen())
                actions.Show(item);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail("Could not put that back", error);
        }
    }

    [RelayCommand]
    void ShowHidden()
    {
        try
        {
            int shown = _context.Gateway.Game.RoomActions.ShowAll();
            _context.Say(shown == 1 ? "Put 1 piece back on screen." : $"Put {shown} pieces back on screen.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail("Could not put the hidden furni back", error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanToggle))]
    Task ToggleAsync(CancellationToken cancellation_token) =>
        RunAsync("Could not press that", (actions, items, token) => actions.ToggleAsync(items, token), cancellation_token);

    [RelayCommand]
    Task RotateAsync(RotationOptionViewModel? option, CancellationToken cancellation_token) =>
        option is null || !option.HasDirection
            ? Task.CompletedTask
            : RunAsync(
                "Could not turn that",
                (actions, items, token) => actions.RotateAsync(items, option.Direction, token),
                cancellation_token);

    [RelayCommand(CanExecute = nameof(CanMove))]
    Task MoveAsync(CancellationToken cancellation_token) =>
        RunAsync("Could not move that", (actions, items, token) => actions.MoveAsync(items, token), cancellation_token);

    [RelayCommand(CanExecute = nameof(CanPickup))]
    async Task PickupAsync(CancellationToken cancellation_token)
    {
        IReadOnlyList<Furni> picked = Chosen();
        if (picked.Count > 1 && !await _context.Dialogs.ConfirmAsync(
            $"Pick up {picked.Count} pieces?",
            "They go back into your inventory, one after another.",
            "Pick up",
            DialogTone.Destructive,
            cancellation_token: cancellation_token))
        {
            return;
        }
        await RunAsync("Could not pick that up", (actions, items, token) => actions.PickupAsync(items, token), cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanEject))]
    async Task EjectAsync(CancellationToken cancellation_token)
    {
        IReadOnlyList<Furni> picked = Chosen();
        if (picked.Count > 1 && !await _context.Dialogs.ConfirmAsync(
            $"Eject {picked.Count} pieces?",
            "They go back to the people who own them, one after another.",
            "Eject",
            DialogTone.Destructive,
            cancellation_token: cancellation_token))
        {
            return;
        }
        await RunAsync("Could not eject that", (actions, items, token) => actions.EjectAsync(items, token), cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanCopyIds))]
    async Task CopyIdsAsync(CancellationToken cancellation_token)
    {
        IReadOnlyList<Furni> picked = Chosen();
        string ids = string.Join(", ", picked.Select(item => item.Id));
        await _context.CopyAsync(ids, picked.Count == 1 ? "Id copied." : $"Copied {picked.Count} ids.", cancellation_token);
    }

    [RelayCommand]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        string[] lines = IsGrid
            ? [.. StackSelection.Items.Select(stack => string.Join('	', stack.Name, stack.Count))]
            : [.. Selection.Items.Select(row => string.Join('	', row.Name, row.Position, row.IdText))];
        if (lines.Length == 0)
            return;
        await _context.CopyAsync(
            string.Join(Environment.NewLine, lines),
            lines.Length == 1 ? "Copied 1 row." : $"Copied {lines.Length} rows.",
            cancellation_token);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    async Task PickAreaAsync(CancellationToken cancellation_token)
    {
        RoomActions actions = _context.Gateway.Game.RoomActions;
        if (IsPicking)
        {
            actions.Cancel();
            return;
        }
        if (actions.IsBusy)
        {
            _context.Warn("Something is already running.");
            return;
        }
        IsPicking = true;
        try
        {
            Area? picked = await actions.SelectAreaAsync(cancellation_token);
            if (picked is { } area)
            {
                _area = area;
                AreaText = $"({area.X1}, {area.Y1})–({area.X2}, {area.Y2})";
                Show(_snapshot);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail("Could not take that area", error);
        }
        finally
        {
            IsPicking = false;
        }
    }

    [RelayCommand]
    void ClearArea()
    {
        if (!HasArea)
            return;
        AreaText = "";
        _area = null;
        Show(_snapshot);
    }

    [RelayCommand]
    void StopRun() => _context.Gateway.Game.RoomActions.Cancel();

    [RelayCommand]
    void ClearFilters() => ClearFilter();

    protected override void OnDisposed()
    {
        Stop();
        base.OnDisposed();
    }

    RoomFurniPiece[] Scope(IReadOnlyList<RoomFurniPiece> pieces) =>
        _area is { } area
            ? [.. pieces.Where(piece => piece.IsFloor && area.Contains(piece.Tile))]
            : [.. pieces];

    IReadOnlyList<Furni> Chosen()
    {
        IEnumerable<Furni> picked = IsGrid
            ? StackSelection.Items.SelectMany(stack => stack.Items)
            : Selection.Items.Select(row => row.Item).OfType<Furni>();
        return RoomFurniSelection.Resolve(_context.Gateway.Game, [.. picked]);
    }

    async Task RunAsync(
        string trouble,
        Func<RoomActions, IReadOnlyList<Furni>, CancellationToken, Task> work,
        CancellationToken cancellation_token)
    {
        IReadOnlyList<Furni> picked = Chosen();
        if (picked.Count == 0)
            return;
        try
        {
            await work(_context.Gateway.Game.RoomActions, picked, cancellation_token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail(trouble, error);
        }
        finally
        {
            RefreshMenu();
        }
    }

    void StartRotations(IReadOnlyList<Furni> picked, bool busy)
    {
        int request = ++_rotation_request;
        FloorItem[] floor = [.. picked.OfType<FloorItem>()];
        if (floor.Length == 0)
        {
            Status("Floor items only");
            return;
        }
        Status("Loading directions…");
        ReadRotationsAsync(floor, request, busy, _context.Lifetime).Observe("ui");
    }

    void Status(string text)
    {
        RotationOptions.Clear();
        RotationOptions.Add(new RotationOptionViewModel(-1, text, null, false));
    }

    async Task ReadRotationsAsync(
        IReadOnlyList<FloorItem> floor,
        int request,
        bool busy,
        CancellationToken cancellation_token)
    {
        FurniData? data = _context.Gateway.Game.GameData.Furni;
        var supported = new List<IReadOnlyList<int>>(floor.Count);
        try
        {
            foreach (FloorItem item in floor)
            {
                FurniInfo? info = data?.GetInfo(item);
                supported.Add(info is null
                    ? []
                    : await _context.Directions.ForAsync(info.Revision, info.Identifier, cancellation_token));
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (request == _rotation_request)
                Status("Could not load directions");
            Diag.Warn($"Could not load furni directions: {error.Message}", "ui");
            return;
        }

        if (request != _rotation_request)
            return;
        int[] directions = RoomRotation.Options(supported, [.. floor.Select(item => item.Direction)]);
        if (directions.Length == 0)
        {
            Status("No other direction");
            return;
        }
        RotationOptions.Clear();
        foreach (int direction in directions)
            RotationOptions.Add(new RotationOptionViewModel(direction, RoomText.Compass(direction), RotateCommand, !busy));
    }

    void ApplyFilter()
    {
        string term = Filter.Trim();
        Rows.ApplyAsync(_all.Rows, row => Keep(row, term), _order, _context.Lifetime).Observe("ui");
    }

    static bool Keep(FurniRowViewModel row, string term)
    {
        if (term.Length == 0)
            return true;
        FurniSearch search = row.Search;
        return search.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            search.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    void OnRowsApplied()
    {
        Selection.Prune(Rows.Visible);
        Fold();
        Describe();
        RefreshMenu();
    }

    void Fold()
    {
        var wanted = new List<FurniStackViewModel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IGrouping<string, FurniRowViewModel> group in Rows.Visible
            .GroupBy(row => FurniStackViewModel.KeyOf(row.Placement, row.Kind), StringComparer.Ordinal))
        {
            FurniRowViewModel[] rows = [.. group];
            if (!_stacks.TryGetValue(group.Key, out FurniStackViewModel? stack))
            {
                stack = new FurniStackViewModel(group.Key);
                _stacks[group.Key] = stack;
            }
            stack.Take(rows[0], [.. rows.Select(row => row.Item).OfType<Furni>()], rows.Length);
            wanted.Add(stack);
            seen.Add(group.Key);
        }
        foreach (string stale in _stacks.Keys.Where(key => !seen.Contains(key)).ToArray())
            _stacks.Remove(stale);
        wanted.Sort(StackOrder);
        Stacks.ReplaceAll(wanted);
        StackSelection.Prune(wanted);
    }

    static int StackOrder(FurniStackViewModel left, FurniStackViewModel right) =>
        string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);

    void Describe()
    {
        if (!_snapshot.IsInRoom)
        {
            CountText = "";
            State.ShowReady();
            return;
        }
        int total = Rows.SourceCount;
        int visible = Rows.Visible.Count;
        int kinds = Stacks.Count;
        string counted = IsGrid
            ? $"{kinds} {(kinds == 1 ? "kind" : "kinds")}, {visible} in total"
            : $"{visible} {(visible == 1 ? "item" : "items")}";
        CountText = HasArea
            ? $"{counted} · area {AreaText} · {_snapshot.Furni.Count} in the room"
            : counted;
        if (_snapshot.Furni.Count == 0)
            State.ShowEmpty(IconKind.Furni, "Nothing is standing here", "This room holds no furni.");
        else if (total == 0 && HasArea)
            State.ShowEmpty(IconKind.Area, "Nothing in that area", "No floor item stands between those two corners.", ClearAreaCommand, "Clear the area");
        else if (visible == 0)
            State.ShowEmpty(IconKind.Search, "No matches", $"Nothing matches “{Filter.Trim()}”.", ClearFiltersCommand, "Clear filters");
        else
            State.ShowReady();
    }

    void ApplyProgress()
    {
        FurniProgress progress = Volatile.Read(ref _latest_progress) is FurniProgress latest
            ? latest
            : _context.Gateway.Game.RoomActions.Progress;
        Progress = progress.IsRunning
            ? new OperationProgress(progress.ToString(), null, StopRunCommand)
            : null;
        RefreshMenu();
    }

    void OnProgress(FurniProgress progress)
    {
        Volatile.Write(ref _latest_progress, progress);
        _progress_signal.Raise();
    }

    void OnFloorItem(FloorItem item) => _items_signal.Raise();

    void OnWallItem(WallItem item) => _items_signal.Raise();

    void OnSelectionChanged() => RefreshMenu();

    partial void OnFilterChanged(string value) => _refilter.Trigger();

    partial void OnViewIndexChanged(int value)
    {
        if (value == 1)
        {
            FurniStackViewModel[] kinds =
            [
                .. Stacks.Where(stack => Selection.Items.Any(row =>
                    FurniStackViewModel.KeyOf(row.Placement, row.Kind) == stack.Key))
            ];
            StackSelection.Replace(kinds);
            StackSelection.Select(kinds);
        }
        else
        {
            FurniRowViewModel[] pieces =
            [
                .. Rows.Visible.Where(row => StackSelection.Items.Any(stack =>
                    stack.Key == FurniStackViewModel.KeyOf(row.Placement, row.Kind)))
            ];
            Selection.Replace(pieces);
            Selection.Select(pieces);
        }
        Describe();
        RefreshMenu();
    }

    sealed class FurniOrder : IComparer<FurniRowViewModel>
    {
        public int Compare(FurniRowViewModel? left, FurniRowViewModel? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int order = string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
            return order != 0 ? order : left.ItemId.CompareTo(right.ItemId);
        }
    }
}
