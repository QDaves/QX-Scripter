using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public enum NavigatorQuickSearch
{
    MyRooms,
    MyFavourites,
    MyRoomRights,
    MyHistory,
    MyFrequentHistory,
    MyFriendsRooms,
    RoomsWhereFriendsAre,
    MyGuildBases
}

public partial class ScriptGlobals
{
    /// <summary>
    /// The navigator: what the hotel offers to search, what the account saved, and the searches
    /// that take no filter.
    /// </summary>
    /// <remarks>
    /// A free-text search on a view code goes through <see cref="SearchRooms"/> or
    /// <see cref="SearchRoomQuery"/>, which match the answer back to the request they sent. This
    /// covers the rest of the navigator.
    /// </remarks>
    public NavigatorState Navigator => Application.Invoke<NavigatorStateRequest, NavigatorState>(
        ApplicationMemberIds.NavigatorState,
        new NavigatorStateRequest(),
        Ct);

    /// <summary>
    /// The navigator's categories and the searches offered under each, fetching them on first use.
    /// </summary>
    /// <remarks>
    /// Worth reading before searching a view code that was guessed at: a code the hotel does not
    /// publish comes back empty rather than refused, which is indistinguishable from a view that
    /// genuinely holds no rooms.
    /// </remarks>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<NavigatorCategory>> GetNavigatorCategories(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        NavigatorState state = Navigator;
        if (!state.MetadataLoaded)
        {
            state = await Application.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorMetadataRefresh,
                new NavigatorRefreshRequest(timeoutMs),
                Ct);
        }
        return state.Categories.Select(CategoryFromSnapshot).ToArray();
    }

    /// <summary>
    /// The room categories a room can be filed under, fetching them on first use.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<FlatCategory>> GetRoomCategories(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        NavigatorState state = Navigator;
        if (!state.FlatCategoriesLoaded)
        {
            state = await Application.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorFlatCategoriesRefresh,
                new NavigatorRefreshRequest(timeoutMs),
                Ct);
        }
        return state.FlatCategories.Select(CategoryFromSnapshot).ToArray();
    }

    /// <summary>
    /// The room categories a room owner may actually choose, which excludes the ones the hotel
    /// assigns itself and the ones reserved for staff.
    /// </summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<IReadOnlyList<FlatCategory>> GetSelectableRoomCategories(int timeoutMs = 10000)
    {
        IReadOnlyList<FlatCategory> categories = await GetRoomCategories(timeoutMs);
        return categories.Where(category => category.IsSelectable).ToArray();
    }

    /// <summary>
    /// Builds a navigator filter string for one field.
    /// </summary>
    /// <remarks>
    /// The prefixes are the client's own: <c>owner:</c>, <c>roomname:</c>, <c>tag:</c> and
    /// <c>group:</c>. Pass the result to <see cref="SearchRooms"/> or
    /// <see cref="SearchRoomQuery"/> as the filter.
    /// </remarks>
    /// <param name="field">Which field to match.</param>
    /// <param name="text">What to look for.</param>
    public static string RoomFilter(RoomSearchField field, string text) =>
        NavigatorManager.FilterText(field, text);

    /// <summary>Searches rooms by owner name.</summary>
    /// <param name="owner">The owner's name, or part of it.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> FindRoomsByOwner(string owner, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Owner, owner, timeoutMs);

    /// <summary>Searches rooms by room name.</summary>
    /// <param name="name">The room name, or part of it.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> FindRoomsByName(string name, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.RoomName, name, timeoutMs);

    /// <summary>Searches rooms by tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> FindRoomsByTag(string tag, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Tag, tag, timeoutMs);

    /// <summary>Searches rooms by the name of the group that owns them.</summary>
    /// <param name="group">The group's name, or part of it.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> FindRoomsByGroup(string group, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Group, group, timeoutMs);

    /// <summary>
    /// Searches rooms across everything the hotel indexes: name, owner and tags.
    /// </summary>
    /// <param name="text">What to look for.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> FindRooms(string text, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Anything, text, timeoutMs);

    /// <summary>
    /// Runs one of the navigator searches that take no filter, such as the account's own rooms or
    /// the rooms its friends are in.
    /// </summary>
    /// <param name="search">Which search.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<RoomDataQuery> FindRooms(
        NavigatorQuickSearch search,
        int timeoutMs = 10000)
    {
        string member_id = search switch
        {
            NavigatorQuickSearch.MyRooms => ApplicationMemberIds.NavigatorSearchMyRooms,
            NavigatorQuickSearch.MyFavourites => ApplicationMemberIds.NavigatorSearchMyFavourites,
            NavigatorQuickSearch.MyRoomRights => ApplicationMemberIds.NavigatorSearchMyRoomRights,
            NavigatorQuickSearch.MyHistory => ApplicationMemberIds.NavigatorSearchMyHistory,
            NavigatorQuickSearch.MyFrequentHistory => ApplicationMemberIds.NavigatorSearchMyFrequentHistory,
            NavigatorQuickSearch.MyFriendsRooms => ApplicationMemberIds.NavigatorSearchMyFriendsRooms,
            NavigatorQuickSearch.RoomsWhereFriendsAre => ApplicationMemberIds.NavigatorSearchFriendsPresent,
            NavigatorQuickSearch.MyGuildBases => ApplicationMemberIds.NavigatorSearchMyGuildBases,
            _ => throw new ArgumentOutOfRangeException(nameof(search))
        };
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                member_id,
                new NavigatorSearchRequest(timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>The rooms the account owns.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> GetMyRooms(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyRooms, timeoutMs);

    /// <summary>The rooms the account's friends are in right now.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> GetRoomsWithFriends(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.RoomsWhereFriendsAre, timeoutMs);

    /// <summary>The rooms the account marked as favourites.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> GetFavouriteRooms(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyFavourites, timeoutMs);

    /// <summary>The rooms the account visited, most recent first.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> GetRoomHistory(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyHistory, timeoutMs);

    /// <summary>The rooms the account has rights in.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public Task<RoomDataQuery> GetRoomsWithRights(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyRoomRights, timeoutMs);

    /// <summary>The most popular rooms, optionally narrowed to one tag.</summary>
    /// <param name="tag">The tag, or empty for the most popular overall.</param>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<RoomDataQuery> GetPopularRooms(string tag = "", int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorPopularSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchPopular,
                new NavigatorPopularSearchInput(tag, -1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>The highest scoring rooms.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<RoomDataQuery> GetHighestScoringRooms(int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchHighestScore,
                new NavigatorAdSearchInput(-1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>The rooms that serve as group bases.</summary>
    /// <param name="timeoutMs">How long to wait for the hotel to answer.</param>
    public async Task<RoomDataQuery> GetGuildBaseRooms(int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchGuildBases,
                new NavigatorAdSearchInput(-1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>The rooms the hotel is currently promoting.</summary>
    public IReadOnlyList<NavigatorLiftedRoom> PromotedRooms =>
        Navigator.LiftedRooms.Select(RoomFromSnapshot).ToArray();

    /// <summary>The searches the account has saved.</summary>
    public IReadOnlyList<NavigatorSearch> SavedSearches =>
        Navigator.SavedSearches.Select(SearchFromSnapshot).ToArray();

    /// <summary>Saves a search so the hotel offers it back in the navigator.</summary>
    /// <param name="searchCode">The view the search belongs to.</param>
    /// <param name="filter">The filter text.</param>
    public void SaveSearch(string searchCode, string filter) =>
        Application.Invoke<NavigatorSavedSearchAddInput, NavigatorOperationResult>(
            ApplicationMemberIds.NavigatorSavedSearchAdd,
            new NavigatorSavedSearchAddInput(searchCode, filter),
            Ct);

    /// <summary>Removes a saved search.</summary>
    /// <param name="savedSearchId">The saved search's identifier.</param>
    public void DeleteSavedSearch(int savedSearchId) =>
        Application.Invoke<NavigatorSavedSearchDeleteInput, NavigatorOperationResult>(
            ApplicationMemberIds.NavigatorSavedSearchDelete,
            new NavigatorSavedSearchDeleteInput(savedSearchId),
            Ct);

    /// <summary>The account's home room, or zero when none is set.</summary>
    public Id HomeRoomId => Navigator.Settings?.HomeRoomId ?? 0;

    /// <summary>Runs a callback for every navigator search result.</summary>
    /// <param name="handler">Receives the result.</param>
    public void OnNavigatorResult(Action<NavigatorSearchResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Track(Application.Subscribe<NavigatorSearchReceived>(
            ApplicationMemberIds.NavigatorSearchReceived,
            Guarded<NavigatorSearchReceived>(
                result => handler(ResultFromSnapshot(result.Result)))));
    }

    private async Task<RoomDataQuery> FindRoomsBy(
        RoomSearchField field,
        string text,
        int timeoutMs)
    {
        // The client sends a free-text search as its own message rather than as a view search, so
        // this does the same; inventing a view code to reuse SearchRooms would be a guess. The
        // answer is matched back by its filter, because the hotel says nothing else about which
        // request a result belongs to.
        NavigatorSearchSnapshot result =
            await Application.InvokeAsync<NavigatorTextSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchText,
                new NavigatorTextSearchInput(field, text, timeoutMs),
                Ct);
        return Query(result);
    }

    private static RoomDataQuery Query(NavigatorSearchSnapshot result) =>
        new(result.Rooms.Select(RoomFromSnapshot));

    private static NavigatorCategory CategoryFromSnapshot(NavigatorCategorySnapshot category) =>
        new(category.SearchCode, category.QuickLinks.Select(SearchFromSnapshot).ToArray());

    private static NavigatorSearch SearchFromSnapshot(NavigatorSearchEntrySnapshot search) =>
        new(search.Id, search.SearchCode, search.Filter, search.Localization);

    private static NavigatorLiftedRoom RoomFromSnapshot(NavigatorLiftedRoomSnapshot room) =>
        new(room.RoomId, room.AreaId, room.Image, room.Caption);

    private static FlatCategory CategoryFromSnapshot(NavigatorFlatCategorySnapshot category) =>
        new(
            category.NodeId,
            category.Name,
            category.Visible,
            category.Automatic,
            category.AutomaticCategoryKey,
            category.GlobalCategoryKey,
            category.StaffOnly);

    private static NavigatorSearchResult ResultFromSnapshot(NavigatorSearchSnapshot result) =>
        new(
            result.SearchCode,
            result.Filter,
            result.Blocks.Select(block => new NavigatorSearchBlock(
                block.SearchCode,
                block.Text,
                block.ActionAllowed,
                block.ForceClosed,
                block.ViewMode,
                block.Rooms.Select(RoomFromSnapshot).ToArray(),
                block.UnityMetadata.Select(metadata => new NavigatorRoomMetadata(
                    metadata.RoomId,
                    metadata.FirstValue,
                    metadata.SecondValue)).ToArray())).ToArray());

    private static RoomData RoomFromSnapshot(RoomDataSnapshot room) => new()
    {
        Id = room.Id,
        Name = room.Name,
        OwnerId = room.OwnerId,
        OwnerName = room.OwnerName,
        DoorMode = (RoomDoorMode)room.DoorMode,
        UserCount = room.UserCount,
        MaxUserCount = room.MaxUserCount,
        Description = room.Description,
        TradeMode = (RoomTradeMode)room.TradeMode,
        Score = room.Score,
        Ranking = room.Ranking,
        Category = room.Category,
        Tags = room.Tags,
        OfficialRoomPicRef = room.OfficialRoomPicture,
        HasGroup = room.HasGroup,
        GroupId = room.GroupId,
        GroupName = room.GroupName,
        GroupBadge = room.GroupBadge,
        HasEvent = room.HasEvent,
        EventName = room.EventName,
        EventDescription = room.EventDescription,
        EventMinutesRemaining = room.EventMinutesRemaining,
        ShowOwner = room.ShowOwner,
        AllowPets = room.AllowPets,
        DisplayRoomEntryAd = room.DisplayRoomEntryAd
    };
}
