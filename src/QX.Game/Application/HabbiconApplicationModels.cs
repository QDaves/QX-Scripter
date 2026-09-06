using Qx.Interception;

namespace Qx.Game.Application;

public sealed record HabbiconStateRequest(long? SnapshotRevision = null);

public sealed record HabbiconVaultSummary(
    bool ShopLoaded,
    bool UserLoaded,
    bool Enabled,
    int CollectionCount,
    int IconCount,
    int OwnedCount,
    int FavoriteCount,
    int ClaimableCount,
    int RecentCount);

public sealed record HabbiconEntryView(
    int Ordinal,
    int HabbiconId,
    string Name,
    int CollectionId,
    int State,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType,
    bool IsOwned,
    bool IsClaimable,
    bool IsPurchasable);

public sealed record HabbiconCollectionView(
    int Ordinal,
    int CollectionId,
    string Name,
    bool Completed,
    int RewardHabbiconId,
    int RewardState,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType,
    bool RewardIsClaimable,
    IReadOnlyList<HabbiconEntryView> Habbicons)
{
    private IReadOnlyList<HabbiconEntryView> habbicons =
        HabbiconApplicationModelFreeze.References(Habbicons, nameof(Habbicons));

    public IReadOnlyList<HabbiconEntryView> Habbicons
    {
        get => habbicons;
        init => habbicons = HabbiconApplicationModelFreeze.References(value, nameof(Habbicons));
    }
}

public sealed record HabbiconRoomUseView(int RoomIndex, int HabbiconId);

public sealed record HabbiconStateView(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long ShopRevision,
    long UserRevision,
    long StatusRevision,
    long InfoRevision,
    long RoomRevision,
    long SettingsRevision,
    long SnapshotRevision,
    HabbiconVaultSummary Vault,
    IReadOnlyList<int> RecentHabbiconIds,
    HabbiconEntryView? LastInfo,
    HabbiconRoomUseView? LastRoomUse)
{
    private IReadOnlyList<int> recent_habbicon_ids =
        HabbiconApplicationModelFreeze.Values(RecentHabbiconIds, nameof(RecentHabbiconIds));

    public IReadOnlyList<int> RecentHabbiconIds
    {
        get => recent_habbicon_ids;
        init => recent_habbicon_ids =
            HabbiconApplicationModelFreeze.Values(value, nameof(RecentHabbiconIds));
    }
}

public sealed record HabbiconCollectionPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record HabbiconCollectionPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long ShopRevision,
    long UserRevision,
    long SnapshotRevision,
    HabbiconVaultSummary Vault,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<HabbiconCollectionView> Collections)
{
    private IReadOnlyList<HabbiconCollectionView> collections =
        HabbiconApplicationModelFreeze.References(Collections, nameof(Collections));

    public IReadOnlyList<HabbiconCollectionView> Collections
    {
        get => collections;
        init => collections =
            HabbiconApplicationModelFreeze.References(value, nameof(Collections));
    }
}

public sealed record HabbiconEntryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

public sealed record HabbiconEntryPage(
    bool Connected,
    ClientType? Client,
    long SessionGeneration,
    long StateRevision,
    long ShopRevision,
    long UserRevision,
    long SnapshotRevision,
    HabbiconVaultSummary Vault,
    int Total,
    int Offset,
    int? NextOffset,
    IReadOnlyList<HabbiconEntryView> Entries)
{
    private IReadOnlyList<HabbiconEntryView> entries =
        HabbiconApplicationModelFreeze.References(Entries, nameof(Entries));

    public IReadOnlyList<HabbiconEntryView> Entries
    {
        get => entries;
        init => entries = HabbiconApplicationModelFreeze.References(value, nameof(Entries));
    }
}

public sealed record HabbiconShopRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconShopRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long ShopRevision,
    long UserRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    HabbiconCollectionPage FirstCollections,
    HabbiconEntryPage FirstEntries);

public sealed record HabbiconInfoRefreshRequest(
    int HabbiconId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconInfoRefreshResult(
    ClientType Client,
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long InfoRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    HabbiconEntryView Habbicon);

public sealed record HabbiconBuyActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconCollectionBuyActionRequest(
    int CollectionId,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconClaimActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconFavoriteActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconUnfavoriteActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

public sealed record HabbiconDispatchResult(
    ClientType Client,
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    int MessagesDispatched);

public enum HabbiconChangeKind
{
    ShopSnapshot,
    InventorySnapshot,
    Status,
    Info,
    RoomUsed,
    Settings,
    Reset
}

public sealed record HabbiconStatusView(int HabbiconId, int State, bool Gained);

public sealed record HabbiconChanged(
    HabbiconChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    ClientType? Client,
    long SessionGeneration,
    long Revision,
    long SourceRevision,
    long? SnapshotRevision,
    HabbiconVaultSummary? Vault,
    HabbiconEntryView? Habbicon,
    HabbiconStatusView? Status,
    HabbiconRoomUseView? RoomUse);

internal static class HabbiconApplicationModelFreeze
{
    public static IReadOnlyList<T> References<T>(IReadOnlyList<T> values, string name)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        var copy = new T[values.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            T value = values[index];
            ArgumentNullException.ThrowIfNull(value, name);
            copy[index] = value;
        }
        return Array.AsReadOnly(copy);
    }

    public static IReadOnlyList<T> Values<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        var copy = new T[values.Count];
        for (int index = 0; index < copy.Length; index++)
            copy[index] = values[index];
        return Array.AsReadOnly(copy);
    }
}
