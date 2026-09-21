using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Game.Application;
using Qx.Model;
using Qx.Presentation.Collections;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Services.Room;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Room;

public sealed partial class RoomBansViewModel : ViewModelBase
{
    static readonly BanOrder _order = new();

    readonly RoomPageContext _context;
    readonly KeyedRows<string, PersonRowViewModel> _all = new(StringComparer.Ordinal);
    readonly Debouncer _refilter;
    readonly CoalescingSignal _moderation;
    IDisposable? _subscription;
    RoomModerationStateView? _state;
    RoomSnapshot _snapshot = RoomSnapshot.Empty;
    long _asked_generation = -1;
    bool _loading;

    public RoomBansViewModel(RoomPageContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _refilter = Own(new Debouncer(context.Dispatcher, context.Time, RoomPeopleViewModel.FilterDelay, ApplyFilter));
        _moderation = new CoalescingSignal(context.Dispatcher, OnModerationChanged);
        Selection.Changed += OnSelectionChanged;
        Rows.Applied += OnRowsApplied;
        Own(() =>
        {
            Selection.Changed -= OnSelectionChanged;
            Rows.Applied -= OnRowsApplied;
        });
    }

    public FilteredRows<PersonRowViewModel> Rows { get; } = new();

    public SelectionList<PersonRowViewModel> Selection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public ViewState State { get; } = new();

    [ObservableProperty]
    public partial string Filter { get; set; } = "";

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnbanCommand), nameof(CopyNamesCommand), nameof(CopySelectionCommand))]
    public partial bool CanUnban { get; private set; }

    public bool IsLoaded => _state?.Loaded == true;

    public void Start()
    {
        _subscription ??= _context.Gateway.SubscribeSignal(ApplicationMemberIds.RoomModerationChanged, _moderation);
    }

    public void Stop()
    {
        Interlocked.Exchange(ref _subscription, null)?.Dispose();
    }

    public void Show(RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        bool moved = snapshot.Identity.Generation != _snapshot.Identity.Generation;
        _snapshot = snapshot;
        if (!moved)
            return;
        _state = null;
        _asked_generation = -1;
        _all.Clear();
        ApplyFilter();
    }

    public void Visit()
    {
        if (!_snapshot.IsInRoom || !_snapshot.Identity.HasRights || _loading || IsLoaded || _asked_generation == _snapshot.Identity.Generation)
            return;
        _asked_generation = _snapshot.Identity.Generation;
        LoadCommand.Execute(null);
    }

    public bool ClearFilter()
    {
        if (Filter.Length == 0)
            return false;
        Filter = "";
        return true;
    }

    [RelayCommand]
    async Task LoadAsync(CancellationToken cancellation_token)
    {
        if (_loading)
            return;
        RoomModerationStateView state;
        try
        {
            state = await RoomModerationReader.ReadAsync(_context.Gateway, cancellation_token);
            if (!state.RoomReady || state.RoomId <= 0)
                throw new InvalidOperationException("A ready room is required to read its ban list.");
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Could not read the ban list: {error.Message}", "ui");
            Describe();
            return;
        }
        if (!_snapshot.Identity.HasRights)
        {
            Describe();
            return;
        }

        _loading = true;
        Describe();
        try
        {
            RoomModerationStateView refreshed = await RoomModerationReader
                .RefreshAsync(_context.Gateway, state, cancellation_token);
            _state = refreshed;
            Take(refreshed);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Could not read the ban list: {error.Message}", "ui");
        }
        finally
        {
            _loading = false;
            ApplyFilter();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUnban))]
    async Task UnbanAsync(CancellationToken cancellation_token)
    {
        PersonRowViewModel[] picked = [.. Selection.Items];
        if (picked.Length == 0)
            return;
        RoomModerationStateView? displayed = _state;
        RoomModerationStateView state;
        try
        {
            state = await RoomModerationReader.ReadAsync(_context.Gateway, cancellation_token);
            if (!state.RoomReady || state.RoomId <= 0)
                throw new InvalidOperationException("A ready room is required to unban a user.");
            if (displayed is null ||
                !displayed.Loaded ||
                !state.Loaded ||
                displayed.SessionGeneration != state.SessionGeneration ||
                displayed.RoomId != state.RoomId ||
                displayed.RoomGeneration != state.RoomGeneration ||
                displayed.BanList.SnapshotRevision != state.BanList.SnapshotRevision)
            {
                throw new InvalidOperationException("The displayed ban list is no longer current.");
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _context.Fail("Could not unban the selection", error);
            return;
        }

        long? snapshot_revision = displayed.BanList.SnapshotRevision;
        int done = 0;
        foreach (PersonRowViewModel row in picked)
        {
            try
            {
                if (row.RoomGeneration != state.RoomGeneration)
                    throw new InvalidOperationException("The selected ban belongs to an earlier room session.");
                await _context.Gateway.InvokeAsync<RoomModerationUnbanRequest, RoomModerationDispatchResult>(
                    ApplicationMemberIds.RoomModerationUnban,
                    new RoomModerationUnbanRequest(
                        row.UserId,
                        state.RoomId,
                        state.SessionGeneration,
                        state.RoomGeneration,
                        snapshot_revision),
                    cancellation_token);
                snapshot_revision = null;
                done++;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                _context.Fail($"Could not unban {row.Name}", error);
            }
        }

        if (done == 0)
            return;
        _context.Say(done == 1 ? $"Let {picked[0].Name} back in." : $"Let {done} people back in.");
        await LoadAsync(cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanUnban))]
    async Task CopyNamesAsync(CancellationToken cancellation_token)
    {
        string names = string.Join(Environment.NewLine, Selection.Items.Select(row => row.Name));
        await _context.CopyAsync(names, Selection.Count == 1 ? "Name copied." : "Names copied.", cancellation_token);
    }

    [RelayCommand(CanExecute = nameof(CanUnban))]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        string[] lines = [.. Selection.Items.Select(row => string.Join('	', row.Name, row.IdText))];
        if (lines.Length == 0)
            return;
        await _context.CopyAsync(
            string.Join(Environment.NewLine, lines),
            lines.Length == 1 ? "Copied 1 row." : $"Copied {lines.Length} rows.",
            cancellation_token);
    }

    [RelayCommand]
    void ClearFilters() => ClearFilter();

    protected override void OnDisposed()
    {
        Stop();
        base.OnDisposed();
    }

    void Take(RoomModerationStateView state)
    {
        RoomBanEntry[] bans = RoomModerationReader.Entries(state);
        _all.Sync(
            bans,
            ban => ban.UserId.ToString(),
            ban => PersonRowViewModel.FromBan(ban, _context.WebHost),
            (row, ban) => row.Take(ban, _context.WebHost));
        SortRefresh.Request();
    }

    void OnModerationChanged()
    {
        if (!_snapshot.IsInRoom)
            return;
        ReadStateAsync(_context.Lifetime).Observe("ui");
    }

    async Task ReadStateAsync(CancellationToken cancellation_token)
    {
        try
        {
            RoomModerationStateView state = await RoomModerationReader.ReadAsync(_context.Gateway, cancellation_token);
            _state = state;
            if (state.Loaded)
                Take(state);
            else
                _all.Clear();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _state = null;
            _all.Clear();
        }
        ApplyFilter();
    }

    void ApplyFilter()
    {
        string term = Filter.Trim();
        Rows.ApplyAsync(_all.Rows, row => Keep(row, term), _order, _context.Lifetime).Observe("ui");
    }

    static bool Keep(PersonRowViewModel row, string term)
    {
        if (term.Length == 0)
            return true;
        PersonSearch search = row.Search;
        return search.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
            search.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }

    void OnSelectionChanged() => CanUnban = Selection.HasAny;

    void OnRowsApplied()
    {
        Selection.Prune(Rows.Visible);
        Describe();
    }

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
        if (_loading)
        {
            CountText = "";
            State.ShowLoading("Asking the hotel…");
            return;
        }
        if (!IsLoaded)
        {
            CountText = "";
            State.ShowEmpty(
                IconKind.Ban,
                "Ban list not loaded",
                "The hotel only sends the ban list when it is asked for, and only to someone with rights in the room.",
                LoadCommand,
                "Load");
            return;
        }
        CountText = total == visible ? $"{total:N0} banned" : $"{visible:N0} of {total:N0} banned";
        if (total == 0)
        {
            CountText = "";
            State.ShowEmpty(IconKind.Ban, "Nobody is banned from this room.");
        }
        else if (visible == 0)
        {
            State.ShowEmpty(
                IconKind.Search,
                "No matches",
                $"Nothing matches “{Filter.Trim()}”.",
                ClearFiltersCommand,
                "Clear filters");
        }
        else
        {
            State.ShowReady();
        }
    }

    partial void OnFilterChanged(string value) => _refilter.Trigger();

    sealed class BanOrder : IComparer<PersonRowViewModel>
    {
        public int Compare(PersonRowViewModel? left, PersonRowViewModel? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int order = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : left.UserId.CompareTo(right.UserId);
        }
    }
}
