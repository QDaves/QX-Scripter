using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Presentation.Services.Library;

public sealed record LibraryDocument
{
    public string? View { get; init; }

    public string? Sort { get; init; }

    public IReadOnlyList<string>? Collapsed { get; init; }

    public IReadOnlyDictionary<string, ScriptMeta>? Scripts { get; init; }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LibraryDocument))]
internal sealed partial class LibraryJson : JsonSerializerContext;

public static class LibraryCodec
{
    public static string Write(LibraryDocument document) =>
        JsonSerializer.Serialize(document, LibraryJson.Default.LibraryDocument);

    public static LibraryDocument Read(string json) =>
        JsonSerializer.Deserialize(json, LibraryJson.Default.LibraryDocument) ?? new LibraryDocument();
}
