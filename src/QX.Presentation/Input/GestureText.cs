using System.Globalization;
using System.Text;

namespace Qx.Presentation.Input;

public static class GestureText
{
    const string AppleModifiers = "⌃⌥⇧⌘";

    static readonly string[] _windows_modifiers = ["Ctrl+", "Alt+", "Shift+"];

    public static string Describe(KeyChord chord, bool apple_glyphs)
    {
        ChordModifiers modifiers = chord.Modifiers;
        var text = new StringBuilder();
        if (apple_glyphs)
        {
            if (modifiers.HasFlag(ChordModifiers.Control))
                text.Append('⌃');
            if (modifiers.HasFlag(ChordModifiers.Alt))
                text.Append('⌥');
            if (modifiers.HasFlag(ChordModifiers.Shift))
                text.Append('⇧');
            if (modifiers.HasFlag(ChordModifiers.Primary))
                text.Append('⌘');
            return text.Append(KeyName(chord.Key)).ToString();
        }
        if (modifiers.HasFlag(ChordModifiers.Control) || modifiers.HasFlag(ChordModifiers.Primary))
            text.Append("Ctrl+");
        if (modifiers.HasFlag(ChordModifiers.Alt))
            text.Append("Alt+");
        if (modifiers.HasFlag(ChordModifiers.Shift))
            text.Append("Shift+");
        return text.Append(KeyName(chord.Key)).ToString();
    }

    public static IReadOnlyList<string> Parts(string gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return [];
        var parts = new List<string>();
        int at = 0;
        while (at < gesture.Length && AppleModifiers.Contains(gesture[at], StringComparison.Ordinal))
        {
            parts.Add(gesture[at].ToString());
            at++;
        }
        string rest = gesture[at..];
        if (parts.Count == 0)
        {
            foreach (string modifier in _windows_modifiers)
            {
                if (!rest.StartsWith(modifier, StringComparison.Ordinal))
                    continue;
                parts.Add(modifier[..^1]);
                rest = rest[modifier.Length..];
            }
        }
        if (rest.Length > 0)
            parts.Add(rest);
        return parts;
    }

    static string KeyName(ChordKey key) => key switch
    {
        >= ChordKey.D0 and <= ChordKey.D9 => ((int)(key - ChordKey.D0)).ToString(CultureInfo.InvariantCulture),
        ChordKey.Escape => "Esc",
        ChordKey.NumPad0 => "Num 0",
        ChordKey.Add => "Num +",
        ChordKey.Subtract => "Num -",
        _ => key.ToString()
    };
}
