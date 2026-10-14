# Building with Wired

Use `WiredBuildPlan` to describe furniture and Wired stacks before changing a room. The same
builder runs through scripts and the application layer used by MCP. It uses the existing typed
forms for all registered triggers, effects, conditions, selectors, add-ons and variables.

## First stack

Choose free tiles and furniture available in the current hotel's Builders Club catalog. Each stack
gets its own tile. Furniture keys identify objects in this plan; they are not hotel IDs.

```csharp
var plan = new WiredBuildPlan();
plan.Furni("lamp", "wf_floor_switch1", 10, 10, direction: 2, state: 0);
plan.Stack("switch", 2, 2)
    .Add<WiredAvatarSaysSomethingTriggerForm>("trigger", "wf_trg_says_something", form => {
        form.Text = "switch";
        form.MatchMode = WiredMatchModeOption.ExactMatch;
    })
    .Add<WiredToggleFurniStateActionForm>("effect", "wf_act_toggle_state", form => {
        form.SelectFurnitureSource(0, WiredFurnitureSource.Picked);
    }, furniture: ["lamp"]);

var check = await CheckWiredBuild(plan);
if (!check.Analysis.Valid)
    throw new InvalidOperationException(string.Join("\n", check.Analysis.Errors));

var progress = OnWiredBuildProgress(step => Status($"{step.Stage}: {step.Completed}/{step.Total}"));
try {
    var result = await BuildWired(plan);
    if (!result.Success)
        throw new InvalidOperationException(result.Error);
} finally {
    progress.Dispose();
}
```

This describes a switch, a speech trigger and a toggle effect. Furniture classes remain explicit:
the builder never substitutes a different color or object because its class name looks similar.
The example requires these three classes in the hotel's catalog and sufficient room rights.

Choose a supported furniture direction. `GameData.Furni.GetInfo(class_name).DefaultDirection`
provides the hotel's default; some furniture accepts fewer directions than the eight protocol
values. If a newly placed item returns a different direction, construction stops with its ID
and observed direction instead of treating it as missing and placing duplicates.

`AnalyzeWiredBuild(plan)` is an offline check. `CheckWiredBuild(plan)` additionally reads actual room
caps, permissions, furniture data, existing items and the error baseline. Neither places furniture.
Building repeats the check because the room may have changed.

Placement advances as soon as the room reports the matching furniture. There is no default fixed
pause between successful requests. The builder learns confirmation latency and increases pacing
for the affected furniture kind after a missing reply or the hotel's
`room.error.placements_too_fast` notification. Pacing decreases again after successful requests.
`PlacementSpacingMilliseconds` defaults to zero; set it only to impose an additional minimum.
State and altitude writes use their own adaptive pacing and confirm the actual resulting values.

Missing replies receive bounded retries. Before sending again, the builder checks for late arrivals
and waits through a recovery window that can finish immediately when the furniture appears.
Progress includes `retry` and `throttled` stages. Failed placement stops the plan rather than
skipping the item. A Builders Club confirmation warning is distinct from rate limiting and stops
the build; new Wired execution errors also stop it. These checks do not make non-idempotent
placement perfectly reliable: the protocol has no request ID linking a late item to a specific
attempt, so ambiguous duplicates still require inspection.

## Configuration and sources

The callback receives the actual typed form, so completion shows its properties and enum values.
Only assigned controls are patched. Explicit `false`, `0`, an empty string or an empty selection
are edits too. Omitted settings retain their received values; unknown protocol fields survive.

Read `GetWiredBuildGuide()` for the workflow. Application member `wired.form.definitions` provides
`SettingsType`, field types, bounds, units, dependencies and usage notes. A draft's initial values
are authoring placeholders, not guaranteed hotel defaults.

For an existing box, configure it directly:

```csharp
await ConfigureWired<WiredAvatarSaysSomethingTriggerForm>(trigger_id, form => {
    form.Text = "start";
    form.OwnerOnly = true;
});
```

Primary and secondary furniture lists are independent. Pass `furniture: ["key"]` and
`secondary_furniture: ["other_key"]` to resolve them after placement, including forward references.
Set each effect's source explicitly. A selector does not automatically select `Selector` on its
consumer. Signal recipients, antenna selections and signal-carried furniture are also separate.
The actual form's allowed sources and selection limits are validated before saving.

```csharp
plan.Stack("area", 3, 2)
    .Add<WiredFurnitureInAreaSelectorForm>("selector", "wf_slc_furni_area", form => {
        WiredSettings.Area(form, 10, 10, 3, 3);
    })
    .Add<WiredToggleFurniStateActionForm>("effect", "wf_act_toggle_state", form => {
        form.SelectFurnitureSource(0, WiredFurnitureSource.Selector);
    });
```

Add the intended trigger to that stack before using it. `Area` validates coordinates and size.
Editor and `compile_check` diagnostics flag out-of-range constant fields (`QX1101`) and new area
selectors missing their rectangle (`QX1102`). They cannot prove gameplay or arbitrary dynamic code.

## Variables, state and snapshots

`Furni(..., state: 2, altitude_centimeters: 50)` sets the writable intrinsic variables `@state` and
`@altitude`, then waits for the resulting room updates. The latter means a Z value of `0.5`.
Unavailable writable variables or missing updates fail explicitly. State numbers depend on the
furniture class; the builder cannot make an unsupported state valid.
Intrinsic writes advance on confirmed state and movement updates, including Wired and roller
movement. Unconfirmed assignments are retried at most three times within the confirmation timeout.
Each attempt reserves time for subsequent retries, even when previous replies were slow.
Increments and other non-idempotent operations are not replayed by this mechanism.

Repeated variable-definition reads check the current room hash before reusing a previously
completed snapshot. A changed hash or room/session scope requires a fresh complete difference
stream. A partial stream or an unanswered hash request never counts as a verified snapshot.

Use `variables: new Dictionary<string, string> { ["field_name"] = "room_variable_name" }` on
`Stack.Add` to bind a variable field by its exact display name. Field names come from the form
definition. The name must resolve uniquely. Planned variable definitions are configured before
their consumers. Do not also assign the same field in the callback.

For a value-or-variable control use `WiredSettings.Constant(number)` or
`WiredSettings.Variable(id, WiredSourceDomain.Furniture)`. The variable reference takes a hotel
variable ID, not its display name. Choose the domain required by that control.
Constants update the mode and numeric value while preserving the inactive variable selection;
server normalization of that unused selection does not invalidate the constant.

Use `capture_snapshot: true` on forms that save furniture state or position. Prepare reference
furniture first, set its selection, then capture. The builder requests capture after applying the
settings; the hotel provides no acknowledgement of the captured contents. Test the resulting effect.

## Construction and reuse

Catalog discovery uses cached exact-kind matches first. On a cold cache, pages containing the
furniture data's BC offer IDs are checked first, followed by Wired pages and other eligible pages.
Discovery stops when every requested kind is found. Offer hints only determine search order;
the actual page and product kind are checked before placement.

Build order is placement, requested state/height changes, variable definitions, add-ons, selectors,
conditions, actions and finally trigger configuration. Trigger furniture is placed last. This
reduces premature activation but is not a transaction: existing triggers remain active. Gate a
running game before modifying it.

The builder resolves exact BC offers, validates cached offers with the catalog cache's normal
freshness policy and sends placement with the retry boolean set to `true`. It waits for the matching
room item before continuing. `PlacementSpacingMilliseconds` defaults to zero; retries and timeouts
are bounded. This is acknowledgement-driven placement, not an unbounded placement flood.

One unclaimed matching object at the requested position can be reused. Multiple matches are an
error. Supply `existing_id` to choose explicitly. `place_if_missing: false` prevents creating an
absent item; an explicitly supplied ID must exist and match. Set `reuse_at_position: false` on
ordinary `Furni` entries when a new copy is intended.

Keep the returned result, including on failure:

```csharp
var result = await BuildWired(plan);
plan.Reuse(result);
```

The next build uses those IDs and requires the same room generation. No automatic rollback picks
up furniture. A failed or cancelled placement may still arrive late: inspect the room before
retrying uncertain objects. `Reuse` does not promise to restore moved items or survive leaving and
re-entering the room; reconcile identities explicitly in that case.

The default `WiredBuildOptions` hides settings windows and verifies saved controls by reading them
back. Set `HideSettings: false` for visible editing. Inspection follows the same default:

```csharp
var forms = await InspectWired([trigger_id, effect_id], hide_settings: false);
```

Inspection requires server-granted read permission. Construction and configuration require read
and modification permissions. These methods do not bypass an owner's settings. A save receipt and
matching readback prove configuration acceptance, not correct runtime effects.

## Usage and larger games

Use loops and shared functions to generate plan entries. Keep stacks separate and readable; place
selectors and their consumers together. Conditions, temporary furniture and signal effects use the
same typed `Add<T>` mechanism. A temporary-furniture effect is still an executing effect: avoiding
the permanent furniture cap does not avoid execution or spawn limits.

`CheckWiredBuild` uses the current server-reported floor cap and usage cap. The default
`MaximumUsageFraction` is 0.7, leaving headroom; it is a configurable construction threshold, not a
claim about the server's Heavy threshold. The builder checks usage and new error counts every 25
placements/configurations and at completion, stopping if the headroom is exhausted or a new error
appears. It neither clears errors nor disables unrelated Wired.

```csharp
plan.Workload = new WiredWorkload(
    ActivationsPerSecond: 2,
    ActionsPerActivation: 3,
    TargetsPerAction: 20,
    ReceiverActivations: 1);
var estimate = AnalyzeWiredBuild(plan);
var measured = await MeasureWiredUsage(duration_ms: 3000, interval_ms: 500);
```

Work units are activations × actions × targets × receiver activations. They exclude unmodelled
selector scans, conditions and downstream activity. `EstimatedUsage` stays null unless you provide
`MeasuredCostPerWorkUnit` from a representative measurement. That coefficient belongs to your
workload and room; there is no furniture-count-to-server-cost formula.

Without a supplied workload, analysis derives `AutomaticWorkloads` per stack from explicitly
configured periodic triggers and picked furniture selections. It uses the native timer units
(50 milliseconds or 5 seconds). Unknown event rates, user targets, selectors and signal payloads
remain null instead of inventing counts. The total is derived only when every stack is resolvable;
these direct action-target work units still exclude add-ons, conditions and downstream events.

Measurement samples the whole room while it runs: initial/final statistics, average, peak, Heavy
and newly counted errors. It does not fire triggers. Test initialization, maximum simultaneous
players and resets, with completion counters and an independent stop mechanism. Prefer event-driven
updates, restrict selector areas, avoid unnecessary writes and bound signal queues. Delaying work
does not remove its cost. Heavy, execution cap, delayed events, recursion and temporary placement
limits are distinct constraints.

## MCP and application members

`application_wired_build_guide` is in the default tool inventory. Other members can be discovered
with `list_mcp_tools` and inspected with `describe_mcp_tool`. All use the same implementation as
scripts; no packet-building code is necessary.

| Member | Purpose |
| --- | --- |
| `wired.build.guide` | Workflow, example, member IDs and constraints, available offline |
| `wired.form.definitions` | Registered typed forms, fields and usage notes |
| `wired.build.analyze` | Validate a plan without a connection |
| `wired.build.preflight` | Read and validate against the current room |
| `wired.build` | Construct and configure the plan, returning item IDs and partial results |
| `wired.configure` | Read, patch and verify one existing form |
| `wired.inspect` | Read forms with per-item errors |
| `wired.usage.measure` | Sample current whole-room usage and error deltas |

MCP plans use `items`, `options` and `workload`. Each item specifies `key`, `class_name`, `x`, `y`,
and optionally `category`, `code`, `settings`, `furniture_keys`, `secondary_furniture_keys`,
`variables_by_name`, `capture_snapshot`, `state` and `altitude_centimeters`. `settings.fields` holds
named field edits from `wired.form.definitions`. Use `describe_mcp_tool` for exact schemas, enum
representations and the nested value shape. Typed C# callbacks are converted to that same patch
representation. `wired.build.progress` is a script/UI/CLI event; it is not an MCP streaming tool.
