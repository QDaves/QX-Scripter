using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;
using Qx;
using Qx.Game.Application;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the hotel's gift wrapping options, or <see langword="null"/> until the configuration
    /// has been received.
    /// </summary>
    /// <remarks>
    /// The options hold whether wrapping is enabled, what it costs, and the available box, ribbon
    /// and wrapping paper type ids.
    /// </remarks>
    public GiftWrappingConfiguration? GiftWrapping =>
        Gifts.WrappingConfiguration;

    /// <summary>
    /// Gets the club gift catalogue, or <see langword="null"/> until it has been received.
    /// </summary>
    /// <remarks>
    /// It holds how many gifts are available now, how many days until the next one, the offers
    /// that can be chosen and the eligibility of each offer.
    /// </remarks>
    public ClubGiftInfo? ClubGifts => Gifts.ClubGifts;

    /// <summary>
    /// Gets the server's confirmation of the club gift chosen most recently, or
    /// <see langword="null"/> when no club gift was selected this session.
    /// </summary>
    /// <remarks>
    /// The confirmation carries the product code and the products it granted.
    /// </remarks>
    public ClubGiftSelected? LastSelectedClubGift => Gifts.LastClubGift;

    /// <summary>
    /// Gets the contents of the most recently opened present, or <see langword="null"/> when no
    /// present was opened this session.
    /// </summary>
    /// <remarks>
    /// The contents hold the item type and class id, the product code, whether it was placed
    /// straight into the room, and the pet figure when the present held a pet.
    /// </remarks>
    public PresentOpened? LastOpenedPresent => Gifts.LastOpenedPresent;

    /// <summary>
    /// Gets the last club gift notification, or <see langword="null"/> when none has arrived.
    /// </summary>
    public ClubGiftNotification? LatestClubGiftNotification =>
    Gifts.LatestNotification;

    /// <summary>
    /// Gets the new user gift offer, or <see langword="null"/> when it has not been received.
    /// </summary>
    public NuxGiftOffer? NewUserGiftOffer => Gifts.NewUserOffer;

    /// <summary>
    /// Gets whether each catalog offer can be sent as a gift, keyed by offer id.
    /// </summary>
    /// <remarks>
    /// It holds the answers to <see cref="RequestOfferGiftability(int)"/> for up to 500 offers;
    /// the oldest answer is dropped when a new offer would exceed the limit. Every read returns a new
    /// copy.
    /// </remarks>
    public IReadOnlyDictionary<int, bool> OfferGiftability =>
        Gifts.OfferGiftability;

    /// <summary>
    /// Asks for the gift wrapping options.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the answer lands in <see cref="GiftWrapping"/> and runs the
    /// <see cref="OnGiftWrappingChanged(Action{GiftWrappingConfiguration})"/> handlers.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestGiftWrappingConfiguration() =>
        Gifts.RequestWrappingConfiguration();

    /// <summary>
    /// Opens a present standing in the current room.
    /// </summary>
    /// <remarks>
    /// It returns immediately. The user must be in a room that has finished loading; the contents
    /// arrive through <see cref="OnPresentOpened(Action{PresentOpened})"/> and
    /// <see cref="LastOpenedPresent"/>.
    /// </remarks>
    /// <param name="furniId">The floor item id of the present in the room.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="furniId"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session or no ready room.</exception>
    public void OpenPresent(Id furniId) => Gifts.OpenPresent(furniId);

    /// <summary>
    /// Asks for the club gift catalogue.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the answer lands in <see cref="ClubGifts"/> and runs the
    /// <see cref="OnClubGiftsChanged(Action{ClubGiftInfo})"/> handlers.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestClubGifts() => Gifts.RequestClubGifts();

    /// <summary>
    /// Claims one of the available club gifts.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the server confirms through
    /// <see cref="OnClubGiftSelected(Action{ClubGiftSelected})"/> and
    /// <see cref="LastSelectedClubGift"/>.
    /// </remarks>
    /// <param name="productCode">The product code taken from a club gift offer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="productCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void SelectClubGift(string productCode) =>
        Gifts.SelectClubGift(productCode);

    /// <summary>
    /// Asks whether a catalog offer can be sent as a gift.
    /// </summary>
    /// <remarks>
    /// It returns immediately; the answer lands in <see cref="OfferGiftability"/> and runs the
    /// <see cref="OnOfferGiftabilityChanged(Action{IsOfferGiftable})"/> handlers.
    /// </remarks>
    /// <param name="offerId">The id of the catalog offer.</param>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void RequestOfferGiftability(int offerId) =>
    Gifts.RequestOfferGiftability(offerId);

    /// <summary>
    /// Selects gifts from the new user gift offer.
    /// </summary>
    /// <remarks>
    /// It returns immediately. A new user gift offer must have been received in the current
    /// session.
    /// </remarks>
    /// <param name="selections">The chosen gift for each day and step.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selections"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when there are more than 21845 selections.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session or no new user gift offer.</exception>
    public void SelectNewUserGifts(
    params NuxGiftSelection[] selections) =>
    Gifts.SelectNewUserGifts(selections);

    /// <summary>
    /// Gets whether the hotel has said the account still has the new user flow to finish.
    /// </summary>
    public bool NewUserFlowIsIncomplete => Gifts.NewUserFlowIsIncomplete;

    /// <summary>
    /// Takes the first choice at every step of the new user gift offer.
    /// </summary>
    /// <remarks>
    /// A convenience over <see cref="SelectNewUserGifts"/> for the common case of not caring which
    /// bundle arrives. Steps with no options are skipped rather than sent as a choice of nothing.
    /// The selection is sent without waiting for an answer.
    /// </remarks>
    /// <returns>The number of choices claimed; zero when no offer has arrived or no step has options.</returns>
    /// <exception cref="InvalidDataException">Thrown when the offer changed, or its pages were inconsistent, while the steps were being collected.</exception>
    public int SelectFirstNewUserGifts()
    {
        const int page_limit = 500;
        const int maximum_pages = 132;
        GiftStateView state = _application.Invoke<GiftStateRequest, GiftStateView>(
            ApplicationMemberIds.GiftsState,
            new GiftStateRequest(),
            Ct);
        if (state.NewUserOffer is null)
            return 0;
        GiftNewUserOfferPage page = _application.Invoke<
            GiftNewUserOfferPageRequest,
            GiftNewUserOfferPage>(
                ApplicationMemberIds.GiftsNewUserOfferList,
                new GiftNewUserOfferPageRequest(Limit: page_limit),
                Ct);
        if (!page.Loaded)
            return 0;
        if (!page.Connected ||
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
            page = _application.Invoke<
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
        _application.Invoke<GiftNewUserSelectRequest, GiftNewUserSelectDispatchReceipt>(
            ApplicationMemberIds.GiftsNewUserSelect,
            new GiftNewUserSelectRequest(
                Array.AsReadOnly(selections.ToArray()),
                session_generation,
                offer_revision),
            Ct);
        return selections.Count;
    }

    /// <summary>Tells the hotel to advance the new user flow to its next step.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the application runtime is not active or there is no hotel session.</exception>
    public void AdvanceNewUserFlow() => Gifts.AdvanceNewUserFlow();
}
