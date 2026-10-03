using Qx.Platform;

namespace Qx.Scripting;

/// <summary>
/// Represents the physical keyboard, read system-wide whichever window has focus.
/// </summary>
/// <remarks>
/// Reading is supported on Windows, on macOS once Input Monitoring is allowed, and on Linux in an
/// X11 or XWayland session. Every handler is removed when the script stops.
/// </remarks>
public sealed class Keyboard
{
    readonly KeyboardReader _reader;
    readonly Func<Action, Action> _guard;
    readonly Func<IDisposable, IDisposable> _track;

    internal Keyboard(KeyboardReader reader, Func<Action, Action> guard, Func<IDisposable, IDisposable> track)
    {
        _reader = reader;
        _guard = guard;
        _track = track;
    }

    /// <summary>Gets whether keys can be read on this system.</summary>
    /// <remarks>Becomes <see langword="false"/> for good after a native read fails.</remarks>
    public bool IsSupported => _reader.IsSupported;

    /// <summary>
    /// Gets what the keyboard is read through, or why it cannot be read.
    /// </summary>
    /// <remarks>
    /// For example <c>Windows</c>, or a reason such as a missing Input Monitoring permission on macOS
    /// or a Wayland session on Linux.
    /// </remarks>
    public string Status => _reader.Status;

    /// <summary>Gets whether the key is held down at this moment.</summary>
    /// <param name="key">The key to check.</param>
    /// <returns>
    /// <see langword="true"/> when the key is down; <see langword="false"/> when it is up or reading
    /// is not supported.
    /// </returns>
    public bool IsDown(Key key) => _reader.IsDown(key);

    /// <summary>
    /// Registers a handler that runs each time the key is pressed.
    /// </summary>
    /// <remarks>
    /// Holding the key down does not repeat it, and a key already held when the handler is
    /// registered is only reported on its next press. The keys are polled every 10 milliseconds on
    /// a background thread and the handler runs there; an exception it throws is reported as a
    /// background error of the script. Nothing is reported when reading is not supported.
    /// </remarks>
    /// <param name="key">The key to watch.</param>
    /// <param name="handler">The handler to call when the key goes down.</param>
    /// <returns>A handle that removes the handler when disposed; it is also disposed when the script stops.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnDown(Key key, Action handler) => Watch(key, handler, true);

    /// <summary>Registers a handler that runs each time the key is released.</summary>
    /// <remarks>
    /// The handler runs on the keyboard thread, the same way as for
    /// <see cref="OnDown(Key, Action)"/>.
    /// </remarks>
    /// <param name="key">The key to watch.</param>
    /// <param name="handler">The handler to call when the key comes back up.</param>
    /// <returns>A handle that removes the handler when disposed; it is also disposed when the script stops.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public IDisposable OnUp(Key key, Action handler) => Watch(key, handler, false);

    IDisposable Watch(Key key, Action handler, bool pressed)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Action guarded = _guard(handler);
        return _track(_reader.Watch(key, down =>
        {
            if (down == pressed)
                guarded();
        }));
    }
}
