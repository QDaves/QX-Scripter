using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Protocol;

public sealed class MessagesJson
{
    [JsonPropertyName("Incoming")]
    public List<MessageEntry> Incoming { get; set; } = [];

    [JsonPropertyName("Outgoing")]
    public List<MessageEntry> Outgoing { get; set; } = [];

    public static MessagesJson Parse(string json) =>
        JsonSerializer.Deserialize(json, MessagesJsonContext.Default.MessagesJson) ?? new MessagesJson();

    public static MessagesJson Load(string path) => Parse(File.ReadAllText(path));
}

public sealed class MessageEntry
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";
}
