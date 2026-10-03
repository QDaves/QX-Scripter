using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Registers a handler that runs when the gift wrapping options arrive.
    /// </summary>
    /// <remarks>
    /// The options hold whether wrapping is enabled, its price, and the available box, ribbon and
    /// wrapping paper type ids.
    /// </remarks>
    /// <param name="handler">The handler to call with the wrapping configuration.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnGiftWrappingChanged(
        Action<GiftWrappingConfiguration> handler)
        => Subscribe(handler, value => Gifts.WrappingConfigurationChanged += value,
            value => Gifts.WrappingConfigurationChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the club gift catalogue arrives.
    /// </summary>
    /// <remarks>
    /// The catalogue holds the gifts available now, the days until the next one, the offers that
    /// can be chosen and the eligibility of each offer.
    /// </remarks>
    /// <param name="handler">The handler to call with the catalogue.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnClubGiftsChanged(Action<ClubGiftInfo> handler)
        => Subscribe(handler, value => Gifts.ClubGiftsChanged += value,
            value => Gifts.ClubGiftsChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the server confirms a chosen club gift.
    /// </summary>
    /// <param name="handler">The handler to call with the confirmation, which carries the product code and the products it granted.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnClubGiftSelected(Action<ClubGiftSelected> handler)
        => Subscribe(handler, value => Gifts.ClubGiftSelectedReceived += value,
            value => Gifts.ClubGiftSelectedReceived -= value);

    /// <summary>
    /// Registers a handler that runs when a present was opened and its contents are revealed.
    /// </summary>
    /// <remarks>
    /// The contents hold the item type and class id, the product code, whether it was placed
    /// straight into the room, and the pet figure when the present held a pet.
    /// </remarks>
    /// <param name="handler">The handler to call with the contents.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnPresentOpened(Action<PresentOpened> handler)
        => Subscribe(handler, value => Gifts.PresentOpenedReceived += value,
            value => Gifts.PresentOpenedReceived -= value);

    /// <summary>
    /// Registers a handler that runs when the hotel reports that the receiver of a gift does not
    /// exist.
    /// </summary>
    /// <param name="handler">The handler to call with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnGiftReceiverNotFound(Action handler)
    => Subscribe(handler, value => Gifts.GiftReceiverNotFound += value,
        value => Gifts.GiftReceiverNotFound -= value);

    /// <summary>
    /// Registers a handler that runs when a club gift notification arrives.
    /// </summary>
    /// <param name="handler">The handler to call with the notification.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnClubGiftNotification(Action<ClubGiftNotification> handler)
    => Subscribe(handler, value => Gifts.ClubGiftNotificationReceived += value,
        value => Gifts.ClubGiftNotificationReceived -= value);

    /// <summary>
    /// Registers a handler that runs when the hotel answers whether an offer can be gifted.
    /// </summary>
    /// <param name="handler">The handler to call with the answer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnOfferGiftabilityChanged(Action<IsOfferGiftable> handler)
    => Subscribe(handler, value => Gifts.OfferGiftabilityChanged += value,
        value => Gifts.OfferGiftabilityChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the new user gift offer arrives.
    /// </summary>
    /// <param name="handler">The handler to call with the offer.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnNewUserGiftOfferChanged(Action<NuxGiftOffer> handler)
    => Subscribe(handler, value => Gifts.NewUserOfferChanged += value,
        value => Gifts.NewUserOfferChanged -= value);

    /// <summary>
    /// Registers a handler that runs when the hotel reports that the account has not finished the
    /// new user flow.
    /// </summary>
    /// <remarks>
    /// The report carries nothing: the notification is the whole message. An established account
    /// never sees it.
    /// </remarks>
    /// <param name="handler">The handler to call when the report arrives.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnNewUserFlowIncomplete(Action handler)
        => Subscribe(handler, value => Gifts.NewUserFlowIncomplete += value,
            value => Gifts.NewUserFlowIncomplete -= value);

    /// <summary>
    /// Registers a handler that runs after the cached gift state has been emptied.
    /// </summary>
    /// <remarks>
    /// The state is emptied when the hotel connection closes. Every gift value is unset and the
    /// giftability map is empty by the time the handler runs.
    /// </remarks>
    /// <param name="handler">The handler to call with no arguments.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnGiftsReset(Action handler)
        => Subscribe(handler, value => Gifts.ResetCompleted += value,
            value => Gifts.ResetCompleted -= value);
}
