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

    public AppPaths(string config_root, string local_root, string temp_root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config_root);
        ArgumentException.ThrowIfNullOrWhiteSpace(local_root);
        ArgumentException.ThrowIfNullOrWhiteSpace(temp_root);
        ConfigRoot = config_root;
        ScriptsDirectory = Path.Combine(config_root, "scripts");
        SettingsFile = Path.Combine(config_root, "settings.json");
        LibraryFile = Path.Combine(config_root, "library.json");
        DraftsFile = Path.Combine(config_root, "drafts.json");
        WardrobeFile = Path.Combine(config_root, "wardrobe.json");
        RulesFile = Path.Combine(config_root, "rules.json");
        McpConfigFile = Path.Combine(config_root, "mcp.json");
        LogsDirectory = Path.Combine(config_root, "logs");
        LogFile = Path.Combine(LogsDirectory, "qx.log");
        CrashLogFile = OperatingSystem.IsLinux() ? Path.Combine(LogsDirectory, "qx_crash.log") : Path.Combine(temp_root, "qx_crash.log");
        ImageCacheDirectory = Path.Combine(config_root, "imagecache");
        HeaderCatalogCache = Path.Combine(local_root, "header-catalogs");
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
