using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Subscription event subscriptions.
/// <para>
/// Every <c>On*</c> method registers a handler and returns the handle that removes it again. The
/// subscription is also tracked by the script and torn down when the script stops, so the handle
/// only has to be kept when the script wants to unsubscribe earlier. Disposing it more than once
/// is harmless.
/// </para>
/// <para>
/// Handlers run inline on the interception thread while the triggering packet is dispatched, not
/// on the script thread, and after the cached subscription state has already been updated. Keep
/// them short and do not block inside them.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs when the details of one subscription product arrive.
    /// </summary>
    /// <remarks>
    /// The details hold the days left in the period, the periods held and paid ahead, the VIP
    /// flag, past club and VIP days, and the minutes until expiry.
    /// </remarks>
    /// <param name="handler">The handler to call with the details, which carry their own product name.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionInfoChanged(Action<ScrSendUserInfo> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.UserInfo &&
                    change.Product is { } product)
                {
                    handler(LegacySubscriptionProduct(product));
                }
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the Habbo Club kickback summary arrives.
    /// </summary>
    /// <remarks>
    /// The summary holds the streak length, the kickback percentage, the credits spent, missed and
    /// rewarded, and the time until the next payday.
    /// </remarks>
    /// <param name="handler">The handler to call with the summary.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionKickbackChanged(
        Action<ScrSendKickbackInfo> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.KickbackInfo &&
                    change.Kickback is { } kickback)
                {
                    handler(LegacySubscriptionKickback(kickback));
                }
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the server reports how many Builders Club furniture the
    /// local user has placed.
    /// </summary>
    /// <param name="handler">The handler to call with the count.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnBuildersClubFurniCountChanged(
        Action<BuildersClubFurniCount> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.BuildersClubFurniCount &&
                    change.BuildersClubFurniCount is int furni_count)
                {
                    handler(new BuildersClubFurniCount(furni_count));
                }
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the server sends the Builders Club membership status.
    /// </summary>
    /// <remarks>
    /// The status holds the seconds left, the furni limit, the maximum furni limit and, when the
    /// server sends it, the seconds left including the grace period.
    /// </remarks>
    /// <param name="handler">The handler to call with the membership status.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnBuildersClubStatusChanged(
    Action<BuildersClubMembershipStatus> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.BuildersClubMembershipStatus &&
                    change.BuildersClubMembership is { } membership)
                {
                    handler(LegacySubscriptionMembership(membership));
                }
            })));
    }

    /// <summary>
    /// Registers a handler that runs when the server sends a Builders Club placement warning.
    /// </summary>
    /// <remarks>
    /// The warning names the catalog page, offer and extra parameter of the placement, and its
    /// floor tile and direction or its wall location.
    /// </remarks>
    /// <param name="handler">The handler to call with the warning.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnBuildersClubPlacementWarning(
    Action<BuildersClubPlacementWarning> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.BuildersClubPlacementWarning &&
                    change.PlacementWarning is { } warning)
                {
                    handler(LegacySubscriptionPlacementWarning(warning));
                }
            })));
    }

    /// <summary>
    /// Registers a handler that runs after the cached subscription state was emptied.
    /// </summary>
    /// <remarks>
    /// It only runs for a reset that leaves no hotel session bound, which happens when the session
    /// ends; the reset for a newly connected session does not call it. The subscription map is
    /// empty and every other value unset by the time the handler runs.
    /// </remarks>
    /// <param name="handler">The handler to call, with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionsReset(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.Reset && !change.Connected)
                    handler();
            })));
    }
}
