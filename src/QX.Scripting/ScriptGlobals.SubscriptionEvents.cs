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
    /// Raised when the details of one subscription product arrive: days left in the period,
    /// periods held and paid ahead, the VIP flag, past club and VIP days, and the minutes until
    /// expiry.
    /// </summary>
    /// <param name="handler">Receives the details, which carry their own product name.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionInfoChanged(Action<ScrSendUserInfo> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
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
    /// Raised when the Habbo Club kickback summary arrives: streak length, kickback percentage,
    /// credits spent, missed and rewarded, and the time until the next payday.
    /// </summary>
    /// <param name="handler">Receives the summary.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionKickbackChanged(
        Action<ScrSendKickbackInfo> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
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
    /// Raised when the server reports how many Builders Club furniture the local user has placed.
    /// </summary>
    /// <param name="handler">Receives the count.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnBuildersClubFurniCountChanged(
        Action<BuildersClubFurniCount> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
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
    /// Raised when the Builders Club membership status changes: seconds of membership left, the
    /// current and maximum furniture limits, and the grace period when the server sends one.
    /// </summary>
    /// <param name="handler">Receives the status.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>Flash only; never fires on a Unity session.</remarks>
    public IDisposable OnBuildersClubStatusChanged(
        Action<BuildersClubMembershipStatus> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
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
    /// Raised when the server warns that a Builders Club placement would push the user over the
    /// furniture limit, carrying the catalog page and offer plus the floor or wall position that
    /// was attempted.
    /// </summary>
    /// <param name="handler">Receives the warning.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    /// <remarks>Flash only; never fires on a Unity session.</remarks>
    public IDisposable OnBuildersClubPlacementWarning(
        Action<BuildersClubPlacementWarning> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
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
    /// Raised after the cached subscription state was emptied for a new session, which happens on
    /// reconnect. The subscription map is empty and every other value unset by the time the
    /// handler runs.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnSubscriptionsReset(Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(Application.Subscribe<SubscriptionChanged>(
            ApplicationMemberIds.SubscriptionsChanged,
            Guarded<SubscriptionChanged>(change =>
            {
                if (change.Kind is SubscriptionChangeKind.Reset && change.Client is null)
                    handler();
            })));
    }
}
