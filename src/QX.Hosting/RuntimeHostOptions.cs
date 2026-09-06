using Qx.ClientCatalog.InstalledClients;
using Qx.Interception.GEarth;
using Qx.Mcp;

namespace Qx.Hosting;

public sealed record RuntimeHostOptions
{
    public GEarthOptions GEarth { get; init; } = new();

    public string ScriptsDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "scripts");

    public string SessionRulesPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "rules.json");

    public string HeaderCatalogCachePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QX",
        "header-catalogs");

    public InstalledClientMonitorOptions InstalledClients { get; init; } = new();

    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan TransportRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public bool ReconnectTransport { get; init; }

    public bool EnableTransport { get; init; } = true;

    public bool EnableFallbackCatalogs { get; init; } = true;

    public bool EnableClientMonitoring { get; init; } = true;

    public bool EnableMcp { get; init; } = true;

    public int McpPort { get; init; } = 9390;

    public McpConfig? McpConfiguration { get; init; }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(GEarth);
        ArgumentException.ThrowIfNullOrWhiteSpace(ScriptsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(SessionRulesPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(HeaderCatalogCachePath);
        ArgumentNullException.ThrowIfNull(InstalledClients);
        if (HttpTimeout != Timeout.InfiniteTimeSpan && HttpTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HttpTimeout));
        if (TransportRetryDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TransportRetryDelay));
        if (McpPort is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(McpPort));
        _ = Path.GetFullPath(ScriptsDirectory);
        _ = Path.GetFullPath(SessionRulesPath);
        _ = Path.GetFullPath(HeaderCatalogCachePath);
    }
}
