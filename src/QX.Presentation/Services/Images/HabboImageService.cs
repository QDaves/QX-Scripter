using System.Collections.Concurrent;
using Qx.Presentation.Platform;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Images;

public sealed class HabboImageService : IImageService, IDisposable
{
    public const int MaxConcurrentDownloads = 8;
    public static readonly TimeSpan FailureHold = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    readonly HttpClient _http;
    readonly TimeProvider _time;
    readonly string _root;
    readonly SemaphoreSlim _gate = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    readonly ConcurrentDictionary<string, Task<byte[]?>> _loading = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, DateTimeOffset> _failed = new(StringComparer.Ordinal);

    public HabboImageService(IAppPaths paths, TimeProvider time, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _root = paths.ImageCacheDirectory;
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = RequestTimeout;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("QX");
    }

    public async Task<byte[]?> LoadBytesAsync(string? url, CancellationToken cancellation_token = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (_failed.TryGetValue(url, out DateTimeOffset failed_at))
        {
            if (_time.GetUtcNow() - failed_at < FailureHold)
                return null;
            _failed.TryRemove(url, out _);
        }
        var created = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<byte[]?> loading = _loading.GetOrAdd(url, created.Task);
        if (ReferenceEquals(loading, created.Task))
            FillAsync(url, created).Observe("images");
        return await loading.WaitAsync(cancellation_token).ConfigureAwait(false);
    }

    public async Task<bool> PreloadAsync(string? url, CancellationToken cancellation_token = default) =>
        await LoadBytesAsync(url, cancellation_token).ConfigureAwait(false) is not null;

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }

    async Task FillAsync(string url, TaskCompletionSource<byte[]?> loading)
    {
        try
        {
            loading.TrySetResult(await FetchAsync(url).ConfigureAwait(false));
        }
        catch (Exception error)
        {
            loading.TrySetException(error);
        }
        finally
        {
            _loading.TryRemove(url, out _);
        }
    }

    async Task<byte[]?> FetchAsync(string url)
    {
        string path = DiskPath(url);
        if (await ReadDiskAsync(path).ConfigureAwait(false) is { } cached && ImageBytes.LooksLikeImage(cached))
            return cached;
        byte[]? raw = await DownloadAsync(url).ConfigureAwait(false);
        if (raw is null || !ImageBytes.LooksLikeImage(raw))
        {
            _failed[url] = _time.GetUtcNow();
            return null;
        }
        await WriteDiskAsync(path, raw).ConfigureAwait(false);
        return raw;
    }

    async Task<byte[]?> DownloadAsync(string url)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(url).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false) : null;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    string DiskPath(string url)
    {
        string name = HabboUrls.DiskName(url);
        return Path.Combine(_root, name[..2], name + ".img");
    }

    static async Task<byte[]?> ReadDiskAsync(string path)
    {
        try
        {
            return File.Exists(path) ? await File.ReadAllBytesAsync(path).ConfigureAwait(false) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static async Task WriteDiskAsync(string path, byte[] raw)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string staging = path + ".tmp";
            await File.WriteAllBytesAsync(staging, raw).ConfigureAwait(false);
            File.Move(staging, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }
}
