using Qx.Diagnostics;
using Qx.Presentation.Threading;

namespace Qx.Presentation.Services.Files;

public sealed class AtomicJsonFile : IDisposable
{
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(300);

    readonly string _path;
    readonly string _category;
    readonly string _trouble;
    readonly Func<string> _render;
    readonly Debouncer _save;
    readonly SemaphoreSlim _write_gate = new(1, 1);
    string _written;

    public AtomicJsonFile(string path, string category, string trouble, Func<string> render, IUiDispatcher dispatcher, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(trouble);
        ArgumentNullException.ThrowIfNull(render);
        _path = path;
        _category = category;
        _trouble = trouble;
        _render = render;
        _written = render();
        _save = new Debouncer(dispatcher, time, SaveDelay, () => WriteAsync(render(), CancellationToken.None).Observe(category));
    }

    public void Schedule() => _save.Trigger();

    public Task FlushAsync(CancellationToken cancellation_token)
    {
        _save.Cancel();
        return WriteAsync(_render(), cancellation_token);
    }

    public bool FlushNow(TimeSpan budget)
    {
        _save.Cancel();
        string json = _render();
        if (!_write_gate.Wait(budget))
        {
            Diag.Warn($"{_trouble}: the writer stayed busy for {budget.TotalMilliseconds:0} ms.", _category);
            return false;
        }
        try
        {
            if (string.Equals(json, _written, StringComparison.Ordinal))
                return true;
            Write(json);
            _written = json;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Diag.Warn($"{_trouble}: {error.Message}", _category);
            return false;
        }
        finally
        {
            _write_gate.Release();
        }
    }

    public void Dispose()
    {
        _save.Dispose();
        _write_gate.Dispose();
    }

    async Task WriteAsync(string json, CancellationToken cancellation_token)
    {
        await _write_gate.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            if (string.Equals(json, _written, StringComparison.Ordinal))
                return;
            await Task.Run(() => Write(json), cancellation_token).ConfigureAwait(false);
            _written = json;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Diag.Warn($"{_trouble}: {error.Message}", _category);
        }
        finally
        {
            _write_gate.Release();
        }
    }

    void Write(string json)
    {
        string staging = _path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(staging, json);
        File.Move(staging, _path, overwrite: true);
    }
}
