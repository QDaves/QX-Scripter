using System.Collections.ObjectModel;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.Application;

internal sealed partial class GiftApplication
{
    public GiftStateView ReadState(GiftStateRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        GiftState state = gifts.State;
        bool connected = Connected(state);
        var giftability = state.OfferGiftability
            .OrderBy(entry => entry.Key)
            .ToDictionary(entry => entry.Key, entry => entry.Value.Value);
        var result = new GiftStateView(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.Revision,
            state.WrappingRevision,
            state.ClubInfoRevision,
            state.ClubSelectedRevision,
            state.PresentOpenedRevision,
            state.ReceiverNotFoundRevision,
            state.ClubNotificationRevision,
            state.OfferGiftabilityRevision,
            state.NewUserOfferRevision,
            state.NewUserIncompleteRevision,
            state.Wrapping is { } wrapping ? WrappingSummary(wrapping) : null,
            state.ClubInfo is { } club_info ? ClubInfoSummary(club_info) : null,
            state.ClubSelected is { } selected ? ClubSelectedSummary(selected) : null,
            state.PresentOpened,
            state.ClubNotification,
            state.NewUserOffer is { } new_user_offer
                ? NewUserOfferSummary(new_user_offer)
                : null,
            state.NewUserFlowIncomplete,
            new ReadOnlyDictionary<int, bool>(giftability));
        RequireStateSession(state);
        return result;
    }

    public GiftWrappingPage ReadWrapping(GiftWrappingPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        if (!Enum.IsDefined(request.Collection))
            throw new ArgumentOutOfRangeException(nameof(request.Collection));
        GiftSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        GiftState state = lease.State;
        GiftWrappingConfiguration? snapshot = state.Wrapping;
        IReadOnlyList<int> values = snapshot is null
            ? Array.Empty<int>()
            : WrappingValues(snapshot, request.Collection);
        IReadOnlyList<int> page = Slice(values, request.Offset, request.Limit);
        bool connected = Connected(state);
        var result = new GiftWrappingPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.WrappingRevision,
            lease.Revision,
            snapshot is not null,
            snapshot?.IsWrappingEnabled,
            snapshot?.WrappingPrice,
            request.Collection,
            values.Count,
            request.Offset,
            NextOffset(request.Offset, page.Count, values.Count),
            page);
        RequireLeaseActive(lease);
        return result;
    }

    public GiftClubInfoPage ReadClubInfo(GiftClubInfoPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        if (!Enum.IsDefined(request.Collection))
            throw new ArgumentOutOfRangeException(nameof(request.Collection));
        GiftSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        return ClubInfoPage(
            lease,
            request.Collection,
            request.Offset,
            request.Limit);
    }

    public GiftClubSelectedPage ReadClubSelected(GiftClubSelectedPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        if (!Enum.IsDefined(request.Collection))
            throw new ArgumentOutOfRangeException(nameof(request.Collection));
        GiftSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        GiftState state = lease.State;
        ClubGiftSelected? snapshot = state.ClubSelected;
        IReadOnlyList<CatalogProduct> products = Array.Empty<CatalogProduct>();
        IReadOnlyList<CatalogPageProduct> unity_products = Array.Empty<CatalogPageProduct>();
        int total = 0;
        int returned = 0;
        if (snapshot is not null)
        {
            switch (request.Collection)
            {
                case GiftClubSelectedCollection.Products:
                    products = Slice(snapshot.Products, request.Offset, request.Limit);
                    total = snapshot.Products.Count;
                    returned = products.Count;
                    break;
                case GiftClubSelectedCollection.UnityProducts:
                    IReadOnlyList<CatalogPageProduct> source =
                        snapshot.UnityProducts ?? Array.Empty<CatalogPageProduct>();
                    unity_products = Slice(source, request.Offset, request.Limit);
                    total = source.Count;
                    returned = unity_products.Count;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Collection));
            }
        }
        bool connected = Connected(state);
        var result = new GiftClubSelectedPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.ClubSelectedRevision,
            lease.Revision,
            snapshot is not null,
            snapshot?.ProductCode,
            snapshot?.Products.Count ?? 0,
            snapshot?.UnityProducts?.Count ?? 0,
            request.Collection,
            total,
            request.Offset,
            NextOffset(request.Offset, returned, total),
            products,
            unity_products);
        RequireLeaseActive(lease);
        return result;
    }

    public GiftNewUserOfferPage ReadNewUserOffer(GiftNewUserOfferPageRequest request)
    {
        using Invocation invocation = EnterInvocation();
        ArgumentNullException.ThrowIfNull(request);
        ValidatePage(request.Offset, request.Limit, request.SnapshotRevision);
        if (!Enum.IsDefined(request.Collection))
            throw new ArgumentOutOfRangeException(nameof(request.Collection));
        GiftSnapshotLease lease = request.SnapshotRevision is long revision
            ? ReadLease(revision)
            : StoreCurrentLease();
        GiftState state = lease.State;
        NuxGiftOffer? snapshot = state.NewUserOffer;
        int total_steps = snapshot?.Steps.Count ?? 0;
        int total_options = snapshot is null ? 0 : CountNewUserOptions(snapshot);
        int total_products = snapshot is null ? 0 : CountNewUserProducts(snapshot);
        IReadOnlyList<GiftNewUserStepView> steps = Array.Empty<GiftNewUserStepView>();
        IReadOnlyList<GiftNewUserOptionView> options = Array.Empty<GiftNewUserOptionView>();
        IReadOnlyList<GiftNewUserProductView> products = Array.Empty<GiftNewUserProductView>();
        int total = 0;
        int returned = 0;
        if (snapshot is not null)
        {
            switch (request.Collection)
            {
                case GiftNewUserOfferCollection.Steps:
                    steps = NewUserSteps(snapshot, request.Offset, request.Limit);
                    total = total_steps;
                    returned = steps.Count;
                    break;
                case GiftNewUserOfferCollection.Options:
                    options = NewUserOptions(snapshot, request.Offset, request.Limit);
                    total = total_options;
                    returned = options.Count;
                    break;
                case GiftNewUserOfferCollection.Products:
                    products = NewUserProducts(snapshot, request.Offset, request.Limit);
                    total = total_products;
                    returned = products.Count;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Collection));
            }
        }
        bool connected = Connected(state);
        var result = new GiftNewUserOfferPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.NewUserOfferRevision,
            lease.Revision,
            snapshot is not null,
            total_steps,
            total_options,
            total_products,
            request.Collection,
            total,
            request.Offset,
            NextOffset(request.Offset, returned, total),
            steps,
            options,
            products);
        RequireLeaseActive(lease);
        return result;
    }

    private GiftClubInfoPage ClubInfoPage(
        GiftSnapshotLease lease,
        GiftClubInfoCollection collection,
        int offset,
        int limit)
    {
        GiftState state = lease.State;
        ClubGiftInfo? snapshot = state.ClubInfo;
        int total_offers = snapshot?.Offers.Count ?? 0;
        int total_eligibility = snapshot?.GiftEligibility.Count ?? 0;
        int total_products = snapshot is null ? 0 : CountClubProducts(snapshot);
        int total_unity_references = snapshot is null
            ? 0
            : CountClubUnityProductReferences(snapshot);
        int total_unity_products = snapshot is null ? 0 : CountClubUnityProducts(snapshot);
        IReadOnlyList<GiftClubOfferView> offers = Array.Empty<GiftClubOfferView>();
        IReadOnlyList<GiftClubEligibilityView> eligibility =
            Array.Empty<GiftClubEligibilityView>();
        IReadOnlyList<GiftClubProductView> products = Array.Empty<GiftClubProductView>();
        IReadOnlyList<GiftClubUnityProductReferenceView> unity_references =
            Array.Empty<GiftClubUnityProductReferenceView>();
        IReadOnlyList<GiftClubUnityProductView> unity_products =
            Array.Empty<GiftClubUnityProductView>();
        int total;
        int returned;
        switch (collection)
        {
            case GiftClubInfoCollection.Offers:
                offers = snapshot is null
                    ? Array.Empty<GiftClubOfferView>()
                    : ClubOffers(snapshot, offset, limit);
                total = total_offers;
                returned = offers.Count;
                break;
            case GiftClubInfoCollection.Eligibility:
                eligibility = snapshot is null
                    ? Array.Empty<GiftClubEligibilityView>()
                    : ClubEligibility(snapshot, offset, limit);
                total = total_eligibility;
                returned = eligibility.Count;
                break;
            case GiftClubInfoCollection.Products:
                products = snapshot is null
                    ? Array.Empty<GiftClubProductView>()
                    : ClubProducts(snapshot, offset, limit);
                total = total_products;
                returned = products.Count;
                break;
            case GiftClubInfoCollection.UnityProductReferences:
                unity_references = snapshot is null
                    ? Array.Empty<GiftClubUnityProductReferenceView>()
                    : ClubUnityProductReferences(snapshot, offset, limit);
                total = total_unity_references;
                returned = unity_references.Count;
                break;
            case GiftClubInfoCollection.UnityProducts:
                unity_products = snapshot is null
                    ? Array.Empty<GiftClubUnityProductView>()
                    : ClubUnityProducts(snapshot, offset, limit);
                total = total_unity_products;
                returned = unity_products.Count;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(collection));
        }
        bool connected = Connected(state);
        var result = new GiftClubInfoPage(
            connected,
            connected ? state.Session!.Client : null,
            state.SessionGeneration,
            state.ClubInfoRevision,
            lease.Revision,
            snapshot is not null,
            snapshot?.DaysUntilNextGift,
            snapshot?.GiftsAvailable,
            total_offers,
            total_eligibility,
            total_products,
            total_unity_references,
            total_unity_products,
            collection,
            total,
            offset,
            NextOffset(offset, returned, total),
            offers,
            eligibility,
            products,
            unity_references,
            unity_products);
        RequireLeaseActive(lease);
        return result;
    }

    private static GiftWrappingSummaryView WrappingSummary(
        GiftWrappingConfiguration value) => new(
        value.IsWrappingEnabled,
        value.WrappingPrice,
        value.StuffTypes.Count,
        value.BoxTypes.Count,
        value.RibbonTypes.Count,
        value.DefaultStuffTypes.Count);

    private static GiftClubInfoSummaryView ClubInfoSummary(ClubGiftInfo value) => new(
        value.DaysUntilNextGift,
        value.GiftsAvailable,
        value.Offers.Count,
        value.GiftEligibility.Count,
        CountClubProducts(value),
        CountClubUnityProductReferences(value),
        CountClubUnityProducts(value));

    private static GiftClubSelectedSummaryView ClubSelectedSummary(
        ClubGiftSelected value) => new(
        value.ProductCode,
        value.Products.Count,
        value.UnityProducts?.Count ?? 0);

    private static GiftNewUserOfferSummaryView NewUserOfferSummary(
        NuxGiftOffer value) => new(
        value.Steps.Count,
        CountNewUserOptions(value),
        CountNewUserProducts(value));

    private bool Connected(GiftState state) =>
        state.Session is not null && ReferenceEquals(connection.Session, state.Session);

    private void RequireStateSession(GiftState state)
    {
        GiftState current = gifts.State;
        if (!ReferenceEquals(current.Session, state.Session) ||
            current.SessionGeneration != state.SessionGeneration ||
            !ReferenceEquals(connection.Session, state.Session))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the gift state was being read.");
        }
    }

    private void RequireLeaseActive(GiftSnapshotLease lease)
    {
        if (!LeaseActive(lease))
        {
            throw new InvalidOperationException(
                "The hotel session changed while the gift snapshot was being read.");
        }
    }

    private static IReadOnlyList<int> WrappingValues(
        GiftWrappingConfiguration value,
        GiftWrappingCollection collection) => collection switch
    {
        GiftWrappingCollection.StuffTypes => value.StuffTypes,
        GiftWrappingCollection.BoxTypes => value.BoxTypes,
        GiftWrappingCollection.RibbonTypes => value.RibbonTypes,
        GiftWrappingCollection.DefaultStuffTypes => value.DefaultStuffTypes,
        _ => throw new ArgumentOutOfRangeException(nameof(collection))
    };

    private static IReadOnlyList<GiftClubOfferView> ClubOffers(
        ClubGiftInfo value,
        int offset,
        int limit)
    {
        int count = PageCount(value.Offers.Count, offset, limit);
        var page = new GiftClubOfferView[count];
        for (int index = 0; index < count; index++)
        {
            int ordinal = checked(offset + index);
            CatalogPageOffer offer = value.Offers[ordinal];
            page[index] = new GiftClubOfferView(
                ordinal,
                offer.OfferId,
                offer.LocalizationId,
                offer.IsRent,
                offer.PriceInCredits,
                offer.PriceInActivityPoints,
                offer.ActivityPointType,
                offer.PriceInSilver,
                offer.Giftable,
                offer.ClubLevel,
                offer.BundlePurchaseAllowed,
                offer.IsPet,
                offer.PreviewImage,
                offer.Products.Count,
                offer.UnityProductReferences?.Count ?? 0,
                offer.UnityProducts?.Count ?? 0);
        }
        return Array.AsReadOnly(page);
    }

    private static IReadOnlyList<GiftClubEligibilityView> ClubEligibility(
        ClubGiftInfo value,
        int offset,
        int limit)
    {
        int count = PageCount(value.GiftEligibility.Count, offset, limit);
        var page = new GiftClubEligibilityView[count];
        for (int index = 0; index < count; index++)
        {
            int ordinal = checked(offset + index);
            ClubGiftEligibility entry = value.GiftEligibility[ordinal];
            page[index] = new GiftClubEligibilityView(
                ordinal,
                entry.OfferId,
                entry.IsVip,
                entry.DaysRequired,
                entry.IsSelectable);
        }
        return Array.AsReadOnly(page);
    }

    private static IReadOnlyList<GiftClubProductView> ClubProducts(
        ClubGiftInfo value,
        int offset,
        int limit)
    {
        var page = new List<GiftClubProductView>(limit);
        int ordinal = 0;
        for (int offer_ordinal = 0;
            offer_ordinal < value.Offers.Count && page.Count < limit;
            offer_ordinal++)
        {
            IReadOnlyList<CatalogProduct> products = value.Offers[offer_ordinal].Products;
            for (int product_ordinal = 0;
                product_ordinal < products.Count && page.Count < limit;
                product_ordinal++)
            {
                if (ordinal++ < offset)
                    continue;
                page.Add(new GiftClubProductView(
                    offer_ordinal,
                    product_ordinal,
                    products[product_ordinal]));
            }
        }
        return Array.AsReadOnly(page.ToArray());
    }

    private static IReadOnlyList<GiftClubUnityProductReferenceView>
        ClubUnityProductReferences(
            ClubGiftInfo value,
            int offset,
            int limit)
    {
        var page = new List<GiftClubUnityProductReferenceView>(limit);
        int ordinal = 0;
        for (int offer_ordinal = 0;
            offer_ordinal < value.Offers.Count && page.Count < limit;
            offer_ordinal++)
        {
            IReadOnlyList<CatalogPageProductReference> references =
                value.Offers[offer_ordinal].UnityProductReferences ??
                Array.Empty<CatalogPageProductReference>();
            for (int reference_ordinal = 0;
                reference_ordinal < references.Count && page.Count < limit;
                reference_ordinal++)
            {
                if (ordinal++ < offset)
                    continue;
                page.Add(new GiftClubUnityProductReferenceView(
                    offer_ordinal,
                    reference_ordinal,
                    references[reference_ordinal]));
            }
        }
        return Array.AsReadOnly(page.ToArray());
    }

    private static IReadOnlyList<GiftClubUnityProductView> ClubUnityProducts(
        ClubGiftInfo value,
        int offset,
        int limit)
    {
        var page = new List<GiftClubUnityProductView>(limit);
        int ordinal = 0;
        for (int offer_ordinal = 0;
            offer_ordinal < value.Offers.Count && page.Count < limit;
            offer_ordinal++)
        {
            IReadOnlyList<CatalogPageProduct> products =
                value.Offers[offer_ordinal].UnityProducts ??
                Array.Empty<CatalogPageProduct>();
            for (int product_ordinal = 0;
                product_ordinal < products.Count && page.Count < limit;
                product_ordinal++)
            {
                if (ordinal++ < offset)
                    continue;
                page.Add(new GiftClubUnityProductView(
                    offer_ordinal,
                    product_ordinal,
                    products[product_ordinal]));
            }
        }
        return Array.AsReadOnly(page.ToArray());
    }

    private static IReadOnlyList<GiftNewUserStepView> NewUserSteps(
        NuxGiftOffer value,
        int offset,
        int limit)
    {
        int count = PageCount(value.Steps.Count, offset, limit);
        var page = new GiftNewUserStepView[count];
        for (int index = 0; index < count; index++)
        {
            int ordinal = checked(offset + index);
            NuxGiftStep step = value.Steps[ordinal];
            page[index] = new GiftNewUserStepView(
                ordinal,
                step.DayIndex,
                step.StepIndex,
                step.Options.Count);
        }
        return Array.AsReadOnly(page);
    }

    private static IReadOnlyList<GiftNewUserOptionView> NewUserOptions(
        NuxGiftOffer value,
        int offset,
        int limit)
    {
        var page = new List<GiftNewUserOptionView>(limit);
        int ordinal = 0;
        for (int step_ordinal = 0;
            step_ordinal < value.Steps.Count && page.Count < limit;
            step_ordinal++)
        {
            IReadOnlyList<NuxGiftOption> options = value.Steps[step_ordinal].Options;
            for (int option_ordinal = 0;
                option_ordinal < options.Count && page.Count < limit;
                option_ordinal++)
            {
                if (ordinal++ < offset)
                    continue;
                NuxGiftOption option = options[option_ordinal];
                page.Add(new GiftNewUserOptionView(
                    step_ordinal,
                    option_ordinal,
                    option.ThumbnailUrl,
                    option.Products.Count));
            }
        }
        return Array.AsReadOnly(page.ToArray());
    }

    private static IReadOnlyList<GiftNewUserProductView> NewUserProducts(
        NuxGiftOffer value,
        int offset,
        int limit)
    {
        var page = new List<GiftNewUserProductView>(limit);
        int ordinal = 0;
        for (int step_ordinal = 0;
            step_ordinal < value.Steps.Count && page.Count < limit;
            step_ordinal++)
        {
            IReadOnlyList<NuxGiftOption> options = value.Steps[step_ordinal].Options;
            for (int option_ordinal = 0;
                option_ordinal < options.Count && page.Count < limit;
                option_ordinal++)
            {
                IReadOnlyList<NuxGiftProduct> products = options[option_ordinal].Products;
                for (int product_ordinal = 0;
                    product_ordinal < products.Count && page.Count < limit;
                    product_ordinal++)
                {
                    if (ordinal++ < offset)
                        continue;
                    NuxGiftProduct product = products[product_ordinal];
                    page.Add(new GiftNewUserProductView(
                        step_ordinal,
                        option_ordinal,
                        product_ordinal,
                        product.ProductCode,
                        product.LocalizationKey));
                }
            }
        }
        return Array.AsReadOnly(page.ToArray());
    }

    private static int CountClubProducts(ClubGiftInfo value) =>
        SumCounts(value.Offers, offer => offer.Products.Count);

    private static int CountClubUnityProductReferences(ClubGiftInfo value) =>
        SumCounts(value.Offers, offer => offer.UnityProductReferences?.Count ?? 0);

    private static int CountClubUnityProducts(ClubGiftInfo value) =>
        SumCounts(value.Offers, offer => offer.UnityProducts?.Count ?? 0);

    private static int CountNewUserOptions(NuxGiftOffer value) =>
        SumCounts(value.Steps, step => step.Options.Count);

    private static int CountNewUserProducts(NuxGiftOffer value)
    {
        long total = 0;
        foreach (NuxGiftStep step in value.Steps)
        {
            foreach (NuxGiftOption option in step.Options)
            {
                total += option.Products.Count;
                if (total > int.MaxValue)
                    throw new InvalidDataException("The new-user gift product count is too large.");
            }
        }
        return (int)total;
    }

    private static int SumCounts<T>(IReadOnlyList<T> values, Func<T, int> count)
    {
        long total = 0;
        foreach (T value in values)
        {
            total += count(value);
            if (total > int.MaxValue)
                throw new InvalidDataException("The gift collection count is too large.");
        }
        return (int)total;
    }

    private static IReadOnlyList<T> Slice<T>(
        IReadOnlyList<T> values,
        int offset,
        int limit)
    {
        int count = PageCount(values.Count, offset, limit);
        var page = new T[count];
        for (int index = 0; index < count; index++)
            page[index] = values[offset + index];
        return Array.AsReadOnly(page);
    }

    private static int PageCount(int total, int offset, int limit) =>
        offset >= total ? 0 : Math.Min(limit, total - offset);

    private static int? NextOffset(int offset, int count, int total)
    {
        int consumed = checked(offset + count);
        return consumed < total ? consumed : null;
    }

    private static void ValidatePage(int offset, int limit, long? snapshot_revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ValidatePageLimit(limit);
        if (snapshot_revision is <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshot_revision));
        if (offset != 0 && snapshot_revision is null)
        {
            throw new ArgumentException(
                "Continuation pages require a snapshot revision.",
                nameof(snapshot_revision));
        }
    }
}
