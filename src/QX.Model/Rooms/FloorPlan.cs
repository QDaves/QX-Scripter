using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the static floor layout of a room.</summary>
/// <remarks>
/// Received as the Flash <c>FloorHeightmap</c> message. The map is a block of text with one line
/// per row. Each character is a tile height, <c>0</c> to <c>9</c> and then <c>a</c> to <c>z</c>
/// (or <c>A</c> to <c>Z</c>) for 10 to 35, and <c>x</c> or <c>X</c> marks a spot with no floor.
/// </remarks>
public sealed class FloorPlan : IParserComposer<FloorPlan>
{
    private readonly int[] _tiles;

    /// <summary>Gets whether the room is drawn at the legacy 32 pixel scale instead of 64.</summary>
    public bool UseLegacyScale { get; init; }
    /// <summary>Gets the wall height as sent by the hotel.</summary>
    public int WallHeight { get; init; }
    /// <summary>Gets the floor map text.</summary>
    public string Map { get; }
    /// <summary>Gets the regions of the floor that area-hide furni hide.</summary>
    public IReadOnlyList<AreaHideData> HiddenAreas { get; init; } = [];
    /// <summary>Gets the x coordinate of the room camera as sent by the hotel.</summary>
    public int CameraX { get; init; }
    /// <summary>Gets the y coordinate of the room camera as sent by the hotel.</summary>
    public int CameraY { get; init; }
    /// <summary>Gets the z coordinate of the room camera as sent by the hotel.</summary>
    public float CameraZ { get; init; }
    /// <summary>Gets whether the camera fields hold values; always <see langword="true"/> for a parsed floor plan.</summary>
    public bool HasCameraData { get; init; } = true;

    /// <summary>Gets the number of tiles along x, which is the length of the longest map line.</summary>
    public int Width { get; }
    /// <summary>Gets the number of tiles along y, which is the number of map lines.</summary>
    public int Length { get; }
    /// <summary>Gets the drawing scale in pixels: 32 with <see cref="UseLegacyScale"/>, otherwise 64.</summary>
    public int Scale => UseLegacyScale ? 32 : 64;
    /// <summary>Gets the height of every tile row by row, with -1 where there is no floor.</summary>
    public IReadOnlyList<int> Tiles => _tiles;

    /// <summary>Gets the height of a tile, or -1 where there is no floor.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public int this[int x, int y] => HeightAt(x, y);
    /// <summary>Gets the height of a tile, or -1 where there is no floor.</summary>
    /// <param name="point">The tile coordinates.</param>
    public int this[Point point] => HeightAt(point.X, point.Y);

    /// <summary>Initializes a new instance of the <see cref="FloorPlan"/> class from map text.</summary>
    /// <remarks>
    /// Lines are split on carriage returns and line feeds, and empty lines are dropped. Positions past
    /// the end of a shorter line count as no floor.
    /// </remarks>
    /// <param name="map">The floor map text; <see langword="null"/> is treated as empty.</param>
    public FloorPlan(string map)
    {
        Map = map ?? "";
        _tiles = build_tiles(Map, out int width, out int length);
        Width = width;
        Length = length;
    }

    /// <summary>Gets the height of a tile.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    /// <returns>The tile height, or -1 when there is no floor or the tile is outside the map.</returns>
    public int HeightAt(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Length ? -1 : _tiles[y * Width + x];

    /// <summary>Gets whether a tile is floor, meaning its height is not -1.</summary>
    /// <param name="x">The tile x coordinate.</param>
    /// <param name="y">The tile y coordinate.</param>
    public bool IsOpen(int x, int y) => HeightAt(x, y) >= 0;

    /// <summary>Reads a floor plan from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static FloorPlan Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FloorPlan ParseFlash(in PacketReader p)
    {
        bool legacy = p.ReadBool();
        int wall_height = p.ReadInt();
        string map = p.ReadString();
        int count = checked((ushort)p.ReadInt());
        var hidden = new AreaHideData[count];
        for (int i = 0; i < count; i++)
            hidden[i] = p.Parse<AreaHideData>();

        return new FloorPlan(map)
        {
            UseLegacyScale = legacy,
            WallHeight = wall_height,
            HiddenAreas = hidden,
            CameraX = p.ReadInt(),
            CameraY = p.ReadInt(),
            CameraZ = p.ReadFloatBinary(),
            HasCameraData = true
        };
    }

    /// <summary>Writes the floor plan to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="OverflowException">Thrown when there are more than 65535 hidden areas.</exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FloorPlan value, in PacketWriter p)
    {
        ushort count = checked((ushort)value.HiddenAreas.Count);
        p.WriteBool(value.UseLegacyScale);
        p.WriteInt(value.WallHeight);
        p.WriteString(value.Map);
        p.WriteInt(count);
        foreach (AreaHideData area in value.HiddenAreas)
            p.Compose(area);
        p.WriteInt(value.CameraX);
        p.WriteInt(value.CameraY);
        p.WriteFloatBinary(value.CameraZ);
    }

    private static int[] build_tiles(string map, out int width, out int length)
    {
        string[] lines = map.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        length = lines.Length;
        width = 0;
        foreach (string line in lines)
            if (line.Length > width)
                width = line.Length;

        int[] tiles = new int[width * length];
        Array.Fill(tiles, -1);
        for (int y = 0; y < lines.Length; y++)
        {
            string line = lines[y];
            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];
                if (c is not ('x' or 'X'))
                    tiles[y * width + x] = height_from_char(c);
            }
        }

        return tiles;
    }

    private static int height_from_char(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'z' => 10 + (c - 'a'),
        >= 'A' and <= 'Z' => 10 + (c - 'A'),
        _ => 0
    };
}
