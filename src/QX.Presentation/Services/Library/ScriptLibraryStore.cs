using System.Text.Json;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Threading;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Library;

public sealed class ScriptLibraryStore : IScriptLibrary, IDisposable
{
    readonly AtomicJsonFile _file;
    readonly Dictionary<string, ScriptMeta> _scripts = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _collapsed = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, LastRun> _runs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<string>> _community_approvals = new(StringComparer.Ordinal);
    IReadOnlyList<string>? _categories;
    LibrarySort _sort;

    public ScriptLibraryStore(IAppPaths paths, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Load(paths.LibraryFile);
        _file = new AtomicJsonFile(paths.LibraryFile, "library", "The script library could not be saved", Render, dispatcher, time);
    }

    public LibrarySort Sort
    {
        get => _sort;
        set
        {
            if (_sort == value)
                return;
            _sort = value;
            Save();
        }
    }

    public IReadOnlyList<string> Categories => _categories ??=
        [.. _scripts.Values
            .Select(meta => meta.Category?.Trim())
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Select(category => category!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.CurrentCulture)];

    public event Action? Changed;

    public bool IsCollapsed(string category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return _collapsed.Contains(category);
    }

    public void SetCollapsed(string category, bool collapsed)
    {
        ArgumentNullException.ThrowIfNull(category);
        bool changed = collapsed ? _collapsed.Add(category) : _collapsed.Remove(category);
        if (changed)
            Save();
    }

    public ScriptMeta Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _scripts.GetValueOrDefault(name, ScriptMeta.Empty);
    }

    public void Set(string name, ScriptMeta meta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(meta);
        if (meta.IsEmpty)
        {
            if (_scripts.Remove(name))
                Save();
            return;
        }
        if (_scripts.TryGetValue(name, out ScriptMeta? current) && current == meta)
            return;
        _scripts[name] = meta;
        Save();
    }

    public void Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        bool ran = _runs.Remove(name);
        if (_scripts.Remove(name))
            Save();
        else if (ran)
            Changed?.Invoke();
    }

    public void Rename(string from, string to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return;
        bool ran = _runs.Remove(from, out LastRun? run);
        if (ran && run is not null)
            _runs[to] = run;
        if (!_scripts.Remove(from, out ScriptMeta? meta))
        {
            if (ran)
                Changed?.Invoke();
            return;
        }
        _scripts[to] = meta;
        Save();
    }

    public int RenameCategory(string from, string to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        string wanted = to.Trim();
        int changed = 0;
        foreach (string name in _scripts.Keys.ToArray())
        {
            if (!string.Equals(_scripts[name].Category?.Trim(), from.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            _scripts[name] = _scripts[name] with { Category = wanted.Length == 0 ? null : wanted };
            if (_scripts[name].IsEmpty)
                _scripts.Remove(name);
            changed++;
        }
        if (changed > 0)
            Save();
        return changed;
    }

    public int RemoveCategory(string category) => RenameCategory(category, "");

    public LastRun? LastRunOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _runs.GetValueOrDefault(name);
    }

    public void RecordRunStarted(string name, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        _runs[name] = new LastRun(at, null);
        Changed?.Invoke();
    }

    public void RecordRunFinished(string name, ScriptRunState outcome)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        DateTimeOffset at = _runs.TryGetValue(name, out LastRun? previous) ? previous.At : DateTimeOffset.Now;
        _runs[name] = new LastRun(at, outcome);
        Changed?.Invoke();
    }

    public Task FlushAsync(CancellationToken cancellationToken) => _file.FlushAsync(cancellationToken);

    public bool IsCommunityApproved(string id, string sha256) =>
        _community_approvals.TryGetValue(id, out HashSet<string>? approved) && approved.Contains(sha256);

    public void ApproveCommunity(string id, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!IsCodeHash(sha256))
            throw new ArgumentException("A SHA-256 code hash is required.", nameof(sha256));
        if (!_community_approvals.TryGetValue(id, out HashSet<string>? approved))
            _community_approvals[id] = approved = new(StringComparer.Ordinal);
        if (approved.Add(sha256))
            _file.Schedule();
    }

    public bool FlushNow(TimeSpan budget) => _file.FlushNow(budget);

    public void Dispose() => _file.Dispose();

    void Save()
    {
        _categories = null;
        _file.Schedule();
        Changed?.Invoke();
    }

    string Render() => LibraryCodec.Write(new LibraryDocument
    {
        Sort = _sort.ToString().ToLowerInvariant(),
        Collapsed = [.. _collapsed.OrderBy(category => category, StringComparer.OrdinalIgnoreCase)],
        Scripts = _scripts.Count == 0 ? null : new Dictionary<string, ScriptMeta>(_scripts, StringComparer.OrdinalIgnoreCase),
        CommunityApprovals = _community_approvals.Count == 0 ? null : _community_approvals.ToDictionary(
            entry => entry.Key, entry => entry.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal)
    });

    void Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            LibraryDocument document = LibraryCodec.Read(File.ReadAllText(path));
            _sort = Enum.TryParse(document.Sort, ignoreCase: true, out LibrarySort sort) ? sort : LibrarySort.Modified;
            foreach (string category in document.Collapsed ?? [])
            {
                if (!string.IsNullOrWhiteSpace(category))
                    _collapsed.Add(category);
            }
            foreach ((string name, ScriptMeta meta) in document.Scripts ?? new Dictionary<string, ScriptMeta>())
            {
                if (!string.IsNullOrWhiteSpace(name) && meta is { IsEmpty: false })
                    _scripts[name] = meta;
            }
            foreach ((string id, string[] hashes) in document.CommunityApprovals ?? new Dictionary<string, string[]>())
            {
                if (string.IsNullOrWhiteSpace(id) || hashes is null)
                    continue;
                HashSet<string> approved = new(hashes.Where(IsCodeHash), StringComparer.Ordinal);
                if (approved.Count > 0)
                    _community_approvals[id] = approved;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    static bool IsCodeHash(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
