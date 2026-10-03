using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Game.Snapshots;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Provides JSON serialization for query envelopes.
/// </summary>
/// <remarks>
/// The output is indented and uses the web defaults, so property names are camel case. Ids are
/// written as decimal strings and enums as their member names.
/// </remarks>
public static class QueryJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    /// <summary>
    /// Serializes a query envelope to JSON.
    /// </summary>
    /// <typeparam name="T">The type of the envelope's data.</typeparam>
    /// <param name="result">The envelope to serialize.</param>
    /// <returns>The JSON text of the envelope.</returns>
    public static string Serialize<T>(QueryEnvelope<T> result) =>
        JsonSerializer.Serialize(result, SerializerOptions);

    /// <summary>
    /// Serializes a successful query result to JSON.
    /// </summary>
    /// <remarks>
    /// The envelope's metadata reports the data as ready, loaded and not stale, with nothing pending.
    /// </remarks>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="query">The query name.</param>
    /// <param name="data">The result data.</param>
    /// <param name="truncated">Whether the data was cut short.</param>
    /// <param name="capturedAtUtc">The capture time, or <see langword="null"/> to use the current UTC time.</param>
    /// <returns>The JSON text of the envelope.</returns>
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

    /// <summary>
    /// Serializes a failed query result to JSON.
    /// </summary>
    /// <remarks>
    /// The error code is derived from the exception type. The envelope's data is
    /// <see langword="null"/> and every metadata flag is <see langword="false"/>.
    /// </remarks>
    /// <param name="query">The query name.</param>
    /// <param name="error">The exception that failed the query.</param>
    /// <param name="cancellationToken">The token of the failed operation. When it was canceled, an <see cref="OperationCanceledException"/> is reported as <c>cancelled</c> instead of <c>timeout</c>.</param>
    /// <param name="capturedAtUtc">The capture time, or <see langword="null"/> to use the current UTC time.</param>
    /// <returns>The JSON text of the envelope.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
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
