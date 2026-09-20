namespace Qx.Presentation.Platform;

public interface IFilePickerService
{
    Task<FilePickResult> PickFileAsync(string title, CancellationToken cancellation_token = default);

    Task<FilePickResult> SaveTextAsync(string suggested_name, string content, CancellationToken cancellation_token = default);
}
