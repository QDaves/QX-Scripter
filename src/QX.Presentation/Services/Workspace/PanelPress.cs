using Qx.Presentation.Services.Runs;

namespace Qx.Presentation.Services.Workspace;

public static class PanelPress
{
    public static void Route(ScriptRunController run, string? button)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (button is { Length: > 0 })
        {
            run.Press(button);
            return;
        }
        if (run.IsAlive)
            run.RequestStop();
        else
            run.Start(null, panel_mode: true);
    }
}
