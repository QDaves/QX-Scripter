using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Selling on the marketplace and the account-level marketplace operations, on top of the search
/// and purchase helpers.
/// <para>
/// <b>Request shape.</b> These await the hotel's answer and report it in the returned result rather
/// than throwing on a refusal: a rejected listing, a sold-out offer or an ineligible account all
/// come back as a result code. They throw only when the request itself could not be made — a
/// timeout, a dropped connection, or a client that cannot express the message.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Lists inventory items for sale at one price each.
    /// </summary>
    /// <remarks>
    /// The hotel prices per item, so listing several ids creates several offers at the same price.
    /// Check <see cref="CanSellOnMarketplace"/> first: the hotel refuses silently once the account
    /// has reached its open-offer limit. The result carries the hotel's verdict rather than
    /// throwing, so a refusal is a value to inspect and not an exception.
    /// </remarks>
    /// <param name="price">Price per item in credits, before the hotel's commission.</param>
    /// <param name="category">Whether the items are floor or wall furni.</param>
    /// <param name="itemIds">The inventory item identifiers to list.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    /// <exception cref="Qx.Game.RequestTimeoutException">The hotel did not answer in time.</exception>
    public Task<MarketplaceMakeOfferResult> SellOnMarketplace(
        int price,
        MarketplaceFurniCategory category,
        IReadOnlyList<Id> itemIds,
        int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceMakeOfferRequest, MarketplaceMakeOfferResult>(
            ApplicationMemberIds.MarketplaceOfferMake,
            new MarketplaceMakeOfferRequest(
                price,
                (MarketplaceSellCategory)category,
                itemIds,
                timeoutMs),
            Ct).AsTask();

    /// <summary>Lists one inventory item for sale.</summary>
    /// <param name="price">Price in credits, before the hotel's commission.</param>
    /// <param name="category">Whether the item is floor or wall furni.</param>
    /// <param name="itemId">The inventory item identifier.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceMakeOfferResult> SellOnMarketplace(
        int price,
        MarketplaceFurniCategory category,
        Id itemId,
        int timeoutMs = 10000) =>
        SellOnMarketplace(price, category, [itemId], timeoutMs);

    /// <summary>
    /// Lists an inventory item for sale, taking its category from the item itself.
    /// </summary>
    /// <param name="price">Price in credits, before the hotel's commission.</param>
    /// <param name="item">The inventory item to list.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
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
    /// offer was already sold or the price had moved.
    /// </remarks>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceBuyResult> BuyMarketplaceOfferAsync(Id offerId, int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceBuyRequest, MarketplaceBuyResult>(
            ApplicationMemberIds.MarketplaceOfferBuy,
            new MarketplaceBuyRequest(offerId, TimeoutMilliseconds: timeoutMs),
            Ct).AsTask();

    /// <summary>Withdraws one of the local user's own offers and waits for the outcome.</summary>
    /// <param name="offerId">The offer to withdraw.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceCancelOfferResult> CancelMarketplaceOfferAsync(
        Id offerId,
        int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceCancelRequest, MarketplaceCancelOfferResult>(
            ApplicationMemberIds.MarketplaceOfferCancel,
            new MarketplaceCancelRequest(offerId, timeoutMs),
            Ct).AsTask();

    /// <summary>Withdraws every open offer the local user has listed.</summary>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceCancelAllOffersSnapshot> CancelAllMarketplaceOffers(int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceCancelAllRequest, MarketplaceCancelAllOffersSnapshot>(
            ApplicationMemberIds.MarketplaceOffersCancelAll,
            new MarketplaceCancelAllRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Clears the local user's sold or expired offer history.
    /// </summary>
    /// <remarks>
    /// Flash only. Only <see cref="MarketplaceOwnOffersCategory.Sold"/> and
    /// <see cref="MarketplaceOwnOffersCategory.Expired"/> can be cleared; open offers have to be
    /// withdrawn instead.
    /// </remarks>
    /// <param name="category">Which history to clear.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceClearOwnHistoryResult> ClearMarketplaceHistory(
        MarketplaceOwnOffersCategory category,
        int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceHistoryClearRequest, MarketplaceClearOwnHistoryResult>(
            ApplicationMemberIds.MarketplaceHistoryClear,
            new MarketplaceHistoryClearRequest(
                (MarketplaceHistoryCategory)category,
                timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Asks the hotel whether the account may list another offer right now.
    /// </summary>
    /// <remarks>
    /// The answer carries how many offers are already open and the account's limit, which is what
    /// makes it worth checking before a bulk listing run.
    /// </remarks>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceCanMakeOfferResult> CanSellOnMarketplace(int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceRefreshRequest, MarketplaceCanMakeOfferResult>(
            ApplicationMemberIds.MarketplaceEligibilityRefresh,
            new MarketplaceRefreshRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Reads the hotel's marketplace settings: commission, price bounds and offer lifetime.
    /// </summary>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public Task<MarketplaceConfiguration> GetMarketplaceConfiguration(int timeoutMs = 10000) =>
        Application.InvokeAsync<MarketplaceRefreshRequest, MarketplaceConfiguration>(
            ApplicationMemberIds.MarketplaceConfigurationRefresh,
            new MarketplaceRefreshRequest(timeoutMs),
            Ct).AsTask();

    /// <summary>
    /// Collects the credits earned from sold offers. Fire-and-forget; the new balance arrives as a
    /// currency update.
    /// </summary>
    public void CollectMarketplaceEarnings() =>
        Application.Invoke<MarketplaceCommandRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceCreditsRedeem,
            new MarketplaceCommandRequest(),
            Ct);

    /// <summary>Opens the hotel's marketplace token purchase. Fire-and-forget.</summary>
    public void BuyMarketplaceTokens() =>
        Application.Invoke<MarketplaceCommandRequest, MarketplaceDispatchResult>(
            ApplicationMemberIds.MarketplaceTokensBuy,
            new MarketplaceCommandRequest(),
            Ct);
}
