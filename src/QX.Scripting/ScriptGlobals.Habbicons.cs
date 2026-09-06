using Qx.Game;
using Qx.Game.Application;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    /// <summary>
    /// The habbicons: the small pictures that can be sent in a private conversation, the
    /// collections they are sold in, and which of them the local user owns.
    /// </summary>
    public HabbiconManager Habbicons => Game.Habbicons;

    /// <summary>Whether the hotel has habbicons switched on.</summary>
    public bool HabbiconsEnabled => Game.Habbicons.IsEnabled;

    /// <summary>
    /// The habbicon collections, fetching the shop from the hotel on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<HabbiconCollection>> GetHabbiconCollections(int timeoutMs = 10000) =>
        (await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false)).Collections;

    /// <summary>
    /// Every habbicon the shop knows, with the local user's state applied.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Habbicon>> GetHabbicons(int timeoutMs = 10000) =>
        (await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false)).Habbicons;

    /// <summary>The habbicons the local user owns, favourited or not.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Habbicon>> GetOwnedHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        return Array.AsReadOnly(snapshot.Habbicons.Where(icon => icon.IsOwned).ToArray());
    }

    /// <summary>The habbicons that are earned and still waiting to be claimed.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<Habbicon>> GetClaimableHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        return Array.AsReadOnly(snapshot.Habbicons.Where(icon => icon.IsClaimable).ToArray());
    }

    /// <summary>Looks a habbicon up by name, ignoring case.</summary>
    /// <param name="name">The icon's name.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    /// <returns>The icon, or <see langword="null"/> when the shop has no icon by that name.</returns>
    public async Task<Habbicon?> FindHabbicon(string name, int timeoutMs = 10000)
    {
        ArgumentNullException.ThrowIfNull(name);
        IReadOnlyList<Habbicon> icons = await GetHabbicons(timeoutMs);
        return icons.FirstOrDefault(icon =>
            icon.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Buys a single habbicon.</summary>
    /// <param name="habbiconId">The icon to buy.</param>
    public void BuyHabbicon(int habbiconId) => Game.Habbicons.Buy(habbiconId);

    /// <summary>Buys a whole habbicon collection.</summary>
    /// <param name="collectionId">The collection to buy.</param>
    public void BuyHabbiconCollection(int collectionId) => Game.Habbicons.BuyCollection(collectionId);

    /// <summary>Claims a habbicon that has been earned.</summary>
    /// <param name="habbiconId">The icon to claim.</param>
    public void ClaimHabbicon(int habbiconId) => Game.Habbicons.Claim(habbiconId);

    /// <summary>
    /// Claims every habbicon that is earned and unclaimed.
    /// </summary>
    /// <remarks>
    /// The hotel confirms each claim with its own status change, so this returns once the requests
    /// are away. Subscribe with <see cref="OnHabbiconGained"/> to see them land.
    /// </remarks>
    /// <param name="timeoutMs">How long to wait for the shop.</param>
    /// <returns>How many claims were sent.</returns>
    public async Task<int> ClaimAllHabbicons(int timeoutMs = 10000)
    {
        HabbiconReadSnapshot snapshot = await ReadHabbiconSnapshot(timeoutMs).ConfigureAwait(false);
        Habbicon[] claimable = snapshot.Habbicons.Where(icon => icon.IsClaimable).ToArray();
        foreach (Habbicon icon in claimable)
        {
            HabbiconDispatchResult result = await Application
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

    /// <summary>Marks an owned habbicon as a favourite.</summary>
    /// <param name="habbiconId">The icon to favourite.</param>
    public void FavoriteHabbicon(int habbiconId) => Game.Habbicons.Favorite(habbiconId);

    /// <summary>Removes a habbicon from the favourites.</summary>
    /// <param name="habbiconId">The icon to unfavourite.</param>
    public void UnfavoriteHabbicon(int habbiconId) => Game.Habbicons.Unfavorite(habbiconId);

    /// <summary>Runs a callback whenever a habbicon's state changes.</summary>
    /// <param name="handler">Receives the identifier and the new state.</param>
    public void OnHabbiconStatusChanged(Action<UserHabbiconStatusChanged> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Habbicons.StatusChanged += value,
            value => Game.Habbicons.StatusChanged -= value);
    }

    /// <summary>Runs a callback whenever a habbicon is newly acquired.</summary>
    /// <param name="handler">Receives the icon's identifier.</param>
    public void OnHabbiconGained(Action<int> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Habbicons.IconGained += value,
            value => Game.Habbicons.IconGained -= value);
    }

    /// <summary>Runs a callback whenever someone in the room uses a habbicon.</summary>
    /// <param name="handler">Receives the room index of the user and the icon's identifier.</param>
    public void OnHabbiconUsed(Action<RoomUseHabbicon> handler)
    {
        _ = Subscribe(
            handler,
            value => Game.Habbicons.UsedInRoom += value,
            value => Game.Habbicons.UsedInRoom -= value);
    }

    private async Task<HabbiconReadSnapshot> ReadHabbiconSnapshot(int timeout_ms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout_ms, "timeoutMs");
        HabbiconStateView state = await Application
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
            first_collections = await Application
                .InvokeAsync<HabbiconCollectionPageRequest, HabbiconCollectionPage>(
                    ApplicationMemberIds.HabbiconCollectionsList,
                    new HabbiconCollectionPageRequest(
                        Limit: 500,
                        SnapshotRevision: state.SnapshotRevision),
                    Ct)
                .ConfigureAwait(false);
            first_entries = await Application
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
            HabbiconShopRefreshResult refreshed = await Application
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
            page = await Application
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
            page = await Application
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
            state.Client is null ||
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
            page.Client != state.Client ||
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
            page.Client != state.Client ||
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
            collections.Client != refreshed.Client ||
            collections.SessionGeneration != refreshed.SessionGeneration ||
            collections.StateRevision != refreshed.StateRevision ||
            collections.ShopRevision != refreshed.ShopRevision ||
            collections.UserRevision != refreshed.UserRevision ||
            collections.SnapshotRevision != refreshed.SnapshotRevision ||
            entries.Connected != collections.Connected ||
            entries.Client != collections.Client ||
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
            first_page.Client is null ||
            first_page.SnapshotRevision <= 0 ||
            page.Connected != first_page.Connected ||
            page.Client != first_page.Client ||
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
            first_page.Client is null ||
            first_page.SnapshotRevision <= 0 ||
            page.Connected != first_page.Connected ||
            page.Client != first_page.Client ||
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
