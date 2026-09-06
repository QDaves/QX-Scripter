using System.Text;
using System.Text.RegularExpressions;

namespace Qx.Mcp;

/// <summary>Where a script's text lives.</summary>
/// <param name="Name">The saved script's name, or null for the active editor tab.</param>
public readonly record struct ScriptTarget(string? Name)
{
    /// <summary>Whether this addresses the editor rather than a file.</summary>
    public bool IsTab => Name is null;

    /// <summary>How to name it in a message.</summary>
    public string Label => Name is null ? "the active tab" : $"'{Name}'";
}

/// <summary>One place a search matched.</summary>
/// <param name="Line">The one-based line number.</param>
/// <param name="Text">The line itself.</param>
public readonly record struct ScriptHit(int Line, string Text);

/// <summary>A named region of a script and where it sits.</summary>
/// <param name="Kind">What it is: a directive, a function, a type, a handler.</param>
/// <param name="Name">Its name.</param>
/// <param name="Line">The one-based line it starts on.</param>
/// <param name="EndLine">The one-based line it ends on, equal to <paramref name="Line"/> when it is one line.</param>
/// <param name="Signature">The declaring line, trimmed.</param>
public readonly record struct ScriptRegion(string Kind, string Name, int Line, int EndLine, string Signature);

/// <summary>
/// Reading and changing a script without moving the whole file across the wire.
/// </summary>
/// <remarks>
/// Every operation is line-oriented and one-based, because that is what a compiler diagnostic hands
/// an agent. Edits are all-or-nothing: a patch that cannot place one of its changes exactly changes
/// nothing, so a half-applied file never reaches the editor.
/// </remarks>
public static partial class McpScriptEditing
{
    /// <summary>The most lines one read returns before it has to be paged.</summary>
    public const int MaxLines = 2000;

    /// <summary>The most hits one search returns.</summary>
    public const int MaxHits = 200;

    /// <summary>
    /// Numbers a range of lines the way a diagnostic does.
    /// </summary>
    /// <param name="code">The whole script.</param>
    /// <param name="offset">The one-based first line, clamped into the file.</param>
    /// <param name="limit">How many lines at most.</param>
    public static string Read(string code, int offset, int limit)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length == 0)
            return "(empty)";
        string[] lines = Split(code);

        int first = Math.Clamp(offset <= 0 ? 1 : offset, 1, lines.Length);
        int count = Math.Clamp(limit <= 0 ? MaxLines : limit, 1, MaxLines);
        int last = Math.Min(lines.Length, first + count - 1);

        var text = new StringBuilder();
        text.Append(lines.Length).Append(" lines total");
        if (first > 1 || last < lines.Length)
            text.Append(", showing ").Append(first).Append('-').Append(last);
        text.Append('\n');

        int width = last.ToString().Length;
        for (int line = first; line <= last; line++)
            text.Append(line.ToString().PadLeft(width)).Append("  ").Append(lines[line - 1]).Append('\n');

        if (last < lines.Length)
            text.Append("... ").Append(lines.Length - last).Append(" more lines, read on from ").Append(last + 1);
        return text.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// The lines around one line, which is what a compiler error points at.
    /// </summary>
    /// <param name="code">The whole script.</param>
    /// <param name="line">The one-based line to centre on.</param>
    /// <param name="context">How many lines to show either side.</param>
    public static string ReadAround(string code, int line, int context)
    {
        ArgumentNullException.ThrowIfNull(code);
        int span = Math.Max(0, context);
        return Read(code, Math.Max(1, line - span), span * 2 + 1);
    }

    /// <summary>
    /// Every line matching a pattern, with the lines around it.
    /// </summary>
    /// <param name="code">The whole script.</param>
    /// <param name="pattern">A literal, or a regular expression when <paramref name="regex"/>.</param>
    /// <param name="regex">Whether the pattern is a regular expression.</param>
    /// <param name="ignoreCase">Whether to match without regard to case.</param>
    /// <param name="context">How many lines to show either side of each hit.</param>
    public static IReadOnlyList<ScriptHit> Find(
        string code,
        string pattern,
        bool regex,
        bool ignoreCase,
        int context = 0)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrEmpty(pattern);

        string[] lines = Split(code);
        var hits = new List<ScriptHit>();
        Regex? expression = regex
            ? new Regex(pattern, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None, TimeSpan.FromSeconds(2))
            : null;
        StringComparison comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var shown = new HashSet<int>();
        for (int line = 1; line <= lines.Length && hits.Count < MaxHits; line++)
        {
            bool matched = expression is null
                ? lines[line - 1].Contains(pattern, comparison)
                : expression.IsMatch(lines[line - 1]);
            if (!matched)
                continue;

            for (int around = Math.Max(1, line - context); around <= Math.Min(lines.Length, line + context); around++)
            {
                if (shown.Add(around))
                    hits.Add(new ScriptHit(around, lines[around - 1]));
            }
        }

        hits.Sort((a, b) => a.Line.CompareTo(b.Line));
        return hits;
    }

    /// <summary>
    /// The named regions of a script: its panel directives, its functions, its types and the
    /// handlers it registers.
    /// </summary>
    /// <remarks>
    /// A script is top-level code, so there is no syntax tree to walk without compiling it. This
    /// reads what a person would look for and reports where it is, which is enough to jump to a
    /// place instead of reading the file to find it.
    /// </remarks>
    /// <param name="code">The whole script.</param>
    public static IReadOnlyList<ScriptRegion> Outline(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string[] lines = Split(code);
        var regions = new List<ScriptRegion>();

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            string trimmed = line.Trim();
            int number = index + 1;

            if (trimmed.StartsWith("//@ui:", StringComparison.Ordinal))
            {
                string rest = trimmed[6..].Trim();
                string kind = rest.Split(' ', 2)[0];
                regions.Add(new ScriptRegion("directive", kind, number, number, trimmed));
                continue;
            }

            if (Declaration(trimmed) is { } declaration)
            {
                regions.Add(new ScriptRegion(
                    declaration.Kind,
                    declaration.Name,
                    number,
                    EndOfBlock(lines, index),
                    trimmed));
                continue;
            }

            if (HandlerRegistration(trimmed) is { } handler)
                regions.Add(new ScriptRegion("handler", handler, number, EndOfBlock(lines, index), trimmed));
        }

        return regions;
    }

    /// <summary>
    /// One named region with its lines, or null when the script has no such name.
    /// </summary>
    /// <param name="code">The whole script.</param>
    /// <param name="name">The region's name, matched without regard to case.</param>
    public static (ScriptRegion Region, string Text)? Region(string code, string name)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrEmpty(name);

        string[] lines = Split(code);
        foreach (ScriptRegion region in Outline(code))
        {
            if (!string.Equals(region.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            var text = new StringBuilder();
            int width = region.EndLine.ToString().Length;
            for (int line = region.Line; line <= region.EndLine; line++)
                text.Append(line.ToString().PadLeft(width)).Append("  ").Append(lines[line - 1]).Append('\n');
            return (region, text.ToString().TrimEnd('\n'));
        }
        return null;
    }

    /// <summary>
    /// Applies exact replacements, all of them or none.
    /// </summary>
    /// <remarks>
    /// A replacement that is not found, or found more than once without being told to replace every
    /// occurrence, is an error rather than a guess: the point of editing by text is that the agent
    /// named something it had actually read.
    /// </remarks>
    /// <param name="code">The whole script.</param>
    /// <param name="edits">What to replace with what.</param>
    /// <param name="report">What each edit did, in order.</param>
    /// <returns>The changed script.</returns>
    /// <exception cref="ArgumentException">An edit could not be placed exactly.</exception>
    public static string Patch(
        string code,
        IReadOnlyList<(string Old, string New, bool All)> edits,
        out IReadOnlyList<string> report)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0)
            throw new ArgumentException("Name at least one edit.", nameof(edits));

        string updated = code;
        var lines = new List<string>();
        for (int index = 0; index < edits.Count; index++)
        {
            (string old, string replacement, bool all) = edits[index];
            if (old.Length == 0)
                throw new ArgumentException($"Edit {index + 1} has nothing to replace.", nameof(edits));

            int occurrences = Count(updated, old);
            if (occurrences == 0)
            {
                throw new ArgumentException(
                    $"Edit {index + 1} did not match anything. Read the lines again; the text has to be exactly as it stands, indentation included.",
                    nameof(edits));
            }
            if (occurrences > 1 && !all)
            {
                throw new ArgumentException(
                    $"Edit {index + 1} matches {occurrences} places. Give more surrounding text, or set all to true to change every one.",
                    nameof(edits));
            }

            int at = LineOf(updated, updated.IndexOf(old, StringComparison.Ordinal));
            updated = all
                ? updated.Replace(old, replacement, StringComparison.Ordinal)
                : ReplaceFirst(updated, old, replacement);
            lines.Add(all && occurrences > 1
                ? $"edit {index + 1}: {occurrences} places, first at line {at}"
                : $"edit {index + 1}: line {at}");
        }

        report = lines;
        return updated;
    }

    /// <summary>
    /// Replaces a run of lines, or inserts before one when the run is empty.
    /// </summary>
    /// <param name="code">The whole script.</param>
    /// <param name="first">The one-based first line to replace.</param>
    /// <param name="last">The one-based last line to replace, or <paramref name="first"/> - 1 to insert.</param>
    /// <param name="replacement">What goes there; empty deletes the run.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside the script.</exception>
    public static string ReplaceLines(string code, int first, int last, string replacement)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(replacement);

        string[] lines = Split(code);
        if (first < 1 || first > lines.Length + 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(first), first, $"The script has {lines.Length} lines.");
        }
        if (last < first - 1 || last > lines.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(last), last, $"The script has {lines.Length} lines, and last may be first - 1 to insert.");
        }

        var updated = new List<string>(lines[..(first - 1)]);
        if (replacement.Length > 0)
            updated.AddRange(Split(replacement));
        updated.AddRange(lines[last..]);
        return string.Join("\n", updated);
    }

    /// <summary>How many lines a script has.</summary>
    /// <param name="code">The whole script.</param>
    public static int LineCount(string code) => Split(code).Length;

    private static string[] Split(string code) =>
        code.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static int Count(string text, string value)
    {
        int found = 0;
        for (int at = text.IndexOf(value, StringComparison.Ordinal);
             at >= 0;
             at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            found++;
        }
        return found;
    }

    private static string ReplaceFirst(string text, string old, string replacement)
    {
        int at = text.IndexOf(old, StringComparison.Ordinal);
        return text[..at] + replacement + text[(at + old.Length)..];
    }

    private static int LineOf(string text, int index) =>
        index < 0 ? 0 : text.AsSpan(0, index).Count('\n') + 1;

    private static (string Kind, string Name)? Declaration(string line)
    {
        Match type = TypeDeclaration().Match(line);
        if (type.Success)
            return (type.Groups[1].Value, type.Groups[2].Value);

        Match method = MethodDeclaration().Match(line);
        if (method.Success && !Keywords.Contains(method.Groups[1].Value))
            return ("function", method.Groups[1].Value);

        return null;
    }

    private static string? HandlerRegistration(string line)
    {
        Match match = Registration().Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The line a block ends on, or the same line when it opens none.
    /// </summary>
    /// <remarks>
    /// Braces are counted rather than parsed, so a brace inside a string or a comment throws the
    /// count off. Reporting the declaring line alone in that case is better than reporting a span
    /// that runs to the end of the file.
    /// </remarks>
    private static int EndOfBlock(string[] lines, int index)
    {
        int depth = 0;
        bool opened = false;
        for (int at = index; at < lines.Length; at++)
        {
            foreach (char character in lines[at])
            {
                if (character == '{')
                {
                    depth++;
                    opened = true;
                }
                else if (character == '}')
                {
                    depth--;
                    if (opened && depth == 0)
                        return at + 1;
                }
            }
            if (!opened && lines[at].TrimEnd().EndsWith(';'))
                return at + 1;
        }
        return index + 1;
    }

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "do", "fixed"
    };

    [GeneratedRegex(@"^\s*(?:public\s+|private\s+|internal\s+|sealed\s+|static\s+|partial\s+|abstract\s+)*(class|record|struct|enum|interface)\s+(\w+)")]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"^\s*(?:public\s+|private\s+|internal\s+|static\s+|async\s+)*(?:[\w<>\[\],\.\?]+\s+)(\w+)\s*\([^)]*\)\s*(?:=>|\{|$)")]
    private static partial Regex MethodDeclaration();

    [GeneratedRegex(@"^\s*(On\w+|Ui\.OnClick)\s*\(")]
    private static partial Regex Registration();
}
