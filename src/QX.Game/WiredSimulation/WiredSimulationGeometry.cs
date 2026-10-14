namespace Qx.Game.WiredSimulation;

internal static class WiredSimulationGeometry
{
    internal static long height_value(double value) => checked((long)Math.Floor(value * 100 + 0.5));
}
