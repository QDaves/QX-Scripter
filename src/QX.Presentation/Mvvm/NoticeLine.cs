using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Mvvm;

public sealed partial class NoticeLine : ObservableObject, IDisposable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(3);

    readonly TimeProvider _time;
    readonly Debouncer _expiry;

    public NoticeLine(IUiDispatcher dispatcher, TimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _expiry = new Debouncer(dispatcher, time, Lifetime, Clear);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial Notice? Current { get; private set; }

    public bool HasNotice => Current is not null;

    public void Show(NoticeSeverity severity, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Current = new Notice(severity, text, _time.GetUtcNow());
        _expiry.Trigger();
    }

    [RelayCommand]
    public void Clear()
    {
        _expiry.Cancel();
        Current = null;
    }

    public void Dispose() => _expiry.Dispose();
}
