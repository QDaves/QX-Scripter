using System.Globalization;

namespace Qx.Presentation.Platform;

public sealed record ProfileOverrides(string? Root, int? McpPort)
{
    public static ProfileOverrides FromEnvironment()
    {
        string? root = Environment.GetEnvironmentVariable("QX_PROFILE_ROOT");
        string? port = Environment.GetEnvironmentVariable("QX_MCP_PORT");
        return new ProfileOverrides(
            string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root),
            int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) && parsed is > 0 and <= ushort.MaxValue ? parsed : null);
    }

    public IAppPaths Paths() =>
        Root is null ? new AppPaths() : new AppPaths(Path.Combine(Root, "config"), Path.Combine(Root, "local"), Path.Combine(Root, "temp"));
}
