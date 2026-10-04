using Qx.Game.Protocol;
using Qx.Game.Snapshots;
using Qx.Messages;
using Qx.Model;
using Qx.Model.Messages.Incoming;
using System.Collections.ObjectModel;

namespace Qx.Game;

/// <summary>
/// Represents the key that identifies a furni kind in the marketplace item statistics.
/// </summary>
/// <param name="FurniCategory">The marketplace category of the furni.</param>
/// <param name="FurniTypeId">The furni type id.</param>
public readonly record struct MarketplaceItemKey(
    MarketplaceFurniCategory FurniCategory,
    int FurniTypeId);

/// <summary>
/// Represents an immutable copy of a marketplace offer.
/// </summary>
/// <param name="OfferId">The id of the offer.</param>
/// <param name="Status">The raw status code of the offer. See <see cref="MarketplaceOfferSnapshot.OfferStatus"/>.</param>
/// <param name="WireType">The raw offer type code. See <see cref="MarketplaceOfferSnapshot.OfferType"/>.</param>
/// <param name="Kind">The furni type id of the offered item.</param>
/// <param name="Data">The item data of a floor offer, or <see langword="null"/> for other offer types.</param>
/// <param name="WallData">The data string of a wall offer, or an empty string for other offer types.</param>
/// <param name="UniqueSerialNumber">The serial number of a limited edition offer, or 0 for other offer types.</param>
/// <param name="UniqueSeriesSize">The series size of a limited edition offer, or 0 for other offer types.</param>
/// <param name="IsUsed">Whether the item of a used floor offer has been used, or <see langword="null"/> for other offer types.</param>
/// <param name="Price">The price of the offer in credits.</param>
/// <param name="MinutesRemaining">The number of minutes until the offer expires.</param>
/// <param name="AveragePrice">The average price of the item in credits.</param>
/// <param name="TradeVolume">The trade volume of the item, which the parsed offer layout does not carry and is 0.</param>
/// <param name="Offers">The number of open offers for the item in a search result, or 0 for the local user's own offers.</param>
/// <param name="StatusTimeMilliseconds">
/// The time the local user's own offer sold or expired, in Unix milliseconds, or
/// <see langword="null"/> when the hotel did not send it.
/// </param>
public sealed record MarketplaceOfferSnapshot(
    Id OfferId,
    int Status,
    int WireType,
    int Kind,
    ItemDataSnapshot? Data,
    string WallData,
    int UniqueSerialNumber,
    int UniqueSeriesSize,
    bool? IsUsed,
    int Price,
    int MinutesRemaining,
    int AveragePrice,
    int TradeVolume,
    int Offers,
    long? StatusTimeMilliseconds)
{
    /// <summary>
    /// Gets the status of the offer.
    /// </summary>
    public MarketplaceOfferStatus OfferStatus => (MarketplaceOfferStatus)Status;
    /// <summary>
    /// Gets the type of the offer.
    /// </summary>
    public MarketplaceOfferType OfferType => (MarketplaceOfferType)WireType;
    /// <summary>
    /// Gets the furni type id of the offered item.
    /// </summary>
    public int FurniTypeId => Kind;
    /// <summary>
    /// Gets whether the offer contains a wall item.
    /// </summary>
    public bool IsWall => OfferType is MarketplaceOfferType.Wall;
    /// <summary>
    /// Gets the marketplace category the offered item belongs to.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown when the offer type is not supported.</exception>
    /// <remarks>
    /// Floor and used floor offers map to <see cref="MarketplaceFurniCategory.Floor"/>, wall offers to
    /// <see cref="MarketplaceFurniCategory.Wall"/> and limited edition offers to
    /// <see cref="MarketplaceFurniCategory.Limited"/>.
    /// </remarks>
    public MarketplaceFurniCategory FurniCategory => OfferType switch
    {
        MarketplaceOfferType.Floor or MarketplaceOfferType.UsableFloor =>
            MarketplaceFurniCategory.Floor,
        MarketplaceOfferType.Wall => MarketplaceFurniCategory.Wall,
        MarketplaceOfferType.LimitedEdition => MarketplaceFurniCategory.Limited,
        _ => throw new InvalidDataException(
            $"Unsupported marketplace offer type {WireType}.")
    };
}

/// <summary>
/// Represents an immutable copy of a marketplace search result.
/// </summary>
/// <param name="Offers">The offers in the result, one per offer id.</param>
/// <param name="TotalItemsFound">The total number of items the hotel found for the search.</param>
public sealed record MarketplaceOffersSnapshot(
    IReadOnlyList<MarketplaceOfferSnapshot> Offers,
    int TotalItemsFound);

/// <summary>
/// Represents an immutable copy of the local user's own marketplace offers.
/// </summary>
/// <param name="CreditsWaiting">The credits from sold offers that are waiting to be redeemed.</param>
/// <param name="Offers">The local user's offers, one per offer id.</param>
public sealed record MarketplaceOwnOffersSnapshot(
    int CreditsWaiting,
    IReadOnlyList<MarketplaceOfferSnapshot> Offers);

/// <summary>
/// Represents an immutable copy of the marketplace statistics for one furni kind.
/// </summary>
/// <param name="AverageSalePrice">The average sale price in credits.</param>
/// <param name="OfferCount">The number of open offers for the item.</param>
/// <param name="HistoryLengthDays">The number of days the history covers.</param>
/// <param name="History">The daily trade history.</param>
/// <param name="FurniTypeId">The furni type id.</param>
/// <param name="FurniCategory">The marketplace category of the furni.</param>
/// <param name="LowestPrice">The lowest open offer price, or <see langword="null"/> when the hotel did not send it.</param>
/// <param name="SuggestedPrice">The suggested price, or <see langword="null"/> when the hotel did not send it.</param>
public sealed record MarketplaceItemStatsSnapshot(
    int AverageSalePrice,
    int OfferCount,
    int HistoryLengthDays,
    IReadOnlyList<MarketplaceTradeInfo> History,
    int FurniTypeId,
    MarketplaceFurniCategory FurniCategory,
    int? LowestPrice,
    int? SuggestedPrice);

/// <summary>
/// Represents an immutable copy of the result of canceling all of the local user's offers.
/// </summary>
/// <param name="OfferIds">The distinct ids of the canceled offers.</param>
/// <param name="Success">Whether the hotel reported success.</param>
public sealed record MarketplaceCancelAllOffersSnapshot(
    IReadOnlyList<Id> OfferIds,
    bool Success);

/// <summary>
/// Represents an immutable view of the marketplace state at one point in time.
/// </summary>
/// <param name="Generation">The session generation the state belongs to.</param>
/// <param name="Revision">The revision of the state, which increases with every change.</param>
/// <param name="Configuration">The marketplace configuration, or <see langword="null"/> when not received.</param>
/// <param name="Eligibility">The last answer to whether the local user may make an offer, or <see langword="null"/> when not received.</param>
/// <param name="SearchResult">The last search result, or <see langword="null"/> when not received.</param>
/// <param name="OwnOffers">The local user's own offers, or <see langword="null"/> when not received.</param>
/// <param name="ItemStats">The item statistics received so far, keyed by category and furni type id.</param>
/// <param name="LastMakeOfferResult">The result of the last offer made, or <see langword="null"/> when none was received.</param>
/// <param name="LastBuyResult">The result of the last purchase, or <see langword="null"/> when none was received.</param>
/// <param name="LastCancelOfferResult">The result of the last offer cancellation, or <see langword="null"/> when none was received.</param>
/// <param name="LastCancelAllOffersResult">The result of the last cancellation of all offers, or <see langword="null"/> when none was received.</param>
/// <param name="LastClearHistoryResult">The result of the last own history clear, or <see langword="null"/> when none was received.</param>
public sealed record MarketplaceSnapshot(
    long Generation,
    long Revision,
    MarketplaceConfiguration? Configuration,
    MarketplaceCanMakeOfferResult? Eligibility,
    MarketplaceOffersSnapshot? SearchResult,
    MarketplaceOwnOffersSnapshot? OwnOffers,
    IReadOnlyDictionary<MarketplaceItemKey, MarketplaceItemStatsSnapshot> ItemStats,
    MarketplaceMakeOfferResult? LastMakeOfferResult,
    MarketplaceBuyResult? LastBuyResult,
    MarketplaceCancelOfferResult? LastCancelOfferResult,
    MarketplaceCancelAllOffersSnapshot? LastCancelAllOffersResult,
    MarketplaceClearOwnHistoryResult? LastClearHistoryResult)
{
    /// <summary>
    /// Gets an empty marketplace state.
    /// </summary>
    public static MarketplaceSnapshot Empty { get; } = new(
        0,
        0,
        null,
        null,
        null,
        null,
        EmptyMap<MarketplaceItemKey, MarketplaceItemStatsSnapshot>(),
        null,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Gets whether the marketplace configuration has been received.
    /// </summary>
    public bool ConfigurationLoaded => Configuration is not null;
    /// <summary>
    /// Gets whether an answer to whether the local user may make an offer has been received.
    /// </summary>
    public bool EligibilityLoaded => Eligibility is not null;

    /// <summary>
    /// Finds an offer in the last search result.
    /// </summary>
    /// <param name="offerId">The id of the offer.</param>
    /// <returns>The offer, or <see langword="null"/> when it is not in the last search result.</returns>
    public MarketplaceOfferSnapshot? FindSearchOffer(Id offerId) =>
        SearchResult?.Offers.FirstOrDefault(offer => offer.OfferId == offerId);

    /// <summary>
    /// Finds an offer among the local user's own offers.
    /// </summary>
    /// <param name="offerId">The id of the offer.</param>
    /// <returns>The offer, or <see langword="null"/> when it is not among the own offers.</returns>
    public MarketplaceOfferSnapshot? FindOwnOffer(Id offerId) =>
        OwnOffers?.Offers.FirstOrDefault(offer => offer.OfferId == offerId);

    /// <summary>
    /// Finds an offer in the last search result, then among the local user's own offers.
    /// </summary>
    /// <param name="offerId">The id of the offer.</param>
    /// <returns>The offer, or <see langword="null"/> when it is in neither list.</returns>
    public MarketplaceOfferSnapshot? FindOffer(Id offerId) =>
        FindSearchOffer(offerId) ?? FindOwnOffer(offerId);

    /// <summary>
    /// Finds the statistics received for a furni kind.
    /// </summary>
    /// <param name="furniCategory">The marketplace category of the furni.</param>
    /// <param name="furniTypeId">The furni type id.</param>
    /// <returns>The statistics, or <see langword="null"/> when none were received.</returns>
    public MarketplaceItemStatsSnapshot? FindItemStats(
        MarketplaceFurniCategory furniCategory,
        int furniTypeId) =>
        ItemStats.GetValueOrDefault(
            new MarketplaceItemKey(furniCategory, furniTypeId));

    private static IReadOnlyDictionary<TKey, TValue>
        EmptyMap<TKey, TValue>() where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>());
}

internal enum MarketplaceStateChangeKind
{
    Configuration,
    Eligibility,
    Search,
    OwnOffers,
    ItemStats,
    MakeResult,
    BuyResult,
    CancelResult,
    CancelAllResult,
    ClearHistoryResult,
    Reset
}

internal sealed record MarketplaceStateUpdate(
    MarketplaceStateChangeKind Kind,
    MarketplaceSnapshot State,
    object? Value);

/// <summary>
/// Manages the marketplace state received from the hotel.
/// </summary>
/// <remarks>
/// <para>
/// All members are safe to call from any thread. Each change publishes a new immutable
/// <see cref="MarketplaceSnapshot"/>. A successful cancellation removes the canceled offers from the
/// stored own offers.
/// </para>
/// <para>
/// The state is cleared when the hotel connection closes.
/// </para>
/// </remarks>
public sealed class MarketplaceManager : GameStateManager
{
    private readonly object publication_sync = new();
    private readonly object state_sync = new();
    private readonly Dictionary<MarketplaceItemKey, MarketplaceItemStatsSnapshot> item_stats = [];
    private MarketplaceSnapshot snapshot = MarketplaceSnapshot.Empty;
    private MarketplaceConfiguration? configuration;
    private MarketplaceCanMakeOfferResult? eligibility;
    private MarketplaceOffersSnapshot? search_result;
    private MarketplaceOwnOffersSnapshot? own_offers;
    private MarketplaceMakeOfferResult? last_make_offer_result;
    private MarketplaceBuyResult? last_buy_result;
    private MarketplaceCancelOfferResult? last_cancel_offer_result;
    private MarketplaceCancelAllOffersSnapshot? last_cancel_all_offers_result;
    private MarketplaceClearOwnHistoryResult? last_clear_history_result;
    private long generation;
    private long revision;
    private long committed_generation;
    private long reset_generation = -1;

    /// <summary>
    /// Gets the current marketplace state.
    /// </summary>
    public MarketplaceSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal event Action<MarketplaceStateUpdate>? StateChanged;

    /// <inheritdoc/>
    protected override void OnAttach()
    {
        OnIncoming(
            MessageContracts.Marketplace.Configuration.Snapshot,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.Configuration,
                message,
                () => configuration = message));
        OnIncoming(
            MessageContracts.Marketplace.Eligibility.Result,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.Eligibility,
                message,
                () => eligibility = message));
        OnIncoming(
            MessageContracts.Marketplace.Offers.SearchResult,
            (message, state_generation) =>
            {
                MarketplaceOffersSnapshot value = SnapshotOf(message);
                Store(
                    state_generation,
                    MarketplaceStateChangeKind.Search,
                    value,
                    () => search_result = value);
            });
        OnIncoming(
            MessageContracts.Marketplace.Offers.OwnSnapshot,
            (message, state_generation) =>
            {
                MarketplaceOwnOffersSnapshot value = SnapshotOf(message);
                Store(
                    state_generation,
                    MarketplaceStateChangeKind.OwnOffers,
                    value,
                    () => own_offers = value);
            });
        OnIncoming(
            MessageContracts.Marketplace.ItemStats.Snapshot,
            (message, state_generation) =>
            {
                MarketplaceItemStatsSnapshot value = SnapshotOf(message);
                var key = new MarketplaceItemKey(
                    value.FurniCategory,
                    value.FurniTypeId);
                Store(
                    state_generation,
                    MarketplaceStateChangeKind.ItemStats,
                    value,
                    () => item_stats[key] = value);
            });
        OnIncoming(
            MessageContracts.Marketplace.Offers.MakeResult,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.MakeResult,
                message,
                () => last_make_offer_result = message));
        OnIncoming(
            MessageContracts.Marketplace.Offers.BuyResult,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.BuyResult,
                message,
                () => last_buy_result = message));
        OnIncoming(
            MessageContracts.Marketplace.Offers.CancelResult,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.CancelResult,
                message,
                () =>
                {
                    last_cancel_offer_result = message;
                    if (message.Success)
                        RemoveOwnOffers([message.OfferId]);
                }));
        OnIncoming(
            MessageContracts.Marketplace.Offers.CancelAllResult,
            (message, state_generation) =>
            {
                MarketplaceCancelAllOffersSnapshot value = SnapshotOf(message);
                Store(
                    state_generation,
                    MarketplaceStateChangeKind.CancelAllResult,
                    value,
                    () =>
                    {
                        last_cancel_all_offers_result = value;
                        if (value.Success)
                            RemoveOwnOffers(value.OfferIds);
                    });
            });
        OnIncoming(
            MessageContracts.Marketplace.Offers.ClearOwnHistoryResult,
            (message, state_generation) => Store(
                state_generation,
                MarketplaceStateChangeKind.ClearHistoryResult,
                message,
                () => last_clear_history_result = message));
    }

    /// <inheritdoc/>
    protected override void Reset()
    {
        long state_generation = CurrentStateGeneration;
        lock (publication_sync)
        {
            MarketplaceSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation ||
                    state_generation == reset_generation)
                {
                    return;
                }
                committed_generation = state_generation;
                reset_generation = state_generation;
                configuration = null;
                eligibility = null;
                search_result = null;
                own_offers = null;
                item_stats.Clear();
                last_make_offer_result = null;
                last_buy_result = null;
                last_cancel_offer_result = null;
                last_cancel_all_offers_result = null;
                last_clear_history_result = null;
                generation = state_generation;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new MarketplaceStateUpdate(
                MarketplaceStateChangeKind.Reset,
                updated,
                null));
        }
    }

    internal static MarketplaceOffersSnapshot SnapshotOf(MarketplaceOffers value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarketplaceOffersSnapshot(
            SnapshotOffers(value.Offers),
            value.TotalItemsFound);
    }

    internal static MarketplaceOwnOffersSnapshot SnapshotOf(MarketplaceOwnOffers value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarketplaceOwnOffersSnapshot(
            value.CreditsWaiting,
            SnapshotOffers(value.Offers));
    }

    internal static MarketplaceItemStatsSnapshot SnapshotOf(MarketplaceItemStats value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarketplaceItemStatsSnapshot(
            value.AverageSalePrice,
            value.OfferCount,
            value.HistoryLengthDays,
            ReadOnly(value.History),
            value.FurniTypeId,
            value.FurniCategory,
            value.LowestPrice,
            value.SuggestedPrice);
    }

    internal static MarketplaceCancelAllOffersSnapshot SnapshotOf(
        MarketplaceCancelAllOffersResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarketplaceCancelAllOffersSnapshot(
            ReadOnly(value.OfferIds.Distinct()),
            value.Success);
    }

    internal static bool Equivalent(
        MarketplaceOffersSnapshot left,
        MarketplaceOffersSnapshot right) =>
        left.TotalItemsFound == right.TotalItemsFound &&
        Equivalent(left.Offers, right.Offers);

    internal static bool Equivalent(
        MarketplaceOwnOffersSnapshot left,
        MarketplaceOwnOffersSnapshot right) =>
        left.CreditsWaiting == right.CreditsWaiting &&
        Equivalent(left.Offers, right.Offers);

    internal static bool Equivalent(
        MarketplaceItemStatsSnapshot left,
        MarketplaceItemStatsSnapshot right) =>
        left.AverageSalePrice == right.AverageSalePrice &&
        left.OfferCount == right.OfferCount &&
        left.HistoryLengthDays == right.HistoryLengthDays &&
        left.History.SequenceEqual(right.History) &&
        left.FurniTypeId == right.FurniTypeId &&
        left.FurniCategory == right.FurniCategory &&
        left.LowestPrice == right.LowestPrice &&
        left.SuggestedPrice == right.SuggestedPrice;

    internal static bool Equivalent(
        MarketplaceCancelAllOffersSnapshot left,
        MarketplaceCancelAllOffersSnapshot right) =>
        left.Success == right.Success &&
        left.OfferIds.SequenceEqual(right.OfferIds);

    private void Store(
        long state_generation,
        MarketplaceStateChangeKind kind,
        object value,
        Action mutation)
    {
        lock (publication_sync)
        {
            MarketplaceSnapshot updated;
            lock (state_sync)
            {
                if (state_generation < committed_generation)
                    return;
                committed_generation = state_generation;
                reset_generation = -1;
                mutation();
                generation = state_generation;
                revision++;
                updated = PublishState();
            }
            StateChanged?.Invoke(new MarketplaceStateUpdate(kind, updated, value));
        }
    }

    private MarketplaceSnapshot PublishState()
    {
        var updated = new MarketplaceSnapshot(
            generation,
            revision,
            configuration,
            eligibility,
            search_result,
            own_offers,
            ReadOnly(item_stats),
            last_make_offer_result,
            last_buy_result,
            last_cancel_offer_result,
            last_cancel_all_offers_result,
            last_clear_history_result);
        Volatile.Write(ref snapshot, updated);
        return updated;
    }

    private void RemoveOwnOffers(IReadOnlyCollection<Id> offer_ids)
    {
        if (own_offers is null ||
            own_offers.Offers.Count == 0 ||
            offer_ids.Count == 0)
        {
            return;
        }
        var removed = new HashSet<Id>(offer_ids);
        MarketplaceOfferSnapshot[] kept =
        [
            .. own_offers.Offers.Where(offer => !removed.Contains(offer.OfferId))
        ];
        if (kept.Length != own_offers.Offers.Count)
            own_offers = own_offers with { Offers = Array.AsReadOnly(kept) };
    }

    private static IReadOnlyList<MarketplaceOfferSnapshot> SnapshotOffers(
        IReadOnlyList<MarketplaceOffer> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        var positions = new Dictionary<Id, int>();
        var values = new List<MarketplaceOfferSnapshot>(offers.Count);
        foreach (MarketplaceOffer offer in offers)
        {
            ArgumentNullException.ThrowIfNull(offer);
            MarketplaceOfferSnapshot value = SnapshotOf(offer);
            if (positions.TryGetValue(value.OfferId, out int position))
            {
                values[position] = value;
                continue;
            }
            positions.Add(value.OfferId, values.Count);
            values.Add(value);
        }
        return Array.AsReadOnly(values.ToArray());
    }

    private static MarketplaceOfferSnapshot SnapshotOf(MarketplaceOffer value) => new(
        value.OfferId,
        value.Status,
        value.WireType,
        value.Kind,
        value.Data is null ? null : SnapshotOf(value.Data),
        value.WallData,
        value.UniqueSerialNumber,
        value.UniqueSeriesSize,
        value.IsUsed,
        value.Price,
        value.MinutesRemaining,
        value.AveragePrice,
        value.TradeVolume,
        value.Offers,
        value.StatusTimeMilliseconds);

    private static ItemDataSnapshot SnapshotOf(ItemData value)
    {
        ItemDataSnapshot data = SnapshotFactory.From(value);
        return data with
        {
            MapEntries = data.MapEntries is null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(
                        data.MapEntries,
                        StringComparer.Ordinal)),
            StringValues = data.StringValues is null
                ? null
                : ReadOnly(data.StringValues),
            IntValues = data.IntValues is null
                ? null
                : ReadOnly(data.IntValues),
            HighScores = data.HighScores is null
                ? null
                : ReadOnly(data.HighScores.Select(score => score with
                {
                    Names = ReadOnly(score.Names)
                }))
        };
    }

    private static bool Equivalent(
        IReadOnlyList<MarketplaceOfferSnapshot> left,
        IReadOnlyList<MarketplaceOfferSnapshot> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            MarketplaceOfferSnapshot first = left[index];
            MarketplaceOfferSnapshot second = right[index];
            if (first.OfferId != second.OfferId ||
                first.Status != second.Status ||
                first.WireType != second.WireType ||
                first.Kind != second.Kind ||
                first.WallData != second.WallData ||
                first.UniqueSerialNumber != second.UniqueSerialNumber ||
                first.UniqueSeriesSize != second.UniqueSeriesSize ||
                first.IsUsed != second.IsUsed ||
                first.Price != second.Price ||
                first.MinutesRemaining != second.MinutesRemaining ||
                first.AveragePrice != second.AveragePrice ||
                first.TradeVolume != second.TradeVolume ||
                first.Offers != second.Offers ||
                first.StatusTimeMilliseconds != second.StatusTimeMilliseconds ||
                !Equivalent(first.Data, second.Data))
            {
                return false;
            }
        }
        return true;
    }

    private static bool Equivalent(ItemDataSnapshot? left, ItemDataSnapshot? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null ||
            left.Type != right.Type ||
            left.Flags != right.Flags ||
            left.Value != right.Value ||
            left.State != right.State ||
            left.IsLimitedRare != right.IsLimitedRare ||
            left.UniqueSerialNumber != right.UniqueSerialNumber ||
            left.UniqueSeriesSize != right.UniqueSeriesSize ||
            left.UniqueLimitedData != right.UniqueLimitedData ||
            left.VoteResult != right.VoteResult ||
            left.ScoreType != right.ScoreType ||
            left.ClearType != right.ClearType ||
            left.Hits != right.Hits ||
            left.Target != right.Target ||
            !Equivalent(left.MapEntries, right.MapEntries) ||
            !Equivalent(left.StringValues, right.StringValues) ||
            !Equivalent(left.IntValues, right.IntValues) ||
            !Equivalent(left.HighScores, right.HighScores))
        {
            return false;
        }
        return true;
    }

    private static bool Equivalent(
        IReadOnlyDictionary<string, string>? left,
        IReadOnlyDictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        return left.All(pair =>
            right.TryGetValue(pair.Key, out string? value) &&
            value == pair.Value);
    }

    private static bool Equivalent<T>(
        IReadOnlyList<T>? left,
        IReadOnlyList<T>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        return left is not null && right is not null && left.SequenceEqual(right);
    }

    private static bool Equivalent(
        IReadOnlyList<HighScoreSnapshot>? left,
        IReadOnlyList<HighScoreSnapshot>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            if (left[index].Score != right[index].Score ||
                !left[index].Names.SequenceEqual(right[index].Names))
            {
                return false;
            }
        }
        return true;
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(
        Dictionary<TKey, TValue> values) where TKey : notnull =>
        new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>(values));
}
