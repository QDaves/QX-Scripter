using MaterialDesignThemes.Wpf;
using Qx.Model;

namespace Qx.Ui;

/// <summary>
/// One line on any of the game pages.
/// </summary>
/// <remarks>
/// A friend, an inventory item, an outfit and a line of chat all draw the same four things: a
/// picture, a name, a line under it and something small on the right. One row type for all of them
/// keeps the pages looking like one tool rather than four that happen to ship together.
/// </remarks>
public sealed class GameRow(string? imageUrl = null) : RemoteImage
{
    public required string Name { get; init; }

    /// <summary>The line under the name.</summary>
    public string Detail { get; init; } = "";

    /// <summary>What sits on the right: a time, a count, a category.</summary>
    public string Trailing { get; init; } = "";

    /// <summary>A short word beside the name, such as who is speaking or what kind of thing it is.</summary>
    public string Tag { get; init; } = "";

    public bool IsOnline { get; init; }

    /// <summary>
    /// How strongly the picture is drawn.
    /// </summary>
    /// <remarks>
    /// An offline friend is shown faded rather than in a second colour, so the list says who is
    /// about without a legend and without a column spent on it.
    /// </remarks>
    public double Dimmed => IsOnline ? 1.0 : 0.45;

    /// <summary>
    /// How much grey is washed over the picture.
    /// </summary>
    /// <remarks>
    /// Fading the picture instead let whatever stands behind it show through, which is what put a
    /// half-visible placeholder inside every offline head. A wash sits on top and follows the
    /// picture's own shape, so an offline friend goes grey rather than transparent.
    /// </remarks>
    public double Greyed => IsOnline ? 0.0 : 0.55;

    /// <summary>What the row stands for, so an action can find it again.</summary>
    public Id Key { get; init; }

    /// <summary>
    /// The row's id as a person reads it.
    /// </summary>
    /// <remarks>
    /// A furni carries the same number in the room and in the inventory, negated while it is held.
    /// Showing the sign says nothing anybody wants to know and reads as a different item; the value
    /// itself keeps its sign, because the messages that place an item into a trade want it as it is.
    /// </remarks>
    public string KeyText => (long)Key < 0 ? (-(long)Key).ToString() : Key.ToString();

    /// <summary>Which half of the wire this is, where the row stands for furniture.</summary>
    public ItemType? ItemKind { get; init; }

    /// <summary>
    /// Every copy the row folded together.
    /// </summary>
    /// <remarks>
    /// A row stands for a kind rather than a thing, so selling from it needs the copies themselves.
    /// <see cref="Key"/> names only the first, which is enough to identify the row and not enough to
    /// act on it.
    /// </remarks>
    public IReadOnlyList<Id> ItemIds { get; init; } = [];

    /// <summary>The class name the marketplace knows the kind by.</summary>
    public string Identifier { get; init; } = "";

    /// <summary>Whether the hotel lets this change hands at all.</summary>
    public bool Tradeable { get; init; }

    /// <summary>
    /// Whether an offer could be made for this row.
    /// </summary>
    /// <remarks>
    /// A pet and a bot are neither, whatever the market says about furniture: they have no class
    /// name to ask a price for and no category to sell under.
    /// </remarks>
    public bool CanSell => Tradeable && ItemKind is not null && Identifier.Length > 0;

    private MarketplacePrice? _market;

    /// <summary>What the hotel's marketplace says the kind is going for, once it has been asked.</summary>
    public MarketplacePrice? Market
    {
        get => _market;
        set
        {
            _market = value;
            Raise(nameof(Market));
            Raise(nameof(MarketText));
            Raise(nameof(HasMarket));
            Raise(nameof(MarketValue));
        }
    }

    public bool HasMarket => Market?.IsKnown == true;

    /// <summary>
    /// The asking price as a number, for sorting and for filtering by it.
    /// </summary>
    /// <remarks>
    /// Sorting the column on its own text puts <c>9c</c> above <c>~120c</c> and both below anything
    /// with a tilde, which orders the market by punctuation. A kind with no price sorts below every
    /// kind that has one rather than as free.
    /// </remarks>
    public int MarketValue => Market?.Suggested ?? -1;

    /// <summary>
    /// The price as a reader should see it.
    /// </summary>
    /// <remarks>
    /// A tilde marks a figure taken from what the kind has been going for rather than from an offer
    /// standing right now, because the two are not the same promise. Nothing at all is shown while
    /// the answer is still being fetched, so an empty cell never reads as "free".
    /// </remarks>
    public string MarketText => !CanSell
        ? "—"
        : Market is null
            ? ""
            : Market.IsCurrent
                ? $"{Market.CurrentPrice}c"
                : Market.IsKnown
                    ? $"~{Market.AveragePrice}c"
                    : "no offers";

    public override string? ImageUrl { get; } = imageUrl;

    public bool HasDetail => Detail.Length > 0;
    public bool HasTag => Tag.Length > 0;

    /// <summary>Stands in for a picture that never arrives, so a row is never blank.</summary>
    public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

    /// <summary>
    /// What kind of thing this is, as a column of its own.
    /// </summary>
    /// <remarks>
    /// A table wants each fact in its own cell. Folding the kind into the line under the name is
    /// fine for a list of rows and useless the moment anyone wants to sort by it.
    /// </remarks>
    public string Group { get; init; } = "";

    /// <summary>How many of this kind are owned; one unless the row stands for several.</summary>
    public int Count { get; init; } = 1;

    public bool HasMany => Count > 1;

    /// <summary>The glyph shown where no picture arrives.</summary>
    public PackIconKind Fallback { get; init; } = PackIconKind.PackageVariantClosed;

    /// <summary>What a tile says when the pointer rests on it.</summary>
    public string Tooltip => Detail.Length > 0
        ? $"{Name}\n{Detail}" + (HasMany ? $"\n{Count} owned" : "")
        : Name + (HasMany ? $"\n{Count} owned" : "");
}
