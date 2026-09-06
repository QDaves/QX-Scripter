using Qx.Messages;
using Qx.Model.Marketplace;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

public sealed record MarketplaceStateRequest(
    int Page = 0,
    int PageSize = 100);

public sealed record MarketplaceRefreshRequest(
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceItemStatsRequest(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId,
    string ExtraData = "",
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceSearchRequest(
    string SearchQuery = "",
    int MinimumPrice = -1,
    int MaximumPrice = -1,
    MarketplaceSortOrder SortOrder = MarketplaceSortOrder.HighestPrice,
    bool CombineUniqueOffers = true,
    int Page = 0,
    int PageSize = 100,
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceOwnOffersRequest(
    MarketplaceOwnOffersCategory Category = MarketplaceOwnOffersCategory.Open,
    int Page = 0,
    int PageSize = 100,
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceMakeOfferRequest(
    int Price,
    MarketplaceSellCategory FurniCategory,
    IReadOnlyList<Id> ItemIds,
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceBuyRequest(
    Id OfferId,
    string ExtraData = "",
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceBuySendRequest(
    Id OfferId,
    string ExtraData = "");

public sealed record MarketplaceCancelRequest(
    Id OfferId,
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceCancelSendRequest(Id OfferId);

public sealed record MarketplaceCancelAllRequest(
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceHistoryClearRequest(
    MarketplaceHistoryCategory Category,
    int TimeoutMilliseconds = 10000);

public sealed record MarketplaceCommandRequest;

public sealed record MarketplaceOfferPage(
    long Generation,
    long Revision,
    int Page,
    int PageSize,
    int CachedItems,
    int TotalItemsFound,
    IReadOnlyList<MarketplaceOfferSnapshot> Offers);

public sealed record MarketplaceOwnOfferPage(
    long Generation,
    long Revision,
    int Page,
    int PageSize,
    int TotalItems,
    int CreditsWaiting,
    MarketplaceOwnOffersCategory? Category,
    IReadOnlyList<MarketplaceOfferSnapshot> Offers);

public sealed record MarketplaceItemStatsPage(
    int Page,
    int PageSize,
    int TotalItems,
    IReadOnlyList<MarketplaceItemStatsSnapshot> Items);

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

public sealed record MarketplaceStateSummary(
    long Generation,
    long Revision,
    bool ConfigurationLoaded,
    bool EligibilityLoaded,
    int CachedSearchOffers,
    int TotalSearchOffers,
    int CachedOwnOffers,
    int CachedItemStats);

public sealed record MarketplaceDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    Id? OfferId = null,
    MarketplaceOwnOffersCategory? Category = null);

public enum MarketplaceSellCategory
{
    Floor = (int)MarketplaceFurniCategory.Floor,
    Wall = (int)MarketplaceFurniCategory.Wall
}

public enum MarketplaceHistoryCategory
{
    Sold = (int)MarketplaceOwnOffersCategory.Sold,
    Expired = (int)MarketplaceOwnOffersCategory.Expired
}

public enum MarketplaceChangeKind
{
    Configuration,
    Eligibility,
    Search,
    OwnOffers,
    ItemStats,
    MakeResult,
    BuyResult,
    CancelResult,
    CancelAllResult,
    ClearHistoryResult,
    Reset
}

public sealed record MarketplaceChanged(
    MarketplaceChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    MarketplaceStateSummary State);

public sealed record MarketplaceConfigurationChanged(
    long Generation,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    MarketplaceConfiguration Configuration);

public sealed record MarketplaceEligibilityChanged(
    long Generation,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    MarketplaceCanMakeOfferResult Eligibility);

public sealed record MarketplaceSearchReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceOfferPage Result);

public sealed record MarketplaceOwnOffersReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceOwnOfferPage Result);

public sealed record MarketplaceItemStatsReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceItemStatsSnapshot Result);

public sealed record MarketplaceMakeOfferResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceMakeOfferResult Result);

public sealed record MarketplaceBuyResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceBuyResult Result);

public sealed record MarketplaceCancelResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceCancelOfferResult Result);

public sealed record MarketplaceCancelAllResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceCancelAllOffersSnapshot Result);

public sealed record MarketplaceHistoryClearResultReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    MarketplaceClearOwnHistoryResult Result);
