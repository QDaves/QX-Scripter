namespace Qx.Model;

public readonly record struct HeightmapTile(int X, int Y, short Value)
{
    public bool IsFloor => Value >= 0;
    public bool IsBlocked => (Value & 0x4000) != 0;
    public bool IsFree => IsFloor && !IsBlocked;
    public double Height => Value >= 0 ? (Value & 0x3FFF) / 256.0 : -1;
    public Point Location => new(X, Y);
}
