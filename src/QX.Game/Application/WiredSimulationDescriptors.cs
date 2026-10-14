using Qx.Game.WiredSimulation;
using System.Diagnostics.CodeAnalysis;

namespace Qx.Game.Application;

internal static class WiredSimulationDescriptors
{
    public static ApplicationDescriptor Guide { get; } = offline(ApplicationMemberIds.WiredSimulationGuide, "Wired simulation coverage",
        "Read before simulation. Lists every registered Wired type, modeled versus unsupported rules and known limitations. Offline results never prove server equivalence.", typeof(WiredSimulationCoverageRequest), typeof(WiredSimulationGuide), true);
    public static ApplicationDescriptor Create { get; } = offline(ApplicationMemberIds.WiredSimulationCreate, "Create offline Wired simulation",
        "Creates an isolated deterministic simulation from explicit furniture, users, typed Wired nodes, variable definitions and floor geometry. Does not contact or alter the hotel. Maximum 16 sessions; delete unused sessions.", typeof(WiredSimulationCreateRequest), typeof(WiredSimulationSession));
    public static ApplicationDescriptor Plan { get; } = offline(ApplicationMemberIds.WiredSimulationPlan, "Apply build plan offline",
        "Maps native build-plan keys and typed settings onto a detached room snapshot. Requires explicit class metadata and variable definitions; places nothing in the hotel.", typeof(WiredSimulationPlanRequest), typeof(WiredSimulationRoom), true);
    public static ApplicationDescriptor Run { get; } = offline(ApplicationMemberIds.WiredSimulationRun, "Run offline Wired simulation",
        "Injects events at absolute virtual milliseconds and advances simulation. Supports bounded stepping and pre-action furniture-ID breakpoints. Returns state, diagnostics, work counters and trace; counters are not measured server usage.", typeof(WiredSimulationRunRequest), typeof(WiredSimulationResult));
    public static ApplicationDescriptor Inspect { get; } = offline(ApplicationMemberIds.WiredSimulationInspect, "Inspect offline Wired simulation",
        "Returns a detached snapshot, trace, effects and pending event count without advancing time.", typeof(WiredSimulationSessionRequest), typeof(WiredSimulationResult), true);
    public static ApplicationDescriptor Fork { get; } = offline(ApplicationMemberIds.WiredSimulationFork, "Fork offline Wired simulation",
        "Clones state, queued events and random generator for independent scenario comparison.", typeof(WiredSimulationSessionRequest), typeof(WiredSimulationSession));
    public static ApplicationDescriptor Delete { get; } = offline(ApplicationMemberIds.WiredSimulationDelete, "Delete offline Wired simulation",
        "Releases a local simulation session. Does not delete room furniture.", typeof(WiredSimulationSessionRequest), typeof(WiredSimulationRemoved));
    public static ApplicationDescriptor Assert { get; } = offline(ApplicationMemberIds.WiredSimulationAssert, "Assert offline Wired state",
        "Checks furniture state, position, presence, counts, variables, user effects, score or no_errors against the local model.", typeof(WiredSimulationAssertRequest), typeof(IReadOnlyList<WiredSimulationAssertionResult>), true);
    public static ApplicationDescriptor Capture { get; } = new(ApplicationMemberIds.WiredSimulationCapture, "Capture room for offline simulation",
        "Copies cached room geometry, furniture, avatars and authorized Wired forms. Optional object_values attaches current-generation readouts and reconciles exact native geometry and furniture state; user room indices map to account IDs. Consistent sub-centimetre precision is retained. Invalid or saturated geometry and unavailable timestamps remain incomplete. May query missing definitions and selected contract contents; never edits the room or automatically reads every holder. Separate replies are not an atomic snapshot.",
        ApplicationMemberKind.Query, ApplicationExposure.All, typeof(WiredSimulationCaptureRequest), typeof(WiredSimulationRoom),
        parameters: parameters(typeof(WiredSimulationCaptureRequest)), requiredStates: [ApplicationStateKey.RoomActive], toolHints: new(true, false, true, false));
    public static ApplicationDescriptor LeaderboardPreview { get; } = offline(ApplicationMemberIds.WiredSimulationLeaderboardPreview, "Preview offline Wired leaderboard",
        "Projects the logical leaderboard from complete StoredVariables, configured filters and the selected variable. Includes users outside the room. Requires an Epoch for row timestamps. Does not predict the hotel's asynchronous display refresh or send packets.",
        typeof(WiredSimulationLeaderboardRequest), typeof(WiredSimulationLeaderboardPage), true);

    private static ApplicationDescriptor offline(string id, string name, string description,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type request, Type response, bool read_only = false) =>
        new(id, name, description, read_only ? ApplicationMemberKind.Query : ApplicationMemberKind.Operation,
            ApplicationExposure.All, request, response, parameters(request), toolHints: new(read_only, false, read_only, false),
            invocationScope: ApplicationInvocationScope.Persistent);

    private static ApplicationParameterDescriptor[] parameters(Type type)
    {
        if (type == typeof(WiredSimulationCreateRequest)) return [
            new("room", typeof(WiredSimulationRoom), true, null, "Detached room snapshot with explicit geometry, forms and variable values."),
            new("options", typeof(WiredSimulationOptions), false, null, "Deterministic seed and local execution limits; not guaranteed hotel limits.")];
        if (type == typeof(WiredSimulationSessionRequest)) return [new("session_id", typeof(string), true, null, "Local simulation session identifier.")];
        if (type == typeof(WiredSimulationRunRequest)) return [
            new("session_id", typeof(string), true, null, "Local simulation session identifier."),
            new("events", typeof(IReadOnlyList<WiredSimulationEvent>), false, null, "Events at absolute virtual timestamps. VariableObserved supplies an explicit Quest, QuestChain or DailyTask value or absence for a user account ID; notifications require observed change and origin fields. ApiRequest uses explicit room.Api permissions. LinkMessage and LinkDataframe require a receiving endpoint, explicit Link payload and room.Links permissions. These operations send no network requests."),
            new("advance_milliseconds", typeof(long), false, 1000L, "Virtual time to advance from current time."),
            new("maximum_events", typeof(int?), false, null, "Optional step limit; one for a single queue entry."),
            new("breakpoints", typeof(IReadOnlyList<long>), false, null, "Pause before these Wired furniture IDs run.")];
        if (type == typeof(WiredSimulationAssertRequest)) return [
            new("session_id", typeof(string), true, null, "Local simulation session identifier."),
            new("assertions", typeof(IReadOnlyList<WiredSimulationAssertion>), true, null, "Expected local states, values or invariants.")];
        if (type == typeof(WiredSimulationPlanRequest)) return [
            new("plan", typeof(WiredBuildRequest), true, null, "Native build plan to apply offline."),
            new("room", typeof(WiredSimulationRoom), true, null, "Room with geometry and metadata for all referenced furniture classes.")];
        if (type == typeof(WiredSimulationCaptureRequest)) return [
            new("forms", typeof(IReadOnlyList<WiredFormView>), false, null, "Previously authorized form reads from the current room generation."),
            new("variables", typeof(IReadOnlyList<WiredSimulationVariable>), false, null, "Explicit variable definitions for the offline snapshot."),
            new("object_values", typeof(IReadOnlyList<WiredVariablesObjectSnapshot>), false, null, "Explicit current-generation readouts. Exact native geometry and furniture state reconcile the cached snapshot. User targets use room indices. Values may be clamped and do not include timestamps.")];
        if (type == typeof(WiredSimulationLeaderboardRequest)) return [
            new("session_id", typeof(string), true, null, "Local simulation session identifier."),
            new("furniture_id", typeof(long), true, null, "Variable leaderboard furniture ID in the simulated room."),
            new("variable_id", typeof(string), false, null, "Offered variable ID; defaults to the first configured choice."),
            new("page", typeof(int), false, 1, "One-based page number.")];
        return [];
    }
}
