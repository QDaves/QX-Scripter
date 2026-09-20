using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public sealed partial class ConfirmDialogViewModel(string title, string message, string accept_text, DialogTone tone = DialogTone.Neutral, string? caption = null)
    : DialogViewModel<bool>
{
    public override string Title => title;

    public override IconKind Icon => tone == DialogTone.Destructive ? IconKind.Warning : IconKind.Question;

    public override DialogTone Tone => tone;

    public bool IsDestructive => tone == DialogTone.Destructive;

    public override string? Caption => caption;

    public string Message => message;

    public string AcceptText => accept_text;

    protected override bool DismissResult => false;

    [RelayCommand]
    void Accept() => Close(true);
}
