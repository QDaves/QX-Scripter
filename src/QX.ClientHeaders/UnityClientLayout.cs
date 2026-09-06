namespace Qx.Unity;

public sealed record UnityClientLayout(
    string RootPath,
    string GameAssemblyPath,
    string MetadataPath,
    string? Version)
{
    const string GameAssemblyName = "GameAssembly.dll";
    const string MetadataName = "global-metadata.dat";

    public static UnityClientLayout Locate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full_path = Path.GetFullPath(path);

        if (File.Exists(full_path))
        {
            string name = Path.GetFileName(full_path);
            if (name.Equals(GameAssemblyName, StringComparison.OrdinalIgnoreCase))
                return FromStandaloneRoot(Path.GetDirectoryName(full_path)!);
            if (name.Equals(MetadataName, StringComparison.OrdinalIgnoreCase))
                return FromMetadata(full_path);
            throw new InvalidDataException($"Unsupported Unity client file '{name}'.");
        }

        if (!Directory.Exists(full_path))
            throw new DirectoryNotFoundException($"Unity client path was not found: {full_path}");

        string standalone = Path.Combine(full_path, "StandaloneWindows");
        if (File.Exists(Path.Combine(standalone, GameAssemblyName)))
            return FromStandaloneRoot(standalone);
        if (File.Exists(Path.Combine(full_path, GameAssemblyName)))
            return FromStandaloneRoot(full_path);

        throw new InvalidDataException($"Unity client path does not contain {GameAssemblyName}: {full_path}");
    }

    static UnityClientLayout FromMetadata(string metadata_path)
    {
        DirectoryInfo? current = new FileInfo(metadata_path).Directory;
        while (current is not null)
        {
            string assembly_path = Path.Combine(current.FullName, GameAssemblyName);
            if (File.Exists(assembly_path))
                return Build(current.FullName, assembly_path, metadata_path);
            current = current.Parent;
        }
        throw new InvalidDataException($"Could not locate {GameAssemblyName} for metadata '{metadata_path}'.");
    }

    static UnityClientLayout FromStandaloneRoot(string root)
    {
        string assembly_path = Path.Combine(root, GameAssemblyName);
        string[] metadata_paths;
        try
        {
            metadata_paths = Directory.GetFiles(root, MetadataName, SearchOption.AllDirectories);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Unable to inspect Unity client directory '{root}'.", error);
        }

        if (metadata_paths.Length != 1)
            throw new InvalidDataException(
                $"Expected one {MetadataName} below '{root}', found {metadata_paths.Length}.");
        return Build(root, assembly_path, metadata_paths[0]);
    }

    static UnityClientLayout Build(string root, string assembly_path, string metadata_path)
    {
        Span<byte> signature = stackalloc byte[2];
        using (var file = new FileStream(assembly_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            file.ReadExactly(signature);
        if (signature[0] != (byte)'M' || signature[1] != (byte)'Z')
            throw new InvalidDataException($"{GameAssemblyName} is not a Windows PE file.");

        string? version = new DirectoryInfo(root).Parent?.Name;
        if (version?.Equals("StandaloneWindows", StringComparison.OrdinalIgnoreCase) == true)
            version = new DirectoryInfo(root).Parent?.Parent?.Name;
        return new UnityClientLayout(
            Path.GetFullPath(root),
            Path.GetFullPath(assembly_path),
            Path.GetFullPath(metadata_path),
            version);
    }
}
