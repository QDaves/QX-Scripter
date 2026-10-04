using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using RoslynPad.Editor;

namespace Qx.Desktop.Editor;

public sealed class GuardedCompletionProvider : ICodeEditorCompletionProvider
{
    readonly ICodeEditorCompletionProvider _inner;
    readonly TextEditor _editor;

    public GuardedCompletionProvider(ICodeEditorCompletionProvider inner, TextEditor editor)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public async Task<CompletionResult> GetCompletionData(int position, char? triggerChar, bool useSignatureHelp)
    {
        ITextSourceVersion? requested = _editor.Document?.Version;
        CompletionResult result = await _inner.GetCompletionData(position, triggerChar, useSignatureHelp);
        if (requested is null || result.CompletionData is not { Count: > 0 } items)
            return result;
        if (!CompletionSnapshot.IsIntact(requested, _editor.Document?.Version, position))
            return new CompletionResult(null, result.OverloadProvider, result.UseHardSelection);
        return new CompletionResult(
            [.. items.Select(item => (ICompletionDataEx)new GuardedCompletionData(item, requested, position))],
            result.OverloadProvider,
            result.UseHardSelection);
    }

    sealed class GuardedCompletionData(ICompletionDataEx inner, ITextSourceVersion requested, int position) : ICompletionDataEx
    {
        public IImage Image => inner.Image;

        public string Text => inner.Text;

        public object Content => inner.Content;

        public object Description => inner.Description;

        public double Priority => inner.Priority;

        public bool IsSelected => inner.IsSelected;

        public string SortText => inner.SortText;

        public void Complete(TextArea text_area, ISegment completion_segment, EventArgs insertion_request)
        {
            if (CompletionSnapshot.IsIntact(requested, text_area.Document?.Version, position))
                inner.Complete(text_area, completion_segment, insertion_request);
        }
    }

    static class CompletionSnapshot
    {
        public static bool IsIntact(ITextSourceVersion requested, ITextSourceVersion? current, int position)
        {
            if (current is null || !requested.BelongsToSameDocumentAs(current))
                return false;
            int growth = 0;
            foreach (TextChangeEventArgs change in requested.GetChangesTo(current))
            {
                if (change.Offset < position)
                    return false;
                growth += change.InsertionLength - change.RemovalLength;
            }
            return growth >= 0;
        }
    }
}
