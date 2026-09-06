using Qx.Messages;

namespace Qx.Model;

public readonly record struct Point(int X, int Y) : IParserComposer<Point>
{
    public static readonly Point Zero = new(0, 0);

    public override string ToString() => $"({X}, {Y})";

    public static Point Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(X);
        p.WriteInt(Y);
    }

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator -(Point p) => new(-p.X, -p.Y);

    public static implicit operator Point((int X, int Y) xy) => new(xy.X, xy.Y);
}
