using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

/// <summary>
/// Specifies a navigator search that takes no filter.
/// </summary>
public enum NavigatorQuickSearch
{
    /// <summary>The rooms the account owns.</summary>
    MyRooms,
    /// <summary>The rooms the account marked as favorites.</summary>
    MyFavourites,
    /// <summary>The rooms where the account has rights.</summary>
    MyRoomRights,
    /// <summary>The rooms the account visited recently.</summary>
    MyHistory,
    /// <summary>The rooms the account visits most often.</summary>
    MyFrequentHistory,
    /// <summary>The rooms owned by the account's friends.</summary>
    MyFriendsRooms,
    /// <summary>The rooms the account's friends are in right now.</summary>
    RoomsWhereFriendsAre,
    /// <summary>The base rooms of the groups the account has joined.</summary>
    MyGuildBases
}

public partial class ScriptGlobals
{
    /// <summary>
    /// Gets a snapshot of the navigator state: what the hotel offers to search, what the account
    /// saved, and its navigator settings.
    /// </summary>
    /// <remarks>
    /// Every read returns the state as last received; nothing is requested. A free text search on a
    /// view code goes through <see cref="SearchRooms"/> or <see cref="SearchRoomQuery"/>, which
    /// match the answer back to the request they sent. This covers the rest of the navigator.
    /// </remarks>
    public NavigatorState Navigator => _application.Invoke<NavigatorStateRequest, NavigatorState>(
        ApplicationMemberIds.NavigatorState,
        new NavigatorStateRequest(),
        Ct);

    /// <summary>
    /// Gets the navigator's categories and the searches offered under each, fetching them on first use.
    /// </summary>
    /// <remarks>
    /// Read them before searching a view code that was guessed at: a code the hotel does not
    /// publish comes back empty rather than refused, which is indistinguishable from a view that
    /// holds no rooms. When the metadata has not been received yet it is requested; the reply also
    /// reaches the game client.
    /// </remarks>
    /// <param name="timeoutMs">
    /// The timeout in milliseconds, from 1 to 120000, covering one automatic retry. It only applies
    /// when the metadata has to be requested.
    /// </param>
    /// <returns>The navigator categories.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative, or above 120000 when a request is needed.</exception>
    public async Task<IReadOnlyList<NavigatorCategory>> GetNavigatorCategories(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        NavigatorState state = Navigator;
        if (!state.MetadataLoaded)
        {
            state = await _application.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorMetadataRefresh,
                new NavigatorRefreshRequest(timeoutMs),
                Ct);
        }
        return state.Categories.Select(CategoryFromSnapshot).ToArray();
    }

    /// <summary>
    /// Gets the room categories a room can be filed under, fetching them on first use.
    /// </summary>
    /// <param name="timeoutMs">
    /// The timeout in milliseconds, from 1 to 120000, covering one automatic retry. It only applies
    /// when the categories have to be requested.
    /// </param>
    /// <returns>Every room category the hotel sent, including hidden and staff only ones.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMs"/> is zero or negative, or above 120000 when a request is needed.</exception>
    public async Task<IReadOnlyList<FlatCategory>> GetRoomCategories(int timeoutMs = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        NavigatorState state = Navigator;
        if (!state.FlatCategoriesLoaded)
        {
            state = await _application.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorFlatCategoriesRefresh,
                new NavigatorRefreshRequest(timeoutMs),
                Ct);
        }
        return state.FlatCategories.Select(CategoryFromSnapshot).ToArray();
    }

    /// <summary>
    /// Gets the room categories a room owner may actually choose, fetching them on first use.
    /// </summary>
    /// <remarks>
    /// This excludes hidden categories, the ones the hotel assigns itself and the ones reserved for staff.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, used when the categories have to be requested.</param>
    /// <returns>The categories whose <see cref="FlatCategory.IsSelectable"/> is <see langword="true"/>.</returns>
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
    /// <param name="field">The field to match.</param>
    /// <param name="text">The text to look for.</param>
    /// <returns>The filter text, which is <paramref name="text"/> with the field's prefix, or unchanged for <see cref="RoomSearchField.Anything"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="field"/> is not a defined value.</exception>
    public static string RoomFilter(RoomSearchField field, string text) =>
        NavigatorManager.FilterText(field, text);

    /// <summary>Searches rooms by owner name.</summary>
    /// <remarks>
    /// The answer is matched back by its filter text. It also reaches the game client.
    /// </remarks>
    /// <param name="owner">The owner's name, or part of it.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> FindRoomsByOwner(string owner, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Owner, owner, timeoutMs);

    /// <summary>Searches rooms by room name.</summary>
    /// <remarks>
    /// The answer is matched back by its filter text. It also reaches the game client.
    /// </remarks>
    /// <param name="name">The room name, or part of it.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> FindRoomsByName(string name, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.RoomName, name, timeoutMs);

    /// <summary>Searches rooms by tag.</summary>
    /// <remarks>
    /// The answer is matched back by its filter text. It also reaches the game client.
    /// </remarks>
    /// <param name="tag">The tag.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> FindRoomsByTag(string tag, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Tag, tag, timeoutMs);

    /// <summary>Searches rooms by the name of the group that owns them.</summary>
    /// <remarks>
    /// The answer is matched back by its filter text. It also reaches the game client.
    /// </remarks>
    /// <param name="group">The group's name, or part of it.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> FindRoomsByGroup(string group, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Group, group, timeoutMs);

    /// <summary>
    /// Searches rooms across everything the hotel indexes, such as name, owner and tags.
    /// </summary>
    /// <remarks>
    /// The text is sent without a field prefix. The answer is matched back by its filter text and
    /// also reaches the game client.
    /// </remarks>
    /// <param name="text">The text to look for.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> FindRooms(string text, int timeoutMs = 10000) =>
        FindRoomsBy(RoomSearchField.Anything, text, timeoutMs);

    /// <summary>
    /// Runs one of the navigator searches that take no filter, such as the account's own rooms or
    /// the rooms its friends are in.
    /// </summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer, and it also
    /// reaches the game client.
    /// </remarks>
    /// <param name="search">The search to run.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="search"/> is not a defined value, or <paramref name="timeoutMs"/> is outside 1 to 120000.</exception>
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
            await _application.InvokeAsync<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                member_id,
                new NavigatorSearchRequest(timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>Gets the rooms the account owns.</summary>
    /// <remarks>Same as <see cref="FindRooms(NavigatorQuickSearch, int)"/> with <see cref="NavigatorQuickSearch.MyRooms"/>.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> GetMyRooms(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyRooms, timeoutMs);

    /// <summary>Gets the rooms the account's friends are in right now.</summary>
    /// <remarks>Same as <see cref="FindRooms(NavigatorQuickSearch, int)"/> with <see cref="NavigatorQuickSearch.RoomsWhereFriendsAre"/>.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> GetRoomsWithFriends(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.RoomsWhereFriendsAre, timeoutMs);

    /// <summary>Gets the rooms the account marked as favorites.</summary>
    /// <remarks>Same as <see cref="FindRooms(NavigatorQuickSearch, int)"/> with <see cref="NavigatorQuickSearch.MyFavourites"/>.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> GetFavouriteRooms(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyFavourites, timeoutMs);

    /// <summary>Gets the rooms the account visited recently, in the order the hotel sends them.</summary>
    /// <remarks>Same as <see cref="FindRooms(NavigatorQuickSearch, int)"/> with <see cref="NavigatorQuickSearch.MyHistory"/>.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> GetRoomHistory(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyHistory, timeoutMs);

    /// <summary>Gets the rooms the account has rights in.</summary>
    /// <remarks>Same as <see cref="FindRooms(NavigatorQuickSearch, int)"/> with <see cref="NavigatorQuickSearch.MyRoomRights"/>.</remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public Task<RoomDataQuery> GetRoomsWithRights(int timeoutMs = 10000) =>
        FindRooms(NavigatorQuickSearch.MyRoomRights, timeoutMs);

    /// <summary>Gets the most popular rooms, optionally narrowed to one tag.</summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer, and it also
    /// reaches the game client.
    /// </remarks>
    /// <param name="tag">The tag, or empty for the most popular overall.</param>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public async Task<RoomDataQuery> GetPopularRooms(string tag = "", int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await _application.InvokeAsync<NavigatorPopularSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchPopular,
                new NavigatorPopularSearchInput(tag, -1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>Gets the hotel's highest scoring rooms.</summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer, and it also
    /// reaches the game client.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public async Task<RoomDataQuery> GetHighestScoringRooms(int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await _application.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchHighestScore,
                new NavigatorAdSearchInput(-1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>Gets the public rooms that serve as group bases.</summary>
    /// <remarks>
    /// The first search result that arrives after the request is taken as the answer, and it also
    /// reaches the game client.
    /// </remarks>
    /// <param name="timeoutMs">The timeout in milliseconds, from 1 to 120000, covering one automatic retry.</param>
    /// <returns>A query over the rooms found.</returns>
    public async Task<RoomDataQuery> GetGuildBaseRooms(int timeoutMs = 10000)
    {
        NavigatorSearchSnapshot result =
            await _application.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                ApplicationMemberIds.NavigatorSearchGuildBases,
                new NavigatorAdSearchInput(-1, timeoutMs),
                Ct);
        return Query(result);
    }

    /// <summary>Gets the rooms the hotel is currently promoting in the navigator.</summary>
    /// <remarks>
    /// The list is read from the navigator state as last received, and is empty until the hotel has
    /// sent it.
    /// </remarks>
    public IReadOnlyList<NavigatorLiftedRoom> PromotedRooms =>
        Navigator.LiftedRooms.Select(RoomFromSnapshot).ToArray();

    /// <summary>Gets the searches the account has saved.</summary>
    /// <remarks>
    /// The list is read from the navigator state as last received, and is empty until the hotel has
    /// sent it.
    /// </remarks>
    public IReadOnlyList<NavigatorSearch> SavedSearches =>
        Navigator.SavedSearches.Select(SearchFromSnapshot).ToArray();

    /// <summary>Saves a search so the hotel offers it back in the navigator.</summary>
    /// <param name="searchCode">The view the search belongs to.</param>
    /// <param name="filter">The filter text.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="searchCode"/> is empty or white space.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void SaveSearch(string searchCode, string filter) =>
        _application.Invoke<NavigatorSavedSearchAddInput, NavigatorOperationResult>(
            ApplicationMemberIds.NavigatorSavedSearchAdd,
            new NavigatorSavedSearchAddInput(searchCode, filter),
            Ct);

    /// <summary>Removes a saved search.</summary>
    /// <param name="savedSearchId">The saved search's identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="savedSearchId"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when there is no active hotel session.</exception>
    public void DeleteSavedSearch(int savedSearchId) =>
        _application.Invoke<NavigatorSavedSearchDeleteInput, NavigatorOperationResult>(
            ApplicationMemberIds.NavigatorSavedSearchDelete,
            new NavigatorSavedSearchDeleteInput(savedSearchId),
            Ct);

    /// <summary>Gets the account's home room, or zero when none is set or the navigator settings have not arrived.</summary>
    public Id HomeRoomId => Navigator.Settings?.HomeRoomId ?? 0;

    /// <summary>Registers a handler that runs for every navigator search result the hotel sends.</summary>
    /// <remarks>
    /// Results of searches made by the game client and by the script are both reported.
    /// </remarks>
    /// <param name="handler">The handler to call with the result.</param>
    /// <returns>A handle that removes the handler when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnNavigatorResult(Action<NavigatorSearchResult> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Track(_application.Subscribe<NavigatorSearchReceived>(
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
            await _application.InvokeAsync<NavigatorTextSearchInput, NavigatorSearchSnapshot>(
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
                block.Rooms.Select(RoomFromSnapshot).ToArray())).ToArray());

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
