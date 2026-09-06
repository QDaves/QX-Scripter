using System.ComponentModel;
using System.Windows.Media;

namespace Qx.Ui;

/// <summary>
/// One outfit as a tile: the whole figure, turnable.
/// </summary>
/// <remarks>
/// The direction lives on the tile rather than on what is saved, because which way a figure happens
/// to be facing is how you are looking at it, not part of the outfit. Turning one re-asks the
/// imaging host, which is why the url is rebuilt rather than the picture rotated.
/// </remarks>
public sealed class OutfitTile : RemoteImage
{
    private int _direction = 2;
    private string? _url;
    private bool _turning;

    public OutfitTile(SavedOutfit outfit)
    {
        Outfit = outfit;
        _url = HabboImages.FigureUrl(outfit.Figure, _direction);
    }

    public SavedOutfit Outfit { get; }

    public string Figure => Outfit.Figure;

    public string Gender => Outfit.Gender;

    /// <summary>What it is called, falling back to something short from the figure itself.</summary>
    public string Title => Outfit.Name is { Length: > 0 } name ? name : Short(Outfit.Figure);

    public bool IsFemale => string.Equals(Outfit.Gender, "F", StringComparison.OrdinalIgnoreCase);

    public override string? ImageUrl => _url;

    public int Direction => _direction;

    /// <summary>
    /// Turns the figure, once the next view is in hand.
    /// </summary>
    /// <remarks>
    /// Each direction is its own render at its own address, so turning is a fetch. Pointing the tile
    /// at the new address first emptied it until the picture arrived, which is exactly the flicker
    /// that made the turn look broken. The old view stays up until the new one is ready, and a fetch
    /// that fails changes nothing at all rather than leaving a hole.
    /// </remarks>
    public void Turn(int by) => Observe(() => TurnAsync(by));

    private async Task TurnAsync(int by)
    {
        if (_turning)
            return;

        int next = (((_direction + by) % 8) + 8) % 8;
        string? url = HabboImages.FigureUrl(Outfit.Figure, next);
        if (url is null)
            return;

        _turning = true;
        try
        {
            ImageSource? picture = await HabboImages.LoadAsync(url).ConfigureAwait(true);
            if (picture is null)
                return;

            _direction = next;
            _url = url;
            Adopt(picture);
        }
        finally
        {
            _turning = false;
        }
    }

    /// <summary>
    /// The first two parts of a figure, which is enough to tell one tile from another.
    /// </summary>
    /// <remarks>
    /// A whole figure string is eighty characters of numbers and would fill the tile with something
    /// nobody reads.
    /// </remarks>
    private static string Short(string figure)
    {
        string[] parts = figure.Split('.');
        return parts.Length <= 2 ? figure : string.Join('.', parts.Take(2)) + "…";
    }
}
