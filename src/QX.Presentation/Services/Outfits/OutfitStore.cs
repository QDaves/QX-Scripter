using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Outfits;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<SavedOutfit>))]
internal sealed partial class OutfitJson : JsonSerializerContext;

public sealed class OutfitStore : IOutfitStore, IDisposable
{
    readonly List<SavedOutfit> _outfits = [];
    readonly AtomicJsonFile _file;

    public OutfitStore(IAppPaths paths, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Load(paths.WardrobeFile);
        _file = new AtomicJsonFile(paths.WardrobeFile, "wardrobe", "Wardrobe could not be saved", Serialize, dispatcher, time);
    }

    public IReadOnlyList<SavedOutfit> Outfits => _outfits;

    public event Action? Changed;

    public bool Contains(string figure) => IndexOf(figure) >= 0;

    public bool Add(SavedOutfit outfit)
    {
        ArgumentNullException.ThrowIfNull(outfit);
        if (string.IsNullOrWhiteSpace(outfit.Figure) || Contains(outfit.Figure))
            return false;
        _outfits.Add(outfit);
        Publish();
        return true;
    }

    public int AddRange(IEnumerable<SavedOutfit> outfits)
    {
        ArgumentNullException.ThrowIfNull(outfits);
        int added = 0;
        foreach (SavedOutfit outfit in outfits)
        {
            if (string.IsNullOrWhiteSpace(outfit.Figure) || Contains(outfit.Figure))
                continue;
            _outfits.Add(outfit);
            added++;
        }
        if (added > 0)
            Publish();
        return added;
    }

    public int RemoveRange(IEnumerable<SavedOutfit> outfits)
    {
        ArgumentNullException.ThrowIfNull(outfits);
        int removed = 0;
        foreach (SavedOutfit outfit in outfits.ToArray())
        {
            int at = IndexOf(outfit.Figure);
            if (at < 0)
                continue;
            _outfits.RemoveAt(at);
            removed++;
        }
        if (removed > 0)
            Publish();
        return removed;
    }

    public bool Rename(SavedOutfit outfit, string name)
    {
        ArgumentNullException.ThrowIfNull(outfit);
        ArgumentNullException.ThrowIfNull(name);
        int at = IndexOf(outfit.Figure);
        if (at < 0)
            return false;
        string trimmed = name.Trim();
        if (string.Equals(_outfits[at].Name, trimmed, StringComparison.Ordinal))
            return false;
        _outfits[at] = _outfits[at] with { Name = trimmed };
        Publish();
        return true;
    }

    public Task FlushAsync(CancellationToken cancellation_token) => _file.FlushAsync(cancellation_token);

    public bool FlushNow(TimeSpan budget) => _file.FlushNow(budget);

    public void Dispose() => _file.Dispose();

    int IndexOf(string figure) =>
        _outfits.FindIndex(kept => string.Equals(kept.Figure, figure, StringComparison.OrdinalIgnoreCase));

    void Publish()
    {
        Changed?.Invoke();
        _file.Schedule();
    }

    string Serialize() => JsonSerializer.Serialize(_outfits, OutfitJson.Default.ListSavedOutfit);

    void Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            List<SavedOutfit>? kept = JsonSerializer.Deserialize(File.ReadAllText(path), OutfitJson.Default.ListSavedOutfit);
            if (kept is null)
                return;
            _outfits.AddRange(kept.Where(outfit => !string.IsNullOrWhiteSpace(outfit.Figure)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            _outfits.Clear();
        }
    }
}
