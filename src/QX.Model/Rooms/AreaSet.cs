using System.Collections;

namespace Qx.Model;

/// <summary>Represents an immutable union of <see cref="Area"/> rectangles.</summary>
/// <remarks>
/// Tile queries treat the set as one shape, so tiles covered by several areas count once.
/// Enumerating the set yields its distinct areas.
/// </remarks>
public sealed class AreaSet : IReadOnlyCollection<Area>
{
    /// <summary>The largest number of tiles <see cref="Tiles"/> will materialize.</summary>
    public const int TileMaterializationLimit = 1_000_000;

    private readonly Area[] _areas;
    private readonly IReadOnlyList<Area> _area_view;
    private readonly VerticalBand[] _bands;
    private readonly GeometryBounds? _geometry_bounds;
    private readonly Lazy<IReadOnlyList<Point>> _tiles;
    private readonly long _tile_count;

    /// <summary>Gets the number of distinct areas in the set.</summary>
    public int Count => _areas.Length;
    /// <summary>Gets the number of distinct tiles the set covers.</summary>
    public long TileCount => _tile_count;
    /// <summary>Gets the distinct areas in the order they were first given.</summary>
    public IReadOnlyList<Area> Areas => _area_view;
    /// <summary>Gets every tile the set covers, ordered by y and then by x.</summary>
    /// <remarks>The list is built on first access and cached.</remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set covers more than <see cref="TileMaterializationLimit"/> tiles; use
    /// <see cref="EnumerateTiles"/> instead.
    /// </exception>
    public IReadOnlyList<Point> Tiles => _tiles.Value;
    /// <summary>
    /// Gets the smallest area that contains every area in the set, or <see langword="null"/> when
    /// the set is empty or the bounds exceed the coordinate range.
    /// </summary>
    /// <remarks>The bounds use the height of the first area's origin.</remarks>
    public Area? Bounds { get; }

    /// <summary>Initializes a new instance of the <see cref="AreaSet"/> class.</summary>
    /// <param name="areas">The areas to combine; duplicates are removed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="areas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any area is empty.</exception>
    public AreaSet(IEnumerable<Area> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        _areas = areas.Distinct().ToArray();
        if (_areas.Any(area => area.IsEmpty))
            throw new ArgumentException("Area sets cannot contain empty areas.", nameof(areas));

        _area_view = Array.AsReadOnly(_areas);
        if (_areas.Length > 0)
        {
            long left = _areas[0].X1;
            long top = _areas[0].Y1;
            long right = (long)_areas[0].X2 + 1;
            long bottom = (long)_areas[0].Y2 + 1;
            for (int i = 1; i < _areas.Length; i++)
            {
                left = Math.Min(left, _areas[i].X1);
                top = Math.Min(top, _areas[i].Y1);
                right = Math.Max(right, (long)_areas[i].X2 + 1);
                bottom = Math.Max(bottom, (long)_areas[i].Y2 + 1);
            }

            _geometry_bounds = new GeometryBounds(left, top, right, bottom);
            long width = right - left;
            long length = bottom - top;
            if (width <= int.MaxValue && length <= int.MaxValue)
            {
                Bounds = new Area(
                    new Tile((int)left, (int)top, _areas[0].Origin.Z),
                    (int)width,
                    (int)length);
            }
        }

        SweepGeometry geometry = BuildGeometry(_areas);
        _bands = geometry.Bands;
        _tile_count = geometry.TileCount;
        _tiles = new Lazy<IReadOnlyList<Point>>(
            () => Array.AsReadOnly(MaterializeTiles()),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Initializes a new instance of the <see cref="AreaSet"/> class.</summary>
    /// <param name="areas">The areas to combine; duplicates are removed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="areas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any area is empty.</exception>
    public AreaSet(params Area[] areas)
        : this((IEnumerable<Area>)areas)
    {
    }

    /// <summary>Gets whether any area in the set covers a tile.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public bool Contains(int x, int y)
    {
        if (_geometry_bounds is not { } bounds || !bounds.Contains(x, y))
            return false;

        int band_index = FindFirstBandEndingAfter(y);
        return band_index < _bands.Length &&
               _bands[band_index].Start <= y &&
               Contains(_bands[band_index].Intervals, x);
    }

    /// <summary>Gets whether any area in the set covers a tile.</summary>
    /// <param name="point">The tile coordinates.</param>
    public bool Contains(Point point) => Contains(point.X, point.Y);

    /// <summary>Gets whether any area in the set covers a tile, ignoring its height.</summary>
    /// <param name="tile">The tile.</param>
    public bool Contains(Tile tile) => Contains(tile.X, tile.Y);

    /// <summary>Gets whether the set covers every tile of an area.</summary>
    /// <remarks>The tiles may be covered by several areas of the set together.</remarks>
    /// <param name="area">The area to check.</param>
    public bool Contains(Area area)
    {
        if (_geometry_bounds is not { } bounds || !bounds.Contains(area))
            return false;

        long left = area.X1;
        long right = (long)area.X2 + 1;
        long top = area.Y1;
        long bottom = (long)area.Y2 + 1;
        long cursor = top;
        int band_index = FindFirstBandEndingAfter(top);

        while (cursor < bottom && band_index < _bands.Length)
        {
            VerticalBand band = _bands[band_index];
            if (band.Start > cursor)
                return false;
            if (!Contains(band.Intervals, left, right))
                return false;

            cursor = Math.Min(bottom, band.End);
            band_index++;
        }

        return cursor == bottom;
    }

    /// <summary>Gets whether the set covers at least one tile of an area.</summary>
    /// <param name="area">The area to check.</param>
    public bool Intersects(Area area)
    {
        if (_geometry_bounds is not { } bounds || !bounds.Intersects(area))
            return false;

        long left = area.X1;
        long right = (long)area.X2 + 1;
        long top = area.Y1;
        long bottom = (long)area.Y2 + 1;
        int band_index = FindFirstBandEndingAfter(top);

        while (band_index < _bands.Length)
        {
            VerticalBand band = _bands[band_index];
            if (band.Start >= bottom)
                return false;
            if (Intersects(band.Intervals, left, right))
                return true;
            band_index++;
        }

        return false;
    }

    /// <summary>Creates a set that also contains another area.</summary>
    /// <param name="area">The area to add.</param>
    /// <returns>A new set; this set is unchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="area"/> is empty.</exception>
    public AreaSet Union(Area area) => new(_areas.Append(area));

    /// <summary>Creates a set that also contains other areas.</summary>
    /// <param name="areas">The areas to add.</param>
    /// <returns>A new set; this set is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="areas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any added area is empty.</exception>
    public AreaSet Union(IEnumerable<Area> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return new AreaSet(_areas.Concat(areas));
    }

    /// <summary>Creates a set that also contains the areas of another set.</summary>
    /// <param name="areas">The set to add.</param>
    /// <returns>A new set; this set is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="areas"/> is <see langword="null"/>.</exception>
    public AreaSet Union(AreaSet areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return Union(areas._areas);
    }

    /// <summary>Moves every area in the set by an offset.</summary>
    /// <param name="offset">The number of tiles to move along x and y.</param>
    /// <returns>A new set with the moved areas.</returns>
    public AreaSet Translate(Point offset) =>
        new(_areas.Select(area => area.Translate(offset)));

    /// <summary>Grows every area in the set by the same number of tiles on every side.</summary>
    /// <param name="amount">The number of tiles to add on each side.</param>
    /// <returns>A new set with the expanded areas.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    public AreaSet Expand(int amount) =>
        new(_areas.Select(area => area.Expand(amount)));

    /// <summary>Enumerates every tile the set covers, ordered by y and then by x, without caching them.</summary>
    /// <param name="maximumTileCount">The largest number of tiles the caller accepts.</param>
    /// <returns>The covered tiles.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumTileCount"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="TileCount"/> exceeds <paramref name="maximumTileCount"/>.</exception>
    public IEnumerable<Point> EnumerateTiles(long maximumTileCount)
    {
        if (maximumTileCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(maximumTileCount),
                maximumTileCount,
                "The maximum tile count must not be negative.");

        EnsureTileBudget(maximumTileCount);
        return EnumerateTilesCore();
    }

    /// <summary>Returns an enumerator over the distinct areas in the set.</summary>
    /// <returns>An enumerator that yields the areas in the order of <see cref="Areas"/>.</returns>
    public IEnumerator<Area> GetEnumerator() =>
        ((IEnumerable<Area>)_areas).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a set from the given areas.</summary>
    /// <param name="areas">The areas to combine; duplicates are removed.</param>
    /// <returns>The new set.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="areas"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any area is empty.</exception>
    public static AreaSet Of(params Area[] areas) => new(areas);

    private static SweepGeometry BuildGeometry(Area[] areas)
    {
        if (areas.Length == 0)
            return new SweepGeometry([], 0);

        long[] coordinates = new long[checked(areas.Length * 2)];
        for (int i = 0; i < areas.Length; i++)
        {
            coordinates[i * 2] = areas[i].X1;
            coordinates[(i * 2) + 1] = (long)areas[i].X2 + 1;
        }

        Array.Sort(coordinates);
        int coordinate_count = 1;
        for (int i = 1; i < coordinates.Length; i++)
        {
            if (coordinates[i] == coordinates[coordinate_count - 1])
                continue;
            coordinates[coordinate_count++] = coordinates[i];
        }
        Array.Resize(ref coordinates, coordinate_count);

        var coordinate_index = new Dictionary<long, int>(coordinates.Length);
        for (int i = 0; i < coordinates.Length; i++)
            coordinate_index.Add(coordinates[i], i);

        var events = new SweepEvent[checked(areas.Length * 2)];
        for (int i = 0; i < areas.Length; i++)
        {
            Area area = areas[i];
            int start = coordinate_index[area.X1];
            int end = coordinate_index[(long)area.X2 + 1];
            events[i * 2] = new SweepEvent(area.Y1, start, end, 1);
            events[(i * 2) + 1] = new SweepEvent((long)area.Y2 + 1, start, end, -1);
        }
        Array.Sort(events, static (first, second) => first.Y.CompareTo(second.Y));

        var coverage = new CoverageIndex(coordinates);
        var bands = new List<VerticalBand>();
        HorizontalInterval[] intervals = [];
        long tile_count = 0;
        long previous_y = events[0].Y;
        int event_index = 0;

        while (event_index < events.Length)
        {
            long y = events[event_index].Y;
            if (y > previous_y && coverage.CoveredLength > 0)
            {
                tile_count = checked(
                    tile_count +
                    checked(coverage.CoveredLength * (y - previous_y)));
                AddBand(bands, previous_y, y, intervals);
            }

            bool changed = false;
            while (event_index < events.Length && events[event_index].Y == y)
            {
                SweepEvent sweep_event = events[event_index++];
                changed |= coverage.Add(sweep_event.Start, sweep_event.End, sweep_event.Delta);
            }

            if (changed)
            {
                HorizontalInterval[] next = coverage.CaptureIntervals();
                if (intervals.AsSpan().SequenceEqual(next))
                    next = intervals;
                intervals = next;
            }

            previous_y = y;
        }

        return new SweepGeometry(bands.ToArray(), tile_count);
    }

    private static void AddBand(
        List<VerticalBand> bands,
        long start,
        long end,
        HorizontalInterval[] intervals)
    {
        if (bands.Count > 0)
        {
            VerticalBand previous = bands[^1];
            if (previous.End == start && ReferenceEquals(previous.Intervals, intervals))
            {
                bands[^1] = previous with { End = end };
                return;
            }
        }

        bands.Add(new VerticalBand(start, end, intervals));
    }

    private static bool Contains(HorizontalInterval[] intervals, long x)
    {
        int low = 0;
        int high = intervals.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            HorizontalInterval interval = intervals[middle];
            if (x < interval.Start)
            {
                high = middle - 1;
                continue;
            }
            if (x >= interval.End)
            {
                low = middle + 1;
                continue;
            }
            return true;
        }
        return false;
    }

    private static bool Contains(HorizontalInterval[] intervals, long left, long right)
    {
        int low = 0;
        int high = intervals.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            HorizontalInterval interval = intervals[middle];
            if (left < interval.Start)
            {
                high = middle - 1;
                continue;
            }
            if (left >= interval.End)
            {
                low = middle + 1;
                continue;
            }
            return interval.End >= right;
        }
        return false;
    }

    private static bool Intersects(HorizontalInterval[] intervals, long left, long right)
    {
        int low = 0;
        int high = intervals.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (intervals[middle].End <= left)
                low = middle + 1;
            else
                high = middle;
        }

        return low < intervals.Length && intervals[low].Start < right;
    }

    private int FindFirstBandEndingAfter(long y)
    {
        int low = 0;
        int high = _bands.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_bands[middle].End <= y)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private Point[] MaterializeTiles()
    {
        EnsureTileBudget(TileMaterializationLimit);
        return EnumerateTilesCore().ToArray();
    }

    private void EnsureTileBudget(long maximum_tile_count)
    {
        if (_tile_count <= maximum_tile_count)
            return;

        throw new InvalidOperationException(
            $"The area set contains {_tile_count} tiles, exceeding the requested limit of {maximum_tile_count}. " +
            $"Use {nameof(EnumerateTiles)} with an explicit limit for deterministic streaming.");
    }

    private IEnumerable<Point> EnumerateTilesCore()
    {
        foreach (VerticalBand band in _bands)
        {
            for (long y = band.Start; y < band.End; y++)
            {
                foreach (HorizontalInterval interval in band.Intervals)
                {
                    for (long x = interval.Start; x < interval.End; x++)
                        yield return new Point((int)x, (int)y);
                }
            }
        }
    }

    private sealed class CoverageIndex
    {
        private readonly long[] _coordinates;
        private readonly int[] _cover_count;
        private readonly long[] _covered_length;
        private readonly int _segment_count;

        public long CoveredLength => _covered_length[1];

        public CoverageIndex(long[] coordinates)
        {
            _coordinates = coordinates;
            _segment_count = coordinates.Length - 1;
            int capacity = checked((_segment_count * 4) + 4);
            _cover_count = new int[capacity];
            _covered_length = new long[capacity];
        }

        public bool Add(int start, int end, int delta)
        {
            long previous = CoveredLength;
            Add(1, 0, _segment_count, start, end, delta);
            return CoveredLength != previous;
        }

        public HorizontalInterval[] CaptureIntervals()
        {
            if (CoveredLength == 0)
                return [];

            var intervals = new List<HorizontalInterval>();
            CaptureIntervals(1, 0, _segment_count, intervals);
            return intervals.ToArray();
        }

        private void Add(
            int node,
            int left,
            int right,
            int start,
            int end,
            int delta)
        {
            if (start <= left && right <= end)
            {
                _cover_count[node] = checked(_cover_count[node] + delta);
                if (_cover_count[node] < 0)
                    throw new InvalidOperationException("Sweep coverage became negative.");
                UpdateLength(node, left, right);
                return;
            }

            int middle = left + ((right - left) / 2);
            if (start < middle)
                Add(node * 2, left, middle, start, end, delta);
            if (end > middle)
                Add((node * 2) + 1, middle, right, start, end, delta);
            UpdateLength(node, left, right);
        }

        private void UpdateLength(int node, int left, int right)
        {
            if (_cover_count[node] > 0)
            {
                _covered_length[node] = _coordinates[right] - _coordinates[left];
                return;
            }

            _covered_length[node] = right - left == 1
                ? 0
                : _covered_length[node * 2] + _covered_length[(node * 2) + 1];
        }

        private void CaptureIntervals(
            int node,
            int left,
            int right,
            List<HorizontalInterval> intervals)
        {
            if (_covered_length[node] == 0)
                return;

            if (_cover_count[node] > 0 || right - left == 1)
            {
                AddInterval(intervals, _coordinates[left], _coordinates[right]);
                return;
            }

            int middle = left + ((right - left) / 2);
            CaptureIntervals(node * 2, left, middle, intervals);
            CaptureIntervals((node * 2) + 1, middle, right, intervals);
        }

        private static void AddInterval(
            List<HorizontalInterval> intervals,
            long start,
            long end)
        {
            if (intervals.Count == 0 || intervals[^1].End < start)
            {
                intervals.Add(new HorizontalInterval(start, end));
                return;
            }

            HorizontalInterval previous = intervals[^1];
            intervals[^1] = previous with { End = Math.Max(previous.End, end) };
        }
    }

    private readonly record struct SweepGeometry(VerticalBand[] Bands, long TileCount);
    private readonly record struct SweepEvent(long Y, int Start, int End, int Delta);
    private readonly record struct GeometryBounds(long Left, long Top, long Right, long Bottom)
    {
        public bool Contains(long x, long y) =>
            x >= Left && x < Right && y >= Top && y < Bottom;

        public bool Contains(Area area) =>
            !area.IsEmpty &&
            area.X1 >= Left &&
            (long)area.X2 + 1 <= Right &&
            area.Y1 >= Top &&
            (long)area.Y2 + 1 <= Bottom;

        public bool Intersects(Area area) =>
            !area.IsEmpty &&
            area.X1 < Right &&
            (long)area.X2 + 1 > Left &&
            area.Y1 < Bottom &&
            (long)area.Y2 + 1 > Top;
    }
    private readonly record struct VerticalBand(
        long Start,
        long End,
        HorizontalInterval[] Intervals);
    private readonly record struct HorizontalInterval(long Start, long End);
}
