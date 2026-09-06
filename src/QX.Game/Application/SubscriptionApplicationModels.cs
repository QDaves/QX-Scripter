using Qx.Model;

namespace Qx.Game.Application;

public sealed record SubscriptionStateRequest(
    string? ProductName = null,
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

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

public sealed record SubscriptionBuildersClubMembershipView(
    int SecondsLeft,
    int FurniLimit,
    int MaxFurniLimit,
    int? SecondsLeftWithGrace,
    int EffectiveSecondsLeftWithGrace);

public enum SubscriptionPlacementKind
{
    Floor,
    Wall
}

public sealed record SubscriptionBuildersClubPlacementWarningView(
    int PageId,
    int OfferId,
    string ExtraParam,
    SubscriptionPlacementKind PlacementKind,
    int? X,
    int? Y,
    int? Direction,
    string? WallLocation);

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
    public bool ReservedWireFlag { get; init; }
}

public sealed record SubscriptionClubOffersSummaryView(
    int DaysLeft,
    int TotalOffers);

public sealed record SubscriptionClubOffersPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record SubscriptionClubOffersRefreshRequest(
    int OfferType = 1,
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record SubscriptionClubOffersPage(
    bool Connected,
    ClientType? Client,
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

public sealed record SubscriptionBuildersClubWallPlaceRequest(
    int PageId,
    int OfferId,
    string WallLocation,
    string ExtraData = "",
    bool IsRetry = false,
    long? ExpectedSessionGeneration = null,
    long? ExpectedRoomGeneration = null);

public sealed record SubscriptionBuildersClubPlacementDispatchReceipt(
    SubscriptionPlacementKind PlacementKind,
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    Id RoomId,
    long RoomGeneration,
    long RoomRevision,
    int PageId,
    int OfferId,
    bool IsRetry,
    int MessagesDispatched);

public sealed record SubscriptionStateView(
    bool Connected,
    ClientType? Client,
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
    public long ClubOffersRevision { get; init; }
    public SubscriptionClubOffersSummaryView? ClubOffers { get; init; }
}

public sealed record SubscriptionUserInfoRefreshRequest(
    string ProductName = "habbo_club",
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record SubscriptionKickbackRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record SubscriptionBuildersClubFurniCountRefreshRequest(
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record SubscriptionUserInfoRefreshResult(
    ClientType Client,
    long SessionGeneration,
    long Revision,
    long UserInfoRevision,
    DateTimeOffset ObservedAtUtc,
    SubscriptionProductView Product);

public sealed record SubscriptionKickbackRefreshResult(
    ClientType Client,
    long SessionGeneration,
    long Revision,
    long KickbackRevision,
    DateTimeOffset ObservedAtUtc,
    SubscriptionKickbackView Kickback);

public sealed record SubscriptionBuildersClubFurniCountRefreshResult(
    ClientType Client,
    long SessionGeneration,
    long Revision,
    long BuildersClubFurniCountRevision,
    DateTimeOffset ObservedAtUtc,
    int FurniCount);

public enum SubscriptionChangeKind
{
    UserInfo,
    KickbackInfo,
    BuildersClubFurniCount,
    BuildersClubMembershipStatus,
    BuildersClubPlacementWarning,
    Reset,
    ClubOffers
}

public sealed record SubscriptionChanged(
    SubscriptionChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    SubscriptionProductView? Product,
    SubscriptionKickbackView? Kickback,
    int? BuildersClubFurniCount,
    SubscriptionBuildersClubMembershipView? BuildersClubMembership,
    SubscriptionBuildersClubPlacementWarningView? PlacementWarning)
{
    public SubscriptionClubOffersSummaryView? ClubOffers { get; init; }
}

internal interface ISubscriptionOperations
{
    void RequestUserInfo(string product_name);
    void RequestKickbackInfo();
    void RequestBuildersClubFurniCount();
}
