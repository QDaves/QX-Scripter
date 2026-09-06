using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Game.Application;
using Qx.Protocol;

namespace Qx.Hosting;

public static class ApplicationJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(object? value, bool indented = true)
    {
        JsonSerializerOptions options = new(Options) { WriteIndented = indented };
        return JsonSerializer.Serialize(value, options);
    }

    public static object Deserialize(JsonElement value, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            using JsonDocument empty = JsonDocument.Parse("{}");
            return empty.RootElement.Deserialize(type, Options)
                ?? throw new JsonException($"Could not create '{type.FullName}'.");
        }
        if (value.ValueKind is not JsonValueKind.Object)
            throw new JsonException("Application arguments must be a JSON object.");
        return value.Deserialize(type, Options)
            ?? throw new JsonException($"Could not create '{type.FullName}'.");
    }

    public static object Describe(ApplicationMemberDescription member) => new
    {
        id = member.Descriptor.Id,
        title = member.Descriptor.Title,
        description = member.Descriptor.Description,
        kind = Name(member.Descriptor.Kind),
        invocation_scope = Name(member.Descriptor.InvocationScope),
        exposure = Enum.GetValues<ApplicationExposure>()
            .Where(value => value is not (ApplicationExposure.None or ApplicationExposure.All) &&
                member.Descriptor.Exposure.HasFlag(value))
            .Select(Name)
            .ToArray(),
        request_type = member.Descriptor.RequestType?.FullName,
        result_type = member.Descriptor.ResultType.FullName,
        parameters = member.Descriptor.Parameters.Select(parameter => new
        {
            name = parameter.Name,
            type = TypeName(parameter.Type),
            required = parameter.Required,
            default_value = parameter.DefaultValue,
            description = parameter.Description
        }).ToArray(),
        required_states = member.Descriptor.RequiredStates.Select(Name).ToArray(),
        state_effects = member.Descriptor.StateEffects.Select(effect => new
        {
            state = Name(effect.State),
            kind = Name(effect.Kind)
        }).ToArray(),
        tool_hints = member.Descriptor.ToolHints is { } hints
            ? new
            {
                read_only = hints.ReadOnly,
                destructive = hints.Destructive,
                idempotent = hints.Idempotent,
                open_world = hints.OpenWorld
            }
            : null,
        messages = member.Availability.ActiveMessages.Select(Message).ToArray(),
        clients = member.Availability.Clients.Select(client => new
        {
            client = Name(client.Client),
            supported = client.Supported,
            messages = client.Messages.Select(Message).ToArray()
        }).ToArray(),
        availability = DescribeAvailability(member.Availability),
        input_schema = InputSchema(member.Descriptor),
        output_schema = OutputSchema(member.Descriptor.ResultType)
    };

    public static object InputSchema(ApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (ApplicationParameterDescriptor parameter in descriptor.Parameters)
        {
            Dictionary<string, object?> schema = SchemaFor(parameter.Type, []);
            schema["description"] = parameter.Description;
            ApplyConstraints(schema, parameter.Type, parameter.Constraints);
            if (!parameter.Required)
                schema["default"] = SchemaValue(parameter.DefaultValue);
            properties[parameter.Name] = schema;
        }
        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = descriptor.Parameters
                .Where(parameter => parameter.Required)
                .Select(parameter => parameter.Name)
                .ToArray(),
            ["additionalProperties"] = false
        };
    }

    public static object OutputSchema(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return SchemaFor(type, []);
    }

    public static object DescribeAvailability(ApplicationAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(availability);
        return new
        {
            available = availability.Available,
            client = Name(availability.Client),
            missing_states = availability.MissingStates.Select(Name).ToArray(),
            unresolved_messages = availability.ActiveMessages
                .Where(message => !message.Resolved)
                .Select(message => message.Key.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            unsupported_messages = availability.ActiveMessages
                .Where(message => !message.Supported)
                .Select(message => message.Key.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            unavailable_wire_capabilities = availability.ActiveMessages
                .Where(message => message.WireAvailable is false)
                .Select(message => new
                {
                    key = message.Key.Value,
                    capability = message.WireCapability,
                    reason = message.WireReason,
                    headers = message.HeaderCapabilities
                        .Where(candidate => !candidate.Available)
                        .Select(candidate => new
                        {
                            candidate.Header,
                            capability = candidate.Capability,
                            candidate.Reason
                        })
                        .ToArray()
                })
                .ToArray(),
            catalog = availability.CatalogProvenance is { } provenance
                ? new
                {
                    origin = Name(provenance.Origin),
                    provenance.Source,
                    provenance.ClientVersion,
                    provenance.SourceSha256
                }
                : null
        };
    }

    private static object Message(ApplicationMessageAvailability message) => new
    {
        key = message.Key.Value,
        direction = Name(message.Direction),
        role = Name(message.Role),
        required = message.Required,
        message.Registered,
        message.Supported,
        message.Resolved,
        model_type = message.ModelType?.FullName,
        headers = message.Headers,
        wire_capability = message.WireCapability,
        wire_available = message.WireAvailable,
        wire_reason = message.WireReason,
        header_capabilities = message.HeaderCapabilities.Select(candidate => new
        {
            candidate.Header,
            capability = candidate.Capability,
            candidate.Available,
            candidate.Reason
        }).ToArray()
    };

    private static void ApplyConstraints(
        Dictionary<string, object?> schema,
        Type type,
        ApplicationParameterConstraints? constraints)
    {
        if (constraints is null)
            return;
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        bool decimal_string = effective == typeof(long) || effective == typeof(Id);
        if (constraints.Minimum is { } minimum)
        {
            schema[decimal_string ? "x-qx-minimum-decimal" : "minimum"] =
                decimal_string
                    ? minimum.ToString(CultureInfo.InvariantCulture)
                    : checked((int)minimum);
        }
        if (constraints.Maximum is { } maximum)
        {
            schema[decimal_string ? "x-qx-maximum-decimal" : "maximum"] =
                decimal_string
                    ? maximum.ToString(CultureInfo.InvariantCulture)
                    : checked((int)maximum);
        }
        if (constraints.MinLength is { } min_length)
            schema["minLength"] = min_length;
        if (constraints.MaxLength is { } max_length)
            schema["maxLength"] = max_length;
        if (constraints.MinItems is { } min_items)
            schema["minItems"] = min_items;
        if (constraints.MaxItems is { } max_items)
            schema["maxItems"] = max_items;
        if (constraints.MaxUtf8Bytes is { } max_utf8_bytes)
            schema["x-qx-max-utf8-bytes"] = max_utf8_bytes;
        if (constraints.Pattern is { } pattern)
            schema["pattern"] = pattern;
    }

    private static object? SchemaValue(object? value) => value switch
    {
        long number => number.ToString(CultureInfo.InvariantCulture),
        Id id => id.ToString(),
        Enum enumeration => Snake(enumeration.ToString()),
        _ => value
    };

    private static Dictionary<string, object?> SchemaFor(
        Type type,
        HashSet<Type> visited)
    {
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return new()
            {
                ["anyOf"] = new object[]
                {
                    SchemaFor(underlying, visited),
                    new Dictionary<string, object?> { ["type"] = "null" }
                }
            };
        }
        if (effective == typeof(string))
            return new() { ["type"] = "string" };
        if (effective == typeof(bool))
            return new() { ["type"] = "boolean" };
        if (effective == typeof(int) || effective == typeof(short) || effective == typeof(byte))
            return new() { ["type"] = "integer" };
        if (effective == typeof(long) || effective == typeof(Id))
        {
            return new()
            {
                ["type"] = "string",
                ["pattern"] = "^-?[0-9]+$"
            };
        }
        if (effective == typeof(DateTimeOffset) || effective == typeof(DateTime))
            return new() { ["type"] = "string", ["format"] = "date-time" };
        if (effective == typeof(ClientType))
        {
            return new()
            {
                ["type"] = "string",
                ["enum"] = new[] { "flash", "unity" }
            };
        }
        if (effective.IsEnum)
        {
            return new()
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(effective).Select(Snake).ToArray()
            };
        }
        Type? dictionary_value_type = DictionaryValue(effective);
        if (dictionary_value_type is not null)
        {
            return new()
            {
                ["type"] = "object",
                ["additionalProperties"] = SchemaFor(
                    dictionary_value_type,
                    visited)
            };
        }
        Type? element_type = CollectionElement(effective);
        if (element_type is not null)
        {
            return new()
            {
                ["type"] = "array",
                ["items"] = SchemaFor(element_type, visited)
            };
        }
        if (!visited.Add(effective))
            return new() { ["type"] = "object" };

        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        foreach (PropertyInfo property in effective
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
                     .OrderBy(property => property.MetadataToken))
        {
            JsonIgnoreAttribute? ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            if (ignore?.Condition is JsonIgnoreCondition.Always)
                continue;
            Dictionary<string, object?> schema = SchemaFor(property.PropertyType, visited);
            if (!property.PropertyType.IsValueType &&
                new NullabilityInfoContext().Create(property).ReadState is NullabilityState.Nullable)
            {
                schema = new Dictionary<string, object?>
                {
                    ["anyOf"] = new object[]
                    {
                        schema,
                        new Dictionary<string, object?> { ["type"] = "null" }
                    }
                };
            }
            string name = Snake(property.Name);
            properties[name] = schema;
            if (ignore is null || ignore.Condition is JsonIgnoreCondition.Never)
                required.Add(name);
        }
        visited.Remove(effective);
        return new()
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static Type? DictionaryValue(Type type)
    {
        Type? dictionary = type
            .GetInterfaces()
            .Append(type)
            .FirstOrDefault(value => value.IsGenericType &&
                value.GetGenericTypeDefinition() is { } definition &&
                (definition == typeof(IDictionary<,>) ||
                    definition == typeof(IReadOnlyDictionary<,>)));
        return dictionary?.GetGenericArguments()[1];
    }

    private static Type? CollectionElement(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();
        Type? enumerable = type
            .GetInterfaces()
            .Append(type)
            .FirstOrDefault(value => value.IsGenericType &&
                value.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    private static string TypeName(Type type)
    {
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        return effective == typeof(Id) ? "id" : Snake(effective.Name);
    }

    private static string Name<T>(T value) where T : struct, Enum => Snake(value.ToString());

    private static string Snake(string value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        options.Converters.Add(new IdConverter());
        options.Converters.Add(new Int64Converter());
        return options;
    }

    private sealed class IdConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.Number when reader.TryGetInt64(out long value) => value,
                JsonTokenType.String when Id.TryParse(reader.GetString(), out Id value) => value,
                _ => throw new JsonException("Expected a decimal Habbo identifier.")
            };

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());

        public override Id ReadAsPropertyName(
            ref Utf8JsonReader reader,
            Type type,
            JsonSerializerOptions options) =>
            Id.TryParse(reader.GetString(), out Id value)
                ? value
                : throw new JsonException("Expected a decimal Habbo identifier.");

        public override void WriteAsPropertyName(
            Utf8JsonWriter writer,
            Id value,
            JsonSerializerOptions options) =>
            writer.WritePropertyName(value.ToString());
    }

    private sealed class Int64Converter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
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

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}
