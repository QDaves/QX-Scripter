using System.Collections.Concurrent;
using System.Text;
using Qx.Diagnostics;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Library;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Files;

public sealed class ScriptFileService(IAppPaths paths) : IScriptFileService
{
    const int HeaderLength = 4096;

    static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    readonly ConcurrentDictionary<string, (DateTime EditedAt, long Length, ScriptHeader Header)> _headers = new(PathComparison.Comparer);

    public string ScriptsDirectory { get; } = (paths ?? throw new ArgumentNullException(nameof(paths))).ScriptsDirectory;

    public string PathFor(string typedName) => ScriptFileName.PathIn(ScriptsDirectory, typedName);

    public bool Exists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public Task<IReadOnlyList<ScriptFileEntry>> ListAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<ScriptFileEntry>>(() =>
        {
            if (!Directory.Exists(ScriptsDirectory))
                return [];
            var entries = new List<ScriptFileEntry>();
            foreach (string path in Directory.EnumerateFiles(ScriptsDirectory, "*" + ScriptFileName.Extension, SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                if (!info.Exists)
                    continue;
                entries.Add(new ScriptFileEntry(info.FullName, ScriptFileName.NameOf(info.FullName), info.LastWriteTimeUtc, info.Length, HeaderOf(info)));
            }
            return entries;
        }, cancellationToken);

    ScriptHeader HeaderOf(FileInfo info)
    {
        if (_headers.TryGetValue(info.FullName, out var cached) && cached.EditedAt == info.LastWriteTimeUtc && cached.Length == info.Length)
            return cached.Header;
        ScriptHeader header;
        try
        {
            using var reader = new StreamReader(info.FullName, _utf8, detectEncodingFromByteOrderMarks: true);
            char[] start = new char[HeaderLength];
            header = ScriptHeader.Parse(new string(start, 0, reader.ReadBlock(start, 0, start.Length)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            header = new ScriptHeader(null, null);
        }
        _headers[info.FullName] = (info.LastWriteTimeUtc, info.Length, header);
        return header;
    }

    public Task<string?> ReadAsync(string path, CancellationToken cancellationToken) =>
        Task.Run<string?>(() =>
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }, cancellationToken);

    public Task<FileOperationResult> WriteAsync(string path, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(text);
        return Task.Run(() =>
        {
            try
            {
                string staging = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(staging, text, _utf8);
                File.Move(staging, path, overwrite: true);
                return FileOperationResult.Ok;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return FileOperationResult.Failed(error);
            }
        }, cancellationToken);
    }

    public Task<FileOperationResult> MoveAsync(string from, string to, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Move(from, to, overwrite: true);
                return FileOperationResult.Ok;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return FileOperationResult.Failed(error);
            }
        }, cancellationToken);

    public Task<FileOperationResult> CopyAsync(string from, string to, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, overwrite: false);
                return FileOperationResult.Ok;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return FileOperationResult.Failed(error);
            }
        }, cancellationToken);

    public Task<FileOperationResult> DeleteAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            try
            {
                File.Delete(path);
                return FileOperationResult.Ok;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return FileOperationResult.Failed(error);
            }
        }, cancellationToken);

    public IDisposable Watch(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        Directory.CreateDirectory(ScriptsDirectory);
        return new FolderWatch(ScriptsDirectory, changed);
    }

    sealed class FolderWatch : IDisposable
    {
        readonly string _folder;
        readonly Action _changed;
        FileSystemWatcher? _watcher;
        bool _closed;
        bool _recreated;

        public FolderWatch(string folder, Action changed)
        {
            _folder = folder;
            _changed = changed;
            _watcher = Build();
        }

        public void Dispose()
        {
            Volatile.Write(ref _closed, true);
            Drop(Interlocked.Exchange(ref _watcher, null));
        }

        FileSystemWatcher Build()
        {
            var watcher = new FileSystemWatcher(_folder, "*" + ScriptFileName.Extension)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false
            };
            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        void Drop(FileSystemWatcher? watcher)
        {
            if (watcher is null)
                return;
            Unsubscribe(watcher);
            watcher.Dispose();
        }

        void Unsubscribe(FileSystemWatcher watcher)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnChanged;
            watcher.Deleted -= OnChanged;
            watcher.Changed -= OnChanged;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
        }

        void OnChanged(object? sender, FileSystemEventArgs args) => _changed();

        void OnRenamed(object? sender, RenamedEventArgs args) => _changed();

        void OnError(object? sender, ErrorEventArgs args)
        {
            Diag.Warn($"The script folder watcher failed: {args.GetException().Message}", "library");
            if (_recreated || _watcher is null)
                return;
            _recreated = true;
            try
            {
                Drop(Interlocked.Exchange(ref _watcher, null));
                if (Volatile.Read(ref _closed))
                    return;
                FileSystemWatcher rebuilt = Build();
                if (Interlocked.CompareExchange(ref _watcher, rebuilt, null) is not null)
                {
                    Drop(rebuilt);
                    return;
                }
                if (Volatile.Read(ref _closed))
                {
                    Drop(Interlocked.Exchange(ref _watcher, null));
                    return;
                }
                _changed();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Diag.Warn($"The script folder watcher could not be restarted: {error.Message}", "library");
            }
        }
    }
}
