using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using RoslynPad.Editor;

namespace Qx.Ui;

public sealed class DarkEditorColors : ClassificationHighlightColors
{
    public DarkEditorColors()
    {
        DefaultBrush = Make("#D4D4D4");
        KeywordBrush = Make("#569CD6");
        PreprocessorKeywordBrush = Make("#9B9B9B");
        TypeBrush = Make("#4EC9B0");
        StaticSymbolBrush = Make("#4EC9B0");
        MethodBrush = Make("#DCDCAA");
        ParameterBrush = Make("#9CDCFE");
        StringBrush = Make("#CE9178");
        CommentBrush = Make("#6A9955");
        XmlCommentBrush = Make("#6A9955");
        BraceMatchingBrush = Make(null, "#3A3D41");
    }

    private static HighlightingColor Make(string? foreground, string? background = null) => new()
    {
        Foreground = foreground is null ? null : new SolidHighlightBrush(Parse(foreground)),
        Background = background is null ? null : new SolidHighlightBrush(Parse(background))
    };

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
}

internal sealed class SolidHighlightBrush : HighlightingBrush
{
    private readonly SolidColorBrush _brush;

    public SolidHighlightBrush(Color color)
    {
        _brush = new SolidColorBrush(color);
        _brush.Freeze();
    }

    public override Brush GetBrush(ITextRunConstructionContext context) => _brush;

    public override string ToString() => _brush.Color.ToString();
}
