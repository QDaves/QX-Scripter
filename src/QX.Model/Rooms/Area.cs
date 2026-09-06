using System.Collections;

namespace Qx.Model;

public readonly record struct Area : IEnumerable<Tile>
{
    private readonly Tile _origin;
    private readonly int _width;
    private readonly int _length;

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

    public int Width
    {
        get => _width;
        init
        {
            ValidateWidth(_origin, value);
            _width = value;
        }
    }

    public int Length
    {
        get => _length;
        init
        {
            ValidateLength(_origin, value);
            _length = value;
        }
    }

    public int X1 => Origin.X;
    public int Y1 => Origin.Y;
    public int X2 => checked(Origin.X + (Width - 1));
    public int Y2 => checked(Origin.Y + (Length - 1));
    public Point Size => new(Width, Length);
    public Tile Opposite => new(X2, Y2, Origin.Z);
    public long TileCount => (long)Width * Length;
    public bool IsEmpty => Width <= 0 || Length <= 0;
    public IEnumerable<Point> Points => EnumeratePoints();
    public IEnumerable<Tile> Tiles => this;

    public Area(Tile origin) : this(origin, 1, 1)
    {
    }

    public Area(Point origin) : this(new Tile(origin.X, origin.Y), 1, 1)
    {
    }

    public Area(Point origin, int width, int length)
        : this(new Tile(origin.X, origin.Y), width, length)
    {
    }

    public Area(Tile Origin, int Width, int Length)
    {
        ValidateWidth(Origin, Width);
        ValidateLength(Origin, Length);
        _origin = Origin;
        _width = Width;
        _length = Length;
    }

    public Area(Point first, Point second)
        : this(first.X, first.Y, second.X, second.Y)
    {
    }

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

    public void Deconstruct(out Tile Origin, out int Width, out int Length)
    {
        Origin = this.Origin;
        Width = this.Width;
        Length = this.Length;
    }

    public bool Contains(int x, int y) =>
        !IsEmpty && x >= X1 && x <= X2 && y >= Y1 && y <= Y2;

    public bool Contains(Point point) => Contains(point.X, point.Y);

    public bool Contains(Tile tile) => Contains(tile.X, tile.Y);

    public bool Contains(Area area) =>
        !IsEmpty &&
        !area.IsEmpty &&
        area.X1 >= X1 &&
        area.X2 <= X2 &&
        area.Y1 >= Y1 &&
        area.Y2 <= Y2;

    public bool Intersects(Area area) =>
        !IsEmpty &&
        !area.IsEmpty &&
        X1 <= area.X2 &&
        X2 >= area.X1 &&
        Y1 <= area.Y2 &&
        Y2 >= area.Y1;

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

    public Area? Intersection(Area area) =>
        TryIntersect(area, out Area intersection) ? intersection : null;

    public Area BoundingUnion(Area area) =>
        FromBounds(
            Math.Min(X1, area.X1),
            Math.Min(Y1, area.Y1),
            Math.Max(X2, area.X2),
            Math.Max(Y2, area.Y2),
            Origin.Z);

    public Area Translate(Point offset) =>
        new(
            new Tile(
                checked(Origin.X + offset.X),
                checked(Origin.Y + offset.Y),
                Origin.Z),
            Width,
            Length);

    public Area Translate(int x, int y) => Translate(new Point(x, y));

    public Area Expand(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Expansion must not be negative.");
        return Expand(amount, amount, amount, amount);
    }

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

    public Area Flip() => new(Origin, Length, Width);

    public IEnumerator<Tile> GetEnumerator() => EnumerateTiles().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{Origin.XY} {Width}x{Length}";

    public static implicit operator Area((Point Origin, int Width, int Length) area) =>
        new(area.Origin, area.Width, area.Length);

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
