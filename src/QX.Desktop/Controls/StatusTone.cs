using Avalonia.Controls;

namespace Qx.Desktop.Controls;

public enum StatusTone
{
    Neutral,
    Accent,
    Running,
    Success,
    Warning,
    Danger,
    Info
}

static class StatusTones
{
    public static void Apply(IPseudoClasses classes, StatusTone tone)
    {
        classes.Set(":neutral", tone == StatusTone.Neutral);
        classes.Set(":accent", tone == StatusTone.Accent);
        classes.Set(":running", tone == StatusTone.Running);
        classes.Set(":success", tone == StatusTone.Success);
        classes.Set(":warning", tone == StatusTone.Warning);
        classes.Set(":danger", tone == StatusTone.Danger);
        classes.Set(":info", tone == StatusTone.Info);
    }
}
