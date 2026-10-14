namespace Qx.Game.Application;

public sealed record WiredBuildGuideRequest;

public sealed record WiredBuildGuide(IReadOnlyList<string> Workflow, IReadOnlyDictionary<string, string> Members,
    string Example, IReadOnlyList<string> Limits)
{
    public static WiredBuildGuide Read() => new(
        [
            "Read wired.form.definitions for the desired category/code. SettingsType is the C# class; Fields expose writable controls, choices, bounds, dependencies and units; Usage explains companions and source semantics.",
            "Create a WiredBuildPlan. Add ordinary furniture with Furni(key, exact_class, x, y, state: value). Put each event-driven stack on its own tile with Stack(key, x, y).Add<TForm>(name, exact_class, form => ...).",
            "Use symbolic furniture keys for primary and secondary selections. Set every consuming effect's furniture/user source explicitly. A selector does not automatically change consumer sources; signal antennas and signal payloads are separate inputs.",
            "Use variables: new Dictionary<string,string> { [field_name] = exact_room_variable_name } for variable references. Planned variable definitions are configured before consumers. Ambiguous or missing names stop construction.",
            "AnalyzeWiredBuild runs locally. CheckWiredBuild also reads current room caps, permissions, furniture identities and error counts. Resolve all errors and review warnings before BuildWired.",
            "BuildWired resolves catalog offers once, confirms each placement, sets requested states through the native writable @state variable, preserves unspecified settings, configures triggers last and verifies changed saves. Progress is available through OnWiredBuildProgress.",
            "After a failed build with a returned result, use plan.Reuse(result) or returned item IDs as ExistingId on the next plan. Cancellation throws; inspect the room to reconcile partial and late placements. Nothing is automatically deleted. Same-position reuse requires exactly one unclaimed match; duplicate matches fail instead of choosing arbitrarily.",
            "For an existing form use ConfigureWired<TForm>(id, form => ...). For inspection use InspectWired(ids); server read permission is required. Dialogs are hidden by default; options.HideSettings=false makes them visible.",
            "For snapshots, set source furniture to the intended state first and save its selection and component flags. A successful configuration save refreshes the stored values. Request capture_snapshot to force that save even when settings are unchanged. ApplyWiredSnapshot restores stored values into the room; it does not capture them.",
            "MeasureWiredUsage samples the actual room during a representative game event. Verify target results and completion counters as well as usage and new errors. A configuration save does not prove gameplay."
        ],
        new Dictionary<string, string> {
            ["definitions"] = ApplicationMemberIds.WiredFormDefinitions,
            ["analyze"] = ApplicationMemberIds.WiredBuildAnalyze,
            ["preflight"] = ApplicationMemberIds.WiredBuildPreflight,
            ["build"] = ApplicationMemberIds.WiredBuild,
            ["configure"] = ApplicationMemberIds.WiredConfigure,
            ["inspect"] = ApplicationMemberIds.WiredInspect,
            ["measure"] = ApplicationMemberIds.WiredUsageMeasure
        },
        """
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
        var progress = OnWiredBuildProgress(step => Status($"{step.Stage}: {step.Completed}/{step.Total}"));
        try {
            var result = await BuildWired(plan);
            if (!result.Success) throw new InvalidOperationException(result.Error);
        } finally {
            progress.Dispose();
        }
        """,
        [
            "Exact class names and offers depend on the current hotel. The example's ordinary switch is illustrative; select a class that exists in that hotel's Builders Club catalog. A bare class never silently selects another color variant.",
            "Use a supported furniture direction; GameData.Furni.GetInfo(class_name).DefaultDirection gives the hotel's default. A different direction returned by the server stops placement with the observed item ID instead of retrying it.",
            "Draft forms validate known field types and bounds; they are not received server defaults. Real furniture source lists, selection limits and permissions are checked on the actual form.",
            "Existing triggers remain active during editing. Build in a quiescent area and gate active game logic before changing a running game. No generic trigger-disable guarantee exists.",
            "Placements advance on observed room additions, not a fixed sleep. Confirmation deadlines learn response latency; missing confirmations and room.error.placements_too_fast increase pacing per furniture kind. Bounded retries recheck late arrivals before sending, use the Builders Club retry flag as true and never skip missing items. Uncorrelated late replies can still produce duplicates; ambiguous results require explicit reconciliation. PlacementSpacingMilliseconds defaults to zero and is only an optional caller-imposed minimum.",
            "Work units = activations per second × actions × targets × receiver activations. This excludes unmodelled selector, condition and downstream costs. EstimatedUsage is only populated with a caller-supplied measured coefficient, never a claimed server formula.",
            "Budget caps come from the live room. Heavy, usage exhaustion, pending delayed events and recursion timeouts are distinct. Successful historical burst sizes are not universal limits.",
            "All registered form categories use the existing native typed form pipeline. Furniture without a registered or publicly available settings form cannot be configured by inventing fields."
        ]);
}
