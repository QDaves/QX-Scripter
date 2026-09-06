using System.Globalization;

namespace Flazzy.ABC;

public readonly record struct ASFloat4(float X, float Y, float Z, float W)
{
    public static ASFloat4 NaN { get; } = new(float.NaN, float.NaN, float.NaN, float.NaN);

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{X:R},{Y:R},{Z:R},{W:R}");
    }
}
