using System.Text.Json;
using Qx.Game.Application;
using Qx.Mcp;

namespace Qx.Hosting;

public static class ApplicationMcpTools
{
    public static IReadOnlyList<McpTool> Create(IApplicationRuntime application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var tools = new List<McpTool>
        {
            ListTool(application),
            DescribeTool(application)
        };
        tools.AddRange(application.Members
            .Where(descriptor => descriptor.Kind is not ApplicationMemberKind.Event &&
                descriptor.Exposure.HasFlag(ApplicationExposure.Mcp))
            .Select(descriptor => MemberTool(application, descriptor)));
        return Array.AsReadOnly(tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray());
    }

    private static McpTool ListTool(IApplicationRuntime application) => new()
    {
        Name = "list_application_members",
        Title = "List application members",
        Description = "List the shared QX application queries, operations and events with live Flash/Unity availability.",
        InputSchema = EmptySchema(),
        OutputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["members"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "object" }
                }
            },
            ["required"] = new[] { "members" },
            ["additionalProperties"] = false
        },
        Metadata = new Dictionary<string, object?>
        {
            ["qx.surface"] = "application",
            ["qx.kind"] = "catalog"
        },
        Annotations = new McpToolAnnotations(true, false, true, false),
        Handler = (_, _) => Task.FromResult(ApplicationJson.Serialize(new
        {
            members = application.Members
                .Select(member => ApplicationJson.Describe(application.Describe(member.Id)))
                .ToArray()
        }))
    };

    private static McpTool DescribeTool(IApplicationRuntime application) => new()
    {
        Name = "describe_application_member",
        Title = "Describe application member",
        Description = "Describe one shared application member, including parameters, state requirements, client support, semantic messages and active catalog evidence.",
        InputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["id"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Canonical application member id."
                }
            },
            ["required"] = new[] { "id" },
            ["additionalProperties"] = false
        },
        OutputSchema = new Dictionary<string, object?> { ["type"] = "object" },
        Metadata = new Dictionary<string, object?>
        {
            ["qx.surface"] = "application",
            ["qx.kind"] = "descriptor"
        },
        Annotations = new McpToolAnnotations(true, false, true, false),
        Handler = (args, _) => Task.FromResult(ApplicationJson.Serialize(
            ApplicationJson.Describe(application.Describe(RequiredString(args, "id")))))
    };

    private static McpTool MemberTool(
        IApplicationRuntime application,
        ApplicationDescriptor descriptor)
    {
        Type request_type = descriptor.RequestType
            ?? throw new InvalidOperationException($"Application member '{descriptor.Id}' has no request type.");
        ApplicationToolHints hints = descriptor.ToolHints
            ?? throw new InvalidOperationException($"Application member '{descriptor.Id}' has no MCP tool hints.");
        return new McpTool
        {
            Name = ToolName(descriptor.Id),
            Title = descriptor.Title,
            Description = descriptor.Description,
            InputSchema = ApplicationJson.InputSchema(descriptor),
            OutputSchema = ApplicationJson.OutputSchema(descriptor.ResultType),
            Metadata = Metadata(descriptor),
            Annotations = new McpToolAnnotations(
                hints.ReadOnly,
                hints.Destructive,
                hints.Idempotent,
                hints.OpenWorld),
            Handler = async (args, cancellation_token) =>
            {
                object request = ApplicationJson.Deserialize(args, request_type);
                try
                {
                    object? result = await application
                        .InvokeAsync(descriptor.Id, request, cancellation_token)
                        .ConfigureAwait(false);
                    return ApplicationJson.Serialize(result);
                }
                catch (ApplicationUnavailableException error)
                {
                    throw Unavailable(descriptor, error);
                }
            }
        };
    }

    private static McpToolException Unavailable(
        ApplicationDescriptor descriptor,
        ApplicationUnavailableException error)
    {
        string[] missing_states = error.Availability.MissingStates
            .Select(value => Snake(value.ToString()))
            .ToArray();
        string[] unresolved_messages = error.Availability.ActiveMessages
            .Where(message => !message.Resolved)
            .Select(message => message.Key.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] unavailable_wire_capabilities = error.Availability.ActiveMessages
            .Where(message => message.WireAvailable is false)
            .Select(message => message.WireReason is { Length: > 0 } reason
                ? $"{message.Key.Value}: {reason}"
                : $"{message.Key.Value}: {message.WireCapability}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string reason = string.Join("; ", new[]
        {
            missing_states.Length == 0
                ? null
                : "missing states: " + string.Join(", ", missing_states),
            unresolved_messages.Length == 0
                ? null
                : "unresolved messages: " + string.Join(", ", unresolved_messages),
            unavailable_wire_capabilities.Length == 0
                ? null
                : "unavailable wire capabilities: " + string.Join(", ", unavailable_wire_capabilities)
        }.Where(value => value is not null));
        string message = reason.Length == 0
            ? error.Message
            : $"{error.Message} {reason}.";
        return new McpToolException(
            message,
            new Dictionary<string, object?>
            {
                ["qx.error"] = "application_unavailable",
                ["qx.id"] = descriptor.Id,
                ["qx.availability"] = ApplicationJson.DescribeAvailability(error.Availability)
            });
    }

    private static IReadOnlyDictionary<string, object?> Metadata(ApplicationDescriptor descriptor) =>
        new Dictionary<string, object?>
        {
            ["qx.surface"] = "application",
            ["qx.id"] = descriptor.Id,
            ["qx.kind"] = Snake(descriptor.Kind.ToString()),
            ["qx.invocation_scope"] = Snake(descriptor.InvocationScope.ToString()),
            ["qx.request_type"] = descriptor.RequestType?.FullName,
            ["qx.result_type"] = descriptor.ResultType.FullName,
            ["qx.required_states"] = descriptor.RequiredStates.Select(value => Snake(value.ToString())).ToArray(),
            ["qx.state_effects"] = descriptor.StateEffects.Select(effect => new Dictionary<string, object?>
            {
                ["state"] = Snake(effect.State.ToString()),
                ["kind"] = Snake(effect.Kind.ToString())
            }).ToArray(),
            ["qx.messages"] = descriptor.Messages.Select(message => new Dictionary<string, object?>
            {
                ["key"] = message.Key.Value,
                ["direction"] = Snake(message.Direction.ToString()),
                ["role"] = Snake(message.Role.ToString()),
                ["required"] = message.Required
            }).ToArray()
        };

    private static object EmptySchema() => new Dictionary<string, object?>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object?>(),
        ["required"] = Array.Empty<string>(),
        ["additionalProperties"] = false
    };

    private static string ToolName(string id) => "application_" + id
        .Replace('.', '_')
        .Replace('-', '_');

    private static string RequiredString(JsonElement args, string name)
    {
        if (args.ValueKind is JsonValueKind.Object &&
            args.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind is JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }
        throw new ArgumentException($"'{name}' is required.", nameof(args));
    }

    private static string Snake(string value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value);
}
