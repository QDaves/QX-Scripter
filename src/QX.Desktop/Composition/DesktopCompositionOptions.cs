using Qx.Presentation.Runtime;

namespace Qx.Desktop.Composition;

public sealed record DesktopCompositionOptions(RuntimeProfile Runtime, bool GlobalHotkeys, bool UpdateCheck, TimeProvider Time, int? McpPort = null)
{
    public static DesktopCompositionOptions Live { get; } = new(RuntimeProfile.Live, GlobalHotkeys: true, UpdateCheck: true, Time: TimeProvider.System);
}
