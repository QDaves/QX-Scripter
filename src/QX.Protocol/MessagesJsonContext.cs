using System.Text.Json.Serialization;

namespace Qx.Protocol;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(MessagesJson))]
internal partial class MessagesJsonContext : JsonSerializerContext;
