using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Qx.Scripting.Hosting;
using RoslynPad.Editor;
using RoslynPad.Roslyn;
using RoslynPad.Roslyn.Completion;

namespace Qx.Desktop.Editor;

public sealed partial class UiDirectiveCompletionProvider : ICodeEditorCompletionProvider
{
    static readonly UiDirective[] _directives = [.. UiSpec.Directives.OrderBy(directive => directive.Key, StringComparer.Ordinal)];

    readonly ICodeEditorCompletionProvider _inner;
    readonly TextEditor _editor;

    public UiDirectiveCompletionProvider(ICodeEditorCompletionProvider inner, TextEditor editor)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public Task<CompletionResult> GetCompletionData(int position, char? triggerChar, bool useSignatureHelp)
    {
        if (useSignatureHelp || _editor.Document is not { } document || Typed(document, position, triggerChar) is not { } typed)
            return _inner.GetCompletionData(position, triggerChar, useSignatureHelp);
        UiDirective? preselected = typed.Word.Length == 0
            ? null
            : _directives.FirstOrDefault(directive => directive.Key.StartsWith(typed.Word, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new CompletionResult(
            [.. _directives.Select(directive => (ICompletionDataEx)new DirectiveCompletion(directive, typed.Start, directive == preselected))],
            null,
            useHardSelection: true));
    }

    static (int Start, string Word)? Typed(TextDocument document, int position, char? trigger)
    {
        if (position < 0 || position > document.TextLength)
            return null;
        DocumentLine line = document.GetLineByOffset(position);
        Match typed = Directive().Match(document.GetText(line.Offset, position - line.Offset));
        if (!typed.Success)
            return null;
        bool prefixed = typed.Groups["prefix"].Success;
        string word = typed.Groups["word"].Value;
        bool opens = trigger switch
        {
            null => true,
            '@' => !prefixed && word.Length == 0,
            ':' => prefixed && word.Length == 0,
            char letter when char.IsLetter(letter) => word.Length == 1,
            _ => false
        };
        return opens ? (line.Offset + typed.Groups["at"].Index, word) : null;
    }

    [GeneratedRegex(@"^\s*//\s*(?<at>@)(?<prefix>ui:)?(?<word>\w*)$", RegexOptions.IgnoreCase)]
    private static partial Regex Directive();

    [GeneratedRegex(@"\G@(?:ui:)?\w*", RegexOptions.IgnoreCase)]
    private static partial Regex Written();

    sealed class DirectiveCompletion(UiDirective directive, int start, bool selected) : ICompletionDataEx
    {
        public IImage? Image => Glyph.Keyword.ToImageSource();

        public string Text => directive.Key;

        public object Content => directive.Key;

        public object Description => directive.Syntax + Environment.NewLine + directive.Summary;

        public double Priority => 0;

        public bool IsSelected => selected;

        public string SortText => directive.Key;

        public void Complete(TextArea text_area, ISegment completion_segment, EventArgs insertion_request)
        {
            if (insertion_request is TextInputEventArgs { Text.Length: > 0 })
                return;
            TextDocument document = text_area.Document;
            DocumentLine line = document.GetLineByOffset(start);
            Match written = Written().Match(document.GetText(line), start - line.Offset);
            int end = Math.Max(completion_segment.EndOffset, start + written.Length);
            string replacement = "@ui:" + directive.Key;
            document.Replace(start, end - start, replacement);
            text_area.Caret.Offset = start + replacement.Length;
        }
    }
}
