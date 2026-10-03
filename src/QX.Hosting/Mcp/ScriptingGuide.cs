using System.Text;
using System.Text.RegularExpressions;

namespace Qx.Hosting;

/// <summary>One page of the scripting guide.</summary>
/// <param name="Topic">The page's file name without extension, which is how it is asked for.</param>
/// <param name="Title">The page's heading.</param>
/// <param name="Summary">The page's first paragraph.</param>
/// <param name="Text">The whole page.</param>
/// <param name="ReleaseNote">Whether the page is one of the release notes rather than a guide page.</param>
internal sealed record GuidePage(string Topic, string Title, string Summary, string Text, bool ReleaseNote);

/// <summary>
/// The pages of the documentation site, embedded at build time, in the order of its table of
/// contents, so an MCP client reads the same guide the website shows.
/// </summary>
/// <remarks>
/// The pages under the release notes group of the table of contents are read by topic only, so the
/// whole guide stays about writing scripts and small enough for one read.
/// </remarks>
internal static partial class ScriptingGuide
{
    public const string Everything = "all";

    private const string Prefix = "Qx.Docs.";
    private const string ReleaseNotes = "Release notes";
    private static readonly Lazy<IReadOnlyList<GuidePage>> Pages = new(Load);

    public static string Overview()
    {
        var text = new StringBuilder()
            .AppendLine("QX Scripter scripting guide. Pass one of these topics to read its page, or \"all\" for every guide page.")
            .AppendLine();
        foreach (GuidePage page in Pages.Value.Where(page => !page.ReleaseNote))
            text.AppendLine(Line(page));
        GuidePage[] notes = [.. Pages.Value.Where(page => page.ReleaseNote)];
        if (notes.Length > 0)
        {
            text.AppendLine().AppendLine("Release notes, read by topic only:");
            foreach (GuidePage page in notes)
                text.AppendLine(Line(page));
        }
        return text.ToString().TrimEnd();
    }

    public static string Read(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        if (string.Equals(topic.Trim(), Everything, StringComparison.OrdinalIgnoreCase))
            return string.Join("\n\n", Pages.Value.Where(page => !page.ReleaseNote).Select(page => page.Text));
        return Pages.Value.FirstOrDefault(page => string.Equals(page.Topic, topic.Trim(), StringComparison.OrdinalIgnoreCase))?.Text
            ?? throw new ArgumentException(
                $"No guide topic '{topic}'. Topics: {string.Join(", ", Pages.Value.Select(page => page.Topic))}.",
                nameof(topic));
    }

    private static string Line(GuidePage page) => $"{page.Topic}: {page.Title}. {page.Summary}";

    private static IReadOnlyList<GuidePage> Load() =>
        [.. Entries(Resource("toc.yml"))
            .Select(entry => Page(entry.Topic, Plain(Resource(entry.Topic + ".md")), entry.ReleaseNote))];

    private static IEnumerable<(string Topic, bool ReleaseNote)> Entries(string toc)
    {
        string? group = null;
        foreach (string line in toc.Split('\n'))
        {
            if (GroupRegex().Match(line) is { Success: true } top)
                group = top.Groups["name"].Value;
            else if (TopicRegex().Match(line) is { Success: true } entry)
                yield return (entry.Groups["topic"].Value, group == ReleaseNotes);
        }
    }

    private static GuidePage Page(string topic, string text, bool release_note)
    {
        string[] lines = text.Split('\n');
        string title = lines.FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal))?[2..].Trim() ?? topic;
        string summary = string.Join(' ', lines
            .SkipWhile(line => !line.StartsWith("# ", StringComparison.Ordinal))
            .Skip(1)
            .SkipWhile(string.IsNullOrWhiteSpace)
            .TakeWhile(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim()));
        return new GuidePage(topic, title, summary, text, release_note);
    }

    private static string Plain(string markdown) =>
        CrossReferenceRegex().Replace(markdown.Replace("\r\n", "\n", StringComparison.Ordinal), match => $"`{match.Groups["name"].Value}`").Trim();

    private static string Resource(string file)
    {
        using Stream stream = typeof(ScriptingGuide).Assembly.GetManifestResourceStream(Prefix + file)
            ?? throw new InvalidOperationException($"Embedded resource '{Prefix + file}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^- name:\s*(?<name>.+?)\s*$")]
    private static partial Regex GroupRegex();

    [GeneratedRegex(@"href:\s*(?<topic>[\w-]+)\.md")]
    private static partial Regex TopicRegex();

    [GeneratedRegex(@"<xref:(?<name>[^>]+)>")]
    private static partial Regex CrossReferenceRegex();
}
