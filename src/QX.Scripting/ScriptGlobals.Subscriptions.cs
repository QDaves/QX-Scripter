using System.Collections.ObjectModel;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Subscriptions;

namespace Qx.Scripting;

/// <content>
/// Subscriptions: Habbo Club, VIP and Builders Club state for the local user, plus the
/// fire-and-forget requests that fill it.
/// <para>
/// Subscription info, the kickback summary and the Builders Club furniture count work on both the
/// Flash and the Unity client. The Builders Club membership status and the placement warning are
/// Flash only — their payloads are Flash-shaped and the tracker registers them for the Flash
/// client alone.
/// </para>
/// <para>
/// Nothing here blocks or returns a value. Each request sends one message and returns
/// immediately; the answer surfaces later through the cached state and the subscription events.
/// To wait for a specific reply, subscribe first and then send the request.
/// </para>
/// <para>The cached state is cleared when the session resets.</para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Every subscription the server has reported on, keyed by product name (case-insensitively),
    /// with the days left in the period, periods held and paid ahead, the VIP flag and the minutes
    /// until expiry. Empty until a subscription has been requested.
    /// </summary>
    /// <returns>A snapshot copy, not a live view.</returns>
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
    /// The Habbo Club kickback summary: streak length, first subscription date, kickback
    /// percentage, credits spent, missed and rewarded, and the time until the next payday.
    /// <see langword="null"/> until it has been requested.
    /// </summary>
    public ScrSendKickbackInfo? SubscriptionKickback
    {
        get
        {
            SubscriptionKickbackView? kickback = ReadSubscriptionState().Kickback;
            return kickback is null ? null : LegacySubscriptionKickback(kickback);
        }
    }

    /// <summary>
    /// How many Builders Club furniture the local user currently has placed, as of the last count
    /// the server sent. <see langword="null"/> until it has been requested.
    /// </summary>
    public BuildersClubFurniCount? BuildersClubFurnitureCount
    {
        get
        {
            int? furni_count = ReadSubscriptionState().BuildersClubFurniCount;
            return furni_count is int value ? new BuildersClubFurniCount(value) : null;
        }
    }

    /// <summary>
    /// The Builders Club membership status: seconds of membership left, the current and maximum
    /// furniture limits, and the grace-period length when the server sends one.
    /// <see langword="null"/> until the server pushes it.
    /// </summary>
    /// <remarks>Flash only; this never becomes non-null on a Unity session.</remarks>
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
    /// The most recent warning that placing this catalog offer would exceed the Builders Club
    /// furniture limit, carrying the catalog page and offer plus the floor or wall position that
    /// was attempted. <see langword="null"/> when no warning has arrived.
    /// </summary>
    /// <remarks>Flash only; this never becomes non-null on a Unity session.</remarks>
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
    /// <param name="product_name">
    /// The subscription product, for example <c>habbo_club</c> or <c>builders_club</c>. Matched
    /// case-insensitively.
    /// </param>
    /// <returns>
    /// The subscription info, or <see langword="null"/> when this product has not been requested.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="product_name"/> is null.</exception>
    public ScrSendUserInfo? FindSubscription(string product_name)
    {
        ArgumentNullException.ThrowIfNull(product_name);
        SubscriptionProductView? product = ReadSubscriptionState().Products.FirstOrDefault(
            value => string.Equals(
                value.ProductName,
                product_name,
                StringComparison.OrdinalIgnoreCase));
        return product is null ? null : LegacySubscriptionProduct(product);
    }

    /// <summary>
    /// Asks for one subscription's details. Returns immediately; the answer lands in the
    /// subscription map, keyed by the product name the server echoes back, and raises the
    /// subscription-info event.
    /// </summary>
    /// <param name="product_name">
    /// The subscription product to ask about. Defaults to <c>habbo_club</c>; <c>builders_club</c>
    /// is the other product the hotel uses.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="product_name"/> is null.</exception>
    public void RequestSubscriptionInfo(
        string product_name = "habbo_club") =>
        Subscriptions.RequestUserInfo(product_name);

    /// <summary>
    /// Asks for the Habbo Club kickback summary. Returns immediately; the answer lands in the
    /// kickback state and raises the kickback event.
    /// </summary>
    public void RequestSubscriptionKickback() =>
        Subscriptions.RequestKickbackInfo();

    /// <summary>
    /// Asks how many Builders Club furniture the local user has placed. Returns immediately; the
    /// answer lands in the furniture-count state and raises the matching event.
    /// </summary>
    public void RequestBuildersClubFurniCount() =>
        Subscriptions.RequestBuildersClubFurniCount();

    private SubscriptionStateView ReadSubscriptionState() =>
        Application.Invoke<SubscriptionStateRequest, SubscriptionStateView>(
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
