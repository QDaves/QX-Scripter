namespace Qx.Presentation.Services.Status;

public interface ISessionStatusService
{
    SessionStatus Current { get; }

    event Action<SessionStatus>? Changed;

    void RefreshRuntime();
}
