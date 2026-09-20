using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.Mvvm;

public sealed partial class ViewState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(IsLoading), nameof(IsBlocking))]
    public partial ViewStateKind Kind { get; private set; }

    [ObservableProperty]
    public partial IconKind Icon { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string Message { get; private set; } = "";

    [ObservableProperty]
    public partial ICommand? Action { get; private set; }

    [ObservableProperty]
    public partial string ActionText { get; private set; } = "";

    [ObservableProperty]
    public partial ICommand? Cancel { get; private set; }

    public bool IsReady => Kind == ViewStateKind.Ready;

    public bool IsLoading => Kind == ViewStateKind.Loading;

    public bool IsBlocking => Kind is ViewStateKind.Loading or ViewStateKind.Unavailable or ViewStateKind.Error;

    public void ShowReady() => Set(ViewStateKind.Ready, IconKind.None, "", "", null, "", null);

    public void ShowLoading(string message, ICommand? cancel = null) =>
        Set(ViewStateKind.Loading, IconKind.None, "", message, null, "", cancel);

    public void ShowEmpty(IconKind icon, string title, string message = "", ICommand? action = null, string action_text = "") =>
        Set(ViewStateKind.Empty, icon, title, message, action, action_text, null);

    public void ShowUnavailable(IconKind icon, string title, string message = "", ICommand? action = null, string action_text = "") =>
        Set(ViewStateKind.Unavailable, icon, title, message, action, action_text, null);

    public void ShowError(string title, string message, ICommand? retry = null) =>
        Set(ViewStateKind.Error, IconKind.Error, title, message, retry, retry is null ? "" : "Try again", null);

    void Set(ViewStateKind kind, IconKind icon, string title, string message, ICommand? action, string action_text, ICommand? cancel)
    {
        Icon = icon;
        Title = title;
        Message = message;
        Action = action;
        ActionText = action_text;
        Cancel = cancel;
        Kind = kind;
    }
}
