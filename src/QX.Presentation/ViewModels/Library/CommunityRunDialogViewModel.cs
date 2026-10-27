using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Diagnostics;
using Qx.Presentation.Dialogs;
using Qx.Presentation.Threading;
using Qx.Presentation.Visuals;

namespace Qx.Presentation.ViewModels.Library;

public enum CommunityRunChoice
{
    Cancel,
    Review,
    Run
}

public sealed partial class CommunityRunDialogViewModel(string script, string author, TimeProvider time, IUiDispatcher dispatcher)
    : DialogViewModel<CommunityRunChoice>, IDisposable
{
    const int WaitSeconds = 5;
    CancellationTokenSource? _countdown;
    long? _shown;

    public override string Title => "Run community script?";
    public override IconKind Icon => IconKind.Warning;
    public override DialogTone Tone => DialogTone.Destructive;
    public string Script => script;
    public string Author => $"By {author}";
    public string CountdownText => SecondsRemaining > 0 ? $"{SecondsRemaining}s" : "";
    public bool CanRun => _shown is { } shown && time.GetElapsedTime(shown) >= TimeSpan.FromSeconds(WaitSeconds) && !IsClosed;
    protected override CommunityRunChoice DismissResult => CommunityRunChoice.Cancel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountdownText), nameof(CanRun))]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    public partial int SecondsRemaining { get; private set; } = WaitSeconds;

    public void Start()
    {
        if (_countdown is not null || IsClosed)
            return;
        _shown ??= time.GetTimestamp();
        _countdown = new CancellationTokenSource();
        CountAsync(_countdown.Token).Observe("community");
    }

    async Task CountAsync(CancellationToken cancellation_token)
    {
        try
        {
            while (!IsClosed && !cancellation_token.IsCancellationRequested)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    SecondsRemaining = Math.Max(0, (int)Math.Ceiling(WaitSeconds - time.GetElapsedTime(_shown!.Value).TotalSeconds));
                }, cancellation_token);
                if (SecondsRemaining == 0)
                    return;
                await Task.Delay(TimeSpan.FromMilliseconds(200), time, cancellation_token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested) { }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    void Accept()
    {
        if (CanRun)
            Close(CommunityRunChoice.Run);
    }

    [RelayCommand]
    void Review() => Close(CommunityRunChoice.Review);

    public void Dispose()
    {
        _countdown?.Cancel();
        _countdown?.Dispose();
        _countdown = null;
    }
}
