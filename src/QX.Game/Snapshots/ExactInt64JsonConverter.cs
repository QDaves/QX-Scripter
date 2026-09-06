using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Game.Snapshots;

public sealed class ExactInt64JsonConverter : JsonConverter<long>
{
    public override long Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetInt64(out long value) => value,
            JsonTokenType.String when long.TryParse(
                reader.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long value) => value,
            _ => throw new JsonException("Expected a signed 64-bit decimal value.")
        };

    public override void Write(
        Utf8JsonWriter writer,
        long value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
