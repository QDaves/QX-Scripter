using Qx.Model;
using Qx.Model.Messages.Incoming;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private int? movement_curve(IReadOnlyList<WiredSimulationNode> addons, Frame frame, bool ignore_cancellation = false)
    {
        var curves = addons.Where(node => node.Name == "JumpStrength").ToArray();
        if (curves.Length == 0) return null;
        var curve = curves[0];
        if (curves.Length > 1) unsupported(curve, "Multiple movement curves require separate precedence conformance.");
        if (!ignore_cancellation && addons.Any(node => node.Name == "NoMoveAnimation")) return null;
        var value = operand(curve, frame, "operand_kind", "strength", "reference_target", "reference_variable", 0);
        if (value is null) unsupported(curve, "An absent movement-curve operand requires separate conformance.");
        if (curve.Number("operand_kind") == 0 && value is (< -1000 or > 1000))
            unsupported(curve, "Constant movement curves must remain within the configured range.");
        if (value is < int.MinValue or > int.MaxValue)
            unsupported(curve, "Movement-curve variables outside the protocol integer range require separate conformance.");
        return value == 0 ? null : (int)value!.Value;
    }

    private static Tile position(WiredSimulationFurniture item) => new(item.X, item.Y, (float)item.Z);
    private static Tile position(WiredSimulationUser user) => new(user.X, user.Y, (float)user.Z);

    private static WiredSimulationMovement furniture_motion(WiredSimulationFurniture before, WiredSimulationFurniture after,
        long duration, int? curve) => new(WiredMovementType.FloorItem, position(before), position(after), checked((int)duration), after.Direction, CurveStrength: curve);

    private static WiredSimulationMovement user_motion(WiredSimulationUser before, WiredSimulationUser after,
        long duration, int? curve) => new(WiredMovementType.Avatar, position(before), position(after), checked((int)duration),
            after.Direction, after.Direction, IsSlide: true, JumpPower: curve);
}
