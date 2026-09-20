using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public sealed partial class PromptDialogViewModel : DialogViewModel<string?>
{
    readonly PromptRequest _request;

    public PromptDialogViewModel(PromptRequest request)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        Text = request.Initial;
    }

    public override string Title => _request.Title;

    public override IconKind Icon => _request.Icon;

    public override string? Caption => _request.Caption;

    public string AcceptText => _request.AcceptText;

    public string Placeholder => _request.Placeholder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial string Text { get; set; }

    protected override string? DismissResult => null;

    bool CanAccept() => _request.AllowEmpty || Text.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    void Accept() => Close(Text);
}
