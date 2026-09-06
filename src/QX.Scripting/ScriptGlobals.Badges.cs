using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <content>
/// Badges: the local user's badge collection and the badge slots users have equipped. Available on
/// both the Flash and the Unity client.
/// <para>
/// Two different things live here. The <em>owned badge</em> collection is the local user's own
/// inventory; it arrives in fragments and has to be loaded once before it is complete. The
/// <em>selected badge</em> sets are the up-to-five badges a user shows on their profile; they are
/// cached per user as the server pushes them for avatars in the room and for profiles that were
/// looked at, and are never fetched on their own by anything here.
/// </para>
/// <para>
/// Only the load helpers touch the network. Every other member reads the cache and returns a copy
/// taken under the tracker's lock.
/// </para>
/// </content>
public partial class ScriptGlobals
{
    /// <summary>
    /// Every badge the local user owns, as far as the inventory has been loaded. Empty until the
    /// inventory is loaded, and possibly incomplete while a load is in progress.
    /// </summary>
    /// <returns>A snapshot copy, not a live view.</returns>
    public IEnumerable<OwnedBadge> OwnedBadges => BadgeInventory.OwnedBadges;

    /// <summary>
    /// The equipped badge sets cached so far, one entry per user the server has reported badges
    /// for. Empty until the server pushes some.
    /// </summary>
    /// <returns>A snapshot copy, not a live view.</returns>
    public IEnumerable<UserBadges> SelectedBadgeSets => BadgeInventory.SelectedBadgeSets;

    /// <summary>
    /// Whether every fragment of the badge inventory has arrived, so the owned-badge collection is
    /// complete.
    /// </summary>
    public bool IsBadgeInventoryLoaded => BadgeInventory.IsLoaded;

    /// <summary>
    /// Whether a badge inventory load is in flight right now. It is possible for the inventory to
    /// be both loaded and loading, when a reload has been started over an existing collection.
    /// </summary>
    public bool IsBadgeInventoryLoading => BadgeInventory.IsLoading;

    /// <summary>
    /// Whether the currently held badges are left over from a previous, now-superseded load. The
    /// entries can still be read, but a badge added since then may be missing.
    /// </summary>
    public bool IsBadgeInventoryStale => BadgeInventory.IsStale;

    /// <summary>Finds an owned badge by its badge code, case-insensitively.</summary>
    /// <param name="code">The badge code, for example <c>ACH_BasicClub1</c>.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty or whitespace.</exception>
    public OwnedBadge? GetOwnedBadge(string code) => BadgeInventory.Badge(code);

    /// <summary>
    /// Finds an owned badge by its 32-bit badge id. Badges whose native id does not fit in 32 bits
    /// are skipped by this overload.
    /// </summary>
    /// <param name="badge_id">The badge id.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    public OwnedBadge? GetOwnedBadge(int badge_id) => BadgeInventory.Badge(badge_id);

    /// <summary>
    /// Finds an owned badge by its native badge id, which is 64-bit wide on the Unity client.
    /// </summary>
    /// <param name="badge_id">The native badge id.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    public OwnedBadge? GetOwnedBadge(Id badge_id) => BadgeInventory.Badge(badge_id);

    /// <summary>
    /// The cached badge set one user has equipped on their profile, exactly as the server last
    /// pushed it. Nothing is requested — this only answers for users whose badges have already
    /// been seen.
    /// </summary>
    /// <param name="user_id">The user's account id.</param>
    /// <returns>
    /// The badge set, or <see langword="null"/> when no badges have been seen for this user.
    /// </returns>
    public UserBadges? GetCachedSelectedBadgeSet(Id user_id) =>
        BadgeInventory.SelectedBadgeSet(user_id);

    /// <summary>
    /// The cached badges one user has equipped, as a plain list. Nothing is requested.
    /// </summary>
    /// <param name="user_id">The user's account id.</param>
    /// <returns>
    /// A snapshot copy of the badges, or an empty list when none have been seen for this user.
    /// </returns>
    public IReadOnlyList<SelectedBadge> GetCachedSelectedBadges(Id user_id) =>
        BadgeInventory.SelectedBadgesFor(user_id);

    /// <summary>
    /// Loads the local user's badge inventory, requesting it if necessary, and waits until every
    /// fragment has arrived.
    /// </summary>
    /// <param name="timeout_ms">
    /// How long to wait for the load to finish, in milliseconds. Must be positive.
    /// </param>
    /// <returns>
    /// The complete owned-badge collection. When the inventory is already loaded this returns the
    /// cached collection without touching the network. Concurrent callers share one request rather
    /// than each sending their own.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout_ms"/> is zero or negative.</exception>
    /// <exception cref="TimeoutException">The fragments did not all arrive within the timeout.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    /// <exception cref="InvalidOperationException">
    /// The connection closed mid-load, or the fragment stream can no longer be correlated to a
    /// request — the latter is a
    /// <see cref="Qx.Game.FragmentedLoadCorrelationException"/>, which resolves once the next
    /// complete inventory arrives or the session reconnects.
    /// </exception>
    public Task<IReadOnlyCollection<OwnedBadge>> EnsureBadgeInventoryLoaded(
        int timeout_ms = 10000) =>
        BadgeInventory.EnsureLoadedAsync(timeout_ms, Ct);

    /// <summary>
    /// The local user's badge collection, loading it first if it is not loaded yet. Identical to
    /// <see cref="EnsureBadgeInventoryLoaded(int)"/>; kept as the more discoverable name.
    /// </summary>
    /// <param name="timeout_ms">How long to wait for the load to finish, in milliseconds.</param>
    /// <returns>The complete owned-badge collection.</returns>
    /// <exception cref="TimeoutException">The fragments did not all arrive within the timeout.</exception>
    /// <exception cref="OperationCanceledException">The script was stopped while waiting.</exception>
    public Task<IReadOnlyCollection<OwnedBadge>> GetUserBadges(int timeout_ms = 10000) =>
        EnsureBadgeInventoryLoaded(timeout_ms);

    /// <summary>
    /// Starts a filter/sort/projection query over the badges currently cached as owned. This does
    /// not load anything: query an unloaded inventory and the result is empty.
    /// </summary>
    /// <returns>A query over a snapshot of the owned badges.</returns>
    public BadgeQuery QueryOwnedBadges() =>
        new(BadgeInventory.OwnedBadges);

    /// <summary>Starts a badge query over a caller-supplied sequence instead of the cache.</summary>
    /// <param name="badges">The badges to query.</param>
    /// <returns>A query over the given badges.</returns>
    public BadgeQuery QueryOwnedBadges(IEnumerable<OwnedBadge> badges) =>
        new(badges);

    /// <summary>
    /// Starts a query over the badges one user has equipped, taken from the cache. Nothing is
    /// requested: for a user whose badges have not been seen the query is empty.
    /// </summary>
    /// <param name="user_id">The user's account id.</param>
    /// <returns>A query over a snapshot of that user's equipped badges.</returns>
    public SelectedBadgeQuery QuerySelectedBadges(Id user_id) =>
        new(BadgeInventory.SelectedBadgesFor(user_id));

    /// <summary>
    /// Starts a selected-badge query over a caller-supplied sequence instead of the cache.
    /// </summary>
    /// <param name="badges">The badges to query.</param>
    /// <returns>A query over the given badges.</returns>
    public SelectedBadgeQuery QuerySelectedBadges(IEnumerable<SelectedBadge> badges) =>
        new(badges);

    /// <summary>
    /// Raised each time a badge inventory load completes, which includes reloads, so it can fire
    /// more than once per session.
    /// </summary>
    /// <param name="handler">Invoked with no arguments; read the owned badges afterwards.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also torn down when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnBadgeInventoryLoaded(Action handler)
        => Subscribe(
            handler,
            value => BadgeInventory.Loaded += value,
            value => BadgeInventory.Loaded -= value);

    /// <summary>
    /// Raised when a badge the user did not have appears — a newly earned badge, or one seen for
    /// the first time while the inventory loads.
    /// </summary>
    /// <param name="handler">Receives the badge.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnOwnedBadgeAdded(Action<OwnedBadge> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.BadgeAdded += value,
            value => BadgeInventory.BadgeAdded -= value);

    /// <summary>
    /// Raised when an already-known badge is re-reported with different data, for example a new
    /// owner count or rarity.
    /// </summary>
    /// <param name="handler">Receives the updated badge.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnOwnedBadgeUpdated(Action<OwnedBadge> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.BadgeUpdated += value,
            value => BadgeInventory.BadgeUpdated -= value);

    /// <summary>
    /// Raised when the server reports the badges a user has equipped — for any user, not only the
    /// local one. This is the hook for watching badge sets of avatars in the room.
    /// </summary>
    /// <param name="handler">Receives the badge set, which carries its own user id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The script globals have already been disposed.</exception>
    public IDisposable OnSelectedBadgesUpdated(Action<UserBadges> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.SelectedBadgesUpdated += value,
            value => BadgeInventory.SelectedBadgesUpdated -= value);
}
