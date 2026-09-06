using System.IO;
using System.Text.Json;

namespace Qx.Ui;

/// <summary>An unsaved buffer as it was last seen.</summary>
/// <param name="Name">The tab's name, which is all an untitled buffer has to be known by.</param>
/// <param name="Code">What was in it.</param>
public sealed record Draft(string Name, string Code);

/// <summary>
/// Keeps unsaved buffers on disk so a crash does not take them.
/// </summary>
/// <remarks>
/// <para>
/// Only tabs with no file. A saved script already has somewhere to be, and the ordinary close
/// path asks before discarding anything — so this covers the one case nothing else does: QX going
/// away without being asked, which for a window left open beside a game for hours is the likely
/// way it ends.
/// </para>
/// <para>
/// Written whole and replaced atomically. A draft file caught half-written would be worse than no
/// draft at all, because it would be restored looking complete.
/// </para>
/// </remarks>
public sealed class DraftStore
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "drafts.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private string _lastWritten = "";

    public DraftStore() : this(DefaultPath)
    {
    }

    public DraftStore(string path) => _path = path;

    /// <summary>Reads whatever was left behind, and never throws on a file that cannot be read.</summary>
    public IReadOnlyList<Draft> Load()
    {
        try
        {
            return !File.Exists(_path)
                ? []
                : JsonSerializer.Deserialize<List<Draft>>(File.ReadAllText(_path), Json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Replaces the stored drafts, doing nothing when they have not changed.
    /// </summary>
    /// <remarks>
    /// Called on a timer while someone is typing, so the common case has to cost nothing: the
    /// serialized form is compared against the last one written rather than the disk being
    /// rewritten every few seconds for a buffer nobody touched.
    /// </remarks>
    public void Save(IReadOnlyList<Draft> drafts)
    {
        try
        {
            if (drafts.Count == 0)
            {
                if (_lastWritten.Length == 0)
                    return;
                _lastWritten = "";
                if (File.Exists(_path))
                    File.Delete(_path);
                return;
            }

            string json = JsonSerializer.Serialize(drafts, Json);
            if (string.Equals(json, _lastWritten, StringComparison.Ordinal))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            // Through a temporary file, so a crash mid-write leaves the previous drafts rather
            // than a truncated file that would restore as an empty script.
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
            _lastWritten = json;
        }
        catch
        {
        }
    }

    /// <summary>Forgets everything, for a shutdown that ended cleanly.</summary>
    public void Clear() => Save([]);
}
