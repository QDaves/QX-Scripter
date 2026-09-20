using System.Security.Cryptography;
using System.Text;

namespace Qx.Presentation.Services.Images;

public static class HabboUrls
{
    public static string? Head(string? figure, string web_host) =>
        string.IsNullOrWhiteSpace(figure) ? null : $"https://{Host(web_host)}/habbo-imaging/avatarimage?direction=2&head_direction=2&headonly=1&figure={Uri.EscapeDataString(figure)}";

    public static string? Body(string? figure, string web_host) =>
        string.IsNullOrWhiteSpace(figure) ? null : $"https://{Host(web_host)}/habbo-imaging/avatarimage?direction=2&head_direction=2&figure={Uri.EscapeDataString(figure)}";

    public static string? HeadForName(string? name, string web_host) =>
        string.IsNullOrWhiteSpace(name) ? null : $"https://{Host(web_host)}/habbo-imaging/avatarimage?direction=2&head_direction=2&headonly=1&user={Uri.EscapeDataString(name)}";

    public static string? Figure(string? figure, int direction, string web_host, string size = "n")
    {
        if (string.IsNullOrWhiteSpace(figure))
            return null;
        int turned = ((direction % 8) + 8) % 8;
        return $"https://{Host(web_host)}/habbo-imaging/avatarimage?direction={turned}&head_direction={turned}&size={size}&gesture=sml&action=std&figure={Uri.EscapeDataString(figure)}";
    }

    public static string? RoomThumbnail(long room_id, string web_host) =>
        room_id <= 0 ? null : $"https://habbo-stories-content.s3.amazonaws.com/navigator-thumbnail/hh{HotelIdentifier(web_host)}/{room_id}.png";

    public static string? OfficialRoomPicture(string? picture_ref) =>
        string.IsNullOrWhiteSpace(picture_ref) ? null : $"https://images.habbo.com/web_images/{picture_ref.TrimStart('/')}";

    public static string? FurniIcon(int revision, string? identifier) =>
        revision <= 0 || string.IsNullOrWhiteSpace(identifier) ? null : $"https://images.habbo.com/dcr/hof_furni/{revision}/{identifier.Replace('*', '_')}_icon.png";

    public static bool IsFurniIcon(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.Contains("/dcr/hof_furni/", StringComparison.OrdinalIgnoreCase);
    }

    public static string HotelIdentifier(string web_host)
    {
        string host = Host(web_host).ToLowerInvariant();
        if (host == "sandbox.habbo.com")
            return "s2";
        if (host.EndsWith(".habbo.com.br", StringComparison.Ordinal))
            return "br";
        if (host.EndsWith(".habbo.com.tr", StringComparison.Ordinal))
            return "tr";
        if (host.EndsWith(".habbo.com", StringComparison.Ordinal) || !host.Contains('.'))
            return "us";
        return host[(host.LastIndexOf('.') + 1)..];
    }

    public static string DiskName(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return hash[..32];
    }

    static string Host(string web_host) => string.IsNullOrWhiteSpace(web_host) ? HotelContext.DefaultWebHost : web_host;
}
