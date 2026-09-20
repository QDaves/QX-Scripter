using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Diagnostics;
using Qx.Game;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.GameCatalog;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Threading;

namespace Qx.Presentation.ViewModels.GameCatalog;

public sealed partial class GameDataViewModel : PageViewModel
{
    readonly IGameGateway _gateway;
    readonly ShopCatalog _shop;
    readonly CoalescingSignal _data_changed;
    readonly SerialOperation _refreshing = new();
    string _status = "";

    public GameDataViewModel(
        IGameGateway gateway,
        IDialogService dialogs,
        IClipboardService clipboard,
        IMarketplacePrices prices,
        ShopCatalog shop,
        MarketplaceTrade trade,
        IUiDispatcher dispatcher,
        TimeProvider time,
        AppLifetime lifetime)
        : base(PageKey.GameData)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _shop = shop ?? throw new ArgumentNullException(nameof(shop));
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(lifetime);
        Notices = Own(new NoticeLine(dispatcher, time));
        Furni = Own(new FurniTabViewModel(gateway, dialogs, clipboard, prices, shop, trade, Notices, dispatcher, time, lifetime.Token));
        Texts = Own(Entries(new KeyValueLabels("text", "texts", "Search the external texts", "External texts", "Key", "Value"), clipboard, dispatcher, time, lifetime.Token));
        Variables = Own(Entries(new KeyValueLabels("variable", "variables", "Search the external variables", "External variables", "Key", "Value"), clipboard, dispatcher, time, lifetime.Token));
        Products = Own(Entries(new KeyValueLabels("product", "products", "Search the product data", "Product data", "Code", "Name"), clipboard, dispatcher, time, lifetime.Token));
        _data_changed = new CoalescingSignal(dispatcher, OnDataChanged);
        GameData data = gateway.Game.GameData;
        data.Loaded += OnDataLoaded;
        data.Status += OnDataStatus;
        gateway.SessionChanged += OnSessionChanged;
        Own(() =>
        {
            data.Loaded -= OnDataLoaded;
            data.Status -= OnDataStatus;
            gateway.SessionChanged -= OnSessionChanged;
        });
        Watch(Furni);
        Watch(Texts);
        Watch(Variables);
        Watch(Products);
        shop.Watch();
        Own(shop.Stop);
    }

    public NoticeLine Notices { get; }

    public FurniTabViewModel Furni { get; }

    public KeyValueTabViewModel Texts { get; }

    public KeyValueTabViewModel Variables { get; }

    public KeyValueTabViewModel Products { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText), nameof(SelectedTabIndex))]
    public partial GameDataTab SelectedTab { get; set; }

    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set => SelectedTab = Enum.IsDefined((GameDataTab)value) ? (GameDataTab)value : GameDataTab.Furni;
    }

    public string CountText => SelectedTab switch
    {
        GameDataTab.Texts => Texts.CountText,
        GameDataTab.Variables => Variables.CountText,
        GameDataTab.Products => Products.CountText,
        _ => Furni.CountText
    };

    public OperationProgress? Progress => Furni.Progress;

    public override bool TryClearSearch()
    {
        bool cleared = Furni.TryClearSearch();
        cleared |= Texts.TryClearSearch();
        cleared |= Variables.TryClearSearch();
        cleared |= Products.TryClearSearch();
        return cleared;
    }

    protected override async Task OnActivatedAsync(CancellationToken cancellation_token)
    {
        Furni.RefreshGates();
        await RefreshAsync(cancellation_token);
    }

    KeyValueTabViewModel Entries(KeyValueLabels labels, IClipboardService clipboard, IUiDispatcher dispatcher, TimeProvider time, CancellationToken lifetime) =>
        new(labels, clipboard, Notices, dispatcher, time, lifetime);

    void Watch(ObservableObject tab)
    {
        tab.PropertyChanged += OnTabChanged;
        Own(() => tab.PropertyChanged -= OnTabChanged);
    }

    void OnTabChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FurniTabViewModel.CountText))
            OnPropertyChanged(nameof(CountText));
        else if (e.PropertyName == nameof(FurniTabViewModel.Progress))
            OnPropertyChanged(nameof(Progress));
    }

    void OnDataLoaded() => _data_changed.Raise();

    void OnDataStatus(string message)
    {
        Volatile.Write(ref _status, message);
        _data_changed.Raise();
    }

    void OnSessionChanged()
    {
        Furni.RefreshGates();
        _shop.Invalidate(null);
        _data_changed.Raise();
    }

    void OnDataChanged()
    {
        if (!IsActive)
            return;
        RefreshAsync(ActivationToken).Observe("ui");
    }

    async Task RefreshAsync(CancellationToken cancellation_token)
    {
        OperationLease lease = await _refreshing.StartAsync(cancellation_token);
        try
        {
            GameData data = _gateway.Game.GameData;
            if (!data.IsLoaded)
            {
                string reason = Failure();
                Furni.ShowMissing(reason);
                Texts.ShowMissing(reason);
                Variables.ShowMissing(reason);
                Products.ShowMissing(reason);
                Subtitle = "";
                return;
            }
            FurniData? furni = data.Furni;
            ExternalTexts? texts = data.Texts;
            ExternalVariables? variables = data.Variables;
            ProductData? products = data.Products;
            IReadOnlyList<FurniSnapshot> furni_rows = await Task.Run(
                () => GameDataProjection.Furni(furni, texts),
                lease.Token);
            IReadOnlyList<KeyValueEntry> text_rows = await Task.Run(() => GameDataProjection.Texts(texts), lease.Token);
            IReadOnlyList<KeyValueEntry> variable_rows = await Task.Run(() => GameDataProjection.Variables(variables), lease.Token);
            IReadOnlyList<KeyValueEntry> product_rows = await Task.Run(() => GameDataProjection.Products(products), lease.Token);
            if (!_refreshing.IsCurrent(lease))
                return;
            Furni.Load(furni_rows);
            Texts.Load(text_rows);
            Variables.Load(variable_rows);
            Products.Load(product_rows);
            Subtitle = $"{furni_rows.Count:N0} furni · {text_rows.Count:N0} texts · {variable_rows.Count:N0} variables · {product_rows.Count:N0} products";
            await MergeShopAsync(lease.Token);
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
        }
        finally
        {
            _refreshing.Complete(lease);
        }
    }

    async Task MergeShopAsync(CancellationToken cancellation_token)
    {
        try
        {
            IReadOnlyDictionary<FurniKey, IReadOnlyList<FurniShopOffer>>? offers = await _shop.MergeAsync(null, null, cancellation_token);
            if (offers is not null)
                Furni.SetShopOffers(offers);
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
        }
        catch (GameUnavailableException)
        {
            Furni.SetShopOffers(null);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Diag.Warn($"Shop prices could not be read: {error.Message}", "ui");
        }
    }

    string Failure() => GameDataProjection.Failure(Volatile.Read(ref _status));
}
