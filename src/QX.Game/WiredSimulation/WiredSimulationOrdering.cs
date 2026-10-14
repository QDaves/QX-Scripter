using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static bool static_empty_action(WiredSimulationNode node) =>
        node.Name is "MoveAsGroup" or "RelativeFurniMove" or "MoveFurni" or "SetFurniAltitude" or "ToggleFurniState" or "ToggleToRandomState" &&
        furniture_source(node) == WiredFurnitureSource.Picked && (node.Furniture?.Count ?? 0) == 0;
}
