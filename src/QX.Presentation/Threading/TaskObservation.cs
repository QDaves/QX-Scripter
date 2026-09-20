using Qx.Diagnostics;

namespace Qx.Presentation.Threading;

public static class TaskObservation
{
    public static void Observe(this Task task, string category)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        if (task.IsCompletedSuccessfully)
            return;
        _ = task.ContinueWith(
            static (completed, state) => Report(completed, (string)state!),
            category,
            CancellationToken.None,
            TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    static void Report(Task completed, string category)
    {
        if (completed.IsCanceled || completed.Exception is null)
            return;
        foreach (Exception error in completed.Exception.InnerExceptions)
        {
            if (error is not OperationCanceledException)
                Diag.Error(error.ToString(), category);
        }
    }
}
