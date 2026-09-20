using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Rules;

namespace Qx.Presentation.ViewModels.General;

public sealed partial class GeneralSwitchViewModel : GeneralRowViewModel
{
    readonly Func<SessionRules, bool> _read;
    readonly Action<SessionRules, bool> _write;
    readonly Action _changed;

    internal GeneralSwitchViewModel(
        string label,
        string description,
        Func<SessionRules, bool> read,
        Action<SessionRules, bool> write,
        Action changed,
        Func<SessionRules, bool>? available = null)
        : base(label, description, available)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    internal static GeneralSwitchViewModel Unavailable(string label, string description, Action changed) =>
        new(label, description, static _ => false, static (_, _) => { }, changed, static _ => false);

    internal override void Read(SessionRules rules) => IsOn = _read(rules);

    internal override void Write(SessionRules rules) => _write(rules, IsOn);

    partial void OnIsOnChanged(bool value) => _changed();
}
