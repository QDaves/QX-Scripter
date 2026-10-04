using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets every badge the local user owns, as far as the inventory has been loaded.
    /// </summary>
    /// <remarks>
    /// Before the inventory is loaded it holds only the badges received during the session, and
    /// it can be incomplete while a load is in progress. Check <see cref="IsBadgeInventoryLoaded"/>
    /// and <see cref="IsBadgeInventoryStale"/> to tell a complete list from a partial one. Every
    /// read returns a snapshot copy, not a live view.
    /// </remarks>
    public IEnumerable<OwnedBadge> OwnedBadges => BadgeInventory.OwnedBadges;

    /// <summary>
    /// Gets the equipped badge sets cached so far, one entry per user the server has reported
    /// badges for.
    /// </summary>
    /// <remarks>
    /// It is empty until the server pushes some. Every read returns a snapshot copy, not a live
    /// view.
    /// </remarks>
    public IEnumerable<UserBadges> SelectedBadgeSets => BadgeInventory.SelectedBadgeSets;

    /// <summary>
    /// Gets whether every fragment of the badge inventory has arrived in the current session.
    /// </summary>
    public bool IsBadgeInventoryLoaded => BadgeInventory.IsLoaded;

    /// <summary>
    /// Gets whether a badge inventory request is pending or its fragments are still arriving.
    /// </summary>
    /// <remarks>
    /// The inventory can be both loaded and loading, when a reload has been started over an
    /// existing collection.
    /// </remarks>
    public bool IsBadgeInventoryLoading => BadgeInventory.IsLoading;

    /// <summary>
    /// Gets whether <see cref="OwnedBadges"/> holds badges that a completed load has not yet
    /// confirmed.
    /// </summary>
    /// <remarks>
    /// The entries can still be read, but they may be incomplete or out of date until the next
    /// load completes.
    /// </remarks>
    public bool IsBadgeInventoryStale => BadgeInventory.IsStale;

    /// <summary>Finds an owned badge by its badge code, ignoring case.</summary>
    /// <param name="code">The badge code, for example <c>ACH_BasicClub1</c>.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="code"/> is empty or white space.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    public OwnedBadge? GetOwnedBadge(string code) => BadgeInventory.Badge(code);

    /// <summary>
    /// Finds an owned badge by its 32-bit badge id.
    /// </summary>
    /// <remarks>
    /// Badges whose native id does not fit in 32 bits are never matched by this overload.
    /// </remarks>
    /// <param name="badgeId">The badge id.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    public OwnedBadge? GetOwnedBadge(int badgeId) => BadgeInventory.Badge(badgeId);

    /// <summary>Finds an owned badge by its badge id.</summary>
    /// <param name="badgeId">The badge id.</param>
    /// <returns>The badge, or <see langword="null"/> when the user does not own it.</returns>
    public OwnedBadge? GetOwnedBadge(Id badgeId) => BadgeInventory.Badge(badgeId);

    /// <summary>
    /// Gets the cached badge set one user has equipped on their profile, exactly as the server last
    /// pushed it.
    /// </summary>
    /// <remarks>
    /// Nothing is requested, so it only answers for users whose badges have already been seen.
    /// </remarks>
    /// <param name="userId">The user's account id.</param>
    /// <returns>
    /// The badge set, or <see langword="null"/> when no badges have been seen for this user.
    /// </returns>
    public UserBadges? GetCachedSelectedBadgeSet(Id userId) =>
        BadgeInventory.SelectedBadgeSet(userId);

    /// <summary>
    /// Gets the cached badges one user has equipped, as a plain list.
    /// </summary>
    /// <remarks>Nothing is requested.</remarks>
    /// <param name="userId">The user's account id.</param>
    /// <returns>
    /// A snapshot copy of the badges, or an empty list when none have been seen for this user.
    /// </returns>
    public IReadOnlyList<SelectedBadge> GetCachedSelectedBadges(Id userId) =>
        BadgeInventory.SelectedBadgesFor(userId);

    /// <summary>
    /// Loads the local user's badge inventory, requesting it if necessary, and waits until every
    /// fragment has arrived.
    /// </summary>
    /// <remarks>
    /// When the inventory is already loaded it returns the cached collection without touching the
    /// network. Concurrent callers share one request rather than each sending their own.
    /// </remarks>
    /// <param name="timeoutMs">
    /// The time to wait for the load to finish, in milliseconds. It must be positive.
    /// </param>
    /// <returns>
    /// The complete owned badge collection.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the fragments did not all arrive within the timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the application runtime is not active, the connection closed during the load, or the
    /// fragment stream can no longer be correlated to a request. The last case is a
    /// <see cref="Qx.Game.FragmentedLoadCorrelationException"/>, which resolves once the next
    /// complete inventory arrives or the session reconnects.
    /// </exception>
    public Task<IReadOnlyCollection<OwnedBadge>> EnsureBadgeInventoryLoaded(
        int timeoutMs = 10000) =>
        BadgeInventory.EnsureLoadedAsync(timeoutMs, Ct);

    /// <summary>
    /// Gets the local user's badge collection, loading it first when it is not loaded yet.
    /// </summary>
    /// <remarks>
    /// Identical to <see cref="EnsureBadgeInventoryLoaded(int)"/>; kept as the more discoverable
    /// name.
    /// </remarks>
    /// <param name="timeoutMs">The time to wait for the load to finish, in milliseconds.</param>
    /// <returns>The complete owned badge collection.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="Qx.Game.RequestTimeoutException">Thrown when the fragments did not all arrive within the timeout.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped while waiting.</exception>
    public Task<IReadOnlyCollection<OwnedBadge>> GetUserBadges(int timeoutMs = 10000) =>
        EnsureBadgeInventoryLoaded(timeoutMs);

    /// <summary>
    /// Starts a filter, sort and projection query over the badges currently cached as owned.
    /// </summary>
    /// <remarks>
    /// Nothing is loaded: a query over an inventory that has not been loaded holds only the badges
    /// received during the session.
    /// </remarks>
    /// <returns>A query over a snapshot of the owned badges.</returns>
    public BadgeQuery QueryOwnedBadges() =>
        new(BadgeInventory.OwnedBadges);

    /// <summary>Starts a badge query over a caller-supplied sequence instead of the cache.</summary>
    /// <param name="badges">The badges to query.</param>
    /// <returns>A query over the given badges.</returns>
    public BadgeQuery QueryOwnedBadges(IEnumerable<OwnedBadge> badges) =>
        new(badges);

    /// <summary>
    /// Starts a query over the badges one user has equipped, taken from the cache.
    /// </summary>
    /// <remarks>
    /// Nothing is requested: for a user whose badges have not been seen the query is empty.
    /// </remarks>
    /// <param name="userId">The user's account id.</param>
    /// <returns>A query over a snapshot of that user's equipped badges.</returns>
    public SelectedBadgeQuery QuerySelectedBadges(Id userId) =>
        new(BadgeInventory.SelectedBadgesFor(userId));

    /// <summary>
    /// Starts a selected-badge query over a caller-supplied sequence instead of the cache.
    /// </summary>
    /// <param name="badges">The badges to query.</param>
    /// <returns>A query over the given badges.</returns>
    public SelectedBadgeQuery QuerySelectedBadges(IEnumerable<SelectedBadge> badges) =>
        new(badges);

    /// <summary>
    /// Registers a handler that runs each time a badge inventory load completes.
    /// </summary>
    /// <remarks>
    /// Reloads count too, so it can fire more than once per session.
    /// </remarks>
    /// <param name="handler">The handler to call with no arguments; read the owned badges afterwards.</param>
    /// <returns>
    /// A handle that removes the handler when disposed. The subscription is also removed when
    /// the script stops, so the handle only has to be kept to unsubscribe earlier.
    /// </returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnBadgeInventoryLoaded(Action handler)
        => Subscribe(
            handler,
            value => BadgeInventory.Loaded += value,
            value => BadgeInventory.Loaded -= value);

    /// <summary>
    /// Registers a handler that runs when a badge that is not yet in <see cref="OwnedBadges"/> is
    /// received.
    /// </summary>
    /// <remarks>
    /// It fires for badges the server hands out and for badges granted by achievements, not for
    /// badges read from an inventory load. Before the inventory has loaded, a badge the user already
    /// owned can also count as added.
    /// </remarks>
    /// <param name="handler">The handler to call with the badge.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnOwnedBadgeAdded(Action<OwnedBadge> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.BadgeAdded += value,
            value => BadgeInventory.BadgeAdded -= value);

    /// <summary>
    /// Registers a handler that runs when an already known badge is received again.
    /// </summary>
    /// <remarks>
    /// The badge usually carries different data, for example a new owner count or rarity.
    /// </remarks>
    /// <param name="handler">The handler to call with the updated badge.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnOwnedBadgeUpdated(Action<OwnedBadge> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.BadgeUpdated += value,
            value => BadgeInventory.BadgeUpdated -= value);

    /// <summary>
    /// Registers a handler that runs when an achievement level takes a badge out of
    /// <see cref="OwnedBadges"/>.
    /// </summary>
    /// <remarks>
    /// The removed badge is usually the previous level of that achievement. A badge that drops out
    /// of <see cref="OwnedBadges"/> because the inventory is reloaded or the session ends does not
    /// raise it.
    /// </remarks>
    /// <param name="handler">The handler to call with the removed badge.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnOwnedBadgeRemoved(Action<OwnedBadge> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.BadgeRemoved += value,
            value => BadgeInventory.BadgeRemoved -= value);

    /// <summary>
    /// Registers a handler that runs when the server reports the badges a user has equipped.
    /// </summary>
    /// <remarks>
    /// It fires for any user, not only the local one, so it is the hook for watching the badge
    /// sets of avatars in the room.
    /// </remarks>
    /// <param name="handler">The handler to call with the badge set, which carries its own user id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the script globals have already been disposed.</exception>
    public IDisposable OnSelectedBadgesUpdated(Action<UserBadges> handler)
        => Subscribe(
            handler,
            value => BadgeInventory.SelectedBadgesUpdated += value,
            value => BadgeInventory.SelectedBadgesUpdated -= value);
}
