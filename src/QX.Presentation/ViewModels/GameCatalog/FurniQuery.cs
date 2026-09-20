using System.Globalization;
using Qx.Model;
using Qx.Presentation.Services.FurniLookup;

namespace Qx.Presentation.ViewModels.GameCatalog;

public sealed record FurniFilter(
    string Term,
    FurniPlacementFilter Placement,
    FurniAvailability Availability,
    string Line,
    string Category,
    string MinPrice,
    string MaxPrice)
{
    public static FurniFilter Empty { get; } = new("", FurniPlacementFilter.All, FurniAvailability.Anywhere, "", "", "", "");

    public int ActiveCount =>
        (Placement == FurniPlacementFilter.All ? 0 : 1) +
        (Availability == FurniAvailability.Anywhere ? 0 : 1) +
        (Line.Length == 0 ? 0 : 1) +
        (Category.Length == 0 ? 0 : 1) +
        (MinPrice.Length == 0 ? 0 : 1) +
        (MaxPrice.Length == 0 ? 0 : 1);

    public bool NeedsPrices => Availability != FurniAvailability.Anywhere || MinPrice.Length > 0 || MaxPrice.Length > 0;

    public static int? Price(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) && value >= 0 ? value : null;
}

public sealed class FurniQuery : IComparer<FurniRowViewModel>
{
    const int KindRank = 7;
    const int NoRank = 8;

    readonly Dictionary<FurniRowViewModel, int> _ranks = [];
    readonly FurniFilter _filter;
    readonly string _term;
    readonly string _line;
    readonly string _category;
    readonly int? _kind;
    readonly int? _minimum;
    readonly int? _maximum;

    public FurniQuery(FurniFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _term = filter.Term.Trim();
        _line = filter.Line.Trim();
        _category = filter.Category.Trim();
        _kind = int.TryParse(_term, NumberStyles.Integer, CultureInfo.CurrentCulture, out int kind) ? kind : null;
        _minimum = FurniFilter.Price(filter.MinPrice);
        _maximum = FurniFilter.Price(filter.MaxPrice);
    }

    public bool Keep(FurniRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (Rank(row) == NoRank)
            return false;
        FurniSearchKey key = row.Key;
        if (_filter.Placement != FurniPlacementFilter.All && key.Type != Wanted(_filter.Placement))
            return false;
        if (!Available(key))
            return false;
        if (_line.Length > 0 && !key.Line.Contains(_line, StringComparison.CurrentCultureIgnoreCase))
            return false;
        if (_category.Length > 0 && !key.Category.Contains(_category, StringComparison.CurrentCultureIgnoreCase))
            return false;
        if (_minimum is { } minimum && (key.CheapestCredits is not { } low || low < minimum))
            return false;
        if (_maximum is { } maximum && (key.CheapestCredits is not { } high || high > maximum))
            return false;
        return true;
    }

    public int Compare(FurniRowViewModel? left, FurniRowViewModel? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        int ranks = Rank(left).CompareTo(Rank(right));
        return ranks != 0 ? ranks : string.Compare(left.Key.Name, right.Key.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    static ItemType Wanted(FurniPlacementFilter placement) =>
        placement == FurniPlacementFilter.Wall ? ItemType.Wall : ItemType.Floor;

    bool Available(FurniSearchKey key) => _filter.Availability switch
    {
        FurniAvailability.Marketplace => key.HasMarketplace,
        FurniAvailability.Shop => key.HasShop,
        FurniAvailability.Both => key.HasMarketplace && key.HasShop,
        _ => true
    };

    int Rank(FurniRowViewModel row)
    {
        if (_ranks.TryGetValue(row, out int cached))
            return cached;
        FurniSearchKey key = row.Key;
        int rank = FurniSearch.Rank(key.Name, key.Identifier, key.Extra, _term) is { } text
            ? text
            : _kind is { } kind && key.Kind == kind
                ? KindRank
                : NoRank;
        _ranks[row] = rank;
        return rank;
    }
}
