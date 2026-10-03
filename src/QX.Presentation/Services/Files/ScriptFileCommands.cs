using Qx.Presentation.Dialogs;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.Library;
using Qx.Presentation.Services.Lifecycle;
using Qx.Presentation.Services.Output;
using Qx.Presentation.Services.Runs;
using Qx.Presentation.Services.Settings;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.Visuals;
using Qx.Scripting.Hosting;

namespace Qx.Presentation.Services.Files;

public enum CloseOutcome
{
    Closed,
    Stopping,
    Cancelled
}

public interface IScriptFileCommands
{
    ScriptDocument NewDocument();

    Task<ScriptDocument?> OpenAsync(string path, CancellationToken cancellationToken);

    Task OpenDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);

    Task<bool> SaveAsync(ScriptDocument document, CancellationToken cancellationToken);

    Task<bool> SaveAsAsync(ScriptDocument document, CancellationToken cancellationToken);

    Task<bool> RenameAsync(ScriptDocument document, CancellationToken cancellationToken);

    Task<bool> RenameFileAsync(string path, CancellationToken cancellationToken);

    Task<bool> DuplicateAsync(string path, CancellationToken cancellationToken);

    Task<bool> SetGroupAsync(string path, string? group, CancellationToken cancellationToken);

    Task<bool> SaveInPlaceAsync(ScriptDocument document, CancellationToken cancellationToken);

    Task<FileOperationResult> RenameUnattendedAsync(string path, string typed, CancellationToken cancellationToken);

    Task<FileOperationResult> DeleteUnattendedAsync(string path, CancellationToken cancellationToken);

    Task RevealAsync(string path, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string path, CancellationToken cancellationToken);

    Task<CloseOutcome> CloseAsync(ScriptDocument document, CancellationToken cancellationToken);

    Task CloseOthersAsync(ScriptDocument keep, CancellationToken cancellationToken);

    Task CloseToTheRightAsync(ScriptDocument anchor, CancellationToken cancellationToken);

    Task<bool> ReopenClosedAsync(CancellationToken cancellationToken);
}

public sealed class ScriptFileCommands(
    IScriptWorkspace workspace,
    IScriptFileService files,
    IScriptLibrary library,
    IScriptRunRegistry runs,
    ISettingsStore settings,
    IDialogService dialogs,
    INavigationService navigation,
    IFileRevealer revealer) : IScriptFileCommands
{
    public ScriptDocument NewDocument()
    {
        ScriptDocument document = workspace.AddNew();
        navigation.Navigate(PageKey.Editor);
        return document;
    }

    public async Task<ScriptDocument?> OpenAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        OpenResult opened = await workspace.OpenAsync(path, cancellationToken);
        if (opened.Outcome is OpenOutcome.Missing or OpenOutcome.Unreadable)
            return null;
        if (opened.Document is not { } document)
            return null;
        workspace.Active = document;
        navigation.Navigate(PageKey.Editor);
        return document;
    }

    public async Task OpenDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (string path in paths.Where(candidate => ScriptFileName.IsScript(candidate) && files.Exists(candidate)))
            await OpenAsync(path, cancellationToken);
    }

    public async Task<bool> SaveAsync(ScriptDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FilePath is not { } known)
            return await SaveThroughPromptAsync(document, cancellationToken);
        if (!await WriteAsync(document, known, cancellationToken))
            return false;
        await FollowDeclaredNameAsync(document, known, cancellationToken);
        return true;
    }

    public async Task<bool> SaveAsAsync(ScriptDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return await SaveThroughPromptAsync(document, cancellationToken);
    }

    public async Task<bool> RenameAsync(ScriptDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        string? typed = await AskForNameAsync(document.Name, cancellationToken);
        if (typed is null || !Alive(document))
            return false;
        DeclareName(document, typed);
        if (document.FilePath is not { } path)
        {
            document.Rename(typed);
            return true;
        }
        return await MoveFileAsync(path, typed, cancellationToken);
    }

    public async Task<bool> RenameFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string from = PathComparison.Full(path);
        string? typed = await AskForNameAsync(ScriptFileName.NameOf(from), cancellationToken);
        if (typed is null || !await MoveFileAsync(from, typed, cancellationToken))
            return false;
        await DeclareNameInFileAsync(files.PathFor(typed), cancellationToken);
        return true;
    }

    public Task<bool> SaveInPlaceAsync(ScriptDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.FilePath is { } path ? WriteAsync(document, path, cancellationToken) : Task.FromResult(false);
    }

    public async Task<FileOperationResult> RenameUnattendedAsync(string path, string typed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(typed);
        string from = PathComparison.Full(path);
        string to = files.PathFor(typed);
        if (string.Equals(from, to, StringComparison.Ordinal))
            return FileOperationResult.Ok;
        if (runs.LivePaths.Contains(from))
            return new FileOperationResult(false, $"“{ScriptFileName.NameOf(from)}” is running; stop it before renaming it.");
        if (!PathComparison.Same(from, to))
        {
            if (Occupied(to, null) is { } holder)
                return new FileOperationResult(false, $"“{holder.Name}” is open in another tab.");
            if (files.Exists(to))
                return new FileOperationResult(false, $"A script named “{ScriptFileName.NameOf(to)}” already exists.");
        }
        FileOperationResult moved = await MoveCoreAsync(from, to, cancellationToken);
        if (moved.Succeeded)
            await DeclareNameInFileAsync(to, cancellationToken);
        return moved;
    }

    public async Task<FileOperationResult> DeleteUnattendedAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = PathComparison.Full(path);
        if (runs.LivePaths.Contains(full))
            return new FileOperationResult(false, $"“{ScriptFileName.NameOf(full)}” has a run behind it; stop it first.");
        return await DeleteCoreAsync(full, cancellationToken);
    }

    public async Task<bool> DuplicateAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string from = PathComparison.Full(path);
        string name = ScriptFileName.NextCopy(ScriptFileName.NameOf(from), candidate => files.Exists(files.PathFor(candidate)));
        string to = files.PathFor(name);
        FileOperationResult copied = await files.CopyAsync(from, to, cancellationToken);
        if (!copied.Succeeded)
        {
            await dialogs.AlertAsync("Duplicate failed", copied.Failure ?? "The script could not be duplicated.", cancellationToken);
            return false;
        }
        ScriptMeta meta = library.Get(ScriptFileName.NameOf(from));
        if (!meta.IsEmpty)
            library.Set(name, meta);
        CopyPanelMemory(from, to);
        await DeclareNameInFileAsync(to, cancellationToken);
        return true;
    }

    public Task<bool> SetGroupAsync(string path, string? group, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return RewriteHeaderAsync(PathComparison.Full(path), code => ScriptHeader.WithGroup(code, group), cancellationToken);
    }

    public async Task RevealAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!await revealer.RevealAsync(path, cancellationToken))
            await dialogs.AlertAsync("Could not open the folder", "The folder could not be opened.", cancellationToken);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = PathComparison.Full(path);
        string name = ScriptFileName.NameOf(full);
        if (runs.LivePaths.Contains(full))
        {
            await dialogs.AlertAsync("Script is still open", $"“{name}” has a run behind it. Stop it or close its tab before deleting the script.", cancellationToken);
            return false;
        }
        bool confirmed = await dialogs.ConfirmAsync(
            "Delete script?",
            $"“{name}” will be permanently deleted from the script library.",
            "Delete",
            DialogTone.Destructive,
            cancellationToken: cancellationToken);
        if (!confirmed)
            return false;
        FileOperationResult deleted = await DeleteCoreAsync(full, cancellationToken);
        if (!deleted.Succeeded)
        {
            await dialogs.AlertAsync("Delete failed", deleted.Failure ?? "The script could not be deleted.", cancellationToken);
            return false;
        }
        return true;
    }

    async Task<FileOperationResult> DeleteCoreAsync(string full, CancellationToken cancellation_token)
    {
        FileOperationResult deleted = await files.DeleteAsync(full, cancellation_token);
        if (!deleted.Succeeded)
            return deleted;
        library.Remove(ScriptFileName.NameOf(full));
        ForgetPanelMemory(full);
        if (workspace.FindByPath(full) is { } document && !document.IsClosed)
            document.MarkUnsaved();
        return deleted;
    }

    public async Task<CloseOutcome> CloseAsync(ScriptDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Run.IsWorking)
        {
            document.Run.RequestStop();
            return CloseOutcome.Stopping;
        }
        if (document.IsModified)
        {
            bool discard = await dialogs.ConfirmAsync(
                "Discard unsaved changes?",
                $"“{document.Name}” has unsaved changes. Close it and discard those changes?",
                "Discard",
                DialogTone.Destructive,
                cancellationToken: cancellationToken);
            if (!Alive(document))
                return CloseOutcome.Closed;
            if (!discard)
                return CloseOutcome.Cancelled;
        }
        workspace.Remove(document);
        return CloseOutcome.Closed;
    }

    public Task CloseOthersAsync(ScriptDocument keep, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keep);
        return CloseManyAsync(keep, [.. workspace.Documents.Where(document => !ReferenceEquals(document, keep))], cancellationToken);
    }

    public Task CloseToTheRightAsync(ScriptDocument anchor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        int index = workspace.Documents.IndexOf(anchor);
        return index < 0
            ? Task.CompletedTask
            : CloseManyAsync(anchor, [.. workspace.Documents.Skip(index + 1)], cancellationToken);
    }

    async Task CloseManyAsync(ScriptDocument keep, ScriptDocument[] others, CancellationToken cancellation_token)
    {
        if (others.Length == 0)
            return;
        int modified = others.Count(document => document.IsModified);
        if (modified > 0)
        {
            bool discard = await dialogs.ConfirmAsync(
                "Close other scripts?",
                modified == 1
                    ? "One other script has unsaved changes. Close the others and discard them?"
                    : $"{modified} other scripts have unsaved changes. Close them and discard those changes?",
                "Discard and close",
                DialogTone.Destructive,
                cancellationToken: cancellation_token);
            if (!discard || !Alive(keep))
                return;
        }
        var stopping = new List<ScriptDocument>();
        foreach (ScriptDocument document in others)
        {
            if (!workspace.Contains(document))
                continue;
            if (document.Run.IsWorking)
            {
                document.Run.RequestStop();
                stopping.Add(document);
                continue;
            }
            workspace.Remove(document);
        }
        foreach (ScriptDocument document in stopping)
        {
            try
            {
                await document.Run.Completion.WaitAsync(ShellCloseCoordinator.RunStopBudget);
            }
            catch (Exception error) when (error is TimeoutException or OperationCanceledException)
            {
            }
            if (workspace.Contains(document) && !document.Run.IsWorking)
                workspace.Remove(document);
        }
        if (workspace.Contains(keep))
            workspace.Active = keep;
    }

    public async Task<bool> ReopenClosedAsync(CancellationToken cancellationToken)
    {
        if (workspace.TakeReopenable() is not { } path)
            return false;
        return await OpenAsync(path, cancellationToken) is not null;
    }

    async Task<bool> MoveFileAsync(string path, string typed, CancellationToken cancellation_token)
    {
        string from = PathComparison.Full(path);
        string to = files.PathFor(typed);
        if (string.Equals(from, to, StringComparison.Ordinal))
            return false;
        if (!PathComparison.Same(from, to))
        {
            if (Occupied(to, null) is { } holder)
            {
                await AlertOpenAsync(holder, cancellation_token);
                return false;
            }
            if (files.Exists(to) && !await ConfirmReplaceAsync(to, cancellation_token))
                return false;
        }
        FileOperationResult moved = await MoveCoreAsync(from, to, cancellation_token);
        if (!moved.Succeeded)
        {
            await dialogs.AlertAsync("Rename failed", moved.Failure ?? "The script could not be renamed.", cancellation_token);
            return false;
        }
        return true;
    }

    async Task<FileOperationResult> MoveCoreAsync(string from, string to, CancellationToken cancellation_token)
    {
        FileOperationResult moved = await files.MoveAsync(from, to, cancellation_token);
        if (!moved.Succeeded)
            return moved;
        library.Rename(ScriptFileName.NameOf(from), ScriptFileName.NameOf(to));
        MovePanelMemory(from, to);
        workspace.NoteMoved(from, to);
        if (workspace.FindByPath(from) is { } document && !document.IsClosed)
        {
            bool modified = document.IsModified;
            document.MoveTo(to);
            if (modified)
                document.MarkModified();
        }
        return moved;
    }

    Task<bool> DeclareNameInFileAsync(string path, CancellationToken cancellation_token)
    {
        string name = ScriptFileName.NameOf(path);
        return RewriteHeaderAsync(path, code => ScriptHeader.Parse(code).Name is null ? code : ScriptHeader.WithName(code, name), cancellation_token);
    }

    Task<string?> AskForNameAsync(string current, CancellationToken cancellation_token) =>
        dialogs.PromptAsync(new PromptRequest("Rename script", current, "Rename", "Script name", IconKind.Rename), cancellation_token);

    async Task FollowDeclaredNameAsync(ScriptDocument document, string path, CancellationToken cancellation_token)
    {
        if (ScriptFileName.FromDirective(document.Text) is not { } declared ||
            string.Equals(declared, ScriptFileName.NameOf(path), StringComparison.Ordinal))
        {
            return;
        }
        if (document.Run.IsAlive)
        {
            document.Run.Output.Write($"The script declares the name “{declared}”; stop it to rename the file.", OutputLevel.Warning);
            return;
        }
        await MoveFileAsync(path, declared, cancellation_token);
    }

    static void DeclareName(ScriptDocument document, string name)
    {
        if (ScriptHeader.Parse(document.Text).Name is not null)
            document.ReplaceText(ScriptHeader.WithName(document.Text, name));
    }

    async Task<bool> RewriteHeaderAsync(string path, Func<string, string> rewrite, CancellationToken cancellation_token)
    {
        if (workspace.FindByPath(path) is { IsClosed: false } document)
        {
            bool clean = !document.IsModified;
            string updated = rewrite(document.Text);
            if (string.Equals(updated, document.Text, StringComparison.Ordinal))
                return true;
            document.ReplaceText(updated);
            return !clean || await WriteAsync(document, path, cancellation_token);
        }
        if (await files.ReadAsync(path, cancellation_token) is not { } code)
            return false;
        string rewritten = rewrite(code);
        if (string.Equals(rewritten, code, StringComparison.Ordinal))
            return true;
        FileOperationResult written = await files.WriteAsync(path, rewritten, cancellation_token);
        if (!written.Succeeded)
            await dialogs.AlertAsync("Update failed", written.Failure ?? "The script could not be written.", cancellation_token);
        return written.Succeeded;
    }

    async Task<bool> SaveThroughPromptAsync(ScriptDocument document, CancellationToken cancellation_token)
    {
        string? typed = await dialogs.PromptAsync(new PromptRequest("Save script", document.Name, "Save", "Script name", IconKind.Save), cancellation_token);
        if (typed is null || !Alive(document))
            return false;
        DeclareName(document, typed);
        string target = files.PathFor(typed);
        if (PathComparison.Same(document.FilePath, target))
            return await WriteAsync(document, target, cancellation_token);
        if (Occupied(target, document) is { } holder)
        {
            await AlertOpenAsync(holder, cancellation_token);
            return false;
        }
        if (files.Exists(target) && !await ConfirmReplaceAsync(target, cancellation_token))
            return false;
        if (!Alive(document))
            return false;
        string? previous = document.FilePath;
        if (!await WriteAsync(document, target, cancellation_token))
            return false;
        if (previous is { Length: > 0 })
        {
            ScriptMeta meta = library.Get(ScriptFileName.NameOf(previous));
            if (!meta.IsEmpty)
                library.Set(ScriptFileName.NameOf(target), meta);
            CopyPanelMemory(previous, target);
        }
        return true;
    }

    async Task<bool> WriteAsync(ScriptDocument document, string path, CancellationToken cancellation_token)
    {
        string text = document.Text;
        FileOperationResult written = await files.WriteAsync(path, text, cancellation_token);
        if (!Alive(document))
            return false;
        if (!written.Succeeded)
        {
            document.Run.Output.Write("Save failed: " + (written.Failure ?? "the script could not be written."), OutputLevel.Error);
            return false;
        }
        document.MarkSaved(path, text);
        return true;
    }

    async Task<bool> ConfirmReplaceAsync(string target, CancellationToken cancellation_token) =>
        await dialogs.ConfirmAsync(
            "Replace existing script?",
            $"A script named “{ScriptFileName.NameOf(target)}” already exists.",
            "Replace",
            DialogTone.Destructive,
            cancellationToken: cancellation_token);

    Task AlertOpenAsync(ScriptDocument holder, CancellationToken cancellation_token) =>
        dialogs.AlertAsync("Script is already open", $"“{holder.Name}” is open in another tab. Close it or pick another name.", cancellation_token);

    ScriptDocument? Occupied(string target, ScriptDocument? self)
    {
        ScriptDocument? holder = workspace.FindByPath(target);
        return holder is null || ReferenceEquals(holder, self) ? null : holder;
    }

    bool Alive(ScriptDocument document) => workspace.Contains(document) && !document.IsClosed;

    void MovePanelMemory(string from, string to)
    {
        ScriptPanelMemory? memory = settings.PanelFor(from);
        ForgetPanelMemory(from);
        if (memory is not null)
            settings.RememberPanel(to, memory.Panel, memory.Values);
    }

    void CopyPanelMemory(string from, string to)
    {
        if (settings.PanelFor(from) is { } memory)
            settings.RememberPanel(to, memory.Panel, memory.Values);
    }

    void ForgetPanelMemory(string path) => settings.ForgetPanel(path);
}
