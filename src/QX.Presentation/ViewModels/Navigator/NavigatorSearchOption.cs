using Qx.Game.Api;
using Qx.Game.Navigator;
using Qx.Game;

namespace Qx.Presentation.ViewModels.Navigator;

public enum NavigatorSearchMode
{
    Text,
    Quick,
    Popular,
    Ad
}

public sealed record NavigatorSearchOption(
    string Label,
    string MemberId,
    NavigatorSearchMode Mode,
    RoomSearchField Field = RoomSearchField.Anything,
    bool QueryRequired = false)
{
    public bool TakesQuery => Mode is NavigatorSearchMode.Text or NavigatorSearchMode.Popular;
}

public sealed record NavigatorSearchList(string Label, NavigatorSearchMode Mode, IReadOnlyList<NavigatorSearchOption> Options);

public static class NavigatorSearchOptions
{
    public static IReadOnlyList<NavigatorSearchOption> All { get; } =
    [
        new("Everything", ApiIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Anything, true),
        new("Owner", ApiIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Owner, true),
        new("Room name", ApiIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.RoomName, true),
        new("Tag", ApiIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Tag, true),
        new("Group", ApiIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Group, true),
        new("My rooms", ApiIds.NavigatorSearchMyRooms, NavigatorSearchMode.Quick),
        new("Favourites", ApiIds.NavigatorSearchMyFavourites, NavigatorSearchMode.Quick),
        new("Room rights", ApiIds.NavigatorSearchMyRoomRights, NavigatorSearchMode.Quick),
        new("History", ApiIds.NavigatorSearchMyHistory, NavigatorSearchMode.Quick),
        new("Frequent rooms", ApiIds.NavigatorSearchMyFrequentHistory, NavigatorSearchMode.Quick),
        new("Friends' rooms", ApiIds.NavigatorSearchMyFriendsRooms, NavigatorSearchMode.Quick),
        new("Friends here", ApiIds.NavigatorSearchFriendsPresent, NavigatorSearchMode.Quick),
        new("My guild bases", ApiIds.NavigatorSearchMyGuildBases, NavigatorSearchMode.Quick),
        new("Popular", ApiIds.NavigatorSearchPopular, NavigatorSearchMode.Popular),
        new("Highest score", ApiIds.NavigatorSearchHighestScore, NavigatorSearchMode.Ad),
        new("Guild bases", ApiIds.NavigatorSearchGuildBases, NavigatorSearchMode.Ad)
    ];

    public static IReadOnlyList<NavigatorSearchList> Lists { get; } =
    [
        new("Search", NavigatorSearchMode.Text, Of(NavigatorSearchMode.Text)),
        new("My lists", NavigatorSearchMode.Quick, Of(NavigatorSearchMode.Quick)),
        new("Popular", NavigatorSearchMode.Popular, Of(NavigatorSearchMode.Popular)),
        new("Top lists", NavigatorSearchMode.Ad, Of(NavigatorSearchMode.Ad))
    ];

    public static NavigatorSearchList For(NavigatorSearchMode mode) =>
        Lists.First(list => list.Mode == mode);

    static IReadOnlyList<NavigatorSearchOption> Of(NavigatorSearchMode mode) =>
        [.. All.Where(option => option.Mode == mode)];
}
