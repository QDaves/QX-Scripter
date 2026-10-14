using Qx.Game.WiredSimulation;

namespace Qx.Game.Application;

public sealed record WiredSimulationCreateRequest(WiredSimulationRoom Room, WiredSimulationOptions? Options = null);
public sealed record WiredSimulationSessionRequest(string SessionId);
public sealed record WiredSimulationRunRequest(string SessionId, IReadOnlyList<WiredSimulationEvent>? Events = null,
    long AdvanceMilliseconds = 1000, int? MaximumEvents = null, IReadOnlyList<long>? Breakpoints = null);
public sealed record WiredSimulationAssertRequest(string SessionId, IReadOnlyList<WiredSimulationAssertion> Assertions);
public sealed record WiredSimulationCaptureRequest(IReadOnlyList<WiredFormView>? Forms = null,
    IReadOnlyList<WiredSimulationVariable>? Variables = null,
    IReadOnlyList<WiredVariablesObjectSnapshot>? ObjectValues = null);
public sealed record WiredSimulationSession(string SessionId, WiredSimulationResult Result);
public sealed record WiredSimulationRemoved(bool Removed);
public sealed record WiredSimulationCoverageRequest;
public sealed record WiredSimulationGuide(string Scope, IReadOnlyList<string> Workflow,
    IReadOnlyList<WiredSimulationCoverage> Rules);
public sealed record WiredSimulationPlanRequest(WiredBuildRequest Plan, WiredSimulationRoom Room);
public sealed record WiredSimulationLeaderboardRequest(string SessionId, long FurnitureId, string? VariableId = null, int Page = 1);
