using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Rules;

namespace Qx.Presentation.ViewModels.General;

public sealed partial class GeneralNumberViewModel : GeneralRowViewModel
{
    readonly Func<SessionRules, int> _read;
    readonly Action<SessionRules, int> _write;
    readonly Action _changed;
    int _value;

    internal GeneralNumberViewModel(
        string label,
        string unit,
        int minimum,
        int maximum,
        string automation_name,
        Func<SessionRules, int> read,
        Action<SessionRules, int> write,
        Action changed,
        Func<SessionRules, bool>? available = null)
        : base(label, "", available)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(automation_name);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimum, maximum);
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        Unit = unit;
        Minimum = minimum;
        Maximum = maximum;
        AutomationName = automation_name;
    }

    public string Unit { get; }

    public int Minimum { get; }

    public int Maximum { get; }

    public string AutomationName { get; }

    public string Range => string.Create(CultureInfo.CurrentCulture, $"{Minimum} – {Maximum}");

    [ObservableProperty]
    public partial string Text { get; set; } = "";

    public void Commit()
    {
        int wanted = int.TryParse(Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int typed)
            ? Math.Clamp(typed, Minimum, Maximum)
            : _value;
        if (wanted == _value)
        {
            Show();
            return;
        }
        _value = wanted;
        _changed();
    }

    public void Revert() => Show();

    internal override void Read(SessionRules rules)
    {
        _value = Math.Clamp(_read(rules), Minimum, Maximum);
        Show();
    }

    internal override void Write(SessionRules rules) => _write(rules, _value);

    void Show() => Text = _value.ToString(CultureInfo.CurrentCulture);
}
