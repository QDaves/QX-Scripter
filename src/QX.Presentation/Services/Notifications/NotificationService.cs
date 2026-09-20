using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Mvvm;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Notifications;

public sealed class NotificationService : INotificationService, IDisposable
{
    public const int Capacity = 4;
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(6);

    readonly ToastQueue<Toast> _queue;
    readonly IDialogService _dialogs;
    readonly TimeProvider _time;
    readonly List<Toast> _waiting = [];
    long _next_id;

    public NotificationService(IDialogService dialogs, IUiDispatcher dispatcher, TimeProvider time)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _queue = new ToastQueue<Toast>(dispatcher, time, Capacity, Lifetime);
        if (dialogs is INotifyPropertyChanged changes)
            changes.PropertyChanged += OnDialogsChanged;
    }

    public ReadOnlyObservableCollection<Toast> Toasts => _queue.Toasts;

    public void Show(string text, NoticeSeverity severity = NoticeSeverity.Info, string? action_text = null, ICommand? action = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var toast = new Toast(++_next_id, severity, text, action_text, action, _time.GetUtcNow());
        if (!_dialogs.IsOpen)
        {
            _queue.Show(toast);
            return;
        }
        _waiting.Add(toast);
        while (_waiting.Count > Capacity)
            _waiting.RemoveAt(0);
    }

    public void Dismiss(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        _waiting.Remove(toast);
        _queue.Remove(toast);
    }

    public void Dispose()
    {
        if (_dialogs is INotifyPropertyChanged changes)
            changes.PropertyChanged -= OnDialogsChanged;
        _waiting.Clear();
        _queue.Dispose();
    }

    void OnDialogsChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(IDialogService.Current) || _dialogs.IsOpen || _waiting.Count == 0)
            return;
        Toast[] held = [.. _waiting];
        _waiting.Clear();
        foreach (Toast toast in held)
            _queue.Show(toast);
    }
}
