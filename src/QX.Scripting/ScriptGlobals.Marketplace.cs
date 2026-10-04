using Qx.Game;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets a snapshot of the marketplace state with one page of each cached list.
    /// </summary>
    /// <remarks>
    /// The same page and page size apply to the cached search result, the own offers and the
    /// item statistics; the statistics are ordered by furni category, then by furni type id. The
    /// configuration, the eligibility and the last action results are always included. Nothing
    /// is requested from the server.
    /// </remarks>
    /// <param name="page">The zero-based page index.</param>
    /// <param name="pageSize">The number of entries per page, from 1 to 250.</param>
    /// <returns>The marketplace state view for that page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="page"/> is negative, or <paramref name="pageSize"/> is outside 1 to 250.
    /// </exception>
    public MarketplaceStateView GetMarketplaceStatePage(
        int page = 0,
        int pageSize = 100) =>
        _application.Invoke<MarketplaceStateRequest, MarketplaceStateView>(
            ApplicationMemberIds.MarketplaceState,
            new MarketplaceStateRequest(page, pageSize),
            Ct);

    /// <summary>
    /// Gets the marketplace's server settings, or <see langword="null"/> until a configuration
    /// message has arrived.
    /// </summary>
    /// <remarks>
    /// The settings cover whether the marketplace is enabled, the commission and selling fee, the
    /// token batch price and size, the allowed price range, the offer lifetime in hours, the
    /// averaging period and the revenue limits.
    /// </remarks>
    public MarketplaceConfiguration? MarketplaceSettings =>
        Marketplace.Configuration;

    /// <summary>
    /// Gets the server's last answer on whether the local user may post another offer, or
    /// <see langword="null"/> when it was never asked.
    /// </summary>
    /// <remarks>
    /// It carries the result code and the remaining token count.
    /// </remarks>
    public MarketplaceCanMakeOfferResult? MarketplaceEligibility =>
        Marketplace.Eligibility;

    /// <summary>
    /// Gets the first page of up to 100 offers from the last cached marketplace search, or
    /// <see langword="null"/> when no search result is cached.
    /// </summary>
    public MarketplaceOfferPage? LatestMarketplaceSearch =>
        Marketplace.SearchResult;

    /// <summary>
    /// Gets the first page of up to 100 of the local user's own marketplace offers, or
    /// <see langword="null"/> when the own offer list is not cached.
    /// </summary>
    /// <remarks>
    /// The page also carries the total number of own offers and the credits waiting to be
    /// redeemed.
    /// </remarks>
    public MarketplaceOwnOfferPage? OwnMarketplaceOffers =>
        Marketplace.OwnOffers;

    /// <summary>
    /// Gets the first 100 cached marketplace item statistics, ordered by furni category and then
    /// by furni type id.
    /// </summary>
    /// <remarks>
    /// The list is empty when no statistics have been received. Use
    /// <see cref="FindMarketplaceItemStats"/> to look up one furni kind across all pages.
    /// </remarks>
    public IReadOnlyList<MarketplaceItemStatsSnapshot> MarketplaceItemStatistics =>
        Marketplace.ItemStats.Items;

    /// <summary>Finds one offer inside the cached search result.</summary>
    /// <remarks>
    /// Every page of the cached result is read, and the read starts again, up to three times, when
    /// the marketplace state changes in between. Nothing is requested from the server.
    /// </remarks>
    /// <param name="offerId">The marketplace offer id.</param>
    /// <returns>
    /// The offer, or <see langword="null"/> when no search result is cached or it contains no
    /// offer with that id.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the marketplace state kept changing while it was being read.</exception>
    public MarketplaceOfferSnapshot? FindMarketplaceOffer(Id offerId) =>
        FindMarketplaceStateItem(
            state => state.SearchResult?.Offers.FirstOrDefault(
                offer => offer.OfferId == offerId),
            state => state.SearchResult?.CachedItems ?? 0);

    /// <summary>Finds one of the local user's own offers in the cached own offer list.</summary>
    /// <remarks>
    /// It reads the cache the same way as <see cref="FindMarketplaceOffer"/>.
    /// </remarks>
    /// <param name="offerId">The marketplace offer id.</param>
    /// <returns>
    /// The offer, or <see langword="null"/> when the own offer list is not cached or contains no
    /// offer with that id.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the marketplace state kept changing while it was being read.</exception>
    public MarketplaceOfferSnapshot? FindOwnMarketplaceOffer(Id offerId) =>
        FindMarketplaceStateItem(
            state => state.OwnOffers?.Offers.FirstOrDefault(
                offer => offer.OfferId == offerId),
            state => state.OwnOffers?.TotalItems ?? 0);

    /// <summary>Finds a cached price history entry for one furni kind.</summary>
    /// <remarks>
    /// It reads the cache the same way as <see cref="FindMarketplaceOffer"/>.
    /// </remarks>
    /// <param name="furniCategory">
    /// The marketplace category: <c>Floor</c> = 1, <c>Wall</c> = 2, <c>Limited</c> = 3.
    /// </param>
    /// <param name="furniTypeId">
    /// The furni type id, which is the class id shared by every copy of that furni, not an item id.
    /// </param>
    /// <returns>
    /// The statistics, or <see langword="null"/> when none have been received for this category
    /// and type.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the marketplace state kept changing while it was being read.</exception>
    public MarketplaceItemStatsSnapshot? FindMarketplaceItemStats(
        MarketplaceFurniCategory furniCategory,
        int furniTypeId) =>
        FindMarketplaceStateItem(
            state => state.ItemStats.Items.FirstOrDefault(stats =>
                stats.FurniCategory == furniCategory &&
                stats.FurniTypeId == furniTypeId),
            state => state.ItemStats.TotalItems);

    private T? FindMarketplaceStateItem<T>(
        Func<MarketplaceStateView, T?> find,
        Func<MarketplaceStateView, int> count) where T : class
    {
        const int page_size = 250;
        var expected_session = Session;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            MarketplaceStateView first = GetMarketplaceStatePage(0, page_size);
            int pages = Math.Max(1, (count(first) + page_size - 1) / page_size);
            bool consistent = true;
            for (int page = 0; page < pages; page++)
            {
                MarketplaceStateView current = page == 0
                    ? first
                    : GetMarketplaceStatePage(page, page_size);
                if (current.Generation != first.Generation || current.Revision != first.Revision)
                {
                    consistent = false;
                    break;
                }
                if (find(current) is { } value)
                {
                    if (!ReferenceEquals(Session, expected_session))
                        break;
                    return value;
                }
            }
            if (!consistent || !ReferenceEquals(Session, expected_session))
                continue;
            MarketplaceStateView verification = GetMarketplaceStatePage(0, 1);
            if (verification.Generation == first.Generation &&
                verification.Revision == first.Revision &&
                ReferenceEquals(Session, expected_session))
            {
                return null;
            }
        }
        throw new InvalidOperationException(
            "The marketplace state changed continuously while it was being read.");
    }
}
