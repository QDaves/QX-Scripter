using Qx.Messages;

namespace Qx.Model;

public sealed class FloorPlan : IParserComposer<FloorPlan>
{
    private readonly int[] _tiles;

    public bool UseLegacyScale { get; init; }
    public int WallHeight { get; init; }
    public string Map { get; }
    public IReadOnlyList<AreaHideData> HiddenAreas { get; init; } = [];
    public int CameraX { get; init; }
    public int CameraY { get; init; }
    public float CameraZ { get; init; }
    public bool HasCameraData { get; init; } = true;

    public int Width { get; }
    public int Length { get; }
    public int Scale => UseLegacyScale ? 32 : 64;
    public IReadOnlyList<int> Tiles => _tiles;

    public int this[int x, int y] => HeightAt(x, y);
    public int this[Point point] => HeightAt(point.X, point.Y);

    public FloorPlan(string map)
    {
        Map = map ?? "";
        _tiles = build_tiles(Map, out int width, out int length);
        Width = width;
        Length = length;
    }

    public int HeightAt(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Length ? -1 : _tiles[y * Width + x];

    public bool IsOpen(int x, int y) => HeightAt(x, y) >= 0;

    public static FloorPlan Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static FloorPlan ParseUnity(in PacketReader p)
    {
        bool legacy = p.ReadBool();
        int wall_height = p.ReadInt();
        string map = p.ReadString();
        int count = unchecked((ushort)p.ReadShort());
        var hidden = new AreaHideData[count];
        for (int i = 0; i < count; i++)
            hidden[i] = p.Parse<AreaHideData>();

        bool has_camera_data = p.Available > 0;
        int camera_x = 0;
        int camera_y = 0;
        float camera_z = 0;
        if (has_camera_data)
        {
            camera_x = p.ReadInt();
            camera_y = p.ReadInt();
            camera_z = p.ReadFloatBinary();
        }

        return new FloorPlan(map)
        {
            UseLegacyScale = legacy,
            WallHeight = wall_height,
            HiddenAreas = hidden,
            CameraX = camera_x,
            CameraY = camera_y,
            CameraZ = camera_z,
            HasCameraData = has_camera_data
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(FloorPlan value, in PacketWriter p)
    {
        ushort count = checked((ushort)value.HiddenAreas.Count);
        p.WriteBool(value.UseLegacyScale);
        p.WriteInt(value.WallHeight);
        p.WriteString(value.Map);
        p.WriteShort(unchecked((short)count));
        foreach (AreaHideData area in value.HiddenAreas)
            p.Compose(area);
        if (value.HasCameraData)
        {
            p.WriteInt(value.CameraX);
            p.WriteInt(value.CameraY);
            p.WriteFloatBinary(value.CameraZ);
        }
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
