using Qx.Model.Messages.Incoming;
using Qx;
using Qx.Game.Application;

namespace Qx.Scripting;

/// <content>
/// Gifts, presents and club gifts: cached gift state plus the fire-and-forget requests and actions
/// that drive it.
/// <para>
/// Gift wrapping, present opening, club gifts and gift purchasing work on both the Flash and the
/// Unity client. Four pieces are Flash only, because their payloads are Flash-shaped and the
/// tracker registers them for the Flash client alone: the "recipient does not exist" signal, the
/// club-gift-waiting notification, the per-offer giftability answer, and the new-user gift offer.
/// </para>
/// <para>
/// Nothing here blocks or returns a value. Each request sends one message and returns
/// immediately; the answer surfaces later through the cached state and the gift events. To wait
/// for a specific reply, subscribe first and then send the request.
/// </para>
/// <para>The cached state is cleared when the session resets.</para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// The hotel's gift wrapping options: whether wrapping is enabled, what it costs, and the
    /// available box, ribbon and wrapping-paper type ids. <see langword="null"/> until the
    /// configuration has been requested.
    /// </summary>
    public GiftWrappingConfiguration? GiftWrapping =>
        Gifts.WrappingConfiguration;

    /// <summary>
    /// The club gift catalogue: how many gifts are available now, how many days until the next
    /// one, the offers that can be chosen and the per-offer eligibility. <see langword="null"/>
    /// until it has been requested.
    /// </summary>
    public ClubGiftInfo? ClubGifts => Gifts.ClubGifts;

    /// <summary>
    /// The server's confirmation of the club gift chosen most recently, carrying the product code
    /// and the products it granted. <see langword="null"/> when no club gift was selected this
    /// session.
    /// </summary>
    public ClubGiftSelected? LastSelectedClubGift => Gifts.LastClubGift;

    /// <summary>
    /// What came out of the most recently opened present: the item type and class id, the product
    /// code, whether it was placed straight into the room, and the pet figure when the present
    /// held a pet. <see langword="null"/> when no present was opened this session.
    /// </summary>
    public PresentOpened? LastOpenedPresent => Gifts.LastOpenedPresent;

    /// <summary>
    /// The most recent "you have club gifts waiting" announcement, carrying how many are waiting.
    /// <see langword="null"/> when none has arrived.
    /// </summary>
    /// <remarks>Flash only; this never becomes non-null on a Unity session.</remarks>
    public ClubGiftNotification? LatestClubGiftNotification =>
        Gifts.LatestNotification;

    /// <summary>
    /// The new-user gift offer: the chooseable gift steps of the onboarding flow.
    /// <see langword="null"/> when the server has not offered one.
    /// </summary>
    /// <remarks>Flash only; this never becomes non-null on a Unity session.</remarks>
    public NuxGiftOffer? NewUserGiftOffer => Gifts.NewUserOffer;

    public IReadOnlyDictionary<int, bool> OfferGiftability =>
        Gifts.OfferGiftability;

    /// <summary>
    /// Asks for the gift wrapping options. Returns immediately; the answer lands in the wrapping
    /// state and raises the wrapping event.
    /// </summary>
    public void RequestGiftWrappingConfiguration() =>
        Gifts.RequestWrappingConfiguration();

    /// <summary>
    /// Opens a present standing in the room. Returns immediately; the contents arrive as a
    /// present-opened message.
    /// </summary>
    /// <param name="furni_id">The floor item id of the present in the room.</param>
    public void OpenPresent(Id furni_id) => Gifts.OpenPresent(furni_id);

    /// <summary>
    /// Buys a catalog offer as a gift for another user, with wrapping, a message and an optional
    /// incognito flag. Returns immediately; failure to find the recipient surfaces as the
    /// receiver-not-found event.
    /// </summary>
    /// <param name="request">
    /// The full purchase: catalog page and offer id, extra data, recipient name, gift message, box
    /// and ribbon type, colour, and whether the sender stays anonymous. The quantity field is
    /// Unity only and is filled in with 1 automatically when a Unity session leaves it unset.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public void PurchaseFromCatalogAsGift(
        PurchaseFromCatalogAsGift request) =>
        Gifts.Purchase(request);

    /// <summary>
    /// Asks for the club gift catalogue. Returns immediately; the answer lands in the club-gift
    /// state and raises the club-gift event.
    /// </summary>
    /// <remarks>
    /// The outgoing message differs by client — Flash sends <c>GetClubGift</c>, Unity sends
    /// <c>GetSelectableClubGiftInfo</c> — and the right one is chosen automatically.
    /// </remarks>
    public void RequestClubGifts() => Gifts.RequestClubGifts();

    /// <summary>
    /// Claims one of the available club gifts. Returns immediately; the server confirms with the
    /// club-gift-selected message.
    /// </summary>
    /// <param name="product_code">The product code taken from a club gift offer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="product_code"/> is null.</exception>
    public void SelectClubGift(string product_code) =>
        Gifts.SelectClubGift(product_code);

    /// <summary>
    /// Asks whether one catalog offer may be sent as a gift. Returns immediately; the answer lands
    /// in the giftability map and raises the giftability event.
    /// </summary>
    /// <param name="offer_id">The catalog offer id.</param>
    /// <remarks>Flash only. On a Unity session the answer is never accepted.</remarks>
    public void RequestOfferGiftability(int offer_id) =>
        Gifts.RequestOfferGiftability(offer_id);

    /// <summary>
    /// Submits the choices for the new-user gift flow. Returns immediately.
    /// </summary>
    /// <param name="selections">
    /// One entry per step: the day index, the step index and the index of the chosen gift within
    /// that step, all taken from the new-user gift offer.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="selections"/> is null.</exception>
    /// <remarks>
    /// The outgoing message differs by client — Flash sends <c>NewUserExperienceGetGifts</c>,
    /// Unity sends <c>NuxGetGifts</c> — and the right one is chosen automatically. The offer that
    /// supplies the indices is however only decoded on Flash.
    /// </remarks>
    public void SelectNewUserGifts(
        params NuxGiftSelection[] selections) =>
        Gifts.SelectNewUserGifts(selections);

    /// <summary>
    /// Whether the hotel has said the account still has the new-user flow to finish.
    /// </summary>
    public bool NewUserFlowIsIncomplete => Gifts.NewUserFlowIsIncomplete;

    /// <summary>
    /// Takes the first choice at every step of the new-user gift offer.
    /// </summary>
    /// <remarks>
    /// A convenience over <see cref="SelectNewUserGifts"/> for the common case of not caring which
    /// bundle arrives. Steps with no options are skipped rather than sent as a choice of nothing.
    /// </remarks>
    /// <returns>How many choices were claimed; zero when no offer has arrived.</returns>
    public int SelectFirstNewUserGifts()
    {
        const int page_limit = 500;
        const int maximum_pages = 132;
        GiftStateView state = Application.Invoke<GiftStateRequest, GiftStateView>(
            ApplicationMemberIds.GiftsState,
            new GiftStateRequest(),
            Ct);
        if (state.NewUserOffer is null)
            return 0;
        GiftNewUserOfferPage page = Application.Invoke<
            GiftNewUserOfferPageRequest,
            GiftNewUserOfferPage>(
                ApplicationMemberIds.GiftsNewUserOfferList,
                new GiftNewUserOfferPageRequest(Limit: page_limit),
                Ct);
        if (!page.Loaded)
            return 0;
        if (!page.Connected ||
            page.Client is null ||
            page.Client != state.Client ||
            page.SessionGeneration != state.SessionGeneration ||
            page.NewUserOfferRevision != state.NewUserOfferRevision ||
            page.SessionGeneration <= 0 ||
            page.NewUserOfferRevision <= 0 ||
            page.SnapshotRevision <= 0 ||
            page.TotalSteps < 0 ||
            page.TotalOptions < 0 ||
            page.TotalProducts < 0 ||
            page.TotalSteps != state.NewUserOffer.StepCount ||
            page.TotalOptions != state.NewUserOffer.OptionCount ||
            page.TotalProducts != state.NewUserOffer.ProductCount ||
            page.Total != page.TotalSteps ||
            page.Offset != 0 ||
            page.Collection is not GiftNewUserOfferCollection.Steps)
        {
            throw new InvalidDataException("New-user gift pagination returned invalid metadata.");
        }

        ClientType client = page.Client.Value;
        long session_generation = page.SessionGeneration;
        long offer_revision = page.NewUserOfferRevision;
        long snapshot_revision = page.SnapshotRevision;
        int total_steps = page.TotalSteps;
        int total_options = page.TotalOptions;
        int total_products = page.TotalProducts;
        int expected_offset = 0;
        int expected_step_ordinal = 0;
        var selections = new List<NuxGiftSelection>();

        for (int page_number = 0; page_number < maximum_pages; page_number++)
        {
            if (!page.Connected ||
                page.Client != client ||
                page.SessionGeneration != session_generation ||
                page.NewUserOfferRevision != offer_revision ||
                page.SnapshotRevision != snapshot_revision ||
                !page.Loaded ||
                page.TotalSteps != total_steps ||
                page.TotalOptions != total_options ||
                page.TotalProducts != total_products ||
                page.Total != total_steps ||
                page.Collection is not GiftNewUserOfferCollection.Steps ||
                page.Offset != expected_offset ||
                page.Steps is null ||
                page.Steps.Count > page_limit ||
                (long)page.Offset + page.Steps.Count > total_steps)
            {
                throw new InvalidDataException(
                    "New-user gift offer changed while its steps were being collected.");
            }

            foreach (GiftNewUserStepView step in page.Steps)
            {
                if (step is null ||
                    step.StepOrdinal != expected_step_ordinal++ ||
                    step.OptionCount < 0)
                {
                    throw new InvalidDataException(
                        "New-user gift pagination returned an invalid step.");
                }
                if (step.OptionCount > 0)
                {
                    selections.Add(new NuxGiftSelection(
                        step.DayIndex,
                        step.StepIndex,
                        0));
                }
            }

            int consumed = checked(page.Offset + page.Steps.Count);
            if (page.NextOffset is not int next_offset)
            {
                if (consumed != total_steps || expected_step_ordinal != total_steps)
                {
                    throw new InvalidDataException(
                        "New-user gift pagination returned an incomplete result.");
                }
                break;
            }
            if (page.Steps.Count == 0 ||
                next_offset != consumed ||
                next_offset >= total_steps ||
                page_number == maximum_pages - 1)
            {
                throw new InvalidDataException(
                    "New-user gift pagination returned an invalid continuation.");
            }

            expected_offset = next_offset;
            page = Application.Invoke<
                GiftNewUserOfferPageRequest,
                GiftNewUserOfferPage>(
                    ApplicationMemberIds.GiftsNewUserOfferList,
                    new GiftNewUserOfferPageRequest(
                        GiftNewUserOfferCollection.Steps,
                        expected_offset,
                        page_limit,
                        snapshot_revision),
                    Ct);
        }

        if (selections.Count == 0)
            return 0;
        Application.Invoke<GiftNewUserSelectRequest, GiftNewUserSelectDispatchReceipt>(
            ApplicationMemberIds.GiftsNewUserSelect,
            new GiftNewUserSelectRequest(
                Array.AsReadOnly(selections.ToArray()),
                session_generation,
                offer_revision),
            Ct);
        return selections.Count;
    }

    /// <summary>Tells the hotel to advance the new-user script to its next step.</summary>
    public void AdvanceNewUserFlow() => Gifts.AdvanceNewUserFlow();
}
