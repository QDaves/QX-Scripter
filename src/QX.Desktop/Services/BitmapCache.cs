using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Qx.Presentation.Services.Images;
using Qx.Presentation.Threading;

namespace Qx.Desktop.Services;

public sealed class BitmapCache(IImageService images, TimeProvider? time = null)
{
    public const int Capacity = 2048;

    readonly ConcurrentDictionary<string, Task<Bitmap?>> _loading = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, byte> _undecodable = new(StringComparer.Ordinal);
    readonly LinkedList<string> _recent = new();
    readonly Dictionary<string, (Bitmap Image, LinkedListNode<string> Node, long Created)> _memory = new(StringComparer.Ordinal);
    readonly Lock _memory_gate = new();
    readonly TimeProvider _time = time ?? TimeProvider.System;

    public Bitmap? Cached(string url)
    {
        lock (_memory_gate)
        {
            if (!_memory.TryGetValue(url, out var entry))
                return null;
            if (HabboUrls.IsUserAvatar(url) && _time.GetElapsedTime(entry.Created) >= HabboUrls.UserAvatarCacheLifetime)
            {
                _recent.Remove(entry.Node);
                _memory.Remove(url);
                return null;
            }
            return entry.Image;
        }
    }

    public async Task<Bitmap?> LoadAsync(string url, bool exactPixels, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (Cached(url) is { } image)
            return image;
        if (_undecodable.ContainsKey(url))
            return null;
        var created = new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Bitmap?> loading = _loading.GetOrAdd(url, created.Task);
        if (ReferenceEquals(loading, created.Task))
            FillAsync(url, exactPixels, created).Observe("images");
        return await loading.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    async Task FillAsync(string url, bool exact_pixels, TaskCompletionSource<Bitmap?> loading)
    {
        try
        {
            byte[]? raw = await images.LoadBytesAsync(url, CancellationToken.None).ConfigureAwait(false);
            Bitmap? decoded = raw is null ? null : await Task.Run(() => AvaloniaImageDecoder.Decode(raw, exact_pixels)).ConfigureAwait(false);
            if (raw is not null && decoded is null && !HabboUrls.IsUserAvatar(url))
                _undecodable.TryAdd(url, 0);
            loading.TrySetResult(decoded is null ? null : Remember(url, decoded));
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

    Bitmap Remember(string url, Bitmap image)
    {
        lock (_memory_gate)
        {
            if (_memory.TryGetValue(url, out var existing))
            {
                _recent.Remove(existing.Node);
                _recent.AddFirst(existing.Node);
                return existing.Image;
            }
            _memory[url] = (image, _recent.AddFirst(url), _time.GetTimestamp());
            while (_memory.Count > Capacity && _recent.Last is { } oldest)
            {
                _recent.RemoveLast();
                _memory.Remove(oldest.Value);
            }
        }
        return image;
    }
}
