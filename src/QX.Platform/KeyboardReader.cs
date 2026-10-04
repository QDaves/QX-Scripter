namespace Qx.Platform;

/// <summary>Provides system-wide reads of the physical keyboard, whichever window has focus.</summary>
/// <remarks>
/// <para>
/// Every call to <see cref="Create"/> opens a separate native reader. The watchers of one reader are
/// polled together on one background thread, which only runs while something is being watched.
/// Reading is supported on Windows, on macOS once Input Monitoring is allowed, and on Linux in an X11
/// or XWayland session.
/// </para>
/// <para>
/// The host shares one reader between the session rules and every script it runs. Scripts read it
/// through their <c>Keyboard</c> property, which removes their handlers when the script stops.
/// </para>
/// </remarks>
public sealed class KeyboardReader : IDisposable
{
    /// <summary>The interval between two polls of the watched keys, 10 milliseconds.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    readonly object _sync = new();
    readonly List<Watcher> _watchers = [];
    readonly ManualResetEventSlim _stopped = new(false);
    IKeyReader _reader;
    Thread? _poller;
    bool _disposed;

    KeyboardReader(IKeyReader reader) => _reader = reader;

    /// <summary>Opens a new reader of the keyboard of the system QX runs on.</summary>
    /// <remarks>
    /// Does not throw when the system cannot be read. The returned reader then reports
    /// <see cref="IsSupported"/> as <see langword="false"/> and gives the reason in <see cref="Status"/>.
    /// </remarks>
    /// <returns>A new reader of the system keyboard, owned by the caller.</returns>
    public static KeyboardReader Create() => new(KeyReaders.Open());

    /// <summary>Creates a reader that never reports a key, with the reason it cannot.</summary>
    /// <param name="reason">The reason reported by <see cref="Status"/>.</param>
    /// <returns>An unsupported reader.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reason"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is <see langword="null"/>.</exception>
    public static KeyboardReader Unsupported(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(new NoKeyReader(reason));
    }

    /// <summary>Gets whether keys can be read on this system right now.</summary>
    /// <remarks>Becomes <see langword="false"/> for good after a native read fails.</remarks>
    public bool IsSupported
    {
        get
        {
            lock (_sync)
                return _reader.IsSupported;
        }
    }

    /// <summary>Gets what the keyboard is read through, or why it cannot be read.</summary>
    /// <remarks>For example <c>Windows</c>, <c>macOS</c> or <c>X11</c>, or a message that explains the failure.</remarks>
    public string Status
    {
        get
        {
            lock (_sync)
                return _reader.Status;
        }
    }

    /// <summary>Gets whether the key is held down at this moment.</summary>
    /// <param name="key">The key to check.</param>
    /// <returns>
    /// <see langword="true"/> when the key is down; <see langword="false"/> when it is up, reading is not
    /// supported or the reader is disposed.
    /// </returns>
    public bool IsDown(Key key)
    {
        lock (_sync)
        {
            if (_disposed || !_reader.IsSupported)
                return false;
            try
            {
                _reader.Refresh();
                return _reader.IsDown(key);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Fail(error);
                return false;
            }
        }
    }

    /// <summary>Watches a key and calls back whenever it goes down or comes back up.</summary>
    /// <remarks>
    /// The callback receives <see langword="true"/> when the key goes down and <see langword="false"/>
    /// when it comes back up. A key already held when watching starts is not reported until it changes.
    /// The callback runs on the keyboard thread and exceptions it throws are ignored. Nothing is reported
    /// when reading is not supported.
    /// </remarks>
    /// <param name="key">The key to watch.</param>
    /// <param name="changed">The callback that receives the new key state.</param>
    /// <returns>A handle that stops watching when disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="changed"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the reader is disposed.</exception>
    public IDisposable Watch(Key key, Action<bool> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var watcher = new Watcher(this, key, changed, IsDownLocked(key));
            _watchers.Add(watcher);
            if (_poller is null && _reader.IsSupported)
            {
                _poller = new Thread(Poll) { IsBackground = true, Name = "QX keyboard" };
                _poller.Start();
            }
            return watcher;
        }
    }

    /// <summary>Stops every watcher and releases the native keyboard resources.</summary>
    /// <remarks>Waits up to one second for the keyboard thread to finish.</remarks>
    public void Dispose()
    {
        Thread? poller;
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            _watchers.Clear();
            poller = _poller;
            _poller = null;
            _stopped.Set();
        }
        if (poller is not null && poller != Thread.CurrentThread)
            poller.Join(TimeSpan.FromSeconds(1));
        lock (_sync)
            _reader.Dispose();
    }

    void Remove(Watcher watcher)
    {
        lock (_sync)
            _watchers.Remove(watcher);
    }

    bool IsDownLocked(Key key)
    {
        if (!_reader.IsSupported)
            return false;
        try
        {
            _reader.Refresh();
            return _reader.IsDown(key);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Fail(error);
            return false;
        }
    }

    void Fail(Exception error)
    {
        IKeyReader failed = _reader;
        _reader = new NoKeyReader($"Keyboard reading stopped: {error.GetType().Name}: {error.Message}");
        try
        {
            failed.Dispose();
        }
        catch (Exception dispose_error) when (dispose_error is not OutOfMemoryException)
        {
        }
    }

    void Poll()
    {
        var changes = new List<(Action<bool> Changed, bool Down)>();
        while (!_stopped.Wait(PollInterval))
        {
            lock (_sync)
            {
                if (_disposed || _watchers.Count == 0 || !_reader.IsSupported)
                {
                    _poller = null;
                    return;
                }
                try
                {
                    _reader.Refresh();
                    foreach (Watcher watcher in _watchers)
                    {
                        bool down = _reader.IsDown(watcher.Key);
                        if (down == watcher.Down)
                            continue;
                        watcher.Down = down;
                        changes.Add((watcher.Changed, down));
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Fail(error);
                    _poller = null;
                    return;
                }
            }
            foreach ((Action<bool> changed, bool down) in changes)
            {
                try
                {
                    changed(down);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                }
            }
            changes.Clear();
        }
    }

    sealed class Watcher(KeyboardReader owner, Key key, Action<bool> changed, bool down) : IDisposable
    {
        int _disposed;

        public Key Key { get; } = key;

        public Action<bool> Changed { get; } = changed;

        public bool Down { get; set; } = down;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Remove(this);
        }
    }
}
