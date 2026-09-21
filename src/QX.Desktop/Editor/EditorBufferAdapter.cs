using AvaloniaEdit;
using AvaloniaEdit.Document;
using Qx.Presentation.Services.Workspace;

namespace Qx.Desktop.Editor;

public sealed class EditorBufferAdapter : IScriptTextBuffer, IDisposable
{
    readonly TextEditor _editor;
    readonly ScriptDocument _document;
    bool _replacing;
    bool _disposed;

    public EditorBufferAdapter(TextEditor editor, ScriptDocument document)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _editor.TextChanged += OnTextChanged;
    }

    public string Text => _editor.Document?.Text ?? "";

    public void Replace(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_editor.Document is not { } document)
            return;
        string current = document.Text;
        int shared = Math.Min(current.Length, text.Length);
        int prefix = 0;
        while (prefix < shared && current[prefix] == text[prefix])
            prefix++;
        int suffix = 0;
        while (suffix < shared - prefix && current[current.Length - 1 - suffix] == text[text.Length - 1 - suffix])
            suffix++;
        int removed = current.Length - prefix - suffix;
        string inserted = text.Substring(prefix, text.Length - prefix - suffix);
        if (removed == 0 && inserted.Length == 0)
            return;
        _replacing = true;
        try
        {
            document.Replace(prefix, removed, inserted);
        }
        finally
        {
            _replacing = false;
        }
    }

    public void Insert(string text, int caret_offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_editor.Document is not { } document)
            return;
        int start = Math.Clamp(_editor.CaretOffset, 0, document.TextLength);
        document.Insert(start, text);
        _editor.CaretOffset = Math.Clamp(start + caret_offset, 0, document.TextLength);
    }

    public bool GoTo(int line, int column)
    {
        if (_editor.Document is not { } document)
            return false;
        int wanted = Math.Clamp(line, 1, document.LineCount);
        DocumentLine target = document.GetLineByNumber(wanted);
        int offset = Math.Clamp(target.Offset + Math.Max(0, column - 1), target.Offset, target.EndOffset);
        _editor.CaretOffset = offset;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.ScrollTo(wanted, Math.Max(1, column));
        return true;
    }

    public void Focus() => _editor.TextArea.Focus();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _editor.TextChanged -= OnTextChanged;
        _document.DetachBuffer(this);
    }

    void OnTextChanged(object? sender, EventArgs args)
    {
        if (!_replacing)
            _document.NoteEdited();
    }
}
