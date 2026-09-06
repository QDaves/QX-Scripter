using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Threading;
using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Ui;

public sealed record FurniShopOffer(
    int PageId,
    int OfferId,
    string Page,
    int Amount,
    int PriceInCredits,
    int PriceInActivityPoints,
    int ActivityPointType,
    int PriceInSilver)
{
    public bool IsCreditOnly => PriceInActivityPoints == 0 && PriceInSilver == 0;

    public string PriceText
    {
        get
        {
            var parts = new List<string>();
            if (PriceInCredits > 0)
                parts.Add($"{PriceInCredits}c");
            if (PriceInActivityPoints > 0)
                parts.Add($"{PriceInActivityPoints} AP{ActivityPointType}");
            if (PriceInSilver > 0)
                parts.Add($"{PriceInSilver} silver");
            return parts.Count == 0 ? "free" : string.Join(" + ", parts);
        }
    }

    public string Details => Amount > 1
        ? $"{PriceText} · {Amount} items · {Page}"
        : $"{PriceText} · {Page}";

    public bool UsesSameCurrencies(FurniShopOffer other) =>
        (PriceInCredits > 0) == (other.PriceInCredits > 0) &&
        (PriceInActivityPoints > 0) == (other.PriceInActivityPoints > 0) &&
        (PriceInSilver > 0) == (other.PriceInSilver > 0) &&
        (PriceInActivityPoints == 0 || ActivityPointType == other.ActivityPointType);

    public bool CostsMoreThan(FurniShopOffer other) =>
        PriceInCredits > other.PriceInCredits ||
        PriceInActivityPoints > other.PriceInActivityPoints ||
        PriceInSilver > other.PriceInSilver;
}

public sealed class FurniDefinitionRow(string? imageUrl = null) : RemoteImage
{
    public required string Name { get; init; }
    public required string Identifier { get; init; }
    public required int Kind { get; init; }
    public required string Placement { get; init; }
    public string Line { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public override string? ImageUrl { get; } = imageUrl;
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";
    public ItemType Kinds => Placement == "wall" ? ItemType.Wall : ItemType.Floor;

    private MarketplacePrice? _market;
    private IReadOnlyList<FurniShopOffer> _shop_offers = [];

    public MarketplacePrice? Market
    {
        get => _market;
        set
        {
            _market = value;
            Raise(nameof(Market));
            Raise(nameof(MarketText));
            Raise(nameof(HasMarketplace));
            Raise(nameof(CheapestCredits));
        }
    }

    public IReadOnlyList<FurniShopOffer> ShopOffers => _shop_offers;
    public FurniShopOffer? ShopOffer => _shop_offers.FirstOrDefault();
    public bool HasMarketplace => Market?.CurrentPrice is not null;
    public bool HasShop => _shop_offers.Count > 0;
    public int? ShopCredits => ShopOffer is { IsCreditOnly: true } offer ? offer.PriceInCredits : null;
    public string ShopText => ShopOffer?.PriceText ?? "—";
    public string ShopDetails => ShopOffer?.Details ?? "";

    public int? CheapestCredits
    {
        get
        {
            int? market = Market?.CurrentPrice;
            int? shop = ShopCredits;
            return market is null ? shop : shop is null ? market : Math.Min(market.Value, shop.Value);
        }
    }

    public string MarketText => Market is null
        ? ""
        : Market.IsCurrent
            ? $"{Market.CurrentPrice}c"
            : Market.IsKnown
                ? $"~{Market.AveragePrice}c"
                : "—";

    public void SetShopOffers(IEnumerable<FurniShopOffer> offers)
    {
        _shop_offers =
        [
            .. offers
                .OrderBy(offer => offer.IsCreditOnly ? 0 : 1)
                .ThenBy(offer => offer.PriceInCredits)
                .ThenBy(offer => offer.PriceInActivityPoints)
                .ThenBy(offer => offer.PriceInSilver)
                .ThenBy(offer => offer.OfferId)
        ];
        Raise(nameof(ShopOffers));
        Raise(nameof(ShopOffer));
        Raise(nameof(HasShop));
        Raise(nameof(ShopCredits));
        Raise(nameof(ShopText));
        Raise(nameof(ShopDetails));
        Raise(nameof(CheapestCredits));
    }

    public void ReplaceShopOffer(FurniShopOffer offer) =>
        SetShopOffers(_shop_offers
            .Where(current => current.PageId != offer.PageId || current.OfferId != offer.OfferId)
            .Append(offer));
}

public sealed record KeyValueRow(string Key, string Value);

public partial class GameDataPage : GamePage
{
    private sealed class ScanRun
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly AsyncManualResetEvent _cancelled = new();
        private readonly AsyncManualResetEvent _completion = new();
        private bool _cancelling;
        private bool _completing;

        public CancellationToken Token => _cancellation.Token;

        public async Task CancelAsync()
        {
            bool cancel;
            bool completing;
            lock (_sync)
            {
                completing = _completing;
                cancel = !_cancelling && !completing;
                if (cancel)
                    _cancelling = true;
            }

            if (completing)
            {
                await _completion.WaitAsync().ConfigureAwait(false);
                return;
            }
            if (!cancel)
            {
                await _cancelled.WaitAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await _cancellation.CancelAsync().ConfigureAwait(false);
            }
            finally
            {
                _cancelled.Set();
            }
        }

        public async Task CompleteAsync()
        {
            bool complete;
            bool wait_for_cancellation;
            lock (_sync)
            {
                complete = !_completing;
                if (complete)
                    _completing = true;
                wait_for_cancellation = _cancelling;
            }

            if (!complete)
            {
                await _completion.WaitAsync().ConfigureAwait(false);
                return;
            }
            if (wait_for_cancellation)
                await _cancelled.WaitAsync().ConfigureAwait(false);

            _cancellation.Dispose();
            _completion.Set();
        }

        public Task WaitForCompletionAsync()
        {
            return _completion.WaitAsync();
        }
    }

    private IReadOnlyList<FurniDefinitionRow> _furni = [];
    private IReadOnlyList<KeyValueRow> _texts = [];
    private IReadOnlyList<KeyValueRow> _variables = [];
    private IReadOnlyList<KeyValueRow> _products = [];
    private ScanRun? _price_scan;
    private ScanRun? _shop_scan;
    private ScanRun? _buy_scan;
    private IDisposable? _catalog_publications;
    private bool _shop_loaded;
    private bool _shop_complete;
    private bool _buying;
    private long _catalog_subscription_generation;
    private long _shop_session_generation;
    private long _shop_catalog_generation;

    private readonly System.Windows.Threading.DispatcherTimer _price_delay = new()
    {
        Interval = TimeSpan.FromMilliseconds(750)
    };

    public GameDataPage()
    {
        InitializeComponent();
        _price_delay.Tick += AskForPrices;
        ComboBoxPopupBackground.Apply(BuySource);
        ComboBoxPopupBackground.Apply(FurniTypeFilter);
        ComboBoxPopupBackground.Apply(FurniAvailabilityFilter);
    }

    public override bool IsSearching =>
        FurniFilter.Text.Length > 0 || FurniLineFilter.Text.Length > 0 ||
        FurniCategoryFilter.Text.Length > 0 || FurniMinPrice.Text.Length > 0 ||
        FurniMaxPrice.Text.Length > 0 || TextsFilter.Text.Length > 0 ||
        VariablesFilter.Text.Length > 0 || ProductsFilter.Text.Length > 0 ||
        SelectedTag(FurniTypeFilter) != "all" ||
        SelectedTag(FurniAvailabilityFilter) != "all";

    protected override void Attach(GameState game) => game.GameData.Loaded += RefreshIfVisible;

    protected override void Detach(GameState game)
    {
        game.GameData.Loaded -= RefreshIfVisible;
        if (Volatile.Read(ref _price_scan) is { } price_scan)
            Observe(() => StopRunAsync(price_scan));
        InvalidateShop(null);
    }

    protected override void AttachApplication(IApplicationRuntime application)
    {
        long generation = Interlocked.Increment(ref _catalog_subscription_generation);
        _catalog_publications = application.Subscribe<CatalogPublishedEvent>(
            ApplicationMemberIds.CatalogPublished,
            _ => CatalogRepublished(application, generation));
    }

    protected override void DetachApplication(IApplicationRuntime application)
    {
        Interlocked.Increment(ref _catalog_subscription_generation);
        _catalog_publications?.Dispose();
        _catalog_publications = null;
        InvalidateShop(null);
    }

    public override void Refresh()
    {
        GameData? data = Game?.GameData;

        if (data is null || !data.IsLoaded)
        {
            _furni = [];
            _texts = [];
            _variables = [];
            _products = [];
            EmptyNotice.Visibility = Visibility.Visible;
            EmptyText.Text = Game is null
                ? "Connect to load the hotel's game data."
                : "The hotel's game data has not arrived yet.";
            Subheading.Text = "";
            ApplyAll();
            return;
        }

        EmptyNotice.Visibility = Visibility.Collapsed;
        _furni = data.Furni is null
            ? []
            : [.. data.Furni.FloorItems.Select(info => Row(info, "floor"))
                .Concat(data.Furni.WallItems.Select(info => Row(info, "wall")))
                .OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)];
        _texts = data.Texts is null
            ? []
            : [.. data.Texts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new KeyValueRow(pair.Key, pair.Value))];
        _variables = data.Variables is null
            ? []
            : [.. data.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new KeyValueRow(pair.Key, pair.Value))];
        _products = data.Products is null
            ? []
            : [.. data.Products.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new KeyValueRow(pair.Key, Describe(pair.Value)))];

        if (Game is { } game && Application is { } application)
            MergeShop(game, application);
        else
            ClearShopOffers();
        ShopScanLabel.Text = _shop_loaded ? "Refresh shop prices" : "Show shop prices";
        Subheading.Text =
            $"{_furni.Count:N0} furni · {_texts.Count:N0} texts · " +
            $"{_variables.Count:N0} variables · {_products.Count:N0} products";
        ApplyAll();
    }

    private static string Describe(ProductInfo product) =>
        product.Description.Length > 0 ? $"{product.Name} — {product.Description}" : product.Name;

    private string Named(FurniInfo info)
    {
        if (info.Name is not { Length: > 2 } name)
            return info.Identifier;
        if (name[0] != '$' || name[^1] != '$')
            return name;
        return Localized(name) ?? info.Identifier;
    }

    private string? Localized(string? key)
    {
        if (key is not { Length: > 0 } value || Game?.GameData.Texts is not { } texts)
            return null;

        string normalized = value.Length > 2 && value[0] == '$' && value[^1] == '$'
            ? value[1..^1]
            : value;
        return texts.TryGet(normalized, out string resolved) && resolved.Length > 0
            ? resolved
            : null;
    }

    private FurniDefinitionRow Row(FurniInfo info, string placement)
    {
        string name = DisplayText(Named(info));
        var row = new FurniDefinitionRow(HabboImages.FurniIconUrl(info.Revision, info.Identifier))
        {
            Name = name.Length > 0 ? name : info.Identifier,
            Identifier = info.Identifier,
            Kind = info.Kind,
            Placement = placement,
            Line = DisplayText(info.Line),
            Category = DisplayText(info.Category),
            Description = DisplayText(info.Description)
        };
        row.Market = MarketplacePrices.Known(row.Kinds, row.Identifier);
        return row;
    }

    private static string DisplayText(string? value) =>
        string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private bool MergeShop(
        GameState game,
        IApplicationRuntime application,
        long? expected_session_generation = null,
        long? expected_catalog_generation = null)
    {
        CatalogStateView before = application.Invoke<CatalogStateRequest, CatalogStateView>(
            ApplicationMemberIds.CatalogState,
            new CatalogStateRequest());
        if ((expected_session_generation is { } session_generation &&
                before.SessionGeneration != session_generation) ||
            (expected_catalog_generation is { } catalog_generation &&
                before.CatalogGeneration != catalog_generation))
        {
            return false;
        }

        Dictionary<(ItemType Type, int Kind), FurniShopOffer[]> offers = game.Catalog
            .CachedOffers()
            .SelectMany(match => match.Offer.Products
                .Select(product => (Match: match, Product: product, Type: ProductItemType(product))))
            .Where(entry => entry.Type is not null && entry.Product.FurniClassId > 0)
            .GroupBy(entry => (entry.Type!.Value, entry.Product.FurniClassId))
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => ShopOffer(entry.Match, entry.Product)).ToArray());

        CatalogStateView after = application.Invoke<CatalogStateRequest, CatalogStateView>(
            ApplicationMemberIds.CatalogState,
            new CatalogStateRequest());
        if (!ReferenceEquals(Game, game) || !ReferenceEquals(Application, application) ||
            after.SessionGeneration != before.SessionGeneration ||
            after.CatalogGeneration != before.CatalogGeneration)
        {
            return false;
        }

        foreach (FurniDefinitionRow row in _furni)
        {
            row.SetShopOffers(offers.GetValueOrDefault((row.Kinds, row.Kind)) ?? []);
        }
        _shop_session_generation = after.SessionGeneration;
        _shop_catalog_generation = after.CatalogGeneration;
        return true;
    }

    private void CatalogRepublished(IApplicationRuntime application, long generation) => OnUi(() =>
    {
        if (generation != Volatile.Read(ref _catalog_subscription_generation) ||
            !ReferenceEquals(Application, application))
        {
            return;
        }
        InvalidateShop("The shop catalog changed. Scan it again.");
    });

    private void InvalidateShop(string? status)
    {
        if (Volatile.Read(ref _shop_scan) is { } shop_scan)
            Observe(() => StopRunAsync(shop_scan));
        if (Volatile.Read(ref _buy_scan) is { } buy_scan)
            Observe(() => StopRunAsync(buy_scan));
        _shop_loaded = false;
        _shop_complete = false;
        _shop_session_generation = 0;
        _shop_catalog_generation = 0;
        ClearShopOffers();
        ShopScanButton.IsEnabled = !_buying;
        ShopScanLabel.Text = "Show shop prices";
        if (_furni.Count > 0)
            ApplyFurni(false);
        if (status is not null)
            FurniStatus.Text = status;
    }

    private void ClearShopOffers()
    {
        foreach (FurniDefinitionRow row in _furni)
            row.SetShopOffers([]);
    }

    private FurniShopOffer ShopOffer(CatalogOfferMatch match, CatalogProduct product) =>
        new(
            match.Page.PageId,
            match.Offer.OfferId,
            match.Node?.Localization is { Length: > 0 } page
                ? Localized(page) ?? page
                : $"Page {match.Page.PageId}",
            product.ProductCount,
            match.Offer.PriceInCredits,
            match.Offer.PriceInActivityPoints,
            match.Offer.ActivityPointType,
            match.Offer.PriceInSilver);

    internal static ItemType? ProductItemType(CatalogProduct product)
    {
        if (product.UnityProductType is short unity_type)
        {
            return unity_type switch
            {
                0 => ItemType.Wall,
                1 => ItemType.Floor,
                _ => null
            };
        }

        return product.ProductType switch
        {
            CatalogProduct.TypeStuff => ItemType.Floor,
            CatalogProduct.TypeItem => ItemType.Wall,
            _ => null
        };
    }

    internal static ItemType? ProductItemType(CatalogProductView product)
    {
        if (product.UnityProductType is short unity_type)
        {
            return unity_type switch
            {
                0 => ItemType.Wall,
                1 => ItemType.Floor,
                _ => null
            };
        }

        return product.ProductType switch
        {
            CatalogProduct.TypeStuff => ItemType.Floor,
            CatalogProduct.TypeItem => ItemType.Wall,
            _ => null
        };
    }

    private void ApplyAll()
    {
        ApplyFurni();
        ApplyTexts();
        ApplyVariables();
        ApplyProducts();
    }

    private void ApplyFurni(bool ask_for_prices = true, bool return_to_top = false)
    {
        string term = FurniFilter.Text.Trim();
        IEnumerable<FurniDefinitionRow> rows = term.Length == 0
            ? _furni
            : _furni
                .Select(row => (Row: row, Rank: FurniSearch.Rank(
                    row.Name,
                    row.Identifier,
                    $"{row.Description} {row.Line} {row.Category}",
                    term)))
                .Where(entry => entry.Rank is not null ||
                    int.TryParse(term, out int kind) && entry.Row.Kind == kind)
                .OrderBy(entry => entry.Rank ?? 7)
                .ThenBy(entry => entry.Row.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => entry.Row);

        string type = SelectedTag(FurniTypeFilter);
        string availability = SelectedTag(FurniAvailabilityFilter);
        string line = FurniLineFilter.Text.Trim();
        string category = FurniCategoryFilter.Text.Trim();
        int? minimum = Price(FurniMinPrice.Text);
        int? maximum = Price(FurniMaxPrice.Text);

        rows = rows.Where(row =>
            (type == "all" || row.Placement == type) &&
            Available(row, availability) &&
            (line.Length == 0 || row.Line.Contains(line, StringComparison.CurrentCultureIgnoreCase)) &&
            (category.Length == 0 || row.Category.Contains(category, StringComparison.CurrentCultureIgnoreCase)) &&
            (minimum is null || row.CheapestCredits is int low && low >= minimum) &&
            (maximum is null || row.CheapestCredits is int high && high <= maximum));

        List<FurniDefinitionRow> shown = [.. rows];
        UpdateFurniRows(FurniList, shown);
        if (return_to_top)
            TreeLookup.FirstChild<ScrollViewer>(FurniList)?.ScrollToTop();
        FurniStatus.Text = Count(shown.Count, _furni.Count, "definition", "definitions");
        int filter_count = ActiveFilterCount();
        FurniFilterCount.Text = filter_count > 0 ? filter_count.ToString() : "";
        if (ask_for_prices)
            AskForPricesLater();
    }

    private static bool Available(FurniDefinitionRow row, string availability) => availability switch
    {
        "marketplace" => row.HasMarketplace,
        "shop" => row.HasShop,
        "both" => row.HasMarketplace && row.HasShop,
        _ => true
    };

    private static int? Price(string text) =>
        int.TryParse(text.Trim(), out int value) && value >= 0 ? value : null;

    private int ActiveFilterCount()
    {
        int count = 0;
        if (SelectedTag(FurniTypeFilter) != "all") count++;
        if (SelectedTag(FurniAvailabilityFilter) != "all") count++;
        if (FurniLineFilter.Text.Length > 0) count++;
        if (FurniCategoryFilter.Text.Length > 0) count++;
        if (FurniMinPrice.Text.Length > 0) count++;
        if (FurniMaxPrice.Text.Length > 0) count++;
        return count;
    }

    private static string SelectedTag(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";

    private void AskForPricesLater()
    {
        _price_delay.Stop();
        _price_delay.Start();
    }

    private void AskForPrices(object? sender, EventArgs e) => Observe(AskForPricesAsync);

    private static async Task StopRunAsync(ScanRun scan)
    {
        try
        {
            await scan.CancelAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            Qx.Diagnostics.Diag.Error(error.ToString(), "ui");
        }
        await scan.WaitForCompletionAsync().ConfigureAwait(true);
    }

    private async Task AskForPricesAsync()
    {
        _price_delay.Stop();
        var scan = new ScanRun();
        ScanRun? previous = Interlocked.Exchange(ref _price_scan, scan);
        try
        {
            if (previous is not null)
                await StopRunAsync(previous).ConfigureAwait(true);
            if (!ReferenceEquals(Volatile.Read(ref _price_scan), scan))
                return;
            await LoadVisiblePricesAsync(scan).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await scan.CompleteAsync().ConfigureAwait(true);
            Interlocked.CompareExchange(ref _price_scan, null, scan);
        }
    }

    private async Task LoadVisiblePricesAsync(ScanRun scan)
    {
        FurniDefinitionRow[] furni = [.. VisibleItems.Rows<FurniDefinitionRow>(FurniList)];
        if (furni.Length == 0 && FurniList.IsVisible)
            furni = [.. FurniList.Items.OfType<FurniDefinitionRow>().Take(MarketplacePrices.BatchSize)];

        (ItemType Type, string Identifier)[] wanted =
        [
            .. furni.Where(row => row.Market is null && row.Identifier.Length > 0)
                .Select(row => (row.Kinds, row.Identifier))
                .Distinct()
        ];
        if (wanted.Length == 0)
            return;

        var prices = new Dictionary<(ItemType Type, string Identifier), MarketplacePrice>();
        for (int start = 0; start < wanted.Length; start += MarketplacePrices.BatchSize)
        {
            (ItemType Type, string Identifier)[] batch =
                [.. wanted.Skip(start).Take(MarketplacePrices.BatchSize)];
            foreach (var entry in await MarketplacePrices.FetchAsync(batch, scan.Token).ConfigureAwait(true))
                prices[entry.Key] = entry.Value;
            if (start + MarketplacePrices.BatchSize < wanted.Length)
                await Task.Delay(150, scan.Token).ConfigureAwait(true);
        }

        scan.Token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Volatile.Read(ref _price_scan), scan))
            return;

        foreach (FurniDefinitionRow row in furni)
        {
            if (prices.TryGetValue((row.Kinds, row.Identifier), out MarketplacePrice? price))
                row.Market = price;
            else if (MarketplacePrices.WasRead(row.Kinds, row.Identifier))
                row.Market = new MarketplacePrice(row.Kinds, row.Identifier, null, null, 0, 0);
        }

        if (SelectedTag(FurniAvailabilityFilter) != "all" ||
            FurniMinPrice.Text.Length > 0 || FurniMaxPrice.Text.Length > 0)
        {
            ApplyFurni(false);
        }
    }

    private void ScanShop(object sender, RoutedEventArgs e) => Observe(ScanShopAsync);

    private async Task ScanShopAsync()
    {
        if (Game is not { } game || Application is not { } application)
        {
            FurniStatus.Text = "Connect before scanning the shop.";
            return;
        }

        var scan = new ScanRun();
        ScanRun? previous = Interlocked.Exchange(ref _shop_scan, scan);

        try
        {
            if (previous is not null)
                await StopRunAsync(previous).ConfigureAwait(true);
            if (!ReferenceEquals(Volatile.Read(ref _shop_scan), scan))
                return;

            _shop_complete = false;
            ShopScanButton.IsEnabled = false;
            ShopScanLabel.Text = "Scanning…";
            CatalogStateView state = application.Invoke<CatalogStateRequest, CatalogStateView>(
                ApplicationMemberIds.CatalogState,
                new CatalogStateRequest(),
                scan.Token);
            CatalogLoadView report = await application
                .InvokeAsync<CatalogLoadRequest, CatalogLoadView>(
                    ApplicationMemberIds.CatalogPagesLoad,
                    new CatalogLoadRequest(
                        OnlyVisible: false,
                        DelayMilliseconds: 150,
                        MaxAgeMilliseconds: _shop_loaded ? 0 : 300000,
                        TimeoutMilliseconds: 2500,
                        ExpectedSessionGeneration: state.SessionGeneration,
                        ExpectedCatalogGeneration: state.CatalogGeneration),
                    scan.Token)
                .ConfigureAwait(true);

            scan.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_shop_scan, scan) || !ReferenceEquals(Game, game) ||
                !ReferenceEquals(Application, application))
            {
                return;
            }
            if (!MergeShop(
                    game,
                    application,
                    report.SessionGeneration,
                    report.CatalogGeneration))
            {
                InvalidateShop("The shop changed while it was being scanned. Scan it again.");
                return;
            }
            _shop_loaded = true;
            _shop_complete = report.Refused == 0;
            ApplyFurni();
            FurniStatus.Text = report.Refused == 0
                ? $"Shop prices loaded from {report.Available:N0} pages."
                : $"Shop prices loaded · {report.Refused:N0} of {report.Total:N0} pages did not answer.";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_shop_scan, scan) && ReferenceEquals(Game, game) &&
                ReferenceEquals(Application, application))
            {
                FurniStatus.Text = "Shop scan cancelled.";
            }
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_shop_scan, scan) && ReferenceEquals(Game, game) &&
                ReferenceEquals(Application, application))
            {
                FurniStatus.Text = $"Shop scan failed: {error.Message}";
            }
        }
        finally
        {
            await scan.CompleteAsync().ConfigureAwait(true);
            bool current = ReferenceEquals(
                Interlocked.CompareExchange(ref _shop_scan, null, scan),
                scan);
            if (current && ReferenceEquals(Game, game) && ReferenceEquals(Application, application))
            {
                ShopScanButton.IsEnabled = !_buying;
                ShopScanLabel.Text = _shop_loaded ? "Refresh shop prices" : "Show shop prices";
            }
        }
    }

    private void BuySelected(object sender, RoutedEventArgs e) => Observe(BuySelectedAsync);

    private async Task BuySelectedAsync()
    {
        if (_buying || Game is not { } game || Application is not { } application ||
            FurniList.SelectedItem is not FurniDefinitionRow row)
            return;

        var scan = new ScanRun();
        ScanRun? previous = Interlocked.Exchange(ref _buy_scan, scan);

        try
        {
            if (previous is not null)
                await StopRunAsync(previous).ConfigureAwait(true);
            if (!ReferenceEquals(Volatile.Read(ref _buy_scan), scan))
                return;

            _buying = true;
            BuyButton.IsEnabled = false;
            ShopScanButton.IsEnabled = false;
            await BuyAsync(game, application, row, SelectedTag(BuySource), scan.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(_buy_scan, scan) && ReferenceEquals(Game, game) &&
                ReferenceEquals(Application, application))
            {
                FurniStatus.Text = "Purchase cancelled.";
            }
        }
        catch (Exception error)
        {
            if (ReferenceEquals(_buy_scan, scan) && ReferenceEquals(Game, game) &&
                ReferenceEquals(Application, application))
            {
                FurniStatus.Text = $"Purchase failed: {error.Message}";
            }
        }
        finally
        {
            await scan.CompleteAsync().ConfigureAwait(true);
            bool current = ReferenceEquals(
                Interlocked.CompareExchange(ref _buy_scan, null, scan),
                scan);
            if (current)
            {
                _buying = false;
            }
            if (current && ReferenceEquals(Game, game) && ReferenceEquals(Application, application))
            {
                BuyButton.IsEnabled = FurniList.SelectedItem is FurniDefinitionRow;
                ShopScanButton.IsEnabled = _shop_scan is null;
            }
        }
    }

    private async Task BuyAsync(
        GameState game,
        IApplicationRuntime application,
        FurniDefinitionRow row,
        string source,
        CancellationToken cancellation_token)
    {
        if (source == "cheapest" && !_shop_complete)
        {
            FurniStatus.Text = _shop_loaded
                ? "Refresh the shop until every page answers before choosing the cheapest source."
                : "Load shop prices before choosing the cheapest source.";
            return;
        }

        int? shown_market = row.Market?.CurrentPrice;
        FurniShopOffer? shown_shop = row.ShopOffer;
        MarketplaceOfferSnapshot? market = source is "marketplace" or "cheapest"
            ? await FreshMarketplaceOfferAsync(application, row, cancellation_token).ConfigureAwait(true)
            : null;
        FurniShopOffer? shop = (source is "shop" or "cheapest") && shown_shop is not null
            ? await FreshShopOfferAsync(application, row, shown_shop!, cancellation_token).ConfigureAwait(true)
            : null;

        cancellation_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Game, game) || !ReferenceEquals(Application, application) ||
            !ReferenceEquals(FurniList.SelectedItem, row))
        {
            return;
        }
        if (source is "marketplace" or "cheapest")
            SetFreshMarket(row, market);
        if ((source is "marketplace" or "cheapest") && market is not null)
        {
            if (shown_market is null)
            {
                FurniStatus.Text = $"Marketplace price loaded: {market.Price}c. Review it and click Buy again.";
                return;
            }
            if (market.Price > shown_market.Value)
            {
                FurniStatus.Text = $"Marketplace price rose from {shown_market}c to {market.Price}c. Nothing was bought.";
                return;
            }
        }

        if (shown_shop is not null && shop is not null)
        {
            row.ReplaceShopOffer(shop);
            if (!shop.UsesSameCurrencies(shown_shop))
            {
                FurniStatus.Text = "The shop price changed currency. Nothing was bought.";
                return;
            }
            if (shop.CostsMoreThan(shown_shop))
            {
                FurniStatus.Text = $"Shop price rose from {shown_shop.PriceText} to {shop.PriceText}. Nothing was bought.";
                return;
            }
        }

        if (source == "marketplace")
        {
            if (market is null)
            {
                FurniStatus.Text = "No current marketplace offer was found for this furni.";
                return;
            }
            await BuyMarketplaceAsync(application, row, market, cancellation_token).ConfigureAwait(true);
            return;
        }

        if (source == "shop")
        {
            if (shown_shop is null || shop is null)
            {
                FurniStatus.Text = "This furni is not available in the loaded shop.";
                return;
            }
            await BuyShopAsync(game, application, row, shop, cancellation_token).ConfigureAwait(true);
            return;
        }

        if (market is null && shop is null)
        {
            FurniStatus.Text = "No current shop or marketplace offer was found.";
            return;
        }
        if (market is not null && shop is not null && !shop.IsCreditOnly)
        {
            FurniStatus.Text = "Shop and marketplace use different currencies. Choose the source explicitly.";
            return;
        }
        if (shop is not null && (market is null || shop.PriceInCredits <= market.Price))
            await BuyShopAsync(game, application, row, shop, cancellation_token).ConfigureAwait(true);
        else
            await BuyMarketplaceAsync(application, row, market!, cancellation_token).ConfigureAwait(true);
    }

    private async Task<MarketplaceOfferSnapshot?> FreshMarketplaceOfferAsync(
        IApplicationRuntime application,
        FurniDefinitionRow row,
        CancellationToken cancellation_token)
    {
        foreach (string query in new[] { row.Name, row.Identifier }
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase))
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                MarketplaceOfferPage first = await application
                    .InvokeAsync<MarketplaceSearchRequest, MarketplaceOfferPage>(
                        ApplicationMemberIds.MarketplaceSearch,
                        new MarketplaceSearchRequest(
                            SearchQuery: query,
                            SortOrder: MarketplaceSortOrder.LowestPrice,
                            PageSize: 250),
                        cancellation_token)
                    .ConfigureAwait(true);
                if (MatchingOffer(first, row) is { } first_match)
                    return first_match;

                int pages = (first.CachedItems + first.PageSize - 1) / first.PageSize;
                bool consistent = true;
                for (int page = 1; page < pages; page++)
                {
                    MarketplaceStateView state = application.Invoke<MarketplaceStateRequest, MarketplaceStateView>(
                        ApplicationMemberIds.MarketplaceState,
                        new MarketplaceStateRequest(page, first.PageSize),
                        cancellation_token);
                    if (state.Generation != first.Generation || state.Revision != first.Revision ||
                        state.SearchResult is not { } current ||
                        current.Generation != first.Generation || current.Revision != first.Revision)
                    {
                        consistent = false;
                        break;
                    }
                    if (MatchingOffer(current, row) is { } match)
                        return match;
                }
                if (consistent)
                    break;
            }
        }
        return null;
    }

    private static MarketplaceOfferSnapshot? MatchingOffer(
        MarketplaceOfferPage result,
        FurniDefinitionRow row) =>
        result.Offers
            .Where(offer => offer.Kind == row.Kind && offer.OfferStatus == MarketplaceOfferStatus.Open)
            .Where(offer => row.Kinds == ItemType.Wall
                ? offer.OfferType is MarketplaceOfferType.Wall
                : offer.OfferType is MarketplaceOfferType.Floor or
                    MarketplaceOfferType.LimitedEdition or
                    MarketplaceOfferType.UsableFloor)
            .OrderBy(offer => offer.Price)
            .FirstOrDefault();

    private static void SetFreshMarket(FurniDefinitionRow row, MarketplaceOfferSnapshot? offer)
    {
        int? average = offer is { AveragePrice: > 0 }
            ? offer.AveragePrice
            : row.Market?.AveragePrice;
        row.Market = new MarketplacePrice(
            row.Kinds,
            row.Identifier,
            offer?.Price,
            average,
            offer?.Offers ?? 0,
            offer?.TradeVolume ?? 0);
    }

    private async Task<FurniShopOffer?> FreshShopOfferAsync(
        IApplicationRuntime application,
        FurniDefinitionRow row,
        FurniShopOffer shown,
        CancellationToken cancellation_token)
    {
        CatalogPageView page = await application
            .InvokeAsync<CatalogPageGetRequest, CatalogPageView>(
                ApplicationMemberIds.CatalogPageGet,
                new CatalogPageGetRequest(
                    shown.PageId,
                    shown.OfferId,
                    MaxAgeMilliseconds: 0,
                    TimeoutMilliseconds: 2500,
                    ExpectedSessionGeneration: _shop_session_generation,
                    ExpectedCatalogGeneration: _shop_catalog_generation),
                cancellation_token)
            .ConfigureAwait(true);
        CatalogOfferView? offer = page.Offers.FirstOrDefault(candidate => candidate.OfferId == shown.OfferId);
        CatalogProductView? product = offer?.Products.FirstOrDefault(candidate =>
            candidate.FurniClassId == row.Kind && ProductItemType(candidate) == row.Kinds);
        return offer is null || product is null
            ? null
            : new FurniShopOffer(
                page.PageId,
                offer.OfferId,
                shown.Page,
                product.ProductCount,
                offer.PriceInCredits,
                offer.PriceInActivityPoints,
                offer.ActivityPointType,
                offer.PriceInSilver);
    }

    private async Task BuyMarketplaceAsync(
        IApplicationRuntime application,
        FurniDefinitionRow row,
        MarketplaceOfferSnapshot offer,
        CancellationToken cancellation_token)
    {
        if (!ConfirmPurchase(row.Name, "Marketplace", $"{offer.Price}c"))
        {
            FurniStatus.Text = "Purchase cancelled.";
            return;
        }

        MarketplaceBuyResult result = await application
            .InvokeAsync<MarketplaceBuyRequest, MarketplaceBuyResult>(
                ApplicationMemberIds.MarketplaceOfferBuy,
                new MarketplaceBuyRequest(offer.OfferId),
                cancellation_token)
            .ConfigureAwait(true);
        FurniStatus.Text = result.ResultCode switch
        {
            MarketplaceBuyResultCode.Success => $"Bought {row.Name} from the marketplace for {offer.Price}c.",
            MarketplaceBuyResultCode.OfferUnavailable => "The marketplace offer is no longer available.",
            MarketplaceBuyResultCode.OfferUpdated => $"The offer changed to {result.NewPrice}c. Nothing was bought.",
            MarketplaceBuyResultCode.NotEnoughCredits => "Not enough credits for this marketplace offer.",
            _ => $"Marketplace refused the purchase with result {result.Result}."
        };
    }

    private async Task BuyShopAsync(
        GameState game,
        IApplicationRuntime application,
        FurniDefinitionRow row,
        FurniShopOffer offer,
        CancellationToken cancellation_token)
    {
        if (!ConfirmPurchase(row.Name, "Shop", offer.PriceText))
        {
            FurniStatus.Text = "Purchase cancelled.";
            return;
        }

        cancellation_token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(Game, game) || !ReferenceEquals(Application, application) ||
            !ReferenceEquals(FurniList.SelectedItem, row))
        {
            return;
        }

        CatalogPurchaseDispatchReceipt receipt = await application
            .InvokeAsync<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
                ApplicationMemberIds.CatalogPurchaseSend,
                new CatalogPurchaseSendRequest(
                    offer.PageId,
                    offer.OfferId,
                    ExpectedSessionGeneration: _shop_session_generation,
                    ExpectedCatalogGeneration: _shop_catalog_generation),
                cancellation_token)
            .ConfigureAwait(true);
        cancellation_token.ThrowIfCancellationRequested();
        if (receipt.MessagesDispatched != 1 || receipt.PageId != offer.PageId ||
            receipt.OfferId != offer.OfferId || receipt.Quantity != 1)
        {
            throw new InvalidOperationException("The catalog purchase dispatch receipt is invalid.");
        }
        if (!ReferenceEquals(Game, game) || !ReferenceEquals(Application, application) ||
            !ReferenceEquals(FurniList.SelectedItem, row) ||
            receipt.SessionGeneration != _shop_session_generation ||
            receipt.CatalogGeneration != _shop_catalog_generation)
        {
            return;
        }
        FurniStatus.Text = $"Shop purchase sent for {row.Name} at {offer.PriceText}.";
    }

    private bool ConfirmPurchase(string name, string source, string price)
    {
        return Window.GetWindow(this) is { } owner && ConfirmDialog.Ask(
            owner,
            "Confirm purchase",
            $"Buy “{name}” from the {source} for {price}?",
            "Buy");
    }

    internal static void UpdateFurniRows(DataGrid list, IReadOnlyList<FurniDefinitionRow> rows)
    {
        FurniDefinitionRow? selected = list.SelectedItem as FurniDefinitionRow;
        FurniDefinitionRow[] current = [.. list.Items.OfType<FurniDefinitionRow>()];
        if (!current.SequenceEqual(rows))
            list.ItemsSource = rows;
        if (selected is not null && rows.Contains(selected))
            list.SelectedItem = selected;
    }

    internal static void PickFurniRow(DataGrid list, DependencyObject? source)
    {
        if (source is not null &&
            ItemsControl.ContainerFromElement(list, source) is DataGridRow row &&
            !ReferenceEquals(list.SelectedItem, row.Item))
        {
            list.SelectedItem = row.Item;
        }
    }

    private void FurniPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            PickFurniRow(FurniList, e.OriginalSource as DependencyObject);
    }

    private void ToggleFurniFilters(object sender, RoutedEventArgs e) =>
        FurniFiltersPopup.IsOpen = !FurniFiltersPopup.IsOpen;

    private void ClearFurniFilters(object sender, RoutedEventArgs e)
    {
        FurniTypeFilter.SelectedIndex = 0;
        FurniAvailabilityFilter.SelectedIndex = 0;
        FurniLineFilter.Clear();
        FurniCategoryFilter.Clear();
        FurniMinPrice.Clear();
        FurniMaxPrice.Clear();
        ApplyFurni(return_to_top: true);
    }

    private void FurniOptionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
            ApplyFurni(return_to_top: true);
    }

    private void FurniOptionTextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized)
            ApplyFurni(return_to_top: true);
    }

    private void FurniColumnsChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
            return;
        KindColumn.Visibility = ShowKindColumn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PlaceColumn.Visibility = ShowPlaceColumn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FurniSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        BuyButton.IsEnabled = !_buying && FurniList.SelectedItem is FurniDefinitionRow;

    private void FurniScrolled(object sender, ScrollChangedEventArgs e) => AskForPricesLater();

    private void ApplyTexts()
    {
        List<KeyValueRow> rows = Filter(_texts, TextsFilter.Text);
        TextsList.ItemsSource = rows;
        TextsStatus.Text = Count(rows.Count, _texts.Count, "text", "texts");
    }

    private void ApplyVariables()
    {
        List<KeyValueRow> rows = Filter(_variables, VariablesFilter.Text);
        VariablesList.ItemsSource = rows;
        VariablesStatus.Text = Count(rows.Count, _variables.Count, "variable", "variables");
    }

    private void ApplyProducts()
    {
        List<KeyValueRow> rows = Filter(_products, ProductsFilter.Text);
        ProductsList.ItemsSource = rows;
        ProductsStatus.Text = Count(rows.Count, _products.Count, "product", "products");
    }

    private static List<KeyValueRow> Filter(IReadOnlyList<KeyValueRow> source, string text)
    {
        string term = text.Trim();
        return term.Length == 0
            ? [.. source]
            : [.. source.Where(row =>
                row.Key.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                row.Value.Contains(term, StringComparison.CurrentCultureIgnoreCase))];
    }

    private static string Count(int shown, int total, string one, string many) =>
        shown == total
            ? $"{shown:N0} {(shown == 1 ? one : many)}"
            : $"{shown:N0} of {total:N0} {many}";

    private void FurniFilterChanged(object sender, TextChangedEventArgs e) =>
        ApplyFurni(return_to_top: true);
    private void TextsFilterChanged(object sender, TextChangedEventArgs e) => ApplyTexts();
    private void VariablesFilterChanged(object sender, TextChangedEventArgs e) => ApplyVariables();
    private void ProductsFilterChanged(object sender, TextChangedEventArgs e) => ApplyProducts();
}
