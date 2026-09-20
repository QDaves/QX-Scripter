using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Mvvm;

public sealed partial class NoticeLine : ObservableObject, IDisposable
{
    public static readonly TimeSpan TransientLifetime = TimeSpan.FromSeconds(6);

    readonly TimeProvider _time;
    readonly Debouncer _expiry;

    public NoticeLine(IUiDispatcher dispatcher, TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _expiry = new Debouncer(dispatcher, time, TransientLifetime, Clear);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial Notice? Current { get; private set; }

    public bool HasNotice => Current is not null;

    public void Show(NoticeSeverity severity, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Current = new Notice(severity, text, _time.GetUtcNow());
        if (severity is NoticeSeverity.Info or NoticeSeverity.Success)
            _expiry.Trigger();
        else
            _expiry.Cancel();
    }

    [RelayCommand]
    public void Clear()
    {
        _expiry.Cancel();
        Current = null;
    }

    public void Dispose() => _expiry.Dispose();
}
