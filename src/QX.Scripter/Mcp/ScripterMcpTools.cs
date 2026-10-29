using System.Text.Json;
using Qx.Game.Messages;
using Qx.Mcp;
using static Qx.Mcp.McpArguments;
using static Qx.Mcp.McpSchema;

namespace Qx.Scripter;

internal sealed class ScripterMcpTools(IMcpHost host, Func<McpConfig> config)
{
    private const int DefaultRunTimeoutMs = 30000;
    private const int MinRunTimeoutMs = 1000;
    private const int MaxRunTimeoutMs = 600000;
    private const int MaxOffset = 1000000;
    private readonly IMcpHost _host = host;
    private McpConfig Config => config();
    private bool EditorAvailable => (_host.RuntimeCapabilities & McpRuntimeCapability.Editor) != 0;
    public string Instructions => EditorAvailable ? CoreInstructions + EditorInstructions : CoreInstructions;
    private const string CoreInstructions =
        """
        QX Scripter drives a live Habbo client. It attaches to a Flash session through a
        G-Earth interceptor, mirrors the whole game state, and hosts a C# scripting runtime that runs
        inside the QX process.

        EDITING SCRIPTS
        Saved scripts belong to QX. Create, read, change and run them through these tools first; edit
        .csx files on disk only when no tool can do the job, and never copy or concatenate scripts
        into the library. The tools edit an open tab live and keep the editor, the library and
        running panels in step. Test and debug code runs with run_code, which leaves no file and no
        tab behind (background=true, get_run and stop_run for longer runs); save a script only when
        it is meant to stay. Find code with outline_script,
        find_in_script or read_script, then change it with patch_script (exact text; line endings do
        not matter) or replace_script_lines (by line number). save_script writes a script whole, for
        new scripts. A tab without unsaved changes is saved at once, so never save, close and reopen.
        Run with run_tab, which the user can watch and stop_tab can end, or with run_script.

        FINDING TOOLS
        tools/list holds the everyday set. Several hundred more game operations (catalog,
        marketplace, trade, wired, navigator, forums, quests, badges and more) are named
        application_*: search them with list_mcp_tools, read one schema with describe_mcp_tool, and
        call them with call_mcp_tool, or read_mcp_tool for read-only ones. Both keep the target's
        permissions and timeout and return its output in result.

        ORIENT
        get_server_info reports the running version, the negotiated protocol and which capabilities
        this server is currently allowed to use. get_connection reports whether the interceptor and
        the hotel session are live; nearly every game tool returns empty data until they are.

        SCRIPTS
        Scripts are C# (Roslyn scripting, top-level statements). Every public member of ScriptGlobals is
        in scope as a top-level symbol: Room, Users, FloorItems, Send, OnIn/OnOut, Delay, Log and more.
        get_scripting_guide lists the guide's topics and returns any page, or every guide page with "all".
        list_api without a filter names the member groups and starts a paged list of that surface;
        with a filter it returns the exact C# signatures of the matching members, name matches first.
        search_types and search_members find the model types and members behind it and page with
        nextOffset; every result says whether its namespace is imported or which using it needs.
        Before Wired construction, configuration or repair, call get_wired_knowledge with no path.
        It returns essential defaults, required starting documents and an index into the bundled
        qx-wired skill: SKILL.md is its guide, references/catalog/overview.md lists every Wired form
        by type and references/ holds the documents. Read the relevant rules, source-slot
        dependencies and execution limits. search_wired_knowledge finds furniture
        classes, variables and patterns; list_wired_knowledge browses the skill. get_wired_knowledge
        reads a document or JSON pointer in bounded pages. Follow next_offset until the required
        material is complete. No local files are required.
        get_type returns one type with its documentation; a type with more than 150 members lists
        only their names, and member picks the members to read. compile_check compiles a script
        without running it and also reports unknown message names (QX1001), message types a message
        is not parsed into (QX1002), unknown //@ui directives (QX1004) and Ui calls on undeclared
        panel controls (QX1005). run_code and run_script take timeout_ms and are abandoned when it
        expires, so never loop without checking Ct. get_protocol_messages maps a Flash message name
        to the model type OnIn<T>/OnOut<T> parse it into, and a model name back to its messages.

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
        list_application_members pages through every application query, operation and event in
        compact rows with their application_* tool and live availability; detail=true adds the full
        schemas. describe_application_member takes a member id or tool name and returns its
        parameters, message evidence and the C# script call that reaches it.
        """;
    private const string EditorInstructions =
        """

        EDITOR
        Editor tools expose open tabs, their current source, execution state, output and diagnostics.
        list_tabs shows each tab's file and whether it has unsaved changes. A script can give its tab
        a panel of inputs, inline buttons and output boxes with //@ui: directives and drive it through
        Ui. run_tab starts the tab and returns at once; follow it with get_tab_status and
        get_tab_output.
        """;
    private static readonly McpToolAnnotations ClosedReadOnly = new(true, false, false, false);
    private static readonly McpToolAnnotations OpenReadOnly = new(true, false, false, true);
    private static readonly McpToolAnnotations ClosedWrite = new(false, false, false, false);
    private static readonly McpToolAnnotations ClosedIdempotentWrite = new(false, false, true, false);
    private static readonly McpToolAnnotations ClosedDestructiveWrite = new(false, true, false, false);
    private static readonly McpToolAnnotations ClosedIdempotentDestructiveWrite = new(false, true, true, false);
    private static readonly McpToolAnnotations OpenDestructiveWrite = new(false, true, false, true);

    public IReadOnlyList<McpTool> Create() => new List<McpTool>
    {
        new McpTool
        {
            Name = "run_code",
            Description =
                "Compile and run C# against the QX API (Room, Users, Send, OnIn/OnOut, Log, Delay and the rest of " +
                "ScriptGlobals) without saving it or opening a tab, which makes it the tool for test and debug code. " +
                "Returns the output once the run ends; it is cancelled after timeout_ms, and code that never awaits " +
                "or checks Ct keeps running and is abandoned. " + BackgroundSentence,
            InputSchema = MixedSchema(
                [("code", "string", "C# script code")],
                RunTimeoutProperty(),
                BackgroundProperty()),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute,
            Timeout = RunTimeout,
            Handler = (args, ct) => Bool(args, "background")
                ? Task.FromResult(_host.StartCode(Str(args, "code"), OptionalRunTimeout(args)))
                : _host.RunCodeAsync(Str(args, "code"), ct)
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
            Description = "Get compact connection readiness: interceptor, hotel session, message catalog and wire profile. Set detail=true for host, port, hotel version and the missing wire capabilities.",
            InputSchema = OptionalSchema(
                ("detail", "boolean", "include endpoint, hotel version and missing wire capabilities", false, null, null)),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetConnectionAsync(Bool(args, "detail"), ct)
        },
        new McpTool
        {
            Name = "get_protocol_messages",
            Description = "Look up Flash messages in the message registry. Each entry gives the stable key, the Flash names, the model type the message parses into (model), the MessageKeys member (key_member), the MessageContracts member (contract) and the headers the active session catalog resolves it to. query matches keys, Flash names and model type names, so AvatarChat finds Chat, Shout and Whisper. Scripts hook and send by Flash name with the model as the type, as in OnIn<FloorItemUpdate>(\"ObjectUpdate\", ...), and the typed SendToServer/SendToClient take the key_member. QX feature and extension code depends on keys and contracts, never on header IDs. explicit_only=false adds generated legacy keys (stable=false, key_kind=legacy) and unmapped catalog headers; scripts can still hook them by name, but they carry no model.",
            InputSchema = OptionalSchema(
                ("query", "string", "optional substring of a key, Flash name or model type name", "", null, null),
                ("direction", "string", "in, out, or both", "both", null, null),
                ("explicit_only", "boolean", "return only stable semantic keys; false also includes clearly marked legacy evidence", true, null, null),
                ("resolved_only", "boolean", "return only messages resolved for the active hotel session", false, null, null),
                ("limit", "integer", "maximum returned messages", 100, 1, 500),
                OffsetProperty("message")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => _host.GetProtocolMessagesAsync(
                Str(args, "query"),
                Str(args, "direction", "both"),
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
            Description = "Get a compact inventory grouped by item type (floor or wall) and kind, fetching a full baseline when missing or stale by default. Set detail=true for complete per-item data. Continuation pages require the snapshot_revision returned by the first page.",
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
            Description = "Get a bounded structured pet inventory with exact Flash fields, fragment generation and load state, fetching it when missing or stale by default. Continuation pages require the snapshot_revision returned by the first page.",
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
            Description = "List saved script names, optionally only those containing 'query'.",
            InputSchema = OptionalSchema(("query", "string", "optional name substring", null, null, null)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(string.Join("\n", Str(args, "query") is { Length: > 0 } query
                ? _host.SearchScripts(query)
                : _host.ListScripts()))
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
                ["required"] = EditorAvailable ? new[] { "edits" } : ["name", "edits"],
                ["additionalProperties"] = false
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
            Description =
                "Write a whole script by name, creating it when it does not exist. When the script is open in the " +
                "editor its tab takes the code too. For changes to an existing script prefer patch_script or " +
                "replace_script_lines, which do not resend the file.",
            InputSchema = Schema(("name", "string", "script name"), ("code", "string", "C# script code")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = async (args, ct) =>
            {
                string name = Str(args, "name");
                string code = Str(args, "code");
                return name.Length > 0 && await _host.EditOpenScriptAsync(name, _ => code, ct).ConfigureAwait(false) is { } done
                    ? done
                    : _host.SaveScript(name, code);
            }
        },
        new McpTool
        {
            Name = "run_script",
            Description =
                "Run a saved script by name without a tab and return its output once it ends; it is cancelled after " +
                "timeout_ms. An open tab's current text is what runs. For panels, and for runs the user should " +
                "watch, prefer run_tab. " + BackgroundSentence,
            InputSchema = MixedSchema(
                [("name", "string", "script name")],
                RunTimeoutProperty(),
                BackgroundProperty()),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute,
            Timeout = RunTimeout,
            Handler = (args, ct) => Bool(args, "background")
                ? _host.StartScriptAsync(Str(args, "name"), OptionalRunTimeout(args), ct)
                : _host.RunScriptAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_run",
            Description =
                "Read a background run started by run_code or run_script: its state, errors and the output lines " +
                "from 'since' on. Pass the returned next as since to read only new lines. Without id, lists the " +
                "background runs.",
            InputSchema = OptionalSchema(
                ("id", "integer", "run id; leave it out to list the runs", null, 1, null),
                ("since", "integer", "first output line to return", 0, 0, null)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.ReadRun(
                OptionalPositiveLong(args, "id"),
                BoundedInt(args, "since", 0, 0, int.MaxValue)))
        },
        new McpTool
        {
            Name = "stop_run",
            Description = "Stop a background run started by run_code or run_script.",
            InputSchema = Schema(("id", "integer", "run id")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Execute,
            Handler = (args, ct) => Task.FromResult(_host.StopRun(Long(args, "id")))
        },
        new McpTool
        {
            Name = "get_avatar",
            Description = "Get a room user's full live state by name: position, facing, gender, group, stance, rights, dance, effect and hand item with their names, idle and typing. data is null when no user of that name is in the room, and metadata.ready and metadata.loaded say whether the room has loaded.",
            InputSchema = Schema(("name", "string", "user name")),
            Annotations = OpenReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetAvatar(Str(args, "name")))
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
            Description = "Get the cached group-forum list with unread counts and per-forum thread and message totals. Fetching requests the matching page of the chosen list first. Set detail=true for the cached permissions and moderation flags of the returned forums. Continuation pages require the snapshot_revision returned by the first page.",
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
            Name = "remove_rights",
            Description = "Remove room controller rights from a user by id (room owner only).",
            InputSchema = Schema(("id", "id", "user id")),
            Annotations = OpenDestructiveWrite,
            Handler = (args, ct) => Task.FromResult(_host.RemoveRights(Long(args, "id")))
        },
        new McpTool
        {
            Name = "delete_script",
            Description = "Delete a saved script by name. A script that is running is refused; an open tab keeps its text as an unsaved tab.",
            InputSchema = Schema(("name", "string", "script name")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => _host.DeleteScriptAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "rename_script",
            Description =
                "Rename a saved script. Its open tab, library group and panel values move with it, and a /// @name " +
                "line follows the new name. An existing script of the new name is never replaced.",
            InputSchema = Schema(("name", "string", "current name"), ("new_name", "string", "new name")),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.FileWrite,
            Handler = (args, ct) => _host.RenameScriptAsync(Str(args, "name"), Str(args, "new_name"), ct)
        },
        new McpTool
        {
            Name = "list_api",
            Description =
                "List the members a script calls by bare name (every public ScriptGlobals property and method, static " +
                "ones included) with their exact C# signatures and a one-line summary. Without a filter it starts with " +
                "the member groups. A filter ranks name matches first, then group, signature and summary matches. The " +
                "last line gives the total and, when more members follow, the nextOffset of the next page.",
            InputSchema = OptionalSchema(
                ("filter", "string", "optional text to look for, matched against member names first", null, null, null),
                ("limit", "integer", "maximum returned members", 100, 1, 500),
                OffsetProperty("member")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.ListApi(
                Str(args, "filter"),
                BoundedInt(args, "limit", 100, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset)))
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
            Description =
                "Search script-visible classes, interfaces, structs, enums and delegates by simple or fully qualified name. " +
                "Returns {items, total, nextOffset}. Exact names rank first; among equal matches ScriptGlobals comes " +
                "first, then imported namespaces, then the rest, and host types come last. Each type says whether it " +
                "is imported or which using it needs. Obsolete types are left out.",
            InputSchema = OptionalSchema(
                ("query", "string", "optional type-name substring", null, null, null),
                ("assembly", "string", "optional assembly-name filter", null, null, null),
                ("limit", "integer", "maximum returned types", 50, 1, 500),
                OffsetProperty("type")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.SearchTypes(
                Str(args, "query"),
                Str(args, "assembly"),
                BoundedInt(args, "limit", 50, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset)))
        },
        new McpTool
        {
            Name = "get_type",
            Description =
                "Get the script-visible definition of a type: inheritance, interfaces, whether it is imported, and every " +
                "public member signature with its documentation and obsolete message. A short name shared with nested " +
                "types resolves to the top-level type and lists the others in alsoMatches. A type with more than 150 " +
                "members lists only their names in memberIndex; pass member to read the members whose name contains it.",
            InputSchema = MixedSchema(
                [("name", "string", "simple or fully qualified type name")],
                ("member", "string", "optional text a member name must contain to be returned", null, null, null)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetTypeInfo(Str(args, "name"), Str(args, "member")))
        },
        new McpTool
        {
            Name = "search_members",
            Description =
                "Search public script-visible properties, methods, events and fields across all referenced QX types. " +
                "Returns {items, total, nextOffset}, ranked like search_types. Each member says whether its type is " +
                "imported or which using it needs. Obsolete members and the methods the compiler generates for " +
                "records are left out; include_generated adds the latter.",
            InputSchema = MixedSchema(
                [("query", "string", "member name, signature or declaring type")],
                ("kind", "string", "optional property, method, event or field filter", null, null, null),
                ("include_generated", "boolean", "include the Equals, GetHashCode, ToString and Deconstruct methods of records", false, null, null),
                ("limit", "integer", "maximum returned members", 60, 1, 500),
                OffsetProperty("member")),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.SearchMembers(
                Str(args, "query"),
                Str(args, "kind"),
                Bool(args, "include_generated"),
                BoundedInt(args, "limit", 60, 1, 500),
                BoundedInt(args, "offset", 0, 0, MaxOffset)))
        },
        new McpTool
        {
            Name = "get_wired_knowledge",
            Description = "Read the bundled qx-wired skill. With no path returns the starting guide and indexes. SKILL.md is the skill guide; a skill-relative path such as references/index/wired.json reads a document, optionally narrowed by an RFC 6901 JSON pointer. Offsets count UTF-16 characters; follow next_offset for remaining content. Available without a hotel connection.",
            InputSchema = OptionalSchema(
                ("path", "string", "Skill-relative document path such as SKILL.md or references/index/wired.json; omit for the starting guide", "", null, null),
                ("pointer", "string", "Optional JSON pointer into the document, e.g. /current_qx or /rules/0", "", null, null),
                ("offset", "integer", "Character offset from a previous response", 0, 0, int.MaxValue),
                ("limit", "integer", "Maximum characters returned", 12000, 1, 32000)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetWiredKnowledge(Str(args, "path"), Str(args, "pointer"), BoundedInt(args, "offset", 0, 0, int.MaxValue), BoundedInt(args, "limit", 12000, 1, 32000)))
        },
        new McpTool
        {
            Name = "list_wired_knowledge",
            Description = "Browse the documents of the bundled qx-wired skill by optional path prefix such as references/notes/. Returns titles, paths and pagination. Read a result with get_wired_knowledge.",
            InputSchema = OptionalSchema(
                ("path", "string", "Optional skill-relative directory or filename prefix", "", null, null),
                ("offset", "integer", "Document offset from a previous response", 0, 0, MaxOffset),
                ("limit", "integer", "Maximum documents returned", 40, 1, 100)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.ListWiredKnowledge(Str(args, "path"), BoundedInt(args, "offset", 0, 0, MaxOffset), BoundedInt(args, "limit", 40, 1, 100)))
        },
        new McpTool
        {
            Name = "search_wired_knowledge",
            Description = "Search the bundled qx-wired skill by furniture classname, variable, source, rule or gameplay pattern. All query terms must match. Returns ranked document paths and excerpts; follow next_offset for additional matches.",
            InputSchema = MixedSchema([("query", "string", "Search terms, at most 512 characters")],
                ("path", "string", "Optional skill-relative directory prefix", "", null, null),
                ("offset", "integer", "Search result offset from a previous response", 0, 0, MaxOffset),
                ("limit", "integer", "Maximum matching documents returned", 15, 1, 50)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.SearchWiredKnowledge(Str(args, "query"), Str(args, "path"), BoundedInt(args, "offset", 0, 0, MaxOffset), BoundedInt(args, "limit", 15, 1, 50)))
        },
        new McpTool
        {
            Name = "get_scripting_guide",
            Description =
                "Read the QX scripting guide, the same pages as the documentation site. Without a topic it lists " +
                "every topic with its summary; with a topic it returns that page, and \"all\" returns every guide page " +
                "without the release notes.",
            InputSchema = OptionalSchema(("topic", "string", "page to read, or \"all\"", null, null, null)),
            Annotations = ClosedReadOnly,
            Handler = (args, ct) => Task.FromResult(_host.GetScriptingGuide(Str(args, "topic")))
        },
        new McpTool
        {
            Name = "compile_check",
            Description =
                "Compile-check C# script code against the QX API without running it, including message names, message types " +
                "and panel directives. Returns OK, or one line per error or warning in source order as \"severity ID " +
                "line:column: message\" (file:line:column inside a #load file); a type it cannot find gets the using or namespace it needs.",
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
            Description = "Replace the whole code of the active editor tab. For changes prefer patch_script or replace_script_lines.",
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
            Description =
                "Close an open editor tab by name. A tab with unsaved changes is refused unless 'discard' is set, " +
                "and a running tab is stopped first.",
            InputSchema = MixedSchema(
                [("name", "string", "tab name")],
                ("discard", "boolean", "drop unsaved changes", false, null, null)),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.CloseTabByNameAsync(Str(args, "name"), Bool(args, "discard"), ct)
        },
        new McpTool
        {
            Name = "run_tab",
            Description =
                "Start an editor tab's script by name, or the active tab when omitted, and return at once. It runs " +
                "the tab's current text and saves the tab first when it has a file. Follow the run with " +
                "get_tab_status and get_tab_output, and end it with stop_tab.",
            InputSchema = OptionalSchema(("name", "string", "tab name (optional)", null, null, null)),
            Annotations = OpenDestructiveWrite,
            Capability = McpCapability.Execute | McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.RunActiveTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "stop_tab",
            Description = "Request cancellation of a running editor tab by name, or the active tab when omitted.",
            InputSchema = OptionalSchema(("name", "string", "tab name (optional)", null, null, null)),
            Annotations = ClosedIdempotentDestructiveWrite,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.StopActiveTabAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_output",
            Description = "Get the output text of an editor tab by name (or the active tab).",
            InputSchema = OptionalSchema(("name", "string", "tab name (optional)", null, null, null)),
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabOutputAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_status",
            Description = "Get a structured execution snapshot for an editor tab: state, runtime, output size and error count.",
            InputSchema = OptionalSchema(("name", "string", "tab name (optional)", null, null, null)),
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabStatusAsync(Str(args, "name"), ct)
        },
        new McpTool
        {
            Name = "get_tab_errors",
            Description = "Get structured compile, runtime and background-task errors for an editor tab, including type and source location.",
            InputSchema = OptionalSchema(("name", "string", "tab name (optional)", null, null, null)),
            Annotations = ClosedReadOnly,
            Capability = McpCapability.Editor,
            RuntimeCapability = McpRuntimeCapability.Editor,
            Handler = (args, ct) => _host.GetTabErrorsAsync(Str(args, "name"), ct)
        },
    };

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
            ? "Give 'name' for a saved script, which is taken live from its tab when it is open, or leave it out for the active editor tab."
            : "Give 'name' for the saved script. This runtime has no editor tab fallback.";

    private string ScriptTargetParameterDescription() =>
        EditorAvailable
            ? "saved script name; omit for the active tab"
            : "saved script name";

    private string FindTargetParameterDescription() =>
        EditorAvailable
            ? "saved script name; omit for the active tab"
            : "saved script name; required unless all_scripts is true";

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

    private static int? OptionalRunTimeout(JsonElement args) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty("timeout_ms", out _) ? RunTimeout(args) : null;

    private const string BackgroundSentence =
        "With background=true it returns a run id at once and keeps running, without a time limit unless " +
        "timeout_ms is given: read it with get_run and end it with stop_run.";

    private static (string Name, string Type, string Desc, object? Default, int? Minimum, int? Maximum)
        BackgroundProperty() =>
        ("background", "boolean", "start the run and return its id at once instead of waiting for it", false, null, null);

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
            ["required"] = new[] { first.Name },
            ["additionalProperties"] = false
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
        if (name.Length == 0 && !EditorAvailable)
            throw new ArgumentException("'name' is required because this runtime has no editor.");
        if (await _host.ReadOpenScriptAsync(name, cancellationToken).ConfigureAwait(false) is { } open)
            return open;
        return name.Length > 0 ? _host.GetScript(name) : throw new InvalidOperationException("no active tab");
    }

    /// <summary>
    /// Applies an edit to the script an editing tool was pointed at. An open tab takes the edit in
    /// one step on the editor's thread, so the user's typing and parallel edits cannot interleave
    /// with it; a script that is not open is edited on disk.
    /// </summary>
    private async Task<string> EditCode(JsonElement args, Func<string, string> edit, CancellationToken cancellationToken)
    {
        string name = Str(args, "name");
        if (name.Length == 0)
        {
            if (!EditorAvailable)
                throw new ArgumentException("'name' is required because this runtime has no editor.");

            // Editing the open tab is a different permission from writing a file, and the tool can
            // only declare one, so the second is checked where the target is known.
            IReadOnlyList<string> missing = Config.MissingCapabilities(McpCapability.Editor);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Editing the active tab is disabled: set {string.Join(" and ", missing)} to true in this extension's MCP settings.");
            }
        }

        if (await _host.EditOpenScriptAsync(name, edit, cancellationToken).ConfigureAwait(false) is { } done)
            return done;
        if (name.Length == 0)
            throw new InvalidOperationException("no active tab");
        return _host.SaveScript(name, edit(_host.GetScript(name)));
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

        IReadOnlyList<string> report = [];
        string updated = "";
        string wrote = await EditCode(args, code =>
        {
            updated = McpScriptEditing.Patch(code, requested, out report);
            return updated;
        }, cancellationToken).ConfigureAwait(false);

        return $"{wrote}\n{string.Join("\n", report)}\n{McpScriptEditing.LineCount(updated)} lines";
    }

    private async Task<string> ReplaceScriptLines(JsonElement args, CancellationToken cancellationToken)
    {
        int first = Int(args, "first");
        int last = Int(args, "last", first);
        string replacement = Str(args, "code");
        string updated = "";
        string wrote = await EditCode(args, code => updated = McpScriptEditing.ReplaceLines(code, first, last, replacement), cancellationToken)
            .ConfigureAwait(false);

        string what = last < first
            ? $"inserted before line {first}"
            : $"replaced lines {first}-{last}";
        return $"{wrote}\n{what}\n{McpScriptEditing.LineCount(updated)} lines";
    }

}
