using Qx.Model;

namespace Qx.Scripting;

public sealed class AvatarQuery : QueryCollection<Avatar>
{
    public AvatarQuery(IEnumerable<Avatar> avatars)
        : base(avatars, RoomObjectSnapshot.Copy)
    {
    }

    public AvatarQuery Where(Func<Avatar, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new AvatarQuery(Items.Where(predicate));
    }

    public AvatarQuery ById(params Id[] ids) =>
        ById((IEnumerable<Id>)ids);

    public AvatarQuery ById(IEnumerable<Id> ids)
    {
        HashSet<Id> values = QueryValues.Set(ids);
        return Where(avatar => values.Contains(avatar.Id));
    }

    public AvatarQuery ByIndex(params int[] indices) =>
        ByIndex((IEnumerable<int>)indices);

    public AvatarQuery ByIndex(IEnumerable<int> indices)
    {
        HashSet<int> values = QueryValues.Set(indices);
        return Where(avatar => values.Contains(avatar.Index));
    }

    public AvatarQuery Named(params string[] names) =>
        Named((IEnumerable<string>)names);

    public AvatarQuery Named(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(avatar => values.Contains(avatar.Name));
    }

    public AvatarQuery NotNamed(params string[] names) =>
        NotNamed((IEnumerable<string>)names);

    public AvatarQuery NotNamed(IEnumerable<string> names)
    {
        HashSet<string> values = QueryValues.Strings(names);
        return Where(avatar => !values.Contains(avatar.Name));
    }

    public AvatarQuery NameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(avatar => avatar.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public AvatarQuery NotNameContains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Where(avatar => !avatar.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    public AvatarQuery OfType(params AvatarType[] types) =>
        OfType((IEnumerable<AvatarType>)types);

    public AvatarQuery OfType(IEnumerable<AvatarType> types)
    {
        HashSet<AvatarType> values = QueryValues.Set(types);
        return Where(avatar => values.Contains(avatar.Type));
    }

    public AvatarQuery NotOfType(params AvatarType[] types) =>
        NotOfType((IEnumerable<AvatarType>)types);

    public AvatarQuery NotOfType(IEnumerable<AvatarType> types)
    {
        HashSet<AvatarType> values = QueryValues.Set(types);
        return Where(avatar => !values.Contains(avatar.Type));
    }

    public AvatarQuery OwnedBy(Id ownerId) =>
        Where(avatar => avatar switch
        {
            Pet pet => pet.OwnerId == ownerId,
            Bot bot => bot.OwnerId == ownerId,
            _ => false
        });

    public AvatarQuery OwnedBy(string ownerName)
    {
        ArgumentNullException.ThrowIfNull(ownerName);
        return Where(avatar => avatar switch
        {
            Pet pet => string.Equals(pet.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase),
            Bot bot => string.Equals(bot.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase),
            _ => false
        });
    }

    public AvatarQuery At(
        int? x = null,
        int? y = null,
        float? z = null,
        int? direction = null,
        float epsilon = 0.001f)
    {
        ValidatePosition(z, epsilon);
        return Where(avatar => QueryValues.Position(avatar.Location, x, y, z, direction, avatar.Direction, epsilon));
    }

    public AvatarQuery NotAt(
        int? x = null,
        int? y = null,
        float? z = null,
        int? direction = null,
        float epsilon = 0.001f)
    {
        ValidatePosition(z, epsilon);
        if (!x.HasValue && !y.HasValue && !z.HasValue && !direction.HasValue)
            return new AvatarQuery(Items);
        return Where(avatar => !QueryValues.Position(avatar.Location, x, y, z, direction, avatar.Direction, epsilon));
    }

    public AvatarQuery At(Point point) =>
        At(point.X, point.Y);

    public AvatarQuery At(Tile tile, float epsilon = 0.001f) =>
        At(tile.X, tile.Y, tile.Z, epsilon: epsilon);

    public AvatarQuery At(params Point[] points) =>
        At((IEnumerable<Point>)points);

    public AvatarQuery At(IEnumerable<Point> points)
    {
        HashSet<Point> values = QueryValues.Set(points);
        return Where(avatar => values.Contains(avatar.XY));
    }

    public AvatarQuery NotAt(Point point) =>
        NotAt(point.X, point.Y);

    public AvatarQuery NotAt(Tile tile, float epsilon = 0.001f) =>
        NotAt(tile.X, tile.Y, tile.Z, epsilon: epsilon);

    public AvatarQuery NotAt(params Point[] points) =>
        NotAt((IEnumerable<Point>)points);

    public AvatarQuery NotAt(IEnumerable<Point> points)
    {
        HashSet<Point> values = QueryValues.Set(points);
        return Where(avatar => !values.Contains(avatar.XY));
    }

    public AvatarQuery Inside(Area area) =>
        Where(avatar => area.Contains(avatar.Location));

    public AvatarQuery Inside(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Where(avatar => areas.Contains(avatar.Location));
    }

    public AvatarQuery Outside(Area area) =>
        Where(avatar => !area.Contains(avatar.Location));

    public AvatarQuery Outside(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Where(avatar => !areas.Contains(avatar.Location));
    }

    public AvatarQuery Intersecting(Area area) =>
        Inside(area);

    public AvatarQuery AdjacentTo(Point point, bool diagonals = true) =>
        Where(avatar => QueryValues.Adjacent(new Area(avatar.Location), point, diagonals));

    public AvatarQuery AdjacentTo(Area area, bool diagonals = true) =>
        Where(avatar => QueryValues.Adjacent(new Area(avatar.Location), area, diagonals));

    public AvatarQuery WithinDistance(Point point, double distance)
    {
        if (!double.IsFinite(distance) || distance < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "Distance must be finite and non-negative.");
        double squared = distance * distance;
        return Where(avatar => QueryValues.DistanceSquared(avatar.XY, point) <= squared);
    }

    public AvatarQuery OrderByDistanceTo(Point point) =>
        new(Items.OrderBy(avatar => QueryValues.DistanceSquared(avatar.XY, point)));

    public Avatar? NearestTo(Point point) =>
        Items.MinBy(avatar => QueryValues.DistanceSquared(avatar.XY, point));

    public AvatarQuery Removed(bool value = true) =>
        Where(avatar => avatar.IsRemoved == value);

    public AvatarQuery Present() =>
        Removed(false);

    private static void ValidatePosition(float? z, float epsilon)
    {
        QueryValues.Epsilon(epsilon);
        if (z.HasValue && !float.IsFinite(z.Value))
            throw new ArgumentOutOfRangeException(nameof(z), z, "Z must be finite.");
    }
}
