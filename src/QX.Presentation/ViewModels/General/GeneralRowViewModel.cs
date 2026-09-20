using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Game.Rules;

namespace Qx.Presentation.ViewModels.General;

public abstract partial class GeneralRowViewModel : ObservableObject
{
    readonly Func<SessionRules, bool>? _available;

    protected GeneralRowViewModel(string label, string description, Func<SessionRules, bool>? available)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(description);
        Label = label;
        Description = description;
        _available = available;
    }

    public string Label { get; }

    public string Description { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsLast { get; private set; }

    internal void MarkLast() => IsLast = true;

    internal void Gate(SessionRules rules) => IsEnabled = _available?.Invoke(rules) ?? true;

    internal abstract void Read(SessionRules rules);

    internal abstract void Write(SessionRules rules);
}
