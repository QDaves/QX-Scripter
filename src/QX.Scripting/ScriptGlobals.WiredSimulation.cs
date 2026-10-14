using Qx.Game.Application;
using Qx.Game.WiredSimulation;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public WiredSimulationGuide GetWiredSimulationGuide() =>
        _application.Invoke<WiredSimulationCoverageRequest, WiredSimulationGuide>(ApplicationMemberIds.WiredSimulationGuide, new(), Ct);

    public WiredSimulationSession CreateWiredSimulation(WiredSimulationRoom room, WiredSimulationOptions? options = null) =>
        _application.Invoke<WiredSimulationCreateRequest, WiredSimulationSession>(ApplicationMemberIds.WiredSimulationCreate, new(room, options), Ct);

    public WiredSimulationRoom SimulateWiredPlan(WiredBuildPlan plan, WiredSimulationRoom room) => WiredSimulationPlan.Apply(plan, room);

    public WiredSimulationResult RunWiredSimulation(string session_id, IReadOnlyList<WiredSimulationEvent>? events = null,
        long advance_milliseconds = 1000, int? maximum_events = null, IReadOnlyList<long>? breakpoints = null) =>
        _application.Invoke<WiredSimulationRunRequest, WiredSimulationResult>(ApplicationMemberIds.WiredSimulationRun,
            new(session_id, events, advance_milliseconds, maximum_events, breakpoints), Ct);

    public WiredSimulationResult InspectWiredSimulation(string session_id) =>
        _application.Invoke<WiredSimulationSessionRequest, WiredSimulationResult>(ApplicationMemberIds.WiredSimulationInspect, new(session_id), Ct);

    public WiredSimulationSession ForkWiredSimulation(string session_id) =>
        _application.Invoke<WiredSimulationSessionRequest, WiredSimulationSession>(ApplicationMemberIds.WiredSimulationFork, new(session_id), Ct);

    public bool DeleteWiredSimulation(string session_id) =>
        _application.Invoke<WiredSimulationSessionRequest, WiredSimulationRemoved>(ApplicationMemberIds.WiredSimulationDelete, new(session_id), Ct).Removed;

    public IReadOnlyList<WiredSimulationAssertionResult> AssertWiredSimulation(string session_id, IReadOnlyList<WiredSimulationAssertion> assertions) =>
        _application.Invoke<WiredSimulationAssertRequest, IReadOnlyList<WiredSimulationAssertionResult>>(ApplicationMemberIds.WiredSimulationAssert, new(session_id, assertions), Ct);

    public WiredSimulationRoom CaptureWiredSimulation(IReadOnlyList<WiredFormView>? forms = null,
        IReadOnlyList<WiredSimulationVariable>? variables = null, IReadOnlyList<WiredVariablesObjectSnapshot>? object_values = null) =>
        _application.Invoke<WiredSimulationCaptureRequest, WiredSimulationRoom>(ApplicationMemberIds.WiredSimulationCapture,
            new(forms, variables, object_values), Ct);

    public WiredSimulationLeaderboardPage PreviewWiredLeaderboard(string session_id, long furniture_id, string? variable_id = null, int page = 1) =>
        _application.Invoke<WiredSimulationLeaderboardRequest, WiredSimulationLeaderboardPage>(ApplicationMemberIds.WiredSimulationLeaderboardPreview,
            new(session_id, furniture_id, variable_id, page), Ct);
}
