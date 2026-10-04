using Qx.Interception;

namespace Qx.Game.Application;

/// <summary>Represents a request for the habbicon state view.</summary>
/// <remarks>
/// Used by the <c>habbicons.state</c> query. The application keeps the four most recent habbicon
/// snapshots of the active hotel session, and a retained snapshot can no longer be read once it is
/// dropped or the session changes.
/// </remarks>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state.</param>
public sealed record HabbiconStateRequest(long? SnapshotRevision = null);

/// <summary>Represents a summary of the habbicon shop and the user's habbicons.</summary>
/// <param name="ShopLoaded">Whether the habbicon shop has been received in the current session.</param>
/// <param name="UserLoaded">Whether the user's habbicon inventory has been received in the current session.</param>
/// <param name="Enabled">Whether habbicons are enabled on the hotel, as read from the <c>habbicons.enabled</c> game data flag.</param>
/// <param name="CollectionCount">The number of collections in the shop.</param>
/// <param name="IconCount">The number of habbicons across all collections.</param>
/// <param name="OwnedCount">The number of habbicons the user owns, favorite or not.</param>
/// <param name="FavoriteCount">The number of habbicons the user has marked as a favorite.</param>
/// <param name="ClaimableCount">The number of habbicons the user has earned and not claimed yet.</param>
/// <param name="RecentCount">The number of habbicons in the user's recently used list.</param>
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

/// <summary>Represents a habbicon, the small pictures that can be sent in a private conversation.</summary>
/// <param name="Ordinal">The zero-based position of the habbicon in the list it was read from, or 0 for a single habbicon.</param>
/// <param name="HabbiconId">The habbicon id.</param>
/// <param name="Name">The habbicon name.</param>
/// <param name="CollectionId">The id of the collection the habbicon belongs to.</param>
/// <param name="State">The user's state for the habbicon, 0 for locked, 1 for claimable, 2 for owned and 3 for favorite.</param>
/// <param name="PriceCredits">The price in credits, or 0 when it is not sold for credits.</param>
/// <param name="PriceActivityPoints">The price in the seasonal currency, or 0 when it has no such price.</param>
/// <param name="ActivityPointType">The activity point type of <paramref name="PriceActivityPoints"/>.</param>
/// <param name="IsOwned">Whether the user owns the habbicon, favorite or not.</param>
/// <param name="IsClaimable">Whether the habbicon is earned and waiting to be claimed.</param>
/// <param name="IsPurchasable">Whether the habbicon can be bought, which requires it to be locked and priced in at least one currency.</param>
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

/// <summary>Represents a habbicon collection, a themed set that pays out a reward habbicon once it is complete.</summary>
/// <param name="Ordinal">The zero-based position of the collection in the shop.</param>
/// <param name="CollectionId">The collection id.</param>
/// <param name="Name">The collection name.</param>
/// <param name="Completed">Whether every habbicon in the collection is owned.</param>
/// <param name="RewardHabbiconId">The id of the habbicon awarded for completing the collection, or 0 when there is no reward.</param>
/// <param name="RewardState">The state of the reward habbicon, 0 for locked, 1 for claimable, 2 for owned and 3 for favorite.</param>
/// <param name="PriceCredits">The price of the whole collection in credits.</param>
/// <param name="PriceActivityPoints">The price of the whole collection in the seasonal currency.</param>
/// <param name="ActivityPointType">The activity point type of <paramref name="PriceActivityPoints"/>.</param>
/// <param name="RewardIsClaimable">Whether the completion reward is waiting to be claimed.</param>
/// <param name="Habbicons">The habbicons in the collection, with the user's state applied.</param>
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

    /// <summary>Gets the habbicons in the collection, with the user's state applied.</summary>
    /// <remarks>The list is copied when set, and a <see langword="null"/> list or entry throws <see cref="ArgumentNullException"/>.</remarks>
    public IReadOnlyList<HabbiconEntryView> Habbicons
    {
        get => habbicons;
        init => habbicons = HabbiconApplicationModelFreeze.References(value, nameof(Habbicons));
    }
}

/// <summary>Represents the use of a habbicon by an avatar in the room.</summary>
/// <param name="RoomIndex">The room index of the avatar that used the habbicon.</param>
/// <param name="HabbiconId">The id of the habbicon that was used.</param>
public sealed record HabbiconRoomUseView(int RoomIndex, int HabbiconId);

/// <summary>Represents the habbicon state read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>habbicons.state</c> query. Every revision also increases when the state is
/// cleared for a new or closed hotel session.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="Revision">The habbicon state revision, which increases with every change.</param>
/// <param name="ShopRevision">The revision of the habbicon shop, which increases each time the shop is received.</param>
/// <param name="UserRevision">The revision of the user's habbicon inventory, which increases each time the inventory is received.</param>
/// <param name="StatusRevision">The revision of habbicon status changes, which increases each time the state of one of the user's habbicons changes.</param>
/// <param name="InfoRevision">The revision of habbicon details, which increases each time the details of a habbicon are received.</param>
/// <param name="RoomRevision">The revision of room habbicon use, which increases each time an avatar in the room uses a habbicon.</param>
/// <param name="SettingsRevision">The revision of the habbicon setting, which increases each time the enabled flag changes.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot the view was read from.</param>
/// <param name="Vault">The summary of the habbicon shop and the user's habbicons.</param>
/// <param name="RecentHabbiconIds">The ids of the habbicons the user used most recently, newest first.</param>
/// <param name="LastInfo">The last habbicon details received, or <see langword="null"/> when none have been received.</param>
/// <param name="LastRoomUse">The last habbicon use seen in the room, or <see langword="null"/> when none has been seen.</param>
public sealed record HabbiconStateView(
    bool Connected,
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

    /// <summary>Gets the ids of the habbicons the user used most recently, newest first.</summary>
    /// <remarks>The list is copied when set, and a <see langword="null"/> list throws <see cref="ArgumentNullException"/>.</remarks>
    public IReadOnlyList<int> RecentHabbiconIds
    {
        get => recent_habbicon_ids;
        init => recent_habbicon_ids =
            HabbiconApplicationModelFreeze.Values(value, nameof(RecentHabbiconIds));
    }
}

/// <summary>Represents a request for a page of habbicon collections.</summary>
/// <remarks>Used by the <c>habbicons.collections.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first collection to return.</param>
/// <param name="Limit">The maximum number of collections to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record HabbiconCollectionPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of habbicon collections read from one snapshot.</summary>
/// <remarks>Returned by the <c>habbicons.collections.list</c> query. The collections keep the order in which the server sent them.</remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The habbicon state revision, which increases with every change.</param>
/// <param name="ShopRevision">The revision of the habbicon shop, which increases each time the shop is received.</param>
/// <param name="UserRevision">The revision of the user's habbicon inventory, which increases each time the inventory is received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Vault">The summary of the habbicon shop and the user's habbicons.</param>
/// <param name="Total">The number of collections in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first collection in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more collections.</param>
/// <param name="Collections">The collections in the page.</param>
public sealed record HabbiconCollectionPage(
    bool Connected,
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

    /// <summary>Gets the collections in the page.</summary>
    /// <remarks>The list is copied when set, and a <see langword="null"/> list or entry throws <see cref="ArgumentNullException"/>.</remarks>
    public IReadOnlyList<HabbiconCollectionView> Collections
    {
        get => collections;
        init => collections =
            HabbiconApplicationModelFreeze.References(value, nameof(Collections));
    }
}

/// <summary>Represents a request for a page of habbicons across all collections.</summary>
/// <remarks>Used by the <c>habbicons.entries.list</c> query.</remarks>
/// <param name="Offset">The zero-based index of the first habbicon to return.</param>
/// <param name="Limit">The maximum number of habbicons to return, from 1 to 500.</param>
/// <param name="SnapshotRevision">The revision of a retained snapshot to read, or <see langword="null"/> to capture the current state. Required when <paramref name="Offset"/> is greater than 0.</param>
public sealed record HabbiconEntryPageRequest(
    int Offset = 0,
    int Limit = 100,
    long? SnapshotRevision = null);

/// <summary>Represents a page of habbicons across all collections, read from one snapshot.</summary>
/// <remarks>
/// Returned by the <c>habbicons.entries.list</c> query. The habbicons are listed collection by
/// collection in the order the server sent them, with the user's state applied.
/// </remarks>
/// <param name="Connected">Whether the snapshot belongs to the active hotel session.</param>
/// <param name="SessionGeneration">The generation of the hotel session the snapshot belongs to.</param>
/// <param name="StateRevision">The habbicon state revision, which increases with every change.</param>
/// <param name="ShopRevision">The revision of the habbicon shop, which increases each time the shop is received.</param>
/// <param name="UserRevision">The revision of the user's habbicon inventory, which increases each time the inventory is received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading the next page.</param>
/// <param name="Vault">The summary of the habbicon shop and the user's habbicons.</param>
/// <param name="Total">The number of habbicons in the snapshot.</param>
/// <param name="Offset">The zero-based index of the first habbicon in the page.</param>
/// <param name="NextOffset">The offset of the next page, or <see langword="null"/> when there are no more habbicons.</param>
/// <param name="Entries">The habbicons in the page.</param>
public sealed record HabbiconEntryPage(
    bool Connected,
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

    /// <summary>Gets the habbicons in the page.</summary>
    /// <remarks>The list is copied when set, and a <see langword="null"/> list or entry throws <see cref="ArgumentNullException"/>.</remarks>
    public IReadOnlyList<HabbiconEntryView> Entries
    {
        get => entries;
        init => entries = HabbiconApplicationModelFreeze.References(value, nameof(Entries));
    }
}

/// <summary>Represents a request to reload the habbicon shop from the server.</summary>
/// <remarks>
/// Used by the <c>habbicons.shop.refresh</c> operation. Habbicon refreshes run one at a time. The
/// call waits until earlier shop requests have been answered, sends one request and returns the
/// first shop snapshot received after it. The timeout covers the whole wait.
/// </remarks>
/// <param name="Limit">The maximum number of collections and of habbicons in the first pages of the result, from 1 to 500.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconShopRefreshRequest(
    int Limit = 100,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a habbicon shop refresh.</summary>
/// <remarks>Returned by the <c>habbicons.shop.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the shop snapshot was observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The habbicon state revision after the shop was received.</param>
/// <param name="ShopRevision">The revision of the habbicon shop that was received.</param>
/// <param name="UserRevision">The revision of the user's habbicon inventory when the shop was received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot, to pass when reading further pages.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="FirstCollections">The first page of collections from the refreshed snapshot.</param>
/// <param name="FirstEntries">The first page of habbicons from the refreshed snapshot.</param>
public sealed record HabbiconShopRefreshResult(
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

/// <summary>Represents a request for the details of one habbicon.</summary>
/// <remarks>
/// Used by the <c>habbicons.info.refresh</c> operation. Habbicon refreshes run one at a time. The
/// call waits until earlier requests for the same habbicon have been answered, sends one request and
/// returns the first response for the requested habbicon received after it. The timeout covers the
/// whole wait.
/// </remarks>
/// <param name="HabbiconId">The id of the habbicon.</param>
/// <param name="TimeoutMilliseconds">The maximum time to wait for the response, in milliseconds, from 1 to 120000.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconInfoRefreshRequest(
    int HabbiconId,
    int TimeoutMilliseconds = 10000,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the result of a habbicon details refresh.</summary>
/// <remarks>Returned by the <c>habbicons.info.refresh</c> operation.</remarks>
/// <param name="RefreshedAtUtc">The UTC time the result was created.</param>
/// <param name="ObservedAtUtc">The UTC time the details were observed.</param>
/// <param name="SessionGeneration">The generation of the hotel session the refresh ran in.</param>
/// <param name="StateRevision">The habbicon state revision after the details were received.</param>
/// <param name="InfoRevision">The revision of the habbicon details that were received.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the details.</param>
/// <param name="MessagesDispatched">The number of request messages the call sent.</param>
/// <param name="Habbicon">The habbicon details the server returned.</param>
public sealed record HabbiconInfoRefreshResult(
    DateTimeOffset RefreshedAtUtc,
    DateTimeOffset ObservedAtUtc,
    long SessionGeneration,
    long StateRevision,
    long InfoRevision,
    long SnapshotRevision,
    int MessagesDispatched,
    HabbiconEntryView Habbicon);

/// <summary>Represents a request to buy one habbicon.</summary>
/// <remarks>
/// Used by the <c>habbicons.buy</c> operation, which sends the request and returns without waiting.
/// The resulting state change is published as a <see cref="HabbiconChanged"/>.
/// </remarks>
/// <param name="HabbiconId">The id of the habbicon to buy.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconBuyActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to buy a whole habbicon collection.</summary>
/// <remarks>
/// Used by the <c>habbicons.collection.buy</c> operation, which sends the request and returns
/// without waiting. The resulting state changes are published as <see cref="HabbiconChanged"/> values.
/// </remarks>
/// <param name="CollectionId">The id of the collection to buy.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconCollectionBuyActionRequest(
    int CollectionId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to claim an earned habbicon.</summary>
/// <remarks>
/// Used by the <c>habbicons.claim</c> operation, which sends the request and returns without
/// waiting. The resulting state change is published as a <see cref="HabbiconChanged"/>.
/// </remarks>
/// <param name="HabbiconId">The id of the habbicon to claim.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconClaimActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to mark a habbicon as a favorite.</summary>
/// <remarks>
/// Used by the <c>habbicons.favorite</c> operation, which sends the request and returns without
/// waiting. The resulting state change is published as a <see cref="HabbiconChanged"/>.
/// </remarks>
/// <param name="HabbiconId">The id of the habbicon.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconFavoriteActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents a request to remove a habbicon from the favorites.</summary>
/// <remarks>
/// Used by the <c>habbicons.unfavorite</c> operation, which sends the request and returns without
/// waiting. The resulting state change is published as a <see cref="HabbiconChanged"/>.
/// </remarks>
/// <param name="HabbiconId">The id of the habbicon.</param>
/// <param name="ExpectedSessionGeneration">The hotel session generation the call must run in, or <see langword="null"/> to use the active session.</param>
public sealed record HabbiconUnfavoriteActionRequest(
    int HabbiconId,
    long? ExpectedSessionGeneration = null);

/// <summary>Represents the receipt for a habbicon action that was sent.</summary>
/// <remarks>
/// Returned by the <c>habbicons.buy</c>, <c>habbicons.collection.buy</c>, <c>habbicons.claim</c>,
/// <c>habbicons.favorite</c> and <c>habbicons.unfavorite</c> operations.
/// </remarks>
/// <param name="DispatchedAtUtc">The UTC time the request was sent.</param>
/// <param name="SessionGeneration">The generation of the hotel session the request was sent in.</param>
/// <param name="MessagesDispatched">The number of messages sent.</param>
public sealed record HabbiconDispatchResult(
    DateTimeOffset DispatchedAtUtc,
    long SessionGeneration,
    int MessagesDispatched);

/// <summary>Specifies the kind of a habbicon change.</summary>
public enum HabbiconChangeKind
{
    /// <summary>The habbicon shop was received.</summary>
    ShopSnapshot,
    /// <summary>The user's habbicon inventory was received.</summary>
    InventorySnapshot,
    /// <summary>The state of one of the user's habbicons changed, for example after a purchase or a claim.</summary>
    Status,
    /// <summary>The details of a habbicon were received.</summary>
    Info,
    /// <summary>An avatar in the room used a habbicon.</summary>
    RoomUsed,
    /// <summary>The habbicon enabled flag changed.</summary>
    Settings,
    /// <summary>The habbicon state was cleared for a new or closed hotel session.</summary>
    Reset
}

/// <summary>Represents a change in the state of one of the user's habbicons.</summary>
/// <param name="HabbiconId">The id of the habbicon.</param>
/// <param name="State">The new state, 0 for locked, 1 for claimable, 2 for owned and 3 for favorite.</param>
/// <param name="Gained">Whether the user gained the habbicon with this change, either as a new habbicon or by claiming it.</param>
public sealed record HabbiconStatusView(int HabbiconId, int State, bool Gained);

/// <summary>Represents a change to the habbicon state.</summary>
/// <remarks>Published by the <c>habbicons.changed</c> event.</remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The UTC time the change was published.</param>
/// <param name="Connected">Whether a hotel session is active.</param>
/// <param name="SessionGeneration">The generation of the hotel session the change belongs to.</param>
/// <param name="Revision">The habbicon state revision after the change.</param>
/// <param name="SourceRevision">The revision of the part that changed, or <paramref name="Revision"/> for <see cref="HabbiconChangeKind.Reset"/>.</param>
/// <param name="SnapshotRevision">The revision of the retained snapshot that holds the change, or <see langword="null"/> when no snapshot could be stored.</param>
/// <param name="Vault">The summary of the habbicon shop and the user's habbicons after the change.</param>
/// <param name="Habbicon">The received habbicon details for <see cref="HabbiconChangeKind.Info"/>; otherwise, <see langword="null"/>.</param>
/// <param name="Status">The habbicon state change for <see cref="HabbiconChangeKind.Status"/>; otherwise, <see langword="null"/>.</param>
/// <param name="RoomUse">The habbicon use for <see cref="HabbiconChangeKind.RoomUsed"/>; otherwise, <see langword="null"/>.</param>
public sealed record HabbiconChanged(
    HabbiconChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    bool Connected,
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
