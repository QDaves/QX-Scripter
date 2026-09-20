namespace Qx.Presentation.Services.Images;

public static class IconTrim
{
    public static PixelBounds? Bounds(ReadOnlySpan<byte> bgra, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (bgra.Length < width * height * 4)
            throw new ArgumentException("The buffer is smaller than the image.", nameof(bgra));
        int left = width;
        int top = height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width * 4;
            for (int x = 0; x < width; x++)
            {
                if (bgra[row + x * 4 + 3] == 0)
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }
        if (right < 0)
            return null;
        var bounds = new PixelBounds(left, top, right - left + 1, bottom - top + 1);
        return bounds is { X: 0, Y: 0 } && bounds.Width == width && bounds.Height == height ? null : bounds;
    }
}
