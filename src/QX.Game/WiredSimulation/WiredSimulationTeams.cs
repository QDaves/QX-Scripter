using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private int[]? team_order;

    private void restore_team_order(IReadOnlyList<int>? order)
    {
        if (order is null)
        {
            infer_team_order();
            return;
        }
        if (order.Count != 4 || order.Order().Where((team, index) => team != index + 1).Any() ||
            Enumerable.Range(1, 4).Any(team => !team_scores.ContainsKey(team)) ||
            order.Zip(order.Skip(1)).Any(pair => team_scores[pair.First] < team_scores[pair.Second]))
        {
            fail("snapshot.team_order", "Team order must contain each team exactly once and follow the supplied scores.");
            return;
        }
        team_order = order.ToArray();
    }

    private void infer_team_order()
    {
        if (Enumerable.Range(1, 4).All(team_scores.ContainsKey) && team_scores.Where(pair => pair.Key is >= 1 and <= 4).Select(pair => pair.Value).Distinct().Count() == 4)
            team_order = Enumerable.Range(1, 4).OrderByDescending(team => team_scores[team]).ToArray();
    }

    private void set_team_score(int team, long score)
    {
        team_scores[team] = score;
        if (team_order is not null) team_order = team_order.OrderByDescending(candidate => team_scores[candidate]).ToArray();
        else infer_team_order();
    }

    private bool team_placement(WiredSimulationNode node, Frame frame)
    {
        int team = (int)node.Number("team"), placement = (int)node.Number("placement");
        if (team is < 0 or > 4 || placement is < 0 or > 3)
            unsupported(node, "Team placement requires team 0..4 and placement 0..3.");
        if (team == 0) team = frame.Event.UserId is { } player && users.TryGetValue(player, out var user) ? user.Team : 0;
        if (team == 0) return false;
        if (Enumerable.Range(1, 4).Any(candidate => !team_scores.ContainsKey(candidate)))
            unsupported(node, "Team placement requires explicit scores for all four teams.");
        if (team_order is null)
            unsupported(node, "Equal team scores require the current TeamOrder or a previously established unique ranking.");
        return team_order![placement] == team;
    }
}
