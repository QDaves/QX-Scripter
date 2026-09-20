using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Snapshots;
using Qx.Presentation.Services.Friends;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Friends;

public sealed record FriendFilter(string Term, bool OnlineOnly);

public sealed record FriendText(string Name, string Motto);

public sealed partial class FriendRow : ObservableObject
{
    public FriendRow(FriendSnapshot friend, bool is_here, string? head)
    {
        ArgumentNullException.ThrowIfNull(friend);
        Id = friend.Id;
        IdText = ((long)friend.Id).ToString(CultureInfo.InvariantCulture);
        Take(friend, is_here, head);
    }

    public long Id { get; }

    public string IdText { get; }

    public FriendText Text { get; private set; } = new("", "");

    [ObservableProperty]
    public partial string Name { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MottoTip))]
    public partial string Motto { get; private set; } = "";

    [ObservableProperty]
    public partial string Figure { get; private set; } = "";

    [ObservableProperty]
    public partial string Gender { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastSeen), nameof(LastSeenOrder))]
    public partial bool IsOnline { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastSeen), nameof(LastSeenOrder))]
    public partial long LastOnline { get; private set; }

    [ObservableProperty]
    public partial bool IsHere { get; set; }

    [ObservableProperty]
    public partial ImageRequest? Head { get; private set; }

    public string? MottoTip => Motto.Length == 0 ? null : Motto;

    public string LastSeen => FriendsText.LastSeen(IsOnline, LastOnline);

    public long LastSeenOrder => FriendsText.LastSeenOrder(IsOnline, LastOnline);

    public void Take(FriendSnapshot friend, bool is_here, string? head)
    {
        ArgumentNullException.ThrowIfNull(friend);
        Name = friend.Name;
        Motto = friend.Motto;
        Figure = friend.Figure;
        Gender = friend.Gender;
        IsOnline = friend.IsOnline;
        LastOnline = friend.LastOnline;
        IsHere = is_here;
        Text = new FriendText(friend.Name, friend.Motto);
        Head = head is { Length: > 0 } url ? new ImageRequest(url, false, IconKind.None) : null;
    }

    public bool Matches(FriendFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.OnlineOnly && !IsOnline)
            return false;
        if (filter.Term.Length == 0)
            return true;
        FriendText text = Text;
        return text.Name.Contains(filter.Term, StringComparison.CurrentCultureIgnoreCase) ||
            text.Motto.Contains(filter.Term, StringComparison.CurrentCultureIgnoreCase);
    }
}
