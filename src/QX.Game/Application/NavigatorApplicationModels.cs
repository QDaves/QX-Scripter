using Qx.Model;

namespace Qx.Game.Application;

/// <summary>
/// Represents a request to read the current navigator state.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorState"/>, which returns the current
/// <see cref="NavigatorState"/> without sending anything to the hotel.
/// </remarks>
public sealed record NavigatorStateRequest;

/// <summary>
/// Represents a request to load navigator data from the hotel.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorMetadataRefresh"/> and
/// <see cref="ApplicationMemberIds.NavigatorFlatCategoriesRefresh"/>. Both send one request, wait for
/// the response and for the navigator state to store it, and return the updated
/// <see cref="NavigatorState"/>. The request is sent again once when the first attempt times out.
/// </remarks>
/// <param name="TimeoutMilliseconds">
/// The time to wait for the response in milliseconds, from 1 to 120000. The same limit applies again
/// while waiting for the state to be updated.
/// </param>
public sealed record NavigatorRefreshRequest(int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to run a navigator quick search that takes no arguments.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSearchMyRooms"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchMyFavourites"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchMyRoomRights"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchMyHistory"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchMyFrequentHistory"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchMyFriendsRooms"/>,
/// <see cref="ApplicationMemberIds.NavigatorSearchFriendsPresent"/> and
/// <see cref="ApplicationMemberIds.NavigatorSearchMyGuildBases"/>, which return a
/// <see cref="NavigatorSearchSnapshot"/>. The response does not identify the search, so the first
/// search result received on the session after the request is returned. The request is sent again
/// once when the first attempt times out.
/// </remarks>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the search result in milliseconds, from 1 to 120000.
/// </param>
public sealed record NavigatorSearchRequest(int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to search a navigator view with a filter.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSearchView"/>, which returns the first
/// <see cref="NavigatorSearchSnapshot"/> whose search code and filter match the request. The request
/// is sent again once when the first attempt times out, and the result also reaches the game client.
/// </remarks>
/// <param name="SearchCode">
/// The code of the navigator view to search, such as <c>hotel_view</c>. Must not be empty or white
/// space and must fit in 65535 UTF-8 bytes.
/// </param>
/// <param name="Filter">
/// The filter text in the navigator's prefix syntax, or empty for no filter. Must fit in 65535 UTF-8 bytes.
/// </param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the search result in milliseconds, from 1 to 120000.
/// </param>
public sealed record NavigatorViewSearchInput(
    string SearchCode,
    string Filter = "",
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to search rooms by text in one room field.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSearchText"/>. The text is sent with the prefix of
/// <paramref name="Field"/>, and the first <see cref="NavigatorSearchSnapshot"/> whose filter matches
/// the sent text is returned. The request is sent again once when the first attempt times out.
/// </remarks>
/// <param name="Field">The room field the text is matched against.</param>
/// <param name="Text">The text to search for. With its prefix it must fit in 65535 UTF-8 bytes.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the search result in milliseconds, from 1 to 120000.
/// </param>
public sealed record NavigatorTextSearchInput(
    RoomSearchField Field,
    string Text = "",
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to search the popular rooms, optionally narrowed to one tag.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSearchPopular"/>. The response does not identify
/// the search, so the first search result received on the session after the request is returned. The
/// request is sent again once when the first attempt times out.
/// </remarks>
/// <param name="Tag">The room tag, or empty for the most popular rooms overall. Must fit in 65535 UTF-8 bytes.</param>
/// <param name="AdIndex">The promoted room slot sent with the request, -1 or greater.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the search result in milliseconds, from 1 to 120000.
/// </param>
public sealed record NavigatorPopularSearchInput(
    string Tag = "",
    int AdIndex = -1,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to run a navigator search that takes only a promoted room slot.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSearchHighestScore"/> and
/// <see cref="ApplicationMemberIds.NavigatorSearchGuildBases"/>. The response does not identify the
/// search, so the first search result received on the session after the request is returned. The
/// request is sent again once when the first attempt times out.
/// </remarks>
/// <param name="AdIndex">The promoted room slot sent with the request, -1 or greater.</param>
/// <param name="TimeoutMilliseconds">
/// The total time to wait for the search result in milliseconds, from 1 to 120000.
/// </param>
public sealed record NavigatorAdSearchInput(
    int AdIndex = -1,
    int TimeoutMilliseconds = 10000);

/// <summary>
/// Represents a request to add a search to the local user's saved searches.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSavedSearchAdd"/>. The request is sent without
/// waiting for a response, and the saved searches in <see cref="NavigatorState"/> change when the
/// hotel sends the updated list.
/// </remarks>
/// <param name="SearchCode">
/// The code of the navigator view to save. Must not be empty or white space and must fit in 65535 UTF-8 bytes.
/// </param>
/// <param name="Filter">The filter text to save, or empty for no filter. Must fit in 65535 UTF-8 bytes.</param>
public sealed record NavigatorSavedSearchAddInput(string SearchCode, string Filter = "");

/// <summary>
/// Represents a request to delete one of the local user's saved searches.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorSavedSearchDelete"/>. The request is sent without
/// waiting for a response.
/// </remarks>
/// <param name="SavedSearchId">The id of the saved search, 0 or greater.</param>
public sealed record NavigatorSavedSearchDeleteInput(int SavedSearchId);

/// <summary>
/// Represents a request to collapse or expand a navigator category.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorCategoryCollapse"/> and
/// <see cref="ApplicationMemberIds.NavigatorCategoryExpand"/>. The request is sent without waiting
/// for a response.
/// </remarks>
/// <param name="SearchCode">
/// The code of the navigator category. Must not be empty or white space and must fit in 65535 UTF-8 bytes.
/// </param>
public sealed record NavigatorCategoryInput(string SearchCode);

/// <summary>
/// Represents a request to create a room owned by the local user.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorRoomCreate"/>. The request is sent without waiting
/// for a response, so the id of the new room is not returned.
/// </remarks>
/// <param name="Name">The name of the room. Must not be empty or white space and must fit in 65535 UTF-8 bytes.</param>
/// <param name="Description">The description of the room, which may be empty. Must fit in 65535 UTF-8 bytes.</param>
/// <param name="Model">
/// The name of the floor plan model, such as <c>model_a</c>. Must not be empty or white space and must
/// fit in 65535 UTF-8 bytes.
/// </param>
/// <param name="Category">The id of the room category the room is filed under.</param>
/// <param name="MaxVisitors">The maximum number of visitors.</param>
/// <param name="TradeMode">The trading mode, 0 for disabled, 1 for rights holders only or 2 for everyone.</param>
public sealed record NavigatorRoomCreateInput(
    string Name,
    string Description,
    string Model,
    int Category,
    int MaxVisitors,
    int TradeMode = 0);

/// <summary>
/// Represents a request to delete a room owned by the local user.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorRoomDelete"/>. The room and everything placed in it
/// are deleted without a confirmation step, and the request is sent without waiting for a response.
/// </remarks>
/// <param name="RoomId">The id of the room.</param>
public sealed record NavigatorRoomDeleteInput(Id RoomId);

/// <summary>
/// Represents a request to set or clear the local user's home room.
/// </summary>
/// <remarks>
/// Used by <see cref="ApplicationMemberIds.NavigatorHomeRoomSet"/>. The request is sent without waiting
/// for a response.
/// </remarks>
/// <param name="RoomId">The id of the room, or 0 to clear the home room.</param>
public sealed record NavigatorHomeRoomSetInput(Id RoomId);

/// <summary>
/// Represents the result of a navigator room request that was sent to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.NavigatorRoomCreate"/>,
/// <see cref="ApplicationMemberIds.NavigatorRoomDelete"/> and
/// <see cref="ApplicationMemberIds.NavigatorHomeRoomSet"/>. It confirms the request was sent, not that
/// the hotel applied it.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the request was sent.</param>
/// <param name="RoomId">
/// The id of the room the request named, or <see langword="null"/> for a room creation.
/// </param>
public sealed record NavigatorRoomOperationResult(
    DateTimeOffset DispatchedAtUtc,
    Id? RoomId = null);

/// <summary>
/// Represents the result of a navigator personalization request that was sent to the hotel.
/// </summary>
/// <remarks>
/// Returned by <see cref="ApplicationMemberIds.NavigatorSavedSearchAdd"/>,
/// <see cref="ApplicationMemberIds.NavigatorSavedSearchDelete"/>,
/// <see cref="ApplicationMemberIds.NavigatorCategoryCollapse"/> and
/// <see cref="ApplicationMemberIds.NavigatorCategoryExpand"/>. It confirms the request was sent, not
/// that the hotel applied it.
/// </remarks>
/// <param name="DispatchedAtUtc">The time the request was sent.</param>
/// <param name="SearchCode">
/// The search or category code that was sent, or <see langword="null"/> when deleting a saved search.
/// </param>
/// <param name="Filter">The filter text that was sent, or <see langword="null"/> when the request has no filter.</param>
/// <param name="SavedSearchId">
/// The id of the deleted saved search, or <see langword="null"/> for the other requests.
/// </param>
public sealed record NavigatorOperationResult(
    DateTimeOffset DispatchedAtUtc,
    string? SearchCode = null,
    string? Filter = null,
    int? SavedSearchId = null);

/// <summary>
/// Specifies the kind of change reported by <see cref="NavigatorChanged"/>.
/// </summary>
public enum NavigatorChangeKind
{
    /// <summary>The navigator categories and their quick links were received.</summary>
    Metadata,
    /// <summary>The room categories were received.</summary>
    FlatCategories,
    /// <summary>The local user's saved searches were received.</summary>
    SavedSearches,
    /// <summary>The rooms the hotel promotes were received.</summary>
    LiftedRooms,
    /// <summary>The codes of the collapsed categories were received.</summary>
    CollapsedCategories,
    /// <summary>The local user's navigator settings were received.</summary>
    Settings,
    /// <summary>The local user's navigator window preferences were received.</summary>
    Preferences,
    /// <summary>The state was cleared because the hotel connection closed.</summary>
    Reset
}

/// <summary>
/// Represents a change of the navigator state.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.NavigatorChanged"/>.
/// </remarks>
/// <param name="Kind">The kind of change.</param>
/// <param name="ChangedAtUtc">The time the change was published.</param>
/// <param name="State">The navigator state after the change.</param>
public sealed record NavigatorChanged(
    NavigatorChangeKind Kind,
    DateTimeOffset ChangedAtUtc,
    NavigatorState State);

/// <summary>
/// Represents a navigator search result received from the hotel.
/// </summary>
/// <remarks>
/// Published by <see cref="ApplicationMemberIds.NavigatorSearchReceived"/> for every search result
/// received in the active session, including results of searches the game client sent. Search results
/// are not stored in <see cref="NavigatorState"/>.
/// </remarks>
/// <param name="Generation">The state generation of the hotel session the result was received in.</param>
/// <param name="Revision">The navigator state revision when the result was received.</param>
/// <param name="ReceivedAtUtc">The time the result was published.</param>
/// <param name="Result">The search result.</param>
public sealed record NavigatorSearchReceived(
    long Generation,
    long Revision,
    DateTimeOffset ReceivedAtUtc,
    NavigatorSearchSnapshot Result);
