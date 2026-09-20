using System.Globalization;
using System.Text;
using Qx.Game;

namespace Qx.Presentation.Services.Room;

public static class RoomText
{
    static readonly string[] _compass =
    [
        "North",
        "Northeast",
        "East",
        "Southeast",
        "South",
        "Southwest",
        "West",
        "Northwest"
    ];

    public static string Compass(int direction) => _compass[((direction % 8) + 8) % 8];

    public static string Words(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "";
        var text = new StringBuilder(identifier.Length + 8);
        for (int index = 0; index < identifier.Length; index++)
        {
            char letter = identifier[index];
            if (index > 0 && char.IsUpper(letter) && !char.IsUpper(identifier[index - 1]))
                text.Append(' ');
            text.Append(char.ToLowerInvariant(letter));
        }
        return text.ToString();
    }

    public static string Rights(int? level, bool owner) => owner
        ? "owner"
        : level switch
        {
            null => "not known yet",
            0 => "none",
            1 => "rights",
            2 => "group member",
            3 => "group admin",
            4 => "owner",
            5 => "moderator",
            _ => level.Value.ToString(CultureInfo.CurrentCulture)
        };

    public static string Clock(DateTime moment) => moment.ToString("HH:mm:ss", CultureInfo.CurrentCulture);

    public static string VisitWindow(DateTime? entered, DateTime? left) => (entered, left) switch
    {
        (null, null) => "was already here",
        (null, { } gone) => $"was already here, left {Clock(gone)}",
        ({ } came, null) => $"came in {Clock(came)}",
        ({ } came, { } gone) => $"{Clock(came)} – {Clock(gone)}"
    };

    public static string Copied(MimicParts parts)
    {
        string[] named =
        [
            .. Enum.GetValues<MimicParts>()
                .Where(part =>
                    part is not (MimicParts.None or MimicParts.Appearance or
                        MimicParts.Behaviour or MimicParts.All) &&
                    parts.HasFlag(part))
                .Select(part => part.ToString().ToLowerInvariant())
        ];
        return named.Length switch
        {
            0 => "nothing",
            1 => named[0],
            _ => $"{string.Join(", ", named[..^1])} and {named[^1]}"
        };
    }
}
