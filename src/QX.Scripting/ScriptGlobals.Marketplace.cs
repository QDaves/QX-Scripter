using Qx.Game;
using Qx.Game.Application;
using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Cached marketplace state. Nothing in this section talks to the server: every member reads the
/// snapshot the marketplace tracker builds from marketplace traffic seen on the wire, so a member
/// stays <see langword="null"/> or empty until the matching reply has arrived — either because
/// the game client asked for it, or because a script issued the corresponding request such as
/// <see cref="GetMarketplaceStats(int,int,int)"/> or <see cref="GetMyMarketplaceOffers(int)"/>.
/// <para>
/// The snapshot is emptied when the session resets, so nothing here survives a reconnect.
/// </para>
/// <para>
/// The marketplace has separate verified Flash and Unity wire layouts. Reading cached state is
/// always safe, but the requests that fill it can refuse to run when the active client's layout
/// is not one the parser has verified.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    public MarketplaceStateView MarketplaceState => Marketplace;

    public MarketplaceStateView GetMarketplaceStatePage(
        int page = 0,
        int page_size = 100) =>
        Application.Invoke<MarketplaceStateRequest, MarketplaceStateView>(
            ApplicationMemberIds.MarketplaceState,
            new MarketplaceStateRequest(page, page_size),
            Ct);

    /// <summary>
    /// The marketplace's server-side settings — whether it is enabled, commission and selling
    /// fee, token batch price and size, the allowed price range, offer lifetime in hours and the
    /// averaging period — or <see langword="null"/> until a configuration message has arrived.
    /// </summary>
    public MarketplaceConfiguration? MarketplaceSettings =>
        MarketplaceState.Configuration;

    /// <summary>
    /// The server's last answer to "may the local user post another offer", or
    /// <see langword="null"/> when it was never asked. Carries the result code and, on Flash, the
    /// remaining token count.
    /// </summary>
    public MarketplaceCanMakeOfferResult? MarketplaceEligibility =>
        MarketplaceState.Eligibility;

    public MarketplaceOfferPage? LatestMarketplaceSearch =>
        MarketplaceState.SearchResult;

    public MarketplaceOwnOfferPage? MarketplaceOwnOfferState =>
        MarketplaceState.OwnOffers;

    public IReadOnlyList<MarketplaceItemStatsSnapshot> MarketplaceItemStatistics =>
        MarketplaceState.ItemStats.Items;

    /// <summary>Finds one offer inside the cached search result.</summary>
    /// <param name="offer_id">The marketplace offer id.</param>
    /// <returns>
    /// The offer, or <see langword="null"/> when no search result is cached or it contains no
    /// offer with that id.
    /// </returns>
    public MarketplaceOfferSnapshot? FindMarketplaceOffer(Id offer_id) =>
        FindMarketplaceStateItem(
            state => state.SearchResult?.Offers.FirstOrDefault(
                offer => offer.OfferId == offer_id),
            state => state.SearchResult?.CachedItems ?? 0);

    /// <summary>Finds one of the local user's own offers in the cached own-offer list.</summary>
    /// <param name="offer_id">The marketplace offer id.</param>
    /// <returns>
    /// The offer, or <see langword="null"/> when the own-offer list is not cached or contains no
    /// offer with that id.
    /// </returns>
    public MarketplaceOfferSnapshot? FindOwnMarketplaceOffer(Id offer_id) =>
        FindMarketplaceStateItem(
            state => state.OwnOffers?.Offers.FirstOrDefault(
                offer => offer.OfferId == offer_id),
            state => state.OwnOffers?.TotalItems ?? 0);

    /// <summary>Finds a cached price-history entry for one furni kind.</summary>
    /// <param name="furni_category">
    /// The marketplace category: <c>Floor</c> = 1, <c>Wall</c> = 2, <c>Limited</c> = 3.
    /// </param>
    /// <param name="furni_type_id">
    /// The furni type id — the class id shared by every copy of that furni, not an item id.
    /// </param>
    /// <returns>
    /// The statistics, or <see langword="null"/> when none have been received for this category
    /// and type.
    /// </returns>
    public MarketplaceItemStatsSnapshot? FindMarketplaceItemStats(
        MarketplaceFurniCategory furni_category,
        int furni_type_id) =>
        FindMarketplaceStateItem(
            state => state.ItemStats.Items.FirstOrDefault(stats =>
                stats.FurniCategory == furni_category &&
                stats.FurniTypeId == furni_type_id),
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
