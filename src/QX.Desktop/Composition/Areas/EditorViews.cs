using Qx.Desktop.Editor;
using Qx.Desktop.Views.Editor;
using Qx.Presentation.ViewModels.Editor;

namespace Qx.Desktop.Composition.Areas;

static class EditorViews
{
    public static void Register(ViewRegistry views, RoslynHostProvider hosts)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(hosts);
        views.Register<WorkspaceViewModel>(() =>
        {
            var editor = new WorkspaceView();
            editor.Use(hosts);
            return editor;
        });
    }
}
