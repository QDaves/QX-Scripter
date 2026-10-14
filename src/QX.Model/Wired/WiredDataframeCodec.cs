using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Qx.Model.Wired;

public enum WiredDataframeSource { Number, VariableValue, VariableText, Message }
public enum WiredDataframeDestination { Placeholder, ContextVariable }

public sealed record WiredSendDataframeRow(string Key, WiredDataframeSource Source = WiredDataframeSource.Number,
    int Number = 0, WiredSourceDomain Target = WiredSourceDomain.Furniture, string VariableId = "0", string Text = "");

public sealed record WiredReceiveDataframeRow(string Key, WiredDataframeDestination Destination = WiredDataframeDestination.Placeholder,
    string Placeholder = "", string VariableId = "0");

internal static class WiredDataframeCodec
{
    internal sealed record Payload(int[] Integers, string Text, string[] Variables);

    public static IReadOnlyList<WiredSendDataframeRow> ReadSend(WiredConfigWrite value)
    {
        int count = count_of(value, 3);
        var text = text_of(value.StringParam, count);
        var rows = new WiredSendDataframeRow[count];
        for (int i = 0; i < count; i++)
            rows[i] = new(text[i][0], (WiredDataframeSource)value.IntParams[1 + i * 3], value.IntParams[2 + i * 3],
                (WiredSourceDomain)value.IntParams[3 + i * 3], value.VariableIds[i], text[i][1]);
        return Array.AsReadOnly(rows);
    }

    public static IReadOnlyList<WiredReceiveDataframeRow> ReadReceive(WiredConfigWrite value)
    {
        int count = count_of(value, 1);
        var text = text_of(value.StringParam, count);
        var rows = new WiredReceiveDataframeRow[count];
        for (int i = 0; i < count; i++)
            rows[i] = new(text[i][0], (WiredDataframeDestination)value.IntParams[1 + i], text[i][1], value.VariableIds[i]);
        return Array.AsReadOnly(rows);
    }

    private static int count_of(WiredConfigWrite value, int stride)
    {
        if (value.IntParams.Count == 0 && value.VariableIds.Count == 0 && value.StringParam.Length == 0) return 0;
        if (value.IntParams.Count == 0) throw new InvalidDataException("Missing dataframe row count.");
        int count = value.IntParams[0];
        if (count is < 0 or > 12 || value.IntParams.Count != 1 + stride * count || value.VariableIds.Count != count)
            throw new InvalidDataException("Dataframe row count does not match its numeric or variable payload.");
        return count;
    }

    private static string[][] text_of(string text, int count)
    {
        if (text.Length == 0 && count == 0) return [];
        string[][] rows;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The dataframe text payload must be an array.");
            rows = document.RootElement.EnumerateArray().Select(row =>
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 2 ||
                    row.EnumerateArray().Any(cell => cell.ValueKind != JsonValueKind.String))
                    throw new InvalidDataException("Dataframe text rows must contain exactly a key and text value.");
                return row.EnumerateArray().Select(cell => cell.GetString()!).ToArray();
            }).ToArray();
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid dataframe text payload.", error); }
        if (rows.Length != count) throw new InvalidDataException("The dataframe text row count does not match.");
        return rows;
    }

    public static Payload WriteSend(IReadOnlyList<WiredSendDataframeRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > 12) throw new ArgumentOutOfRangeException(nameof(rows), "A dataframe supports at most 12 rows.");
        var integers = new List<int> { rows.Count };
        var text = new List<string[]>();
        var variables = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            string key = key_of(row.Key);
            if (!keys.Add(key)) throw new ArgumentException("Dataframe keys must be unique.", nameof(rows));
            if (!Enum.IsDefined(row.Source) || !Enum.IsDefined(row.Target)) throw new ArgumentException("Unknown dataframe source or target.", nameof(rows));
            ArgumentNullException.ThrowIfNull(row.Text);
            ArgumentNullException.ThrowIfNull(row.VariableId);
            if ((row.Source is WiredDataframeSource.VariableValue or WiredDataframeSource.VariableText) && (row.VariableId is "" or "0" or "n"))
                throw new ArgumentException("Variable rows require a source variable.", nameof(rows));
            if (Encoding.UTF8.GetByteCount(row.Text) > 1000) throw new ArgumentException("A dataframe message exceeds 1000 UTF-8 bytes.", nameof(rows));
            integers.AddRange([(int)row.Source, row.Number, (int)row.Target]);
            text.Add([key, row.Text]);
            variables.Add(row.VariableId);
        }
        return payload(integers, text, variables);
    }

    public static Payload WriteReceive(IReadOnlyList<WiredReceiveDataframeRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > 12) throw new ArgumentOutOfRangeException(nameof(rows), "A dataframe supports at most 12 rows.");
        var integers = new List<int> { rows.Count };
        var text = new List<string[]>();
        var variables = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var destinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            string key = key_of(row.Key);
            if (!keys.Add(key)) throw new ArgumentException("Dataframe keys must be unique.", nameof(rows));
            if (!Enum.IsDefined(row.Destination)) throw new ArgumentException("Unknown dataframe destination.", nameof(rows));
            ArgumentNullException.ThrowIfNull(row.Placeholder);
            ArgumentNullException.ThrowIfNull(row.VariableId);
            string destination;
            if (row.Destination == WiredDataframeDestination.Placeholder)
            {
                destination = key_of(row.Placeholder);
                if (destination == "trigger_text") throw new ArgumentException("trigger_text is reserved.", nameof(rows));
                destination = "text:" + destination;
            }
            else
            {
                if (row.VariableId is "" or "0" or "n") throw new ArgumentException("A context destination variable is required.", nameof(rows));
                destination = "variable:" + row.VariableId;
            }
            if (!destinations.Add(destination)) throw new ArgumentException("Dataframe destinations must be unique.", nameof(rows));
            integers.Add((int)row.Destination);
            text.Add([key, row.Destination == WiredDataframeDestination.Placeholder ? destination[5..] : row.Placeholder]);
            variables.Add(row.VariableId);
        }
        return payload(integers, text, variables);
    }

    private static Payload payload(List<int> integers, List<string[]> text, List<string> variables)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var row in text)
            {
                writer.WriteStartArray();
                foreach (string cell in row) writer.WriteStringValue(cell);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
        string encoded = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
        if (Encoding.UTF8.GetByteCount(encoded) > 3000 || string.Join('|', text.Select(row => row[0])).Length > 400 ||
            string.Join('|', variables).Length > 240)
            throw new ArgumentException("The dataframe exceeds the client storage limits.");
        return new(integers.ToArray(), encoded, variables.ToArray());
    }

    private static string key_of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string key = value.Replace('_', ' ').Trim(Enumerable.Range(0, 33).Select(i => (char)i).ToArray()).Replace(' ', '_').ToLowerInvariant();
        if (key.Length is < 1 or > 40 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')))
            throw new ArgumentException("Dataframe names must contain 1..40 letters, digits or underscores.");
        return key;
    }
}
