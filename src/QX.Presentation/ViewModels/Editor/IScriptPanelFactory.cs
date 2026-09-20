using Qx.Presentation.Services.Editor;
using Qx.Presentation.Services.Workspace;
using Qx.Presentation.ViewModels.ApiBrowser;
using Qx.Presentation.ViewModels.ScriptPanels;

namespace Qx.Presentation.ViewModels.Editor;

public interface IScriptPanelFactory
{
    ScriptPanelViewModel Create(ScriptDocument document);
}

public interface IApiBrowserFactory
{
    ApiBrowserViewModel Create(IApiInsertTarget target);
}
