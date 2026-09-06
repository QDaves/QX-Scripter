using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Qx.Mcp;

public sealed class McpServer
{
    private const string ProtocolVersion = "2025-11-25";
    private const int DefaultRunTimeoutMs = 30000;
    private const int MinRunTimeoutMs = 1000;
    private const int MaxRunTimeoutMs = 600000;
    private const int AbandonGraceMs = 2000;
    private const int MaxOffset = 1000000;

    private static readonly HashSet<string> SupportedProtocolVersions =
        [.. McpProtocol.Supported];

    private static readonly string ServerVersion = ResolveVersion();

    private static readonly JsonSerializerOptions IndentedJson =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string CoreInstructions =
        """
        QX Scripter drives a live Habbo client. It attaches to a Flash or Unity session through a
        G-Earth interceptor, mirrors the whole game state, and hosts a C# scripting runtime that runs
        inside the QX process.

        ORIENT
        get_server_info reports the running version, the negotiated protocol and which capabilities this
        server is currently allowed to use. list_mcp_tools lists every tool with its required capability
        and whether it is presently permitted. get_connection reports whether the interceptor and the
        hotel session are live; nearly every game tool returns empty data until they are.

        SCRIPTS
        Scripts are C# (Roslyn scripting, top-level statements). Every public member of ScriptGlobals is
        in scope as a top-level symbol: Room, Users, FloorItems, Send, OnIn/OnOut, Delay, Log and more.
        get_scripting_guide is the canonical overview, list_api lists that surface, and search_types,
        get_type and search_members expose the exact model types and signatures behind it. A script
        compile_check compiles without running. run_code and run_script take timeout_ms and are
        abandoned when it expires, so never loop without checking Ct.

        READ RESULTS
        Read tools answer with one envelope: { query, metadata, data, error }. metadata carries ready,
        loaded, stale, truncated, capturedAtUtc and pending; ready and loaded separate "nothing there"
        from "not loaded yet", and pending names what is still missing. Collection tools take limit and
        offset; when metadata.truncated is true, metadata.nextOffset holds the offset of the next page.
        Tokenized collection pages return metadata.snapshotRevision; send it back as snapshot_revision
        for every continuation so all pages come from the same immutable snapshot.
        detail=false (the default) returns a compact projection, detail=true the complete client-shaped
        snapshot.

        SUBSYSTEMS
        Beyond the room and the profile, get_forums, get_forum_threads, get_quests, get_crafting,
        get_subscriptions and get_gifts expose state the hotel only sends after the matching request.
        Marketplace state and operations are exposed through application_marketplace_* tools; use
        list_application_members for their authoritative parameters, client support and message evidence.
        Everything else is reachable from a script through list_api and search_members.
        """;
    private const string EditorInstructions =
        """

        EDITOR
        Editor tools expose open tabs, their current source, execution state, output and diagnostics.
        A script can give its tab a panel of inputs, inline buttons and output boxes with //@ui:
        directives and drive it through Ui. run_tab accepts timeout_ms like the saved-script runners.
        """;
    private static readonly McpToolAnnotations ClosedReadOnly = new(true, false, false, false);
    private static readonly McpToolAnnotations OpenReadOnly = new(true, false, false, true);
    private static readonly McpToolAnnotations ClosedWrite = new(false, false, false, false);
    private static readonly McpToolAnnotations ClosedIdempotentWrite = new(false, false, true, false);
    private static readonly McpToolAnnotations ClosedDestructiveWrite = new(false, true, false, false);
    private static readonly McpToolAnnotations ClosedIdempotentDestructiveWrite = new(false, true, true, false);
    private static readonly McpToolAnnotations OpenWrite = new(false, false, false, true);
    private static readonly McpToolAnnotations OpenDestructiveWrite = new(false, true, false, true);

    private readonly IMcpHost _host;
    private readonly int _basePort;
    private readonly List<McpTool> _tools;
    private McpConfig _config;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private long _last_request_ticks;

    /// <summary>
    /// When an authenticated request was last served, or null if none has been.
    /// </summary>
    /// <remarks>
    /// The closest thing this server has to "a client is connected". MCP over HTTP has no session
    /// to hold open, so the only honest answer is when one last called — the window turns that
    /// into "active" for a while and back again, rather than claiming a connection it cannot see.
    /// </remarks>
    public DateTime? LastRequestUtc =>
        Volatile.Read(ref _last_request_ticks) is var ticks && ticks > 0
            ? new DateTime(ticks, DateTimeKind.Utc)
            : null;

    public int Port { get; private set; }
    public bool IsRunning => _listener?.IsListening == true;
    private bool EditorAvailable => RuntimeSupports(McpRuntimeCapability.Editor);
    private string Instructions =>
        EditorAvailable ? CoreInstructions + EditorInstructions : CoreInstructions;

    /// <summary>
    /// The token and capability flags every request is checked against.
    /// </summary>
    /// <remarks>
    /// Replaceable while the server is listening, so a capability turned off in the interface takes
    /// effect on the next request rather than the next launch. The configuration itself stays
    /// immutable — a whole new one is swapped in, which is a single reference assignment and so
    /// cannot be read half-applied by a request already in flight.
    /// </remarks>
    public McpConfig Config
    {
        get => Volatile.Read(ref _config);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Volatile.Write(ref _config, value);
        }
    }

    /// <summary>The informational version reported to clients, read from the entry assembly.</summary>
    public static string Version => ServerVersion;

    /// <summary>The endpoint an MCP client should be configured with, including the access token.</summary>
    public string ClientUrl =>
        Config.RequireAuth
            ? $"http://127.0.0.1:{Port}/mcp?token={Config.Token}"
            : $"http://127.0.0.1:{Port}/mcp";

    public McpServer(
        IMcpHost host,
        int basePort = 9390,
        McpConfig? config = null) : this(host, basePort, config, null)
    {
    }

    public McpServer(
        IMcpHost host,
        int basePort,
        McpConfig? config,
        IEnumerable<McpTool>? additional_tools)
    {
        _host = host;
        _basePort = basePort;
        Port = basePort;
        _config = config ?? McpConfig.Load();
        _tools = BuildTools(additional_tools);
    }

    public bool Start()
    {
        // Fixed port so the Claude MCP config URL (http://127.0.0.1:9390/mcp) is stable.
        // A dynamic/scanning port would drift and break the static client config.
        // 9390 is QX's own: 9090 is taken by Xabbo.Scripter's MCP and 9092+ by G-Earth extensions.
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{_basePort}/mcp/");
        try
        {
            listener.Start();
            _listener = listener;
            Port = _basePort;
            _cts = new CancellationTokenSource();
            _ = AcceptLoop(_cts.Token);
            return true;
        }
        catch (HttpListenerException)
        {
            listener.Close();
            return false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Close();
        _listener = null;
    }

    public static string PortHolder(int port)
    {
        try
        {
            int pid = owning_pid(port);
            if (pid <= 0)
                return "another process";
            using System.Diagnostics.Process holder = System.Diagnostics.Process.GetProcessById(pid);
            return $"{holder.ProcessName} (pid {pid})";
        }
        catch
        {
            return "another process";
        }
    }

    [System.Runtime.InteropServices.DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        nint table, ref int size, bool order, int af, int tableClass, int reserved);

    private static int owning_pid(int port)
    {
        const int afInet = 2;
        const int tcpTableOwnerPidListener = 3;

        int size = 0;
        GetExtendedTcpTable(0, ref size, false, afInet, tcpTableOwnerPidListener, 0);
        nint buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, afInet, tcpTableOwnerPidListener, 0) != 0)
                return 0;

            int rows = System.Runtime.InteropServices.Marshal.ReadInt32(buffer);
            nint row = buffer + 4;
            for (int i = 0; i < rows; i++)
            {
                int localPort = System.Runtime.InteropServices.Marshal.ReadInt32(row, 8);
                int resolved = ((localPort & 0xFF) << 8) | ((localPort >> 8) & 0xFF);
                if (resolved == port)
                    return System.Runtime.InteropServices.Marshal.ReadInt32(row, 12);
                row += 24;
            }
            return 0;
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    private async Task AcceptLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true })
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                break;
            }

            _ = HandleRequest(context, cancellationToken);
        }
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            HttpListenerRequest request = context.Request;
            if (!IsLoopbackHost(request.Headers["Host"] ?? request.UserHostName))
            {
                CloseResponse(context.Response, HttpStatusCode.Forbidden);
                return;
            }

            if (!IsAllowedOrigin(request.Headers["Origin"]))
            {
                CloseResponse(context.Response, HttpStatusCode.Forbidden);
                return;
            }

            if (request.HttpMethod != "POST")
            {
                context.Response.Headers[HttpResponseHeader.Allow] = "POST";
                CloseResponse(context.Response, HttpStatusCode.MethodNotAllowed);
                return;
            }

            if (Config.RequireAuth && !Config.MatchesToken(PresentedToken(request)))
            {
                context.Response.AddHeader("WWW-Authenticate", "Bearer realm=\"QX Scripter MCP\"");
                CloseResponse(context.Response, HttpStatusCode.Unauthorized);
                return;
            }

            // Stamped only once a caller has got past the token, so a port scan cannot make the
            // window claim an agent is attached.
            Volatile.Write(ref _last_request_ticks, DateTime.UtcNow.Ticks);

            string? protocolVersion = request.Headers["MCP-Protocol-Version"];
            if (!string.IsNullOrWhiteSpace(protocolVersion) && !SupportedProtocolVersions.Contains(protocolVersion))
            {
                // Answered as a modern error rather than a bare status, because that body is how a
                // dual-era client tells a modern server from one that simply does not speak MCP.
                await WriteJsonResponse(
                    context.Response,
                    HttpStatusCode.BadRequest,
                    Error(default, McpProtocol.UnsupportedProtocolVersion, "Unsupported protocol version",
                        new Dictionary<string, object?>
                        {
                            ["supported"] = McpProtocol.Supported,
                            ["requested"] = protocolVersion
                        }),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            using JsonDocument doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && McpProtocol.IsModern(doc.RootElement))
            {
                await ServeModern(context, doc.RootElement, request.Headers.Get, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var responses = new List<string>();
            bool isBatch = doc.RootElement.ValueKind == JsonValueKind.Array;
            bool isEmptyBatch = false;

            if (isBatch)
            {
                string effectiveProtocolVersion = string.IsNullOrWhiteSpace(protocolVersion)
                    ? "2025-03-26"
                    : protocolVersion;
                if (effectiveProtocolVersion != "2025-03-26")
                {
                    CloseResponse(context.Response, HttpStatusCode.BadRequest);
                    return;
                }

                isEmptyBatch = doc.RootElement.GetArrayLength() == 0;
                if (isEmptyBatch)
                    responses.Add(Error(default, -32600, "Invalid Request"));

                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    string? r = await Dispatch(element, cancellationToken).ConfigureAwait(false);
                    if (r is not null)
                        responses.Add(r);
                }
            }
            else
            {
                string? response = await Dispatch(doc.RootElement, cancellationToken).ConfigureAwait(false);
                if (response is not null)
                    responses.Add(response);
            }

            if (responses.Count == 0)
            {
                CloseResponse(context.Response, HttpStatusCode.Accepted);
                return;
            }

            string responseJson = isBatch && !isEmptyBatch
                ? "[" + string.Join(",", responses) + "]"
                : responses[0];
            await WriteJsonResponse(context.Response, HttpStatusCode.OK, responseJson, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            try
            {
                string responseJson = Error(default, -32700, "Parse error");
                await WriteJsonResponse(context.Response, HttpStatusCode.BadRequest, responseJson, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                context.Response.Abort();
            }
        }
        catch (OperationCanceledException)
        {
            context.Response.Abort();
        }
        catch
        {
            try { CloseResponse(context.Response, HttpStatusCode.InternalServerError); } catch { }
        }
    }

    /// <summary>
    /// Answers one stateless request of revision 2026-07-28.
    /// </summary>
    /// <remarks>
    /// Everything the request needs to be understood is in the request, so nothing is looked up and
    /// nothing is kept. The checks in front of the dispatch are the revision's own: the metadata
    /// has to be complete, the version has to be one this server answers, and the mirrored headers
    /// have to agree with the body, because an intermediary may have routed on them.
    /// </remarks>
    private async Task ServeModern(
        HttpListenerContext context,
        JsonElement request,
        Func<string, string?> header,
        CancellationToken cancellationToken)
    {
        JsonElement id = request.TryGetProperty("id", out JsonElement identifier) && IsValidId(identifier)
            ? identifier
            : default;

        McpRejection? rejection = McpProtocol.ReadContext(request, out McpRequestContext protocol)
            ?? McpProtocol.CheckHeaders(request, protocol.ProtocolVersion, header);

        if (rejection is null &&
            request.TryGetProperty("method", out JsonElement methodField) &&
            methodField.ValueKind == JsonValueKind.String &&
            !Answers(methodField.GetString()!))
        {
            rejection = new McpRejection(-32601, $"Method not found: {methodField.GetString()}");
        }

        if (rejection is { } refusal)
        {
            await WriteJsonResponse(
                context.Response,
                (HttpStatusCode)McpProtocol.StatusFor(refusal.Code),
                Error(id, refusal.Code, refusal.Message, refusal.Data),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        string? response = await Dispatch(request, protocol, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            CloseResponse(context.Response, HttpStatusCode.Accepted);
            return;
        }

        await WriteJsonResponse(context.Response, HttpStatusCode.OK, response, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Whether this server implements a method at all, whatever the era.</summary>
    private static bool Answers(string method) => method is
        "server/discover" or "ping" or "tools/list" or "tools/call" or "initialize";

    private Task<string?> Dispatch(JsonElement request, CancellationToken cancellationToken) =>
        Dispatch(request, McpRequestContext.ForLegacy(ProtocolVersion), cancellationToken);

    private async Task<string?> Dispatch(
        JsonElement request,
        McpRequestContext protocol,
        CancellationToken cancellationToken)
    {
        if (request.ValueKind != JsonValueKind.Object)
            return Error(default, -32600, "Invalid Request");

        if (!request.TryGetProperty("jsonrpc", out JsonElement jsonrpc) ||
            jsonrpc.ValueKind != JsonValueKind.String ||
            jsonrpc.GetString() != "2.0")
        {
            return Error(default, -32600, "Invalid Request");
        }

        bool hasId = request.TryGetProperty("id", out JsonElement id);
        if (hasId && !IsValidId(id))
            return Error(default, -32600, "Invalid Request");

        if (!request.TryGetProperty("method", out JsonElement methodElement) ||
            methodElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(methodElement.GetString()))
        {
            if (request.TryGetProperty("error", out _) || hasId && request.TryGetProperty("result", out _))
                return null;
            return Error(hasId ? id : default, -32600, "Invalid Request");
        }

        string method = methodElement.GetString()!;
        if (method.StartsWith("notifications/", StringComparison.Ordinal))
            return hasId ? Error(id, -32600, "Invalid Request") : null;

        if (!hasId)
            return null;

        switch (method)
        {
            case "initialize":
                return Result(id, new Dictionary<string, object?>
                {
                    ["protocolVersion"] = NegotiatedProtocolVersion(request),
                    ["capabilities"] = new Dictionary<string, object?>
                    {
                        ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false }
                    },
                    ["serverInfo"] = new Dictionary<string, object?>
                    {
                        ["name"] = "QX Scripter",
                        ["title"] = "QX Scripter",
                        ["version"] = ServerVersion
                    },
                    ["instructions"] = Instructions
                });

            case "server/discover":
                return Result(id, protocol, new Dictionary<string, object?>
                {
                    ["supportedVersions"] = McpProtocol.Supported,
                    ["capabilities"] = new Dictionary<string, object?>
                    {
                        ["tools"] = new Dictionary<string, object?> { ["listChanged"] = false }
                    },
                    ["instructions"] = Instructions,
                    // The tool list only moves when QX itself is rebuilt, so a client may hold this
                    // for an hour instead of asking again before every call.
                    ["ttlMs"] = 3600000,
                    ["cacheScope"] = "public"
                });

            case "ping":
                return Result(id, protocol, new Dictionary<string, object?>());

            case "tools/list":
                return Result(id, protocol, new Dictionary<string, object?>
                {
                    ["tools"] = _tools.Select(ToolDefinition).ToList()
                });

            case "tools/call":
                return await CallTool(id, protocol, request, cancellationToken).ConfigureAwait(false);

            default:
                return hasId ? Error(id, -32601, $"Method not found: {method}") : null;
        }
    }

    private async Task<string> CallTool(
        JsonElement id,
        McpRequestContext protocol,
        JsonElement request,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out JsonElement parameters) || parameters.ValueKind != JsonValueKind.Object)
            return Error(id, -32602, "Invalid params");

        if (!parameters.TryGetProperty("name", out JsonElement nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            return Error(id, -32602, "Invalid params");
        }

        string name = nameElement.GetString()!;
        JsonElement args = parameters.TryGetProperty("arguments", out JsonElement arguments) ? arguments : default;
        if (args.ValueKind != JsonValueKind.Undefined && args.ValueKind != JsonValueKind.Object)
            return Error(id, -32602, "Invalid params");

        McpTool? tool = _tools.FirstOrDefault(t => t.Name == name);
        if (tool is null)
            return Error(id, -32602, $"Unknown tool: {name}");

        IReadOnlyList<string> missing = Config.MissingCapabilities(tool.Capability);
        if (missing.Count > 0)
        {
            return Result(id, protocol, ToolContent(
                $"'{tool.Name}' is disabled: set {string.Join(" and ", missing)} to true in {McpConfig.DefaultPath} and restart QX Scripter.",
                true));
        }

        return await Invoke(id, protocol, tool, args, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> Invoke(
        JsonElement id,
        McpRequestContext protocol,
        McpTool tool,
        JsonElement args,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource scope = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bool abandoned = false;
        try
        {
            int timeout_ms = tool.Timeout?.Invoke(args) ?? 0;
            Task<string> execution = tool.Handler(args, scope.Token);
            if (timeout_ms <= 0)
            {
                string text = await execution.ConfigureAwait(false);
                return Result(id, protocol, ToolContent(tool, text));
            }

            scope.CancelAfter(timeout_ms);
            try
            {
                string text = await execution
                    .WaitAsync(TimeSpan.FromMilliseconds((double)timeout_ms + AbandonGraceMs), cancellationToken)
                    .ConfigureAwait(false);
                return Result(id, protocol, ToolContent(tool, text));
            }
            catch (TimeoutException)
            {
                abandoned = true;
                Abandon(execution, scope);
                return Result(id, protocol, ToolContent(
                    $"'{tool.Name}' exceeded its {timeout_ms} ms timeout and did not stop when cancelled. " +
                    "It keeps running in QX Scripter; stop it there or from the editor tab. " +
                    "Cancellation only takes effect where the code awaits or checks Ct.",
                    true));
            }
        }
        catch (OperationCanceledException) when (
            scope.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Result(id, protocol, ToolContent($"'{tool.Name}' was cancelled after its timeout expired.", true));
        }
        catch (McpToolException error)
        {
            return Result(id, protocol, ToolContent(error.Message, true, error.Metadata));
        }
        catch (Exception error)
        {
            return Result(id, protocol, ToolContent(error.Message, true));
        }
        finally
        {
            if (!abandoned)
                scope.Dispose();
        }
    }

    private static void Abandon(Task<string> execution, CancellationTokenSource scope) =>
        _ = execution.ContinueWith(
            static (completed, state) =>
            {
                _ = completed.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            scope,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private static Dictionary<string, object?> ToolContent(
        string text,
        bool isError,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var content = new Dictionary<string, object?>
        {
            ["content"] = new List<object?> { new Dictionary<string, object?> { ["type"] = "text", ["text"] = text } },
            ["isError"] = isError
        };
        if (metadata is { Count: > 0 })
            content["_meta"] = metadata;
        return content;
    }

    private static Dictionary<string, object?> ToolContent(McpTool tool, string text)
    {
        Dictionary<string, object?> content = ToolContent(text, false);
        if (tool.OutputSchema is not null)
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                throw new JsonException($"MCP tool '{tool.Name}' returned a non-object structured result.");
            content["structuredContent"] = document.RootElement.Clone();
        }
        return content;
    }

    private List<McpTool> BuildTools(IEnumerable<McpTool>? additional_tools)
    {
        var tools = new List<McpTool>
        {
        new McpTool
        {
            Name = "run_code",
            Description = "Compile and run a C# script against the QX API (Ext, Room, Session, Send, SendToServer/SendToClient, OnIn/OnOut, ReceiveAsync, Log, Delay). Returns the script output. The run is cancelled after timeout_ms; code that never awaits or checks Ct keeps running and is abandoned.",
            InputSchema = MixedSchema(
                [("code", "string", "C# script code")],
                RunTimeoutProperty()),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute,
            Timeout = RunTimeout,
            Handler = (args, ct) => _host.RunCodeAsync(Str(args, "code"), ct)
        },
        new McpTool
        {
            Name = "send_to_server",
            Description = "Send a packet to the server by message name with the given field values (int/string/bool).",
            InputSchema = SchemaWithValues(("name", "string", "outgoing message name")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.SendToServer(Str(args, "name"), Values(args)))
        },
        new McpTool
        {
            Name = "send_to_client",
            Description = "Send a packet to the client by message name with the given field values (int/string/bool).",
            InputSchema = SchemaWithValues(("name", "string", "incoming message name")),
            Annotations = ClosedDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.SendToClient(Str(args, "name"), Values(args)))
        },
        new McpTool
        {
            Name = "get_connection",
            Description = "Get compact connection readiness: interceptor, hotel session, message catalog and wire profile. Set detail=true for client, host, port, hotel version and the missing wire capabilities.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include endpoint, hotel version and missing wire capabilities", false, null, null)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetConnectionAsync(Bool(args, "detail"), ct)
        },
        new McpTool
        {
            Name = "get_protocol_messages",
            Description = "Inspect the authoritative Flash/Unity message registry, active catalog provenance and exact alias-to-header evidence. Stable semantic MessageKeys are the only supported dependency for features. With explicit_only=false, generated legacy keys are returned as stable=false and key_kind=legacy; they remain migration evidence rather than contracts even when their active aliases resolve. Header IDs come from the immutable active-session catalog and are never embedded in feature code.",
            InputSchema = OptionalSchema(
                ("query", "string", "optional semantic key or client header-name substring", "", null, null),
                ("direction", "string", "in, out, or both", "both", null, null),
                ("client", "string", "flash, unity, or all", "all", null, null),
                ("explicit_only", "boolean", "return only stable semantic keys; false also includes clearly marked legacy evidence", true, null, null),
                ("resolved_only", "boolean", "return only messages resolved for the active hotel session", false, null, null),
                ("limit", "integer", "maximum returned messages", 100, 1, 500),
                OffsetProperty("message")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetProtocolMessagesAsync(
                Str(args, "query"),
                Str(args, "direction", "both"),
                Str(args, "client", "all"),
                Bool(args, "explicit_only", true),
                Bool(args, "resolved_only"),
                BoundedInt(args, "limit", 100, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                ct)
        },
        new McpTool
        {
            Name = "get_room",
            Description = "Get structured room state including lifecycle generation, load state, metadata, floor plan summary, heightmap summary and entity counts. The per-tile floor-plan array and the raw map string are omitted unless detail=true.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include the floor-plan tile array and the raw map string", false, null, null)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetRoomAsync(Bool(args, "detail"), ct)
        },
        new McpTool
        {
            Name = "get_avatars",
            Description = "Get bounded snapshots of the room users, pets and bots with identity, position and live status. Set detail=true for the complete client-shaped snapshot including status fragments.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include complete avatar data", false, null, null),
                ("limit", "integer", "maximum returned avatars", 50, 1, 500),
                OffsetProperty("avatar")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetAvatarsAsync(
                Bool(args, "detail"),
                BoundedInt(args, "limit", 50, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                ct)
        },
        new McpTool
        {
            Name = "get_furni",
            Description = "Get compact floor and wall item snapshots with deduplicated definitions. Set detail=true for complete item data.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include complete item data", false, null, null),
                ("limit", "integer", "maximum floor and wall items per type", 25, 1, 200),
                OffsetProperty("item of each type")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetFurniAsync(
                Bool(args, "detail"),
                BoundedInt(args, "limit", 25, 1, 200),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                ct)
        },
        new McpTool
        {
            Name = "get_profile",
            Description = "Get the logged-in user's complete structured profile snapshot and load state, fetching it when missing by default.",
            InputSchema = FetchSchema(),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetProfileAsync(
                Bool(args, "fetch", true),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_friends",
            Description = "Get a compact messenger friend snapshot with online count and load state, fetching a full baseline when missing by default. Set detail=true for all client fields.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "fetch a complete baseline when the cache is not loaded", true, null, null),
                ("detail", "boolean", "include all client fields", false, null, null),
                ("limit", "integer", "maximum returned friends", 200, 1, 500),
                OffsetProperty("friend"),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetFriendsAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                BoundedInt(args, "limit", 200, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_inventory",
            Description = "Get a compact inventory grouped by client type and kind, fetching a full baseline when missing or stale by default. Set detail=true for complete per-item data. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "fetch a complete baseline when missing or stale before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("detail", "boolean", "include complete per-item data", false, null, null),
                ("limit", "integer", "maximum source items before grouping", 200, 1, 500),
                OffsetProperty("item"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetInventoryAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                BoundedInt(args, "limit", 200, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_badge_inventory",
            Description = "Get a bounded structured badge inventory with native IDs, rarity fields, fragment generation and load state, fetching a complete baseline when missing or stale by default. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "fetch a complete baseline when missing or stale before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("limit", "integer", "maximum returned badges", 100, 1, 500),
                OffsetProperty("badge"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetBadgeInventoryAsync(
                Bool(args, "fetch", true),
                BoundedInt(args, "limit", 100, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_pet_inventory",
            Description = "Get a bounded structured pet inventory with exact Flash and Unity fields, fragment generation and load state, fetching it when missing or stale by default. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "fetch a complete baseline when missing or stale before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("limit", "integer", "maximum returned pets", 50, 1, 200),
                OffsetProperty("pet"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetPetInventoryAsync(
                Bool(args, "fetch", true),
                BoundedInt(args, "limit", 50, 1, 200),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_achievements",
            Description = "Get a bounded structured achievement progress snapshot with category, level, reward and load metadata, fetching it when missing by default. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "fetch missing state before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("limit", "integer", "maximum returned achievements", 50, 1, 500),
                OffsetProperty("achievement"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetAchievementsAsync(
                Bool(args, "fetch", true),
                BoundedInt(args, "limit", 50, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "list_scripts",
            Description = "List saved script names.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(string.Join("\n", _host.ListScripts()))
        },
        new McpTool
        {
            Name = "get_script",
            Description = "Get the code of a saved script by name.",
            InputSchema = Schema(("name", "string", "script name")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetScript(Str(args, "name")))
        },
        new McpTool
        {
            Name = "read_script",
            Description =
                "Read a script with line numbers, all of it or a slice. " + ScriptTargetSentence() +
                " 'around' centres on one line " +
                "with 'context' lines either side, which is what a compiler diagnostic points at.",
            InputSchema = ScriptTargetSchema(
                [],
                ("offset", "integer", "one-based first line", 1, 1, 1000000),
                ("limit", "integer", $"how many lines (max {McpScriptEditing.MaxLines})", 400, 1, McpScriptEditing.MaxLines),
                ("around", "integer", "centre on this line instead of using offset", null, 1, 1000000),
                ("context", "integer", "lines either side of 'around'", 20, 0, 400)),
            Annotations = ClosedReadOnly,
            Handler = async (args, ct) =>
            {
                string code = await CodeOf(args, ct).ConfigureAwait(false);
                return args.ValueKind == JsonValueKind.Object && args.TryGetProperty("around", out JsonElement around)
                    ? McpScriptEditing.ReadAround(code, around.GetInt32(), Int(args, "context", 20))
                    : McpScriptEditing.Read(code, Int(args, "offset", 1), Int(args, "limit", 400));
            }
        },
        new McpTool
        {
            Name = "outline_script",
            Description =
                "List a script's named parts with their line ranges: its //@ui: directives, its " +
                "functions and types, and the handlers it registers. Use it to jump to a place " +
                "instead of reading the file to find it.",
            InputSchema = ScriptTargetSchema([]),
            Annotations = ClosedReadOnly,
            Handler = async (args, ct) =>
            {
                IReadOnlyList<ScriptRegion> regions = McpScriptEditing.Outline(
                    await CodeOf(args, ct).ConfigureAwait(false));
                if (regions.Count == 0)
                    return "no named parts found";
                return string.Join("\n", regions.Select(r =>
                    r.Line == r.EndLine
                        ? $"{r.Line,5}       {r.Kind,-9} {r.Name}    {r.Signature}"
                        : $"{r.Line,5}-{r.EndLine,-5} {r.Kind,-9} {r.Name}    {r.Signature}"));
            }
        },
        new McpTool
        {
            Name = "get_script_part",
            Description =
                "Get one named part of a script whole - a function, a type or a handler - with its " +
                "line numbers, without reading the rest of the file.",
            InputSchema = ScriptTargetSchema(
                [("part", "string", "the name from outline_script")]),
            Annotations = ClosedReadOnly,
            Handler = async (args, ct) =>
            {
                string code = await CodeOf(args, ct).ConfigureAwait(false);
                string part = Str(args, "part");
                if (McpScriptEditing.Region(code, part) is not { } found)
                    return $"no part named '{part}'; call outline_script to see what there is";
                return $"{found.Region.Kind} '{found.Region.Name}', lines {found.Region.Line}-{found.Region.EndLine}\n{found.Text}";
            }
        },
        new McpTool
        {
            Name = "find_in_script",
            Description =
                "Find every line matching a literal or a regular expression, with the lines around " +
                "it. Searches one script, or every saved script when 'name' is left out and " +
                "'all_scripts' is true.",
            InputSchema = MixedSchema(
                [("pattern", "string", "literal text, or a regular expression when regex is true")],
                ("name", "string", FindTargetParameterDescription(), null, null, null),
                ("all_scripts", "boolean", "search every saved script instead", false, null, null),
                ("regex", "boolean", "treat the pattern as a regular expression", false, null, null),
                ("ignore_case", "boolean", "match without regard to case", false, null, null),
                ("context", "integer", "lines either side of each hit", 0, 0, 20)),
            Annotations = ClosedReadOnly,
            Handler = async (args, ct) =>
            {
                string pattern = Str(args, "pattern");
                bool regex = Bool(args, "regex");
                bool ignoreCase = Bool(args, "ignore_case");
                int context = Int(args, "context", 0);

                if (!Bool(args, "all_scripts"))
                {
                    string code = await CodeOf(args, ct).ConfigureAwait(false);
                    IReadOnlyList<ScriptHit> hits = McpScriptEditing.Find(code, pattern, regex, ignoreCase, context);
                    return hits.Count == 0
                        ? "no match"
                        : string.Join("\n", hits.Select(h => $"{h.Line,5}  {h.Text}"));
                }

                var found = new List<string>();
                foreach (string script in _host.ListScripts())
                {
                    IReadOnlyList<ScriptHit> hits = McpScriptEditing.Find(
                        _host.GetScript(script), pattern, regex, ignoreCase, context);
                    foreach (ScriptHit hit in hits)
                        found.Add($"{script}:{hit.Line}  {hit.Text}");
                    if (found.Count >= McpScriptEditing.MaxHits)
                        break;
                }
                return found.Count == 0 ? "no match" : string.Join("\n", found);
            }
        },
        new McpTool
        {
            Name = "patch_script",
            Description =
                "Change parts of a script in place instead of resending the whole file. Each edit " +
                "replaces exact text, indentation included. An edit that matches nothing, or " +
                "matches several places without 'all', is refused and nothing is written - so a " +
                "half-applied file is never stored. " + ScriptTargetSentence(),
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["name"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["description"] = ScriptTargetParameterDescription()
                    },
                    ["edits"] = new Dictionary<string, object?>
                    {
                        ["type"] = "array",
                        ["description"] = "the replacements, applied in order",
                        ["minItems"] = 1,
                        ["items"] = new Dictionary<string, object?>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object?>
                            {
                                ["old"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "string",
                                    ["description"] = "the exact text to replace"
                                },
                                ["new"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "string",
                                    ["description"] = "what it becomes; empty deletes it"
                                },
                                ["all"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "boolean",
                                    ["description"] = "replace every occurrence rather than refusing an ambiguous one",
                                    ["default"] = false
                                }
                            },
                            ["required"] = new[] { "old", "new" }
                        }
                    }
                },
                ["required"] = EditorAvailable ? new[] { "edits" } : ["name", "edits"]
            },
            Annotations = ClosedWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => PatchScript(args, ct)
        },
        new McpTool
        {
            Name = "replace_script_lines",
            Description =
                "Replace a run of lines by number, which is the fast path from a compiler " +
                "diagnostic. Set 'last' to 'first' - 1 to insert before that line, and leave " +
                "'code' empty to delete the run.",
            InputSchema = ScriptTargetSchema(
                [("first", "integer", "one-based first line to replace")],
                ("last", "integer", "one-based last line; first - 1 inserts", null, 0, 1000000),
                ("code", "string", "what goes there; empty deletes the run", "", null, null)),
            Annotations = ClosedWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => ReplaceScriptLines(args, ct)
        },
        new McpTool
        {
            Name = "save_script",
            Description = "Save a script by name with the given code.",
            InputSchema = Schema(("name", "string", "script name"), ("code", "string", "C# script code")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => Task.FromResult(_host.SaveScript(Str(args, "name"), Str(args, "code")))
        },
        new McpTool
        {
            Name = "run_script",
            Description = "Run a saved script by name and return its output. The run is cancelled after timeout_ms.",
            InputSchema = MixedSchema(
                [("name", "string", "script name")],
                RunTimeoutProperty()),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute,
            Timeout = RunTimeout,
            Handler = (args, ct) => _host.RunScriptAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_room_data",
            Description = "Get the current room's metadata: name, owner, description, rating, tags, group and active event.",
            InputSchema = Schema(),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetRoomData())
        },
        new McpTool
        {
            Name = "get_avatar",
            Description = "Get a room user's full live state by name: position, facing, gender, group, stance, rights, dance, effect, hand item, idle and typing.",
            InputSchema = Schema(("name", "string", "user name")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetAvatar(Str(args, "name")))
        },
        new McpTool
        {
            Name = "say",
            Description = "Say a message in the room chat.",
            InputSchema = Schema(("message", "string", "chat message")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.Say(Str(args, "message")))
        },
        new McpTool
        {
            Name = "shout",
            Description = "Shout a message in the room.",
            InputSchema = Schema(("message", "string", "chat message")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.Shout(Str(args, "message")))
        },
        new McpTool
        {
            Name = "walk",
            Description = "Walk the avatar to a tile.",
            InputSchema = Schema(("x", "integer", "tile x"), ("y", "integer", "tile y")),
            Annotations = OpenWrite,
            Handler = (args, ct) => Task.FromResult(_host.Walk(Int(args, "x"), Int(args, "y")))
        },
        new McpTool
        {
            Name = "wave",
            Description = "Perform the wave action.",
            InputSchema = Schema(),
            Annotations = OpenWrite,
            Handler = (args, ct) => Task.FromResult(_host.Wave())
        },
        new McpTool
        {
            Name = "dance",
            Description = "Start a dance (1-4) or stop dancing (0).",
            InputSchema = Schema(("style", "integer", "dance id 0-4")),
            Annotations = OpenWrite,
            Handler = (args, ct) => Task.FromResult(_host.Dance(Int(args, "style")))
        },
        new McpTool
        {
            Name = "sign",
            Description = "Hold up a hand sign (0-17).",
            InputSchema = Schema(("sign", "integer", "sign number")),
            Annotations = OpenWrite,
            Handler = (args, ct) => Task.FromResult(_host.Sign(Int(args, "sign")))
        },
        new McpTool
        {
            Name = "get_user_profile",
            Description = "Fetch another user's extended profile by id (name, motto, created, level, achievement points, gems, friends, badges, groups, online status).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetUserProfileAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_group",
            Description = "Fetch a Habbo group's details by id (name, owner, member count, description).",
            InputSchema = Schema(("id", "id", "group id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetGroupAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_badges",
            Description = "Fetch a user's worn/selected badges by id (resolved to display names).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetBadgesAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_relationship",
            Description = "Fetch a user's relationship stats by id (hearts/smiles/skulls counts).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetRelationshipAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "search_user",
            Description = "Search for a user by name (returns id, motto, online status).",
            InputSchema = Schema(("name", "string", "user name")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.SearchUserAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_sticky",
            Description = "Read a post-it / sticky note's text by furni item id.",
            InputSchema = Schema(("id", "id", "wall item id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetStickyAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_pet_info",
            Description = "Fetch a pet's full details by pet id (name, breed, level, xp, energy, happiness, scratches, owner).",
            InputSchema = Schema(("id", "id", "pet id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetPetInfoAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_room_settings",
            Description = "Fetch a room's full settings by room id (name, description, door mode, category, visitor limits, trade mode, tags, pet/walkthrough/wall flags). Requires room ownership.",
            InputSchema = Schema(("id", "id", "room id")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetRoomSettingsAsync(Long(args, "id"), ct)
        },
        new McpTool
        {
            Name = "get_controllers",
            Description = "Get the bounded room-controller snapshot and its load state.",
            InputSchema = OptionalSchema(
                ("limit", "integer", "maximum returned controllers", 100, 1, 500),
                OffsetProperty("controller")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetControllersAsync(
                BoundedInt(args, "limit", 100, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                ct)
        },
        new McpTool
        {
            Name = "get_currencies",
            Description = "Get structured currency balances with independent credits and activity-points load state. Missing credits are fetched by default; activity points remain server-pushed.",
            InputSchema = FetchSchema(),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetCurrenciesAsync(
                Bool(args, "fetch", true),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_heightmap",
            Description = "Get compact live heightmap dimensions and walkability metrics. Set detail=true for raw decoded tiles.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include raw decoded tiles", false, null, null),
                ("limit", "integer", "maximum tiles in detailed output", 512, 1, 4096),
                OffsetProperty("tile")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetHeightmapAsync(
                Bool(args, "detail"),
                BoundedInt(args, "limit", 512, 1, 4096),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                ct)
        },
        new McpTool
        {
            Name = "get_forums",
            Description = "Get the cached group-forum list with unread counts and per-forum thread and message totals. Fetching requests the matching page of the chosen list first; forums are a Flash-only subsystem. Set detail=true for the cached permissions and moderation flags of the returned forums. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "request the matching forum page before returning", true, null, null),
                ("detail", "boolean", "include cached forum details and permissions", false, null, null),
                ("list", "string", "which list to request: my, active or popular", "my", null, null),
                ("limit", "integer", "maximum returned forums", 50, 1, 200),
                OffsetProperty("forum"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetForumsAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                Str(args, "list"),
                BoundedInt(args, "limit", 50, 1, 200),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_forum_threads",
            Description = "Get the cached threads of one group forum with author, sticky, locked, hidden and unread state, plus the forum's own permissions. Fetching requests the matching thread page first. Set detail=true for the complete client-shaped threads. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = MixedSchema(
                [("group_id", "id", "group id owning the forum")],
                ("fetch", "boolean", "request the matching thread page before returning", true, null, null),
                ("detail", "boolean", "include complete thread data", false, null, null),
                ("limit", "integer", "maximum returned threads", 20, 1, 100),
                OffsetProperty("thread"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetForumThreadsAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                Long(args, "group_id"),
                BoundedInt(args, "limit", 20, 1, 100),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_quests",
            Description = "Get the available and seasonal quests with campaign progress, plus the active quest, the daily quest and the last completion or cancellation. Fetching refreshes both quest-list routes before capturing one combined snapshot. Set detail=true for the complete client-shaped quest data. Continuation pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "refresh both quest lists before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("detail", "boolean", "include complete quest data", false, null, null),
                ("limit", "integer", "maximum returned quests", 50, 1, 200),
                OffsetProperty("quest"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetQuestsAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                BoundedInt(args, "limit", 50, 1, 200),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_crafting",
            Description = "Get the latest observed craftable products, bounded prefixes of the latest observed recipe ingredients and usable furniture classes, the latest observed craft result and the available-recipe count. Pass furni_id to dispatch a products request and wait for the first fresh products-route response; furni_id is request metadata because the response does not echo it. Without furni_id only application state is read. Continuation product pages require the snapshot_revision returned by the first page.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "dispatch a products request and wait for the first fresh products-route response before the first page; ignored when snapshot_revision is supplied", true, null, null),
                ("furni_id", "id", "crafting table id used as request metadata; the response does not echo it", null, null, null),
                ("limit", "integer", "maximum returned products", 50, 1, 200),
                OffsetProperty("product"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetCraftingAsync(
                Bool(args, "fetch", true),
                Long(args, "furni_id", 0),
                BoundedInt(args, "limit", 50, 1, 200),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_subscriptions",
            Description = "Get the subscription products the session knows about with days remaining, VIP flag and past membership days, plus the Habbo Club kickback and Builders Club state. Fetching requests the named product first.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "request the named product before returning", true, null, null),
                ("product", "string", "subscription product to request", "habbo_club", null, null),
                ("limit", "integer", "maximum returned products", 20, 1, 100),
                OffsetProperty("product"),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetSubscriptionsAsync(
                Bool(args, "fetch", true),
                Str(args, "product"),
                BoundedInt(args, "limit", 20, 1, 100),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "get_gifts",
            Description = "Get the gift wrapping configuration, selectable club gift offers, last opened present and cached per-offer giftability answers. Fetching refreshes wrapping and club gifts on one session. Continuation pages require the snapshot_revision returned by the first page; detail responses use an explicit bounded nested-product budget.",
            InputSchema = OptionalSchema(
                ("fetch", "boolean", "request the gift configuration before returning", true, null, null),
                ("detail", "boolean", "include bounded club gift offer details and truncation metadata", false, null, null),
                ("limit", "integer", "maximum returned club gift offers", 25, 1, 100),
                OffsetProperty("club gift offer"),
                ("snapshot_revision", "integer", "snapshot revision returned by the first page; required when offset is greater than zero", null, 1, null),
                ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetGiftsAsync(
                Bool(args, "fetch", true),
                Bool(args, "detail"),
                BoundedInt(args, "limit", 25, 1, 100),
                BoundedInt(args, "offset", 0, 0, MaxOffset),
                OptionalPositiveLong(args, "snapshot_revision"),
                BoundedInt(args, "timeout_ms", 10000, 1, 120000),
                ct)
        },
        new McpTool
        {
            Name = "kick",
            Description = "Kick a user from the room by id (needs rights).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.Kick(Long(args, "id")))
        },
        new McpTool
        {
            Name = "mute",
            Description = "Mute a user in the room for N minutes (needs rights).",
            InputSchema = Schema(("id", "id", "user id"), ("minutes", "integer", "mute minutes")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.Mute(Long(args, "id"), Int(args, "minutes")))
        },
        new McpTool
        {
            Name = "ban",
            Description = "Ban a user from the room by id (needs rights).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.Ban(Long(args, "id")))
        },
        new McpTool
        {
            Name = "give_rights",
            Description = "Give room controller rights to a user by id (room owner only).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.GiveRights(Long(args, "id")))
        },
        new McpTool
        {
            Name = "remove_rights",
            Description = "Remove room controller rights from a user by id (room owner only).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.RemoveRights(Long(args, "id")))
        },
        new McpTool
        {
            Name = "let_in",
            Description = "Let a knocking user into a doorbell room by name (needs rights).",
            InputSchema = Schema(("name", "string", "user name")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.LetIn(Str(args, "name")))
        },
        new McpTool
        {
            Name = "respect_pet",
            Description = "Give respect/treat to a pet by id.",
            InputSchema = Schema(("id", "id", "pet id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.RespectPet(Long(args, "id")))
        },
        new McpTool
        {
            Name = "delete_script",
            Description = "Delete a saved script by name.",
            InputSchema = Schema(("name", "string", "script name")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => Task.FromResult(_host.DeleteScript(Str(args, "name")))
        },
        new McpTool
        {
            Name = "rename_script",
            Description = "Rename a saved script.",
            InputSchema = Schema(("name", "string", "current name"), ("new_name", "string", "new name")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => Task.FromResult(_host.RenameScript(Str(args, "name"), Str(args, "new_name")))
        },
        new McpTool
        {
            Name = "search_scripts",
            Description = "Search saved script names by substring.",
            InputSchema = Schema(("query", "string", "search text")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(string.Join("\n", _host.SearchScripts(Str(args, "query"))))
        },
        new McpTool
        {
            Name = "list_api",
            Description = "List the QX scripting API (all ScriptGlobals properties + methods a script can call), optionally filtered by substring.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["filter"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "optional name filter" } },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.ListApi(Str(args, "filter")))
        },
        new McpTool
        {
            Name = "list_libraries",
            Description = "List the exact QX assemblies available to scripts with their versions and exported type counts.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.ListLibraries())
        },
        new McpTool
        {
            Name = "search_types",
            Description = "Search script-visible classes, interfaces, structs, enums and delegates by simple or fully qualified name.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "optional type-name substring" },
                    ["assembly"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "optional assembly-name filter" },
                    ["limit"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "maximum results, defaults to 50" },
                    ["offset"] = new Dictionary<string, object?>
                    {
                        ["type"] = "integer",
                        ["description"] = "zero-based index of the first returned type",
                        ["default"] = 0,
                        ["minimum"] = 0,
                        ["maximum"] = MaxOffset
                    }
                },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.SearchTypes(
                Str(args, "query"),
                Str(args, "assembly"),
                Int(args, "limit"),
                BoundedInt(args, "offset", 0, 0, MaxOffset)))
        },
        new McpTool
        {
            Name = "get_type",
            Description = "Get the complete script-visible definition of a type, including inheritance, interfaces and all public member signatures.",
            InputSchema = Schema(("name", "string", "simple or fully qualified type name")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetTypeInfo(Str(args, "name")))
        },
        new McpTool
        {
            Name = "search_members",
            Description = "Search public script-visible properties, methods, events and fields across all referenced QX types.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "member name, signature or declaring type" },
                    ["kind"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "optional property, method, event or field filter" },
                    ["limit"] = new Dictionary<string, object?> { ["type"] = "integer", ["description"] = "maximum results, defaults to 60" },
                    ["offset"] = new Dictionary<string, object?>
                    {
                        ["type"] = "integer",
                        ["description"] = "zero-based index of the first returned member",
                        ["default"] = 0,
                        ["minimum"] = 0,
                        ["maximum"] = MaxOffset
                    }
                },
                ["required"] = new[] { "query" }
            },
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.SearchMembers(
                Str(args, "query"),
                Str(args, "kind"),
                Int(args, "limit"),
                BoundedInt(args, "offset", 0, 0, MaxOffset)))
        },
        new McpTool
        {
            Name = "get_scripting_guide",
            Description = "Get a concise guide to the QX scripting API, including the //@ui: panel UI grammar, for writing scripts.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetScriptingGuide())
        },
        new McpTool
        {
            Name = "compile_check",
            Description = "Compile-check C# script code against the QX API without running it; returns errors/warnings or OK.",
            InputSchema = Schema(("code", "string", "C# script code")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.CompileCheck(Str(args, "code")))
        },
        new McpTool
        {
            Name = "list_tabs",
            Description = "List the open editor tabs (active tab marked *, running/modified flags).",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.ListTabsAsync(ct)
        },
        new McpTool
        {
            Name = "get_active_tab",
            Description = "Get the active editor tab's name and current code.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetActiveTabAsync(ct)
        },
        new McpTool
        {
            Name = "open_tab",
            Description = "Open a saved script by name in a new editor tab.",
            InputSchema = Schema(("name", "string", "saved script name")),
            Annotations = ClosedIdempotentWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.OpenTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "create_tab",
            Description = "Create a new editor tab with the given name and code.",
            InputSchema = Schema(("name", "string", "tab name"), ("code", "string", "C# code")),
            Annotations = ClosedWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.CreateTabAsync(Str(args, "name"), Str(args, "code"), ct)
        },
        new McpTool
        {
            Name = "edit_tab",
            Description = "Replace the active editor tab's code.",
            InputSchema = Schema(("code", "string", "new C# code")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.EditActiveTabAsync(Str(args, "code"), ct)
        },
        new McpTool
        {
            Name = "select_tab",
            Description = "Switch to an open editor tab by name.",
            InputSchema = Schema(("name", "string", "tab name")),
            Annotations = ClosedIdempotentWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.SelectTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "close_tab",
            Description = "Close an open editor tab by name.",
            InputSchema = Schema(("name", "string", "tab name")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.CloseTabByNameAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "run_tab",
            Description = "Run an editor tab's script by name, or the active tab when omitted. The run is cancelled after timeout_ms.",
            InputSchema = OptionalSchema(
                ("name", "string", "tab name (optional)", null, null, null),
                RunTimeoutProperty()),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute | McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Timeout = RunTimeout,
            Handler = (args, ct) => _host.RunActiveTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "stop_tab",
            Description = "Request cancellation of a running editor tab by name, or the active tab when omitted.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "tab name (optional)" } },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.StopActiveTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_output",
            Description = "Get the output text of an editor tab by name (or the active tab).",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "tab name (optional)" } },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabOutputAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_status",
            Description = "Get a structured execution snapshot for an editor tab: state, runtime, output size and error count.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "tab name (optional)" } },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabStatusAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_errors",
            Description = "Get structured compile, runtime and background-task errors for an editor tab, including type and source location.",
            InputSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?> { ["name"] = new Dictionary<string, object?> { ["type"] = "string", ["description"] = "tab name (optional)" } },
                ["required"] = Array.Empty<string>()
            },
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabErrorsAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_server_info",
            Description = "Get this MCP server's identity and policy: version, protocol versions, endpoint, tool count, run-timeout bounds and which capabilities are enabled.",
            InputSchema = Schema(),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(ServerInfoJson())
        },
        new McpTool
        {
            Name = "list_mcp_tools",
            Description = "List every MCP tool this server exposes with its parameters, capability requirement and whether the current configuration allows calling it.",
            InputSchema = OptionalSchema(
                ("filter", "string", "optional name or description substring", null, null, null)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(ToolCatalogJson(Str(args, "filter")))
        }
        };

        if (additional_tools is not null)
            tools.AddRange(additional_tools);

        if (tools.Any(tool => tool is null))
            throw new InvalidOperationException("MCP tools cannot contain null entries.");
        if (tools.Any(tool => string.IsNullOrWhiteSpace(tool.Name)))
            throw new InvalidOperationException("MCP tool names cannot be empty.");

        string[] duplicates = tools
            .GroupBy(tool => tool.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length != 0)
        {
            throw new InvalidOperationException(
                $"MCP tool names must be unique: {string.Join(", ", duplicates)}.");
        }

        return AvailableTools(tools);
    }

    private static Dictionary<string, object?> ToolDefinition(McpTool tool)
    {
        var definition = new Dictionary<string, object?>
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = tool.InputSchema,
            ["annotations"] = new Dictionary<string, object?>
            {
                ["readOnlyHint"] = tool.Annotations.ReadOnlyHint,
                ["destructiveHint"] = tool.Annotations.DestructiveHint,
                ["idempotentHint"] = tool.Annotations.IdempotentHint,
                ["openWorldHint"] = tool.Annotations.OpenWorldHint
            }
        };
        if (!string.IsNullOrWhiteSpace(tool.Title))
            definition["title"] = tool.Title;
        if (tool.OutputSchema is not null)
            definition["outputSchema"] = tool.OutputSchema;
        if (tool.Metadata is not null)
            definition["_meta"] = tool.Metadata;
        return definition;
    }

    private List<McpTool> AvailableTools(List<McpTool> tools) =>
        tools
            .Where(tool => RuntimeSupports(tool.RuntimeCapability))
            .ToList();

    private static object Schema(params (string Name, string Type, string Desc)[] props) =>
        MixedSchema(props);

    private static object OptionalSchema(
        params (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)[] props) =>
        MixedSchema([], props);

    private object ScriptTargetSchema(
        (string Name, string Type, string Desc)[] required,
        params (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)[] optional)
    {
        if (EditorAvailable)
        {
            var available = new (string, string, string, object?, int?, int?)[optional.Length + 1];
            available[0] = ("name", "string", ScriptTargetParameterDescription(), null, null, null);
            optional.CopyTo(available, 1);
            return MixedSchema(required, available);
        }

        var named = new (string, string, string)[required.Length + 1];
        required.CopyTo(named, 0);
        named[^1] = ("name", "string", ScriptTargetParameterDescription());
        return MixedSchema(named, optional);
    }

    private string ScriptTargetSentence() =>
        EditorAvailable
            ? "Give 'name' for a saved script or leave it out for the active editor tab."
            : "Give 'name' for the saved script. This runtime has no editor tab fallback.";

    private string ScriptTargetParameterDescription() =>
        EditorAvailable
            ? "saved script name; omit for the active tab"
            : "saved script name";

    private string FindTargetParameterDescription() =>
        EditorAvailable
            ? "saved script name; omit for the active tab"
            : "saved script name; required unless all_scripts is true";

    private static object MixedSchema(
        (string Name, string Type, string Desc)[] required,
        params (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)[] optional)
    {
        var properties = new Dictionary<string, object?>();
        foreach ((string name, string type, string desc) in required)
            properties[name] = RequiredProperty(type, desc);

        foreach ((string name, string type, string desc, object? default_value, int? minimum, int? maximum) in optional)
        {
            Dictionary<string, object?> property = RequiredProperty(type, desc);
            if (default_value is not null)
                property["default"] = default_value;
            if (minimum.HasValue)
                property["minimum"] = minimum.Value;
            if (maximum.HasValue)
                property["maximum"] = maximum.Value;
            properties[name] = property;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required.Select(x => x.Name).ToArray()
        };
    }

    private static Dictionary<string, object?> RequiredProperty(string type, string description) =>
        type == "id"
            ? new Dictionary<string, object?>
            {
                ["oneOf"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "integer" },
                    new Dictionary<string, object?>
                    {
                        ["type"] = "string",
                        ["pattern"] = "^-?[0-9]+$"
                    }
                },
                ["description"] = description
            }
            : new Dictionary<string, object?> { ["type"] = type, ["description"] = description };

    private static (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)
        OffsetProperty(string subject) =>
        ("offset", "integer", $"zero-based index of the first returned {subject}", 0, 0, MaxOffset);

    private static (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)
        RunTimeoutProperty() =>
        (
            "timeout_ms",
            "integer",
            $"cancel the run after this many milliseconds ({MinRunTimeoutMs}-{MaxRunTimeoutMs})",
            DefaultRunTimeoutMs,
            MinRunTimeoutMs,
            MaxRunTimeoutMs);

    private static int RunTimeout(JsonElement args) =>
        BoundedInt(args, "timeout_ms", DefaultRunTimeoutMs, MinRunTimeoutMs, MaxRunTimeoutMs);

    private static object FetchSchema() =>
        OptionalSchema(
            ("fetch", "boolean", "fetch missing state before returning", true, null, null),
            ("timeout_ms", "integer", "fetch timeout in milliseconds", 10000, 1, 120000));

    private static object SchemaWithValues((string Name, string Type, string Desc) first)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                [first.Name] = new Dictionary<string, object?> { ["type"] = first.Type, ["description"] = first.Desc },
                ["values"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["description"] = "field values in order (int/string/bool)",
                    ["items"] = new Dictionary<string, object?>()
                }
            },
            ["required"] = new[] { first.Name }
        };
    }

    /// <summary>
    /// The script an editing tool was pointed at: a saved one by name, or the active editor tab.
    /// </summary>
    /// <remarks>
    /// The tab's code comes back behind its name and a rule, so it is taken from after the rule
    /// rather than parsed; an editor that is not up says so instead of returning something that
    /// would then be written back over the file.
    /// </remarks>
    private async Task<string> CodeOf(JsonElement args, CancellationToken cancellationToken)
    {
        string name = Str(args, "name");
        if (name.Length > 0)
            return _host.GetScript(name);

        if (!EditorAvailable)
            throw new ArgumentException("'name' is required because this runtime has no editor.");

        string tab = await _host.GetActiveTabAsync(cancellationToken).ConfigureAwait(false);
        const string rule = "\n----\n";
        int at = tab.IndexOf(rule, StringComparison.Ordinal);
        if (at < 0)
            throw new InvalidOperationException(tab);
        return tab[(at + rule.Length)..];
    }

    private async Task<string> WriteCode(JsonElement args, string code, CancellationToken cancellationToken)
    {
        string name = Str(args, "name");
        if (name.Length > 0)
            return _host.SaveScript(name, code);

        // Editing the open tab is a different permission from writing a file, and the tool can only
        // declare one, so the second is checked where the target is known.
        IReadOnlyList<string> missing = Config.MissingCapabilities(McpCapability.Editor);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Editing the active tab is disabled: set {string.Join(" and ", missing)} to true in {McpConfig.DefaultPath} and restart QX Scripter.");
        }
        return await _host.EditActiveTabAsync(code, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> PatchScript(JsonElement args, CancellationToken cancellationToken)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty("edits", out JsonElement edits) ||
            edits.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("'edits' must be an array of { old, new }.");
        }

        var requested = new List<(string Old, string New, bool All)>();
        foreach (JsonElement edit in edits.EnumerateArray())
        {
            if (edit.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Every edit must be an object of { old, new }.");
            requested.Add((
                edit.TryGetProperty("old", out JsonElement old) ? old.GetString() ?? "" : "",
                edit.TryGetProperty("new", out JsonElement replacement) ? replacement.GetString() ?? "" : "",
                edit.TryGetProperty("all", out JsonElement all) && all.ValueKind == JsonValueKind.True));
        }

        string code = await CodeOf(args, cancellationToken).ConfigureAwait(false);
        string updated = McpScriptEditing.Patch(code, requested, out IReadOnlyList<string> report);
        string wrote = await WriteCode(args, updated, cancellationToken).ConfigureAwait(false);

        return $"{wrote}\n{string.Join("\n", report)}\n{McpScriptEditing.LineCount(updated)} lines";
    }

    private async Task<string> ReplaceScriptLines(JsonElement args, CancellationToken cancellationToken)
    {
        int first = Int(args, "first");
        string code = await CodeOf(args, cancellationToken).ConfigureAwait(false);
        int last = Int(args, "last", first);
        string updated = McpScriptEditing.ReplaceLines(code, first, last, Str(args, "code"));
        string wrote = await WriteCode(args, updated, cancellationToken).ConfigureAwait(false);

        string what = last < first
            ? $"inserted before line {first}"
            : $"replaced lines {first}-{last}";
        return $"{wrote}\n{what}\n{McpScriptEditing.LineCount(updated)} lines";
    }

    private static string Str(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) ? v.GetString() ?? "" : "";

    private static string Str(JsonElement args, string key, string fallback) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement value)
            ? value.GetString() ?? fallback
            : fallback;

    private static int Int(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out JsonElement v) && v.TryGetInt32(out int i) ? i : 0;

    private static int Int(JsonElement args, string key, int fallback) =>
        args.ValueKind == JsonValueKind.Object &&
        args.TryGetProperty(key, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : fallback;

    private static int BoundedInt(
        JsonElement args,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty(key, out JsonElement value))
        {
            return fallback;
        }
        if (!value.TryGetInt32(out int result))
            throw new ArgumentException($"'{key}' must be an integer.", key);
        if (result < minimum || result > maximum)
            throw new ArgumentOutOfRangeException(key, result, $"'{key}' must be between {minimum} and {maximum}.");
        return result;
    }

    private static bool Bool(JsonElement args, string key, bool fallback = false) =>
        args.ValueKind != JsonValueKind.Object ||
        !args.TryGetProperty(key, out JsonElement value)
            ? fallback
            : value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException($"'{key}' must be a boolean.", key)
            };

    private static long Long(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object ||
            !args.TryGetProperty(key, out JsonElement value))
        {
            throw new ArgumentException($"Missing identifier '{key}'.", key);
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long numeric))
            return numeric;
        if (value.ValueKind == JsonValueKind.String &&
            long.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long text))
        {
            return text;
        }

        throw new ArgumentException(
            $"Identifier '{key}' must be a signed 64-bit decimal integer or string.",
            key);
    }

    private static long Long(JsonElement args, string key, long fallback) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out _)
            ? Long(args, key)
            : fallback;

    private static long? OptionalPositiveLong(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out _))
            return null;
        long value = Long(args, key);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(key, value, $"'{key}' must be greater than zero.");
        return value;
    }

    private static object[] Values(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("values", out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<object>();
        foreach (JsonElement element in arr.EnumerateArray())
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int i)) list.Add(i);
                    else list.Add(element.GetInt64());
                    break;
                case JsonValueKind.String:
                    list.Add(element.GetString()!);
                    break;
                case JsonValueKind.True:
                    list.Add(true);
                    break;
                case JsonValueKind.False:
                    list.Add(false);
                    break;
            }
        }
        return list.ToArray();
    }

    private static string Result(JsonElement id, object result) =>
        JsonSerializer.Serialize(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["id"] = IdValue(id), ["result"] = result });

    /// <summary>
    /// Serialises a result, stamping it the way the request's era expects.
    /// </summary>
    /// <remarks>
    /// A modern result says what kind it is and who answered, because the client is holding no
    /// session to look either up in. A legacy result is left exactly as it was, since an older
    /// client validates against a schema that has no room for the extra fields.
    /// </remarks>
    private static string Result(JsonElement id, McpRequestContext context, Dictionary<string, object?> result)
    {
        if (context.Era is McpEra.Modern)
        {
            result["resultType"] = "complete";
            result["_meta"] = new Dictionary<string, object?>
            {
                [McpProtocol.MetaServerInfo] = new Dictionary<string, object?>
                {
                    ["name"] = "QX Scripter",
                    ["version"] = ServerVersion
                }
            };
        }
        return Result(id, result);
    }

    private static string Error(JsonElement id, int code, string message) =>
        Error(id, code, message, null);

    private static string Error(JsonElement id, int code, string message, object? data)
    {
        var error = new Dictionary<string, object?> { ["code"] = code, ["message"] = message };
        if (data is not null)
            error["data"] = data;
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = IdValue(id),
            ["error"] = error
        });
    }

    private static bool IsValidId(JsonElement id) => id.ValueKind switch
    {
        JsonValueKind.Number => id.TryGetInt64(out _),
        JsonValueKind.String => true,
        _ => false
    };

    private static object? IdValue(JsonElement id)
    {
        if (id.ValueKind == JsonValueKind.String)
            return id.GetString();
        if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out long value))
            return value;
        return null;
    }

    private static string NegotiatedProtocolVersion(JsonElement request)
    {
        if (request.TryGetProperty("params", out JsonElement parameters) &&
            parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("protocolVersion", out JsonElement requested) &&
            requested.ValueKind == JsonValueKind.String &&
            SupportedProtocolVersions.Contains(requested.GetString()!) &&
            // The modern revision has no initialize, so a session can never be opened on it.
            requested.GetString() != McpProtocol.Modern)
        {
            return requested.GetString()!;
        }

        return ProtocolVersion;
    }

    private string ServerInfoJson() =>
        JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["name"] = "QX Scripter",
                ["version"] = ServerVersion,
                ["protocolVersion"] = ProtocolVersion,
                ["supportedProtocolVersions"] = SupportedProtocolVersions
                    .OrderByDescending(version => version, StringComparer.Ordinal)
                    .ToArray(),
                ["endpoint"] = $"http://127.0.0.1:{Port}/mcp",
                ["listening"] = IsRunning,
                ["toolCount"] = _tools.Count,
                ["configPath"] = McpConfig.DefaultPath,
                ["authRequired"] = Config.RequireAuth,
                ["capabilities"] = new Dictionary<string, object?>
                {
                    ["allowExecute"] = Config.AllowExecute,
                    ["allowFileWrite"] = Config.AllowFileWrite,
                    ["allowEditor"] = Config.AllowEditor && EditorAvailable
                },
                ["runtimeCapabilities"] = RuntimeCapabilityNames(_host.RuntimeCapabilities),
                ["runTimeoutMs"] = new Dictionary<string, object?>
                {
                    ["default"] = DefaultRunTimeoutMs,
                    ["minimum"] = MinRunTimeoutMs,
                    ["maximum"] = MaxRunTimeoutMs
                }
            },
            IndentedJson);

    private string ToolCatalogJson(string filter) =>
        JsonSerializer.Serialize(
            _tools
                .Where(tool =>
                    string.IsNullOrWhiteSpace(filter) ||
                    tool.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    tool.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tool => tool.Name, StringComparer.Ordinal)
                .Select(tool => new Dictionary<string, object?>
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = ParameterNames(tool.InputSchema),
                    ["requires"] = CapabilityNames(tool.Capability),
                    ["runtimeRequires"] = RuntimeCapabilityNames(tool.RuntimeCapability),
                    ["allowed"] = Config.Allows(tool.Capability),
                    ["readOnly"] = tool.Annotations.ReadOnlyHint,
                    ["destructive"] = tool.Annotations.DestructiveHint
                })
                .ToArray(),
            IndentedJson);

    private static string[] CapabilityNames(McpCapability capability) =>
        capability == McpCapability.None
            ? []
            : Enum.GetValues<McpCapability>()
                .Where(value => value != McpCapability.None && capability.HasFlag(value))
                .Select(value => value.ToString())
                .ToArray();

    private bool RuntimeSupports(McpRuntimeCapability capability) =>
        (_host.RuntimeCapabilities & capability) == capability;

    private static string[] RuntimeCapabilityNames(McpRuntimeCapability capability) =>
        capability == McpRuntimeCapability.None
            ? []
            : Enum.GetValues<McpRuntimeCapability>()
                .Where(value => value != McpRuntimeCapability.None && capability.HasFlag(value))
                .Select(value => value.ToString())
                .ToArray();

    private static string[] ParameterNames(object schema)
    {
        if (schema is not Dictionary<string, object?> map ||
            !map.TryGetValue("properties", out object? raw) ||
            raw is not Dictionary<string, object?> properties)
        {
            return [];
        }

        string[] required = map.TryGetValue("required", out object? names) && names is string[] list
            ? list
            : [];
        return properties.Keys
            .Select(name => required.Contains(name, StringComparer.Ordinal) ? name : name + "?")
            .ToArray();
    }

    private static string ResolveVersion()
    {
        Assembly assembly = Assembly.GetEntryAssembly() ?? typeof(McpServer).Assembly;
        string? version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
            version = assembly.GetName().Version?.ToString();
        if (string.IsNullOrWhiteSpace(version))
            return "0.0.0";

        int metadata = version.IndexOf('+');
        return metadata > 0 ? version[..metadata] : version;
    }

    private static string? PresentedToken(HttpListenerRequest request)
    {
        string? header = request.Headers["X-MCP-Token"];
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        string? authorization = request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            const string scheme = "Bearer ";
            if (authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return authorization[scheme.Length..].Trim();
        }

        string? query = request.QueryString["token"];
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
    }

    /// <summary>True when an HTTP Host header names a loopback address, the only accepted target.</summary>
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (!Uri.TryCreate($"http://{host}/", UriKind.Absolute, out Uri? uri))
            return false;

        string name = uri.Host.Trim('[', ']');
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(name, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return true;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static async Task WriteJsonResponse(
        HttpListenerResponse response,
        HttpStatusCode statusCode,
        string json,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        response.Close();
    }

    private static void CloseResponse(HttpListenerResponse response, HttpStatusCode statusCode)
    {
        response.StatusCode = (int)statusCode;
        response.ContentLength64 = 0;
        response.Close();
    }
}
