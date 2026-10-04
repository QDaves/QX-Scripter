using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Qx.Scripting.Hosting;

/// <summary>Specifies the state of a script run.</summary>
public enum ScriptRunState
{
    /// <summary>No run has started.</summary>
    Idle,
    /// <summary>The run is being prepared: the host waits for the message catalog, compiles the script and sets up its globals.</summary>
    Compiling,
    /// <summary>The script body or the handlers it registered are running.</summary>
    Running,
    /// <summary>A stop was requested and the run has not ended yet.</summary>
    Stopping,
    /// <summary>The run ended normally, including through <see cref="ScriptGlobals.Finish"/>.</summary>
    Finished,
    /// <summary>The run was stopped before it finished.</summary>
    Stopped,
    /// <summary>The run ended with at least one error, such as a compile error, an unhandled exception or a timeout.</summary>
    Faulted
}

/// <summary>Represents an error reported by a script run.</summary>
/// <param name="Stage">
/// The phase the error occurred in, such as <c>compile</c>, <c>setup</c>, <c>runtime</c>,
/// <c>background</c>, <c>timeout</c> or <c>cleanup</c>.
/// </param>
/// <param name="Type">
/// The compiler diagnostic ID, such as <c>CS0103</c>, or the full name of the exception type.
/// </param>
/// <param name="Message">The error message.</param>
/// <param name="File">The file name without its directory, or <see langword="null"/> when it is unknown.</param>
/// <param name="Line">The one-based line number, or <see langword="null"/> when it is unknown.</param>
/// <param name="Column">The one-based column number, or <see langword="null"/> when it is unknown.</param>
/// <param name="StackTrace">
/// The stack frames that carry source information, one per line, or <see langword="null"/> when
/// there are none.
/// </param>
public sealed record ScriptExecutionError(
    string Stage,
    string Type,
    string Message,
    string? File,
    int? Line,
    int? Column,
    string? StackTrace)
{
    /// <summary>
    /// Creates an error in the <c>compile</c> stage from a compiler diagnostic.
    /// </summary>
    /// <remarks>
    /// The message is formatted with the invariant culture, so it is the same English text whatever
    /// the user interface language of the machine.
    /// </remarks>
    /// <param name="diagnostic">The compiler diagnostic.</param>
    /// <param name="fallbackFile">The file to report when the diagnostic has no path, or <see langword="null"/>.</param>
    /// <returns>
    /// The error, with the diagnostic ID as its type and the mapped line and column, which are
    /// <see langword="null"/> when the diagnostic has no location.
    /// </returns>
    public static ScriptExecutionError FromDiagnostic(Diagnostic diagnostic, string? fallbackFile = null)
    {
        FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
        string? file = string.IsNullOrWhiteSpace(span.Path) ? fallbackFile : span.Path;
        int? line = diagnostic.Location == Location.None ? null : span.StartLinePosition.Line + 1;
        int? column = diagnostic.Location == Location.None ? null : span.StartLinePosition.Character + 1;

        return new ScriptExecutionError(
            "compile",
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            FileName(file),
            line,
            column,
            null);
    }

    /// <summary>
    /// Creates an error from an exception raised during a script run.
    /// </summary>
    /// <remarks>
    /// An <see cref="AggregateException"/> with a single inner exception and the wrapper exceptions
    /// of the Roslyn scripting API are unwrapped first. The location comes from the first stack
    /// frame in <paramref name="fallbackFile"/>, or else from the first frame with source
    /// information, and the stack trace lists only the frames in that file when there are any.
    /// </remarks>
    /// <param name="exception">The exception to describe.</param>
    /// <param name="stage">The phase the exception occurred in, such as <c>runtime</c> or <c>background</c>.</param>
    /// <param name="fallbackFile">
    /// The script file, used to pick the relevant stack frames and reported when no frame names a
    /// file, or <see langword="null"/>.
    /// </param>
    /// <returns>The error, with the full name of the exception type and its message.</returns>
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

    /// <summary>
    /// Formats the error as one line with its type, message and location, followed by the stack
    /// trace on the next lines when there is one.
    /// </summary>
    /// <returns>
    /// The formatted text, such as <c>System.Exception: text in script.csx:line 3, column 5</c>.
    /// </returns>
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
