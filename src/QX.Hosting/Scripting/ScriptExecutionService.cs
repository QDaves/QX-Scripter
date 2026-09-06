using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Qx.Game;
using Qx.Game.Application;
using Qx.Interception;
using Qx.Scripting;

namespace Qx.Hosting;

public sealed record ScriptExecutionRequest
{
    public required string Code { get; init; }
    public required string SourceIdentity { get; init; }
    public required string FileName { get; init; }
    public TimeSpan? Timeout { get; init; }
    public TimeSpan BackgroundDrainTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public Action<string>? OutputWritten { get; init; }
    public Action<Diagnostic>? DiagnosticReported { get; init; }
    public Action<ScriptExecutionError>? ErrorReported { get; init; }
    public Action<ScriptRunState>? StateChanged { get; init; }
    public Func<ScriptGlobals, CancellationToken, Task>? ConfigureAsync { get; init; }
    public Func<ScriptGlobals, CancellationToken, Task>? ContinueAsync { get; init; }
    public Func<Task>? DrainAsync { get; init; }
}

public sealed record ScriptExecutionResult(
    string SourceIdentity,
    string FileName,
    ScriptRunState State,
    bool Faulted,
    bool TimedOut,
    bool AlreadyRunning,
    double RuntimeMs,
    string Output,
    ImmutableArray<ScriptExecutionError> Errors);

public sealed class ScriptExecutionService(
    IInterceptor extension,
    GameState game,
    IApplicationRuntime application,
    CancellationToken lifetime = default)
{
    private readonly ConcurrentDictionary<string, object> active =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsRunning(string source_identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source_identity);
        return active.ContainsKey(source_identity);
    }

    public async Task<ScriptExecutionResult> RunAsync(
        ScriptExecutionRequest request,
        CancellationToken cancellation_token = default)
    {
        Validate(request);
        var marker = new object();
        if (!active.TryAdd(request.SourceIdentity, marker))
            return AlreadyActive(request);

        var output = new StringBuilder();
        var errors = new List<ScriptExecutionError>();
        var cancellation_observers = new List<Task>();
        var cancellation_gate = new object();
        var stopwatch = Stopwatch.StartNew();
        var timeout_source = new CancellationTokenSource();
        if (request.Timeout is { } timeout)
            timeout_source.CancelAfter(timeout);
        using var run_source = new CancellationTokenSource();

        ScriptGlobals? globals = null;
        ScriptRunState state = ScriptRunState.Compiling;
        string stage = "catalog";
        int background_faulted = 0;
        int callbacks_closed = 0;
        int cleanup_started = 0;
        int termination_cause = (int)TerminationCause.None;
        bool timed_out = false;

        async Task ObserveCancellationAsync(Task cancellation)
        {
            try
            {
                await cancellation.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                lock (errors)
                {
                    if (Volatile.Read(ref callbacks_closed) == 0)
                    {
                        errors.Add(ScriptExecutionError.FromException(
                            error,
                            "cancellation",
                            request.FileName));
                    }
                }
            }
        }

        void QueueCancellation()
        {
            try
            {
                Task observer = ObserveCancellationAsync(run_source.CancelAsync());
                cancellation_observers.Add(observer);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        bool RequestCancellation(TerminationCause cause)
        {
            lock (cancellation_gate)
            {
                if (Volatile.Read(ref cleanup_started) != 0)
                    return false;
                if (Interlocked.CompareExchange(
                        ref termination_cause,
                        (int)cause,
                        (int)TerminationCause.None) != (int)TerminationCause.None)
                {
                    return false;
                }
                QueueCancellation();
                return true;
            }
        }

        void CaptureAdapterError(Exception error)
        {
            lock (errors)
            {
                if (Volatile.Read(ref callbacks_closed) != 0)
                    return;
                errors.Add(ScriptExecutionError.FromException(error, "adapter", request.FileName));
            }
            RequestCancellation(TerminationCause.BackgroundFault);
        }

        void WriteOutput(string message)
        {
            if (Volatile.Read(ref callbacks_closed) != 0)
                return;
            lock (output)
            {
                if (Volatile.Read(ref callbacks_closed) != 0)
                    return;
                output.AppendLine(message);
            }
            try
            {
                request.OutputWritten?.Invoke(message);
            }
            catch (Exception error)
            {
                CaptureAdapterError(error);
            }
        }

        void AddError(ScriptExecutionError error)
        {
            lock (errors)
            {
                if (Volatile.Read(ref callbacks_closed) != 0)
                    return;
                errors.Add(error);
            }
            if (Volatile.Read(ref callbacks_closed) != 0)
                return;
            try
            {
                request.ErrorReported?.Invoke(error);
            }
            catch (Exception adapter_error)
            {
                CaptureAdapterError(adapter_error);
            }
        }

        void ChangeState(ScriptRunState next)
        {
            state = next;
            try
            {
                request.StateChanged?.Invoke(next);
            }
            catch (Exception error)
            {
                CaptureAdapterError(error);
                state = ScriptRunState.Faulted;
            }
        }

        void ReportDiagnostic(Diagnostic diagnostic)
        {
            try
            {
                request.DiagnosticReported?.Invoke(diagnostic);
            }
            catch (Exception error)
            {
                CaptureAdapterError(error);
            }
        }

        using CancellationTokenRegistration lifetime_registration = lifetime.Register(
            () => RequestCancellation(TerminationCause.External));
        using CancellationTokenRegistration cancellation_registration = cancellation_token.Register(
            () => RequestCancellation(TerminationCause.External));
        using CancellationTokenRegistration timeout_registration = timeout_source.Token.Register(
            () => RequestCancellation(TerminationCause.Timeout));

        try
        {
            ChangeState(ScriptRunState.Compiling);
            await extension.WaitForCatalogBuildAsync(run_source.Token).ConfigureAwait(false);
            stage = "compile";
            ScriptProgram program = await Task.Run(
                () => ScriptEngine.Prepare(request.Code, request.FileName),
                run_source.Token).ConfigureAwait(false);

            foreach (Diagnostic diagnostic in program.Diagnostics.Where(
                         value => value.Severity >= DiagnosticSeverity.Warning))
            {
                ReportDiagnostic(diagnostic);
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    AddError(ScriptExecutionError.FromDiagnostic(diagnostic, request.FileName));
            }

            lock (errors)
            {
                if (program.HasErrors || errors.Count > 0)
                    state = ScriptRunState.Faulted;
            }

            if (state != ScriptRunState.Faulted)
            {
                stage = "setup";
                globals = new ScriptGlobals(extension, game, application, WriteOutput, run_source.Token, error =>
                {
                    if (Volatile.Read(ref callbacks_closed) != 0)
                        return;
                    Interlocked.Exchange(ref background_faulted, 1);
                    AddError(ScriptExecutionError.FromException(error, "background", request.FileName));
                    RequestCancellation(TerminationCause.BackgroundFault);
                }, () =>
                {
                    if (Volatile.Read(ref callbacks_closed) != 0 ||
                        Volatile.Read(ref background_faulted) != 0)
                    {
                        return;
                    }
                    RequestCancellation(TerminationCause.BackgroundFinished);
                });

                if (request.ConfigureAsync is not null)
                {
                    await request.ConfigureAsync(globals, run_source.Token)
                        .WaitAsync(run_source.Token)
                        .ConfigureAwait(false);
                }

                stage = "runtime";
                ChangeState(ScriptRunState.Running);
                await program.RunAsync(globals, run_source.Token).ConfigureAwait(false);
                if (request.ContinueAsync is not null)
                {
                    await request.ContinueAsync(globals, run_source.Token)
                        .WaitAsync(run_source.Token)
                        .ConfigureAwait(false);
                }
                state = ScriptRunState.Finished;
            }
        }
        catch (ScriptFinishedException)
        {
            state = ScriptRunState.Finished;
        }
        catch (OperationCanceledException error)
        {
            var cause = (TerminationCause)Volatile.Read(ref termination_cause);
            timed_out = cause is TerminationCause.Timeout;
            if (Volatile.Read(ref background_faulted) != 0)
            {
                state = ScriptRunState.Faulted;
            }
            else if (cause is TerminationCause.BackgroundFinished)
            {
                state = ScriptRunState.Finished;
            }
            else if (timed_out)
            {
                AddError(new ScriptExecutionError(
                    "timeout",
                    typeof(TimeoutException).FullName!,
                    $"Script execution exceeded {request.Timeout!.Value.TotalMilliseconds:0} ms.",
                    Path.GetFileName(request.FileName),
                    null,
                    null,
                    null));
                state = ScriptRunState.Faulted;
            }
            else if (run_source.IsCancellationRequested)
            {
                state = ScriptRunState.Stopped;
            }
            else
            {
                AddError(ScriptExecutionError.FromException(error, stage, request.FileName));
                state = ScriptRunState.Faulted;
            }
        }
        catch (Exception error)
        {
            AddError(ScriptExecutionError.FromException(error, stage, request.FileName));
            state = ScriptRunState.Faulted;
        }
        finally
        {
            Task[] cancellation_tasks;
            lock (cancellation_gate)
            {
                Interlocked.Exchange(ref cleanup_started, 1);
                QueueCancellation();
                cancellation_tasks = [.. cancellation_observers];
            }
            try
            {
                await Task.WhenAll(cancellation_tasks)
                    .WaitAsync(request.BackgroundDrainTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception error)
            {
                AddError(ScriptExecutionError.FromException(error, "cancellation", request.FileName));
                state = ScriptRunState.Faulted;
            }

            if (request.DrainAsync is not null)
            {
                try
                {
                    await request.DrainAsync()
                        .WaitAsync(request.BackgroundDrainTimeout)
                        .ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    AddError(ScriptExecutionError.FromException(error, "cleanup", request.FileName));
                    state = ScriptRunState.Faulted;
                }
            }

            if (globals is not null)
            {
                try
                {
                    int drain_timeout = checked((int)Math.Ceiling(request.BackgroundDrainTimeout.TotalMilliseconds));
                    if (!await globals.WaitForBackgroundTasksAsync(drain_timeout).ConfigureAwait(false))
                    {
                        AddError(new ScriptExecutionError(
                            "background",
                            typeof(TimeoutException).FullName!,
                            $"Background tasks did not stop within {drain_timeout} ms.",
                            Path.GetFileName(request.FileName),
                            null,
                            null,
                            null));
                        state = ScriptRunState.Faulted;
                    }
                }
                catch (Exception error)
                {
                    AddError(ScriptExecutionError.FromException(error, "background", request.FileName));
                    state = ScriptRunState.Faulted;
                }

                try
                {
                    globals.Dispose();
                }
                catch (Exception error)
                {
                    AddError(ScriptExecutionError.FromException(error, "cleanup", request.FileName));
                    state = ScriptRunState.Faulted;
                }
            }

            if (Volatile.Read(ref background_faulted) != 0)
                state = ScriptRunState.Faulted;
            lock (errors)
            {
                if (errors.Count > 0)
                    state = ScriptRunState.Faulted;
            }
            try
            {
                ChangeState(state);
                stopwatch.Stop();
            }
            finally
            {
                lock (errors)
                    Interlocked.Exchange(ref callbacks_closed, 1);
                active.TryRemove(new KeyValuePair<string, object>(request.SourceIdentity, marker));
                timeout_source.Dispose();
            }
        }

        ScriptExecutionError[] captured_errors;
        string captured_output;
        lock (errors)
            captured_errors = [.. errors];
        lock (output)
            captured_output = output.ToString();
        return new ScriptExecutionResult(
            request.SourceIdentity,
            request.FileName,
            state,
            state == ScriptRunState.Faulted,
            timed_out,
            false,
            stopwatch.Elapsed.TotalMilliseconds,
            captured_output,
            [.. captured_errors]);
    }

    private enum TerminationCause
    {
        None,
        External,
        Timeout,
        BackgroundFault,
        BackgroundFinished
    }

    static void Validate(ScriptExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        if (request.Timeout is { } timeout &&
            (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Timeout));
        }
        if (request.BackgroundDrainTimeout <= TimeSpan.Zero ||
            request.BackgroundDrainTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(request.BackgroundDrainTimeout));
        }
    }

    static ScriptExecutionResult AlreadyActive(ScriptExecutionRequest request)
    {
        var error = new ScriptExecutionError(
            "scheduling",
            typeof(InvalidOperationException).FullName!,
            $"A script execution for '{request.SourceIdentity}' is already running.",
            Path.GetFileName(request.FileName),
            null,
            null,
            null);
        var errors = new List<ScriptExecutionError> { error };
        try
        {
            request.ErrorReported?.Invoke(error);
        }
        catch (Exception adapter_error)
        {
            errors.Add(ScriptExecutionError.FromException(adapter_error, "adapter", request.FileName));
        }
        try
        {
            request.StateChanged?.Invoke(ScriptRunState.Faulted);
        }
        catch (Exception adapter_error)
        {
            errors.Add(ScriptExecutionError.FromException(adapter_error, "adapter", request.FileName));
        }
        return new ScriptExecutionResult(
            request.SourceIdentity,
            request.FileName,
            ScriptRunState.Faulted,
            true,
            false,
            true,
            0,
            "",
            [.. errors]);
    }
}
