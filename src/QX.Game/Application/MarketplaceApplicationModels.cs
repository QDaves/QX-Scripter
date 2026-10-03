using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read one page of the stored marketplace state.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceState"/>. The page applies to the stored search
/// offers, own offers and item statistics. No message is sent to the hotel.
/// </remarks>
/// <param name="Page">The zero-based page index. Must not be negative.</param>
/// <param name="PageSize">The maximum number of records per list in the page, from 1 to 250.</param>
public sealed record MarketplaceStateRequest(
    int Page = 0,
    int PageSize = 100);

/// <summary>
/// Represents a request to load the marketplace configuration or eligibility from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceConfigurationRefresh"/> and
/// <see cref="ApplicationMemberIds.MarketplaceEligibilityRefresh"/>. The request is sent at most twice
/// within the timeout.
/// </remarks>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceRefreshRequest(
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to load the marketplace statistics of one furni kind from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceItemStatsGet"/>. The request is sent at most twice
/// within the timeout and completes with the first statistics received for the same category and furni
/// type id.
/// </remarks>
/// <param name="FurniCategory">
/// The marketplace category of the furni, <see cref="MarketplaceFurniCategory.Floor"/>,
/// <see cref="MarketplaceFurniCategory.Wall"/> or <see cref="MarketplaceFurniCategory.Limited"/>.
/// </param>
/// <param name="FurniTypeId">The furni type id, or the limited edition lookup id. Must be at least 1.</param>
/// <param name="ExtraData">
/// The variant data sent with the request, at most 65535 UTF-8 bytes. Must be empty on the legacy Flash
/// marketplace layout.
/// </param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceItemStatsRequest(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    string ExtraData = "",
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to search the public marketplace offers.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceSearch"/>. The request is sent at most twice within
/// the timeout, and the page is cut from the offers the hotel returned.
/// </remarks>
/// <param name="SearchQuery">The furni name or search text, at most 65535 UTF-8 bytes.</param>
/// <param name="MinimumPrice">The minimum price in credits, or -1 for no lower bound.</param>
/// <param name="MaximumPrice">
/// The maximum price in credits, or -1 for no upper bound. Must not be less than
/// <paramref name="MinimumPrice"/> when both are set.
/// </param>
/// <param name="SortOrder">The order of the results.</param>
/// <param name="CombineUniqueOffers">
/// Whether equivalent offers are grouped. Must be <see langword="true"/> on the legacy Flash marketplace
/// layout, which does not send the flag.
/// </param>
/// <param name="Page">The zero-based page index. Must not be negative.</param>
/// <param name="PageSize">The maximum number of offers in the page, from 1 to 250.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceSearchRequest(
    string SearchQuery = "",
    int MinimumPrice = -1,
    int MaximumPrice = -1,
    MarketplaceSortOrder SortOrder = MarketplaceSortOrder.HighestPrice,
    bool CombineUniqueOffers = true,
    int Page = 0,
    int PageSize = 100,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to load the local user's own marketplace offers from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOwnOffersGet"/>. The request is sent at most twice
/// within the timeout, and the page is cut from the offers the hotel returned.
/// </remarks>
/// <param name="Category">
/// The offers to load. Only <see cref="MarketplaceOwnOffersCategory.Open"/> is supported on the legacy
/// Flash marketplace layout.
/// </param>
/// <param name="Page">The zero-based page index. Must not be negative.</param>
/// <param name="PageSize">The maximum number of offers in the page, from 1 to 250.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceOwnOffersRequest(
    MarketplaceOwnOffersCategory Category = MarketplaceOwnOffersCategory.Open,
    int Page = 0,
    int PageSize = 100,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to list inventory items for sale on the marketplace.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOfferMake"/>. The request is sent once and completes
/// with the next offer result received from the hotel.
/// </remarks>
/// <param name="Price">The price per offer in credits. Must be greater than 0.</param>
/// <param name="FurniCategory">The marketplace category of the items.</param>
/// <param name="ItemIds">
/// The ids of the inventory items to list. Duplicates are removed, and 1 to 1000 positive ids are
/// required. The legacy Flash marketplace layout accepts exactly one item.
/// </param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceMakeOfferRequest(
    int Price,
    MarketplaceSellCategory FurniCategory,
    IReadOnlyList<Id> ItemIds,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to buy a marketplace offer and wait for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOfferBuy"/>. The request is sent once and completes
/// with the first purchase result for the same offer id.
/// </remarks>
/// <param name="OfferId">The id of the offer. Must be positive.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceBuyRequest(
    Id OfferId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to send a marketplace purchase without waiting for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOfferBuySend"/>.
/// </remarks>
/// <param name="OfferId">The id of the offer. Must be positive.</param>
public sealed record MarketplaceBuySendRequest(Id OfferId);

/// <summary>
/// Represents a request to cancel one of the local user's marketplace offers and wait for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOfferCancel"/>. The request is sent once and
/// completes with the first cancellation result for the same offer id. A successful cancellation
/// removes the offer from the stored own offers.
/// </remarks>
/// <param name="OfferId">The id of the offer. Must be positive.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceCancelRequest(
    Id OfferId,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to send a marketplace offer cancellation without waiting for the result.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOfferCancelSend"/>.
/// </remarks>
/// <param name="OfferId">The id of the offer. Must be positive.</param>
public sealed record MarketplaceCancelSendRequest(Id OfferId);

/// <summary>
/// Represents a request to cancel all of the local user's open marketplace offers.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceOffersCancelAll"/>. The request is sent once and
/// completes with the next cancel-all result received. A successful result removes the canceled offers
/// from the stored own offers.
/// </remarks>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceCancelAllRequest(
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to clear the local user's sold or expired marketplace offer history.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceHistoryClear"/>. Requires the modern Flash
/// marketplace layout. The request is sent once and completes with the next history clear result
/// received.
/// </remarks>
/// <param name="Category">The history to clear.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the hotel response in milliseconds, from 1 to 120000.</param>
public sealed record MarketplaceHistoryClearRequest(
    MarketplaceHistoryCategory Category,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a marketplace command that takes no arguments.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.MarketplaceCreditsRedeem"/> and
/// <see cref="ApplicationMemberIds.MarketplaceTokensBuy"/>. The command is sent without waiting for a
/// result.
/// </remarks>
public sealed record MarketplaceCommandRequest;

/// <summary>
/// Represents a page of marketplace search offers.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.MarketplaceSearch"/> and part of
/// <see cref="MarketplaceStateView"/> and <see cref="MarketplaceSearchReceived"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state, which increases with every change.</param>
/// <param name="Page">The zero-based page index.</param>
/// <param name="PageSize">The maximum number of offers in the page.</param>
/// <param name="CachedItems">The number of offers received from the hotel, one per offer id.</param>
/// <param name="TotalItemsFound">The total number of items the hotel reported for the search.</param>
/// <param name="Offers">The offers in the page.</param>
public sealed record MarketplaceOfferPage(
    long Generation,
    long Revision,
    int Page,
    int PageSize,
    int CachedItems,
    int TotalItemsFound,
    IReadOnlyList<MarketplaceOfferSnapshot> Offers);

/// <summary>
/// Represents a page of the local user's own marketplace offers.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.MarketplaceOwnOffersGet"/> and part of
/// <see cref="MarketplaceStateView"/> and <see cref="MarketplaceOwnOffersReceived"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state, which increases with every change.</param>
/// <param name="Page">The zero-based page index.</param>
/// <param name="PageSize">The maximum number of offers in the page.</param>
/// <param name="TotalItems">The number of own offers stored, one per offer id.</param>
/// <param name="CreditsWaiting">The credits from sold offers that are waiting to be redeemed.</param>
/// <param name="Category">
/// The category that was requested, or <see langword="null"/> when the page was read from the stored state
/// or published with an event.
/// </param>
/// <param name="Offers">The offers in the page.</param>
public sealed record MarketplaceOwnOfferPage(
    long Generation,
    long Revision,
    int Page,
    int PageSize,
    int TotalItems,
    int CreditsWaiting,
    MarketplaceOwnOffersCategory? Category,
    IReadOnlyList<MarketplaceOfferSnapshot> Offers);

/// <summary>
/// Represents a page of the stored marketplace item statistics.
/// </summary>
/// <remarks>
/// The statistics are ordered by furni category, then by furni type id.
/// </remarks>
/// <param name="Page">The zero-based page index.</param>
/// <param name="PageSize">The maximum number of statistics in the page.</param>
/// <param name="TotalItems">The number of item statistics stored, one per furni category and type id.</param>
/// <param name="Items">The statistics in the page.</param>
public sealed record MarketplaceItemStatsPage(
    int Page,
    int PageSize,
    int TotalItems,
    IReadOnlyList<MarketplaceItemStatsSnapshot> Items);

/// <summary>
/// Represents one page of the stored marketplace state.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.MarketplaceState"/>. The state is cleared when the hotel
/// connection closes.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state, which increases with every change.</param>
/// <param name="Configuration">The marketplace configuration, or <see langword="null"/> when none was received.</param>
/// <param name="Eligibility">
/// The last answer to whether the local user may make an offer, or <see langword="null"/> when none was received.
/// </param>
/// <param name="SearchResult">A page of the last search result, or <see langword="null"/> when none was received.</param>
/// <param name="OwnOffers">A page of the local user's own offers, or <see langword="null"/> when none were received.</param>
/// <param name="ItemStats">A page of the item statistics received so far.</param>
/// <param name="LastMakeOfferResult">The result of the last offer made, or <see langword="null"/> when none was received.</param>
/// <param name="LastBuyResult">The result of the last purchase, or <see langword="null"/> when none was received.</param>
/// <param name="LastCancelOfferResult">
/// The result of the last offer cancellation, or <see langword="null"/> when none was received.
/// </param>
/// <param name="LastCancelAllOffersResult">
/// The result of the last cancellation of all offers, or <see langword="null"/> when none was received.
/// </param>
/// <param name="LastClearHistoryResult">
/// The result of the last own history clear, or <see langword="null"/> when none was received.
/// </param>
public sealed record MarketplaceStateView(
    long Generation,
    long Revision,
    MarketplaceConfiguration? Configuration,
    MarketplaceCanMakeOfferResult? Eligibility,
    MarketplaceOfferPage? SearchResult,
    MarketplaceOwnOfferPage? OwnOffers,
    MarketplaceItemStatsPage ItemStats,
    MarketplaceMakeOfferResult? LastMakeOfferResult,
    MarketplaceBuyResult? LastBuyResult,
    MarketplaceCancelOfferResult? LastCancelOfferResult,
    MarketplaceCancelAllOffersSnapshot? LastCancelAllOffersResult,
    MarketplaceClearOwnHistoryResult? LastClearHistoryResult);

/// <summary>
/// Represents a summary of the stored marketplace state.
/// </summary>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state, which increases with every change.</param>
/// <param name="ConfigurationLoaded">Whether the marketplace configuration was received.</param>
/// <param name="EligibilityLoaded">Whether an answer to whether the local user may make an offer was received.</param>
/// <param name="CachedSearchOffers">The number of offers in the last search result, or 0 when none was received.</param>
/// <param name="TotalSearchOffers">
/// The total number of items the hotel reported for the last search, or 0 when none was received.
/// </param>
/// <param name="CachedOwnOffers">The number of own offers stored, or 0 when none were received.</param>
/// <param name="CachedItemStats">The number of item statistics stored.</param>
public sealed record MarketplaceStateSummary(
    long Generation,
    long Revision,
    bool ConfigurationLoaded,
    bool EligibilityLoaded,
    int CachedSearchOffers,
    int TotalSearchOffers,
    int CachedOwnOffers,
    int CachedItemStats);

/// <summary>
/// Represents the result of a marketplace message sent without waiting for a response.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.MarketplaceOfferBuySend"/>,
/// <see cref="ApplicationMemberIds.MarketplaceOfferCancelSend"/>,
/// <see cref="ApplicationMemberIds.MarketplaceCreditsRedeem"/> and
/// <see cref="ApplicationMemberIds.MarketplaceTokensBuy"/>.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the message was sent.</param>
/// <param name="OfferId">The id of the offer the message addressed, or <see langword="null"/> for commands without an offer.</param>
/// <param name="Category">
/// The own offers category the message addressed, or <see langword="null"/> for messages without a category.
/// </param>
public sealed record MarketplaceDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    Id? OfferId = null,
    MarketplaceOwnOffersCategory? Category = null);

/// <summary>
/// Specifies the marketplace category of the items in a new offer.
/// </summary>
public enum MarketplaceSellCategory
{
    /// <summary>Floor furni.</summary>
    Floor = (int)MarketplaceFurniCategory.Floor,
    /// <summary>Wall furni.</summary>
    Wall = (int)MarketplaceFurniCategory.Wall
}

/// <summary>
/// Specifies which part of the local user's own offer history is cleared.
/// </summary>
public enum MarketplaceHistoryCategory
{
    /// <summary>Offers that sold.</summary>
    Sold = (int)MarketplaceOwnOffersCategory.Sold,
    /// <summary>Offers that expired unsold.</summary>
    Expired = (int)MarketplaceOwnOffersCategory.Expired
}

/// <summary>
/// Specifies the kind of change reported by <see cref="MarketplaceChanged"/>.
/// </summary>
public enum MarketplaceChangeKind
{
    /// <summary>The marketplace configuration was received.</summary>
    Configuration,
    /// <summary>An answer to whether the local user may make an offer was received.</summary>
    Eligibility,
    /// <summary>A search result was received.</summary>
    Search,
    /// <summary>The local user's own offers were received.</summary>
    OwnOffers,
    /// <summary>The statistics of a furni kind were received.</summary>
    ItemStats,
    /// <summary>The result of making an offer was received.</summary>
    MakeResult,
    /// <summary>The result of a purchase was received.</summary>
    BuyResult,
    /// <summary>The result of canceling an offer was received.</summary>
    CancelResult,
    /// <summary>The result of canceling all offers was received.</summary>
    CancelAllResult,
    /// <summary>The result of clearing the own offer history was received.</summary>
    ClearHistoryResult,
    /// <summary>The state was cleared because the hotel connection closed.</summary>
    Reset
}

/// <summary>
/// Represents a change of the marketplace state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceChanged"/> for every change, including resets.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="State">The summary of the marketplace state after the change.</param>
public sealed record MarketplaceChanged(
    MarketplaceChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    MarketplaceStateSummary State);

/// <summary>
/// Represents the marketplace configuration received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceConfigurationChanged"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the configuration was stored.</param>
/// <param name="ChangedAtUtc">The time the event was published.</param>
/// <param name="Configuration">The marketplace configuration.</param>
public sealed record MarketplaceConfigurationChanged(
    long Generation,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    MarketplaceConfiguration Configuration);

/// <summary>
/// Represents an answer to whether the local user may make a marketplace offer.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceEligibilityChanged"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the answer was stored.</param>
/// <param name="ChangedAtUtc">The time the event was published.</param>
/// <param name="Eligibility">The answer received from the hotel.</param>
public sealed record MarketplaceEligibilityChanged(
    long Generation,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    MarketplaceCanMakeOfferResult Eligibility);

/// <summary>
/// Represents a marketplace search result received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceSearchReceived"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The first page of the result, with at most 100 offers.</param>
public sealed record MarketplaceSearchReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceOfferPage Result);

/// <summary>
/// Represents the local user's own marketplace offers received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceOwnOffersReceived"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the offers were stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The first page of the offers, with at most 100 offers.</param>
public sealed record MarketplaceOwnOffersReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceOwnOfferPage Result);

/// <summary>
/// Represents the marketplace statistics of one furni kind received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceItemStatsReceived"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the statistics were stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The statistics received.</param>
public sealed record MarketplaceItemStatsReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceItemStatsSnapshot Result);

/// <summary>
/// Represents the result of making a marketplace offer received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceOfferMakeResult"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The result received.</param>
public sealed record MarketplaceMakeOfferResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceMakeOfferResult Result);

/// <summary>
/// Represents the result of a marketplace purchase received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceOfferBuyResult"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The result received.</param>
public sealed record MarketplaceBuyResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceBuyResult Result);

/// <summary>
/// Represents the result of canceling a marketplace offer received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceOfferCancelResult"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The result received.</param>
public sealed record MarketplaceCancelResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceCancelOfferResult Result);

/// <summary>
/// Represents the result of canceling all marketplace offers received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceOffersCancelAllResult"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The result received, with the distinct ids of the canceled offers.</param>
public sealed record MarketplaceCancelAllResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceCancelAllOffersSnapshot Result);

/// <summary>
/// Represents the result of clearing the own marketplace offer history received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.MarketplaceHistoryClearResult"/>.
/// </remarks>
/// <param name="Generation">The session generation of the marketplace state.</param>
/// <param name="Revision">The revision of the marketplace state after the result was stored.</param>
/// <param name="ReceivedAtUtc">The time the event was published.</param>
/// <param name="Result">The result received.</param>
public sealed record MarketplaceHistoryClearResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceClearOwnHistoryResult Result);
