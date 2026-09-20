using System.Collections.ObjectModel;
using Qx.Presentation.Services.Drafts;
using Qx.Presentation.Services.Lifecycle;

namespace Qx.Presentation.Services.Workspace;

public enum OpenOutcome
{
    Opened,
    AlreadyOpen,
    Missing,
    Unreadable
}

public sealed record OpenResult(OpenOutcome Outcome, ScriptDocument? Document, string? Failure = null);

public interface IScriptWorkspace : IWorkspaceSession
{
    ReadOnlyObservableCollection<ScriptDocument> Documents { get; }

    ScriptDocument? Active { get; set; }

    bool CanReopenClosed { get; }

    event Action? DocumentsChanged;

    event Action<ScriptDocument?>? ActiveChanged;

    ScriptDocument AddNew();

    ScriptDocument Add(string name, string code, string? path, bool modified);

    Task<OpenResult> OpenAsync(string path, CancellationToken cancellation_token);

    ScriptDocument? FindByPath(string path);

    ScriptDocument? FindByName(string name);

    bool Contains(ScriptDocument document);

    ScriptDocument? Adjacent(int offset);

    void Move(ScriptDocument document, int index);

    void Remove(ScriptDocument document);

    void NoteMoved(string from, string to);

    string? TakeReopenable();

    void RememberPanel(ScriptDocument document);

    IReadOnlyList<Draft> CaptureDrafts(bool include_modified_files);

    Task RestoreAsync(CancellationToken cancellation_token);
}
