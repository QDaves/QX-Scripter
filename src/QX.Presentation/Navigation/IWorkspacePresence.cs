namespace Qx.Presentation.Navigation;

public interface IWorkspacePresence
{
    bool HasDocuments { get; }

    bool IsCodeViewActive { get; }
}
