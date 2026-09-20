using Qx.Model;
using Qx.Presentation.Services.FurniLookup;

namespace Qx.Presentation.ViewModels.Inventory;

public enum InventoryKindFilter
{
    All,
    Floor,
    Wall,
    Pets
}

public enum InventoryTradeFilter
{
    Any,
    Tradeable,
    Locked
}

public enum InventoryViewMode
{
    List,
    Grid
}

public readonly record struct InventoryFilter(
    string Term,
    InventoryKindFilter Kind,
    InventoryTradeFilter Trade,
    int? LeastOwned,
    int? LeastPrice,
    int? MostPrice)
{
    public static InventoryFilter Everything { get; } = new("", InventoryKindFilter.All, InventoryTradeFilter.Any, null, null, null);

    public bool ByPrice => LeastPrice is not null || MostPrice is not null;

    public bool IsFiltering =>
        Term.Length > 0 ||
        Kind != InventoryKindFilter.All ||
        Trade != InventoryTradeFilter.Any ||
        LeastOwned is not null ||
        ByPrice;

    public int? Rank(InventoryRowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return FurniSearch.Rank(facts.Name, facts.Detail, facts.Group, Term);
    }

    public bool Keeps(InventoryRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        InventoryRowFacts facts = row.Facts;
        return Rank(facts) is not null &&
            MatchesKind(facts) &&
            MatchesTrade(facts) &&
            (LeastOwned is not { } fewest || facts.Count >= fewest) &&
            (!ByPrice || Priced(facts));
    }

    bool MatchesKind(InventoryRowFacts facts) => Kind switch
    {
        InventoryKindFilter.Floor => facts.Type == ItemType.Floor,
        InventoryKindFilter.Wall => facts.Type == ItemType.Wall,
        InventoryKindFilter.Pets => facts.Type is null,
        _ => true
    };

    bool MatchesTrade(InventoryRowFacts facts) => Trade switch
    {
        InventoryTradeFilter.Tradeable => facts.Tradeable,
        InventoryTradeFilter.Locked => !facts.Tradeable,
        _ => true
    };

    bool Priced(InventoryRowFacts facts) =>
        facts.Price is { } price &&
        (LeastPrice is not { } floor || price >= floor) &&
        (MostPrice is not { } ceiling || price <= ceiling);
}

public sealed class InventoryOrder(InventoryFilter filter) : IComparer<InventoryRowViewModel>
{
    readonly InventoryFilter _filter = filter;

    public int Compare(InventoryRowViewModel? left, InventoryRowViewModel? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        int ranked = Ranked(left).CompareTo(Ranked(right));
        return ranked != 0 ? ranked : string.Compare(left.Facts.Name, right.Facts.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    int Ranked(InventoryRowViewModel row) =>
        _filter.Term.Length == 0 ? 0 : _filter.Rank(row.Facts) ?? int.MaxValue;
}
