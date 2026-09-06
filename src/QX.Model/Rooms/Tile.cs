using Qx.Messages;

namespace Qx.Model;

public readonly record struct Tile(int X, int Y, float Z) : IParserComposer<Tile>
{
    public Tile(int x, int y) : this(x, y, 0) { }

    public Point XY => new(X, Y);

    public override string ToString() => $"({X}, {Y}, {Z:0.0#######})";

    public static Tile Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt(), p.ReadFloat());

    public static bool TryParseString(string value, out Tile tile)
    {
        tile = default;
        if (value.StartsWith('(') && value.EndsWith(')'))
            value = value[1..^1];

        string[] parts = value.Split(',');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out int x) ||
            !int.TryParse(parts[1], out int y) ||
            !float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        tile = new Tile(x, y, z);
        return true;
    }

    public static Tile ParseString(string value) =>
        TryParseString(value, out Tile tile) ? tile : throw new FormatException($"Invalid tile format: '{value}'.");

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(X);
        p.WriteInt(Y);
        p.WriteFloat(Z);
    }

    public static Tile operator +(Tile t, Point offset) => new(t.X + offset.X, t.Y + offset.Y, t.Z);
    public static Tile operator -(Tile t, Point offset) => new(t.X - offset.X, t.Y - offset.Y, t.Z);

    public static implicit operator Point(Tile t) => new(t.X, t.Y);
    public static implicit operator Tile((int X, int Y, float Z) t) => new(t.X, t.Y, t.Z);
    public static implicit operator Tile((int X, int Y, double Z) t) => new(t.X, t.Y, (float)t.Z);
}
