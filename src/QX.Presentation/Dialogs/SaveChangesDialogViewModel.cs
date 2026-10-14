using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Dialogs;

public enum SaveChangesChoice
{
    Cancel,
    Discard,
    Save
}

public sealed partial class SaveChangesDialogViewModel(string message, string save_text = "Save script")
    : DialogViewModel<SaveChangesChoice>
{
    public override string Title => "Save before closing?";
    public override IconKind Icon => IconKind.Save;
    public string Message => message;
    public string SaveText => save_text;
    protected override SaveChangesChoice DismissResult => SaveChangesChoice.Cancel;

    [RelayCommand]
    void Save() => Close(SaveChangesChoice.Save);

    [RelayCommand]
    void Discard() => Close(SaveChangesChoice.Discard);
}
