using System.Windows;
using System.Windows.Controls;
using Qx.Game;
using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Model;
using Qx.Protocol;

namespace Qx.Ui;

/// <summary>
/// Your friend list, and what you can do with it.
/// </summary>
/// <remarks>
/// <para>
/// A table, so it sorts by who is online, by name, by when they were last seen. The head render is
/// asked for by figure where the hotel gave one and by name where it did not — an offline friend
/// often arrives with an empty figure, which is why they used to be the only rows with no face.
/// </para>
/// <para>
/// Whispering and finding need them to be in the room with you; the rest works from the list alone.
/// </para>
/// </remarks>
public partial class FriendsPage : GamePage
{
    private IReadOnlyList<GameRow> _friends = [];
    private IReadOnlyDictionary<Id, FriendSnapshot> _friend_snapshots =
        new Dictionary<Id, FriendSnapshot>();
    private IDisposable? _friend_changes;

    public FriendsPage() => InitializeComponent();

    public override bool IsSearching => Filter.Text.Length > 0;

    protected override void AttachApplication(IApplicationRuntime application) =>
        _friend_changes = application.Subscribe<FriendChanged>(
            ApplicationMemberIds.FriendsChanged,
            _ => RefreshIfVisible());

    protected override void DetachApplication(IApplicationRuntime application)
    {
        _friend_changes?.Dispose();
        _friend_changes = null;
    }

    /// <summary>
    /// Asks for the friend list rather than waiting to overhear it.
    /// </summary>
    /// <remarks>
    /// The hotel sends it once, at login. Attaching to a game that is already running means never
    /// having seen it, so it has to be asked for — which works the same on both clients, since the
    /// request goes out under whichever name that client uses.
    /// </remarks>
    protected override async Task FetchAsync()
    {
        if (Application is not { } application)
            return;
        await application.InvokeAsync(
            ApplicationMemberIds.FriendsRefresh,
            new FriendsRefreshRequest(Limit: 500)).ConfigureAwait(true);
    }

    protected override void Fetching(string? message)
    {
        if (message is { Length: > 0 })
            Status.Text = message;
    }

    public override void Refresh()
    {
        if (Game is null || Application is not { } application)
        {
            _friends = [];
            _friend_snapshots = new Dictionary<Id, FriendSnapshot>();
            Empty("Connect to see your friends.");
            Apply();
            return;
        }

        FriendListPage page = ReadFriends(application);
        _friend_snapshots = page.Friends.ToDictionary(friend => friend.Id);
        _friends =
        [
            .. page.Friends
                .Select(friend => new GameRow(Head(friend))
                {
                    Name = friend.Name,
                    Detail = friend.Motto,
                    Trailing = friend.IsOnline ? "online" : Ago(friend.LastOnline),
                    Tag = InThisRoom(friend.Name) ? "here" : "",
                    IsOnline = friend.IsOnline,
                    Key = friend.Id
                })
        ];

        int online = _friends.Count(friend => friend.IsOnline);
        Subheading.Text = _friends.Count == 0
            ? ""
            : $"{_friends.Count:N0} {(_friends.Count == 1 ? "friend" : "friends")}, {online:N0} online";

        EmptyNotice.Visibility = _friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_friends.Count == 0)
            EmptyText.Text = page.Loaded ? "No friends yet." : "Nothing yet. Reload to ask the hotel.";

        Apply();
    }

    /// <summary>Whether they are standing in the room we are in, which is what makes finding them possible.</summary>
    private bool InThisRoom(string name) =>
        Game?.Room.Avatars.Any(avatar =>
            avatar is User && string.Equals(avatar.Name, name, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>
    /// The head render, by figure where there is one and by name where there is not.
    /// </summary>
    /// <remarks>
    /// The hotel sends a figure for everyone on the list, but not always: an offline friend can
    /// arrive with the field empty, and those were the rows with no face at all. Asking the imaging
    /// host to look the name up instead costs one redirect and always answers.
    /// </remarks>
    private static string? Head(FriendSnapshot friend) =>
        friend.Figure is { Length: > 0 } figure
            ? HabboImages.HeadUrl(figure)
            : HabboImages.HeadUrlForName(friend.Name);

    /// <summary>
    /// How long ago somebody was last seen, in words rather than a timestamp.
    /// </summary>
    /// <remarks>
    /// The hotel reports this as minutes since they were last online, not as a moment, and a friend
    /// who has never been away reports zero or less.
    /// </remarks>
    private static string Ago(long minutes)
    {
        if (minutes <= 0)
            return "";

        TimeSpan gap = TimeSpan.FromMinutes(minutes);
        return gap switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalHours: < 1 } => $"{(int)gap.TotalMinutes}m ago",
            { TotalDays: < 1 } => $"{(int)gap.TotalHours}h ago",
            { TotalDays: < 30 } => $"{(int)gap.TotalDays}d ago",
            _ => $"{(int)(gap.TotalDays / 30)}mo ago"
        };
    }

    private void Empty(string message)
    {
        EmptyNotice.Visibility = Visibility.Visible;
        EmptyText.Text = message;
        Subheading.Text = "";
    }

    private void Apply()
    {
        string term = Filter.Text.Trim();
        bool onlineOnly = OnlineOnly.IsChecked == true;

        List<GameRow> rows =
        [
            .. _friends
                .Where(row => !onlineOnly || row.IsOnline)
                .Where(row => term.Length == 0 ||
                    row.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                    row.Detail.Contains(term, StringComparison.CurrentCultureIgnoreCase))
        ];

        Rows.ItemsSource = rows;
        Status.Text = rows.Count == _friends.Count
            ? $"{rows.Count:N0} shown"
            : $"{rows.Count:N0} of {_friends.Count:N0} shown";
    }

    private void FilterChanged(object sender, TextChangedEventArgs e) => Apply();

    private void FilterToggled(object sender, RoutedEventArgs e) => Apply();

    private void Reload(object sender, RoutedEventArgs e) => Observe(ReloadAsync);

    private async Task ReloadAsync()
    {
        Status.Text = "Asking the hotel…";
        try
        {
            await FetchAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            Status.Text = $"Could not read the friend list: {error.Message}";
            return;
        }

        Refresh();
    }

    private GameRow? One() => Rows.SelectedItems.OfType<GameRow>().FirstOrDefault();

    /// <summary>The friend as the room knows them, when they are standing in it.</summary>
    private Avatar? InRoom(GameRow row) =>
        Game?.Room.Avatars.FirstOrDefault(avatar =>
            avatar is User && string.Equals(avatar.Name, row.Name, StringComparison.OrdinalIgnoreCase));

    private void FindFriend(object sender, RoutedEventArgs e)
    {
        if (One() is not { } row)
            return;

        if (InRoom(row) is { } avatar)
            Game?.People.Find(avatar);
        else
            Status.Text = $"{row.Name} is not in this room.";
    }

    /// <summary>
    /// Opens a whisper to them in the game's own chat box.
    /// </summary>
    /// <remarks>
    /// Written to the client rather than sent: this puts the whisper prefix in front of you to type
    /// after, which is what the game does when you click somebody's name. Sending would say
    /// something on your behalf that you had not written.
    /// </remarks>
    private void WhisperFriend(object sender, RoutedEventArgs e)
    {
        if (One() is not { } row)
            return;

        try
        {
            Clipboard.SetText($"/whisper {row.Name} ");
            Status.Text = $"Whisper to {row.Name} copied — paste it into the chat box.";
        }
        catch
        {
            Status.Text = "Could not reach the clipboard.";
        }
    }

    /// <summary>
    /// Walks after them, wherever they go.
    /// </summary>
    /// <remarks>
    /// The hotel does the walking once it has been told who, so this is one message rather than a
    /// loop of steps. It only means anything while they are somewhere you can be, so an offline
    /// friend is told about instead of being followed into nowhere.
    /// </remarks>
    private void FollowFriend(object sender, RoutedEventArgs e)
    {
        if (One() is not { } row)
            return;
        if (Application is not { } application)
            return;

        if (!row.IsOnline)
        {
            Status.Text = $"{row.Name} is not online.";
            return;
        }

        try
        {
            application.Invoke<FriendFollowRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendFollow,
                new FriendFollowRequest(row.Key));
            Status.Text = $"Following {row.Name}.";
        }
        catch (Exception error)
        {
            Status.Text = $"Could not follow: {error.Message}";
        }
    }

    private void OpenFriendProfile(object sender, RoutedEventArgs e)
    {
        if (One() is { } row)
            Game?.People.OpenProfile(row.Key);
    }

    private void FriendToWardrobe(object sender, RoutedEventArgs e)
    {
        if (One() is not { } row)
            return;

        _friend_snapshots.TryGetValue(row.Key, out FriendSnapshot? friend);

        if (friend?.Figure is not { Length: > 0 } figure)
        {
            Status.Text = "The hotel has not said what they are wearing.";
            return;
        }

        OutfitStore store = OutfitStore.Shared;
        string gender = friend.Gender is { Length: > 0 } value
            ? value[..1].ToUpperInvariant()
            : "M";

        Status.Text = store.Add(new SavedOutfit(figure, gender, friend.Name))
            ? $"Kept {friend.Name}'s outfit in your wardrobe."
            : "You are already keeping that outfit.";
    }

    private void CopyFriendField(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string field } || One() is not { } row)
            return;

        _friend_snapshots.TryGetValue(row.Key, out FriendSnapshot? friend);

        string text = field switch
        {
            "id" => row.Key.ToString(),
            "motto" => friend?.Motto ?? row.Detail,
            "figure" => friend?.Figure ?? "",
            _ => row.Name
        };

        if (text.Length == 0)
            return;

        try
        {
            Clipboard.SetText(text);
            Status.Text = "Copied.";
        }
        catch
        {
        }
    }

    private void RemoveFriend(object sender, RoutedEventArgs e)
    {
        if (Application is not { } application)
            return;

        Id[] picked = [.. Rows.SelectedItems.OfType<GameRow>().Select(row => row.Key)];
        if (picked.Length == 0)
            return;

        try
        {
            application.Invoke<FriendsRemoveRequest, FriendOperationResult>(
                ApplicationMemberIds.FriendsRemove,
                new FriendsRemoveRequest(picked));
            Status.Text = picked.Length == 1 ? "Friend removed." : $"{picked.Length} friends removed.";
        }
        catch (Exception error)
        {
            Status.Text = $"Could not remove: {error.Message}";
        }
    }

    private static FriendListPage ReadFriends(IApplicationRuntime application)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var friends = new List<FriendSnapshot>();
            FriendListPage? first = null;
            int offset = 0;
            while (true)
            {
                FriendListPage page = application.Invoke<FriendsListRequest, FriendListPage>(
                    ApplicationMemberIds.FriendsList,
                    new FriendsListRequest(Offset: offset, Limit: 500));
                first ??= page;
                if (page.Generation != first.Generation || page.Revision != first.Revision)
                    break;
                friends.AddRange(page.Friends);
                if (page.NextOffset is not int next_offset)
                {
                    return first with
                    {
                        Matched = friends.Count,
                        Offset = 0,
                        NextOffset = null,
                        Friends = friends.ToArray()
                    };
                }
                offset = next_offset;
            }
        }
        throw new InvalidOperationException("The friend list changed continuously while it was being read.");
    }
}
