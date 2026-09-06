using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Xml.Linq;
using Flazzy;
using Flazzy.Tags;
using Qx.Game;

namespace Qx.Ui;

internal static class FurniDirectionCatalog
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly ConcurrentDictionary<string, Task<IReadOnlyList<int>>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    internal static async Task<IReadOnlyList<int>> GetAsync(
        FurniInfo info,
        CancellationToken cancellation_token = default)
    {
        if (info.Revision <= 0 || string.IsNullOrWhiteSpace(info.Identifier))
            return [];

        string identifier = info.Identifier.Replace('*', '_');
        string key = $"{info.Revision}/{identifier}";
        Task<IReadOnlyList<int>> pending = Cache.GetOrAdd(
            key,
            _ => DownloadAsync(info.Revision, identifier, key));
        return await pending.WaitAsync(cancellation_token).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<int>> DownloadAsync(
        int revision,
        string identifier,
        string key)
    {
        try
        {
            string name = Uri.EscapeDataString(identifier);
            byte[] data = await Http.GetByteArrayAsync(
                $"https://images.habbo.com/dcr/hof_furni/{revision}/{name}.swf").ConfigureAwait(false);
            return Parse(data);
        }
        catch
        {
            Cache.TryRemove(key, out _);
            return [];
        }
    }

    internal static IReadOnlyList<int> Parse(byte[] data)
    {
        using var swf = new ShockwaveFlash(data);
        swf.Disassemble();

        foreach (DefineBinaryDataTag binary in swf.Tags.OfType<DefineBinaryDataTag>())
        {
            IReadOnlyList<int> directions = ParseObjectData(binary.Data);
            if (directions.Count != 0)
                return directions;
        }
        return [];
    }

    internal static IReadOnlyList<int> ParseObjectData(byte[] data)
    {
        try
        {
            using var input = new MemoryStream(data, writable: false);
            XDocument document = XDocument.Load(input, LoadOptions.None);
            XElement? root = document.Root;
            if (!string.Equals(root?.Name.LocalName, "objectData", StringComparison.Ordinal))
                return [];

            return root!
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
        catch
        {
            return [];
        }
    }
}
