using Qx.Model.Messages.Incoming;
using Qx;
using Qx.Game.Application;

namespace Qx.Scripting;

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

    public ClubGiftNotification? LatestClubGiftNotification =>
    Gifts.LatestNotification;

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

    public void PurchaseFromCatalogAsGift(
    PurchaseFromCatalogAsGift request) =>
    Gifts.Purchase(request);

    public void RequestClubGifts() => Gifts.RequestClubGifts();

    /// <summary>
    /// Claims one of the available club gifts. Returns immediately; the server confirms with the
    /// club-gift-selected message.
    /// </summary>
    /// <param name="product_code">The product code taken from a club gift offer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="product_code"/> is null.</exception>
    public void SelectClubGift(string product_code) =>
        Gifts.SelectClubGift(product_code);

    public void RequestOfferGiftability(int offer_id) =>
    Gifts.RequestOfferGiftability(offer_id);

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
