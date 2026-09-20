using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Chat;

public enum ChatLineKind
{
    Message,
    Activity
}

[Flags]
public enum ChatCategory
{
    Users = 1,
    Whispers = 2,
    Wired = 4,
    Pets = 8,
    Bots = 16,
    Trades = 32,
    Joins = 64,
    Leaves = 128
}

public sealed partial class ChatLineViewModel : ObservableObject
{
    public ChatLineViewModel(
        ChatLineKind kind,
        long sequence,
        DateTimeOffset at,
        string name,
        string message = "",
        string tag = "",
        ImageRequest? image = null,
        ChatCategory category = ChatCategory.Users)
    {
        Kind = kind;
        Category = category;
        Sequence = sequence;
        At = at;
        Name = name;
        Message = message;
        Tag = tag;
        Image = image;
        Time = at.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    public ChatLineKind Kind { get; }

    public ChatCategory Category { get; }

    public long Sequence { get; }

    public DateTimeOffset At { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    public string Message { get; }

    public string Tag { get; }

    public string Time { get; }

    public ImageRequest? Image { get; }

    public bool IsActivity => Kind == ChatLineKind.Activity;

    public bool IsMessage => Kind == ChatLineKind.Message;

    public bool HasMessage => Message.Length > 0;

    public bool HasTag => Tag.Length > 0;

    public bool HasHead => Image is not null;

    public bool Matches(string term) =>
        Message.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
        Name.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    public bool IsAfter(ChatLineViewModel other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return At != other.At ? At > other.At : Sequence > other.Sequence;
    }
}
