using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Qx.Scripting;

public enum ScriptRunState
{
    Idle,
    Compiling,
    Running,
    Stopping,
    Finished,
    Stopped,
    Faulted
}

public sealed record ScriptExecutionError(
    string Stage,
    string Type,
    string Message,
    string? File,
    int? Line,
    int? Column,
    string? StackTrace)
{
    public static ScriptExecutionError FromDiagnostic(Diagnostic diagnostic, string? fallbackFile = null)
    {
        FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
        string? file = string.IsNullOrWhiteSpace(span.Path) ? fallbackFile : span.Path;
        int? line = diagnostic.Location == Location.None ? null : span.StartLinePosition.Line + 1;
        int? column = diagnostic.Location == Location.None ? null : span.StartLinePosition.Character + 1;

        return new ScriptExecutionError(
            "compile",
            diagnostic.Id,
            diagnostic.GetMessage(),
            FileName(file),
            line,
            column,
            null);
    }

    public static ScriptExecutionError FromException(Exception exception, string stage, string? fallbackFile = null)
    {
        Exception error = Unwrap(exception);
        var trace = new StackTrace(error, true);
        StackFrame[] frames = trace.GetFrames() ?? [];
        string? fallbackName = FileName(fallbackFile);
        StackFrame? source = frames.FirstOrDefault(frame =>
                fallbackName is not null &&
                string.Equals(FileName(frame.GetFileName()), fallbackName, StringComparison.OrdinalIgnoreCase))
            ?? frames.FirstOrDefault(frame => frame.GetFileLineNumber() > 0 || !string.IsNullOrWhiteSpace(frame.GetFileName()));

        string? file = FileName(source?.GetFileName()) ?? FileName(fallbackFile);
        int sourceLine = source?.GetFileLineNumber() ?? 0;
        int sourceColumn = source?.GetFileColumnNumber() ?? 0;
        string? sourceTrace = SourceTrace(frames, fallbackName);

        return new ScriptExecutionError(
            stage,
            error.GetType().FullName ?? error.GetType().Name,
            error.Message,
            file,
            sourceLine > 0 ? sourceLine : null,
            sourceColumn > 0 ? sourceColumn : null,
            sourceTrace);
    }

    public string Format()
    {
        string location = File is null
            ? ""
            : Line is null
                ? $" in {File}"
                : Column is null
                    ? $" in {File}:line {Line}"
                    : $" in {File}:line {Line}, column {Column}";
        string text = $"{Type}: {Message}{location}";
        return string.IsNullOrWhiteSpace(StackTrace) ? text : $"{text}{Environment.NewLine}{StackTrace}";
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is AggregateException { InnerExceptions.Count: 1 } aggregate)
            exception = aggregate.InnerExceptions[0];
        while (exception.InnerException is not null &&
               exception.GetType().Namespace?.StartsWith("Microsoft.CodeAnalysis.Scripting", StringComparison.Ordinal) == true)
            exception = exception.InnerException;
        return exception;
    }

    private static string? SourceTrace(IEnumerable<StackFrame> sourceFrames, string? preferredFile)
    {
        List<StackFrame> allFrames = sourceFrames.ToList();
        List<StackFrame> selectedFrames = preferredFile is null
            ? allFrames
            : allFrames.Where(frame =>
                string.Equals(FileName(frame.GetFileName()), preferredFile, StringComparison.OrdinalIgnoreCase)).ToList();
        if (selectedFrames.Count == 0)
            selectedFrames = allFrames;

        List<string> frames = [];
        foreach (StackFrame frame in selectedFrames)
        {
            string? file = FileName(frame.GetFileName());
            int line = frame.GetFileLineNumber();
            if (file is null && line == 0)
                continue;

            string method = frame.GetMethod()?.ToString() ?? "<script>";
            frames.Add(line > 0 ? $"at {method} in {file}:line {line}" : $"at {method} in {file}");
        }
        return frames.Count == 0 ? null : string.Join(Environment.NewLine, frames);
    }

    private static string? FileName(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
}

public sealed record ScriptExecutionSnapshot(
    ScriptRunState State,
    bool Faulted,
    double RuntimeMs,
    string Output,
    ImmutableArray<ScriptExecutionError> Errors);

public sealed class ScriptFinishedException : OperationCanceledException;
