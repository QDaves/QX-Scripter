namespace Qx.Presentation.Services.Marketplace;

public interface IMarketplacePrices
{
    MarketplacePrice? Known(MarketplaceKind kind);

    bool WasRead(MarketplaceKind kind);

    Task<IReadOnlyDictionary<MarketplaceKind, MarketplacePrice>> FetchAsync(IEnumerable<MarketplaceKind> kinds, CancellationToken cancellation_token);
}
