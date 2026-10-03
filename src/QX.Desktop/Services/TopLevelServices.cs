using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Qx.Diagnostics;
using Qx.Presentation.Platform;

namespace Qx.Desktop.Services;

sealed class AvaloniaClipboardService(TopLevelAccessor top_levels) : IClipboardService
{
    public async Task<bool> TrySetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (top_levels.Current?.Clipboard is not { } clipboard)
        {
            Diag.Warn("The clipboard is not available here.", "ui");
            return false;
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception error)
        {
            Diag.Warn($"The clipboard refused the text: {error.Message}", "ui");
            return false;
        }
    }
}

sealed class AvaloniaLauncherService(TopLevelAccessor top_levels) : ILauncherService
{
    public async Task<bool> OpenUriAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            Diag.Warn($"Refused to open {uri.OriginalString}: only http and https links are opened.", "ui");
            return false;
        }
        if (top_levels.Current?.Launcher is not { } launcher)
            return false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await launcher.LaunchUriAsync(uri);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception error)
        {
            Diag.Warn($"Could not open {uri}: {error.Message}", "ui");
            return false;
        }
    }

    public async Task<bool> OpenFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (top_levels.Current?.Launcher is not { } launcher)
            return false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
            return await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception error)
        {
            Diag.Warn($"Could not open the folder {path}: {error.Message}", "ui");
            return false;
        }
    }
}

sealed class AvaloniaFilePickerService(TopLevelAccessor top_levels) : IFilePickerService
{
    public static readonly FilePickerFileType ScriptFiles = new("C# scripts")
    {
        Patterns = ["*.csx", "*.cs"]
    };

    public async Task<FilePickResult> PickFileAsync(string title, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        if (top_levels.Current?.StorageProvider is not { } storage)
            return new FilePickResult(null, false, "The file picker is not available here.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<IStorageFile> picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [ScriptFiles, FilePickerFileTypes.All]
            });
            if (picked.Count == 0)
                return new FilePickResult(null, true, null);
            return picked[0].TryGetLocalPath() is { Length: > 0 } local
                ? new FilePickResult(local, false, null)
                : new FilePickResult(null, false, "That location has no path on this computer.");
        }
        catch (OperationCanceledException)
        {
            return new FilePickResult(null, true, null);
        }
        catch (Exception error)
        {
            return new FilePickResult(null, false, error.Message);
        }
    }

    public async Task<FilePickResult> SaveTextAsync(string suggestedName, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suggestedName);
        ArgumentNullException.ThrowIfNull(content);
        if (top_levels.Current?.StorageProvider is not { } storage)
            return new FilePickResult(null, false, "The file picker is not available here.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IStorageFile? target = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save",
                SuggestedFileName = suggestedName
            });
            if (target is null)
                return new FilePickResult(null, true, null);
            await using Stream stream = await target.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
            return new FilePickResult(target.TryGetLocalPath(), false, null);
        }
        catch (OperationCanceledException)
        {
            return new FilePickResult(null, true, null);
        }
        catch (Exception error)
        {
            return new FilePickResult(null, false, error.Message);
        }
    }
}
