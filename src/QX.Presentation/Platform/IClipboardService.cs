namespace Qx.Presentation.Platform;

public interface IClipboardService
{
    Task<bool> TrySetTextAsync(string text, CancellationToken cancellation_token = default);
}
