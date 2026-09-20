using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Model;
using Qx.Presentation.Services.GameCatalog;
using Qx.Presentation.Services.Marketplace;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.GameCatalog;

public sealed record FurniSearchKey(
    string Name,
    string Identifier,
    string Extra,
    string Line,
    string Category,
    int Kind,
    ItemType Type,
    int? CheapestCredits,
    bool HasMarketplace,
    bool HasShop);

public sealed partial class FurniRowViewModel : ObservableObject
{
    MarketplacePrice? _market;
    IReadOnlyList<FurniShopOffer> _shop_offers = [];

    public FurniRowViewModel(FurniSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Type = snapshot.Type;
        Kind = snapshot.Kind;
        Name = snapshot.Name;
        Identifier = snapshot.Identifier;
        Line = snapshot.Line;
        Category = snapshot.Category;
        Description = snapshot.Description;
        Image = Picture(snapshot.IconUrl);
        Key = BuildKey();
    }

    public ItemType Type { get; }

    public int Kind { get; }

    public FurniKey Kinds => new(Type, Kind);

    public string Placement => Type == ItemType.Wall ? "wall" : "floor";

    [ObservableProperty]
    public partial string Name { get; private set; }

    [ObservableProperty]
    public partial string Identifier { get; private set; }

    [ObservableProperty]
    public partial string Line { get; private set; }

    [ObservableProperty]
    public partial string Category { get; private set; }

    [ObservableProperty]
    public partial string Description { get; private set; }

    [ObservableProperty]
    public partial ImageRequest? Image { get; private set; }

    public FurniSearchKey Key { get; private set; }

    public MarketplacePrice? Market
    {
        get => _market;
        set
        {
            _market = value;
            Key = BuildKey();
            OnPropertyChanged(nameof(Market));
            OnPropertyChanged(nameof(MarketText));
            OnPropertyChanged(nameof(MarketValue));
            OnPropertyChanged(nameof(HasMarketplace));
            OnPropertyChanged(nameof(CheapestCredits));
        }
    }

    public IReadOnlyList<FurniShopOffer> ShopOffers => _shop_offers;

    public FurniShopOffer? ShopOffer => _shop_offers.Count > 0 ? _shop_offers[0] : null;

    public bool HasShop => _shop_offers.Count > 0;

    public bool HasMarketplace => Market?.CurrentPrice is not null;

    public int? ShopCredits => ShopOffer is { IsCreditOnly: true } offer ? offer.PriceInCredits : null;

    public string ShopText => ShopOffer?.PriceText ?? "—";

    public string ShopDetails => ShopOffer?.Details ?? "";

    public int? MarketValue => Market?.CurrentPrice;

    public string MarketText => Market is null
        ? ""
        : Market.IsCurrent
            ? $"{Market.CurrentPrice}c"
            : Market.IsKnown
                ? $"~{Market.AveragePrice}c"
                : "—";

    public int? CheapestCredits
    {
        get
        {
            int? market = Market?.CurrentPrice;
            int? shop = ShopCredits;
            return market is null ? shop : shop is null ? market : Math.Min(market.Value, shop.Value);
        }
    }

    public void Apply(FurniSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Name = snapshot.Name;
        Identifier = snapshot.Identifier;
        Line = snapshot.Line;
        Category = snapshot.Category;
        Description = snapshot.Description;
        if (Image?.Url != snapshot.IconUrl)
            Image = Picture(snapshot.IconUrl);
        Key = BuildKey();
    }

    public void SetShopOffers(IEnumerable<FurniShopOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        _shop_offers =
        [
            .. offers
                .OrderBy(offer => offer.IsCreditOnly ? 0 : 1)
                .ThenBy(offer => offer.PriceInCredits)
                .ThenBy(offer => offer.PriceInActivityPoints)
                .ThenBy(offer => offer.PriceInSilver)
                .ThenBy(offer => offer.OfferId)
        ];
        Key = BuildKey();
        OnPropertyChanged(nameof(ShopOffers));
        OnPropertyChanged(nameof(ShopOffer));
        OnPropertyChanged(nameof(HasShop));
        OnPropertyChanged(nameof(ShopCredits));
        OnPropertyChanged(nameof(ShopText));
        OnPropertyChanged(nameof(ShopDetails));
        OnPropertyChanged(nameof(CheapestCredits));
    }

    public void ReplaceShopOffer(FurniShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        SetShopOffers(_shop_offers
            .Where(current => current.PageId != offer.PageId || current.OfferId != offer.OfferId)
            .Append(offer));
    }

    static ImageRequest? Picture(string? url) =>
        string.IsNullOrEmpty(url) ? null : new ImageRequest(url, true, IconKind.Furni);

    FurniSearchKey BuildKey() =>
        new(
            Name,
            Identifier,
            $"{Description} {Line} {Category}",
            Line,
            Category,
            Kind,
            Type,
            CheapestCredits,
            HasMarketplace,
            HasShop);
}
