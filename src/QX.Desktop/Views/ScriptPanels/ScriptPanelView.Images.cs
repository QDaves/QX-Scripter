using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia;
using Qx.Presentation.Threading;
using SkiaSharp;

namespace Qx.Desktop.Views.ScriptPanels;

public sealed partial class ScriptPanelView
{
    readonly Dictionary<Image, CancellationTokenSource> _images = [];

    void ImageLoaded(object? sender, RoutedEventArgs args)
    {
        if (sender is not Image image)
            return;
        image.PropertyChanged -= ImageChanged;
        image.PropertyChanged += ImageChanged;
        ReadImageAsync(image).Observe("scripts");
    }

    void ImageUnloaded(object? sender, RoutedEventArgs args)
    {
        if (sender is not Image image)
            return;
        image.PropertyChanged -= ImageChanged;
        ClearImage(image);
    }

    void ImageChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == TagProperty && sender is Image image)
            ReadImageAsync(image).Observe("scripts");
    }

    void ClearImage(Image image)
    {
        if (_images.Remove(image, out var source))
        {
            source.Cancel();
            source.Dispose();
        }
        var previous = image.Source as Bitmap;
        image.Source = null;
        previous?.Dispose();
    }

    async Task ReadImageAsync(Image image)
    {
        ClearImage(image);
        if (image.Tag is not string { Length: > 0 } path)
            return;
        var source = new CancellationTokenSource();
        _images[image] = source;
        CancellationToken token = source.Token;
        Bitmap? bitmap = null;
        try
        {
            bitmap = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var stream = File.OpenRead(path);
                if (stream.Length > 64 * 1024 * 1024)
                    throw new IOException("Image preview exceeds 64 MB.");
                using var codec = SKCodec.Create(stream) ?? throw new IOException("Unsupported image format.");
                SKImageInfo info = codec.Info;
                if (info.Width <= 0 || info.Height <= 0)
                    throw new IOException("Invalid image dimensions.");
                double scale = Math.Min(1, 2048.0 / Math.Max(info.Width, info.Height));
                int width = Math.Max(1, (int)Math.Round(info.Width * scale));
                int height = Math.Max(1, (int)Math.Round(info.Height * scale));
                token.ThrowIfCancellationRequested();
                using var decoded = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                SKCodecResult result = codec.GetPixels(decoded.Info, decoded.GetPixels());
                if (result != SKCodecResult.Success)
                {
                    stream.Position = 0;
                    return info.Width >= info.Height ? Bitmap.DecodeToWidth(stream, width) : Bitmap.DecodeToHeight(stream, height);
                }
                return new Bitmap(Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul,
                    decoded.GetPixels(), new PixelSize(width, height), new Vector(96, 96), decoded.RowBytes);
            }, token);
            if (token.IsCancellationRequested || !image.IsLoaded)
                return;
            image.Source = bitmap;
            bitmap = null;
            ToolTip.SetTip(image, null);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!token.IsCancellationRequested)
                ToolTip.SetTip(image, "Image preview unavailable: " + error.Message);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }
}
