using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Qx.Desktop.Editor;

public enum DirectiveRole
{
    Comment,
    Keyword,
    String,
    Number,
    Type,
    Punctuation,
    Parameter
}

public sealed partial class UiDirectiveColorizer : DocumentColorizingTransformer
{
    static readonly string[] _free_text_kinds = ["title", "desc", "section", "label", "group"];

    readonly Func<DirectiveRole, IBrush?> _brush;

    public UiDirectiveColorizer(Func<DirectiveRole, IBrush?> brush) => _brush = brush ?? throw new ArgumentNullException(nameof(brush));

    protected override void ColorizeLine(DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string text = CurrentContext.Document.GetText(line);
        Match head = Directive().Match(text);
        if (!head.Success)
            return;
        int start = line.Offset;
        Paint(start + head.Groups["prefix"].Index, head.Groups["prefix"].Length, DirectiveRole.Comment);
        Paint(start + head.Groups["kind"].Index, head.Groups["kind"].Length, DirectiveRole.Keyword);
        int rest = head.Index + head.Length;
        if (_free_text_kinds.Contains(head.Groups["kind"].Value, StringComparer.OrdinalIgnoreCase))
        {
            string trailing = text[rest..].TrimEnd();
            Paint(start + rest, trailing.Length, DirectiveRole.String);
            return;
        }
        if (Name().Match(text, rest) is { Success: true } name)
            Paint(start + name.Groups["name"].Index, name.Groups["name"].Length, DirectiveRole.Parameter);
        foreach (Match quoted in Quoted().Matches(text))
            Paint(start + quoted.Index, quoted.Length, DirectiveRole.String);
        foreach (Match pair in Pair().Matches(text))
        {
            Paint(start + pair.Groups["key"].Index, pair.Groups["key"].Length + 1, DirectiveRole.Punctuation);
            Group value = pair.Groups["value"];
            if (value.Success && value.Length > 0 && text[value.Index] != '"')
                Paint(start + value.Index, value.Length, DirectiveRole.Number);
        }
        foreach (Match list in Bracketed().Matches(text))
            Paint(start + list.Index, list.Length, DirectiveRole.Type);
    }

    void Paint(int offset, int length, DirectiveRole role)
    {
        if (length <= 0 || _brush(role) is not { } brush)
            return;
        ChangeLinePart(offset, offset + length, element => element.TextRunProperties.SetForegroundBrush(brush));
    }

    [GeneratedRegex(@"^\s*(?<prefix>//\s*@ui:)(?<kind>[A-Za-z]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Directive();

    [GeneratedRegex(@"\G[^\S\n]+(?<name>[^\s""=\[\]]+)")]
    private static partial Regex Name();

    [GeneratedRegex("\"[^\"]*\"")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"(?<key>[A-Za-z_][A-Za-z0-9_]*)=(?<value>""[^""]*""|[^\s\]]+)")]
    private static partial Regex Pair();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex Bracketed();
}
