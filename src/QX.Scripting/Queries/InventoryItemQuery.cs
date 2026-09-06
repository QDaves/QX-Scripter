using Qx;
using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

public sealed class InventoryItemQuery : QueryCollection<InventoryItem>
{
    private readonly FurniData? _furniData;
    private readonly FurniMetadataResolver _metadata;

    public InventoryItemQuery(IEnumerable<InventoryItem> items, FurniData? furniData)
        : base(items)
    {
        _furniData = furniData;
        _metadata = new FurniMetadataResolver(furniData);
    }

    public bool HasMetadata => _furniData is not null;

    public InventoryItemQuery Where(Func<InventoryItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public InventoryItemQuery ByItemId(params Id[] ids) =>
        ByItemId((IEnumerable<Id>)ids);

    public InventoryItemQuery ByItemId(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(item => values.Contains(item.ItemId));
    }

    public InventoryItemQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public InventoryItemQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(item => values.Contains(item.Id));
    }

    public InventoryItemQuery OfType(params ItemType[] types) =>
        OfType((IEnumerable<ItemType>)types);

    public InventoryItemQuery OfType(IEnumerable<ItemType> types)
    {
        HashSet<ItemType> values = QueryValues.Set(types);
        return Where(item => values.Contains(item.Type));
    }

    public InventoryItemQuery NotOfType(params ItemType[] types) =>
        NotOfType((IEnumerable<ItemType>)types);

    public InventoryItemQuery NotOfType(IEnumerable<ItemType> types)
    {
        HashSet<ItemType> values = QueryValues.Set(types);
        return Where(item => !values.Contains(item.Type));
    }

    public InventoryItemQuery OfKind(params int[] kinds) =>
        OfKind((IEnumerable<int>)kinds);

    public InventoryItemQuery OfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => values.Contains(item.Kind));
    }

    public InventoryItemQuery NotOfKind(params int[] kinds) =>
        NotOfKind((IEnumerable<int>)kinds);

    public InventoryItemQuery NotOfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => !values.Contains(item.Kind));
    }

    public InventoryItemQuery OfIdentifier(params string[] identifiers) =>
        OfIdentifier((IEnumerable<string>)identifiers);

    public InventoryItemQuery OfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && values.Contains(value));
    }

    public InventoryItemQuery NotOfIdentifier(params string[] identifiers) =>
        NotOfIdentifier((IEnumerable<string>)identifiers);

    public InventoryItemQuery NotOfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && !values.Contains(value));
    }

    public InventoryItemQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public InventoryItemQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && values.Contains(value));
    }

    public InventoryItemQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    public InventoryItemQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && !values.Contains(value));
    }

    public InventoryItemQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public InventoryItemQuery NotNameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            !name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public InventoryItemQuery OfCategory(params string[] categories) =>
        OfCategory((IEnumerable<string>)categories);

    public InventoryItemQuery OfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && values.Contains(value));
    }

    public InventoryItemQuery NotOfCategory(params string[] categories) =>
        NotOfCategory((IEnumerable<string>)categories);

    public InventoryItemQuery NotOfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && !values.Contains(value));
    }

    public InventoryItemQuery OfInventoryCategory(params int[] categories) =>
        OfInventoryCategory((IEnumerable<int>)categories);

    public InventoryItemQuery OfInventoryCategory(IEnumerable<int> categories)
    {
        HashSet<int> values = QueryValues.Set(categories);
        return Where(item => values.Contains(item.Category));
    }

    public InventoryItemQuery OfLine(params string[] lines) =>
        OfLine((IEnumerable<string>)lines);

    public InventoryItemQuery OfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && values.Contains(value));
    }

    public InventoryItemQuery NotOfLine(params string[] lines) =>
        NotOfLine((IEnumerable<string>)lines);

    public InventoryItemQuery NotOfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && !values.Contains(value));
    }

    public InventoryItemQuery WithKnownMetadata() =>
        Where(item => _metadata.Info(item) is not null);

    public InventoryItemQuery WithoutKnownMetadata() =>
        Where(item => _metadata.Info(item) is null);

    public InventoryItemQuery OfState(params int[] states) =>
        OfState((IEnumerable<int>)states);

    public InventoryItemQuery OfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => values.Contains(item.Data.State));
    }

    public InventoryItemQuery NotOfState(params int[] states) =>
        NotOfState((IEnumerable<int>)states);

    public InventoryItemQuery NotOfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => !values.Contains(item.Data.State));
    }

    public InventoryItemQuery Nft(bool value = true) =>
        Where(item => item.IsNft == value);

    public InventoryItemQuery Rental(bool value = true) =>
        Where(item => IsRental(item) == value);

    public InventoryItemQuery Unseen(bool value = true) =>
        Where(item => item.IsUnseen == value);

    public InventoryItemQuery Tradeable(bool value = true) =>
        Where(item => item.IsTradeable == value);

    public InventoryItemQuery Sellable(bool value = true) =>
        Where(item => item.IsSellable == value);

    public InventoryItemQuery Groupable(bool value = true) =>
        Where(item => item.IsGroupable == value);

    public InventoryItemQuery Recyclable(bool value = true) =>
        Where(item => item.IsRecyclable == value);

    public InventoryItemQuery ExternalImage(bool value = true) =>
        Where(item => item.IsExternalImage == value);

    public InventoryItemQuery InRoom(Id roomId) =>
        Where(item => item.RoomId == roomId);

    public InventoryItemQuery NftNamed(params string[] names) =>
        NftNamed((IEnumerable<string>)names);

    public InventoryItemQuery NftNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => item.IsNft && values.Contains(item.NftName));
    }

    private InventoryItemQuery Next(IEnumerable<InventoryItem> items) =>
        new(items, _furniData);

    private static bool IsRental(InventoryItem item) =>
        item.HasRentPeriodStarted || item.SecondsToExpiration >= 0;
}
