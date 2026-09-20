using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Services.GameCatalog;

public sealed class MarketplaceTrade
{
    public const int SearchPageSize = 250;
    public const int SearchAttempts = 2;

    readonly IGameGateway _gateway;

    public MarketplaceTrade(IGameGateway gateway) =>
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));

    public async Task<MarketplaceOfferSnapshot?> FreshOfferAsync(string name, string identifier, FurniKey key, CancellationToken cancellation_token)
    {
        foreach (string query in Queries(name, identifier))
        {
            for (int attempt = 0; attempt < SearchAttempts; attempt++)
            {
                MarketplaceOfferPage first = await _gateway.InvokeAsync<MarketplaceSearchRequest, MarketplaceOfferPage>(
                    ApplicationMemberIds.MarketplaceSearch,
                    new MarketplaceSearchRequest(
                        SearchQuery: query,
                        SortOrder: MarketplaceSortOrder.LowestPrice,
                        PageSize: SearchPageSize),
                    cancellation_token);
                if (Cheapest(first, key) is { } found)
                    return found;
                if (first.PageSize <= 0)
                    break;
                int pages = (first.CachedItems + first.PageSize - 1) / first.PageSize;
                bool consistent = true;
                for (int page = 1; page < pages; page++)
                {
                    MarketplaceStateView state = await _gateway.QueryAsync<MarketplaceStateRequest, MarketplaceStateView>(
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
                    if (Cheapest(current, key) is { } match)
                        return match;
                }
                if (consistent)
                    break;
            }
        }
        return null;
    }

    public async Task<MarketplaceBuyResult> BuyAsync(Id offer_id, CancellationToken cancellation_token) =>
        await _gateway.InvokeAsync<MarketplaceBuyRequest, MarketplaceBuyResult>(
            ApplicationMemberIds.MarketplaceOfferBuy,
            new MarketplaceBuyRequest(offer_id),
            cancellation_token);

    static IEnumerable<string> Queries(string name, string identifier) =>
        new[] { name, identifier }
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase);

    static MarketplaceOfferSnapshot? Cheapest(MarketplaceOfferPage page, FurniKey key) =>
        page.Offers
            .Where(offer => offer.Kind == key.Kind && offer.OfferStatus == MarketplaceOfferStatus.Open)
            .Where(offer => key.Type == ItemType.Wall
                ? offer.OfferType is MarketplaceOfferType.Wall
                : offer.OfferType is MarketplaceOfferType.Floor or
                    MarketplaceOfferType.LimitedEdition or
                    MarketplaceOfferType.UsableFloor)
            .OrderBy(offer => offer.Price)
            .FirstOrDefault();
}
