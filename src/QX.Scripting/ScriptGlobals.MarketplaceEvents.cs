using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Marketplace event subscriptions.
/// <para>
/// Every <c>On*</c> method registers a handler and returns the handle that removes it again. The
/// subscription is also tracked by the script and torn down when the script stops, so the handle
/// only has to be kept when the script wants to unsubscribe earlier. Disposing it more than once
/// is harmless.
/// </para>
/// <para>
/// Handlers run inline on the interception thread while the triggering packet is dispatched, not
/// on the script thread, and after the cached marketplace state has already been updated. Keep
/// them short and do not block inside them.
/// </para>
/// <para>
/// These events fire for every matching packet on the connection, including marketplace traffic
/// the game client itself caused, not only replies to requests the script issued.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs whenever the cached marketplace state changes.
    /// </summary>
    /// <remarks>
    /// Every kind of change triggers it, including a reset. The handler receives a fresh
    /// <see cref="Marketplace"/> read, so it sees the first page of up to 100 cached entries.
    /// </remarks>
    /// <param name="handler">The handler to call with the current marketplace state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceStateChanged(Action<MarketplaceStateView> handler) =>
        Track(_application.Subscribe<MarketplaceChanged>(
            ApplicationMemberIds.MarketplaceChanged,
            Guarded<MarketplaceChanged>(_ => handler(Marketplace))));

    /// <summary>
    /// Registers a handler that runs when the server sends the marketplace configuration.
    /// </summary>
    /// <remarks>
    /// The configuration covers whether the marketplace is enabled, the commission and selling
    /// fee, token batch pricing, the allowed price range, the offer lifetime in hours and the
    /// averaging period.
    /// </remarks>
    /// <param name="handler">The handler to call with the configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceConfigurationChanged(
        Action<MarketplaceConfiguration> handler) =>
        Track(_application.Subscribe<MarketplaceConfigurationChanged>(
            ApplicationMemberIds.MarketplaceConfigurationChanged,
            Guarded<MarketplaceConfigurationChanged>(change => handler(change.Configuration))));

    /// <summary>
    /// Registers a handler that runs when the server answers whether the local user may currently
    /// post marketplace offers.
    /// </summary>
    /// <remarks>
    /// The answer carries the result code and the remaining token count.
    /// </remarks>
    /// <param name="handler">The handler to call with the eligibility answer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceEligibilityChanged(
        Action<MarketplaceCanMakeOfferResult> handler) =>
        Track(_application.Subscribe<MarketplaceEligibilityChanged>(
            ApplicationMemberIds.MarketplaceEligibilityChanged,
            Guarded<MarketplaceEligibilityChanged>(change => handler(change.Eligibility))));

    /// <summary>
    /// Registers a handler that runs when a marketplace search returns its offers.
    /// </summary>
    /// <remarks>
    /// Offers sharing an id are collapsed before the handler sees them. The page holds the first
    /// 100 offers of the result; the rest can be read with
    /// <see cref="GetMarketplaceStatePage(int, int)"/>.
    /// </remarks>
    /// <param name="handler">The handler to call with the offer page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceSearchResults(Action<MarketplaceOfferPage> handler) =>
        Track(_application.Subscribe<MarketplaceSearchReceived>(
            ApplicationMemberIds.MarketplaceSearchReceived,
            Guarded<MarketplaceSearchReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the local user's own marketplace offers arrive.
    /// </summary>
    /// <remarks>
    /// The page carries the credits waiting to be redeemed and the first 100 own offers.
    /// </remarks>
    /// <param name="handler">The handler to call with the own offer page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnOwnMarketplaceOffers(Action<MarketplaceOwnOfferPage> handler) =>
        Track(_application.Subscribe<MarketplaceOwnOffersReceived>(
            ApplicationMemberIds.MarketplaceOwnOffersReceived,
            Guarded<MarketplaceOwnOffersReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when price statistics for one furni kind arrive.
    /// </summary>
    /// <remarks>
    /// The statistics hold the average sale price, the current offer count and the daily sale
    /// history. The message carries its own furni category and type id.
    /// </remarks>
    /// <param name="handler">The handler to call with the statistics.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceItemStats(Action<MarketplaceItemStatsSnapshot> handler) =>
        Track(_application.Subscribe<MarketplaceItemStatsReceived>(
            ApplicationMemberIds.MarketplaceItemStatsReceived,
            Guarded<MarketplaceItemStatsReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the server resolves an attempt to post an offer.
    /// </summary>
    /// <remarks>
    /// The message carries only a result code; it does not identify which offer it answers.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceOfferResult(
        Action<MarketplaceMakeOfferResult> handler) =>
        Track(_application.Subscribe<MarketplaceMakeOfferResultReceived>(
            ApplicationMemberIds.MarketplaceOfferMakeResult,
            Guarded<MarketplaceMakeOfferResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the server resolves an attempt to buy an offer.
    /// </summary>
    /// <remarks>
    /// The result carries the result code, the offer id that was requested and, when the offer
    /// was listed again at a different price, the replacement offer id and price.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplacePurchaseResult(
        Action<MarketplaceBuyResult> handler) =>
        Track(_application.Subscribe<MarketplaceBuyResultReceived>(
            ApplicationMemberIds.MarketplaceOfferBuyResult,
            Guarded<MarketplaceBuyResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the server resolves an attempt to cancel one offer.
    /// </summary>
    /// <remarks>
    /// The result carries the offer id and whether the cancellation succeeded.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceOfferCancelResult(
    Action<MarketplaceCancelOfferResult> handler) =>
    Track(_application.Subscribe<MarketplaceCancelResultReceived>(
        ApplicationMemberIds.MarketplaceOfferCancelResult,
        Guarded<MarketplaceCancelResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the server resolves an attempt to cancel every open
    /// offer at once.
    /// </summary>
    /// <remarks>
    /// The result carries the ids that were canceled and whether the request succeeded. The message
    /// only exists in the modern marketplace layout; a legacy Flash build cannot produce it.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceAllOffersCancelResult(
        Action<MarketplaceCancelAllOffersSnapshot> handler) =>
        Track(_application.Subscribe<MarketplaceCancelAllResultReceived>(
            ApplicationMemberIds.MarketplaceOffersCancelAllResult,
            Guarded<MarketplaceCancelAllResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs when the server resolves an attempt to clear the own offer
    /// history.
    /// </summary>
    /// <remarks>
    /// The result carries only whether the history was cleared.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceHistoryClearResult(
    Action<MarketplaceClearOwnHistoryResult> handler) =>
    Track(_application.Subscribe<MarketplaceHistoryClearResultReceived>(
        ApplicationMemberIds.MarketplaceHistoryClearResult,
        Guarded<MarketplaceHistoryClearResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Registers a handler that runs after the cached marketplace state was emptied for a new
    /// session.
    /// </summary>
    /// <remarks>
    /// A reset happens on reconnect. Everything the marketplace state exposes is back to its empty
    /// value by the time the handler runs.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceReset(Action handler) =>
        Track(_application.Subscribe<MarketplaceChanged>(
            ApplicationMemberIds.MarketplaceChanged,
            Guarded<MarketplaceChanged>(change =>
            {
                if (change.Kind is MarketplaceChangeKind.Reset)
                    handler();
            })));
}
