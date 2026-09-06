using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;

namespace Qx.Ui;

public partial class NavigatorPage : GamePage
{
    private static readonly IReadOnlyList<NavigatorSearchOption> options =
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

    private IDisposable? changed_subscription;
    private IDisposable? search_subscription;
    private NavigatorSearchSnapshot? last_result;
    private bool searching;

    public NavigatorPage()
    {
        InitializeComponent();
        SearchMode.ItemsSource = options;
        SearchMode.SelectedIndex = 0;
        ComboBoxPopupBackground.Apply(SearchMode);
    }

    public override bool IsSearching => Query.Text.Length > 0;

    public override void Opened()
    {
        base.Opened();
        Query.Focus();
    }

    public override void Refresh()
    {
        if (Application is not { } application)
        {
            Subheading.Text = "No active application runtime.";
            return;
        }

        try
        {
            NavigatorState state = application.Invoke<NavigatorStateRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorState,
                new NavigatorStateRequest());
            ApplyState(state);
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    protected override async Task FetchAsync()
    {
        if (Application is not { } application)
            return;

        NavigatorState state = application.Invoke<NavigatorStateRequest, NavigatorState>(
            ApplicationMemberIds.NavigatorState,
            new NavigatorStateRequest());
        if (!state.MetadataLoaded)
        {
            await application.InvokeAsync<NavigatorRefreshRequest, NavigatorState>(
                ApplicationMemberIds.NavigatorMetadataRefresh,
                new NavigatorRefreshRequest());
        }
    }

    protected override void Fetching(string? message)
    {
        if (message is not null)
            Status.Text = message;
    }

    protected override void AttachApplication(IApplicationRuntime application)
    {
        changed_subscription = application.Subscribe<NavigatorChanged>(
            ApplicationMemberIds.NavigatorChanged,
            change => OnUi(() =>
            {
                if (change.Kind is NavigatorChangeKind.Reset)
                {
                    last_result = null;
                    ClearResults("Choose a search to find rooms.");
                }
                ApplyState(change.State);
            }));
        search_subscription = application.Subscribe<NavigatorSearchReceived>(
            ApplicationMemberIds.NavigatorSearchReceived,
            result => OnUi(() => ShowResult(result.Result)));
    }

    protected override void DetachApplication(IApplicationRuntime application)
    {
        changed_subscription?.Dispose();
        search_subscription?.Dispose();
        changed_subscription = null;
        search_subscription = null;
        last_result = null;
        ClearResults("Choose a search to find rooms.");
    }

    private void ApplyState(NavigatorState state)
    {
        string home = state.Settings is { HomeRoomId: var home_id } && home_id != 0
            ? $" · home {home_id}"
            : "";
        Subheading.Text = state.MetadataLoaded
            ? $"{state.Categories.Count:N0} categories · {state.SavedSearches.Count:N0} saved{home}"
            : "Navigator metadata has not been loaded yet.";

        if (last_result is { } result && !searching)
            ShowResult(result);
        else if (state.Generation == 0 || Rooms.ItemsSource is null)
            ClearResults("Choose a search to find rooms.");
    }

    private void Search(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private async Task SearchAsync()
    {
        if (searching || Application is not { } application ||
            SearchMode.SelectedItem is not NavigatorSearchOption option)
        {
            return;
        }

        string query = Query.Text.Trim();
        if (option.QueryRequired && query.Length == 0)
        {
            Status.Text = "Enter a search term.";
            Query.Focus();
            return;
        }

        searching = true;
        SearchButton.IsEnabled = false;
        Status.Text = "Searching…";
        try
        {
            NavigatorSearchSnapshot result = option.Mode switch
            {
                NavigatorSearchMode.Text =>
                    await application.InvokeAsync<NavigatorTextSearchInput, NavigatorSearchSnapshot>(
                        option.MemberId,
                        new NavigatorTextSearchInput(option.Field, query)),
                NavigatorSearchMode.Popular =>
                    await application.InvokeAsync<NavigatorPopularSearchInput, NavigatorSearchSnapshot>(
                        option.MemberId,
                        new NavigatorPopularSearchInput(query)),
                NavigatorSearchMode.Ad =>
                    await application.InvokeAsync<NavigatorAdSearchInput, NavigatorSearchSnapshot>(
                        option.MemberId,
                        new NavigatorAdSearchInput()),
                _ => await application.InvokeAsync<NavigatorSearchRequest, NavigatorSearchSnapshot>(
                    option.MemberId,
                    new NavigatorSearchRequest())
            };
            ShowResult(result);
        }
        catch (Exception error)
        {
            Status.Text = $"Search failed: {error.Message}";
        }
        finally
        {
            searching = false;
            SearchButton.IsEnabled = true;
        }
    }

    private void ShowResult(NavigatorSearchSnapshot result)
    {
        last_result = result;
        RoomDataSnapshot[] rooms = result.Rooms.ToArray();
        Rooms.ItemsSource = rooms;
        EmptyNotice.Visibility = rooms.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = rooms.Length == 0 ? "No rooms matched this search." : "";
        Status.Text = rooms.Length == 1 ? "1 room" : $"{rooms.Length:N0} rooms";
    }

    private void ClearResults(string message)
    {
        Rooms.ItemsSource = null;
        EmptyNotice.Visibility = Visibility.Visible;
        EmptyText.Text = message;
        Status.Text = "";
    }

    private void SearchModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchMode.SelectedItem is not NavigatorSearchOption option)
            return;

        Query.IsEnabled = option.Mode is NavigatorSearchMode.Text or NavigatorSearchMode.Popular;
        Query.Tag = option.Mode is NavigatorSearchMode.Popular ? "Optional tag" : "Search rooms";
        if (!Query.IsEnabled)
            Query.Text = "";
    }

    private void QueryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        Search(SearchButton, new RoutedEventArgs());
    }

    private void CopyRoomValue(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            TreeLookup.Ancestor<DataGridCell>(source) is not { } cell ||
            ItemsControl.ContainerFromElement(Rooms, source) is not DataGridRow { Item: RoomDataSnapshot room })
        {
            return;
        }

        string value = cell.Column.DisplayIndex switch
        {
            0 => room.Name,
            1 => room.OwnerName,
            2 => room.UserCount.ToString(),
            3 => room.MaxUserCount.ToString(),
            4 => room.Score.ToString(),
            5 => room.Id.ToString(),
            _ => ""
        };

        if (value.Length == 0)
            return;

        try
        {
            Clipboard.SetText(value);
            Status.Text = $"Copied {value}";
        }
        catch (Exception error)
        {
            Status.Text = $"Could not copy: {error.Message}";
        }
    }

    private void EnterRoom(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(Rooms, source) is not DataGridRow { Item: RoomDataSnapshot room } ||
            Application is not { } application)
        {
            return;
        }

        e.Handled = true;
        try
        {
            RoomLifecycleDispatchResult result = application.Invoke<RoomEnterRequest, RoomLifecycleDispatchResult>(
                ApplicationMemberIds.RoomEnter,
                new RoomEnterRequest(room.Id));
            Status.Text = result.Dispatched ? $"Entering {room.Name}…" : $"Could not enter {room.Name}.";
        }
        catch (Exception error)
        {
            Status.Text = $"Could not enter {room.Name}: {error.Message}";
        }
    }

    private sealed record NavigatorSearchOption(
        string Label,
        string MemberId,
        NavigatorSearchMode Mode,
        RoomSearchField Field = RoomSearchField.Anything,
        bool QueryRequired = false);

    private enum NavigatorSearchMode
    {
        Text,
        Quick,
        Popular,
        Ad
    }
}
