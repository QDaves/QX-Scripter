using Qx.Game.Application;
using Qx.Model.Wired;

namespace Qx.Scripting;

public partial class ScriptGlobals
{
    public WiredBuildGuide GetWiredBuildGuide() =>
        _application.Invoke<WiredBuildGuideRequest, WiredBuildGuide>(ApplicationMemberIds.WiredBuildGuide, new(), Ct);

    public Task<WiredUsageMeasurement> MeasureWiredUsage(int duration_ms = 2000, int interval_ms = 500) =>
        _application.InvokeAsync<WiredUsageMeasureRequest, WiredUsageMeasurement>(ApplicationMemberIds.WiredUsageMeasure,
            new(duration_ms, interval_ms), Ct).AsTask();

    public Task<WiredFormView> ConfigureWired<T>(Id furni_id, Action<T> configure,
        bool capture_snapshot = false, WiredBuildOptions? options = null) where T : WiredForm
    {
        ArgumentNullException.ThrowIfNull(configure);
        T form = WiredFormRegistry.Draft<T>();
        configure(form);
        return _application.InvokeAsync<WiredConfigureRequest, WiredFormView>(ApplicationMemberIds.WiredConfigure,
            new(furni_id, form.Definition.Category, form.Definition.Code, WiredFormPatch.FromEdits(form), capture_snapshot, options), Ct).AsTask();
    }

    public WiredBuildAnalysis AnalyzeWiredBuild(WiredBuildPlan plan) => AnalyzeWiredBuild(plan.ToRequest());

    public WiredBuildAnalysis AnalyzeWiredBuild(WiredBuildRequest request) =>
        _application.Invoke<WiredBuildRequest, WiredBuildAnalysis>(ApplicationMemberIds.WiredBuildAnalyze, request, Ct);

    public Task<WiredBuildPreflight> CheckWiredBuild(WiredBuildPlan plan) => CheckWiredBuild(plan.ToRequest());

    public Task<WiredBuildPreflight> CheckWiredBuild(WiredBuildRequest request) =>
        _application.InvokeAsync<WiredBuildRequest, WiredBuildPreflight>(ApplicationMemberIds.WiredBuildPreflight, request, Ct).AsTask();

    public Task<WiredBuildResult> BuildWired(WiredBuildPlan plan) => BuildWired(plan.ToRequest());

    public Task<WiredBuildResult> BuildWired(WiredBuildRequest request) =>
        _application.InvokeAsync<WiredBuildRequest, WiredBuildResult>(ApplicationMemberIds.WiredBuild, request, Ct).AsTask();

    public Task<WiredInspectResult> InspectWired(IReadOnlyList<Id> furniture_ids, bool hide_settings = true, int timeout_ms = 10000) =>
        _application.InvokeAsync<WiredInspectRequest, WiredInspectResult>(ApplicationMemberIds.WiredInspect,
            new(furniture_ids, hide_settings, timeout_ms), Ct).AsTask();

    public IDisposable OnWiredBuildProgress(Action<WiredBuildProgress> receiver) =>
        _application.Subscribe(ApplicationMemberIds.WiredBuildProgress, receiver);
}
