namespace Qx;

public static class StoragePaths
{
    public static string Configuration { get; } = Path.Combine(ConfigurationHome(), "QX Scripter");

    public static string Cache { get; } = Path.Combine(CacheHome(), "QX");

    public static string Scripts => Path.Combine(Configuration, "scripts");

    public static StringComparer FileComparer => IgnoresPathCase
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static StringComparison FileComparison => IgnoresPathCase
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IgnoresPathCase => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    public static string ConfigurationHome()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(UserHome(), "Library", "Application Support");
        if (OperatingSystem.IsLinux())
            return XdgDirectory("XDG_CONFIG_HOME", ".config");
        return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    }

    private static string CacheHome()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(UserHome(), "Library", "Caches");
        if (OperatingSystem.IsLinux())
            return XdgDirectory("XDG_CACHE_HOME", ".cache");
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    private static string XdgDirectory(string variable, string fallback)
    {
        string? directory = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory)
            ? Path.GetFullPath(directory) : Path.Combine(UserHome(), fallback);
    }

    private static string UserHome()
    {
        string directory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The user profile directory is unavailable.");
        return directory;
    }
}
