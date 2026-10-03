namespace Qx.Presentation.Platform;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
        : this(
            StoragePaths.Configuration,
            StoragePaths.Cache,
            Path.GetTempPath())
    {
    }

    public AppPaths(string configRoot, string localRoot, string tempRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);
        ConfigRoot = configRoot;
        ScriptsDirectory = Path.Combine(configRoot, "scripts");
        SettingsFile = Path.Combine(configRoot, "settings.json");
        LibraryFile = Path.Combine(configRoot, "library.json");
        DraftsFile = Path.Combine(configRoot, "drafts.json");
        WardrobeFile = Path.Combine(configRoot, "wardrobe.json");
        RulesFile = Path.Combine(configRoot, "rules.json");
        McpConfigFile = Path.Combine(configRoot, "mcp.json");
        LogsDirectory = Path.Combine(configRoot, "logs");
        LogFile = Path.Combine(LogsDirectory, "qx.log");
        CrashLogFile = OperatingSystem.IsLinux() ? Path.Combine(LogsDirectory, "qx_crash.log") : Path.Combine(tempRoot, "qx_crash.log");
        ImageCacheDirectory = Path.Combine(configRoot, "imagecache");
        HeaderCatalogCache = Path.Combine(localRoot, "header-catalogs");
    }

    public string ConfigRoot { get; }

    public string ScriptsDirectory { get; }

    public string SettingsFile { get; }

    public string LibraryFile { get; }

    public string DraftsFile { get; }

    public string WardrobeFile { get; }

    public string RulesFile { get; }

    public string McpConfigFile { get; }

    public string LogsDirectory { get; }

    public string LogFile { get; }

    public string CrashLogFile { get; }

    public string ImageCacheDirectory { get; }

    public string HeaderCatalogCache { get; }
}
