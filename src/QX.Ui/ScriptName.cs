using System.IO;

namespace Qx.Ui;

/// <summary>
/// Turns what a user typed into a name a script file can carry. Shared by save, rename and the
/// untitled sequence, so the same text lands on the same file whichever of them asked for it.
/// </summary>
public static class ScriptName
{
    public const string Extension = ".csx";
    public const string Fallback = "untitled";

    /// <summary>
    /// Trims the name, drops a typed extension rather than doubling it, and replaces the
    /// characters a file name cannot hold. Nothing usable falls back to <see cref="Fallback"/>.
    /// </summary>
    public static string Normalize(string? typed)
    {
        string name = (typed ?? "").Trim();

        if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            name = name[..^Extension.Length].TrimEnd();

        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');

        name = name.Trim();
        return name.Length == 0 ? Fallback : name;
    }

    /// <summary>The file a normalised name maps to inside <paramref name="directory"/>.</summary>
    public static string PathIn(string directory, string typed) =>
        Path.Combine(directory, Normalize(typed) + Extension);
}
