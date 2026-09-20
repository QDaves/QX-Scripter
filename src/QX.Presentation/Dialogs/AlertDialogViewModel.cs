using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public sealed partial class AlertDialogViewModel(string title, string message) : DialogViewModel<bool>
{
    public override string Title => title;

    public override IconKind Icon => IconKind.Warning;

    public string Message => message;

    public string AcceptText => "OK";

    protected override bool DismissResult => false;

    [RelayCommand]
    void Accept() => Close(true);
}
