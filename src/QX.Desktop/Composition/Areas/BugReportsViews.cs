using Qx.Desktop.Views.BugReports;
using Qx.Presentation.ViewModels.BugReports;

namespace Qx.Desktop.Composition.Areas;

static class BugReportsViews
{
    public static void Register(ViewRegistry views) =>
        views.Register<BugReportViewModel>(static () => new BugReportView());
}
