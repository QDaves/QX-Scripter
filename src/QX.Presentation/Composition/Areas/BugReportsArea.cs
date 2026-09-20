using Microsoft.Extensions.DependencyInjection;
using Qx.Diagnostics;
using Qx.Presentation.Navigation;
using Qx.Presentation.Platform;
using Qx.Presentation.Services.BugReports;
using Qx.Presentation.Services.Status;
using Qx.Presentation.ViewModels.BugReports;

namespace Qx.Presentation.Composition.Areas;

public static class BugReportsArea
{
    public static IServiceCollection AddBugReportsArea(this IServiceCollection services) =>
        services
            .AddSingleton<IBugReportService>(static provider => new BugReportService(
                provider.GetRequiredService<IAppPaths>(),
                Facts(provider.GetRequiredService<ISessionStatusService>()),
                provider.GetRequiredService<TimeProvider>()))
            .AddPage<BugReportViewModel>(PageKey.BugReport);

    static Func<BugReportContext> Facts(ISessionStatusService status) =>
        () => BugReportFacts.From(status.Current);
}
