using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qx.Presentation.Platform;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Mvvm;

public sealed partial class CopyAction : ObservableObject, IDisposable
{
    public static readonly TimeSpan FeedbackLifetime = TimeSpan.FromMilliseconds(1400);

    readonly IClipboardService _clipboard;
    readonly Func<string?> _text;
    readonly Action<string>? _failed;
    readonly Debouncer _reset;

    public CopyAction(IClipboardService clipboard, IUiDispatcher dispatcher, TimeProvider time, Func<string?> text, Action<string>? failed = null)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _failed = failed;
        _reset = new Debouncer(dispatcher, time, FeedbackLifetime, () => Copied = false);
    }

    [ObservableProperty]
    public partial bool Copied { get; private set; }

    [RelayCommand]
    async Task CopyAsync(CancellationToken cancellation_token)
    {
        string? text = _text();
        if (string.IsNullOrEmpty(text))
            return;
        if (await _clipboard.TrySetTextAsync(text, cancellation_token))
        {
            Copied = true;
            _reset.Trigger();
            return;
        }
        _failed?.Invoke("Could not reach the clipboard. Another program may be holding it.");
    }

    public void Dispose() => _reset.Dispose();
}
