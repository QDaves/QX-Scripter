using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Qx.Scripting;
using RoslynPad.Roslyn;
using RoslynPad.Roslyn.Completion;

namespace Qx.Ui;

/// <summary>
/// Draws a script member with the same glyph the completion list uses for it.
/// </summary>
/// <remarks>
/// The images come from the editor's own glyph service rather than from an icon font, so a member
/// looks identical in the library and in the popup that appears while typing. Resolved once per
/// glyph and held, because the list virtualises and would otherwise ask for the same handful of
/// images on every scroll.
/// </remarks>
public sealed class GlyphToImageConverter : IValueConverter
{
    private static readonly Dictionary<ScriptApiGlyph, ImageSource?> Cache = [];
    private static readonly object Sync = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ScriptApiGlyph glyph)
            return null;

        lock (Sync)
        {
            if (Cache.TryGetValue(glyph, out ImageSource? cached))
                return cached;

            ImageSource? image = null;
            try
            {
                image = Roslyn(glyph).ToImageSource();
            }
            catch
            {
                // An editor that never loaded leaves the row without a glyph rather than without a
                // row: the name and the type carry the meaning, the icon only speeds up scanning.
            }

            Cache[glyph] = image;
            return image;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Glyph Roslyn(ScriptApiGlyph glyph) => glyph switch
    {
        ScriptApiGlyph.Keyword => Glyph.Keyword,
        ScriptApiGlyph.Structure => Glyph.StructurePublic,
        ScriptApiGlyph.Interface => Glyph.InterfacePublic,
        ScriptApiGlyph.Enum => Glyph.EnumPublic,
        ScriptApiGlyph.Delegate => Glyph.DelegatePublic,
        _ => Glyph.ClassPublic
    };
}
