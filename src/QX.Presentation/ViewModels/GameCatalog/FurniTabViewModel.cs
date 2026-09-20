using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Collections;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.GameCatalog;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.GameCatalog;

public sealed partial class FurniTabViewModel : ViewModelBase, IVisibleItemsSink
{
    public const int PriceBatchSize = 25;
    public static readonly TimeSpan FilterDelay = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan PriceBatchGap = TimeSpan.FromMilliseconds(150);

    readonly IGameGateway _gateway;
    readonly IDialogService _dialogs;
    readonly IMarketplacePrices _prices;
    readonly ShopCatalog _shop;
    readonly MarketplaceTrade _trade;
    readonly NoticeLine _notices;
    readonly TimeProvider _time;
    readonly CancellationToken _lifetime;
    readonly KeyedRows<FurniKey, FurniRowViewModel> _all = new();
    readonly SerialOperation _pricing = new();
    readonly Debouncer _filter_delay;
    IReadOnlyDictionary<FurniKey, IReadOnlyList<FurniShopOffer>>? _offers;
    FurniRowViewModel[] _visible = [];
    MemberGate _scan_gate = MemberGate.Open;
    MemberGate _buy_gate = MemberGate.Open;
    bool _reset_scroll;
    bool _reset_after_apply;
    bool _loaded;
    string _missing = "";

    public FurniTabViewModel(
        IGameGateway gateway,
        IDialogService dialogs,
        IClipboardService clipboard,
        IMarketplacePrices prices,
        ShopCatalog shop,
        MarketplaceTrade trade,
        NoticeLine notices,
        IUiDispatcher dispatcher,
        TimeProvider time,
        CancellationToken lifetime)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        _trade = trade ?? throw new ArgumentNullException(nameof(trade));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _lifetime = lifetime;
        ArgumentNullException.ThrowIfNull(dispatcher);
        _filter_delay = Own(new Debouncer(dispatcher, time, FilterDelay, ApplyFilter));
        Copy = Own(new CopyAction(clipboard, dispatcher, time, SelectedText, Failed));
        Rows.Applied += OnRowsApplied;
        Own(() => Rows.Applied -= OnRowsApplied);
        Selection.Changed += OnSelectionChanged;
        Own(() => Selection.Changed -= OnSelectionChanged);
        shop.Invalidated += OnShopInvalidated;
        Own(() => shop.Invalidated -= OnShopInvalidated);
        ScanLabel = "Show shop prices";
        RefreshGates();
        RefreshState();
    }

    public FilteredRows<FurniRowViewModel> Rows { get; } = new();

    public SelectionList<FurniRowViewModel> Selection { get; } = new();

    public ScrollRequest Scroll { get; } = new();

    public SortRefreshRequest SortRefresh { get; } = new();

    public ViewState State { get; } = new();

    public CopyAction Copy { get; }

    public IReadOnlyList<PlacementChoice> PlacementChoices => PlacementChoice.All;

    public IReadOnlyList<AvailabilityChoice> AvailabilityChoices => AvailabilityChoice.All;

    public IReadOnlyList<BuySourceChoice> BuySourceChoices => BuySourceChoice.All;

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial PlacementChoice Placement { get; set; } = PlacementChoice.All[0];

    [ObservableProperty]
    public partial AvailabilityChoice Availability { get; set; } = AvailabilityChoice.All[0];

    [ObservableProperty]
    public partial string LineFilter { get; set; } = "";

    [ObservableProperty]
    public partial string CategoryFilter { get; set; } = "";

    [ObservableProperty]
    public partial string MinPrice { get; set; } = "";

    [ObservableProperty]
    public partial string MaxPrice { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowKindColumn { get; set; }

    [ObservableProperty]
    public partial bool ShowPlaceColumn { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BuyCommand))]
    public partial BuySourceChoice Source { get; set; } = BuySourceChoice.All[0];

    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    [ObservableProperty]
    public partial string FilterCountText { get; private set; } = "";

    [ObservableProperty]
    public partial string ScanLabel { get; private set; }

    [ObservableProperty]
    public partial string? ScanTip { get; private set; }

    [ObservableProperty]
    public partial string? BuyTip { get; private set; }

    [ObservableProperty]
    public partial OperationProgress? Progress { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanShopCommand), nameof(BuyCommand))]
    public partial bool IsScanning { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanShopCommand), nameof(BuyCommand))]
    public partial bool IsBuying { get; private set; }

    public void Load(IReadOnlyList<FurniSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _loaded = true;
        _missing = "";
        _all.Sync(snapshots, snapshot => new FurniKey(snapshot.Type, snapshot.Kind), Create, Update);
        ApplyOffers();
        ApplyFilter();
    }

    public void ShowMissing(string reason)
    {
        _loaded = false;
        _missing = reason;
        _all.Clear();
        ApplyFilter();
    }

    public void SetShopOffers(IReadOnlyDictionary<FurniKey, IReadOnlyList<FurniShopOffer>>? offers)
    {
        _offers = offers;
        ApplyOffers();
    }

    void OnShopInvalidated(string? reason)
    {
        ClearShop();
        if (reason is not null)
            _notices.Show(NoticeSeverity.Warning, reason);
    }

    void ClearShop()
    {
        if (ScanShopCommand.IsRunning)
            ScanShopCancelCommand.Execute(null);
        if (BuyCommand.IsRunning)
            BuyCancelCommand.Execute(null);
        SetShopOffers(null);
        RefreshScanLabel();
        RequestFilter(false);
    }

    public void RefreshGates()
    {
        _scan_gate = _gateway.Gate(ApplicationMemberIds.CatalogPagesLoad);
        _buy_gate = BuyGate(Source.Value);
        ScanTip = _scan_gate.Available ? "Read every catalog page and cache the results" : _scan_gate.Reason;
        BuyTip = _buy_gate.Available ? null : _buy_gate.Reason;
        ScanShopCommand.NotifyCanExecuteChanged();
        BuyCommand.NotifyCanExecuteChanged();
    }

    public bool TryClearSearch()
    {
        bool cleared = SearchText.Length > 0 || CurrentFilter().ActiveCount > 0;
        if (!cleared)
            return false;
        SearchText = "";
        ResetFilters();
        return true;
    }

    public void VisibleChanged(IReadOnlyList<object> visible)
    {
        ArgumentNullException.ThrowIfNull(visible);
        _visible = [.. visible.OfType<FurniRowViewModel>()];
        PriceVisibleAsync().Observe("ui");
    }

    [RelayCommand]
    void ClearFilters() => ResetFilters();

    [RelayCommand]
    void ClearSearchAndFilters()
    {
        SearchText = "";
        ResetFilters();
    }

    [RelayCommand(CanExecute = nameof(CanScanShop), IncludeCancelCommand = true)]
    async Task ScanShopAsync(CancellationToken cancellation_token)
    {
        long invalidations = _shop.Invalidations;
        IsScanning = true;
        RefreshScanLabel();
        Progress = new OperationProgress("Reading the shop catalog…", null, ScanShopCancelCommand);
        try
        {
            CatalogStateView state = await _shop.StateAsync(cancellation_token);
            CatalogLoadView report = await _shop.LoadPagesAsync(state.SessionGeneration, state.CatalogGeneration, cancellation_token);
            cancellation_token.ThrowIfCancellationRequested();
            IReadOnlyDictionary<FurniKey, IReadOnlyList<FurniShopOffer>>? offers =
                await _shop.MergeAsync(report.SessionGeneration, report.CatalogGeneration, cancellation_token);
            if (offers is null)
            {
                _shop.Invalidate("The shop changed while it was being scanned. Scan it again.");
                return;
            }
            _shop.MarkLoaded(report.Refused == 0);
            SetShopOffers(offers);
            RequestFilter(false);
            _notices.Show(
                report.Refused == 0 ? NoticeSeverity.Success : NoticeSeverity.Warning,
                report.Refused == 0
                    ? $"Shop prices loaded from {report.Available:N0} pages."
                    : $"Shop prices loaded · {report.Refused:N0} of {report.Total:N0} pages did not answer.");
        }
        catch (OperationCanceledException)
        {
            if (_shop.Invalidations == invalidations)
                _notices.Show(NoticeSeverity.Info, "Shop scan cancelled.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _notices.Show(NoticeSeverity.Error, $"Shop scan failed: {error.Message}");
        }
        finally
        {
            IsScanning = false;
            Progress = null;
            RefreshScanLabel();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBuy), IncludeCancelCommand = true)]
    async Task BuyAsync(CancellationToken cancellation_token)
    {
        if (Selection.First is not { } row)
            return;
        IsBuying = true;
        RefreshScanLabel();
        Progress = new OperationProgress("Checking the price…", null, BuyCancelCommand);
        try
        {
            await BuyRowAsync(row, cancellation_token);
        }
        catch (OperationCanceledException)
        {
            _notices.Show(NoticeSeverity.Info, "Purchase cancelled.");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _notices.Show(NoticeSeverity.Error, $"Purchase failed: {error.Message}");
        }
        finally
        {
            IsBuying = false;
            Progress = null;
            RefreshScanLabel();
        }
    }

    bool CanScanShop() => !IsBuying && _scan_gate.Available;

    bool CanBuy() => !IsBuying && Selection.First is not null && _buy_gate.Available;

    partial void OnSearchTextChanged(string value) => RequestFilter(true);

    partial void OnPlacementChanged(PlacementChoice value) => RequestFilter(true);

    partial void OnAvailabilityChanged(AvailabilityChoice value) => RequestFilter(true);

    partial void OnLineFilterChanged(string value) => RequestFilter(true);

    partial void OnCategoryFilterChanged(string value) => RequestFilter(true);

    partial void OnMinPriceChanged(string value) => RequestFilter(true);

    partial void OnMaxPriceChanged(string value) => RequestFilter(true);

    partial void OnSourceChanged(BuySourceChoice value) => RefreshGates();

    protected override void OnDisposed()
    {
        _pricing.StopAsync().Observe("ui");
        base.OnDisposed();
    }

    void ResetFilters()
    {
        Placement = PlacementChoice.All[0];
        Availability = AvailabilityChoice.All[0];
        LineFilter = "";
        CategoryFilter = "";
        MinPrice = "";
        MaxPrice = "";
    }

    FurniFilter CurrentFilter() =>
        new(SearchText, Placement.Value, Availability.Value, LineFilter, CategoryFilter, MinPrice, MaxPrice);

    void RequestFilter(bool reset_scroll)
    {
        if (reset_scroll)
            _reset_scroll = true;
        _filter_delay.Trigger();
    }

    void ApplyFilter()
    {
        if (IsDisposed)
            return;
        _reset_after_apply |= _reset_scroll;
        _reset_scroll = false;
        FurniFilter filter = CurrentFilter();
        FilterCountText = filter.ActiveCount > 0 ? filter.ActiveCount.ToString(CultureInfo.CurrentCulture) : "";
        var query = new FurniQuery(filter);
        Rows.ApplyAsync(_all.Rows, query.Keep, query, _lifetime).Observe("ui");
    }

    FurniRowViewModel Create(FurniSnapshot snapshot)
    {
        var row = new FurniRowViewModel(snapshot)
        {
            Market = _prices.Known(new MarketplaceKind(snapshot.Type, snapshot.Identifier))
        };
        return row;
    }

    void Update(FurniRowViewModel row, FurniSnapshot snapshot) => row.Apply(snapshot);

    void ApplyOffers()
    {
        foreach (FurniRowViewModel row in _all.Rows)
        {
            IReadOnlyList<FurniShopOffer>? offers = _offers?.GetValueOrDefault(row.Kinds);
            row.SetShopOffers(offers ?? []);
        }
    }

    void OnRowsApplied()
    {
        Selection.Prune(Rows.Visible);
        RefreshCount();
        RefreshState();
        if (!_reset_after_apply)
            return;
        _reset_after_apply = false;
        Scroll.Reset();
    }

    void OnSelectionChanged() => BuyCommand.NotifyCanExecuteChanged();

    void RefreshCount()
    {
        int total = _all.Count;
        int shown = Rows.Visible.Count;
        CountText = shown == total
            ? total == 1 ? $"{total:N0} definition" : $"{total:N0} definitions"
            : $"{shown:N0} of {total:N0} definitions";
    }

    void RefreshState()
    {
        if (!_loaded)
        {
            State.ShowEmpty(IconKind.GameData, "The hotel's game data has not arrived yet.", _missing);
            return;
        }
        if (Rows.Visible.Count > 0)
        {
            State.ShowReady();
            return;
        }
        FurniFilter filter = CurrentFilter();
        string term = filter.Term.Trim();
        if (term.Length == 0 && filter.ActiveCount == 0)
        {
            State.ShowEmpty(IconKind.GameData, "There are no furni definitions.");
            return;
        }
        State.ShowEmpty(
            IconKind.Search,
            "No matches",
            term.Length > 0 ? $"Nothing matches “{term}”." : "Nothing matches these filters.",
            ClearSearchAndFiltersCommand,
            "Clear filters");
    }

    void RefreshScanLabel()
    {
        ScanLabel = IsScanning ? "Scanning…" : _shop.IsLoaded ? "Refresh shop prices" : "Show shop prices";
        RefreshGates();
    }

    MemberGate BuyGate(BuySource source)
    {
        if (source == BuySource.Marketplace)
            return _gateway.Gate(ApplicationMemberIds.MarketplaceOfferBuy);
        MemberGate shop = _gateway.Gate(ApplicationMemberIds.CatalogPurchaseSend);
        if (source == BuySource.Shop)
            return shop;
        MemberGate market = _gateway.Gate(ApplicationMemberIds.MarketplaceOfferBuy);
        return shop.Available ? market : shop;
    }

    string? SelectedText()
    {
        IReadOnlyList<FurniRowViewModel> rows = Selection.HasAny ? Selection.Items : [.. Rows.Visible];
        if (rows.Count == 0)
            return null;
        var lines = new List<string>(rows.Count);
        foreach (FurniRowViewModel row in rows)
        {
            List<string> cells = [row.Name, row.Identifier, row.ShopText, row.MarketText, row.Line, row.Category];
            if (ShowKindColumn)
                cells.Add(row.Kind.ToString(CultureInfo.InvariantCulture));
            if (ShowPlaceColumn)
                cells.Add(row.Placement);
            lines.Add(string.Join('\t', cells));
        }
        return string.Join(Environment.NewLine, lines);
    }

    void Failed(string message) => _notices.Show(NoticeSeverity.Warning, message);

    async Task PriceVisibleAsync()
    {
        OperationLease lease = await _pricing.StartAsync(_lifetime);
        try
        {
            FurniRowViewModel[] rows = _visible.Length > 0 ? _visible : [.. Rows.Visible.Take(PriceBatchSize)];
            List<FurniRowViewModel> pending = [.. rows.Where(row => row.Market is null && row.Identifier.Length > 0)];
            if (pending.Count == 0)
                return;
            MarketplaceKind[] wanted = [.. pending.Select(row => new MarketplaceKind(row.Type, row.Identifier)).Distinct()];
            Dictionary<MarketplaceKind, MarketplacePrice> found = [];
            for (int start = 0; start < wanted.Length; start += PriceBatchSize)
            {
                MarketplaceKind[] batch = [.. wanted.Skip(start).Take(PriceBatchSize)];
                foreach ((MarketplaceKind kind, MarketplacePrice price) in await _prices.FetchAsync(batch, lease.Token))
                    found[kind] = price;
                if (start + PriceBatchSize < wanted.Length)
                    await Task.Delay(PriceBatchGap, _time, lease.Token);
            }
            lease.Token.ThrowIfCancellationRequested();
            if (!_pricing.IsCurrent(lease))
                return;
            bool changed = false;
            foreach (FurniRowViewModel row in pending)
            {
                var kind = new MarketplaceKind(row.Type, row.Identifier);
                if (found.TryGetValue(kind, out MarketplacePrice? price))
                {
                    row.Market = price;
                    changed = true;
                }
                else if (_prices.WasRead(kind))
                {
                    row.Market = new MarketplacePrice(row.Type, row.Identifier, null, null, 0, 0);
                    changed = true;
                }
            }
            if (!changed)
                return;
            SortRefresh.Request();
            if (CurrentFilter().NeedsPrices)
                RequestFilter(false);
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Marketplace prices could not be read: {error.Message}", "ui");
        }
        finally
        {
            _pricing.Complete(lease);
        }
    }

    async Task BuyRowAsync(FurniRowViewModel row, CancellationToken cancellation_token)
    {
        BuySource source = Source.Value;
        if (source == BuySource.Cheapest && !_shop.IsComplete)
        {
            _notices.Show(
                NoticeSeverity.Warning,
                _shop.IsLoaded
                    ? "Refresh the shop until every page answers before choosing the cheapest source."
                    : "Load shop prices before choosing the cheapest source.");
            return;
        }
        int? shown_market = row.Market?.CurrentPrice;
        FurniShopOffer? shown_shop = row.ShopOffer;
        MarketplaceOfferSnapshot? market = source is BuySource.Marketplace or BuySource.Cheapest
            ? await _trade.FreshOfferAsync(row.Name, row.Identifier, row.Kinds, cancellation_token)
            : null;
        FurniShopOffer? shop = (source is BuySource.Shop or BuySource.Cheapest) && shown_shop is not null
            ? await _shop.FreshOfferAsync(shown_shop, row.Kinds, cancellation_token)
            : null;
        cancellation_token.ThrowIfCancellationRequested();
        if (source is BuySource.Marketplace or BuySource.Cheapest)
            SetFreshMarket(row, market);
        if ((source is BuySource.Marketplace or BuySource.Cheapest) && market is not null)
        {
            if (shown_market is null)
            {
                _notices.Show(NoticeSeverity.Info, $"Marketplace price loaded: {market.Price}c. Review it and click Buy again.");
                return;
            }
            if (market.Price > shown_market)
            {
                _notices.Show(NoticeSeverity.Warning, $"Marketplace price rose from {shown_market}c to {market.Price}c. Nothing was bought.");
                return;
            }
        }
        if (shown_shop is not null && shop is not null)
        {
            row.ReplaceShopOffer(shop);
            if (!shop.UsesSameCurrencies(shown_shop))
            {
                _notices.Show(NoticeSeverity.Warning, "The shop price changed currency. Nothing was bought.");
                return;
            }
            if (shop.CostsMoreThan(shown_shop))
            {
                _notices.Show(NoticeSeverity.Warning, $"Shop price rose from {shown_shop.PriceText} to {shop.PriceText}. Nothing was bought.");
                return;
            }
        }
        switch (source)
        {
            case BuySource.Marketplace when market is null:
                _notices.Show(NoticeSeverity.Warning, "No current marketplace offer was found for this furni.");
                return;
            case BuySource.Marketplace:
                await BuyMarketplaceAsync(row, market, cancellation_token);
                return;
            case BuySource.Shop when shown_shop is null || shop is null:
                _notices.Show(NoticeSeverity.Warning, "This furni is not available in the loaded shop.");
                return;
            case BuySource.Shop:
                await BuyShopAsync(row, shop, cancellation_token);
                return;
        }
        if (market is null && shop is null)
        {
            _notices.Show(NoticeSeverity.Warning, "No current shop or marketplace offer was found.");
            return;
        }
        if (market is not null && shop is { IsCreditOnly: false })
        {
            _notices.Show(NoticeSeverity.Warning, "Shop and marketplace use different currencies. Choose the source explicitly.");
            return;
        }
        if (shop is not null && (market is null || shop.PriceInCredits <= market.Price))
        {
            await BuyShopAsync(row, shop, cancellation_token);
            return;
        }
        if (market is not null)
            await BuyMarketplaceAsync(row, market, cancellation_token);
    }

    async Task BuyMarketplaceAsync(FurniRowViewModel row, MarketplaceOfferSnapshot offer, CancellationToken cancellation_token)
    {
        if (!await ConfirmAsync(row.Name, "Marketplace", $"{offer.Price}c", cancellation_token))
        {
            _notices.Show(NoticeSeverity.Info, "Purchase cancelled.");
            return;
        }
        MarketplaceBuyResult result = await _trade.BuyAsync(offer.OfferId, cancellation_token);
        switch (result.ResultCode)
        {
            case MarketplaceBuyResultCode.Success:
                _notices.Show(NoticeSeverity.Success, $"Bought {row.Name} from the marketplace for {offer.Price}c.");
                return;
            case MarketplaceBuyResultCode.OfferUnavailable:
                _notices.Show(NoticeSeverity.Warning, "The marketplace offer is no longer available.");
                return;
            case MarketplaceBuyResultCode.OfferUpdated:
                _notices.Show(NoticeSeverity.Warning, $"The offer changed to {result.NewPrice}c. Nothing was bought.");
                return;
            case MarketplaceBuyResultCode.NotEnoughCredits:
                _notices.Show(NoticeSeverity.Warning, "Not enough credits for this marketplace offer.");
                return;
            default:
                _notices.Show(NoticeSeverity.Warning, $"Marketplace refused the purchase with result {result.Result}.");
                return;
        }
    }

    async Task BuyShopAsync(FurniRowViewModel row, FurniShopOffer offer, CancellationToken cancellation_token)
    {
        if (!await ConfirmAsync(row.Name, "Shop", offer.PriceText, cancellation_token))
        {
            _notices.Show(NoticeSeverity.Info, "Purchase cancelled.");
            return;
        }
        cancellation_token.ThrowIfCancellationRequested();
        CatalogPurchaseDispatchReceipt receipt = await _shop.SendPurchaseAsync(offer, cancellation_token);
        if (!ShopCatalog.ReceiptMatches(receipt, offer))
            throw new InvalidOperationException("The catalog purchase dispatch receipt is invalid.");
        _notices.Show(NoticeSeverity.Success, $"Shop purchase sent for {row.Name} at {offer.PriceText}.");
    }

    Task<bool> ConfirmAsync(string name, string source, string price, CancellationToken cancellation_token) =>
        _dialogs.ConfirmAsync(
            "Confirm purchase",
            $"Buy “{name}” from the {source} for {price}?",
            "Buy",
            DialogTone.Neutral,
            null,
            cancellation_token);

    static void SetFreshMarket(FurniRowViewModel row, MarketplaceOfferSnapshot? offer)
    {
        int? average = offer is { AveragePrice: > 0 } ? offer.AveragePrice : row.Market?.AveragePrice;
        row.Market = new MarketplacePrice(row.Type, row.Identifier, offer?.Price, average, offer?.Offers ?? 0, offer?.TradeVolume ?? 0);
    }
}
