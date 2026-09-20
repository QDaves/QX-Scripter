using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Navigator;
using Qx.Presentation.Services.Notifications;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.Navigator;

public sealed partial class NavigatorViewModel : PageViewModel
{
    public const string ChooseASearch = "Choose a search to find rooms.";
    public const string NoRoomsMatched = "No rooms matched this search.";
    public const string EnterASearchTerm = "Enter a search term.";
    public const string Searching = "Searching…";
    public const string SearchRooms = "Search rooms";
    public const string OptionalTag = "Optional tag";
    public const string ClipboardUnreachable = "Could not reach the clipboard.";

    static readonly RoomRow[] _no_rooms = [];

    readonly IGameGateway _gateway;
    readonly IClipboardService _clipboard;
    readonly INotificationService _notifications;
    readonly AppLifetime _lifetime;
    readonly KeyedRows<long, RoomRow> _all = new();
    readonly ConcurrentQueue<NavigatorChanged> _changes = new();
    readonly CoalescingSignal _changed;
    readonly CoalescingSignal _received;
    NavigatorSearchSnapshot? _last;
    NavigatorSearchReceived? _incoming;
    CancellationTokenSource? _stop;
    string _empty_message = ChooseASearch;
    bool _metadata_loaded;
    bool _has_result;
    long _generation;
    long _reset;

    public NavigatorViewModel(
        IGameGateway gateway,
        IClipboardService clipboard,
        INotificationService notifications,
        AppLifetime lifetime,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Navigator)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(time);
        Notices = Own(new NoticeLine(dispatcher, time));
        _changed = new CoalescingSignal(dispatcher, DrainChanges);
        _received = new CoalescingSignal(dispatcher, DrainResult);
        Own(gateway.Subscribe<NavigatorChanged>(ApplicationMemberIds.NavigatorChanged, OnNavigatorChanged));
        Own(gateway.Subscribe<NavigatorSearchReceived>(ApplicationMemberIds.NavigatorSearchReceived, OnSearchReceived));
        Rooms.Applied += OnRoomsApplied;
        Selection.Changed += RefreshMenu;
        gateway.SessionChanged += OnSessionChanged;
        Own(() =>
        {
            Rooms.Applied -= OnRoomsApplied;
            Selection.Changed -= RefreshMenu;
            gateway.SessionChanged -= OnSessionChanged;
            _stop?.Cancel();
        });
        Subtitle = NavigatorText.MetadataNotLoaded;
        UseList(SelectedList);
        RefreshMenu();
        ShowState();
    }

    public IReadOnlyList<NavigatorSearchList> Lists => NavigatorSearchOptions.Lists;

    public ResettableCollection<NavigatorSearchOption> Options { get; } = [];

    public FilteredRows<RoomRow> Rooms { get; } = new();

    public SelectionList<RoomRow> Selection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public NoticeLine Notices { get; }

    [ObservableProperty]
    public partial NavigatorSearchList SelectedList { get; set; } = NavigatorSearchOptions.Lists[0];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial NavigatorSearchOption? SelectedOption { get; set; }

    [ObservableProperty]
    public partial bool HasOptionChoice { get; private set; }

    [ObservableProperty]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial bool QueryEnabled { get; private set; } = true;

    [ObservableProperty]
    public partial string QueryPlaceholder { get; private set; } = SearchRooms;

    [ObservableProperty]
    public partial bool QueryFocusRequested { get; set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(CancelSearchCommand))]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySelectionCommand))]
    public partial bool HasSelection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyFieldCommand))]
    public partial bool HasOneSelected { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnterCommand))]
    public partial bool EnterAllowed { get; private set; }

    [ObservableProperty]
    public partial string EnterTip { get; private set; } = "";

    public bool CanSearch => !IsSearching && SelectedOption is not null;

    public override bool TryClearSearch()
    {
        if (Query.Length == 0)
            return false;
        Query = "";
        return true;
    }

    public void RefreshMenu()
    {
        if (_lifetime.IsStopping)
            return;
        MemberGate enter = _gateway.Gate(ApplicationMemberIds.RoomEnter);
        HasSelection = Selection.HasAny;
        HasOneSelected = Selection.HasOne;
        EnterAllowed = enter.Available;
        EnterTip = enter.Available ? "" : enter.Reason;
        EnterCommand.NotifyCanExecuteChanged();
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        await RefreshAsync(cancellation_token);
        await FetchAsync(cancellation_token);
        AskForQueryFocus();
    }

    partial void OnSelectedListChanged(NavigatorSearchList value) => UseList(value);

    partial void OnSelectedOptionChanged(NavigatorSearchOption? value) => RefreshQuery();

    [RelayCommand(CanExecute = nameof(CanSearch))]
    async Task SearchAsync()
    {
        if (IsSearching || SelectedOption is not { } option)
            return;
        string query = Query.Trim();
        if (option.QueryRequired && query.Length == 0)
        {
            Notices.Show(NoticeSeverity.Warning, EnterASearchTerm);
            AskForQueryFocus();
            return;
        }
        var stop = new CancellationTokenSource();
        long reset = _reset;
        _stop = stop;
        IsSearching = true;
        State.ShowLoading(Searching, CancelSearchCommand);
        NavigatorSearchSnapshot? result = null;
        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, stop.Token);
            result = await AskAsync(option, query, linked.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Report(NoticeSeverity.Warning, $"Search failed: {FailureText.Describe(error)}");
        }
        finally
        {
            _stop = null;
            stop.Dispose();
            IsSearching = false;
        }
        if (IsDisposed)
            return;
        if (reset == _reset && !stop.IsCancellationRequested && result is { } snapshot)
            TakeResult(snapshot);
        else
            ShowState();
    }

    [RelayCommand(CanExecute = nameof(IsSearching))]
    void CancelSearch() => _stop?.Cancel();

    [RelayCommand(CanExecute = nameof(CanEnter))]
    async Task EnterAsync(RoomRow? row)
    {
        if ((row ?? Selection.First) is not { } target)
            return;
        try
        {
            RoomLifecycleDispatchResult result = await _gateway.InvokeAsync<RoomEnterRequest, RoomLifecycleDispatchResult>(
                ApplicationMemberIds.RoomEnter,
                new RoomEnterRequest(target.Id),
                _lifetime.Token);
            if (IsDisposed)
                return;
            Report(
                result.Dispatched ? NoticeSeverity.Success : NoticeSeverity.Warning,
                result.Dispatched ? $"Entering {target.Name}…" : $"Could not enter {target.Name}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Report(NoticeSeverity.Warning, $"Could not enter {target.Name}: {FailureText.Describe(error)}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyField))]
    async Task CopyFieldAsync(RoomResultField field, CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        string value = row.Value(field);
        if (value.Length == 0)
            return;
        if (await CopyAsync(value, cancellation_token))
            Notices.Show(NoticeSeverity.Info, $"Copied {value}");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        RoomRow[] picked = [.. Selection.Items];
        if (picked.Length == 0)
            return;
        string text = string.Join(Environment.NewLine, picked.Select(row => row.Line));
        if (await CopyAsync(text, cancellation_token))
            Notices.Show(NoticeSeverity.Info, NavigatorText.Copied(picked.Length));
    }

    bool CanEnter(RoomRow? row) => EnterAllowed && (row ?? Selection.First) is not null;

    bool CanCopyField(RoomResultField field) => HasOneSelected;

    Task<NavigatorSearchSnapshot> AskAsync(NavigatorSearchOption option, string query, CancellationToken cancellation_token) => option.Mode switch
    {
        NavigatorSearchMode.Text => _gateway.InvokeAsync<NavigatorTextSearchInput, NavigatorSearchSnapshot>(
            option.MemberId,
            new NavigatorTextSearchInput(option.Field, query),
            cancellation_token),
        NavigatorSearchMode.Popular => _gateway.InvokeAsync<NavigatorPopularSearchInput, NavigatorSearchSnapshot>(
            option.MemberId,
            new NavigatorPopularSearchInput(query),
            cancellation_token),
        NavigatorSearchMode.Ad => _gateway.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
            option.MemberId,
            new NavigatorAdSearchInput(),
            cancellation_token),
        _ => _gateway.InvokeAsync<NavigatorSearchRequest, NavigatorSearchSnapshot>(
            option.MemberId,
            new NavigatorSearchRequest(),
            cancellation_token)
    };

    async Task RefreshAsync(CancellationToken cancellation_token)
    {
        try
        {
            NavigatorState state = await _gateway.QueryAsync<NavigatorStateRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorState,
                new NavigatorStateRequest(),
                cancellation_token);
            if (!IsDisposed)
                TakeState(state);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
    }

    async Task FetchAsync(CancellationToken cancellation_token)
    {
        if (_metadata_loaded)
            return;
        MemberGate gate = _gateway.Gate(ApplicationMemberIds.NavigatorMetadataRefresh);
        if (!gate.Available)
        {
            ShowState();
            return;
        }
        try
        {
            NavigatorState state = await _gateway.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorMetadataRefresh,
                new NavigatorRefreshRequest(),
                cancellation_token);
            if (!IsDisposed)
                TakeState(state);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (Reportable(error))
        {
            Notices.Show(NoticeSeverity.Warning, FailureText.Describe(error));
        }
    }

    void UseList(NavigatorSearchList list)
    {
        Options.ReplaceAll(list.Options);
        HasOptionChoice = list.Options.Count > 1;
        SelectedOption = list.Options[0];
        RefreshQuery();
    }

    void RefreshQuery()
    {
        NavigatorSearchOption? option = SelectedOption;
        QueryEnabled = option is { TakesQuery: true };
        QueryPlaceholder = option is { Mode: NavigatorSearchMode.Popular } ? OptionalTag : SearchRooms;
        if (!QueryEnabled && Query.Length > 0)
            Query = "";
        ShowState();
    }

    void AskForQueryFocus()
    {
        if (!QueryEnabled)
            return;
        QueryFocusRequested = false;
        QueryFocusRequested = true;
    }

    void TakeState(NavigatorState state)
    {
        _generation = state.Generation;
        _metadata_loaded = state.MetadataLoaded;
        Subtitle = NavigatorText.Summary(state);
        if (_last is { } result && !IsSearching)
        {
            TakeResult(result);
            return;
        }
        if (state.Generation == 0 || !_has_result)
        {
            ClearRooms(ChooseASearch);
            return;
        }
        ShowState();
    }

    void TakeResult(NavigatorSearchSnapshot result)
    {
        _last = result;
        _has_result = true;
        RoomDataSnapshot[] rooms = [.. Once(result)];
        RowChanges changes = _all.Sync(
            rooms,
            room => (long)room.Id,
            room => new RoomRow(room),
            (row, room) => row.Take(room));
        _empty_message = rooms.Length == 0 ? NoRoomsMatched : ChooseASearch;
        if (changes.Updated > 0)
            SortRefresh.Request();
        RoomRow[] ordered = [.. rooms.Select(room => _all.Find(room.Id)).OfType<RoomRow>()];
        Rooms.ApplyAsync(ordered, static _ => true, null, ActivationToken).Observe("ui");
    }

    void ClearRooms(string message)
    {
        _all.Clear();
        _has_result = false;
        _empty_message = message;
        Rooms.ApplyAsync(_no_rooms, static _ => true, null, ActivationToken).Observe("ui");
    }

    void OnRoomsApplied()
    {
        CountText = _has_result ? NavigatorText.Rooms(Rooms.Visible.Count) : "";
        Selection.Prune(Rooms.Visible);
        ShowState();
    }

    void ShowState()
    {
        if (IsSearching || IsDisposed || _lifetime.IsStopping)
            return;
        if (_all.Count > 0)
        {
            State.ShowReady();
            return;
        }
        if (SelectedOption is { } option && _gateway.Gate(option.MemberId) is { Available: false } gate)
        {
            State.ShowUnavailable(Descriptor.Icon, gate.Reason);
            return;
        }
        State.ShowEmpty(Descriptor.Icon, _empty_message);
    }

    void Report(NoticeSeverity severity, string text)
    {
        if (IsActive)
        {
            Notices.Show(severity, text);
            return;
        }
        _notifications.Show(text, severity);
    }

    async Task<bool> CopyAsync(string text, CancellationToken cancellation_token)
    {
        if (text.Length == 0)
            return false;
        try
        {
            if (await _clipboard.TrySetTextAsync(text, cancellation_token))
                return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        Notices.Show(NoticeSeverity.Warning, ClipboardUnreachable);
        return false;
    }

    void OnNavigatorChanged(NavigatorChanged change)
    {
        _changes.Enqueue(change);
        _changed.Raise();
    }

    void OnSearchReceived(NavigatorSearchReceived received)
    {
        Volatile.Write(ref _incoming, received);
        _received.Raise();
    }

    void DrainChanges()
    {
        while (_changes.TryDequeue(out NavigatorChanged? change))
        {
            if (IsDisposed || _lifetime.IsStopping)
                return;
            if (change.Kind == NavigatorChangeKind.Reset)
            {
                _reset++;
                _stop?.Cancel();
                _last = null;
                ClearRooms(ChooseASearch);
            }
            TakeState(change.State);
        }
    }

    void DrainResult()
    {
        if (IsDisposed || _lifetime.IsStopping)
            return;
        if (Interlocked.Exchange(ref _incoming, null) is { } received && received.Generation == _generation)
            TakeResult(received.Result);
    }

    void OnSessionChanged()
    {
        if (_lifetime.IsStopping)
            return;
        RefreshMenu();
        ShowState();
        if (IsActive)
            RefreshAsync(ActivationToken).Observe("ui");
    }

    static IEnumerable<RoomDataSnapshot> Once(NavigatorSearchSnapshot result)
    {
        var seen = new HashSet<long>();
        foreach (RoomDataSnapshot room in result.Rooms)
        {
            if (seen.Add(room.Id))
                yield return room;
        }
    }

    static bool Reportable(Exception error) =>
        error is GameUnavailableException or InvalidOperationException or TimeoutException;
}
