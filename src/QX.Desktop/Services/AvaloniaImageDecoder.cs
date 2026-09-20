using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Qx.Presentation.Services.Images;

namespace Qx.Desktop.Services;

public static class AvaloniaImageDecoder
{
    static readonly Vector _standard_dpi = new(96, 96);

    public static Bitmap? Decode(byte[] raw, bool exact_pixels)
    {
        ArgumentNullException.ThrowIfNull(raw);
        try
        {
            using var stream = new MemoryStream(raw, writable: false);
            if (!exact_pixels)
                return new Bitmap(stream);
            using WriteableBitmap source = WriteableBitmap.Decode(stream);
            return Normalize(source);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException or IOException or ExternalException)
        {
            return null;
        }
    }

    static WriteableBitmap Normalize(WriteableBitmap source)
    {
        using ILockedFramebuffer frame = source.Lock();
        PixelSize size = frame.Size;
        int stride = size.Width * 4;
        byte[] pixels = new byte[stride * size.Height];
        for (int row = 0; row < size.Height; row++)
            Marshal.Copy(frame.Address + row * frame.RowBytes, pixels, row * stride, stride);
        PixelBounds bounds = IconTrim.Bounds(pixels, size.Width, size.Height) ?? new PixelBounds(0, 0, size.Width, size.Height);
        var result = new WriteableBitmap(new PixelSize(bounds.Width, bounds.Height), _standard_dpi, frame.Format, frame.AlphaFormat);
        using ILockedFramebuffer target = result.Lock();
        for (int row = 0; row < bounds.Height; row++)
            Marshal.Copy(pixels, (bounds.Y + row) * stride + bounds.X * 4, target.Address + row * target.RowBytes, bounds.Width * 4);
        return result;
    }
}
