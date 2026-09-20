namespace Qx.Presentation.Services.Outfits;

public interface IOutfitStore
{
    IReadOnlyList<SavedOutfit> Outfits { get; }

    event Action? Changed;

    bool Contains(string figure);

    bool Add(SavedOutfit outfit);

    int AddRange(IEnumerable<SavedOutfit> outfits);

    int RemoveRange(IEnumerable<SavedOutfit> outfits);

    bool Rename(SavedOutfit outfit, string name);

    Task FlushAsync(CancellationToken cancellation_token);

    bool FlushNow(TimeSpan budget);
}
