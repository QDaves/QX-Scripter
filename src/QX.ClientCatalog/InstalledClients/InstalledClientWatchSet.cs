namespace Qx.ClientCatalog.InstalledClients;

internal sealed class InstalledClientWatchSet : IDisposable
{
    readonly Func<IReadOnlyList<string>> _roots;
    readonly Action _changed;
    readonly object _gate = new();
    readonly List<FileSystemWatcher> _watchers = [];
    volatile bool _invalid = true;
    bool _disposed;

    public InstalledClientWatchSet(Func<IReadOnlyList<string>> roots, Action changed)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
    }

    public void Refresh()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string[] roots = _roots()
                .Where(Directory.Exists)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string[] watched = _watchers
                .Select(watcher => Path.GetFullPath(watcher.Path))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!_invalid && roots.SequenceEqual(watched, StringComparer.OrdinalIgnoreCase))
                return;

            ClearCore();
            _invalid = false;
            foreach (string root in roots)
            {
                if (!Add(root))
                    _invalid = true;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            ClearCore();
            _invalid = true;
        }
    }

    bool Add(string root)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.Size,
                InternalBufferSize = 65536
            };
            watcher.Changed += FileChanged;
            watcher.Created += FileChanged;
            watcher.Deleted += FileChanged;
            watcher.Renamed += FileRenamed;
            watcher.Error += WatcherFailed;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
            return true;
        }
        catch (Exception error) when (error is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            watcher?.Dispose();
            return false;
        }
    }

    void FileChanged(object sender, FileSystemEventArgs args) => _changed();

    void FileRenamed(object sender, RenamedEventArgs args) => _changed();

    void WatcherFailed(object sender, ErrorEventArgs args)
    {
        _invalid = true;
        _changed();
    }

    void ClearCore()
    {
        foreach (FileSystemWatcher watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            ClearCore();
        }
    }
}
