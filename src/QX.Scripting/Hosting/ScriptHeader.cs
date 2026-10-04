using System.Text.RegularExpressions;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Represents the directives in a script's leading comment block.
/// </summary>
/// <remarks>
/// <para>
/// <c>/// @name</c> gives the name the script goes by, and <c>/// @group</c> the library group it
/// belongs to.
/// </para>
/// <para>
/// Only the comment lines before the first line of code count, so a directive quoted further down
/// in a string or a comment does not rename or regroup the script.
/// </para>
/// </remarks>
/// <param name="Name">The declared name, or <see langword="null"/> when there is none.</param>
/// <param name="Group">The declared library group, or <see langword="null"/> when there is none.</param>
public sealed partial record ScriptHeader(string? Name, string? Group)
{
    const string NameKey = "name";
    const string GroupKey = "group";

    /// <summary>Reads the <c>/// @name</c> and <c>/// @group</c> directives from a script.</summary>
    /// <remarks>
    /// The leading block ends at the first line that is neither blank nor a <c>//</c> comment.
    /// Directive keys are matched case-insensitively, values are trimmed, and the first occurrence of
    /// each directive wins.
    /// </remarks>
    /// <param name="code">The script source.</param>
    /// <returns>The header, with <see langword="null"/> for each directive that is missing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    public static ScriptHeader Parse(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        string? name = null;
        string? group = null;
        foreach (Directive directive in Directives(code))
        {
            if (directive.Key == NameKey)
                name ??= directive.Value;
            else if (directive.Key == GroupKey)
                group ??= directive.Value;
        }
        return new ScriptHeader(name, group);
    }

    /// <summary>Sets the <c>/// @name</c> directive of a script, adding it at the top when missing.</summary>
    /// <remarks>An existing directive is replaced in place; the rest of the script is left as it is.</remarks>
    /// <param name="code">The script source.</param>
    /// <param name="name">The new name. It is trimmed.</param>
    /// <returns>The updated script source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    public static string WithName(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return With(code, NameKey, name.Trim());
    }

    /// <summary>
    /// Sets the <c>/// @group</c> directive of a script, or removes it when the group is empty.
    /// </summary>
    /// <remarks>
    /// An existing directive is replaced in place, and removing it also removes its line. A missing
    /// directive is added on the line after <c>/// @name</c>, or at the top when there is no name.
    /// </remarks>
    /// <param name="code">The script source.</param>
    /// <param name="group">The new group, trimmed, or <see langword="null"/> or whitespace to remove the directive.</param>
    /// <returns>The updated script source.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    public static string WithGroup(string code, string? group) =>
        With(code, GroupKey, string.IsNullOrWhiteSpace(group) ? null : group.Trim());

    static string With(string code, string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(code);
        string newline = code.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (Directives(code).FirstOrDefault(directive => directive.Key == key) is { } existing)
        {
            string replacement = value is null ? "" : $"/// @{key} {value}";
            int end = existing.Start + existing.Length;
            if (value is null && end < code.Length)
                end += code.AsSpan(end).StartsWith("\r\n") ? 2 : 1;
            return string.Concat(code.AsSpan(0, existing.Start), replacement, code.AsSpan(end));
        }
        if (value is null)
            return code;
        int insert = key == GroupKey && Directives(code).FirstOrDefault(directive => directive.Key == NameKey) is { } name
            ? LineEnd(code, name.Start + name.Length, newline)
            : 0;
        return string.Concat(code.AsSpan(0, insert), $"/// @{key} {value}{newline}", code.AsSpan(insert));
    }

    static int LineEnd(string code, int position, string newline) =>
        position >= code.Length ? code.Length : position + (code.AsSpan(position).StartsWith(newline) ? newline.Length : 1);

    static IEnumerable<Directive> Directives(string code)
    {
        int start = 0;
        while (start < code.Length)
        {
            int end = code.IndexOf('\n', start);
            int length = (end < 0 ? code.Length : end) - start;
            if (length > 0 && code[start + length - 1] == '\r')
                length--;
            string line = code.Substring(start, length);
            string trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
                yield break;
            Match match = DirectiveLine().Match(line);
            if (match.Success)
                yield return new Directive(match.Groups["key"].Value.ToLowerInvariant(), match.Groups["value"].Value.Trim(), start, length);
            if (end < 0)
                yield break;
            start = end + 1;
        }
    }

    sealed record Directive(string Key, string Value, int Start, int Length);

    [GeneratedRegex(@"^\s*///\s*@(?<key>name|group)[^\S\r\n]+(?<value>\S.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveLine();
}
