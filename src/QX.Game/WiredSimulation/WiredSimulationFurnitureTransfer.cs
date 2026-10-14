namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static IReadOnlyList<WiredSimulationFurnitureTransferChoice>? copy_transfer_choices(
        IReadOnlyList<WiredSimulationFurnitureTransferChoice>? choices)
    {
        if (choices is null) return null;
        return choices.Select(choice =>
        {
            if (choice is null || choice.Steps is null || choice.Steps.Any(step => step is null))
                throw new ArgumentException("Furniture transfer choices require nonnull steps.", nameof(choices));
            return choice with { Steps = choice.Steps.ToArray() };
        }).ToArray();
    }

    private void transfer_furniture(WiredSimulationNode node, Frame frame, long[] sources)
    {
        var targets = fs(node, frame, 1);
        if (sources.Length == 0 || targets.Length == 0) return;
        bool large = sources.Concat(targets).Select(id => furniture[id]).Any(item => item.Width != 1 || item.Length != 1);
        if (large && (sources.Length > 1 || targets.Length > 1))
            unsupported(node, "Furniture transfer batches with larger footprints require separate conformance.");
        if (large && siblings(node).Any(addon => addon.Name is "Projectile" or "JumpStrength"))
            unsupported(node, "Larger furniture transfers with projectiles or movement curves require separate conformance.");
        if (targets.Length > 1 && targets.Any(sources.Contains))
            unsupported(node, "A multi-target transfer with targets that are also moving sources requires separate conformance.");
        if ((sources.Length > 1 || targets.Length > 1) &&
            (siblings(node).Any(addon => addon.Name == "CarryUsers") ||
             users.Values.Any(user => sources.Concat(targets).Any(id => covers(furniture[id], user.X, user.Y)))))
            unsupported(node, "Furniture transfer batches with carrying or occupied endpoints require separate conformance.");
        WiredSimulationFurnitureTransferStep[] steps;
        if (sources.Length == 1 && targets.Length == 1)
            steps = [new(sources[0], targets[0])];
        else if (options.FurnitureTransferChoices is { } choices && furniture_transfer_choice < choices.Count)
        {
            var choice = choices[furniture_transfer_choice];
            if (choice.ActionId != node.FurnitureId || choice.Steps.Count != sources.Length ||
                choice.Steps.Select(step => step.FurnitureId).Distinct().Count() != sources.Length ||
                choice.Steps.Any(step => !sources.Contains(step.FurnitureId) || !targets.Contains(step.TargetId)))
                unsupported(node, "The next furniture transfer choice must assign every selected source exactly once to a selected target for this action.");
            steps = choice.Steps.ToArray();
            furniture_transfer_choice++;
        }
        else
        {
            uncertain("movement.transfer.choice", "Furniture transfer order and target choices are not recoverable from saved selection or movement packets. Supply FurnitureTransferChoices for a known branch; a permissive run samples one possible branch.", node.FurnitureId);
            var order = sources.ToArray();
            for (int index = order.Length - 1; index > 0; index--)
            {
                int other = (int)(next_random() % (uint)(index + 1));
                (order[index], order[other]) = (order[other], order[index]);
            }
            steps = order.Select(id => new WiredSimulationFurnitureTransferStep(id, targets.Length == 1 ? targets[0] : choose(targets))).ToArray();
        }
        foreach (var step in steps)
        {
            var item = furniture[step.FurnitureId];
            var target = furniture[step.TargetId];
            if (item.X == target.X && item.Y == target.Y && furniture.Values
                .Where(other => other.Id != item.Id && overlaps(item, other))
                .All(other => other.Z + other.Height <= item.Z + item.Height + 0.0001)) continue;
            move_item(item, target.X, target.Y, null, frame, transfer: true);
        }
    }
}
