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
/// <see cref="CanAfford(CatalogPageOffer, int)"/> does.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>Gets the catalog manager that holds the page cache and the purchase outcomes.</summary>
    public CatalogManager Catalog => Game.Catalog;

    /// <summary>
    /// Gets the last purchase outcome the server sent in the current session, or
    /// <see langword="null"/> when no answer has been received.
    /// </summary>
    public CatalogPurchaseOutcome? LastPurchase => Game.Catalog.LastPurchase;

    /// <summary>
    /// Gets what an offer costs in the activity currency it charges.
    /// </summary>
    /// <param name="offer">The catalog offer.</param>
    /// <returns>The activity currency price, or zero when the offer charges none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offer"/> is <see langword="null"/>.</exception>
    public int ActivityPointPrice(PurchaseOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return offer.PriceInActivityPoints;
    }

    /// <summary>
    /// Gets the balance the local user holds in the activity currency a given offer charges.
    /// </summary>
    /// <remarks>
    /// Activity currencies are per type (duckets are type 0 and diamonds type 5) and an offer
    /// names the type it wants, so the balance has to be looked up per offer rather than read from
    /// one fixed property.
    /// </remarks>
    /// <param name="offer">The catalog offer.</param>
    /// <returns>
    /// The balance of that currency type, or zero when the wallet is loaded but holds none of it.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the activity point balances have not been loaded yet.</exception>
    public int ActivityPointBalance(PurchaseOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return RequireWalletPoint(offer.ActivityPointType);
    }

    /// <summary>
    /// Gets the balance the local user holds in the activity currency a catalog page offer charges.
    /// </summary>
    /// <remarks>
    /// Activity currencies are per type (duckets are type 0 and diamonds type 5) and an offer
    /// names the type it wants, so the balance has to be looked up per offer rather than read from
    /// one fixed property. Silver is not tracked by the wallet, so this says nothing about
    /// <see cref="CatalogPageOffer.PriceInSilver"/>.
    /// </remarks>
    /// <param name="offer">The offer from a loaded catalog page.</param>
    /// <returns>
    /// The balance of that currency type, or zero when the wallet is loaded but holds none of it.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the activity point balances have not been loaded yet.</exception>
    public int ActivityPointBalance(CatalogPageOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return RequireWalletPoint(offer.ActivityPointType);
    }

    /// <summary>
    /// Gets whether the local user can currently pay for an offer.
    /// </summary>
    /// <remarks>
    /// Both the credit price and the activity currency price, multiplied by
    /// <paramref name="quantity"/>, are compared with the wallet. Credits that have not been
    /// loaded count as zero.
    /// </remarks>
    /// <param name="offer">The catalog offer.</param>
    /// <param name="quantity">The number of items to price, for bundles bought in multiples.</param>
    /// <returns>
    /// <see langword="true"/> when the wallet covers both prices; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quantity"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the activity point balances have not arrived and the offer costs activity points.</exception>
    public bool CanAfford(PurchaseOffer offer, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return WalletCovers(
            offer.PriceInCredits,
            offer.PriceInActivityPoints,
            offer.ActivityPointType,
            quantity);
    }

    /// <summary>
    /// Gets whether the local user can currently pay for an offer from a loaded catalog page.
    /// </summary>
    /// <remarks>
    /// This is the check to make before buying, because the offers on a page from
    /// <see cref="GetCatalogPage"/> and in a match from <see cref="FindCatalogOffers"/> have this
    /// type. Both the credit price and the activity currency price, multiplied by
    /// <paramref name="quantity"/>, are compared with the wallet. Credits that have not been
    /// loaded count as zero. Silver is not tracked by the wallet, so
    /// <see cref="CatalogPageOffer.PriceInSilver"/> is not checked.
    /// </remarks>
    /// <param name="offer">The offer from a loaded catalog page.</param>
    /// <param name="quantity">The number of items to price, for bundles bought in multiples.</param>
    /// <returns>
    /// <see langword="true"/> when the wallet covers both prices; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="offer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="quantity"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the activity point balances have not arrived and the offer costs activity points.</exception>
    public bool CanAfford(CatalogPageOffer offer, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return WalletCovers(
            offer.PriceInCredits,
            offer.PriceInActivityPoints,
            offer.ActivityPointType,
            quantity);
    }

    private bool WalletCovers(int credit_price, int point_price, int point_type, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        WalletStateView state = ReadWalletState(point_type);
        return (state.Credits ?? 0) >= (long)credit_price * quantity &&
            (point_price == 0 || RequireWalletPoint(state, point_type) >= (long)point_price * quantity);
    }

    /// <summary>
    /// Sends a catalog purchase for an offer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The page and offer identifiers come from a catalog page; load one with the catalog request
    /// helpers first. <paramref name="extraData"/> carries the per-offer selection the hotel
    /// expects, such as a pet's name and color, a badge code or a wallpaper variant, and is empty
    /// for a plain furni offer.
    /// </para>
    /// <para>
    /// It returns once the request is sent and does not wait for the server's answer. Check
    /// <see cref="CanAfford(CatalogPageOffer, int)"/> before buying, and observe the answer with
    /// <see cref="OnPurchase"/> or <see cref="LastPurchase"/>.
    /// </para>
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="quantity">The number of items to buy.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="extraData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageId"/> or <paramref name="offerId"/> is negative, <paramref name="quantity"/>
    /// is zero or negative, or <paramref name="extraData"/> is too long for the wire.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyFromCatalog(
        int pageId,
        int offerId,
        string extraData = "",
        int quantity = 1) =>
        _application.Invoke<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
            ApplicationMemberIds.CatalogPurchaseSend,
            new CatalogPurchaseSendRequest(pageId, offerId, extraData, quantity),
            Ct);

    /// <summary>
    /// Sends a catalog purchase for an offer, wrapped as a gift for another user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The wrapping is part of the purchase, not a later step: the gift furni, box and ribbon are
    /// chosen here from the options in <see cref="GiftWrapping"/>. Not every offer may be gifted;
    /// read <see cref="CatalogPageOffer.Giftable"/> on a loaded offer, or ask with
    /// <see cref="RequestOfferGiftability(int)"/> and read the answer from
    /// <see cref="OfferGiftability"/>.
    /// </para>
    /// <para>
    /// It returns once the request is sent and does not wait for the server's answer.
    /// </para>
    /// </remarks>
    /// <param name="pageId">The catalog page the offer sits on.</param>
    /// <param name="offerId">The offer to buy.</param>
    /// <param name="receiverName">The name of the user who receives the gift.</param>
    /// <param name="message">The message sent with the gift.</param>
    /// <param name="extraData">The offer's selection data, or empty when it takes none.</param>
    /// <param name="spriteId">
    /// The sprite id of the gift furni the item is wrapped in, one of
    /// <see cref="GiftWrappingConfiguration.StuffTypes"/> or
    /// <see cref="GiftWrappingConfiguration.DefaultStuffTypes"/>. The default 0 is not a sprite
    /// from the wrapping configuration.
    /// </param>
    /// <param name="boxType">The box type, one of <see cref="GiftWrappingConfiguration.BoxTypes"/>.</param>
    /// <param name="ribbonType">The ribbon type, one of <see cref="GiftWrappingConfiguration.RibbonTypes"/>.</param>
    /// <param name="showPurchaserName">
    /// <see langword="true"/> to show the buyer's name to the receiver; otherwise,
    /// <see langword="false"/>.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="receiverName"/> is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> or <paramref name="extraData"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a string argument is longer than 65535 bytes in UTF-8.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyGiftFromCatalog(
        int pageId,
        int offerId,
        string receiverName,
        string message = "",
        string extraData = "",
        int spriteId = 0,
        int boxType = 0,
        int ribbonType = 0,
        bool showPurchaserName = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiverName);
        _application.Invoke<GiftPurchaseRequest, GiftPurchaseDispatchReceipt>(
            ApplicationMemberIds.GiftsPurchase,
            new GiftPurchaseRequest(
                pageId,
                offerId,
                extraData,
                receiverName,
                message,
                spriteId,
                boxType,
                ribbonType,
                showPurchaserName),
            Ct);
    }

    /// <summary>
    /// Sends a catalog purchase wrapped as a gift, described by the gift purchase message itself.
    /// </summary>
    /// <remarks>
    /// Same as
    /// <see cref="BuyGiftFromCatalog(int, int, string, string, string, int, int, int, bool)"/> with
    /// the fields of <paramref name="request"/>. When the receiver does not exist, the
    /// <see cref="OnGiftReceiverNotFound(Action)"/> handlers run.
    /// </remarks>
    /// <param name="request">The purchase, with the offer, the receiver, the gift message and the wrapping.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/>, its gift message or its extra data is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the receiver name is <see langword="null"/>, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a string field is longer than 65535 bytes in UTF-8.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session or the catalog state changed before sending.</exception>
    public void BuyGiftFromCatalog(PurchaseFromCatalogAsGift request)
    {
        ArgumentNullException.ThrowIfNull(request);
        BuyGiftFromCatalog(
            request.PageId,
            request.OfferId,
            request.ReceiverName,
            request.GiftMessage,
            request.ExtraData,
            request.SpriteId,
            request.BoxType,
            request.RibbonType,
            request.ShowPurchaserName);
    }

    /// <summary>
    /// Requests the membership offers the hotel sells, and how much membership the account already
    /// holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each offer prices in credits and optionally in an activity currency, and carries the expiry
    /// the account would reach if it were bought, which is what makes it worth reading before a
    /// purchase rather than after.
    /// </para>
    /// <para>
    /// The request is sent once without a retry, and the reply is not blocked from the game
    /// client. All pages of the reply are collected into one result.
    /// </para>
    /// </remarks>
    /// <param name="offerType">The offer set selector sent to the hotel.</param>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>The offers and the number of membership days the account has left.</returns>
    /// <exception cref="InvalidDataException">Thrown when the offers changed while the pages were being collected, or a page was inconsistent.</exception>
    public async Task<HabboClubOffers> GetClubOffers(
        int offerType = 1,
        int timeoutMs = 10000)
    {
        const int page_limit = 500;
        const int maximum_pages = 132;
        SubscriptionClubOffersPage page = await _application
            .InvokeAsync<SubscriptionClubOffersRefreshRequest, SubscriptionClubOffersPage>(
                ApplicationMemberIds.SubscriptionsClubOffersRefresh,
                new SubscriptionClubOffersRefreshRequest(
                    offerType,
                    page_limit,
                    timeoutMs),
                Ct)
            .ConfigureAwait(false);
        if (!page.Connected ||
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
            page = _application.Invoke<
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
    /// Registers a handler that runs when the hotel answers a catalog purchase, including refusals.
    /// </summary>
    /// <param name="handler">The handler to call with the purchase outcome.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnPurchase(Action<CatalogPurchaseOutcome> handler)
        => Subscribe(
            handler,
            value => Game.Catalog.PurchaseAnswered += value,
            value => Game.Catalog.PurchaseAnswered -= value);

    /// <summary>
    /// Registers a handler that runs when the hotel republishes its catalog.
    /// </summary>
    /// <remarks>
    /// A republish invalidates every catalog page already loaded.
    /// </remarks>
    /// <param name="handler">
    /// The handler to call with the server's message, which says whether the client should
    /// refresh the catalog at once and carries the new furni data hash, if any.
    /// </param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    public IDisposable OnCatalogPublished(Action<CatalogPublished> handler)
    {
        return Track(_application.Subscribe<CatalogPublishedEvent>(
            ApplicationMemberIds.CatalogPublished,
            Guarded<CatalogPublishedEvent>(publication => handler(publication.Publication))));
    }
}
