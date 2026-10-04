using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Files;

public static class ScriptFileName
{
    public const string Untitled = "untitled";
    public const string Extension = ".csx";

    static readonly char[] _invalid_characters = [.. "\"<>|:*?\\/", .. Enumerable.Range(0, 32).Select(code => (char)code)];

    static readonly HashSet<string> _reserved_names = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(number => new[] { $"COM{number}", $"LPT{number}" })],
        StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? typed)
    {
        string name = (typed ?? "").Trim();
        if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            name = name[..^Extension.Length];
        foreach (char invalid in _invalid_characters)
            name = name.Replace(invalid, '_');
        name = name.Trim().TrimEnd('.', ' ');
        if (name.Length == 0)
            return Untitled;
        return _reserved_names.Contains(name) ? name + "_" : name;
    }

    public static string PathIn(string directory, string typed) =>
        Path.Combine(directory, Normalize(typed) + Extension);

    public static string NameOf(string path) => Path.GetFileNameWithoutExtension(path);

    public static bool IsScript(string path) =>
        path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    public static string NextUntitled(IEnumerable<string> openNames, Func<string, bool> existsOnDisk)
    {
        ArgumentNullException.ThrowIfNull(openNames);
        ArgumentNullException.ThrowIfNull(existsOnDisk);
        var taken = new HashSet<string>(openNames, StringComparer.OrdinalIgnoreCase);
        for (int number = 1; ; number++)
        {
            string candidate = number == 1 ? Untitled : $"{Untitled} {number}";
            if (!taken.Contains(candidate) && !existsOnDisk(candidate))
                return candidate;
        }
    }

    public static string NextCopy(string original, Func<string, bool> existsOnDisk)
    {
        ArgumentNullException.ThrowIfNull(existsOnDisk);
        for (int number = 1; ; number++)
        {
            string candidate = number == 1 ? $"{original} copy" : $"{original} copy {number}";
            if (!existsOnDisk(candidate))
                return candidate;
        }
    }

    public static string? FromDirective(string code)
    {
        if (ScriptHeader.Parse(code).Name is not { } declared)
            return null;
        string name = Normalize(declared);
        return name == Untitled ? null : name;
    }
}
