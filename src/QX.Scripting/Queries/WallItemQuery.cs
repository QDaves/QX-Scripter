using Qx;
using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

public sealed class WallItemQuery : QueryCollection<WallItem>
{
    private readonly FurniData? _furniData;
    private readonly FurniMetadataResolver _metadata;

    public WallItemQuery(IEnumerable<WallItem> items, FurniData? furniData)
        : base(items, RoomObjectSnapshot.Copy)
    {
        _furniData = furniData;
        _metadata = new FurniMetadataResolver(furniData);
    }

    public bool HasMetadata => _furniData is not null;

    public WallItemQuery Where(Func<WallItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public WallItemQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public WallItemQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(item => values.Contains(item.Id));
    }

    public WallItemQuery OfKind(params int[] kinds) =>
        OfKind((IEnumerable<int>)kinds);

    public WallItemQuery OfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => values.Contains(item.Kind));
    }

    public WallItemQuery NotOfKind(params int[] kinds) =>
        NotOfKind((IEnumerable<int>)kinds);

    public WallItemQuery NotOfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => !values.Contains(item.Kind));
    }

    public WallItemQuery OfIdentifier(params string[] identifiers) =>
        OfIdentifier((IEnumerable<string>)identifiers);

    public WallItemQuery OfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && values.Contains(value));
    }

    public WallItemQuery NotOfIdentifier(params string[] identifiers) =>
        NotOfIdentifier((IEnumerable<string>)identifiers);

    public WallItemQuery NotOfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && !values.Contains(value));
    }

    public WallItemQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public WallItemQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && values.Contains(value));
    }

    public WallItemQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    public WallItemQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && !values.Contains(value));
    }

    public WallItemQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public WallItemQuery NotNameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            !name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public WallItemQuery OfCategory(params string[] categories) =>
        OfCategory((IEnumerable<string>)categories);

    public WallItemQuery OfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && values.Contains(value));
    }

    public WallItemQuery NotOfCategory(params string[] categories) =>
        NotOfCategory((IEnumerable<string>)categories);

    public WallItemQuery NotOfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && !values.Contains(value));
    }

    public WallItemQuery OfLine(params string[] lines) =>
        OfLine((IEnumerable<string>)lines);

    public WallItemQuery OfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && values.Contains(value));
    }

    public WallItemQuery NotOfLine(params string[] lines) =>
        NotOfLine((IEnumerable<string>)lines);

    public WallItemQuery NotOfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && !values.Contains(value));
    }

    public WallItemQuery WithKnownMetadata() =>
        Where(item => _metadata.Info(item) is not null);

    public WallItemQuery WithoutKnownMetadata() =>
        Where(item => _metadata.Info(item) is null);

    public WallItemQuery OwnedBy(Id ownerId) =>
        Where(item => item.OwnerId == ownerId);

    public WallItemQuery OwnedBy(string ownerName)
    {
        ArgumentNullException.ThrowIfNull(ownerName);
        return Where(item => string.Equals(item.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase));
    }

    public WallItemQuery OfState(params int[] states) =>
        OfState((IEnumerable<int>)states);

    public WallItemQuery OfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => values.Contains(item.State));
    }

    public WallItemQuery NotOfState(params int[] states) =>
        NotOfState((IEnumerable<int>)states);

    public WallItemQuery NotOfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => !values.Contains(item.State));
    }

    public WallItemQuery At(
        int? wallX = null,
        int? wallY = null,
        int? offsetX = null,
        int? offsetY = null,
        WallOrientation? orientation = null) =>
        Where(item =>
            (!wallX.HasValue || item.WX == wallX.Value) &&
            (!wallY.HasValue || item.WY == wallY.Value) &&
            (!offsetX.HasValue || item.LX == offsetX.Value) &&
            (!offsetY.HasValue || item.LY == offsetY.Value) &&
            (!orientation.HasValue || item.Orientation == orientation.Value));

    public WallItemQuery NotAt(
        int? wallX = null,
        int? wallY = null,
        int? offsetX = null,
        int? offsetY = null,
        WallOrientation? orientation = null) =>
        !wallX.HasValue &&
        !wallY.HasValue &&
        !offsetX.HasValue &&
        !offsetY.HasValue &&
        !orientation.HasValue
            ? Next(Items)
            : Where(item =>
                !(
                (!wallX.HasValue || item.WX == wallX.Value) &&
                (!wallY.HasValue || item.WY == wallY.Value) &&
                (!offsetX.HasValue || item.LX == offsetX.Value) &&
                (!offsetY.HasValue || item.LY == offsetY.Value) &&
                (!orientation.HasValue || item.Orientation == orientation.Value)
                ));

    public WallItemQuery At(WallLocation location) =>
        At(
            location.Wall.X,
            location.Wall.Y,
            location.Offset.X,
            location.Offset.Y,
            location.Orientation);

    public WallItemQuery NotAt(WallLocation location) =>
        NotAt(
            location.Wall.X,
            location.Wall.Y,
            location.Offset.X,
            location.Offset.Y,
            location.Orientation);

    public WallItemQuery OfOrientation(WallOrientation orientation) =>
        Where(item => item.Orientation == orientation);

    public WallItemQuery OrderByDistanceToOffset(Point point) =>
        Next(Items.OrderBy(item => QueryValues.DistanceSquared(item.Location.Offset, point)));

    public WallItem? NearestToOffset(Point point) =>
        Items.MinBy(item => QueryValues.DistanceSquared(item.Location.Offset, point));

    public WallItem? NearestTo(WallLocation location) =>
        Items.MinBy(item =>
            QueryValues.DistanceSquared(item.Location.Wall, location.Wall) +
            QueryValues.DistanceSquared(item.Location.Offset, location.Offset));

    public WallItemQuery Hidden(bool value = true) =>
        Where(item => item.IsHidden == value);

    public WallItemQuery Removed(bool value = true) =>
        Where(item => item.IsRemoved == value);

    public WallItemQuery Present() =>
        Removed(false);

    private WallItemQuery Next(IEnumerable<WallItem> items) =>
        new(items, _furniData);
}
