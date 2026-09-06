using Qx.Messages;

namespace Qx.Model;

public sealed class Heightmap : IParserComposer<Heightmap>
{
    private readonly short[] _values;

    public int Width { get; }
    public int Length { get; }
    public int Count => _values.Length;

    public Heightmap(int width, short[] values)
    {
        _values = values;
        Width = width;
        Length = width > 0 ? values.Length / width : 0;
    }

    public HeightmapTile this[int x, int y] => TileAt(x, y);
    public HeightmapTile this[Point point] => TileAt(point.X, point.Y);

    public HeightmapTile TileAt(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Length
            ? new HeightmapTile(x, y, -1)
            : new HeightmapTile(x, y, _values[y * Width + x]);

    public bool IsFree(int x, int y) => TileAt(x, y).IsFree;

    public IEnumerable<HeightmapTile> Tiles
    {
        get
        {
            for (int i = 0; i < _values.Length; i++)
                yield return new HeightmapTile(i % Width, i / Width, _values[i]);
        }
    }

    public void Apply(int x, int y, short value)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Length)
            _values[y * Width + x] = value;
    }

    public static Heightmap Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Heightmap ParseFlash(in PacketReader p) => ParseMap(in p);

    private static Heightmap ParseUnity(in PacketReader p) => ParseMap(in p);

    private static Heightmap ParseMap(in PacketReader p)
    {
        int width = p.ReadInt();
        int n = p.ReadLength();
        var values = new short[n];
        for (int i = 0; i < n; i++)
            values[i] = p.ReadShort();
        return new Heightmap(width, values);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Heightmap value, in PacketWriter p) => value.ComposeMap(in p);

    private static void ComposeUnity(Heightmap value, in PacketWriter p) => value.ComposeMap(in p);

    private void ComposeMap(in PacketWriter p)
    {
        p.WriteInt(Width);
        p.WriteLength((Length)_values.Length);
        foreach (short value in _values)
            p.WriteShort(value);
    }
}
