namespace Qx.Game.Application;

internal static class WiredBuildDescriptors
{
    public static ApplicationDescriptor Guide { get; } = new(
        ApplicationMemberIds.WiredBuildGuide, "Wired building guide",
        "Start here to build or inspect Wired. Returns the native plan/configuration workflow, source wiring rules, C# example, shared application member IDs and limits. No connection required.",
        ApplicationMemberKind.Query, ApplicationExposure.All, typeof(WiredBuildGuideRequest), typeof(WiredBuildGuide),
        toolHints: new(true, false, true, false), invocationScope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Measure { get; } = new(
        ApplicationMemberIds.WiredUsageMeasure, "Measure Wired usage",
        "Samples actual room usage and Heavy while the existing setup runs, compares error counts and returns peak, mean, initial and final budgets. Does not activate stacks. Measures the whole room, not the isolated cost of one setup.",
        ApplicationMemberKind.Operation, ApplicationExposure.All, typeof(WiredUsageMeasureRequest), typeof(WiredUsageMeasurement),
        [new("duration_milliseconds", typeof(int), false, 2000, "Measurement duration, 100 through 60000 milliseconds."),
         new("interval_milliseconds", typeof(int), false, 500, "Delay between samples, 100 through 5000 milliseconds.")],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive], messages: monitor(), toolHints: new(true, false, true, true));

    public static ApplicationDescriptor Configure { get; } = new(
        ApplicationMemberIds.WiredConfigure, "Configure Wired",
        "Reads the current form, validates the expected category and code, patches only supplied controls, saves changed settings or a requested snapshot refresh and verifies readback. Unknown fields survive. Dialogs are hidden by default.",
        ApplicationMemberKind.Operation, ApplicationExposure.All, typeof(WiredConfigureRequest), typeof(WiredFormView),
        [new("furni_id", typeof(Id), true, null, "Existing Wired furniture ID."),
         new("category", typeof(Qx.Model.Wired.WiredFormCategory), true, null, "Expected form category."),
         new("code", typeof(int), true, null, "Expected form code."),
         new("settings", typeof(WiredFormPatch), true, null, "Explicit controls to change."),
         new("capture_snapshot", typeof(bool), false, false, "Force a configuration save to refresh stored snapshot values, even when settings are unchanged. Does not restore furniture in the room."),
         new("options", typeof(WiredBuildOptions), false, null, "Visibility, timeout and readback options.")],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive],
        messages: WiredApplicationDescriptors.FormGet.Messages.Concat(WiredApplicationDescriptors.FormSave.Messages), toolHints: new(false, false, false, true));

    public static ApplicationDescriptor Analyze { get; } = new(
        ApplicationMemberIds.WiredBuildAnalyze, "Analyze Wired plan",
        "Checks typed settings, furniture keys and selector consumers without sending anything. Work estimates are not measured hotel usage.",
        ApplicationMemberKind.Query, ApplicationExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildAnalysis),
        parameters(), toolHints: new(true, false, true, false), invocationScope: ApplicationInvocationScope.Persistent);

    public static ApplicationDescriptor Preflight { get; } = new(
        ApplicationMemberIds.WiredBuildPreflight, "Check Wired build against room",
        "Validates the plan, exact furniture classes, existing IDs, permissions, live room cap and error baseline. Places and changes nothing.",
        ApplicationMemberKind.Operation, ApplicationExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildPreflight),
        parameters(), [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive], messages: monitor(), toolHints: new(true, false, true, true));

    public static ApplicationDescriptor Build { get; } = new(
        ApplicationMemberIds.WiredBuild, "Build Wired plan",
        "Resolves exact Builders Club classes once, reuses unambiguous furniture at planned positions or explicit IDs, verifies placements and states, resolves symbolic selection keys, saves typed forms and checks readback. Placements advance on native room confirmations; missing replies and placement-rate notifications adapt per-kind pacing. Settings dialogs are hidden by default. Trigger forms are configured last; existing triggers remain active. Returns partial progress on failure without deleting furniture. Retrying placements is bounded; late arrivals can require reconciliation.",
        ApplicationMemberKind.Operation, ApplicationExposure.All, typeof(WiredBuildRequest), typeof(WiredBuildResult),
        parameters(), [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive], messages: monitor()
            .Concat(WiredApplicationDescriptors.FormGet.Messages.Select(message => message with { Required = false }))
            .Concat(WiredApplicationDescriptors.FormSave.Messages.Select(message => message with { Required = false })),
        toolHints: new(false, false, false, true));

    public static ApplicationDescriptor Inspect { get; } = new(
        ApplicationMemberIds.WiredInspect, "Inspect Wired forms",
        "Reads requested Wired forms sequentially with server-granted read permission. Preserves unknown fields and reports per-item errors. Dialogs are hidden by default; does not bypass owner permissions.",
        ApplicationMemberKind.Operation, ApplicationExposure.All, typeof(WiredInspectRequest), typeof(WiredInspectResult),
        [new("furniture_ids", typeof(IReadOnlyList<Id>), true, null, "Wired furniture IDs to inspect."),
         new("hide_settings", typeof(bool), false, true, "Suppress settings windows during this operation."),
         new("timeout_milliseconds", typeof(int), false, 10000, "Timeout per form, 1 through 120000.")],
        [ApplicationStateKey.HotelConnected, ApplicationStateKey.RoomActive],
        messages: WiredApplicationDescriptors.FormGet.Messages, toolHints: new(true, false, true, true));

    public static ApplicationDescriptor Progress { get; } = new(
        ApplicationMemberIds.WiredBuildProgress, "Wired build progress", "Reports catalog, placement, configuration, inspection and completion progress.",
        ApplicationMemberKind.Event, ApplicationExposure.Scripting | ApplicationExposure.Ui | ApplicationExposure.Cli, null, typeof(WiredBuildProgress));

    private static ApplicationParameterDescriptor[] parameters() => [
        new("items", typeof(IReadOnlyList<WiredBuildItem>), true, null, "Named furniture, exact class names, tile positions, typed form settings and symbolic furniture references."),
        new("options", typeof(WiredBuildOptions), false, null, "Dialog visibility, verification, bounded placement retries, timeout and live usage headroom."),
        new("workload", typeof(WiredWorkload), false, null, "Optional activation rate, actions, targets and receiver fanout. A measured cost coefficient must be calibrated in the current room.")
    ];

    private static IEnumerable<ApplicationMessageRequirement> monitor() =>
        WiredApplicationDescriptors.RoomStatsGet.Messages.Concat(WiredApplicationDescriptors.RoomErrorLogsGet.Messages);
}
