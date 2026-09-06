using System.Globalization;
using System.Text.Json;

namespace Flazzy.ABC;

public static class ASLiteralFormatter
{
    public static string Format(object? value) => value switch
    {
        ASUndefined => "undefined",
        null => "null",
        string text => JsonSerializer.Serialize(text),
        bool boolean => boolean ? "true" : "false",
        float number => FormatFloat(number),
        ASFloat4 float4 =>
            $"float4({FormatFloat(float4.X)}, {FormatFloat(float4.Y)}, {FormatFloat(float4.Z)}, {FormatFloat(float4.W)})",
        double number when double.IsNaN(number) => "NaN",
        double number when double.IsPositiveInfinity(number) =>
            "Infinity",
        double number when double.IsNegativeInfinity(number) =>
            "-Infinity",
        double number => number.ToString(
            "R",
            CultureInfo.InvariantCulture),
        ASNamespace value_namespace =>
            $"new Namespace({JsonSerializer.Serialize(value_namespace.RuntimeName)})",
        IFormattable formattable => formattable.ToString(
            null,
            CultureInfo.InvariantCulture) ?? "null",
        _ => value.ToString() ?? "null"
    };

    public static string FormatFloat(float value) => value switch
    {
        _ when float.IsNaN(value) => "float.NaN",
        _ when float.IsPositiveInfinity(value) =>
            "float.POSITIVE_INFINITY",
        _ when float.IsNegativeInfinity(value) =>
            "float.NEGATIVE_INFINITY",
        _ => value.ToString("R", CultureInfo.InvariantCulture) + "f"
    };
}
