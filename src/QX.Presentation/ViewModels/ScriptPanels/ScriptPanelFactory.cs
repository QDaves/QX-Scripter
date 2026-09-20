using Qx.Presentation.Platform;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Presentation.ViewModels.ScriptPanels;

public sealed class ScriptPanelFactory(IFilePickerService pickers) : IScriptPanelFactory
{
    public ScriptPanelViewModel Create(ScriptDocument document) => new(document, pickers);
}
