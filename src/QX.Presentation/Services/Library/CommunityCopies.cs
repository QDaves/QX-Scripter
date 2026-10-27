using Qx.Presentation.Dialogs;
using Qx.Presentation.Services.Files;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Threading;
using Qx.Presentation.ViewModels.Library;

namespace Qx.Presentation.Services.Library;

public sealed record CommunityOrigin(string Id, int Version, string Author, string Sha256, string? AcceptedSha256 = null, string? Title = null);

public sealed class CommunityCopies(IScriptFileService files, IScriptLibrary library, IDialogService dialogs, TimeProvider time, IUiDispatcher dispatcher)
{
    public async Task<string> SaveAsync(CommunityScript script, CancellationToken cancellation_token, Func<string, bool>? can_reuse = null)
    {
        IReadOnlyList<ScriptFileEntry> entries = await files.ListAsync(cancellation_token);
        foreach (ScriptFileEntry entry in entries)
        {
            if (library.Get(entry.Name).Community is not { } origin || origin.Id != script.Id || origin.Version != script.Version || origin.Sha256 != script.Sha256)
                continue;
            if ((can_reuse?.Invoke(entry.Path) ?? true) && await files.ReadAsync(entry.Path, cancellation_token) == script.Code)
                return entry.Path;
        }
        string name = ScriptFileName.Normalize(script.Title);
        if (files.Exists(files.PathFor(name)))
            name = ScriptFileName.NextCopy(name, candidate => files.Exists(files.PathFor(candidate)));
        string path = files.PathFor(name);
        FileOperationResult saved = await files.WriteAsync(path, script.Code, cancellation_token);
        if (!saved.Succeeded)
            throw new IOException(saved.Failure);
        library.Set(name, new ScriptMeta { Category = script.Category, Community = new(script.Id, script.Version, script.Author.Name, script.Sha256, Title: script.Title) });
        await library.FlushAsync(cancellation_token);
        return path;
    }

    public async Task<bool> AllowAsync(string? path, string code, CancellationToken cancellation_token, Action? review = null)
    {
        if (path is null)
            return true;
        string name = ScriptFileName.NameOf(path);
        ScriptMeta meta = library.Get(name);
        if (meta.Community is not { } origin)
            return true;
        CommunityOrigin? accepted = await ConfirmAsync(name, origin, code, cancellation_token, review);
        if (accepted is null)
            return false;
        if (accepted == origin)
            return true;
        library.Set(name, meta with { Community = accepted });
        await library.FlushAsync(cancellation_token);
        return true;
    }

    public async Task<bool> AllowDocumentAsync(ScriptDocument document, string code, CancellationToken cancellation_token, Action? review = null)
    {
        if (document.Community is not { } origin)
            return await AllowAsync(document.FilePath, code, cancellation_token, review);
        CommunityOrigin? accepted = await ConfirmAsync(document.Name, origin, code, cancellation_token, review);
        if (accepted is null)
            return false;
        document.Community = accepted;
        if (accepted != origin && document.LibraryName is { } name)
        {
            library.Set(name, library.Get(name) with { Community = accepted });
            await library.FlushAsync(cancellation_token);
        }
        return true;
    }

    async Task<CommunityOrigin?> ConfirmAsync(string name, CommunityOrigin origin, string code, CancellationToken cancellation_token, Action? review)
    {
        string hash = CommunityClient.Hash(code);
        if (origin.AcceptedSha256 == hash || library.IsCommunityApproved(origin.Id, hash))
        {
            await RememberAsync(origin.Id, hash, cancellation_token);
            return origin with { AcceptedSha256 = hash };
        }
        string suffix = $" - {origin.Author} v{origin.Version}";
        string title = origin.Title ?? (name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name);
        using var confirmation = new CommunityRunDialogViewModel(title, origin.Author, time, dispatcher);
        CommunityRunChoice choice = await dialogs.ShowAsync(confirmation, cancellation_token);
        if (choice == CommunityRunChoice.Review)
        {
            review?.Invoke();
            return null;
        }
        if (choice != CommunityRunChoice.Run)
            return null;
        await RememberAsync(origin.Id, hash, cancellation_token);
        return origin with { AcceptedSha256 = hash };
    }

    async Task RememberAsync(string id, string hash, CancellationToken cancellation_token)
    {
        library.ApproveCommunity(id, hash);
        await library.FlushAsync(cancellation_token);
    }
}
