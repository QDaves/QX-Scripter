using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets the habbicon manager, which tracks the small pictures that can be sent in a private
    /// conversation, the collections they are sold in, and which of them the local user owns.
    /// </summary>
    public HabbiconManager Habbicons => Game.Habbicons;

    /// <summary>Gets whether the hotel has habbicons switched on.</summary>
    /// <remarks>
    /// It is read from the <c>habbicons.enabled</c> flag in the hotel's game data.
    /// </remarks>
    public bool HabbiconsEnabled => Game.Habbicons.IsEnabled;

    /// <summary>
    /// Gets the habbicon collections, requesting the shop from the hotel when it has not been
    /// loaded yet.
    /// </summary>
    /// <remarks>
    /// Once the shop is loaded, the cached copy is read without a request. Every page of the
    /// cached snapshot is collected into one list.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>The collections, each with its habbicons.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the habbicon state changed while it was read.</exception>
    public async Task<IReadOnlyList<HabbiconCollection>> GetHabbiconCollections(int timeoutMs = 10000) =>
        (await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false)).Collections;

    /// <summary>
    /// Gets every habbicon the shop knows, with the local user's state applied.
    /// </summary>
    /// <remarks>
    /// The shop is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>Every habbicon the shop lists.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the habbicon state changed while it was read.</exception>
    public async Task<IReadOnlyList<Habbicon>> GetHabbicons(int timeoutMs = 10000) =>
        (await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false)).Habbicons;

    /// <summary>Gets the habbicons the local user owns, favorited or not.</summary>
    /// <remarks>
    /// The shop is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>The owned habbicons.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the habbicon state changed while it was read.</exception>
    public async Task<IReadOnlyList<Habbicon>> GetOwnedHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        return Array.AsReadOnly(snapshot.Habbicons.Where(icon => icon.IsOwned).ToArray());
    }

    /// <summary>Gets the habbicons that are earned and still waiting to be claimed.</summary>
    /// <remarks>
    /// The shop is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>The claimable habbicons.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the habbicon state changed while it was read.</exception>
    public async Task<IReadOnlyList<Habbicon>> GetClaimableHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        return Array.AsReadOnly(snapshot.Habbicons.Where(icon => icon.IsClaimable).ToArray());
    }

    /// <summary>Gets a habbicon by name, ignoring case.</summary>
    /// <remarks>
    /// The shop is requested from the hotel only when it has not been loaded yet.
    /// </remarks>
    /// <param name="name">The icon's name.</param>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>The icon, or <see langword="null"/> when the shop has no icon by that name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the habbicon state changed while it was read.</exception>
    public async Task<Habbicon?> GetHabbicon(string name, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(name);
        IReadOnlyList<Habbicon> icons = await GetHabbicons(timeoutMs);
        return icons.FirstOrDefault(icon =>
            icon.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Sends a request to buy a single habbicon.</summary>
    /// <remarks>
    /// It returns once the request is sent.
    /// </remarks>
    /// <param name="habbiconId">The id of the icon to buy.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void BuyHabbicon(int habbiconId) => Game.Habbicons.Buy(habbiconId);

    /// <summary>Sends a request to buy a whole habbicon collection.</summary>
    /// <remarks>
    /// It returns once the request is sent.
    /// </remarks>
    /// <param name="collectionId">The id of the collection to buy.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void BuyHabbiconCollection(int collectionId) => Game.Habbicons.BuyCollection(collectionId);

    /// <summary>Sends a request to claim a habbicon that has been earned.</summary>
    /// <remarks>
    /// It returns once the request is sent; <see cref="OnHabbiconGained"/> reports the icon once it
    /// is owned.
    /// </remarks>
    /// <param name="habbiconId">The id of the icon to claim.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void ClaimHabbicon(int habbiconId) => Game.Habbicons.Claim(habbiconId);

    /// <summary>
    /// Claims every habbicon that is earned and unclaimed.
    /// </summary>
    /// <remarks>
    /// The hotel confirms each claim with its own status change, so this returns once the requests
    /// are sent, one per claimable icon. Subscribe with <see cref="OnHabbiconGained"/> to see them
    /// land. The shop is requested from the hotel first when it has not been loaded yet.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds for the shop request.</param>
    /// <returns>The number of claims that were sent.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, the session changed, or the habbicon state changed while it was read.</exception>
    public async Task<int> ClaimAllHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        Habbicon[] claimable = snapshot.Habbicons.Where(icon => icon.IsClaimable).ToArray();
        foreach (Habbicon icon in claimable)
        {
            HabbiconDispatchResult result = await _application
                .InvokeAsync<HabbiconClaimActionRequest, HabbiconDispatchResult>(
                    ApplicationMemberIds.HabbiconClaim,
                    new HabbiconClaimActionRequest(
                        icon.HabbiconId,
                        snapshot.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            if (result.SessionGeneration != snapshot.SessionGeneration ||
                result.MessagesDispatched != 1)
            {
                throw new InvalidOperationException(
                    "The habbicon application returned an invalid claim result.");
            }
        }
        return claimable.Length;
    }

    /// <summary>Sends a request to mark an owned habbicon as a favorite.</summary>
    /// <param name="habbiconId">The id of the icon to favorite.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void FavoriteHabbicon(int habbiconId) => Game.Habbicons.Favorite(habbiconId);

    /// <summary>Sends a request to remove a habbicon from the favorites.</summary>
    /// <param name="habbiconId">The id of the icon to unfavorite.</param>
    /// <exception cref="InvalidOperationException">Thrown when there is no hotel session, or the application runtime is not active.</exception>
    public void UnfavoriteHabbicon(int habbiconId) => Game.Habbicons.Unfavorite(habbiconId);

    /// <summary>Registers a handler that runs whenever the state of one of the local user's habbicons changes.</summary>
    /// <param name="handler">The handler to call with the icon's id and its new state.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnHabbiconStatusChanged(Action<UserHabbiconStatusChanged> handler)
        => Subscribe(
            handler,
            value => Game.Habbicons.StatusChanged += value,
            value => Game.Habbicons.StatusChanged -= value);

    /// <summary>Registers a handler that runs whenever the local user gains a habbicon.</summary>
    /// <remarks>
    /// It runs for an icon that is new to the inventory and for a claimable icon that became
    /// owned, but not for the first inventory snapshot of a session.
    /// </remarks>
    /// <param name="handler">The handler to call with the icon's id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnHabbiconGained(Action<int> handler)
        => Subscribe(
            handler,
            value => Game.Habbicons.IconGained += value,
            value => Game.Habbicons.IconGained -= value);

    /// <summary>Registers a handler that runs whenever an avatar in the room uses a habbicon.</summary>
    /// <param name="handler">The handler to call with the room index of the user and the icon's id.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnHabbiconUsed(Action<RoomUseHabbicon> handler)
        => Subscribe(
            handler,
            value => Game.Habbicons.UsedInRoom += value,
            value => Game.Habbicons.UsedInRoom -= value);

    private async Task<HabbiconReadSnapshot> ReadHabbiconSnapshot(int timeout_ms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout_ms, "timeoutMs");
        HabbiconStateView state = await _application
            .InvokeAsync<HabbiconStateRequest, HabbiconStateView>(
                ApplicationMemberIds.HabbiconsState,
                new HabbiconStateRequest(),
                Ct)
            .ConfigureAwait(false);
        ValidateHabbiconState(state);

        HabbiconCollectionPage first_collections;
        HabbiconEntryPage first_entries;
        if (state.Vault.ShopLoaded)
        {
            first_collections = await _application
                .InvokeAsync<HabbiconCollectionPageRequest, HabbiconCollectionPage>(
                    ApplicationMemberIds.HabbiconCollectionsList,
                    new HabbiconCollectionPageRequest(
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            first_entries = await _application
                .InvokeAsync<HabbiconEntryPageRequest, HabbiconEntryPage>(
                    ApplicationMemberIds.HabbiconEntriesList,
                    new HabbiconEntryPageRequest(
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            ValidateHabbiconStatePage(state, first_collections);
            ValidateHabbiconStatePage(state, first_entries);
        }
        else
        {
            HabbiconShopRefreshResult refreshed = await _application
                .InvokeAsync<HabbiconShopRefreshRequest, HabbiconShopRefreshResult>(
                    ApplicationMemberIds.HabbiconShopRefresh,
                    new HabbiconShopRefreshRequest(
                        500,
                        timeout_ms,
                        state.SessionGeneration),
                    Ct)
                .ConfigureAwait(false);
            ValidateHabbiconRefresh(refreshed, state.SessionGeneration);
            first_collections = refreshed.FirstCollections;
            first_entries = refreshed.FirstEntries;
        }

        IReadOnlyList<HabbiconCollection> collections = await ReadHabbiconCollections(
            first_collections).ConfigureAwait(false);
        IReadOnlyList<Habbicon> habbicons = await ReadHabbiconEntries(first_entries)
            .ConfigureAwait(false);
        ValidateHabbiconSnapshot(first_entries.Vault, collections, habbicons);
        return new HabbiconReadSnapshot(first_entries.SessionGeneration, collections, habbicons);
    }

    private async Task<IReadOnlyList<HabbiconCollection>> ReadHabbiconCollections(
        HabbiconCollectionPage first_page)
    {
        HabbiconCollectionPage page = first_page;
        var collections = new List<HabbiconCollection>(page.Total);
        while (true)
        {
            ValidateHabbiconPage(first_page, page, page.Offset);
            for (int index = 0; index < page.Collections.Count; index++)
            {
                HabbiconCollectionView value = page.Collections[index];
                int ordinal = checked(page.Offset + index);
                if (value.Ordinal != ordinal)
                {
                    throw new InvalidOperationException(
                        "The habbicon application returned an invalid collection entry.");
                }
                collections.Add(ToHabbiconCollection(value));
            }
            if (page.NextOffset is not int offset)
                break;
            page = await _application
                .InvokeAsync<HabbiconCollectionPageRequest, HabbiconCollectionPage>(
                    ApplicationMemberIds.HabbiconCollectionsList,
                    new HabbiconCollectionPageRequest(offset, 500, first_page.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
        }
        if (collections.Count != first_page.Total)
            throw new InvalidOperationException("The habbicon application returned an incomplete collection list.");
        return Array.AsReadOnly(collections.ToArray());
    }

    private async Task<IReadOnlyList<Habbicon>> ReadHabbiconEntries(HabbiconEntryPage first_page)
    {
        HabbiconEntryPage page = first_page;
        var habbicons = new List<Habbicon>(page.Total);
        while (true)
        {
            ValidateHabbiconPage(first_page, page, page.Offset);
            for (int index = 0; index < page.Entries.Count; index++)
            {
                HabbiconEntryView value = page.Entries[index];
                int ordinal = checked(page.Offset + index);
                if (value.Ordinal != ordinal)
                {
                    throw new InvalidOperationException(
                        "The habbicon application returned an invalid icon entry.");
                }
                habbicons.Add(ToHabbicon(value));
            }
            if (page.NextOffset is not int offset)
                break;
            page = await _application
                .InvokeAsync<HabbiconEntryPageRequest, HabbiconEntryPage>(
                    ApplicationMemberIds.HabbiconEntriesList,
                    new HabbiconEntryPageRequest(offset, 500, first_page.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
        }
        if (habbicons.Count != first_page.Total)
            throw new InvalidOperationException("The habbicon application returned an incomplete icon list.");
        return Array.AsReadOnly(habbicons.ToArray());
    }

    private static void ValidateHabbiconState(HabbiconStateView state)
    {
        if (!state.Connected ||
            state.SessionGeneration <= 0 ||
            state.SnapshotRevision <= 0 ||
            state.Vault.CollectionCount < 0 ||
            state.Vault.IconCount < 0)
        {
            throw new InvalidOperationException(
                "The habbicon application returned an invalid state snapshot.");
        }
    }

    private static void ValidateHabbiconStatePage(
        HabbiconStateView state,
        HabbiconCollectionPage page)
    {
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.ShopRevision != state.ShopRevision ||
            page.UserRevision != state.UserRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Vault != state.Vault)
        {
            throw new InvalidOperationException(
                "The habbicon application returned a collection page from another snapshot.");
        }
    }

    private static void ValidateHabbiconStatePage(
        HabbiconStateView state,
        HabbiconEntryPage page)
    {
        if (page.Connected != state.Connected ||
            page.SessionGeneration != state.SessionGeneration ||
            page.StateRevision != state.Revision ||
            page.ShopRevision != state.ShopRevision ||
            page.UserRevision != state.UserRevision ||
            page.SnapshotRevision != state.SnapshotRevision ||
            page.Vault != state.Vault)
        {
            throw new InvalidOperationException(
                "The habbicon application returned an icon page from another snapshot.");
        }
    }

    private static void ValidateHabbiconRefresh(
        HabbiconShopRefreshResult refreshed,
        long expected_session_generation)
    {
        HabbiconCollectionPage collections = refreshed.FirstCollections;
        HabbiconEntryPage entries = refreshed.FirstEntries;
        if (refreshed.MessagesDispatched is < 0 or > 1 ||
            refreshed.SessionGeneration != expected_session_generation ||
            refreshed.SnapshotRevision <= 0 ||
            !collections.Connected ||
            collections.SessionGeneration != refreshed.SessionGeneration ||
            collections.StateRevision != refreshed.StateRevision ||
            collections.ShopRevision != refreshed.ShopRevision ||
            collections.UserRevision != refreshed.UserRevision ||
            collections.SnapshotRevision != refreshed.SnapshotRevision ||
            entries.Connected != collections.Connected ||
            entries.SessionGeneration != collections.SessionGeneration ||
            entries.StateRevision != collections.StateRevision ||
            entries.ShopRevision != collections.ShopRevision ||
            entries.UserRevision != collections.UserRevision ||
            entries.SnapshotRevision != collections.SnapshotRevision ||
            entries.Vault != collections.Vault ||
            !collections.Vault.ShopLoaded)
        {
            throw new InvalidOperationException(
                "The habbicon application returned an invalid shop refresh result.");
        }
    }

    private static void ValidateHabbiconPage(
        HabbiconCollectionPage first_page,
        HabbiconCollectionPage page,
        int offset)
    {
        int consumed = checked(offset + page.Collections.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (!first_page.Connected ||
            first_page.SnapshotRevision <= 0 ||
            page.Connected != first_page.Connected ||
            page.SessionGeneration != first_page.SessionGeneration ||
            page.StateRevision != first_page.StateRevision ||
            page.ShopRevision != first_page.ShopRevision ||
            page.UserRevision != first_page.UserRevision ||
            page.SnapshotRevision != first_page.SnapshotRevision ||
            page.Vault != first_page.Vault ||
            page.Total != first_page.Total ||
            page.Total != page.Vault.CollectionCount ||
            page.Offset != offset ||
            page.Collections.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Collections.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException(
                "The habbicon application returned an invalid collection page.");
        }
    }

    private static void ValidateHabbiconPage(
        HabbiconEntryPage first_page,
        HabbiconEntryPage page,
        int offset)
    {
        int consumed = checked(offset + page.Entries.Count);
        int? expected_next = consumed < page.Total ? consumed : null;
        if (!first_page.Connected ||
            first_page.SnapshotRevision <= 0 ||
            page.Connected != first_page.Connected ||
            page.SessionGeneration != first_page.SessionGeneration ||
            page.StateRevision != first_page.StateRevision ||
            page.ShopRevision != first_page.ShopRevision ||
            page.UserRevision != first_page.UserRevision ||
            page.SnapshotRevision != first_page.SnapshotRevision ||
            page.Vault != first_page.Vault ||
            page.Total != first_page.Total ||
            page.Total != page.Vault.IconCount ||
            page.Offset != offset ||
            page.Entries.Count > 500 ||
            consumed > page.Total ||
            consumed < page.Total && page.Entries.Count == 0 ||
            page.NextOffset != expected_next)
        {
            throw new InvalidOperationException(
                "The habbicon application returned an invalid icon page.");
        }
    }

    private static HabbiconCollection ToHabbiconCollection(HabbiconCollectionView value)
    {
        Habbicon[] habbicons = value.Habbicons.Select((entry, ordinal) =>
        {
            if (entry.Ordinal != ordinal)
                throw new InvalidOperationException("The habbicon application returned an invalid nested icon entry.");
            return ToHabbicon(entry);
        }).ToArray();
        var result = new HabbiconCollection(
            value.CollectionId,
            value.Name,
            value.Completed,
            value.RewardHabbiconId,
            (HabbiconState)value.RewardState,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType,
            habbicons);
        if (result.RewardIsClaimable != value.RewardIsClaimable)
            throw new InvalidOperationException("The habbicon application returned inconsistent collection data.");
        return result;
    }

    private static Habbicon ToHabbicon(HabbiconEntryView value)
    {
        var result = new Habbicon(
            value.HabbiconId,
            value.Name,
            value.CollectionId,
            (HabbiconState)value.State,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType);
        if (result.IsOwned != value.IsOwned ||
            result.IsClaimable != value.IsClaimable ||
            result.IsPurchasable != value.IsPurchasable)
        {
            throw new InvalidOperationException("The habbicon application returned inconsistent icon data.");
        }
        return result;
    }

    private static void ValidateHabbiconSnapshot(
        HabbiconVaultSummary summary,
        IReadOnlyList<HabbiconCollection> collections,
        IReadOnlyList<Habbicon> habbicons)
    {
        Habbicon[] nested = collections.SelectMany(collection => collection.Habbicons).ToArray();
        if (!summary.ShopLoaded ||
            summary.CollectionCount != collections.Count ||
            summary.IconCount != habbicons.Count ||
            summary.OwnedCount != habbicons.Count(icon => icon.IsOwned) ||
            summary.FavoriteCount != habbicons.Count(icon => icon.State is HabbiconState.Favorite) ||
            summary.ClaimableCount != habbicons.Count(icon => icon.IsClaimable) ||
            nested.Length != habbicons.Count ||
            !nested.SequenceEqual(habbicons))
        {
            throw new InvalidOperationException(
                "The habbicon application returned inconsistent shop totals.");
        }
    }

    private sealed record HabbiconReadSnapshot(
        long SessionGeneration,
        IReadOnlyList<HabbiconCollection> Collections,
        IReadOnlyList<Habbicon> Habbicons);
}
