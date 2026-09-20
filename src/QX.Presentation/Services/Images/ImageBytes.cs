namespace Qx.Presentation.Services.Images;

public static class ImageBytes
{
    public static bool LooksLikeImage(ReadOnlySpan<byte> raw) =>
        raw.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ||
        raw.StartsWith("GIF8"u8) ||
        raw.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ||
        (raw.Length >= 12 && raw.StartsWith("RIFF"u8) && raw.Slice(8, 4).SequenceEqual("WEBP"u8)) ||
        raw.StartsWith("BM"u8);
}
