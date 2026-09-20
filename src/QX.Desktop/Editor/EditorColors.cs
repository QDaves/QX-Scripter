using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using RoslynPad.Editor;

namespace Qx.Desktop.Editor;

public sealed class QxEditorColors : ClassificationHighlightColors
{
    public QxEditorColors(Func<string, Color?> token)
    {
        ArgumentNullException.ThrowIfNull(token);
        DefaultBrush = Ink(token, "QxEditorForegroundBrush", DefaultBrush);
        TypeBrush = Ink(token, "QxSyntaxTypeBrush", TypeBrush);
        MethodBrush = Ink(token, "QxSyntaxMethodBrush", MethodBrush);
        CommentBrush = Ink(token, "QxSyntaxCommentBrush", CommentBrush);
        XmlCommentBrush = Ink(token, "QxSyntaxCommentBrush", XmlCommentBrush);
        KeywordBrush = Ink(token, "QxSyntaxKeywordBrush", KeywordBrush);
        PreprocessorKeywordBrush = Ink(token, "QxSyntaxPreprocessorBrush", PreprocessorKeywordBrush);
        StringBrush = Ink(token, "QxSyntaxStringBrush", StringBrush);
        ParameterBrush = Ink(token, "QxSyntaxParameterBrush", ParameterBrush);
        StaticSymbolBrush = new HighlightingColor { FontWeight = FontWeight.SemiBold };
        BraceMatchingBrush = token("QxEditorBraceMatchBrush") is { } match
            ? new HighlightingColor { Background = new SimpleHighlightingBrush(match) }
            : BraceMatchingBrush;
    }

    static HighlightingColor Ink(Func<string, Color?> token, string key, HighlightingColor fallback) =>
        token(key) is { } color ? new HighlightingColor { Foreground = new SimpleHighlightingBrush(color) } : fallback;
}
