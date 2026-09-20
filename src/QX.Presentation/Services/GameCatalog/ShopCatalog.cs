using Qx.Game;
using Qx.Game.Application;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Services.Game;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.GameCatalog;

public sealed class ShopCatalog : IDisposable
{
    public const int PageTimeoutMilliseconds = 2500;
    public const int PageDelayMilliseconds = 150;
    public const long FreshPageAgeMilliseconds = 300000;

    readonly IGameGateway _gateway;
    readonly IUiDispatcher _dispatcher;
    readonly Func<IReadOnlyList<CatalogOfferMatch>> _offers;
    IDisposable? _publications;
    long _subscription;
    long _invalidations;

    public ShopCatalog(IGameGateway gateway, IUiDispatcher dispatcher)
        : this(gateway, dispatcher, null)
    {
    }

    public ShopCatalog(IGameGateway gateway, IUiDispatcher dispatcher, Func<IReadOnlyList<CatalogOfferMatch>>? offers)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _offers = offers ?? CachedOffers;
    }

    public bool IsLoaded { get; private set; }

    public bool IsComplete { get; private set; }

    public long SessionGeneration { get; private set; }

    public long CatalogGeneration { get; private set; }

    public long Invalidations => Volatile.Read(ref _invalidations);

    public event Action<string?>? Invalidated;

    public void Watch()
    {
        Stop();
        long subscription = Interlocked.Increment(ref _subscription);
        _publications = _gateway.Subscribe<CatalogPublishedEvent>(
            ApplicationMemberIds.CatalogPublished,
            _ => Republished(subscription));
    }

    public void Stop()
    {
        Interlocked.Increment(ref _subscription);
        Interlocked.Exchange(ref _publications, null)?.Dispose();
    }

    public void Invalidate(string? reason)
    {
        Interlocked.Increment(ref _invalidations);
        IsLoaded = false;
        IsComplete = false;
        SessionGeneration = 0;
        CatalogGeneration = 0;
        Invalidated?.Invoke(reason);
    }

    public void MarkLoaded(bool complete)
    {
        IsLoaded = true;
        IsComplete = complete;
    }

    public async Task<CatalogStateView> StateAsync(CancellationToken cancellation_token) =>
        await _gateway.QueryAsync<CatalogStateRequest, CatalogStateView>(
            ApplicationMemberIds.CatalogState,
            new CatalogStateRequest(),
            cancellation_token);

    public async Task<CatalogLoadView> LoadPagesAsync(long session_generation, long catalog_generation, CancellationToken cancellation_token) =>
        await _gateway.InvokeAsync<CatalogLoadRequest, CatalogLoadView>(
            ApplicationMemberIds.CatalogPagesLoad,
            new CatalogLoadRequest(
                OnlyVisible: false,
                DelayMilliseconds: PageDelayMilliseconds,
                MaxAgeMilliseconds: IsLoaded ? 0 : FreshPageAgeMilliseconds,
                TimeoutMilliseconds: PageTimeoutMilliseconds,
                ExpectedSessionGeneration: session_generation,
                ExpectedCatalogGeneration: catalog_generation),
            cancellation_token);

    public async Task<IReadOnlyDictionary<FurniKey, IReadOnlyList<FurniShopOffer>>?> MergeAsync(
        long? expected_session,
        long? expected_catalog,
        CancellationToken cancellation_token)
    {
        CatalogStateView before = await StateAsync(cancellation_token);
        if ((expected_session is { } session && before.SessionGeneration != session) ||
            (expected_catalog is { } catalog && before.CatalogGeneration != catalog))
        {
            return null;
        }
        if (Group() is not { } offers)
            return null;
        CatalogStateView after = await StateAsync(cancellation_token);
        if (after.SessionGeneration != before.SessionGeneration || after.CatalogGeneration != before.CatalogGeneration)
            return null;
        SessionGeneration = after.SessionGeneration;
        CatalogGeneration = after.CatalogGeneration;
        return offers;
    }

    public async Task<FurniShopOffer?> FreshOfferAsync(FurniShopOffer shown, FurniKey key, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(shown);
        CatalogPageView page = await _gateway.InvokeAsync<CatalogPageGetRequest, CatalogPageView>(
            ApplicationMemberIds.CatalogPageGet,
            new CatalogPageGetRequest(
                shown.PageId,
                shown.OfferId,
                MaxAgeMilliseconds: 0,
                TimeoutMilliseconds: PageTimeoutMilliseconds,
                ExpectedSessionGeneration: SessionGeneration,
                ExpectedCatalogGeneration: CatalogGeneration),
            cancellation_token);
        CatalogOfferView? offer = page.Offers.FirstOrDefault(candidate => candidate.OfferId == shown.OfferId);
        CatalogProductView? product = offer?.Products.FirstOrDefault(candidate =>
            candidate.FurniClassId == key.Kind && ProductType(candidate.ProductType) == key.Type);
        return offer is null || product is null
            ? null
            : new FurniShopOffer(
                page.PageId,
                offer.OfferId,
                shown.Page,
                product.ProductCount,
                offer.PriceInCredits,
                offer.PriceInActivityPoints,
                offer.ActivityPointType,
                offer.PriceInSilver);
    }

    public async Task<CatalogPurchaseDispatchReceipt> SendPurchaseAsync(FurniShopOffer offer, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return await _gateway.InvokeAsync<CatalogPurchaseSendRequest, CatalogPurchaseDispatchReceipt>(
            ApplicationMemberIds.CatalogPurchaseSend,
            new CatalogPurchaseSendRequest(
                offer.PageId,
                offer.OfferId,
                ExpectedSessionGeneration: SessionGeneration,
                ExpectedCatalogGeneration: CatalogGeneration),
            cancellation_token);
    }

    public static bool ReceiptMatches(CatalogPurchaseDispatchReceipt receipt, FurniShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(offer);
        return receipt.MessagesDispatched == 1 &&
            receipt.PageId == offer.PageId &&
            receipt.OfferId == offer.OfferId &&
            receipt.Quantity == 1;
    }

    public static ItemType? ProductType(string product_type)
    {
        return product_type switch
        {
            CatalogProduct.TypeStuff => ItemType.Floor,
            CatalogProduct.TypeItem => ItemType.Wall,
            _ => null
        };
    }

    public void Dispose() => Stop();

    IReadOnlyList<CatalogOfferMatch> CachedOffers() => _gateway.Game.Catalog.CachedOffers();

    Dictionary<FurniKey, IReadOnlyList<FurniShopOffer>>? Group()
    {
        IReadOnlyList<CatalogOfferMatch> matches;
        try
        {
            matches = _offers();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        Dictionary<FurniKey, List<FurniShopOffer>> grouped = [];
        foreach (CatalogOfferMatch match in matches)
        {
            foreach (CatalogProduct product in match.Offer.Products)
            {
                if (ProductType(product.ProductType) is not { } type || product.FurniClassId <= 0)
                    continue;
                var key = new FurniKey(type, product.FurniClassId);
                if (!grouped.TryGetValue(key, out List<FurniShopOffer>? offers))
                    grouped[key] = offers = [];
                offers.Add(Offer(match, product));
            }
        }
        Dictionary<FurniKey, IReadOnlyList<FurniShopOffer>> result = new(grouped.Count);
        foreach ((FurniKey key, List<FurniShopOffer> offers) in grouped)
            result[key] = offers;
        return result;
    }

    FurniShopOffer Offer(CatalogOfferMatch match, CatalogProduct product) =>
        new(
            match.Page.PageId,
            match.Offer.OfferId,
            match.Node?.Localization is { Length: > 0 } page ? Localized(page) ?? page : $"Page {match.Page.PageId}",
            product.ProductCount,
            match.Offer.PriceInCredits,
            match.Offer.PriceInActivityPoints,
            match.Offer.ActivityPointType,
            match.Offer.PriceInSilver);

    string? Localized(string key)
    {
        string lookup = key.Length > 2 && key[0] == '$' && key[^1] == '$' ? key[1..^1] : key;
        ExternalTexts? texts = _gateway.Game.GameData.Texts;
        return texts is not null && texts.TryGet(lookup, out string value) && value.Length > 0 ? value : null;
    }

    void Republished(long subscription) =>
        _dispatcher.Post(() =>
        {
            if (Volatile.Read(ref _subscription) != subscription)
                return;
            Invalidate("The shop catalog changed. Scan it again.");
        });
}
