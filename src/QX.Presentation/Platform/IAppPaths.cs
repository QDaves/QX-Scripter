namespace Qx.Presentation.Platform;

public interface IAppPaths
{
    string ConfigRoot { get; }

    string ScriptsDirectory { get; }

    string SettingsFile { get; }

    string LibraryFile { get; }

    string DraftsFile { get; }

    string WardrobeFile { get; }

    string RulesFile { get; }

    string McpConfigFile { get; }

    string LogsDirectory { get; }

    string LogFile { get; }

    string CrashLogFile { get; }

    string ImageCacheDirectory { get; }

    string HeaderCatalogCache { get; }
}
