using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Game.Snapshots;

namespace Qx.Scripting;

public static class QueryJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string Serialize<T>(QueryEnvelope<T> result) =>
        JsonSerializer.Serialize(result, SerializerOptions);

    public static string SerializeResult<T>(
        string query,
        T data,
        bool truncated = false,
        DateTimeOffset? capturedAtUtc = null) =>
        Serialize(QueryResults.Success(
            query,
            data,
            truncated: truncated,
            capturedAtUtc: capturedAtUtc));

    public static string SerializeFailure(
        string query,
        Exception error,
        CancellationToken cancellationToken = default,
        DateTimeOffset? capturedAtUtc = null) =>
        Serialize(QueryResults.Failure<object>(
            query,
            error,
            cancellationToken,
            capturedAtUtc));

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new IdJsonConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class IdJsonConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Number when reader.TryGetInt64(out long value) => value,
                JsonTokenType.String when Id.TryParse(reader.GetString(), out Id value) => value,
                _ => throw new JsonException("Expected a decimal Habbo identifier.")
            };

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
