using Qx;
using Qx.Game;
using Qx.Model;

namespace Qx.Scripting;

public sealed class FloorItemQuery : QueryCollection<FloorItem>
{
    private readonly FurniData? _furniData;
    private readonly FurniMetadataResolver _metadata;

    public FloorItemQuery(IEnumerable<FloorItem> items, FurniData? furniData)
        : base(items, RoomObjectSnapshot.Copy)
    {
        _furniData = furniData;
        _metadata = new FurniMetadataResolver(furniData);
    }

    public bool HasMetadata => _furniData is not null;

    public FloorItemQuery Where(Func<FloorItem, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Next(Items.Where(predicate));
    }

    public FloorItemQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public FloorItemQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(item => values.Contains(item.Id));
    }

    public FloorItemQuery OfKind(params int[] kinds) =>
        OfKind((IEnumerable<int>)kinds);

    public FloorItemQuery OfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => values.Contains(item.Kind));
    }

    public FloorItemQuery NotOfKind(params int[] kinds) =>
        NotOfKind((IEnumerable<int>)kinds);

    public FloorItemQuery NotOfKind(IEnumerable<int> kinds)
    {
        HashSet<int> values = QueryValues.Set(kinds);
        return Where(item => !values.Contains(item.Kind));
    }

    public FloorItemQuery OfIdentifier(params string[] identifiers) =>
        OfIdentifier((IEnumerable<string>)identifiers);

    public FloorItemQuery OfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && values.Contains(value));
    }

    public FloorItemQuery NotOfIdentifier(params string[] identifiers) =>
        NotOfIdentifier((IEnumerable<string>)identifiers);

    public FloorItemQuery NotOfIdentifier(IEnumerable<string> identifiers)
    {
        HashSet<string> values = QueryValues.Strings(identifiers);
        return Where(item => _metadata.Identifier(item, out string value) && !values.Contains(value));
    }

    public FloorItemQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public FloorItemQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && values.Contains(value));
    }

    public FloorItemQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    public FloorItemQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(item => _metadata.Name(item, out string value) && !values.Contains(value));
    }

    public FloorItemQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public FloorItemQuery NotNameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(item =>
            _metadata.Name(item, out string name) &&
            !name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public FloorItemQuery OfCategory(params string[] categories) =>
        OfCategory((IEnumerable<string>)categories);

    public FloorItemQuery OfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && values.Contains(value));
    }

    public FloorItemQuery NotOfCategory(params string[] categories) =>
        NotOfCategory((IEnumerable<string>)categories);

    public FloorItemQuery NotOfCategory(IEnumerable<string> categories)
    {
        HashSet<string> values = QueryValues.Strings(categories);
        return Where(item => _metadata.Category(item, out string value) && !values.Contains(value));
    }

    public FloorItemQuery OfLine(params string[] lines) =>
        OfLine((IEnumerable<string>)lines);

    public FloorItemQuery OfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && values.Contains(value));
    }

    public FloorItemQuery NotOfLine(params string[] lines) =>
        NotOfLine((IEnumerable<string>)lines);

    public FloorItemQuery NotOfLine(IEnumerable<string> lines)
    {
        HashSet<string> values = QueryValues.Strings(lines);
        return Where(item => _metadata.Line(item, out string value) && !values.Contains(value));
    }

    public FloorItemQuery WithKnownMetadata() =>
        Where(item => _metadata.Info(item) is not null);

    public FloorItemQuery WithoutKnownMetadata() =>
        Where(item => _metadata.Info(item) is null);

    public FloorItemQuery OwnedBy(Id ownerId) =>
        Where(item => item.OwnerId == ownerId);

    public FloorItemQuery OwnedBy(string ownerName)
    {
        ArgumentNullException.ThrowIfNull(ownerName);
        return Where(item => string.Equals(item.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase));
    }

    public FloorItemQuery OfState(params int[] states) =>
        OfState((IEnumerable<int>)states);

    public FloorItemQuery OfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => values.Contains(item.State));
    }

    public FloorItemQuery NotOfState(params int[] states) =>
        NotOfState((IEnumerable<int>)states);

    public FloorItemQuery NotOfState(IEnumerable<int> states)
    {
        HashSet<int> values = QueryValues.Set(states);
        return Where(item => !values.Contains(item.State));
    }

    public FloorItemQuery At(
        int? x = null,
        int? y = null,
        float? z = null,
        int? direction = null,
        float epsilon = 0.001f)
    {
        ValidatePosition(z, epsilon);
        return Where(item => QueryValues.Position(item.Location, x, y, z, direction, item.Direction, epsilon));
    }

    public FloorItemQuery NotAt(
        int? x = null,
        int? y = null,
        float? z = null,
        int? direction = null,
        float epsilon = 0.001f)
    {
        ValidatePosition(z, epsilon);
        if (!x.HasValue && !y.HasValue && !z.HasValue && !direction.HasValue)
            return Next(Items);
        return Where(item => !QueryValues.Position(item.Location, x, y, z, direction, item.Direction, epsilon));
    }

    public FloorItemQuery At(Point point) =>
        At(point.X, point.Y);

    public FloorItemQuery At(Tile tile, float epsilon = 0.001f) =>
        At(tile.X, tile.Y, tile.Z, epsilon: epsilon);

    public FloorItemQuery At(params Point[] points) =>
        At((IEnumerable<Point>)points);

    public FloorItemQuery At(IEnumerable<Point> points)
    {
        HashSet<Point> values = QueryValues.Set(points);
        return Where(item => values.Contains(item.Location.XY));
    }

    public FloorItemQuery NotAt(Point point) =>
        NotAt(point.X, point.Y);

    public FloorItemQuery NotAt(Tile tile, float epsilon = 0.001f) =>
        NotAt(tile.X, tile.Y, tile.Z, epsilon: epsilon);

    public FloorItemQuery NotAt(params Point[] points) =>
        NotAt((IEnumerable<Point>)points);

    public FloorItemQuery NotAt(IEnumerable<Point> points)
    {
        HashSet<Point> values = QueryValues.Set(points);
        return Where(item => !values.Contains(item.Location.XY));
    }

    public FloorItemQuery Inside(Area area) =>
        Where(item => area.Contains(AreaOf(item)));

    public FloorItemQuery Inside(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Where(item => areas.Contains(AreaOf(item)));
    }

    public FloorItemQuery NotInside(Area area) =>
        Where(item => !area.Contains(AreaOf(item)));

    public FloorItemQuery Outside(Area area) =>
        Where(item => !area.Intersects(AreaOf(item)));

    public FloorItemQuery Outside(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Where(item => !areas.Intersects(AreaOf(item)));
    }

    public FloorItemQuery Intersecting(Area area) =>
        Where(item => area.Intersects(AreaOf(item)));

    public FloorItemQuery Intersecting(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Where(item => areas.Intersects(AreaOf(item)));
    }

    public FloorItemQuery AdjacentTo(Point point, bool diagonals = true) =>
        Where(item => QueryValues.Adjacent(AreaOf(item), point, diagonals));

    public FloorItemQuery AdjacentTo(Area area, bool diagonals = true) =>
        Where(item => QueryValues.Adjacent(AreaOf(item), area, diagonals));

    public FloorItemQuery WithinDistance(Point point, double distance)
    {
        if (!double.IsFinite(distance) || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "Distance must be finite and non-negative.");
        double squared = distance * distance;
        return Where(item => QueryValues.DistanceSquared(AreaOf(item), point) <= squared);
    }

    public FloorItemQuery OrderByDistanceTo(Point point) =>
        Next(Items.OrderBy(item => QueryValues.DistanceSquared(AreaOf(item), point)));

    public FloorItem? NearestTo(Point point) =>
        Items.MinBy(item => QueryValues.DistanceSquared(AreaOf(item), point));

    public FloorItemQuery Hidden(bool value = true) =>
        Where(item => item.IsHidden == value);

    public FloorItemQuery Removed(bool value = true) =>
        Where(item => item.IsRemoved == value);

    public FloorItemQuery Present() =>
        Removed(false);

    public Area AreaOf(FloorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _metadata.Area(item);
    }

    private FloorItemQuery Next(IEnumerable<FloorItem> items) =>
        new(items, _furniData);

    private static void ValidatePosition(float? z, float epsilon)
    {
        QueryValues.Epsilon(epsilon);
        if (z.HasValue && !float.IsFinite(z.Value))
            throw new ArgumentOutOfRangeException(nameof(z), z, "Z must be finite.");
    }
}
