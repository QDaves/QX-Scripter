using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read a page of the subscription state from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsState"/>. Up to 16 snapshots are retained,
/// and a retained snapshot becomes unavailable when the hotel session changes or the state is reset.
/// </remarks>
/// <param name="ProductName">
/// The product name to filter by, compared without regard to case, or <see langword="null"/> for all products.
/// Must not be blank when set.
/// </param>
/// <param name="Offset">The zero-based offset of the first product to return.</param>
/// <param name="Limit">The maximum number of products to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.
/// Required when <paramref name="Offset"/> is greater than 0, and the snapshot must have been captured
/// with the same <paramref name="ProductName"/>.
/// </param>
public sealed record SubscriptionStateRequest(
    string? ProductName = null,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents the subscription info the hotel sent for one product.
/// </summary>
/// <param name="Revision">The user info revision at which the product was last stored.</param>
/// <param name="ProductName">The subscription product name, such as <c>habbo_club</c> or <c>builders_club</c>.</param>
/// <param name="DaysToPeriodEnd">The number of days until the current period ends.</param>
/// <param name="MemberPeriods">The number of periods the user has been a member.</param>
/// <param name="PeriodsSubscribedAhead">The number of periods paid in advance.</param>
/// <param name="ResponseType">The response type code sent by the hotel.</param>
/// <param name="HasEverBeenMember">Whether the user has ever been a member.</param>
/// <param name="IsVip">Whether the subscription is a VIP subscription.</param>
/// <param name="PastClubDays">The number of days of past club membership.</param>
/// <param name="PastVipDays">The number of days of past VIP membership.</param>
/// <param name="MinutesUntilExpiration">The number of minutes until the subscription expires.</param>
/// <param name="MinutesSinceLastModified">
/// The number of minutes since the subscription was last modified, or <see langword="null"/> when the hotel did not send it.
/// </param>
public sealed record SubscriptionProductView(
    long Revision,
    string ProductName,
    int DaysToPeriodEnd,
    int MemberPeriods,
    int PeriodsSubscribedAhead,
    int ResponseType,
    bool HasEverBeenMember,
    bool IsVip,
    int PastClubDays,
    int PastVipDays,
    int MinutesUntilExpiration,
    int? MinutesSinceLastModified);

/// <summary>
/// Represents the Habbo Club kickback summary the hotel sent.
/// </summary>
/// <param name="CurrentHcStreak">The length of the current Habbo Club streak.</param>
/// <param name="FirstSubscriptionDate">The date of the first subscription as sent by the hotel.</param>
/// <param name="KickbackPercentage">The kickback percentage.</param>
/// <param name="TotalCreditsMissed">The total credits missed.</param>
/// <param name="TotalCreditsRewarded">The total credits rewarded.</param>
/// <param name="TotalCreditsSpent">The total credits spent.</param>
/// <param name="CreditRewardForStreakBonus">The credit reward for the streak bonus.</param>
/// <param name="CreditRewardForMonthlySpent">The credit reward for the credits spent this month.</param>
/// <param name="TimeUntilPayday">The time until the next payday as sent by the hotel.</param>
public sealed record SubscriptionKickbackView(
    int CurrentHcStreak,
    string FirstSubscriptionDate,
    double KickbackPercentage,
    int TotalCreditsMissed,
    int TotalCreditsRewarded,
    int TotalCreditsSpent,
    int CreditRewardForStreakBonus,
    int CreditRewardForMonthlySpent,
    int TimeUntilPayday);

/// <summary>
/// Represents the Builders Club membership status the hotel sent.
/// </summary>
/// <param name="SecondsLeft">The number of seconds left in the membership.</param>
/// <param name="FurniLimit">The number of Builders Club furni the user can place.</param>
/// <param name="MaxFurniLimit">The maximum Builders Club furni limit.</param>
/// <param name="SecondsLeftWithGrace">
/// The number of seconds left including the grace period, or <see langword="null"/> when the hotel did not send it.
/// </param>
/// <param name="EffectiveSecondsLeftWithGrace">
/// The value of <paramref name="SecondsLeftWithGrace"/>, or <paramref name="SecondsLeft"/> when it was not sent.
/// </param>
public sealed record SubscriptionBuildersClubMembershipView(
    int SecondsLeft,
    int FurniLimit,
    int MaxFurniLimit,
    int? SecondsLeftWithGrace,
    int EffectiveSecondsLeftWithGrace);

/// <summary>
/// Specifies whether a Builders Club placement targets the floor or a wall.
/// </summary>
public enum SubscriptionPlacementKind
{
    /// <summary>A floor item placement on a tile.</summary>
    Floor,
    /// <summary>A wall item placement at a wall location.</summary>
    Wall
}

/// <summary>
/// Represents a Builders Club placement warning the hotel sent.
/// </summary>
/// <param name="PageId">The catalog page id of the placement.</param>
/// <param name="OfferId">The offer id of the placement.</param>
/// <param name="ExtraParam">The extra parameter of the placement.</param>
/// <param name="PlacementKind">Whether the placement targets the floor or a wall.</param>
/// <param name="X">The tile x coordinate, or <see langword="null"/> for a wall placement.</param>
/// <param name="Y">The tile y coordinate, or <see langword="null"/> for a wall placement.</param>
/// <param name="Direction">The item direction, or <see langword="null"/> for a wall placement.</param>
/// <param name="WallLocation">The wall location, or <see langword="null"/> for a floor placement.</param>
public sealed record SubscriptionBuildersClubPlacementWarningView(
    int PageId,
    int OfferId,
    string ExtraParam,
    SubscriptionPlacementKind PlacementKind,
    int? X,
    int? Y,
    int? Direction,
    string? WallLocation);

/// <summary>
/// Represents one club membership offer the hotel sells.
/// </summary>
/// <param name="OfferId">The id of the offer.</param>
/// <param name="ProductCode">The product code of the offer.</param>
/// <param name="PriceCredits">The price in credits.</param>
/// <param name="PriceActivityPoints">The price in activity points.</param>
/// <param name="PriceActivityPointType">The activity point type of <paramref name="PriceActivityPoints"/>.</param>
/// <param name="IsVip">Whether the offer is a VIP membership.</param>
/// <param name="Months">The number of months of membership the offer grants.</param>
/// <param name="ExtraDays">The number of extra days of membership the offer grants.</param>
/// <param name="IsGiftable">Whether the offer can be bought as a gift.</param>
/// <param name="DaysLeftAfterPurchase">The number of membership days the account would have left after buying the offer.</param>
/// <param name="Year">The year of the expiry date the account would reach after buying the offer.</param>
/// <param name="Month">The month of the expiry date the account would reach after buying the offer.</param>
/// <param name="Day">The day of the expiry date the account would reach after buying the offer.</param>
public sealed record SubscriptionClubOfferView(
    int OfferId,
    string ProductCode,
    int PriceCredits,
    int PriceActivityPoints,
    int PriceActivityPointType,
    bool IsVip,
    int Months,
    int ExtraDays,
    bool IsGiftable,
    int DaysLeftAfterPurchase,
    int Year,
    int Month,
    int Day)
{
    /// <summary>
    /// Gets the unnamed flag the hotel sends after the product code.
    /// </summary>
    public bool ReservedWireFlag { get; init; }
}

/// <summary>
/// Represents a summary of the last club offers received.
/// </summary>
/// <param name="DaysLeft">The number of membership days the account has left, as sent with the offers.</param>
/// <param name="TotalOffers">The number of offers received.</param>
public sealed record SubscriptionClubOffersSummaryView(
    int DaysLeft,
    int TotalOffers);

/// <summary>
/// Represents a request to read a page of the last club offers received from a snapshot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsClubOffersList"/>. Up to 16 club offer snapshots
/// are retained, and a retained snapshot becomes unavailable when the hotel session changes or the state is
/// reset. The response does not identify the offer type it was requested for.
/// </remarks>
/// <param name="Offset">The zero-based offset of the first offer to return.</param>
/// <param name="Limit">The maximum number of offers to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">
/// The revision of a retained club offer snapshot to read, or <see langword="null"/> to capture the current state.
/// Required when <paramref name="Offset"/> is greater than 0.
/// </param>
public sealed record SubscriptionClubOffersPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>
/// Represents a request to fetch the club offers from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsClubOffersRefresh"/>. The request is sent once
/// without a retry and completes with the first club offer snapshot stored after it was sent. The response
/// does not identify the offer type, and it is not blocked from the game client.
/// </remarks>
/// <param name="OfferType">The offer set selector sent to the hotel.</param>
/// <param name="Limit">The maximum number of offers in the returned first page, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The time to wait for the response in milliseconds, at least 1.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record SubscriptionClubOffersRefreshRequest(
    int OfferType = 1,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a page of the club offers read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsClubOffersList"/> and
/// <see cref="ApplicationMemberIds.SubscriptionsClubOffersRefresh"/>.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="Revision">The subscription state revision of the snapshot.</param>
/// <param name="ClubOffersRevision">The revision increased when club offers are received or the state resets.</param>
/// <param name="SnapshotRevision">The revision of the retained club offer snapshot, passed back to read the next page.</param>
/// <param name="Loaded">Whether club offers were received.</param>
/// <param name="DaysLeft">
/// The number of membership days the account has left, as sent with the offers, or <see langword="null"/>
/// when <paramref name="Loaded"/> is <see langword="false"/>.
/// </param>
/// <param name="TotalOffers">The number of offers in the snapshot, or 0 when <paramref name="Loaded"/> is <see langword="false"/>.</param>
/// <param name="Offset">The zero-based offset of the first offer in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Offers">The offers in the page, in the order the hotel sent them.</param>
public sealed record SubscriptionClubOffersPage(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long ClubOffersRevision,
    long SnapshotRevision,
    bool Loaded,
    int? DaysLeft,
    int TotalOffers,
    int Offset,
    int? NextOffset,
    IReadOnlyList<SubscriptionClubOfferView> Offers);

/// <summary>
/// Represents a request to place a Builders Club floor offer in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace"/>. A ready room is
/// required. The message is sent once, only while the session and room are unchanged, and the hotel's
/// acceptance is not awaited.
/// </remarks>
/// <param name="PageId">The catalog page id, sent unchanged.</param>
/// <param name="OfferId">The Builders Club offer id, sent unchanged.</param>
/// <param name="X">The tile x coordinate.</param>
/// <param name="Y">The tile y coordinate.</param>
/// <param name="Direction">The item direction.</param>
/// <param name="ExtraData">The offer selection data, at most 65535 UTF-8 bytes.</param>
/// <param name="IsRetry">Whether the placement is flagged to the hotel as a retry.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the placement must run in, or <see langword="null"/> to use the active session.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the placement must run in, or <see langword="null"/> to use the current room.
/// </param>
public sealed record SubscriptionBuildersClubFloorPlaceRequest(
    int PageId,
    int OfferId,
    int X,
    int Y,
    int Direction = 0,
    string ExtraData = "",
    bool IsRetry = false,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents a request to place a Builders Club wall offer in the current room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsBuildersClubWallOfferPlace"/>. A ready room is
/// required. The message is sent once, only while the session and room are unchanged, and the hotel's
/// acceptance is not awaited.
/// </remarks>
/// <param name="PageId">The catalog page id, sent unchanged.</param>
/// <param name="OfferId">The Builders Club offer id, sent unchanged.</param>
/// <param name="WallLocation">The wall location in the Flash text form, not blank and at most 65535 UTF-8 bytes.</param>
/// <param name="ExtraData">The offer selection data, at most 65535 UTF-8 bytes.</param>
/// <param name="IsRetry">Whether the placement is flagged to the hotel as a retry.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the placement must run in, or <see langword="null"/> to use the active session.
/// </param>
/// <param name="ExpectedRoomGeneration">
/// The room generation the placement must run in, or <see langword="null"/> to use the current room.
/// </param>
public sealed record SubscriptionBuildersClubWallPlaceRequest(
    int PageId,
    int OfferId,
    string WallLocation,
    string ExtraData = "",
    bool IsRetry = false,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

/// <summary>
/// Represents the receipt of a Builders Club placement message sent to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsBuildersClubFloorOfferPlace"/> and
/// <see cref="ApplicationMemberIds.SubscriptionsBuildersClubWallOfferPlace"/>. The receipt does not mean
/// the hotel accepted the placement.
/// </remarks>
/// <param name="PlacementKind">Whether the placement targets the floor or a wall.</param>
/// <param name="DispatchedAtUtc">The time the receipt was created after the message was sent.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the message was sent in.</param>
/// <param name="RoomId">The id of the room the placement was sent for.</param>
/// <param name="RoomGeneration">The room generation the message was sent in.</param>
/// <param name="RoomRevision">The room state revision when the message was sent.</param>
/// <param name="PageId">The catalog page id that was sent.</param>
/// <param name="OfferId">The Builders Club offer id that was sent.</param>
/// <param name="IsRetry">Whether the placement was flagged as a retry.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record SubscriptionBuildersClubPlacementDispatchReceipt(
    SubscriptionPlacementKind PlacementKind,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int PageId,
    int OfferId,
    bool IsRetry,
    int MessagesDispatched);

/// <summary>
/// Represents a page of the subscription state read from a snapshot.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsState"/>. Products are sorted by product name
/// without regard to case. The other values are the last ones received, independent of the page.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The state generation of the hotel session the snapshot was captured in.</param>
/// <param name="Revision">The subscription state revision, increased by every received value and every reset.</param>
/// <param name="UserInfoRevision">The revision increased when product info is received or the state resets.</param>
/// <param name="KickbackRevision">The revision increased when kickback info is received or the state resets.</param>
/// <param name="BuildersClubFurniCountRevision">
/// The revision increased when a Builders Club furni count is received or the state resets.
/// </param>
/// <param name="BuildersClubMembershipRevision">
/// The revision increased when a Builders Club membership status is received or the state resets.
/// </param>
/// <param name="BuildersClubPlacementWarningRevision">
/// The revision increased when a Builders Club placement warning is received or the state resets.
/// </param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, passed back to read the next page.</param>
/// <param name="TotalProducts">The number of products stored, at most 500, regardless of the product filter.</param>
/// <param name="MatchedProducts">The number of products that match the product filter.</param>
/// <param name="Offset">The zero-based offset of the first product in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when this is the last page.</param>
/// <param name="Products">The products in the page.</param>
/// <param name="Kickback">The last kickback info received, or <see langword="null"/> when none was received.</param>
/// <param name="BuildersClubFurniCount">
/// The number of Builders Club furni placed as of the last count received, or <see langword="null"/> when none was received.
/// </param>
/// <param name="BuildersClubMembership">
/// The last Builders Club membership status received, or <see langword="null"/> when none was received.
/// </param>
/// <param name="LastPlacementWarning">
/// The last Builders Club placement warning received, or <see langword="null"/> when none was received.
/// </param>
public sealed record SubscriptionStateView(
    bool Connected,
    long SessionGeneration,
    long Revision,
    long UserInfoRevision,
    long KickbackRevision,
    long BuildersClubFurniCountRevision,
    long BuildersClubMembershipRevision,
    long BuildersClubPlacementWarningRevision,
    long SnapshotRevision,
    int TotalProducts,
    int MatchedProducts,
    int Offset,
    int? NextOffset,
    IReadOnlyList<SubscriptionProductView> Products,
    SubscriptionKickbackView? Kickback,
    int? BuildersClubFurniCount,
    SubscriptionBuildersClubMembershipView? BuildersClubMembership,
    SubscriptionBuildersClubPlacementWarningView? LastPlacementWarning)
{
    /// <summary>
    /// Gets the revision increased when club offers are received or the state resets.
    /// </summary>
    public long ClubOffersRevision { get; init; }
    /// <summary>
    /// Gets the summary of the last club offers received, or <see langword="null"/> when none were received.
    /// </summary>
    public SubscriptionClubOffersSummaryView? ClubOffers { get; init; }
}

/// <summary>
/// Represents a request to fetch the subscription info of one product from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsUserInfoRefresh"/>. The request is sent up to twice
/// and completes with the first info for the product, compared without regard to case, that is stored after
/// it was sent. The response is not blocked from the game client.
/// </remarks>
/// <param name="ProductName">
/// The subscription product name sent to the hotel, such as <c>habbo_club</c> or <c>builders_club</c>.
/// Must not be blank.
/// </param>
/// <param name="TimeoutMilliseconds">The total time to wait across both attempts in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record SubscriptionUserInfoRefreshRequest(
    string ProductName = "habbo_club",
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to fetch the Habbo Club kickback info from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsKickbackRefresh"/>. The request is sent up to twice
/// and completes with the first kickback info stored after it was sent. The response is not blocked from the
/// game client.
/// </remarks>
/// <param name="TimeoutMilliseconds">The total time to wait across both attempts in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record SubscriptionKickbackRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents a request to fetch the Builders Club furni count from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.SubscriptionsBuildersClubFurniCountRefresh"/>. The request is
/// sent up to twice and completes with the first furni count stored after it was sent. The response is not
/// blocked from the game client.
/// </remarks>
/// <param name="TimeoutMilliseconds">The total time to wait across both attempts in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">
/// The session generation the refresh must run in, or <see langword="null"/> to use the active session.
/// </param>
public sealed record SubscriptionBuildersClubFurniCountRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>
/// Represents the result of a subscription user info refresh.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsUserInfoRefresh"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="Revision">The subscription state revision after the response was stored.</param>
/// <param name="UserInfoRevision">The user info revision after the response was stored.</param>
/// <param name="ObservedAtUtc">The time the matching response was stored.</param>
/// <param name="Product">The subscription info received for the product.</param>
public sealed record SubscriptionUserInfoRefreshResult(
    long SessionGeneration,
    long Revision,
    long UserInfoRevision,
    DateTimeOffset ObservedAtUtc,
    SubscriptionProductView Product);

/// <summary>
/// Represents the result of a Habbo Club kickback refresh.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsKickbackRefresh"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="Revision">The subscription state revision after the response was stored.</param>
/// <param name="KickbackRevision">The kickback revision after the response was stored.</param>
/// <param name="ObservedAtUtc">The time the matching response was stored.</param>
/// <param name="Kickback">The kickback info received.</param>
public sealed record SubscriptionKickbackRefreshResult(
    long SessionGeneration,
    long Revision,
    long KickbackRevision,
    DateTimeOffset ObservedAtUtc,
    SubscriptionKickbackView Kickback);

/// <summary>
/// Represents the result of a Builders Club furni count refresh.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.SubscriptionsBuildersClubFurniCountRefresh"/>.
/// </remarks>
/// <param name="SessionGeneration">The state generation of the hotel session the refresh ran in.</param>
/// <param name="Revision">The subscription state revision after the response was stored.</param>
/// <param name="BuildersClubFurniCountRevision">The Builders Club furni count revision after the response was stored.</param>
/// <param name="ObservedAtUtc">The time the matching response was stored.</param>
/// <param name="FurniCount">The number of Builders Club furni placed.</param>
public sealed record SubscriptionBuildersClubFurniCountRefreshResult(
    long SessionGeneration,
    long Revision,
    long BuildersClubFurniCountRevision,
    DateTimeOffset ObservedAtUtc,
    int FurniCount);

/// <summary>
/// Specifies the kind of change reported by <see cref="SubscriptionChanged"/>.
/// </summary>
public enum SubscriptionChangeKind
{
    /// <summary>Subscription info for a product was received.</summary>
    UserInfo,
    /// <summary>Habbo Club kickback info was received.</summary>
    KickbackInfo,
    /// <summary>A Builders Club furni count was received.</summary>
    BuildersClubFurniCount,
    /// <summary>A Builders Club membership status was received.</summary>
    BuildersClubMembershipStatus,
    /// <summary>A Builders Club placement warning was received.</summary>
    BuildersClubPlacementWarning,
    /// <summary>The state was cleared because the manager was reset or a new hotel session connected.</summary>
    Reset,
    /// <summary>Club offers were received.</summary>
    ClubOffers
}

/// <summary>
/// Represents a change of the subscription state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.SubscriptionsChanged"/>. Only the value that matches
/// <paramref name="Kind"/> is set.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The state generation of the hotel session.</param>
/// <param name="Revision">The subscription state revision after the change.</param>
/// <param name="SourceRevision">
/// The revision of the changed value, such as the kickback revision for a
/// <see cref="SubscriptionChangeKind.KickbackInfo"/> change, or the state revision for a
/// <see cref="SubscriptionChangeKind.Reset"/>.
/// </param>
/// <param name="Product">
/// The received product info, or <see langword="null"/> when the change is not a
/// <see cref="SubscriptionChangeKind.UserInfo"/>.
/// </param>
/// <param name="Kickback">
/// The received kickback info, or <see langword="null"/> when the change is not a
/// <see cref="SubscriptionChangeKind.KickbackInfo"/>.
/// </param>
/// <param name="BuildersClubFurniCount">
/// The received Builders Club furni count, or <see langword="null"/> when the change is not a
/// <see cref="SubscriptionChangeKind.BuildersClubFurniCount"/>.
/// </param>
/// <param name="BuildersClubMembership">
/// The received Builders Club membership status, or <see langword="null"/> when the change is not a
/// <see cref="SubscriptionChangeKind.BuildersClubMembershipStatus"/>.
/// </param>
/// <param name="PlacementWarning">
/// The received Builders Club placement warning, or <see langword="null"/> when the change is not a
/// <see cref="SubscriptionChangeKind.BuildersClubPlacementWarning"/>.
/// </param>
public sealed record SubscriptionChanged(
    SubscriptionChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    SubscriptionProductView? Product,
    SubscriptionKickbackView? Kickback,
    int? BuildersClubFurniCount,
    SubscriptionBuildersClubMembershipView? BuildersClubMembership,
    SubscriptionBuildersClubPlacementWarningView? PlacementWarning)
{
    /// <summary>
    /// Gets the summary of the received club offers, or <see langword="null"/> when the change is not a
    /// <see cref="SubscriptionChangeKind.ClubOffers"/>.
    /// </summary>
    public SubscriptionClubOffersSummaryView? ClubOffers { get; init; }
}

internal interface ISubscriptionOperations
{
    void RequestUserInfo(string product_name);
    void RequestKickbackInfo();
    void RequestBuildersClubFurniCount();
}
