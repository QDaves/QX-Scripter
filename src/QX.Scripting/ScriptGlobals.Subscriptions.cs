using System.Collections.ObjectModel;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets every subscription the server has reported on, keyed by product name and ignoring case.
    /// </summary>
    /// <remarks>
    /// Each entry holds the days left in the period, the periods held and paid ahead, the VIP flag
    /// and the minutes until expiry. The map is empty until the server has sent subscription info,
    /// and holds at most the first 500 products. Every read builds a new snapshot copy, not a live
    /// view.
    /// </remarks>
    public IReadOnlyDictionary<string, ScrSendUserInfo> SubscriptionInfo
    {
        get
        {
            SubscriptionStateView state = ReadSubscriptionState();
            var products = state.Products.ToDictionary(
                product => product.ProductName,
                LegacySubscriptionProduct,
                StringComparer.OrdinalIgnoreCase);
            return new ReadOnlyDictionary<string, ScrSendUserInfo>(products);
        }
    }

    /// <summary>
    /// Gets the Habbo Club kickback summary, or <see langword="null"/> until the server has sent it.
    /// </summary>
    /// <remarks>
    /// The summary holds the streak length, the first subscription date, the kickback percentage,
    /// the credits spent, missed and rewarded, and the time until the next payday. Every read
    /// builds a new copy.
    /// </remarks>
    public ScrSendKickbackInfo? SubscriptionKickback
    {
        get
        {
            SubscriptionKickbackView? kickback = ReadSubscriptionState().Kickback;
            return kickback is null ? null : LegacySubscriptionKickback(kickback);
        }
    }

    /// <summary>
    /// Gets how many Builders Club furniture the local user has placed, as of the last count the
    /// server sent.
    /// </summary>
    /// <remarks>
    /// It is <see langword="null"/> until the server has sent a count.
    /// </remarks>
    public BuildersClubFurniCount? BuildersClubFurnitureCount
    {
        get
        {
            int? furni_count = ReadSubscriptionState().BuildersClubFurniCount;
            return furni_count is int value ? new BuildersClubFurniCount(value) : null;
        }
    }

    /// <summary>
    /// Gets the last Builders Club membership status the server sent, or <see langword="null"/>
    /// when none has arrived.
    /// </summary>
    /// <remarks>
    /// The status holds the seconds left, the furni limit, the maximum furni limit and, when the
    /// server sends it, the seconds left including the grace period. Every read builds a new copy.
    /// </remarks>
    public BuildersClubMembershipStatus? BuildersClubMembership
    {
        get
        {
            SubscriptionBuildersClubMembershipView? membership =
                ReadSubscriptionState().BuildersClubMembership;
            return membership is null ? null : LegacySubscriptionMembership(membership);
        }
    }

    /// <summary>
    /// Gets the last Builders Club placement warning the server sent, or <see langword="null"/>
    /// when none has arrived.
    /// </summary>
    /// <remarks>
    /// The warning names the catalog page, offer and extra parameter of the placement, and its
    /// floor tile and direction or its wall location.
    /// </remarks>
    /// <exception cref="InvalidDataException">Thrown when the cached warning lacks the position data its placement kind needs.</exception>
    public BuildersClubPlacementWarning? LastBuildersClubPlacementWarning
    {
        get
        {
            SubscriptionBuildersClubPlacementWarningView? warning =
                ReadSubscriptionState().LastPlacementWarning;
            return warning is null ? null : LegacySubscriptionPlacementWarning(warning);
        }
    }

    /// <summary>Finds one cached subscription by product name.</summary>
    /// <param name="productName">
    /// The subscription product, for example <c>habbo_club</c> or <c>builders_club</c>, matched
    /// ignoring case.
    /// </param>
    /// <returns>
    /// The subscription info, or <see langword="null"/> when the server has not sent info for this
    /// product.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="productName"/> is <see langword="null"/>.</exception>
    public ScrSendUserInfo? FindSubscription(string productName)
    {
        ArgumentNullException.ThrowIfNull(productName);
        SubscriptionProductView? product = ReadSubscriptionState().Products.FirstOrDefault(
            value => string.Equals(
                value.ProductName,
                productName,
                StringComparison.OrdinalIgnoreCase));
        return product is null ? null : LegacySubscriptionProduct(product);
    }

    /// <summary>
    /// Requests one subscription's details from the server.
    /// </summary>
    /// <remarks>
    /// It returns immediately. The answer lands in <see cref="SubscriptionInfo"/>, keyed by the
    /// product name the server echoes back, and raises <see cref="OnSubscriptionInfoChanged"/>.
    /// </remarks>
    /// <param name="productName">
    /// The subscription product to ask about. Defaults to <c>habbo_club</c>; <c>builders_club</c>
    /// is the other product the hotel uses.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="productName"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void RequestSubscriptionInfo(
        string productName = "habbo_club") =>
        Subscriptions.RequestUserInfo(productName);

    /// <summary>
    /// Requests the Habbo Club kickback summary from the server.
    /// </summary>
    /// <remarks>
    /// It returns immediately. The answer lands in <see cref="SubscriptionKickback"/> and raises
    /// <see cref="OnSubscriptionKickbackChanged"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void RequestSubscriptionKickback() =>
        Subscriptions.RequestKickbackInfo();

    /// <summary>
    /// Requests how many Builders Club furniture the local user has placed.
    /// </summary>
    /// <remarks>
    /// It returns immediately. The answer lands in <see cref="BuildersClubFurnitureCount"/> and
    /// raises <see cref="OnBuildersClubFurniCountChanged"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void RequestBuildersClubFurniCount() =>
        Subscriptions.RequestBuildersClubFurniCount();

    private SubscriptionStateView ReadSubscriptionState() =>
        _application.Invoke<SubscriptionStateRequest, SubscriptionStateView>(
            ApplicationMemberIds.SubscriptionsState,
            new SubscriptionStateRequest(Limit: 500),
            Ct);

    private static ScrSendUserInfo LegacySubscriptionProduct(
        SubscriptionProductView product) => new(
        product.ProductName,
        product.DaysToPeriodEnd,
        product.MemberPeriods,
        product.PeriodsSubscribedAhead,
        product.ResponseType,
        product.HasEverBeenMember,
        product.IsVip,
        product.PastClubDays,
        product.PastVipDays,
        product.MinutesUntilExpiration,
        product.MinutesSinceLastModified);

    private static ScrSendKickbackInfo LegacySubscriptionKickback(
        SubscriptionKickbackView kickback) => new(
        kickback.CurrentHcStreak,
        kickback.FirstSubscriptionDate,
        kickback.KickbackPercentage,
        kickback.TotalCreditsMissed,
        kickback.TotalCreditsRewarded,
        kickback.TotalCreditsSpent,
        kickback.CreditRewardForStreakBonus,
        kickback.CreditRewardForMonthlySpent,
        kickback.TimeUntilPayday);

    private static BuildersClubMembershipStatus LegacySubscriptionMembership(
        SubscriptionBuildersClubMembershipView membership) => new(
        membership.SecondsLeft,
        membership.FurniLimit,
        membership.MaxFurniLimit,
        membership.SecondsLeftWithGrace);

    private static BuildersClubPlacementWarning LegacySubscriptionPlacementWarning(
        SubscriptionBuildersClubPlacementWarningView warning) => new(
        warning.PageId,
        warning.OfferId,
        warning.ExtraParam,
        warning.PlacementKind switch
        {
            SubscriptionPlacementKind.Floor => new BuildersClubFloorPlacement(
                warning.X ?? throw new InvalidDataException(
                    "A floor placement warning requires an X coordinate."),
                warning.Y ?? throw new InvalidDataException(
                    "A floor placement warning requires a Y coordinate."),
                warning.Direction ?? throw new InvalidDataException(
                    "A floor placement warning requires a direction.")),
            SubscriptionPlacementKind.Wall => new BuildersClubWallPlacement(
                warning.WallLocation ?? throw new InvalidDataException(
                    "A wall placement warning requires a wall location.")),
            _ => throw new InvalidDataException(
                $"Unsupported Builders Club placement kind '{warning.PlacementKind}'.")
        });
}
