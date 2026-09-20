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

    Task<ScriptDocument?> OpenAsync(string path, CancellationToken cancellation_token);

    Task OpenDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellation_token);

    Task<bool> SaveAsync(ScriptDocument document, CancellationToken cancellation_token);

    Task<bool> SaveAsAsync(ScriptDocument document, CancellationToken cancellation_token);

    Task<bool> RenameAsync(ScriptDocument document, CancellationToken cancellation_token);

    Task<bool> RenameFileAsync(string path, CancellationToken cancellation_token);

    Task<bool> DuplicateAsync(string path, CancellationToken cancellation_token);

    Task RevealAsync(string path, CancellationToken cancellation_token);

    Task<bool> DeleteAsync(string path, CancellationToken cancellation_token);

    Task<CloseOutcome> CloseAsync(ScriptDocument document, CancellationToken cancellation_token);

    Task CloseOthersAsync(ScriptDocument keep, CancellationToken cancellation_token);

    Task CloseToTheRightAsync(ScriptDocument anchor, CancellationToken cancellation_token);

    Task<bool> ReopenClosedAsync(CancellationToken cancellation_token);
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

    public async Task<ScriptDocument?> OpenAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        OpenResult opened = await workspace.OpenAsync(path, cancellation_token);
        if (opened.Outcome is OpenOutcome.Missing or OpenOutcome.Unreadable)
            return null;
        if (opened.Document is not { } document)
            return null;
        workspace.Active = document;
        navigation.Navigate(PageKey.Editor);
        return document;
    }

    public async Task OpenDroppedAsync(IReadOnlyList<string> paths, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (string path in paths.Where(candidate => ScriptFileName.IsScript(candidate) && files.Exists(candidate)))
            await OpenAsync(path, cancellation_token);
    }

    public async Task<bool> SaveAsync(ScriptDocument document, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FilePath is { } known)
            return await WriteAsync(document, known, cancellation_token);
        return await SaveThroughPromptAsync(document, cancellation_token);
    }

    public async Task<bool> SaveAsAsync(ScriptDocument document, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        return await SaveThroughPromptAsync(document, cancellation_token);
    }

    public async Task<bool> RenameAsync(ScriptDocument document, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(document);
        string? typed = await AskForNameAsync(document.Name, cancellation_token);
        if (typed is null || !Alive(document))
            return false;
        if (document.FilePath is not { } path)
        {
            document.Rename(typed);
            return true;
        }
        return await MoveFileAsync(path, typed, cancellation_token);
    }

    public async Task<bool> RenameFileAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string from = PathComparison.Full(path);
        string? typed = await AskForNameAsync(ScriptFileName.NameOf(from), cancellation_token);
        return typed is not null && await MoveFileAsync(from, typed, cancellation_token);
    }

    public async Task<bool> DuplicateAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string from = PathComparison.Full(path);
        string name = ScriptFileName.NextCopy(ScriptFileName.NameOf(from), candidate => files.Exists(files.PathFor(candidate)));
        string to = files.PathFor(name);
        FileOperationResult copied = await files.CopyAsync(from, to, cancellation_token);
        if (!copied.Succeeded)
        {
            await dialogs.AlertAsync("Duplicate failed", copied.Failure ?? "The script could not be duplicated.", cancellation_token);
            return false;
        }
        ScriptMeta meta = library.Get(ScriptFileName.NameOf(from));
        if (!meta.IsEmpty)
            library.Set(name, meta);
        CopyPanelMemory(from, to);
        return true;
    }

    public async Task RevealAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!await revealer.RevealAsync(path, cancellation_token))
            await dialogs.AlertAsync("Could not open the folder", "The folder could not be opened.", cancellation_token);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellation_token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = PathComparison.Full(path);
        string name = ScriptFileName.NameOf(full);
        if (runs.LivePaths.Contains(full))
        {
            await dialogs.AlertAsync("Script is still open", $"“{name}” has a run behind it. Stop it or close its tab before deleting the script.", cancellation_token);
            return false;
        }
        bool confirmed = await dialogs.ConfirmAsync(
            "Delete script?",
            $"“{name}” will be permanently deleted from the script library.",
            "Delete",
            DialogTone.Destructive,
            cancellation_token: cancellation_token);
        if (!confirmed)
            return false;
        FileOperationResult deleted = await files.DeleteAsync(full, cancellation_token);
        if (!deleted.Succeeded)
        {
            await dialogs.AlertAsync("Delete failed", deleted.Failure ?? "The script could not be deleted.", cancellation_token);
            return false;
        }
        library.Remove(name);
        ForgetPanelMemory(full);
        if (workspace.FindByPath(full) is { } document && !document.IsClosed)
            document.MarkUnsaved();
        return true;
    }

    public async Task<CloseOutcome> CloseAsync(ScriptDocument document, CancellationToken cancellation_token)
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
                cancellation_token: cancellation_token);
            if (!Alive(document))
                return CloseOutcome.Closed;
            if (!discard)
                return CloseOutcome.Cancelled;
        }
        workspace.Remove(document);
        return CloseOutcome.Closed;
    }

    public Task CloseOthersAsync(ScriptDocument keep, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(keep);
        return CloseManyAsync(keep, [.. workspace.Documents.Where(document => !ReferenceEquals(document, keep))], cancellation_token);
    }

    public Task CloseToTheRightAsync(ScriptDocument anchor, CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        int index = workspace.Documents.IndexOf(anchor);
        return index < 0
            ? Task.CompletedTask
            : CloseManyAsync(anchor, [.. workspace.Documents.Skip(index + 1)], cancellation_token);
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
                cancellation_token: cancellation_token);
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

    public async Task<bool> ReopenClosedAsync(CancellationToken cancellation_token)
    {
        if (workspace.TakeReopenable() is not { } path)
            return false;
        return await OpenAsync(path, cancellation_token) is not null;
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
        FileOperationResult moved = await files.MoveAsync(from, to, cancellation_token);
        if (!moved.Succeeded)
        {
            await dialogs.AlertAsync("Rename failed", moved.Failure ?? "The script could not be renamed.", cancellation_token);
            return false;
        }
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
        return true;
    }

    Task<string?> AskForNameAsync(string current, CancellationToken cancellation_token) =>
        dialogs.PromptAsync(new PromptRequest("Rename script", current, "Rename", "Script name", IconKind.Rename), cancellation_token);

    async Task<bool> SaveThroughPromptAsync(ScriptDocument document, CancellationToken cancellation_token)
    {
        string? typed = await dialogs.PromptAsync(new PromptRequest("Save script", document.Name, "Save", "Script name", IconKind.Save), cancellation_token);
        if (typed is null || !Alive(document))
            return false;
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
            cancellation_token: cancellation_token);

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
