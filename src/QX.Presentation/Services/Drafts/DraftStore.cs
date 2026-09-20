using System.Text.Json;
using System.Text.Json.Serialization;
using Qx.Presentation.Platform;

namespace Qx.Presentation.Services.Drafts;

public sealed record Draft(string Name, string Code, string? Path = null);

public interface IDraftStore
{
    Task<IReadOnlyList<Draft>> LoadAsync(CancellationToken cancellation_token);

    Task SaveAsync(IReadOnlyList<Draft> drafts, CancellationToken cancellation_token);

    Task ClearAsync(CancellationToken cancellation_token);

    bool SaveNow(IReadOnlyList<Draft> drafts, TimeSpan budget);

    void Seal();
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<Draft>))]
internal sealed partial class DraftJson : JsonSerializerContext;

public sealed class DraftStore(IAppPaths paths) : IDraftStore, IDisposable
{
    readonly string _path = (paths ?? throw new ArgumentNullException(nameof(paths))).DraftsFile;
    readonly SemaphoreSlim _gate = new(1, 1);
    string _last_written = "";
    int _sealed;

    public bool IsSealed => Volatile.Read(ref _sealed) != 0;

    public async Task<IReadOnlyList<Draft>> LoadAsync(CancellationToken cancellation_token)
    {
        try
        {
            if (!File.Exists(_path))
                return [];
            string json = await File.ReadAllTextAsync(_path, cancellation_token).ConfigureAwait(false);
            List<Draft>? drafts = JsonSerializer.Deserialize(json, DraftJson.Default.ListDraft);
            return drafts is null ? [] : [.. drafts.Where(draft => draft.Code is not null && draft.Name is not null)];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyList<Draft> drafts, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        if (IsSealed)
            return;
        if (drafts.Count == 0)
        {
            await ClearAsync(cancellation_token).ConfigureAwait(false);
            return;
        }
        string json = JsonSerializer.Serialize([.. drafts], DraftJson.Default.ListDraft);
        await _gate.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            if (!IsSealed && !string.Equals(json, _last_written, StringComparison.Ordinal))
            {
                string staging = _path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                await File.WriteAllTextAsync(staging, json, cancellation_token).ConfigureAwait(false);
                File.Move(staging, _path, overwrite: true);
                _last_written = json;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellation_token)
    {
        if (IsSealed)
            return;
        await _gate.WaitAsync(cancellation_token).ConfigureAwait(false);
        try
        {
            if (!IsSealed)
            {
                _last_written = "";
                File.Delete(_path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool SaveNow(IReadOnlyList<Draft> drafts, TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        if (IsSealed || !_gate.Wait(budget))
            return false;
        try
        {
            if (IsSealed)
                return false;
            if (drafts.Count == 0)
            {
                File.Delete(_path);
                _last_written = "";
                return true;
            }
            string json = JsonSerializer.Serialize([.. drafts], DraftJson.Default.ListDraft);
            string staging = _path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(staging, json);
            File.Move(staging, _path, overwrite: true);
            _last_written = json;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Seal() => Interlocked.Exchange(ref _sealed, 1);

    public void Dispose() => _gate.Dispose();
}
