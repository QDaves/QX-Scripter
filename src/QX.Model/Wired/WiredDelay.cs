namespace Qx.Model.Wired;

public static class WiredDelay
{
    public const int MaximumMilliseconds = 60000;
    public const int PrecisionMilliseconds = 50;

    public static bool IsValid(int value) => value is >= 0 and <= 120 ||
        value is >= -MaximumMilliseconds and < 0 && value % PrecisionMilliseconds == 0;

    public static int ToMilliseconds(int value)
    {
        if (!IsValid(value))
            throw new ArgumentOutOfRangeException(nameof(value), "The delay must be 0..120 half-second pulses or negative milliseconds in steps of 50, up to 60 seconds.");
        return value < 0 ? -value : checked(value * 500);
    }

    public static int FromMilliseconds(int milliseconds)
    {
        if (milliseconds is < 0 or > MaximumMilliseconds || milliseconds % PrecisionMilliseconds != 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "The delay must be 0..60000 milliseconds in steps of 50.");
        return milliseconds % 500 == 0 ? milliseconds / 500 : -milliseconds;
    }
}
