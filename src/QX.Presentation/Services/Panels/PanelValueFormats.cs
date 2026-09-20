using System.Globalization;
using System.Text;

namespace Qx.Presentation.Services.Panels;

public static class PanelValueFormats
{
    public const int MaxOutputLength = 1_000_000;
    public const string TruncatedMarker = "[earlier output truncated]\n";
    public const double DefaultOutputHeight = 160;
    public const double DefaultTableHeight = 220;
    public const double DefaultSpacerHeight = 12;
    public const double DefaultRowGap = 16;
    public const string DefaultColor = "#6E8BFF";

    static readonly NumberFormatInfo _comma_decimal = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static double Extent(double? asked, double fallback) =>
        asked is { } value && double.IsFinite(value) && value > 0 ? value : fallback;

    public static double Bound(double? asked, double fallback) =>
        asked is { } value && double.IsFinite(value) ? value : fallback;

    public static string Bool(bool value) => value ? "true" : "false";

    public static bool ParseBool(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    public static string Number(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Trim().Length == 0)
            return "";
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            double.TryParse(text, NumberStyles.Float, _comma_decimal, out value))
            return value.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    public static string Whole(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Trim().Length == 0)
            return "";
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            return value.ToString(CultureInfo.InvariantCulture);
        return text;
    }

    public static string Slider(double value, bool whole) =>
        whole
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    public static string Cell(string? cell)
    {
        if (string.IsNullOrEmpty(cell))
            return "";
        if (cell.AsSpan().IndexOfAny('\t', '\r', '\n') < 0)
            return cell;
        return cell.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    public static bool AppendLine(StringBuilder output, string text)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(text);
        output.Append(text.ReplaceLineEndings("\n")).Append('\n');
        if (output.Length <= MaxOutputLength)
            return false;
        int remove = output.Length - MaxOutputLength + TruncatedMarker.Length;
        while (remove < output.Length && output[remove] != '\n')
            remove++;
        if (remove < output.Length)
            remove++;
        output.Remove(0, remove);
        output.Insert(0, TruncatedMarker);
        return true;
    }

    public static string DirectiveKey(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var key = new StringBuilder();
        foreach (string line in code.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal) && trimmed.Contains("@ui:", StringComparison.OrdinalIgnoreCase))
                key.Append(trimmed).Append('\n');
        }
        return key.ToString();
    }
}
