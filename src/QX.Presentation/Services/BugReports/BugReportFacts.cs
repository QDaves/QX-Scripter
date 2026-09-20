using System.Globalization;
using System.Runtime.InteropServices;
using Qx.Diagnostics;
using Qx.Presentation.Services.Status;

namespace Qx.Presentation.Services.BugReports;

public static class BugReportFacts
{
    public const string NotConnected = "not connected";

    public static BugReportContext From(SessionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new BugReportContext(
            ProductVersion.Current,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            ".NET " + Environment.Version,
            status.IsGEarthConnected
                ? "connected on port " + status.GEarthPort.ToString(CultureInfo.InvariantCulture)
                : NotConnected,
            status.ClientTooltip.Length == 0 ? NotConnected : status.ClientTooltip,
            status.IsMcpRunning
                ? "running on port " + status.McpPort.ToString(CultureInfo.InvariantCulture)
                : "not running");
    }
}
