namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void freeze_users(WiredSimulationNode node, long[] people)
    {
        int effect = node.Number("freeze_effect") switch
        {
            0 => 218, 1 => 12, 2 => 11, 3 => 53, 4 => 163, _ => -1
        };
        if (effect < 0) unsupported(node, "Unknown freeze effect.");
        foreach (long id in people)
        {
            var user = users[id];
            if (user.NextStep is not null)
                unsupported(node, "Freezing an active walking path requires separate conformance.");
            if (user.Frozen && user.Freeze is null)
                unsupported(node, "Changing a captured freeze requires its previous-effect metadata.");
            int previous = user.Freeze?.PreviousEffect ?? user.Effect;
            if (previous != 0)
                unsupported(node, "Freezing over an existing avatar effect requires separate effect-priority conformance.");
            if (pending_teleport(id))
                unsupported(node, "Freezing during a classic teleport requires separate effect-priority conformance.");
            users[id] = user with { Frozen = true, Effect = effect,
                Freeze = new(previous, effect, node.Flag("cancel_on_teleport")) };
        }
    }

    private void release_users(WiredSimulationNode node, long[] people)
    {
        foreach (long id in people)
        {
            var user = users[id];
            if (user.Frozen && user.Freeze is null)
                unsupported(node, "Releasing a captured freeze requires its previous-effect metadata.");
            if (pending_teleport(id) && user.Frozen)
                unsupported(node, "Releasing a freeze during classic teleport requires separate effect-priority conformance.");
            users[id] = user with { Frozen = false, Effect = user.Freeze?.PreviousEffect ?? user.Effect, Freeze = null };
        }
    }
}
