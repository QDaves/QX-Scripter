using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Selling on the marketplace and the account-level marketplace operations, on top of the search
/// and purchase helpers.
/// <para>
/// <b>Request shape.</b> These await the hotel's answer and report it in the returned result rather
/// than throwing on a refusal: a rejected listing, a sold-out offer or an ineligible account all
/// come back as a result code. They throw only when the request itself could not be made, such as
/// on a timeout, a dropped connection, or a client build that cannot express the message.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Lists inventory items for sale at one price each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hotel prices per item, so listing several ids creates several offers at the same price.
    /// Duplicate ids are sent once. Check <see cref="CanSellOnMarketplace"/> first: the hotel
    /// refuses once the account has reached its open offer limit. The result carries the hotel's
    /// verdict rather than throwing, so a refusal is a value to inspect and not an exception.
    /// </para>
    /// <para>
    /// The request is sent once without a retry, and the reply is not blocked from the game client.
    /// The legacy Flash marketplace layout accepts exactly one item per request.
    /// </para>
    /// </remarks>
    /// <param name="price">The price per item in credits, before the hotel's commission.</param>
    /// <param name="category">The furni category, <see cref="MarketplaceFurniCategory.Floor"/> or <see cref="MarketplaceFurniCategory.Wall"/>.</param>
    /// <param name="itemIds">The inventory item identifiers to list, from 1 to 1000 distinct positive ids.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's verdict on the listing.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="price"/> is zero or negative, <paramref name="category"/> is not floor or
    /// wall, the item ids are empty, too many or not positive, or <paramref name="timeoutMs"/> is out of range.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when the legacy Flash layout is active and more than one item is listed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceMakeOfferResult> SellOnMarketplace(
        int price,
        MarketplaceFurniCategory category,
        IReadOnlyList<Id> itemIds,
        int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceMakeOfferRequest, MarketplaceMakeOfferResult>(
            ApplicationMemberIds.MarketplaceOfferMake,
            new MarketplaceMakeOfferRequest(
                price,
                (MarketplaceSellCategory)category,
                itemIds,
                timeoutMs),
            Ct).AsTask();

    /// <summary>Lists one inventory item for sale.</summary>
    /// <remarks>
    /// It follows the same rules as <see cref="SellOnMarketplace(int, MarketplaceFurniCategory, IReadOnlyList{Id}, int)"/>.
    /// </remarks>
    /// <param name="price">The price in credits, before the hotel's commission.</param>
    /// <param name="category">The furni category, <see cref="MarketplaceFurniCategory.Floor"/> or <see cref="MarketplaceFurniCategory.Wall"/>.</param>
    /// <param name="itemId">The inventory item identifier.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's verdict on the listing.</returns>
    public Task<MarketplaceMakeOfferResult> SellOnMarketplace(
        int price,
        MarketplaceFurniCategory category,
        Id itemId,
        int timeoutMs = 10000) =>
        SellOnMarketplace(price, category, [itemId], timeoutMs);

    /// <summary>
    /// Lists an inventory item for sale, taking its category from the item itself.
    /// </summary>
    /// <remarks>
    /// Wall items are listed as wall furni and every other item as floor furni. It follows the
    /// same rules as <see cref="SellOnMarketplace(int, MarketplaceFurniCategory, IReadOnlyList{Id}, int)"/>.
    /// </remarks>
    /// <param name="price">The price in credits, before the hotel's commission.</param>
    /// <param name="item">The inventory item to list.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's verdict on the listing.</returns>
    public Task<MarketplaceMakeOfferResult> SellOnMarketplace(
        int price,
        InventoryItem item,
        int timeoutMs = 10000) =>
        SellOnMarketplace(
            price,
            item.Type is ItemType.Wall
                ? MarketplaceFurniCategory.Wall
                : MarketplaceFurniCategory.Floor,
            item.ItemId,
            timeoutMs);

    /// <summary>
    /// Buys a listed offer and waits for the outcome.
    /// </summary>
    /// <remarks>
    /// Prefer this over the fire-and-forget <see cref="BuyMarketplaceOffer"/> when the outcome
    /// matters: the result distinguishes a completed purchase from one that failed because the
    /// offer was already sold or the price had moved. The request is sent once without a retry.
    /// </remarks>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The purchase result for the offer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offerId"/> is not positive, or <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceBuyResult> BuyMarketplaceOfferAsync(Id offerId, int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceBuyRequest, MarketplaceBuyResult>(
            ApplicationMemberIds.MarketplaceOfferBuy,
            new MarketplaceBuyRequest(offerId, TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();

    /// <summary>Withdraws one of the local user's own offers and waits for the outcome.</summary>
    /// <remarks>
    /// The request is sent once without a retry.
    /// </remarks>
    /// <param name="offerId">The offer to withdraw.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The cancellation result for the offer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offerId"/> is not positive, or <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceCancelOfferResult> CancelMarketplaceOfferAsync(
        Id offerId,
        int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceCancelRequest, MarketplaceCancelOfferResult>(
            ApplicationMemberIds.MarketplaceOfferCancel,
            new MarketplaceCancelRequest(offerId, timeoutMs),
            Ct).AsTask();

    /// <summary>Withdraws every open offer the local user has listed.</summary>
    /// <remarks>
    /// The request is sent once without a retry.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's answer to the cancellation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceCancelAllOffersSnapshot> CancelAllMarketplaceOffers(int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceCancelAllRequest, MarketplaceCancelAllOffersSnapshot>(
            ApplicationMemberIds.MarketplaceOffersCancelAll,
            new MarketplaceCancelAllRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Clears the local user's sold or expired offer history.
    /// </summary>
    /// <remarks>
    /// It needs the modern Flash marketplace layout. Only
    /// <see cref="MarketplaceOwnOffersCategory.Sold"/> and
    /// <see cref="MarketplaceOwnOffersCategory.Expired"/> can be cleared; open offers have to be
    /// withdrawn instead. The request is sent once without a retry.
    /// </remarks>
    /// <param name="category">The history to clear.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's answer to the clear request.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="category"/> is not sold or expired, or <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="NotSupportedException">Thrown when the session does not use the modern Flash marketplace layout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceClearOwnHistoryResult> ClearMarketplaceHistory(
        MarketplaceOwnOffersCategory category,
        int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceHistoryClearRequest, MarketplaceClearOwnHistoryResult>(
            ApplicationMemberIds.MarketplaceHistoryClear,
            new MarketplaceHistoryClearRequest(
                (MarketplaceHistoryCategory)category,
                timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Asks the hotel whether the account may list another offer right now.
    /// </summary>
    /// <remarks>
    /// The answer carries a result code and the number of tokens left, which is what makes it worth
    /// checking before a bulk listing run. The request is sent at most twice within the timeout.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The hotel's eligibility answer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceCanMakeOfferResult> CanSellOnMarketplace(int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceRefreshRequest, MarketplaceCanMakeOfferResult>(
            ApplicationMemberIds.MarketplaceEligibilityRefresh,
            new MarketplaceRefreshRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Requests the hotel's marketplace settings, such as commission, price bounds and offer lifetime.
    /// </summary>
    /// <remarks>
    /// The request is sent at most twice within the timeout.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000.</param>
    /// <returns>The marketplace configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or it changed during the request.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the hotel did not answer in time.</exception>
    public Task<MarketplaceConfiguration> GetMarketplaceConfiguration(int timeoutMs = 10000) =>
        _application.InvokeAsync<MarketplaceRefreshRequest, MarketplaceConfiguration>(
            ApplicationMemberIds.MarketplaceConfigurationRefresh,
            new MarketplaceRefreshRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>Sends the hotel's marketplace token purchase request.</summary>
    /// <remarks>
    /// It sends the request and returns without waiting for an answer.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session.</exception>
    public void BuyMarketplaceTokens() =>
        _application.Invoke<MarketplaceCommandRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceTokensBuy,
            new MarketplaceCommandRequest(),
            Ct);
}
