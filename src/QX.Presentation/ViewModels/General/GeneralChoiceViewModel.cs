using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Rules;

namespace Qx.Presentation.ViewModels.General;

public sealed record GeneralOption(string Text, string? Tip = null);

public sealed partial class GeneralChoiceViewModel : GeneralRowViewModel
{
    readonly Func<SessionRules, int> _read;
    readonly Action<SessionRules, int> _write;
    readonly Action _changed;

    internal GeneralChoiceViewModel(
        string label,
        string description,
        IReadOnlyList<GeneralOption> options,
        Func<SessionRules, int> read,
        Action<SessionRules, int> write,
        Action changed,
        Func<SessionRules, bool>? available = null)
        : base(label, description, available)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count < 2)
            throw new ArgumentException("A choice needs at least two options.", nameof(options));
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        Options = options;
    }

    public IReadOnlyList<GeneralOption> Options { get; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; }

    internal override void Read(SessionRules rules) => SelectedIndex = _read(rules);

    internal override void Write(SessionRules rules) => _write(rules, SelectedIndex);

    partial void OnSelectedIndexChanged(int value) => _changed();
}
