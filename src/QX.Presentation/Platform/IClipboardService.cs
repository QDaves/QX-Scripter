namespace Qx.Presentation.Platform;

public interface IClipboardService
{
    Task<bool> TrySetTextAsync(string text, CancellationToken cancellationToken = default);
}
