using System.Text.Json.Serialization;

namespace Qx.Presentation.Services.Settings;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SettingsDocument))]
internal sealed partial class SettingsJson : JsonSerializerContext;
