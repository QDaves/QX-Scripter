namespace Qx.Desktop.Services;

public readonly record struct TitleBarInsets(double Leading, double Trailing)
{
    public static TitleBarInsets None { get; } = new(0, 0);
}
