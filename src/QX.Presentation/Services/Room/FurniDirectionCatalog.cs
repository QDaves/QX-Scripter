using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Linq;
using Flazzy;
using Flazzy.Tags;

namespace Qx.Presentation.Services.Room;

public interface IFurniDirections
{
    Task<IReadOnlyList<int>> ForAsync(int revision, string? identifier, CancellationToken cancellation_token = default);
}

public sealed class FurniDirectionCatalog : IFurniDirections, IDisposable
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    readonly HttpClient _http;
    readonly ConcurrentDictionary<string, Task<IReadOnlyList<int>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public FurniDirectionCatalog(HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = RequestTimeout;
    }

    public async Task<IReadOnlyList<int>> ForAsync(
        int revision,
        string? identifier,
        CancellationToken cancellation_token = default)
    {
        if (revision <= 0 || string.IsNullOrWhiteSpace(identifier))
            return [];
        string name = identifier.Replace('*', '_');
        string key = $"{revision}/{name}";
        Task<IReadOnlyList<int>> pending = _cache.GetOrAdd(key, _ => DownloadAsync(revision, name, key));
        return await pending.WaitAsync(cancellation_token).ConfigureAwait(false);
    }

    public void Dispose() => _http.Dispose();

    async Task<IReadOnlyList<int>> DownloadAsync(int revision, string identifier, string key)
    {
        try
        {
            string escaped = Uri.EscapeDataString(identifier);
            byte[] data = await _http
                .GetByteArrayAsync($"https://images.habbo.com/dcr/hof_furni/{revision}/{escaped}.swf")
                .ConfigureAwait(false);
            return Read(data);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            _cache.TryRemove(key, out _);
            return [];
        }
    }

    public static IReadOnlyList<int> Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var swf = new ShockwaveFlash(data);
        swf.Disassemble();
        foreach (DefineBinaryDataTag binary in swf.Tags.OfType<DefineBinaryDataTag>())
        {
            IReadOnlyList<int> directions = ReadObjectData(binary.Data);
            if (directions.Count != 0)
                return directions;
        }
        return [];
    }

    public static IReadOnlyList<int> ReadObjectData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try
        {
            using var input = new MemoryStream(data, writable: false);
            XDocument document = XDocument.Load(input, LoadOptions.None);
            XElement? root = document.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "objectData", StringComparison.Ordinal))
                return [];
            return root
                .Elements().SingleOrDefault(element => element.Name.LocalName == "model")?
                .Elements().SingleOrDefault(element => element.Name.LocalName == "directions")?
                .Elements().Where(element => element.Name.LocalName == "direction")
                .Select(element => element.Attribute("id")?.Value)
                .Select(value => int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int degrees) ? degrees : -1)
                .Where(degrees => degrees is >= 0 and < 360 && degrees % 45 == 0)
                .Select(degrees => degrees / 45)
                .Distinct()
                .Order()
                .ToArray() ?? [];
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return [];
        }
    }
}
