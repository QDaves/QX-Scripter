using System.Reflection;

namespace Qx;

public static class ProductVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(ProductVersion).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return WithoutMetadata(informational);

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string WithoutMetadata(string version)
    {
        int metadata = version.IndexOf('+');
        return metadata > 0 ? version[..metadata] : version;
    }
}
