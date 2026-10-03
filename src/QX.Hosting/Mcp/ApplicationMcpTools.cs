using System.Text.Json;
using Qx.Game.Application;
using Qx.Mcp;

namespace Qx.Hosting;

public static class ApplicationMcpTools
{
    private const int DefaultListLimit = 100;
    private const int MaximumListLimit = 500;
    private const int DefaultDetailLimit = 10;
    private const int MaximumDetailLimit = 50;

    private static readonly string[] kinds = [.. Enum.GetNames<ApplicationMemberKind>().Select(Snake)];

    private static readonly HashSet<string> listed = new(StringComparer.Ordinal)
    {
        ApplicationMemberIds.RoomEnter,
        ApplicationMemberIds.RoomLeave,
        ApplicationMemberIds.RoomChatTalk,
        ApplicationMemberIds.RoomChatShout,
        ApplicationMemberIds.RoomChatWhisper,
        ApplicationMemberIds.RoomAvatarWalk,
        ApplicationMemberIds.RoomAvatarDance,
        ApplicationMemberIds.RoomAvatarExpression,
        ApplicationMemberIds.RoomAvatarSign,
        ApplicationMemberIds.RoomDoorbellAnswer,
        ApplicationMemberIds.RoomPeopleRightsGrant,
        ApplicationMemberIds.RoomPetRespect,
        ApplicationMemberIds.RoomModerationKick,
        ApplicationMemberIds.RoomModerationMute,
        ApplicationMemberIds.RoomModerationBan,
        ApplicationMemberIds.RoomSettingsGet,
        ApplicationMemberIds.RoomStickyGet,
        ApplicationMemberIds.PeopleProfileGet,
        ApplicationMemberIds.PeopleBadgesGet,
        ApplicationMemberIds.PeopleRelationshipGet,
        ApplicationMemberIds.GroupsDetailsGet,
        ApplicationMemberIds.FriendsSearch,
        ApplicationMemberIds.NavigatorSearchText
    };

    public static IReadOnlyList<McpTool> Create(IApplicationRuntime application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var tools = new List<McpTool>
        {
            ListTool(application),
            DescribeTool(application)
        };
        tools.AddRange(application.Members
            .Where(HasTool)
            .Select(descriptor => MemberTool(application, descriptor)));
        return Array.AsReadOnly(tools.OrderBy(tool => tool.Name, StringComparer.Ordinal).ToArray());
    }

    private static McpTool ListTool(IApplicationRuntime application) => new()
    {
        Name = "list_application_members",
        Title = "List application members",
        Description =
            "List the shared QX application queries, operations and events as compact rows: id, kind, title, the " +
            "application_* tool, read_only and live availability. filter, kind and available narrow the list, " +
            "and next_offset continues it. detail=true returns at most 50 full descriptions per page, with schemas, " +
            "message evidence and the C# script call, as describe_application_member does for one member.",
        InputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["filter"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Text the id, title, tool name or description must contain."
                },
                ["kind"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = kinds,
                    ["description"] = "Member kind to list."
                },
                ["available"] = new Dictionary<string, object?>
                {
                    ["type"] = "boolean",
                    ["description"] = "List only members whose live availability matches."
                },
                ["detail"] = new Dictionary<string, object?>
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "Return full descriptions instead of compact rows."
                },
                ["limit"] = new Dictionary<string, object?>
                {
                    ["type"] = "integer",
                    ["default"] = DefaultListLimit,
                    ["minimum"] = 1,
                    ["maximum"] = MaximumListLimit,
                    ["description"] = "Maximum returned members: 100 by default and at most 500 for compact rows, 10 by default and at most 50 with detail=true."
                },
                ["offset"] = new Dictionary<string, object?>
                {
                    ["type"] = "integer",
                    ["default"] = 0,
                    ["minimum"] = 0,
                    ["description"] = "Zero-based index of the first returned member."
                }
            },
            ["required"] = Array.Empty<string>(),
            ["additionalProperties"] = false
        },
        OutputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["members"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?> { ["type"] = "object" }
                },
                ["total"] = new Dictionary<string, object?> { ["type"] = "integer" },
                ["next_offset"] = new Dictionary<string, object?> { ["type"] = new[] { "integer", "null" } }
            },
            ["required"] = new[] { "members", "total", "next_offset" },
            ["additionalProperties"] = false
        },
        Metadata = new Dictionary<string, object?>
        {
            ["qx.surface"] = "application",
            ["qx.kind"] = "catalog"
        },
        Annotations = new McpToolAnnotations(true, false, true, false),
        Handler = (args, _) => Task.FromResult(ListMembers(application, args))
    };

    private static McpTool DescribeTool(IApplicationRuntime application) => new()
    {
        Name = "describe_application_member",
        Title = "Describe application member",
        Description =
            "Describe one shared application member by its id or its application_* tool name: parameters, state " +
            "requirements, message support, semantic messages, active catalog evidence and the C# script call that " +
            "reaches it.",
        InputSchema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["id"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["description"] = "Application member id, such as room.chat.talk, or its tool name, such as application_room_chat_talk."
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
            ApplicationJson.Describe(application.Describe(MemberId(application, RequiredString(args, "id"))))))
    };

    private static string ListMembers(IApplicationRuntime application, JsonElement args)
    {
        string filter = OptionalString(args, "filter");
        string kind = OptionalString(args, "kind");
        if (kind.Length != 0 && !kinds.Contains(kind, StringComparer.Ordinal))
            throw new ArgumentException($"'kind' must be one of {string.Join(", ", kinds)}.", nameof(args));
        bool? available = OptionalBool(args, "available");
        bool detail = OptionalBool(args, "detail") ?? false;
        int limit = detail
            ? OptionalInt(args, "limit", DefaultDetailLimit, 1, MaximumDetailLimit)
            : OptionalInt(args, "limit", DefaultListLimit, 1, MaximumListLimit);
        int offset = OptionalInt(args, "offset", 0, 0, int.MaxValue);

        ApplicationMemberDescription[] matches =
        [
            .. application.Members
                .Where(descriptor => kind.Length == 0 || Snake(descriptor.Kind.ToString()) == kind)
                .Where(descriptor => filter.Length == 0 || Mentions(descriptor, filter))
                .Select(descriptor => application.Describe(descriptor.Id))
                .Where(member => available is null || member.Availability.Available == available)
        ];
        ApplicationMemberDescription[] page = [.. matches.Skip(offset).Take(limit)];
        int end = offset + page.Length;
        return ApplicationJson.Serialize(new
        {
            members = detail
                ? page.Select(ApplicationJson.Describe).ToArray()
                : page.Select(Row).ToArray(),
            total = matches.Length,
            next_offset = end < matches.Length ? end : (int?)null
        });
    }

    private static object Row(ApplicationMemberDescription member) => new
    {
        id = member.Descriptor.Id,
        kind = Snake(member.Descriptor.Kind.ToString()),
        title = member.Descriptor.Title,
        tool = HasTool(member.Descriptor) ? ToolName(member.Descriptor.Id) : null,
        read_only = member.Descriptor.ToolHints?.ReadOnly,
        available = member.Availability.Available
    };

    private static bool Mentions(ApplicationDescriptor descriptor, string text) =>
        descriptor.Id.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        descriptor.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        descriptor.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
        (HasTool(descriptor) && ToolName(descriptor.Id).Contains(text, StringComparison.OrdinalIgnoreCase));

    private static string MemberId(IApplicationRuntime application, string name) =>
        application.Members.FirstOrDefault(descriptor => HasTool(descriptor) && ToolName(descriptor.Id) == name)?.Id ?? name;

    private static bool HasTool(ApplicationDescriptor descriptor) =>
        descriptor.Kind is not ApplicationMemberKind.Event &&
        descriptor.Exposure.HasFlag(ApplicationExposure.Mcp);

    private static McpTool MemberTool(
        IApplicationRuntime application,
        ApplicationDescriptor descriptor)
    {
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
            Listed = listed.Contains(descriptor.Id),
            Annotations = new McpToolAnnotations(
                hints.ReadOnly,
                hints.Destructive,
                hints.Idempotent,
                hints.OpenWorld),
            Handler = async (args, cancellation_token) =>
            {
                object request = ApplicationJson.Deserialize(args, descriptor);
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

    private static string OptionalString(JsonElement args, string name) =>
        Argument(args, name) switch
        {
            null => "",
            { ValueKind: JsonValueKind.String } value => value.GetString()!.Trim(),
            _ => throw new ArgumentException($"'{name}' must be a string.", nameof(args))
        };

    private static bool? OptionalBool(JsonElement args, string name) =>
        Argument(args, name) switch
        {
            null => null,
            { ValueKind: JsonValueKind.True } => true,
            { ValueKind: JsonValueKind.False } => false,
            _ => throw new ArgumentException($"'{name}' must be a boolean.", nameof(args))
        };

    private static int OptionalInt(JsonElement args, string name, int fallback, int minimum, int maximum)
    {
        if (Argument(args, name) is not { } value)
            return fallback;
        if (value.ValueKind is not JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw new ArgumentException($"'{name}' must be an integer.", nameof(args));
        if (result < minimum || result > maximum)
            throw new ArgumentOutOfRangeException(nameof(args), result, $"'{name}' must be between {minimum} and {maximum}.");
        return result;
    }

    private static JsonElement? Argument(JsonElement args, string name) =>
        args.ValueKind is JsonValueKind.Object &&
        args.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind is not JsonValueKind.Null
            ? value
            : null;

    private static string Snake(string value) => JsonNamingPolicy.SnakeCaseLower.ConvertName(value);
}
