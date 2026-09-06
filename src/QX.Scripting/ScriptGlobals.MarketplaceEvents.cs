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
/// the game client itself caused — not only replies to requests the script issued.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    public IDisposable OnMarketplaceStateChanged(Action<MarketplaceStateView> handler) =>
        Track(Application.Subscribe<MarketplaceChanged>(
            ApplicationMemberIds.MarketplaceChanged,
            Guarded<MarketplaceChanged>(_ => handler(MarketplaceState))));

    /// <summary>
    /// Raised when the server sends the marketplace configuration: whether the marketplace is
    /// enabled, commission and selling fee, token batch pricing, the allowed price range, offer
    /// lifetime in hours and the averaging period.
    /// </summary>
    /// <param name="handler">Receives the configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceConfigurationChanged(
        Action<MarketplaceConfiguration> handler) =>
        Track(Application.Subscribe<MarketplaceConfigurationChanged>(
            ApplicationMemberIds.MarketplaceConfigurationChanged,
            Guarded<MarketplaceConfigurationChanged>(change => handler(change.Configuration))));

    /// <summary>
    /// Raised when the server answers whether the local user may currently post marketplace
    /// offers, carrying the result code and, on Flash, the remaining token count.
    /// </summary>
    /// <param name="handler">Receives the eligibility answer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceEligibilityChanged(
        Action<MarketplaceCanMakeOfferResult> handler) =>
        Track(Application.Subscribe<MarketplaceEligibilityChanged>(
            ApplicationMemberIds.MarketplaceEligibilityChanged,
            Guarded<MarketplaceEligibilityChanged>(change => handler(change.Eligibility))));

    /// <summary>
    /// Raised when a marketplace search returns its offers. Offers sharing an id are collapsed
    /// before the handler sees them.
    /// </summary>
    /// <param name="handler">Receives the offer page.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceSearchResults(Action<MarketplaceOfferPage> handler) =>
        Track(Application.Subscribe<MarketplaceSearchReceived>(
            ApplicationMemberIds.MarketplaceSearchReceived,
            Guarded<MarketplaceSearchReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when the local user's own marketplace offers arrive, together with the credits
    /// waiting to be redeemed.
    /// </summary>
    /// <param name="handler">Receives the own-offer list.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnOwnMarketplaceOffers(Action<MarketplaceOwnOfferPage> handler) =>
        Track(Application.Subscribe<MarketplaceOwnOffersReceived>(
            ApplicationMemberIds.MarketplaceOwnOffersReceived,
            Guarded<MarketplaceOwnOffersReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when price statistics for one furni kind arrive: average sale price, current offer
    /// count and the daily sale history. The message carries its own furni category and type id.
    /// </summary>
    /// <param name="handler">Receives the statistics.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceItemStats(Action<MarketplaceItemStatsSnapshot> handler) =>
        Track(Application.Subscribe<MarketplaceItemStatsReceived>(
            ApplicationMemberIds.MarketplaceItemStatsReceived,
            Guarded<MarketplaceItemStatsReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when the server resolves an attempt to post an offer. The message carries only a
    /// result code; it does not identify which offer it answers.
    /// </summary>
    /// <param name="handler">Receives the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceOfferResult(
        Action<MarketplaceMakeOfferResult> handler) =>
        Track(Application.Subscribe<MarketplaceMakeOfferResultReceived>(
            ApplicationMemberIds.MarketplaceOfferMakeResult,
            Guarded<MarketplaceMakeOfferResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when the server resolves an attempt to buy an offer, carrying the result code, the
    /// offer id that was requested and — when the offer was re-listed at a different price — the
    /// replacement offer id and price.
    /// </summary>
    /// <param name="handler">Receives the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplacePurchaseResult(
        Action<MarketplaceBuyResult> handler) =>
        Track(Application.Subscribe<MarketplaceBuyResultReceived>(
            ApplicationMemberIds.MarketplaceOfferBuyResult,
            Guarded<MarketplaceBuyResultReceived>(result => handler(result.Result))));

    /// <summary>Raised when the server resolves an attempt to cancel a single offer.</summary>
    /// <param name="handler">Receives the cancelled offer id and whether it succeeded.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>
    /// Flash only. The Unity cancel-offer payload has no verified layout, so the handler is
    /// registered for the Flash message alone and never fires on a Unity session.
    /// </remarks>
    public IDisposable OnMarketplaceOfferCancelResult(
        Action<MarketplaceCancelOfferResult> handler) =>
        Track(Application.Subscribe<MarketplaceCancelResultReceived>(
            ApplicationMemberIds.MarketplaceOfferCancelResult,
            Guarded<MarketplaceCancelResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when the server resolves an attempt to cancel every open offer at once, carrying
    /// the ids that were cancelled.
    /// </summary>
    /// <param name="handler">Receives the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>
    /// On Flash this message only exists in the modern marketplace layout; a legacy Flash build
    /// cannot produce it.
    /// </remarks>
    public IDisposable OnMarketplaceAllOffersCancelResult(
        Action<MarketplaceCancelAllOffersSnapshot> handler) =>
        Track(Application.Subscribe<MarketplaceCancelAllResultReceived>(
            ApplicationMemberIds.MarketplaceOffersCancelAllResult,
            Guarded<MarketplaceCancelAllResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised when the server resolves an attempt to clear the local user's own marketplace
    /// history.
    /// </summary>
    /// <param name="handler">Receives whether the clear succeeded.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>
    /// Flash only, and only in the modern Flash marketplace layout. The handler is registered for
    /// the Flash message alone and never fires on a Unity session.
    /// </remarks>
    public IDisposable OnMarketplaceHistoryClearResult(
        Action<MarketplaceClearOwnHistoryResult> handler) =>
        Track(Application.Subscribe<MarketplaceHistoryClearResultReceived>(
            ApplicationMemberIds.MarketplaceHistoryClearResult,
            Guarded<MarketplaceHistoryClearResultReceived>(result => handler(result.Result))));

    /// <summary>
    /// Raised after the cached marketplace state was emptied for a new session, which happens on
    /// reconnect. Everything the marketplace state exposes is back to its empty value by the time
    /// the handler runs.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnMarketplaceReset(Action handler) =>
        Track(Application.Subscribe<MarketplaceChanged>(
            ApplicationMemberIds.MarketplaceChanged,
            Guarded<MarketplaceChanged>(change =>
            {
                if (change.Kind is MarketplaceChangeKind.Reset)
                    handler();
            })));
}
