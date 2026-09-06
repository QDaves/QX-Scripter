using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Model.Messages.Outgoing;

namespace Qx.Scripting;

/// <content>
/// Buying from the catalog, and what it costs before you do.
/// <para>
/// <b>Two currencies.</b> An offer can charge credits, an activity currency, or both at once, and
/// the activity currency is identified per offer by its type rather than being one fixed pool. Any
/// affordability check therefore has to look at both sides of the price, which is what
/// <see cref="CanAfford"/> does.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>Catalog purchase outcomes and the republish signal.</summary>
    public CatalogManager Catalog => Game.Catalog;

    /// <summary>The most recent purchase outcome, or <see langword="null"/> before the first.</summary>
    public CatalogPurchaseOutcome? LastPurchase => Game.Catalog.LastPurchase;

    /// <summary>
    /// What an offer costs in the activity currency it charges, or zero when it charges none.
    /// </summary>
    /// <param name="offer">The catalog offer.</param>
    public int ActivityPointPrice(PurchaseOffer offer) => offer.PriceInActivityPoints;

    /// <summary>
    /// The balance the local user holds in the currency a given offer charges.
    /// </summary>
    /// <remarks>
    /// Activity currencies are per type - duckets are type 0 and diamonds type 5 - and an offer
    /// names the type it wants, so the balance has to be looked up per offer rather than read from
    /// one fixed property.
    /// </remarks>
    /// <param name="offer">The catalog offer.</param>
    public int ActivityPointBalance(PurchaseOffer offer) => ReadWalletPoint(offer.ActivityPointType);

    /// <summary>
    /// Whether the local user can currently pay for an offer.
    /// </summary>
    /// <param name="offer">The catalog offer.</param>
    /// <param name="quantity">How many to price, for bundles bought in multiples.</param>
    public bool CanAfford(PurchaseOffer offer, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        WalletStateView state = ReadWalletState(offer.ActivityPointType);
        long credit_price = (long)offer.PriceInCredits * quantity;
        long point_price = (long)offer.PriceInActivityPoints * quantity;
        return (state.Credits ?? 0) >= credit_price &&
            WalletPoint(state, offer.ActivityPointType) >= point_price;
    }

    /// <remarks>
    /// The page and offer identifiers come from a catalog page; load one with the catalog request
    /// helpers first. <paramref name="extraData"/> carries the per-offer selection the hotel expects
    /// - a pet's name and colour, a badge code, a wallpaper variant - and is empty for a plain
    /// furni offer.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="quantity">How many to buy.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyFromCatalog(
        int pageId,
        int offerId,
        string extraData = "",
        int quantity = 1,
        int timeoutMs = 10000) =>
        DispatchCatalogPurchase(pageId, offerId, extraData, quantity, timeoutMs);

    /// <remarks>
    /// The wrapping is part of the purchase, not a later step: box, ribbon and colour are chosen
    /// here. Not every offer may be gifted - check <c>IsOfferGiftable</c> first, or read
    /// <see cref="PurchaseOffer.Giftable"/> on a loaded offer.
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="receiverName">Who receives the gift.</param>
    /// <param name="message">The note that comes with it.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="boxType">Which box the gift is wrapped in.</param>
    /// <param name="ribbonType">Which ribbon the box carries.</param>
    /// <param name="color">The wrapping colour.</param>
    /// <param name="anonymous">Whether the sender stays hidden.</param>
    /// <param name="timeoutMs">How long to wait for the purchase result, in milliseconds.</param>
    public Task<CatalogPurchaseOutcome> BuyGiftFromCatalog(
        int pageId,
        int offerId,
        string receiverName,
        string message = "",
        string extraData = "",
        int boxType = 0,
        int ribbonType = 0,
        int color = 0,
        bool anonymous = false,
        int timeoutMs = 10000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiverName);
        return DispatchGiftPurchase(
            pageId,
            offerId,
            receiverName,
            message,
            extraData,
            boxType,
            ribbonType,
            color,
            anonymous,
            timeoutMs);
    }

    private async Task<CatalogPurchaseOutcome> DispatchGiftPurchase(
        int page_id,
        int offer_id,
        string receiver_name,
        string message,
        string extra_data,
        int box_type,
        int ribbon_type,
        int color,
        bool anonymous,
        int timeout_ms)
    {
        _ = timeout_ms;
        _ = await Application
            .InvokeAsync<GiftPurchaseRequest, GiftPurchaseDispatchReceipt>(
                ApplicationMemberIds.GiftsPurchase,
                new GiftPurchaseRequest(
                    page_id,
                    offer_id,
                    extra_data,
                    receiver_name,
                    message,
                    box_type,
                    ribbon_type,
                    color,
                    !anonymous),
                Ct)
            .ConfigureAwait(false);
        return new CatalogPurchaseOutcome(CatalogPurchaseStatus.Dispatched, null, 0);
    }

    /// <summary>
    /// Reads the membership offers the hotel sells, and how much membership the account already
    /// holds.
    /// </summary>
    /// <remarks>
    /// Each offer prices in credits and optionally in an activity currency, and carries the expiry
    /// the account would reach if it were bought, which is what makes it worth reading before a
    /// purchase rather than after.
    /// </remarks>
    /// <param name="offerType">Which set of offers to list.</param>
    /// <param name="timeoutMs">Total budget in milliseconds.</param>
    public async Task<HabboClubOffers> GetClubOffers(
        int offerType = 1,
        int timeoutMs = 10000)
    {
        const int page_limit = 500;
        const int maximum_pages = 132;
        SubscriptionClubOffersPage page = await Application
            .InvokeAsync<SubscriptionClubOffersRefreshRequest, SubscriptionClubOffersPage>(
                ApplicationMemberIds.SubscriptionsClubOffersRefresh,
                new SubscriptionClubOffersRefreshRequest(
                    offerType,
                    page_limit,
                    timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (!page.Connected ||
            page.Client is null || !ClientTypes.IsSupported(page.Client.Value) ||
            page.SessionGeneration <= 0 ||
            page.Revision <= 0 ||
            page.ClubOffersRevision <= 0 ||
            page.SnapshotRevision <= 0 ||
            !page.Loaded ||
            page.DaysLeft is not int days_left ||
            page.TotalOffers is < 0 or > ushort.MaxValue ||
            page.Offset != 0)
        {
            throw new InvalidDataException("Club-offer refresh returned invalid metadata.");
        }

        ClientType client = page.Client.Value;
        long session_generation = page.SessionGeneration;
        long revision = page.Revision;
        long club_offers_revision = page.ClubOffersRevision;
        long snapshot_revision = page.SnapshotRevision;
        int total_offers = page.TotalOffers;
        var offers = new List<HabboClubOffer>(total_offers);
        int expected_offset = 0;

        for (int page_number = 0; page_number < maximum_pages; page_number++)
        {
            if (!page.Connected ||
                page.Client != client ||
                page.SessionGeneration != session_generation ||
                page.Revision != revision ||
                page.ClubOffersRevision != club_offers_revision ||
                page.SnapshotRevision != snapshot_revision ||
                !page.Loaded ||
                page.DaysLeft != days_left ||
                page.TotalOffers != total_offers ||
                page.Offset != expected_offset ||
                page.Offers is null ||
                page.Offers.Count > page_limit ||
                (long)page.Offset + page.Offers.Count > total_offers)
            {
                throw new InvalidDataException(
                    "Club offers changed while the result was being collected.");
            }

            foreach (SubscriptionClubOfferView offer in page.Offers)
            {
                if (offer is null || offer.ProductCode is null)
                {
                    throw new InvalidDataException(
                        "Club-offer pagination returned an invalid offer.");
                }
                offers.Add(new HabboClubOffer(
                    offer.OfferId,
                    offer.ProductCode,
                    offer.PriceCredits,
                    offer.PriceActivityPoints,
                    offer.PriceActivityPointType,
                    offer.IsVip,
                    offer.Months,
                    offer.ExtraDays,
                    offer.IsGiftable,
                    offer.DaysLeftAfterPurchase,
                    offer.Year,
                    offer.Month,
                    offer.Day)
                {
                    ReservedWireFlag = offer.ReservedWireFlag
                });
            }

            int consumed = checked(page.Offset + page.Offers.Count);
            if (page.NextOffset is not int next_offset)
            {
                if (consumed != total_offers)
                {
                    throw new InvalidDataException(
                        "Club-offer pagination returned an incomplete result.");
                }
                if (offers.Count != total_offers)
                {
                    throw new InvalidDataException(
                        "Club-offer pagination returned an invalid final count.");
                }
                return new HabboClubOffers(Array.AsReadOnly(offers.ToArray()), days_left);
            }
            if (page.Offers.Count == 0 ||
                next_offset != consumed ||
                next_offset >= total_offers ||
                page_number == maximum_pages - 1)
            {
                throw new InvalidDataException(
                    "Club-offer pagination returned an invalid continuation.");
            }

            expected_offset = next_offset;
            page = Application.Invoke<
                SubscriptionClubOffersPageRequest,
                SubscriptionClubOffersPage>(
                    ApplicationMemberIds.SubscriptionsClubOffersList,
                    new SubscriptionClubOffersPageRequest(
                        expected_offset,
                        page_limit,
                        snapshot_revision),
                    Ct);
        }

        throw new InvalidDataException("Club-offer pagination exceeded the wire maximum.");
    }

    /// <summary>
    /// Subscribes to every catalog purchase the hotel answers, including refusals.
    /// </summary>
    /// <param name="handler">Receives the outcome.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnPurchase(Action<CatalogPurchaseOutcome> handler)
        => Subscribe(
            handler,
            value => Game.Catalog.PurchaseAnswered += value,
            value => Game.Catalog.PurchaseAnswered -= value);

    /// <summary>
    /// Subscribes to the hotel republishing its catalog, which invalidates any page already loaded.
    /// </summary>
    /// <param name="handler">Receives the new catalog identifier.</param>
    /// <returns>A handle that unsubscribes when disposed; also disposed when the script stops.</returns>
    public IDisposable OnCatalogPublished(Action<CatalogPublished> handler)
    {
        return Track(Application.Subscribe<CatalogPublishedEvent>(
            ApplicationMemberIds.CatalogPublished,
            Guarded<CatalogPublishedEvent>(publication => handler(publication.Publication))));
    }

    private async Task<CatalogPurchaseOutcome> DispatchCatalogPurchase(
        int page_id,
        int offer_id,
        string extra_data,
        int quantity,
        int timeout_ms)
    {
        _ = timeout_ms;
        _ = await Application
            .InvokeAsync<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
                ApplicationMemberIds.CatalogPurchaseSend,
                new CatalogPurchaseSendRequest(page_id, offer_id, extra_data, quantity),
                Ct)
            .ConfigureAwait(false);
        return new CatalogPurchaseOutcome(CatalogPurchaseStatus.Dispatched, null, 0);
    }

}
