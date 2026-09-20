namespace Qx.Presentation.Threading;

public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action work, UiPriority priority = UiPriority.Normal);

    void Post(Func<Task> work, UiPriority priority = UiPriority.Normal);

    Task InvokeAsync(Action work, CancellationToken cancellation_token = default);

    Task<T> InvokeAsync<T>(Func<T> work, CancellationToken cancellation_token = default);

    Task InvokeAsync(Func<Task> work, CancellationToken cancellation_token = default);

    Task<T> InvokeAsync<T>(Func<Task<T>> work, CancellationToken cancellation_token = default);
}
