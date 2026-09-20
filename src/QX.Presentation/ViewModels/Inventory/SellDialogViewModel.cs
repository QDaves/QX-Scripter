using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Inventory;

public enum SellPricingMode
{
    Market,
    Undercut,
    Fixed
}

public sealed record SellOutcome(int OffersMade, int Refused, string? Failure);

public sealed partial class SellDialogViewModel : DialogViewModel<SellOutcome>, IDisposable
{
    public const int MinimumPrice = 2;
    public const int OfferTimeoutMs = 4000;
    public const int Accepted = 1;

    readonly IGameGateway _gateway;
    readonly CancellationTokenSource _life = new();
    bool _split_offers;
    bool _stop;
    bool _selling;
    bool _pricing;
    bool _disposed;

    public SellDialogViewModel(IEnumerable<SellCandidate> rows, IMarketplacePrices prices, IGameGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(prices);
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        Rows =
        [
            .. rows.Select(candidate => new SellRowViewModel(candidate with
            {
                Market = candidate.Market ?? prices.Known(new MarketplaceKind(candidate.Type, candidate.Identifier))
            }))
        ];
        foreach (SellRowViewModel row in Rows)
            row.PropertyChanged += OnRowChanged;
        ApplyPricing();
    }

    public override string Title => "Sell on marketplace";

    public override IconKind Icon => IconKind.Tag;

    public IReadOnlyList<SellRowViewModel> Rows { get; }

    public int OffersMade { get; private set; }

    public int Refused { get; private set; }

    public string? Failure { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMarketMode), nameof(IsUndercutMode), nameof(IsFixedMode))]
    public partial SellPricingMode Mode { get; set; }

    [ObservableProperty]
    public partial string UndercutText { get; set; } = "1";

    [ObservableProperty]
    public partial string FixedText { get; set; } = "";

    [ObservableProperty]
    public partial string UnpricedText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasUnpriced { get; private set; }

    [ObservableProperty]
    public partial string UnpricedNote { get; private set; } = "";

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SellText))]
    [NotifyCanExecuteChangedFor(nameof(SellCommand))]
    public partial int Items { get; private set; }

    public bool IsMarketMode
    {
        get => Mode == SellPricingMode.Market;
        set => Choose(SellPricingMode.Market, value);
    }

    public bool IsUndercutMode
    {
        get => Mode == SellPricingMode.Undercut;
        set => Choose(SellPricingMode.Undercut, value);
    }

    public bool IsFixedMode
    {
        get => Mode == SellPricingMode.Fixed;
        set => Choose(SellPricingMode.Fixed, value);
    }

    public string SellText => Items switch
    {
        0 => "Sell",
        1 => "Sell 1 item",
        _ => $"Sell {Items.ToString("N0", CultureInfo.CurrentCulture)} items"
    };

    protected override SellOutcome DismissResult => new(OffersMade, Refused, Failure);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (SellRowViewModel row in Rows)
            row.PropertyChanged -= OnRowChanged;
        _life.Cancel();
        _life.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanSell))]
    async Task SellAsync()
    {
        _selling = true;
        SellCommand.NotifyCanExecuteChanged();
        try
        {
            await OfferAllAsync(_life.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Failure ??= error.Message;
        }
        Close(new SellOutcome(OffersMade, Refused, Failure));
    }

    bool CanSell() => Items > 0 && !_selling;

    async Task OfferAllAsync(CancellationToken cancellation_token)
    {
        SellRowViewModel[] going = [.. Rows.Where(row => row.Amount > 0 && row.Price >= MinimumPrice)];
        foreach (SellRowViewModel row in going)
        {
            if (Stopped(cancellation_token))
                break;
            MarketplaceSellCategory category = row.Type == ItemType.Wall
                ? MarketplaceSellCategory.Wall
                : MarketplaceSellCategory.Floor;
            IReadOnlyList<Id> items = row.Chosen;
            if (_split_offers && items.Count > 1)
            {
                await SplitAsync(row, category, items, cancellation_token);
                continue;
            }
            if (await OfferAsync(row, category, items, cancellation_token) != OfferOutcome.Reshape)
                continue;
            _split_offers = true;
            await SplitAsync(row, category, items, cancellation_token);
        }
        Summarise();
    }

    async Task SplitAsync(SellRowViewModel row, MarketplaceSellCategory category, IReadOnlyList<Id> items, CancellationToken cancellation_token)
    {
        foreach (Id item in items)
        {
            if (Stopped(cancellation_token))
                return;
            await OfferAsync(row, category, [item], cancellation_token);
        }
    }

    async Task<OfferOutcome> OfferAsync(SellRowViewModel row, MarketplaceSellCategory category, IReadOnlyList<Id> items, CancellationToken cancellation_token)
    {
        try
        {
            MarketplaceMakeOfferResult? result = await _gateway.InvokeAsync<MarketplaceMakeOfferRequest, MarketplaceMakeOfferResult>(
                ApplicationMemberIds.MarketplaceOfferMake,
                new MarketplaceMakeOfferRequest(row.Price, category, items, OfferTimeoutMs),
                cancellation_token);
            if (result is { Result: Accepted })
            {
                OffersMade += items.Count;
                return OfferOutcome.Accepted;
            }
            Refuse(row, items.Count, $"the hotel refused it (code {result?.Result ?? 0}).");
            return OfferOutcome.Refused;
        }
        catch (NotSupportedException error)
        {
            if (items.Count > 1)
                return OfferOutcome.Reshape;
            Refuse(row, items.Count, error.Message);
            _stop = true;
            return OfferOutcome.Refused;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Refuse(row, items.Count, error.Message);
            _stop = error is InvalidOperationException or GameUnavailableException;
            return OfferOutcome.Refused;
        }
    }

    bool Stopped(CancellationToken cancellation_token) =>
        _stop || IsClosed || cancellation_token.IsCancellationRequested;

    void Refuse(SellRowViewModel row, int count, string reason)
    {
        Refused += count;
        Failure ??= $"{row.Name}: {reason}";
    }

    void Summarise()
    {
        if (Refused == 0 || Failure is null)
            return;
        Failure = OffersMade > 0
            ? $"{OffersMade} listed, {Refused} refused. First: {Failure}"
            : $"None listed, {Refused} refused. First: {Failure}";
    }

    void Choose(SellPricingMode mode, bool chosen)
    {
        if (chosen)
            Mode = mode;
    }

    void ApplyPricing()
    {
        _pricing = true;
        int undercut = Read(UndercutText) ?? 0;
        int? flat = Read(FixedText);
        int? unpriced = Read(UnpricedText);
        foreach (SellRowViewModel row in Rows)
        {
            if (Mode == SellPricingMode.Fixed)
            {
                if (flat is { } fixed_price)
                    row.Apply(fixed_price);
                continue;
            }
            if (row.Market?.Suggested is not { } market)
            {
                if (unpriced is { } own)
                    row.Apply(own);
                continue;
            }
            row.Apply(Mode == SellPricingMode.Undercut ? market - undercut : market);
        }
        int without = Rows.Count(row => !row.IsPriced);
        HasUnpriced = without > 0 && Mode != SellPricingMode.Fixed;
        UnpricedNote = without == 1
            ? "One kind has never been offered, so there is no price to work from. Set one here:"
            : $"{without} kinds have never been offered, so there is no price to work from. Set one here:";
        _pricing = false;
        Refresh();
    }

    void Refresh()
    {
        SellRowViewModel[] going = [.. Rows.Where(row => row.Amount > 0 && row.Price >= MinimumPrice)];
        int items = going.Sum(row => row.Amount);
        long credits = going.Sum(row => (long)row.Amount * row.Price);
        int blocked = Rows.Count(row => row.Amount > 0 && row.Price < MinimumPrice);
        Summary = items == 0
            ? "Nothing to sell."
            : blocked == 0
                ? $"{items} item{(items == 1 ? "" : "s")} across {going.Length} offer{(going.Length == 1 ? "" : "s")}, {credits.ToString("N0", CultureInfo.CurrentCulture)} credits if they all sell."
                : $"{items} item{(items == 1 ? "" : "s")}, {credits.ToString("N0", CultureInfo.CurrentCulture)} credits. {blocked} without a price and left out.";
        Items = items;
    }

    void OnRowChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!_pricing && args.PropertyName is nameof(SellRowViewModel.Amount) or nameof(SellRowViewModel.Price))
            Refresh();
    }

    partial void OnModeChanged(SellPricingMode value) => ApplyPricing();

    partial void OnUndercutTextChanged(string value) => ApplyPricing();

    partial void OnFixedTextChanged(string value) => ApplyPricing();

    partial void OnUnpricedTextChanged(string value) => ApplyPricing();

    static int? Read(string? text) =>
        int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) ? value : null;

    enum OfferOutcome
    {
        Accepted,
        Refused,
        Reshape
    }
}
