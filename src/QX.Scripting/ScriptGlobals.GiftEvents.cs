using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Raised when the gift wrapping options arrive: whether wrapping is enabled, its price, and
    /// the available box, ribbon and wrapping-paper type ids.
    /// </summary>
    /// <param name="handler">Receives the wrapping configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnGiftWrappingChanged(
        Action<GiftWrappingConfiguration> handler)
        => Subscribe(handler, value => Gifts.WrappingConfigurationChanged += value,
            value => Gifts.WrappingConfigurationChanged -= value);

    /// <summary>
    /// Raised when the club gift catalogue arrives: gifts available now, days until the next one,
    /// the choosable offers and the per-offer eligibility.
    /// </summary>
    /// <param name="handler">Receives the catalogue.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnClubGiftsChanged(Action<ClubGiftInfo> handler)
        => Subscribe(handler, value => Gifts.ClubGiftsChanged += value,
            value => Gifts.ClubGiftsChanged -= value);

    /// <summary>
    /// Raised when the server confirms a chosen club gift, carrying the product code and the
    /// products it granted.
    /// </summary>
    /// <param name="handler">Receives the confirmation.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnClubGiftSelected(Action<ClubGiftSelected> handler)
        => Subscribe(handler, value => Gifts.ClubGiftSelectedReceived += value,
            value => Gifts.ClubGiftSelectedReceived -= value);

    /// <summary>
    /// Raised when a present was opened and its contents are revealed: the item type and class id,
    /// the product code, whether it was placed straight into the room, and the pet figure when the
    /// present held a pet.
    /// </summary>
    /// <param name="handler">Receives the contents.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnPresentOpened(Action<PresentOpened> handler)
        => Subscribe(handler, value => Gifts.PresentOpenedReceived += value,
            value => Gifts.PresentOpenedReceived -= value);

    public IDisposable OnGiftReceiverNotFound(Action handler)
    => Subscribe(handler, value => Gifts.GiftReceiverNotFound += value,
        value => Gifts.GiftReceiverNotFound -= value);

    public IDisposable OnClubGiftNotification(Action<ClubGiftNotification> handler)
    => Subscribe(handler, value => Gifts.ClubGiftNotificationReceived += value,
        value => Gifts.ClubGiftNotificationReceived -= value);

    public IDisposable OnOfferGiftabilityChanged(Action<IsOfferGiftable> handler)
    => Subscribe(handler, value => Gifts.OfferGiftabilityChanged += value,
        value => Gifts.OfferGiftabilityChanged -= value);

    public IDisposable OnNewUserGiftOfferChanged(Action<NuxGiftOffer> handler)
    => Subscribe(handler, value => Gifts.NewUserOfferChanged += value,
        value => Gifts.NewUserOfferChanged -= value);

    /// <summary>
    /// Raised when the hotel reports that the account has not finished the new-user flow.
    /// </summary>
    /// <remarks>
    /// Carries nothing: the notification is the whole message. An established account never sees
    /// it.
    /// </remarks>
    /// <param name="handler">Called when the report arrives.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnNewUserFlowIncomplete(Action handler)
        => Subscribe(handler, value => Gifts.NewUserFlowIncomplete += value,
            value => Gifts.NewUserFlowIncomplete -= value);

    /// <summary>
    /// Raised after the cached gift state was emptied for a new session, which happens on
    /// reconnect. Every gift value is unset and the giftability map is empty by the time the
    /// handler runs.
    /// </summary>
    /// <param name="handler">Invoked with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnGiftsReset(Action handler)
        => Subscribe(handler, value => Gifts.ResetCompleted += value,
            value => Gifts.ResetCompleted -= value);
}
