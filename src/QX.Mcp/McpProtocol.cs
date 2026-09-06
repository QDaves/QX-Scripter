using System.Text;
using System.Text.Json;

namespace Qx.Mcp;

/// <summary>Which shape of the protocol a request speaks.</summary>
public enum McpEra
{
    /// <summary>Session based, opened with <c>initialize</c>. Revision 2025-11-25 and earlier.</summary>
    Legacy,

    /// <summary>Stateless, carrying its version and capabilities per request. Revision 2026-07-28.</summary>
    Modern
}

/// <summary>
/// What a single request said about itself.
/// </summary>
/// <remarks>
/// A modern request is self-contained: nothing here is remembered between requests, which is the
/// point of the revision. A legacy request carries none of it and is answered from the version its
/// <c>initialize</c> negotiated.
/// </remarks>
/// <param name="Era">Which shape the request speaks.</param>
/// <param name="ProtocolVersion">The version the request declared.</param>
/// <param name="ClientName">The client's self-reported name, for logging only.</param>
/// <param name="ClientVersion">The client's self-reported version, for logging only.</param>
/// <param name="Capabilities">The capability names the client declared for this request.</param>
/// <param name="Extensions">The extension identifiers the client declared for this request.</param>
public sealed record McpRequestContext(
    McpEra Era,
    string ProtocolVersion,
    string? ClientName,
    string? ClientVersion,
    IReadOnlySet<string> Capabilities,
    IReadOnlySet<string> Extensions)
{
    /// <summary>A legacy request, which says nothing about itself.</summary>
    public static McpRequestContext ForLegacy(string protocolVersion) =>
        new(McpEra.Legacy, protocolVersion, null, null, EmptySet, EmptySet);

    private static readonly HashSet<string> EmptySet = new(StringComparer.Ordinal);

    /// <summary>Whether the client declared an extension for this request.</summary>
    /// <param name="identifier">The extension identifier.</param>
    public bool Supports(string identifier) => Extensions.Contains(identifier);
}

/// <summary>Why a request could not be accepted before it was dispatched.</summary>
/// <param name="Code">The JSON-RPC error code to answer with.</param>
/// <param name="Message">The message to answer with.</param>
/// <param name="Data">Extra structured detail for the error, or null.</param>
public sealed record McpRejection(int Code, string Message, object? Data = null);

/// <summary>
/// The parts of revision 2026-07-28 that decide whether a request is even addressed to this server:
/// the per-request metadata, the mirrored headers, and the error codes for getting either wrong.
/// </summary>
public static class McpProtocol
{
    /// <summary>The revision this server speaks natively.</summary>
    public const string Modern = "2026-07-28";

    /// <summary>Every revision this server answers, newest first.</summary>
    public static readonly string[] Supported =
    [
        Modern,
        "2025-11-25",
        "2025-06-18",
        "2025-03-26"
    ];

    /// <summary>The prefix the specification reserves for its own metadata keys.</summary>
    public const string MetaPrefix = "io.modelcontextprotocol/";

    public const string MetaProtocolVersion = MetaPrefix + "protocolVersion";
    public const string MetaClientInfo = MetaPrefix + "clientInfo";
    public const string MetaClientCapabilities = MetaPrefix + "clientCapabilities";
    public const string MetaServerInfo = MetaPrefix + "serverInfo";
    public const string MetaLogLevel = MetaPrefix + "logLevel";

    /// <summary>The headers do not agree with the body, or a required one is missing.</summary>
    public const int HeaderMismatch = -32020;

    /// <summary>The request needs a client capability the request did not declare.</summary>
    public const int MissingRequiredClientCapability = -32021;

    /// <summary>The request declared a version this server does not answer.</summary>
    public const int UnsupportedProtocolVersion = -32022;

    private const string Base64Open = "=?base64?";
    private const string Base64Close = "?=";

    /// <summary>Whether this server answers a revision.</summary>
    /// <param name="version">The revision the request declared.</param>
    public static bool Answers(string? version) =>
        version is not null && Array.IndexOf(Supported, version) >= 0;

    /// <summary>
    /// Whether a request is written in the modern shape.
    /// </summary>
    /// <remarks>
    /// Decided by the body alone: a modern request carries its version in <c>params._meta</c>, and
    /// a legacy one cannot. The header is not enough, because a legacy client sends it too.
    /// </remarks>
    /// <param name="request">The JSON-RPC request object.</param>
    public static bool IsModern(JsonElement request) =>
        Meta(request) is { } meta &&
        meta.TryGetProperty(MetaProtocolVersion, out JsonElement version) &&
        version.ValueKind == JsonValueKind.String;

    /// <summary>
    /// Reads what a modern request says about itself.
    /// </summary>
    /// <param name="request">The JSON-RPC request object.</param>
    /// <param name="context">What the request declared, when it is well formed.</param>
    /// <returns>Why it was refused, or null when it was accepted.</returns>
    public static McpRejection? ReadContext(JsonElement request, out McpRequestContext context)
    {
        context = McpRequestContext.ForLegacy(Modern);

        JsonElement? meta = Meta(request);
        if (meta is not { } fields ||
            !fields.TryGetProperty(MetaProtocolVersion, out JsonElement versionField) ||
            versionField.ValueKind != JsonValueKind.String)
        {
            return new McpRejection(-32602, $"Missing required '{MetaProtocolVersion}' in params._meta.");
        }

        string version = versionField.GetString()!;
        if (!Answers(version))
        {
            return new McpRejection(
                UnsupportedProtocolVersion,
                "Unsupported protocol version",
                new Dictionary<string, object?>
                {
                    ["supported"] = Supported,
                    ["requested"] = version
                });
        }

        // Required even when empty: a server may not assume a capability the client stayed silent
        // about, so the difference between "declared none" and "did not say" has to be visible.
        if (!fields.TryGetProperty(MetaClientCapabilities, out JsonElement capabilities) ||
            capabilities.ValueKind != JsonValueKind.Object)
        {
            return new McpRejection(-32602, $"Missing required '{MetaClientCapabilities}' in params._meta.");
        }

        string? clientName = null;
        string? clientVersion = null;
        if (fields.TryGetProperty(MetaClientInfo, out JsonElement clientInfo) &&
            clientInfo.ValueKind == JsonValueKind.Object)
        {
            clientName = Text(clientInfo, "name");
            clientVersion = Text(clientInfo, "version");
        }

        var declared = new HashSet<string>(StringComparer.Ordinal);
        var extensions = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty capability in capabilities.EnumerateObject())
        {
            if (capability.NameEquals("extensions"))
            {
                if (capability.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty extension in capability.Value.EnumerateObject())
                        extensions.Add(extension.Name);
                }
                continue;
            }
            declared.Add(capability.Name);
        }

        context = new McpRequestContext(
            McpEra.Modern,
            version,
            clientName,
            clientVersion,
            declared,
            extensions);
        return null;
    }

    /// <summary>
    /// Checks the headers a modern request mirrors from its body.
    /// </summary>
    /// <remarks>
    /// The point of the mirror is that a proxy can route on the header without parsing the body, so
    /// the two disagreeing is a security problem rather than a formatting one and is refused.
    /// </remarks>
    /// <param name="request">The JSON-RPC request object.</param>
    /// <param name="version">The version declared in the body.</param>
    /// <param name="header">Reads a request header by name.</param>
    /// <returns>Why it was refused, or null when the headers agree.</returns>
    public static McpRejection? CheckHeaders(
        JsonElement request,
        string version,
        Func<string, string?> header)
    {
        ArgumentNullException.ThrowIfNull(header);

        string? declaredVersion = header("MCP-Protocol-Version");
        if (string.IsNullOrEmpty(declaredVersion))
            return new McpRejection(HeaderMismatch, "Missing required MCP-Protocol-Version header.");
        if (!string.Equals(declaredVersion, version, StringComparison.Ordinal))
        {
            return new McpRejection(
                HeaderMismatch,
                $"Header mismatch: MCP-Protocol-Version header value '{declaredVersion}' does not match body value '{version}'.");
        }

        string method = request.TryGetProperty("method", out JsonElement methodField) &&
            methodField.ValueKind == JsonValueKind.String
            ? methodField.GetString()!
            : "";

        string? declaredMethod = header("Mcp-Method");
        if (string.IsNullOrEmpty(declaredMethod))
            return new McpRejection(HeaderMismatch, "Missing required Mcp-Method header.");
        if (!string.Equals(declaredMethod, method, StringComparison.Ordinal))
        {
            return new McpRejection(
                HeaderMismatch,
                $"Header mismatch: Mcp-Method header value '{declaredMethod}' does not match body value '{method}'.");
        }

        if (NamedValue(request, method) is not { } name)
            return null;

        string? declaredName = Decode(header("Mcp-Name"));
        if (declaredName is null)
            return new McpRejection(HeaderMismatch, "Missing required Mcp-Name header.");
        if (!string.Equals(declaredName, name, StringComparison.Ordinal))
        {
            return new McpRejection(
                HeaderMismatch,
                $"Header mismatch: Mcp-Name header value '{declaredName}' does not match body value '{name}'.");
        }

        return null;
    }

    /// <summary>
    /// The body value a request mirrors into <c>Mcp-Name</c>, or null when the method mirrors none.
    /// </summary>
    /// <param name="request">The JSON-RPC request object.</param>
    /// <param name="method">The request's method.</param>
    public static string? NamedValue(JsonElement request, string method)
    {
        if (!request.TryGetProperty("params", out JsonElement parameters) ||
            parameters.ValueKind != JsonValueKind.Object)
            return null;

        return method switch
        {
            "tools/call" or "prompts/get" => Text(parameters, "name"),
            "resources/read" => Text(parameters, "uri"),
            _ => null
        };
    }

    /// <summary>
    /// Reads a header value, undoing the base64 sentinel a client uses for anything a header
    /// cannot carry as plain ASCII.
    /// </summary>
    /// <param name="value">The raw header value.</param>
    public static string? Decode(string? value)
    {
        if (value is null)
            return null;
        if (!value.StartsWith(Base64Open, StringComparison.Ordinal) ||
            !value.EndsWith(Base64Close, StringComparison.Ordinal) ||
            value.Length < Base64Open.Length + Base64Close.Length)
        {
            return value;
        }

        string encoded = value[Base64Open.Length..^Base64Close.Length];
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            // Left as written rather than guessed at: a value that only looks encoded is compared
            // as it stands and fails the match, which is the answer the client needs anyway.
            return value;
        }
    }

    /// <summary>The HTTP status a rejection is answered with.</summary>
    /// <param name="code">The JSON-RPC error code.</param>
    public static int StatusFor(int code) => code switch
    {
        -32601 => 404,
        _ => 400
    };

    private static JsonElement? Meta(JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object ||
            !request.TryGetProperty("params", out JsonElement parameters) ||
            parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("_meta", out JsonElement meta) ||
            meta.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return meta;
    }

    private static string? Text(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
