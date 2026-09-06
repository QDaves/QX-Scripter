namespace Qx.Ui;

/// <summary>What a console line is, which decides how it is coloured.</summary>
/// <remarks>
/// Set by whoever writes the line, never guessed from its text. The host knows when it is
/// reporting a failure; a script's own <c>Log</c> is always <see cref="Info"/>, because a script
/// that happens to print the word "failed" is not the host reporting an error.
/// </remarks>
public enum OutputLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// One line in a tab's console.
/// </summary>
/// <remarks>
/// A record rather than a string so the view can bind a timestamp and a colour without parsing
/// anything back out, and so the buffer can be capped by counting lines instead of characters.
/// </remarks>
/// <param name="Text">The line as written, without a trailing newline.</param>
/// <param name="Level">What kind of line it is.</param>
/// <param name="At">When it was written, for the timestamp column.</param>
public sealed record OutputLine(string Text, OutputLevel Level, DateTime At)
{
    /// <summary>The timestamp column, pre-formatted so the binding does no work per frame.</summary>
    public string Time { get; } = At.ToString("HH:mm:ss");

    /// <summary>What lands on the clipboard: the timestamp is chrome, the text is the line.</summary>
    public override string ToString() => Text;
}
