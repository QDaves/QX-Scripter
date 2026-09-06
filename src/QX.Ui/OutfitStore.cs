using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Ui;

/// <summary>One saved outfit.</summary>
/// <param name="Figure">The figure string, which is also what makes it unique.</param>
/// <param name="Gender">"M" or "F", as the hotel spells it.</param>
/// <param name="Name">What you called it, or empty for the figure to speak for itself.</param>
public sealed record SavedOutfit(string Figure, string Gender, string Name = "");

/// <summary>
/// The outfits you keep, as opposed to the ones the hotel keeps.
/// </summary>
/// <remarks>
/// <para>
/// The in-game wardrobe holds ten slots and lives on the hotel. This holds as many as you like and
/// lives on this machine, which is the point: a figure you liked on somebody else, or one you wore
/// two years ago, has nowhere to go in ten slots.
/// </para>
/// <para>
/// Keyed by the figure string, so importing the in-game wardrobe twice adds nothing the second
/// time, and neither does adding what you are already wearing.
/// </para>
/// </remarks>
public sealed class OutfitStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "wardrobe.json");

    private readonly string _path;
    private readonly List<SavedOutfit> _outfits = [];

    /// <summary>
    /// The one shelf everything shares.
    /// </summary>
    /// <remarks>
    /// Three places add to the wardrobe and one shows it. Each holding its own copy meant an outfit
    /// added from a room was written to the file and never seen by the page already open on it, so
    /// it appeared only after a restart. One instance and one list, with a note when it changes.
    /// </remarks>
    public static OutfitStore Shared { get; } = new(DefaultPath);

    /// <summary>Raised whenever the shelf gains or loses something.</summary>
    public event Action? Changed;

    public OutfitStore() : this(DefaultPath)
    {
    }

    public OutfitStore(string path)
    {
        _path = path;
        Load();
    }

    public IReadOnlyList<SavedOutfit> Outfits => _outfits;

    public int Count => _outfits.Count;

    /// <summary>Adds one outfit, unless the same figure is already kept.</summary>
    /// <returns>Whether anything was added.</returns>
    public bool Add(SavedOutfit outfit)
    {
        if (string.IsNullOrWhiteSpace(outfit.Figure))
            return false;
        if (_outfits.Any(kept => string.Equals(kept.Figure, outfit.Figure, StringComparison.OrdinalIgnoreCase)))
            return false;

        _outfits.Add(outfit);
        Save();
        return true;
    }

    /// <summary>Adds several at once, and says how many were new.</summary>
    public int AddRange(IEnumerable<SavedOutfit> outfits)
    {
        int added = 0;
        foreach (SavedOutfit outfit in outfits)
        {
            if (string.IsNullOrWhiteSpace(outfit.Figure))
                continue;
            if (_outfits.Any(kept => string.Equals(kept.Figure, outfit.Figure, StringComparison.OrdinalIgnoreCase)))
                continue;

            _outfits.Add(outfit);
            added++;
        }

        if (added > 0)
            Save();
        return added;
    }

    public bool Remove(SavedOutfit outfit)
    {
        int at = _outfits.FindIndex(kept =>
            string.Equals(kept.Figure, outfit.Figure, StringComparison.OrdinalIgnoreCase));
        if (at < 0)
            return false;

        _outfits.RemoveAt(at);
        Save();
        return true;
    }

    public int RemoveRange(IEnumerable<SavedOutfit> outfits)
    {
        int removed = 0;
        foreach (SavedOutfit outfit in outfits.ToArray())
        {
            int at = _outfits.FindIndex(kept =>
                string.Equals(kept.Figure, outfit.Figure, StringComparison.OrdinalIgnoreCase));
            if (at < 0)
                continue;

            _outfits.RemoveAt(at);
            removed++;
        }

        if (removed > 0)
            Save();
        return removed;
    }

    /// <summary>Renames one, keeping its place in the list.</summary>
    public void Rename(SavedOutfit outfit, string name)
    {
        int at = _outfits.FindIndex(kept =>
            string.Equals(kept.Figure, outfit.Figure, StringComparison.OrdinalIgnoreCase));
        if (at < 0)
            return;

        _outfits[at] = _outfits[at] with { Name = name.Trim() };
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            if (JsonSerializer.Deserialize<List<SavedOutfit>>(File.ReadAllText(_path), Json) is not { } kept)
                return;

            _outfits.AddRange(kept.Where(outfit => !string.IsNullOrWhiteSpace(outfit.Figure)));
        }
        catch
        {
            // A wardrobe that cannot be read starts empty rather than stopping the tool. Nothing is
            // overwritten until something is actually added.
        }
    }

    private void Save()
    {
        Changed?.Invoke();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_outfits, Json));
        }
        catch
        {
        }
    }
}
