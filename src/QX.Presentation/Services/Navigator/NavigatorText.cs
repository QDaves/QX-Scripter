using System.Globalization;
using Qx.Game;
using Qx.Model;

namespace Qx.Presentation.Services.Navigator;

public static class NavigatorText
{
    public const string MetadataNotLoaded = "Navigator metadata has not been loaded yet.";

    public static string Summary(NavigatorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.MetadataLoaded)
            return MetadataNotLoaded;
        string home = state.Settings is { HomeRoomId: var home_id } && home_id != 0 ? $" · home {home_id}" : "";
        return $"{state.Categories.Count:N0} categories · {state.SavedSearches.Count:N0} saved{home}";
    }

    public static string Rooms(int count) => count == 1 ? "1 room" : $"{count:N0} rooms";

    public static string Copied(int rows) => rows == 1 ? "Copied 1 row." : $"Copied {rows:N0} rows.";

    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Door(int mode) => (RoomDoorMode)mode switch
    {
        RoomDoorMode.Open => "Open",
        RoomDoorMode.Doorbell => "Doorbell",
        RoomDoorMode.Password => "Password",
        RoomDoorMode.Invisible => "Invisible",
        RoomDoorMode.NewUsersOnly => "New users",
        _ => ""
    };

    public static string? Detail(string description, IReadOnlyList<string> tags, string group, string happening)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var lines = new List<string>(4);
        if (description is { Length: > 0 })
            lines.Add(description);
        if (tags.Count > 0)
            lines.Add("Tags: " + string.Join(", ", tags));
        if (group is { Length: > 0 })
            lines.Add("Group: " + group);
        if (happening is { Length: > 0 })
            lines.Add("Event: " + happening);
        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }
}
