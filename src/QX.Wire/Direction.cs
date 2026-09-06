namespace Qx;

[Flags]
public enum Direction
{
    None = 0,
    In = 1 << 0,
    Out = 1 << 1,
    Both = In | Out
}
