using Qx.Game;
using Qx.Game.Application;

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
        new("Everything", ApplicationMemberIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Anything, true),
        new("Owner", ApplicationMemberIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Owner, true),
        new("Room name", ApplicationMemberIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.RoomName, true),
        new("Tag", ApplicationMemberIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Tag, true),
        new("Group", ApplicationMemberIds.NavigatorSearchText, NavigatorSearchMode.Text, RoomSearchField.Group, true),
        new("My rooms", ApplicationMemberIds.NavigatorSearchMyRooms, NavigatorSearchMode.Quick),
        new("Favourites", ApplicationMemberIds.NavigatorSearchMyFavourites, NavigatorSearchMode.Quick),
        new("Room rights", ApplicationMemberIds.NavigatorSearchMyRoomRights, NavigatorSearchMode.Quick),
        new("History", ApplicationMemberIds.NavigatorSearchMyHistory, NavigatorSearchMode.Quick),
        new("Frequent rooms", ApplicationMemberIds.NavigatorSearchMyFrequentHistory, NavigatorSearchMode.Quick),
        new("Friends' rooms", ApplicationMemberIds.NavigatorSearchMyFriendsRooms, NavigatorSearchMode.Quick),
        new("Friends here", ApplicationMemberIds.NavigatorSearchFriendsPresent, NavigatorSearchMode.Quick),
        new("My guild bases", ApplicationMemberIds.NavigatorSearchMyGuildBases, NavigatorSearchMode.Quick),
        new("Popular", ApplicationMemberIds.NavigatorSearchPopular, NavigatorSearchMode.Popular),
        new("Highest score", ApplicationMemberIds.NavigatorSearchHighestScore, NavigatorSearchMode.Ad),
        new("Guild bases", ApplicationMemberIds.NavigatorSearchGuildBases, NavigatorSearchMode.Ad)
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
