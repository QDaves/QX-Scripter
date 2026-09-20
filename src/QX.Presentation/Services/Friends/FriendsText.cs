namespace Qx.Presentation.Services.Friends;

public static class FriendsText
{
    public const string Online = "online";
    public const string DefaultGender = "M";
    public const long NeverSeen = long.MaxValue;
    public const long SeenNow = -1;

    public static string Ago(long minutes)
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

    public static string LastSeen(bool is_online, long minutes) => is_online ? Online : Ago(minutes);

    public static long LastSeenOrder(bool is_online, long minutes) =>
        is_online ? SeenNow : minutes <= 0 ? NeverSeen : minutes;

    public static string Summary(int total, int online) =>
        $"{total:N0} {(total == 1 ? "friend" : "friends")}, {online:N0} online";

    public static string Shown(int visible, int total) =>
        visible == total ? $"{visible:N0} shown" : $"{visible:N0} of {total:N0} shown";

    public static string Removed(int count) => count == 1 ? "Friend removed." : $"{count} friends removed.";

    public static string Copied(int rows) => rows == 1 ? "Copied 1 row." : $"Copied {rows} rows.";

    public static string Gender(string? gender) =>
        string.IsNullOrWhiteSpace(gender) || string.Equals(gender, "None", StringComparison.OrdinalIgnoreCase)
            ? DefaultGender
            : gender.Trim()[..1].ToUpperInvariant();

    public static string WhisperPrefix(string name) => $"/whisper {name} ";
}
