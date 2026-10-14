namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static int recolor_component(string name) => name switch
    {
        "~recolorable_furni.color.rgb" => 0,
        "~recolorable_furni.color.rgb.red" => 1,
        "~recolorable_furni.color.rgb.green" => 2,
        "~recolorable_furni.color.rgb.blue" => 3,
        _ => -1
    };

    private void recolor(long id, long color, Frame frame)
    {
        var item = furniture[id];
        if (item.RecolorRgb is null || item.RecolorPalette is null)
        {
            note(WiredSimulationSeverity.Unsupported, "recolor.metadata", "Recolor writes require a captured color and the current palette for this furniture class.", frame.Stack);
            status = WiredSimulationStatus.Faulted;
            throw new SimulationFailure();
        }
        if (color is < 0 or > 0xffffff || !item.RecolorPalette.Contains((int)color))
        {
            record("recolor.rejected", frame.Stack, "The requested color is outside the furniture palette.", [id]);
            return;
        }
        if (item.RecolorRgb == color) return;
        furniture[id] = item with { RecolorRgb = (int)color };
        writes++;
        charge(frame, 1, frame.Stack);
        record("recolor", frame.Stack, "Furniture color changed.", [id], item.RecolorRgb, color);
        effects.Add(new(now, "Recolor", frame.Stack ?? 0, [id], Value: color));
    }
}
