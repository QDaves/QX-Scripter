using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the live stacking heights of a room's tiles, which decide where avatars can walk.</summary>
/// <remarks>
/// Received as the Flash <c>HeightMap</c> message. Each tile holds a raw value; see
/// <see cref="HeightmapTile.Value"/> for how it is encoded.
/// </remarks>
public sealed class Heightmap : IParserComposer<Heightmap>
{
    private readonly short[] _values;

    /// <summary>Gets the number of tiles along x.</summary>
    public int Width { get; }
    /// <summary>Gets the number of tiles along y.</summary>
    public int Length { get; }
    /// <summary>Gets the number of raw values in the heightmap.</summary>
    public int Count => _values.Length;

    /// <summary>Initializes a new instance of the <see cref="Heightmap"/> class.</summary>
    /// <remarks>The array is used directly, not copied, and <see cref="Apply"/> writes into it.</remarks>
    /// <param name="width">The number of tiles along x.</param>
    /// <param name="values">The raw tile values, row by row.</param>
    public Heightmap(int width, short[] values)
    {
        _values = values;
        Width = width;
        Length = width > 0 ? values.Length / width : 0;
    }

    /// <summary>Gets a tile of the heightmap.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public HeightmapTile this[int x, int y] => TileAt(x, y);
    /// <summary>Gets a tile of the heightmap.</summary>
    /// <param name="point">The tile coordinates.</param>
    public HeightmapTile this[Point point] => TileAt(point.X, point.Y);

    /// <summary>Gets a tile of the heightmap.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    /// <returns>The tile, with a raw value of -1 when it is outside the heightmap.</returns>
    public HeightmapTile TileAt(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Length
            ? new HeightmapTile(x, y, -1)
            : new HeightmapTile(x, y, _values[y * Width + x]);

    /// <summary>Gets whether a tile is floor and not blocked.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public bool IsFree(int x, int y) => TileAt(x, y).IsFree;

    /// <summary>Gets every tile of the heightmap, row by row.</summary>
    public IEnumerable<HeightmapTile> Tiles
    {
        get
        {
            for (int i = 0; i < _values.Length; i++)
                yield return new HeightmapTile(i % Width, i / Width, _values[i]);
        }
    }

    /// <summary>Sets the raw value of a tile; coordinates outside the heightmap are ignored.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    /// <param name="value">The new raw value.</param>
    public void Apply(int x, int y, short value)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Length)
            _values[y * Width + x] = value;
    }

    /// <summary>Reads a heightmap from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static Heightmap Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Heightmap ParseFlash(in PacketReader p) => ParseMap(in p);

    private static Heightmap ParseMap(in PacketReader p)
    {
        int width = p.ReadInt();
        int n = p.ReadLength();
        var values = new short[n];
        for (int i = 0; i < n; i++)
            values[i] = p.ReadShort();
        return new Heightmap(width, values);
    }

    /// <summary>Writes the heightmap to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Heightmap value, in PacketWriter p) => value.ComposeMap(in p);

    private void ComposeMap(in PacketWriter p)
    {
        p.WriteInt(Width);
        p.WriteLength((Length)_values.Length);
        foreach (short value in _values)
            p.WriteShort(value);
    }
}
