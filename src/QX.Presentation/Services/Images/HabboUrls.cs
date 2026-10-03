using System.Security.Cryptography;
using System.Text;

namespace Qx.Presentation.Services.Images;

public static class HabboUrls
{
    public static string? Head(string? figure, string webHost) =>
        string.IsNullOrWhiteSpace(figure) ? null : $"https://{Host(webHost)}/habbo-imaging/avatarimage?direction=2&head_direction=2&headonly=1&figure={Uri.EscapeDataString(figure)}";

    public static string? Body(string? figure, string webHost) =>
        string.IsNullOrWhiteSpace(figure) ? null : $"https://{Host(webHost)}/habbo-imaging/avatarimage?direction=2&head_direction=2&figure={Uri.EscapeDataString(figure)}";

    public static string? HeadForName(string? name, string webHost) =>
        string.IsNullOrWhiteSpace(name) ? null : $"https://{Host(webHost)}/habbo-imaging/avatarimage?direction=2&head_direction=2&headonly=1&user={Uri.EscapeDataString(name)}";

    public static string? Figure(string? figure, int direction, string webHost, string size = "n")
    {
        if (string.IsNullOrWhiteSpace(figure))
            return null;
        int turned = ((direction % 8) + 8) % 8;
        return $"https://{Host(webHost)}/habbo-imaging/avatarimage?direction={turned}&head_direction={turned}&size={size}&gesture=sml&action=std&figure={Uri.EscapeDataString(figure)}";
    }

    public static string? RoomThumbnail(long roomId, string webHost) =>
        roomId <= 0 ? null : $"https://habbo-stories-content.s3.amazonaws.com/navigator-thumbnail/hh{HotelIdentifier(webHost)}/{roomId}.png";

    public static string? OfficialRoomPicture(string? pictureRef) =>
        string.IsNullOrWhiteSpace(pictureRef) ? null : $"https://images.habbo.com/web_images/{pictureRef.TrimStart('/')}";

    public static string? FurniIcon(int revision, string? identifier) =>
        revision <= 0 || string.IsNullOrWhiteSpace(identifier) ? null : $"https://images.habbo.com/dcr/hof_furni/{revision}/{identifier.Replace('*', '_')}_icon.png";

    public static bool IsFurniIcon(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.Contains("/dcr/hof_furni/", StringComparison.OrdinalIgnoreCase);
    }

    public static string HotelIdentifier(string webHost)
    {
        string host = Host(webHost).ToLowerInvariant();
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
