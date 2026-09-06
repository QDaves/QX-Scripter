using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Qx;

namespace Qx.Protocol.Sulek;

public sealed partial class SulekMessages
{
    [JsonPropertyName("messages")]
    public SulekDirections Messages { get; set; } = new();

    public static SulekMessages Parse(string json) =>
        JsonSerializer.Deserialize(json, SulekJsonContext.Default.SulekMessages) ?? new SulekMessages();

    public MessageCatalog ToCatalog()
    {
        var catalog = new MessageCatalog();
        foreach (SulekEntry entry in Messages.Incoming)
            catalog.Add(Direction.In, entry.Id, CleanName(entry.Name));
        foreach (SulekEntry entry in Messages.Outgoing)
            catalog.Add(Direction.Out, entry.Id, CleanName(entry.Name));
        return catalog;
    }

    public static string CleanName(string name) => SuffixRegex().Replace(name, "");

    [GeneratedRegex(@"(((Message)?Composer)|((Message)?Event))$")]
    private static partial Regex SuffixRegex();
}

public sealed class SulekDirections
{
    [JsonPropertyName("incoming")]
    public List<SulekEntry> Incoming { get; set; } = [];

    [JsonPropertyName("outgoing")]
    public List<SulekEntry> Outgoing { get; set; } = [];
}

public sealed class SulekEntry
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SulekMessages))]
internal partial class SulekJsonContext : JsonSerializerContext;
