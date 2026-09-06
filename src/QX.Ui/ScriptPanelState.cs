using System.Text;

namespace Qx.Ui;

public sealed class ScriptPanelState
{
    private const int MaxOutputLength = 1_000_000;
    private const string TruncatedOutput = "[earlier output truncated]\n";
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, StringBuilder> _outputs = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;

    public void SaveValues(IReadOnlyDictionary<string, string> values)
    {
        _values.Clear();
        foreach ((string name, string value) in values)
            _values[name] = value;
    }

    public void ClearOutputs() => _outputs.Clear();

    /// <summary>
    /// Replaces one control's saved value.
    /// </summary>
    /// <remarks>
    /// Written to when a script changes a value itself, so switching away from the panel and back
    /// shows what the script set rather than what the user last typed.
    /// </remarks>
    /// <param name="name">The control's name.</param>
    /// <param name="value">The new value.</param>
    public void SetValue(string name, string value) => _values[name] = value;

    /// <summary>Replaces one output box's contents.</summary>
    /// <param name="name">The box's name.</param>
    /// <param name="text">The new contents.</param>
    public void SetOutput(string name, string text) =>
        _outputs[name] = new StringBuilder(text);

    public bool AppendOutput(string name, string text)
    {
        if (!_outputs.TryGetValue(name, out StringBuilder? output))
        {
            output = new StringBuilder();
            _outputs[name] = output;
        }

        output.AppendLine(text);
        if (output.Length <= MaxOutputLength)
            return false;

        int removeLength = output.Length - MaxOutputLength + TruncatedOutput.Length;
        while (removeLength < output.Length && output[removeLength] != '\n')
            removeLength++;
        if (removeLength < output.Length)
            removeLength++;
        output.Remove(0, removeLength);
        output.Insert(0, TruncatedOutput);
        return true;
    }

    public string OutputValue(string name) =>
        _outputs.TryGetValue(name, out StringBuilder? output) ? output.ToString() : "";

    public IReadOnlyDictionary<string, string> OutputValues() =>
        _outputs.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
}
