namespace Qx.Scripter;

public static class ScripterPaths
{
    public static string Configuration { get; } = StoragePaths.ConfigurationFor("QX Scripter");

    public static string Scripts => Path.Combine(Configuration, "scripts");
}
