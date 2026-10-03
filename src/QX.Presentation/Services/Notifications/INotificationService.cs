using System.Collections.ObjectModel;
using System.Windows.Input;
using Qx.Presentation.Mvvm;

namespace Qx.Presentation.Services.Notifications;

public interface INotificationService
{
    ReadOnlyObservableCollection<Toast> Toasts { get; }

    void Show(string text, NoticeSeverity severity = NoticeSeverity.Info, string? actionText = null, ICommand? action = null);

    void Dismiss(Toast toast);
}
