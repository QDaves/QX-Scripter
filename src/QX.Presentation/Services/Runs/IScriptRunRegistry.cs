namespace Qx.Presentation.Services.Runs;

public interface IScriptRunRegistry
{
    int RunningCount { get; }

    int ExternalCount { get; }

    IReadOnlySet<string> LivePaths { get; }

    IReadOnlySet<string> WorkingPaths { get; }

    event Action? Changed;

    int StopAll();

    Task WhenAllStoppedAsync(CancellationToken cancellation_token);
}
