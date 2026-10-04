using System.Collections;

namespace Qx.Model;

/// <summary>Represents a rectangle of room tiles.</summary>
/// <remarks>
/// The rectangle starts at <see cref="Origin"/> and spans <see cref="Width"/> tiles along x and
/// <see cref="Length"/> tiles along y. Enumerating it yields its tiles row by row, each with the
/// height of <see cref="Origin"/>.
/// </remarks>
public readonly record struct Area : IEnumerable<Tile>
{
    private readonly Tile _origin;
    private readonly int _width;
    private readonly int _length;

    /// <summary>Gets the corner tile with the lowest x and y coordinates.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the area would extend past the coordinate range.</exception>
    public Tile Origin
    {
        get => _origin;
        init
        {
            if (_width > 0)
                ValidateWidth(value, _width);
            if (_length > 0)
                ValidateLength(value, _length);
            _origin = value;
        }
    }

    /// <summary>Gets the number of tiles the area spans along x.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when set to 0 or less, or when the area would extend past the coordinate range.
    /// </exception>
    public int Width
    {
        get => _width;
        init
        {
            ValidateWidth(_origin, value);
            _width = value;
        }
    }

    /// <summary>Gets the number of tiles the area spans along y.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when set to 0 or less, or when the area would extend past the coordinate range.
    /// </exception>
    public int Length
    {
        get => _length;
        init
        {
            ValidateLength(_origin, value);
            _length = value;
        }
    }

    /// <summary>Gets the lowest x coordinate inside the area.</summary>
    public int X1 => Origin.X;
    /// <summary>Gets the lowest y coordinate inside the area.</summary>
    public int Y1 => Origin.Y;
    /// <summary>Gets the highest x coordinate inside the area.</summary>
    public int X2 => checked(Origin.X + (Width - 1));
    /// <summary>Gets the highest y coordinate inside the area.</summary>
    public int Y2 => checked(Origin.Y + (Length - 1));
    /// <summary>Gets the width and length of the area as a <see cref="Point"/>.</summary>
    public Point Size => new(Width, Length);
    /// <summary>Gets the corner tile opposite <see cref="Origin"/>, at (<see cref="X2"/>, <see cref="Y2"/>) with the origin's height.</summary>
    public Tile Opposite => new(X2, Y2, Origin.Z);
    /// <summary>Gets the number of tiles in the area.</summary>
    public long TileCount => (long)Width * Length;
    /// <summary>Gets whether the area has no tiles, which is only the case for the default value.</summary>
    public bool IsEmpty => Width <= 0 || Length <= 0;
    /// <summary>Gets the coordinates of every tile in the area, row by row.</summary>
    public IEnumerable<Point> Points => EnumeratePoints();
    /// <summary>Gets every tile in the area, row by row, each with the height of <see cref="Origin"/>.</summary>
    public IEnumerable<Tile> Tiles => this;

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct that covers one tile.</summary>
    /// <param name="origin">The tile.</param>
    public Area(Tile origin) : this(origin, 1, 1)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct that covers one tile at height 0.</summary>
    /// <param name="origin">The tile coordinates.</param>
    public Area(Point origin) : this(new Tile(origin.X, origin.Y), 1, 1)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct at height 0.</summary>
    /// <param name="origin">The corner with the lowest x and y coordinates.</param>
    /// <param name="width">The number of tiles along x.</param>
    /// <param name="length">The number of tiles along y.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="length"/> is 0 or less, or the area
    /// would extend past the coordinate range.
    /// </exception>
    public Area(Point origin, int width, int length)
        : this(new Tile(origin.X, origin.Y), width, length)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct.</summary>
    /// <param name="origin">The corner tile with the lowest x and y coordinates.</param>
    /// <param name="width">The number of tiles along x.</param>
    /// <param name="length">The number of tiles along y.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="width"/> or <paramref name="length"/> is 0 or less, or the area
    /// would extend past the coordinate range.
    /// </exception>
    public Area(Tile origin, int width, int length)
    {
        ValidateWidth(origin, width);
        ValidateLength(origin, length);
        _origin = origin;
        _width = width;
        _length = length;
    }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct between two corners at height 0.</summary>
    /// <remarks>The corners may be given in any order; both are inside the area.</remarks>
    /// <param name="first">One corner of the area.</param>
    /// <param name="second">The opposite corner of the area.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the area is wider or longer than <see cref="int.MaxValue"/> tiles.</exception>
    public Area(Point first, Point second)
        : this(first.X, first.Y, second.X, second.Y)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Area"/> struct between two corners at height 0.</summary>
    /// <remarks>The corners may be given in any order; both are inside the area.</remarks>
    /// <param name="x1">The x coordinate of one corner.</param>
    /// <param name="y1">The y coordinate of one corner.</param>
    /// <param name="x2">The x coordinate of the opposite corner.</param>
    /// <param name="y2">The y coordinate of the opposite corner.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the area is wider or longer than <see cref="int.MaxValue"/> tiles.</exception>
    public Area(int x1, int y1, int x2, int y2)
    {
        int left = Math.Min(x1, x2);
        int top = Math.Min(y1, y2);
        int right = Math.Max(x1, x2);
        int bottom = Math.Max(y1, y2);
        long width = (long)right - left + 1;
        long length = (long)bottom - top + 1;
        if (width > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(x2), x2, "The area width exceeds the supported range.");
        if (length > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(y2), y2, "The area length exceeds the supported range.");

        _origin = new Tile(left, top);
        _width = (int)width;
        _length = (int)length;
    }

    /// <summary>Deconstructs the area into its origin and size.</summary>
    /// <param name="origin">The corner tile with the lowest x and y coordinates.</param>
    /// <param name="width">The number of tiles along x.</param>
    /// <param name="length">The number of tiles along y.</param>
    public void Deconstruct(out Tile origin, out int width, out int length)
    {
        origin = Origin;
        width = Width;
        length = Length;
    }

    /// <summary>Gets whether a tile lies inside the area.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public bool Contains(int x, int y) =>
        !IsEmpty && x >= X1 && x <= X2 && y >= Y1 && y <= Y2;

    /// <summary>Gets whether a tile lies inside the area.</summary>
    /// <param name="point">The tile coordinates.</param>
    public bool Contains(Point point) => Contains(point.X, point.Y);

    /// <summary>Gets whether a tile lies inside the area, ignoring its height.</summary>
    /// <param name="tile">The tile.</param>
    public bool Contains(Tile tile) => Contains(tile.X, tile.Y);

    /// <summary>Gets whether another area lies completely inside this one.</summary>
    /// <param name="area">The area to check.</param>
    /// <returns><see langword="false"/> when either area is empty.</returns>
    public bool Contains(Area area) =>
        !IsEmpty &&
        !area.IsEmpty &&
        area.X1 >= X1 &&
        area.X2 <= X2 &&
        area.Y1 >= Y1 &&
        area.Y2 <= Y2;

    /// <summary>Gets whether the area shares at least one tile with another area.</summary>
    /// <param name="area">The area to check.</param>
    /// <returns><see langword="false"/> when either area is empty.</returns>
    public bool Intersects(Area area) =>
        !IsEmpty &&
        !area.IsEmpty &&
        X1 <= area.X2 &&
        X2 >= area.X1 &&
        Y1 <= area.Y2 &&
        Y2 >= area.Y1;

    /// <summary>Gets the overlap of the area with another area.</summary>
    /// <param name="area">The other area.</param>
    /// <param name="intersection">
    /// The tiles both areas share, at this area's origin height, or the default value when they do
    /// not overlap.
    /// </param>
    /// <returns><see langword="true"/> when the areas overlap; otherwise, <see langword="false"/>.</returns>
    public bool TryIntersect(Area area, out Area intersection)
    {
        int left = Math.Max(X1, area.X1);
        int top = Math.Max(Y1, area.Y1);
        int right = Math.Min(X2, area.X2);
        int bottom = Math.Min(Y2, area.Y2);
        if (left > right || top > bottom)
        {
            intersection = default;
            return false;
        }

        intersection = new Area(new Tile(left, top, Origin.Z), right - left + 1, bottom - top + 1);
        return true;
    }

    /// <summary>Gets the overlap of the area with another area.</summary>
    /// <param name="area">The other area.</param>
    /// <returns>
    /// The tiles both areas share, at this area's origin height, or <see langword="null"/> when they
    /// do not overlap.
    /// </returns>
    public Area? Intersection(Area area) =>
        TryIntersect(area, out Area intersection) ? intersection : null;

    /// <summary>Gets the smallest area that contains both this area and another.</summary>
    /// <param name="area">The other area.</param>
    /// <returns>The bounding rectangle of both areas, at this area's origin height.</returns>
    public Area BoundingUnion(Area area) =>
        FromBounds(
            Math.Min(X1, area.X1),
            Math.Min(Y1, area.Y1),
            Math.Max(X2, area.X2),
            Math.Max(Y2, area.Y2),
            Origin.Z);

    /// <summary>Moves the area by an offset.</summary>
    /// <param name="offset">The number of tiles to move along x and y.</param>
    /// <returns>A new area of the same size at the moved origin.</returns>
    /// <exception cref="OverflowException">Thrown when the moved origin is outside the coordinate range.</exception>
    public Area Translate(Point offset) =>
        new(
            new Tile(
                checked(Origin.X + offset.X),
                checked(Origin.Y + offset.Y),
                Origin.Z),
            Width,
            Length);

    /// <summary>Moves the area by an offset.</summary>
    /// <param name="x">The number of tiles to move along x.</param>
    /// <param name="y">The number of tiles to move along y.</param>
    /// <returns>A new area of the same size at the moved origin.</returns>
    /// <exception cref="OverflowException">Thrown when the moved origin is outside the coordinate range.</exception>
    public Area Translate(int x, int y) => Translate(new Point(x, y));

    /// <summary>Grows the area by the same number of tiles on every side.</summary>
    /// <param name="amount">The number of tiles to add on each side.</param>
    /// <returns>The expanded area.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="amount"/> is negative or the result exceeds the coordinate range.
    /// </exception>
    public Area Expand(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Expansion must not be negative.");
        return Expand(amount, amount, amount, amount);
    }

    /// <summary>Grows the area by a number of tiles on each side.</summary>
    /// <param name="left">The number of tiles to add towards lower x.</param>
    /// <param name="top">The number of tiles to add towards lower y.</param>
    /// <param name="right">The number of tiles to add towards higher x.</param>
    /// <param name="bottom">The number of tiles to add towards higher y.</param>
    /// <returns>The expanded area.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any amount is negative or the result exceeds the coordinate range.
    /// </exception>
    public Area Expand(int left, int top, int right, int bottom)
    {
        if (left < 0)
            throw new ArgumentOutOfRangeException(nameof(left), left, "Expansion must not be negative.");
        if (top < 0)
            throw new ArgumentOutOfRangeException(nameof(top), top, "Expansion must not be negative.");
        if (right < 0)
            throw new ArgumentOutOfRangeException(nameof(right), right, "Expansion must not be negative.");
        if (bottom < 0)
            throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "Expansion must not be negative.");

        long x1 = (long)X1 - left;
        long y1 = (long)Y1 - top;
        long x2 = (long)X2 + right;
        long y2 = (long)Y2 + bottom;
        if (x1 < int.MinValue || y1 < int.MinValue || x2 > int.MaxValue || y2 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(left), "The expanded area exceeds the coordinate range.");

        return FromBounds((int)x1, (int)y1, (int)x2, (int)y2, Origin.Z);
    }

    /// <summary>Swaps the area's width and length, keeping its origin.</summary>
    /// <returns>The rotated area.</returns>
    public Area Flip() => new(Origin, Length, Width);

    /// <summary>Returns an enumerator over the area's tiles, row by row.</summary>
    /// <returns>An enumerator that yields each tile with the height of <see cref="Origin"/>.</returns>
    public IEnumerator<Tile> GetEnumerator() => EnumerateTiles().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the area's origin and size.</summary>
    /// <returns>A string in the form <c>(X, Y) WidthxLength</c>.</returns>
    public override string ToString() => $"{Origin.XY} {Width}x{Length}";

    /// <summary>Converts an origin and size to an <see cref="Area"/> at height 0.</summary>
    /// <param name="area">The origin, width and length.</param>
    public static implicit operator Area((Point Origin, int Width, int Length) area) =>
        new(area.Origin, area.Width, area.Length);

    /// <summary>Converts two opposite corners to an <see cref="Area"/> at height 0.</summary>
    /// <param name="corners">The two corners, in any order.</param>
    public static implicit operator Area((Point First, Point Second) corners) =>
        new(corners.First, corners.Second);

    private static Area FromBounds(int x1, int y1, int x2, int y2, float z) =>
        new(new Tile(x1, y1, z), checked(x2 - x1 + 1), checked(y2 - y1 + 1));

    private static void ValidateWidth(Tile origin, int width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), width, "Width must be greater than zero.");
        if (origin.X + (long)width - 1 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Width), width, "The area exceeds the coordinate range.");
    }

    private static void ValidateLength(Tile origin, int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(Length), length, "Length must be greater than zero.");
        if (origin.Y + (long)length - 1 > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Length), length, "The area exceeds the coordinate range.");
    }

    private IEnumerable<Point> EnumeratePoints()
    {
        if (IsEmpty)
            yield break;

        int y = Y1;
        while (true)
        {
            int x = X1;
            while (true)
            {
                yield return new Point(x, y);
                if (x == X2)
                    break;
                x++;
            }

            if (y == Y2)
                break;
            y++;
        }
    }

    private IEnumerable<Tile> EnumerateTiles()
    {
        if (IsEmpty)
            yield break;

        int y = Y1;
        while (true)
        {
            int x = X1;
            while (true)
            {
                yield return new Tile(x, y, Origin.Z);
                if (x == X2)
                    break;
                x++;
            }

            if (y == Y2)
                break;
            y++;
        }
    }
}
