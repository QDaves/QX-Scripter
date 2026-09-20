using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game;
using Qx.Game.Application;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Inventory;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Inventory;

public sealed partial class InventoryViewModel : PageViewModel, IVisibleItemsSink
{
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan ScanDelay = TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan BatchGap = TimeSpan.FromMilliseconds(150);

    public const int BatchSize = 25;

    readonly IGameGateway _gateway;
    readonly IMarketplacePrices _prices;
    readonly IDialogService _dialogs;
    readonly IClipboardService _clipboard;
    readonly TimeProvider _time;
    readonly KeyedRows<InventoryItemKind, InventoryRowViewModel> _all = new();
    readonly FilteredRows<InventoryRowViewModel> _rows = new();
    readonly SerialOperation _reads = new();
    readonly SerialOperation _scans = new();
    readonly Debouncer _refilter;
    readonly Debouncer _rescan;
    readonly CoalescingSignal _changed;
    InventoryContents _contents = InventoryContents.Disconnected;
    InventoryFilter _applied = InventoryFilter.Everything;
    IReadOnlyList<InventoryRowViewModel> _visible = [];
    int _asked;
    int _to_ask;
    bool _fetching;
    bool _resetting;

    public InventoryViewModel(
        IGameGateway gateway,
        IMarketplacePrices prices,
        IDialogService dialogs,
        IClipboardService clipboard,
        IUiDispatcher dispatcher,
        TimeProvider time)
        : base(PageKey.Inventory)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        ArgumentNullException.ThrowIfNull(dispatcher);
        Notices = Own(new NoticeLine(dispatcher, time));
        _refilter = Own(new Debouncer(dispatcher, time, SearchDelay, Filtered));
        _rescan = Own(new Debouncer(dispatcher, time, ScanDelay, Scan));
        _changed = new CoalescingSignal(dispatcher, Reread);
        Selection.Changed += OnSelectionChanged;
        Own(() => Selection.Changed -= OnSelectionChanged);
        Own(_gateway.SubscribeSignal(ApplicationMemberIds.InventoryFurniChanged, _changed));
        Own(_gateway.SubscribeSignal(ApplicationMemberIds.InventoryPetsChanged, _changed));
        _gateway.SessionChanged += OnSessionChanged;
        Own(() => _gateway.SessionChanged -= OnSessionChanged);
        GameData data = _gateway.Game.GameData;
        data.Loaded += OnGameDataLoaded;
        Own(() => data.Loaded -= OnGameDataLoaded);
    }

    public NoticeLine Notices { get; }

    public SelectionList<InventoryRowViewModel> Selection { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public ResettableCollection<InventoryRowViewModel> Rows => _rows.Visible;

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial OperationProgress? Progress { get; private set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KindIndex))]
    public partial InventoryKindFilter KindFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TradeIndex))]
    public partial InventoryTradeFilter TradeFilter { get; set; }

    [ObservableProperty]
    public partial int? LeastOwned { get; set; }

    [ObservableProperty]
    public partial int? LeastPrice { get; set; }

    [ObservableProperty]
    public partial int? MostPrice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeIndex), nameof(IsListView), nameof(IsGridView))]
    public partial InventoryViewMode Mode { get; set; }

    [ObservableProperty]
    public partial bool IsFiltering { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SellCommand))]
    public partial bool CanSellSelection { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyIdentifierCommand), nameof(CopyItemIdCommand), nameof(CopySelectionCommand))]
    public partial bool HasSelection { get; private set; }

    public int KindIndex
    {
        get => (int)KindFilter;
        set => KindFilter = (InventoryKindFilter)value;
    }

    public int TradeIndex
    {
        get => (int)TradeFilter;
        set => TradeFilter = (InventoryTradeFilter)value;
    }

    public int ModeIndex
    {
        get => (int)Mode;
        set => Mode = (InventoryViewMode)value;
    }

    public bool IsListView => Mode == InventoryViewMode.List;

    public bool IsGridView => Mode == InventoryViewMode.Grid;

    public override bool TryClearSearch()
    {
        if (!IsFiltering)
            return false;
        ClearFilters();
        return true;
    }

    public void VisibleChanged(IReadOnlyList<object> visible)
    {
        ArgumentNullException.ThrowIfNull(visible);
        _visible = [.. visible.OfType<InventoryRowViewModel>()];
        Rearm();
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        await ReadAsync(cancellation_token);
        await LoadAsync(cancellation_token);
        await ReadAsync(cancellation_token);
    }

    protected override void OnDeactivated()
    {
        _rescan.Cancel();
        _scans.StopAsync().Observe("inventory");
    }

    [RelayCommand(IncludeCancelCommand = true)]
    async Task ReloadAsync(CancellationToken cancellation_token)
    {
        try
        {
            await LoadAsync(cancellation_token);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            return;
        }
        await ReadAsync(cancellation_token);
    }

    [RelayCommand]
    void ClearFilters()
    {
        _resetting = true;
        SearchText = "";
        KindFilter = InventoryKindFilter.All;
        TradeFilter = InventoryTradeFilter.Any;
        LeastOwned = null;
        LeastPrice = null;
        MostPrice = null;
        _resetting = false;
        _refilter.Cancel();
        Filtered();
    }

    [RelayCommand(CanExecute = nameof(CanSellSelection))]
    async Task SellAsync()
    {
        if (!_gateway.IsHotelConnected)
        {
            Notices.Show(NoticeSeverity.Info, "Connect before listing marketplace offers.");
            return;
        }
        SellCandidate[] chosen =
        [
            .. Selection.Items
                .Where(row => row.CanSell && row.ItemIds.Count > 0)
                .Select(row => new SellCandidate(row.Name, row.Type!.Value, row.Identifier, row.ItemIds, row.Market))
        ];
        if (chosen.Length == 0)
        {
            Notices.Show(NoticeSeverity.Info, "Nothing there can be sold on the marketplace.");
            return;
        }
        using var sell = new SellDialogViewModel(chosen, _prices, _gateway);
        SellOutcome outcome = await _dialogs.ShowAsync(sell, ActivationToken);
        Report(outcome);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    Task CopyIdentifierAsync(CancellationToken cancellation_token) =>
        CopyAsync(Selection.Items.Select(row => row.Identifier).Where(text => text.Length > 0), cancellation_token);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    Task CopyItemIdAsync(CancellationToken cancellation_token) =>
        CopyAsync(Selection.Items.Select(row => row.KeyText), cancellation_token);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    async Task CopySelectionAsync(CancellationToken cancellation_token)
    {
        int rows = Selection.Count;
        if (!await CopyAsync(Selection.Items.Select(Tabbed), cancellation_token, silent: true))
            return;
        Notices.Show(NoticeSeverity.Success, rows == 1 ? "Copied 1 row." : $"Copied {rows.ToString("N0", CultureInfo.CurrentCulture)} rows.");
    }

    async Task<bool> CopyAsync(IEnumerable<string> values, CancellationToken cancellation_token, bool silent = false)
    {
        string text = string.Join(Environment.NewLine, values);
        if (text.Length == 0)
            return false;
        if (!await _clipboard.TrySetTextAsync(text, cancellation_token))
        {
            Notices.Show(NoticeSeverity.Warning, "Could not reach the clipboard. Another program may be holding it.");
            return false;
        }
        if (!silent)
            Notices.Show(NoticeSeverity.Success, "Copied.");
        return true;
    }

    static string Tabbed(InventoryRowViewModel row) =>
        string.Join('\t', row.Name, row.Detail, row.GroupText, row.OwnedText, row.MarketText, row.KeyText);

    void Report(SellOutcome outcome)
    {
        if (outcome.Failure is { Length: > 0 } failure)
        {
            Notices.Show(
                outcome.OffersMade > 0 ? NoticeSeverity.Warning : NoticeSeverity.Error,
                outcome.OffersMade > 0
                    ? $"{outcome.OffersMade} offered, then it stopped: {failure}"
                    : $"Nothing was offered: {failure}");
            return;
        }
        if (outcome.OffersMade > 0)
            Notices.Show(NoticeSeverity.Success, $"{outcome.OffersMade} offer{(outcome.OffersMade == 1 ? "" : "s")} made.");
    }

    async Task LoadAsync(CancellationToken cancellation_token)
    {
        if (!_gateway.IsHotelConnected)
            return;
        _fetching = true;
        State.ShowLoading("Asking the hotel…", ReloadCommand.IsRunning ? ReloadCancelCommand : null);
        try
        {
            InventoryStateView? state = await _gateway.QueryAsync<InventoryStateRequest, InventoryStateView>(
                ApplicationMemberIds.InventoryState,
                new InventoryStateRequest(),
                cancellation_token);
            if (state is null)
                return;
            var loads = new List<Task>(2);
            if (Wanted(state.Furni))
            {
                loads.Add(_gateway.InvokeAsync<InventoryFurniRefreshRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniRefresh,
                    new InventoryFurniRefreshRequest(Limit: InventoryRead.PageLimit),
                    cancellation_token));
            }
            if (Wanted(state.Pets))
            {
                loads.Add(_gateway.InvokeAsync<InventoryPetRefreshRequest, InventoryPetPage>(
                    ApplicationMemberIds.InventoryPetsRefresh,
                    new InventoryPetRefreshRequest(Limit: InventoryRead.PageLimit),
                    cancellation_token));
            }
            foreach (Task load in loads)
                await load.WaitAsync(cancellation_token);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Notices.Show(NoticeSeverity.Warning, $"Could not read the inventory: {FailureText.Describe(error)}");
        }
        finally
        {
            _fetching = false;
        }
    }

    static bool Wanted(InventoryCollectionStateView state) => !state.Loaded || state.Stale || state.RecoveryPending;

    async Task ReadAsync(CancellationToken cancellation_token)
    {
        OperationLease lease = await _reads.StartAsync(cancellation_token);
        try
        {
            InventoryContents contents = await InventoryRead.LoadAsync(_gateway, lease.Token);
            if (!_reads.IsCurrent(lease))
                return;
            Adopt(contents);
            await ApplyAsync(lease.Token);
            Rearm();
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        catch (ApplicationUnavailableException)
        {
            Adopt(InventoryContents.Disconnected);
            await ApplyAsync(CancellationToken.None);
        }
        catch (GameUnavailableException error)
        {
            Adopt(InventoryContents.Disconnected);
            await ApplyAsync(CancellationToken.None);
            Notices.Show(NoticeSeverity.Warning, error.Message);
        }
        catch (InvalidOperationException)
        {
            Adopt(new InventoryContents(true, false, _contents.Loaded, 0, 0, 0, []));
            await ApplyAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            State.ShowError("Could not read the inventory", FailureText.Describe(error), ReloadCommand);
        }
        finally
        {
            _reads.Complete(lease);
        }
    }

    void Adopt(InventoryContents contents)
    {
        _contents = contents;
        _all.Sync(contents.Entries, entry => entry.Key, entry => new InventoryRowViewModel(entry), (row, entry) => row.Adopt(entry));
        Subtitle = contents.Entries.Count == 0
            ? ""
            : $"{Number(contents.Total)} {(contents.Total == 1 ? "item" : "items")} in {Number(contents.Kinds)} {(contents.Kinds == 1 ? "kind" : "kinds")}" +
              (contents.Pets > 0 ? $" · {Number(contents.Pets)} {(contents.Pets == 1 ? "pet" : "pets")}" : "");
    }

    void Reread()
    {
        if (IsActive)
            ReadAsync(ActivationToken).Observe("inventory");
    }

    void OnSessionChanged() => Reread();

    void OnGameDataLoaded() => _changed.Raise();

    void OnSelectionChanged()
    {
        HasSelection = Selection.HasAny;
        CanSellSelection = Selection.Items.Any(row => row.CanSell && row.ItemIds.Count > 0);
    }

    void Filtered()
    {
        if (!_resetting)
            ApplyAsync(ActivationToken).Observe("inventory");
    }

    async Task ApplyAsync(CancellationToken cancellation_token)
    {
        InventoryFilter filter = Filter();
        _applied = filter;
        IsFiltering = filter.IsFiltering;
        await _rows.ApplyAsync(_all.Rows, filter.Keeps, new InventoryOrder(filter), cancellation_token);
        Selection.Prune(_rows.Visible);
        Refresh();
    }

    InventoryFilter Filter() => new(SearchText.Trim(), KindFilter, TradeFilter, Positive(LeastOwned), Positive(LeastPrice), Positive(MostPrice));

    static int? Positive(int? value) => value is { } number && number > 0 ? number : null;

    void Refresh()
    {
        int shown = _rows.Visible.Count;
        int total = _all.Count;
        CountText = shown == total
            ? $"{Number(shown)} shown"
            : $"{Number(shown)} of {Number(total)} shown";
        if (_fetching)
            return;
        if (!_contents.Connected)
        {
            State.ShowUnavailable(IconKind.ConnectionOff, "Not connected", "Connect to the hotel through G-Earth first.");
            return;
        }
        if (!_contents.Consistent)
        {
            State.ShowUnavailable(IconKind.Refresh, "The inventory changed", "Reload it before using these items.");
            return;
        }
        if (total == 0)
        {
            State.ShowEmpty(
                IconKind.Inventory,
                _contents.Loaded ? "Your inventory is empty." : "Nothing yet",
                _contents.Loaded ? "" : "Reload to ask the hotel for it.");
            return;
        }
        if (shown == 0)
        {
            State.ShowEmpty(IconKind.Search, "No matches", _applied.Term.Length > 0 ? $"Nothing matches “{_applied.Term}”." : "");
            return;
        }
        State.ShowReady();
    }

    void Rearm()
    {
        bool moved = false;
        foreach (InventoryRowViewModel row in _all.Rows)
        {
            if (row.CanSell && _prices.Known(row.Kind) is { } held)
                moved |= row.ShowPrice(held);
        }
        if (moved)
            SortRefresh.Request();
        _rescan.Trigger();
    }

    void Scan() => ScanAsync(Scannable(), ActivationToken).Observe("inventory");

    IReadOnlyList<InventoryRowViewModel> Scannable()
    {
        if (_visible.Count > 0)
            return _visible;
        return _rows.Visible.Count > 0 ? [.. _rows.Visible.Take(BatchSize)] : [.. _all.Rows.Take(BatchSize)];
    }

    async Task ScanAsync(IReadOnlyList<InventoryRowViewModel> rows, CancellationToken cancellation_token)
    {
        OperationLease lease = await _scans.StartAsync(cancellation_token);
        try
        {
            MarketplaceKind[] kinds = [.. rows.Where(row => row.CanSell).Select(row => row.Kind).Distinct()];
            _asked = 0;
            _to_ask = kinds.Length;
            Announce();
            for (int start = 0; start < kinds.Length; start += BatchSize)
            {
                MarketplaceKind[] batch = [.. kinds.Skip(start).Take(BatchSize)];
                IReadOnlyDictionary<MarketplaceKind, MarketplacePrice> found = await _prices.FetchAsync(batch, lease.Token);
                if (!_scans.IsCurrent(lease))
                    return;
                bool moved = Record(rows, batch, found);
                _asked += batch.Length;
                Announce();
                if (moved && _applied.ByPrice)
                    await ApplyAsync(lease.Token);
                if (start + BatchSize < kinds.Length)
                    await Task.Delay(BatchGap, _time, lease.Token);
            }
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (_scans.IsCurrent(lease))
                Notices.Show(NoticeSeverity.Warning, $"Prices stopped at {Number(_asked)}: {error.Message}");
        }
        finally
        {
            if (_scans.IsCurrent(lease))
            {
                _to_ask = _asked;
                Announce();
            }
            _scans.Complete(lease);
        }
    }

    bool Record(IReadOnlyList<InventoryRowViewModel> rows, MarketplaceKind[] batch, IReadOnlyDictionary<MarketplaceKind, MarketplacePrice> found)
    {
        var asked = batch.ToHashSet();
        bool moved = false;
        foreach (InventoryRowViewModel row in rows)
        {
            if (!row.CanSell || !asked.Contains(row.Kind))
                continue;
            if (found.TryGetValue(row.Kind, out MarketplacePrice? price))
                moved |= row.ShowPrice(price);
            else if (_prices.WasRead(row.Kind))
                moved |= row.ShowPrice(new MarketplacePrice(row.Kind.Type, row.Kind.Identifier, null, null, 0, 0));
        }
        if (moved)
            SortRefresh.Request();
        return moved;
    }

    void Announce() =>
        Progress = _asked < _to_ask
            ? new OperationProgress($"Reading prices, {Number(_asked)} of {Number(_to_ask)} kinds", _to_ask > 0 ? _asked / (double)_to_ask : null)
            : null;

    static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    partial void OnSearchTextChanged(string value)
    {
        if (!_resetting)
            _refilter.Trigger();
    }

    partial void OnKindFilterChanged(InventoryKindFilter value) => Filtered();

    partial void OnTradeFilterChanged(InventoryTradeFilter value) => Filtered();

    partial void OnLeastOwnedChanged(int? value) => Filtered();

    partial void OnLeastPriceChanged(int? value) => Filtered();

    partial void OnMostPriceChanged(int? value) => Filtered();

    partial void OnModeChanged(InventoryViewMode value)
    {
        Selection.Select(Selection.Items);
        Rearm();
    }
}
