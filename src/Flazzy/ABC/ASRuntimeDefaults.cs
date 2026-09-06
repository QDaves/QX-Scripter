namespace Flazzy.ABC;

public static class ASRuntimeDefaults
{
    public static float FloatNaN { get; } =
        BitConverter.Int32BitsToSingle(
            0x7FFFFFFF);

    public static double NumberNaN { get; } =
        FloatNaN;

    public static ASFloat4 Float4NaN { get; } =
        new(
            FloatNaN,
            FloatNaN,
            FloatNaN,
            FloatNaN);
}
