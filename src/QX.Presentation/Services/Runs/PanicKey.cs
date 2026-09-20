using Qx.Diagnostics;
using Qx.Presentation.Input;
using Qx.Presentation.Platform;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Runs;

public sealed class PanicKey : IDisposable
{
    public static readonly KeyChord Chord = new(ChordKey.F12, ChordModifiers.Control | ChordModifiers.Alt | ChordModifiers.Shift);

    readonly IGlobalHotkeys _hotkeys;
    readonly IScriptRunRegistry _runs;
    readonly IUiDispatcher _dispatcher;
    readonly IGestureFormatter _gestures;
    IDisposable? _registration;

    public PanicKey(IGlobalHotkeys hotkeys, IScriptRunRegistry runs, IUiDispatcher dispatcher, IGestureFormatter gestures)
    {
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _gestures = gestures ?? throw new ArgumentNullException(nameof(gestures));
    }

    public bool IsRegistered => _registration is not null;

    public string Gesture => _gestures.Describe(Chord);

    public string? GestureText => IsRegistered ? Gesture : null;

    public async Task RegisterAsync(CancellationToken cancellation_token)
    {
        if (_registration is not null)
            return;
        if (!_hotkeys.IsSupported)
        {
            Diag.Info($"The panic key {Gesture} is not available on this system; stop scripts from the tab or the status bar.", "hotkey");
            return;
        }
        _registration = await _hotkeys.TryRegisterAsync(Chord, Pressed, cancellation_token);
        if (_registration is null)
            Diag.Warn($"Panic key {Gesture} is held by another application; stop from the tab or the status bar instead.", "hotkey");
    }

    public void Dispose()
    {
        _registration?.Dispose();
        _registration = null;
    }

    void Pressed() => _dispatcher.Post(StopAll, UiPriority.Input);

    void StopAll() => _runs.StopAll();
}
