using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void give_legacy_reward(WiredSimulationNode node, long[] people)
    {
        if (people.Length == 0) return;
        if (people.Any(id => users[id].IsBot || users[id].IsPet))
            unsupported(node, "Legacy rewards require human recipients.");
        var rewards = node.Fields["rewards"].Rewards!;
        var outcomes = options.RewardOutcomes;
        if (outcomes is null || outcomes.Count - reward_outcome_index < people.Length)
        {
            note(WiredSimulationSeverity.Uncertain, "reward.outcome_unknown", "Legacy reward eligibility, cooldowns, total limits and random draws require explicit RewardOutcomes. No badge or product is assumed to be granted.", node.FurnitureId);
            effects.Add(new(now, "RewardRequested", node.FurnitureId, people.ToArray()));
            return;
        }
        for (int index = 0; index < people.Length; index++)
        {
            var outcome = outcomes[reward_outcome_index + index];
            if (outcome.ActionId != node.FurnitureId || outcome.UserId != people[index] ||
                outcome.RewardIndex is { } choice && (choice < 0 || choice >= rewards.Count ||
                    rewards[choice].Type is not (0 or 1) || !node.Flag("unique_rewards") && rewards[choice].Probability <= 0))
                unsupported(node, "The next reward outcome must match this action, recipient and a configured reward entry.");
        }
        foreach (long id in people)
        {
            var outcome = outcomes[reward_outcome_index++];
            if (outcome.RewardIndex is not { } choice)
            {
                effects.Add(new(now, "RewardNotGranted", node.FurnitureId, [id]));
                continue;
            }
            var reward = rewards[choice];
            effects.Add(new(now, reward.Type == 0 ? "RewardBadgeGranted" : "RewardProductGranted", node.FurnitureId, [id], Text: reward.Code));
        }
        note(WiredSimulationSeverity.Information, "reward.observed_outcome", "Replayed explicit reward outcomes. Owned badges, worn badges, inventory delivery and server eligibility are separate state and are not invented.", node.FurnitureId);
    }

    private void validate_progress()
    {
        foreach (var user in users.Values)
        {
            if (user.Achievements is { } achievements &&
                (achievements.Any(item => item is null || string.IsNullOrWhiteSpace(item.Name) || item.Value is < 0 or > int.MaxValue) ||
                 achievements.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != achievements.Count))
                fail("progress.achievements", "Achievement observations require distinct names and nonnegative signed 32-bit progress.", user.Id);
            if (user.RewardTracks is not { } tracks) continue;
            if (tracks.Any(track => track is null || string.IsNullOrWhiteSpace(track.Id) || track.Tasks is null) ||
                tracks.Select(track => track.Id).Distinct(StringComparer.Ordinal).Count() != tracks.Count)
            {
                fail("progress.tracks", "Reward track observations require distinct IDs and explicit task lists.", user.Id);
                continue;
            }
            foreach (var track in tracks)
                if (track.Tasks.Any(task => task is null || string.IsNullOrWhiteSpace(task.Id) ||
                    task.Value is < 0 or > int.MaxValue || task.ResetValue is < 0 or > int.MaxValue) ||
                    track.Tasks.Select(task => task.Id).Distinct(StringComparer.Ordinal).Count() != track.Tasks.Count)
                    fail("progress.tasks", "Track task observations require distinct IDs and nonnegative signed 32-bit progress.", user.Id);
        }
    }

    private void progress_users(WiredSimulationNode node, Frame frame, long[] people)
    {
        if (people.Length == 0) return;
        if (people.Any(id => users[id].IsBot || users[id].IsPet))
            unsupported(node, "Achievement and reward track progression for bots or pets requires separate conformance.");
        long? score = null;
        if (node.Name != "ResetRewardTrack")
        {
            if (node.Number("score_kind") is not (0 or 1)) unsupported(node, "Unknown progress operand kind.");
            score = operand(node, frame, "score_kind", "score", "score_target", "score_variable");
            if (score is null) return;
            if (score is < 0 or > int.MaxValue) unsupported(node, "Progress operands outside the client signed 32-bit range require separate conformance.");
        }
        var updates = new List<WiredSimulationUser>();
        foreach (long id in people)
        {
            var user = users[id];
            if (node.Name == "ProgressAchievement")
            {
                string name = node.Text("achievement_name");
                var previous = user.Achievements?.SingleOrDefault(item => item.Name == name);
                if (previous is null) unsupported(node, "Achievement progress requires an explicit prior observation for this user and achievement.");
                string code = previous!.Code ?? previous.Name;
                if (code.StartsWith("ACH_WF_", StringComparison.Ordinal)) code = code[7..];
                bool enabled = nodes.Values.Where(item => item.Name == "AchievementEnabler")
                    .SelectMany(item => item.Text("achievements").Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    .Contains(code, StringComparer.Ordinal);
                if (!enabled) unsupported(node, "The achievement requires a matching captured AchievementEnabler entry.");
                long mode = node.Number("progression_mode");
                if (mode is not (0 or 1)) unsupported(node, "Unknown achievement progression mode.");
                long value = progress_value(node, previous.Value, score!.Value, mode == 1);
                updates.Add(user with { Achievements = user.Achievements!.Select(item => item.Name == name ? item with { Value = value } : item).ToArray() });
            }
            else
            {
                string track_id = node.Text("track_id");
                var track = user.RewardTracks?.SingleOrDefault(item => item.Id == track_id);
                if (track is null) unsupported(node, "Reward track progression requires an explicit prior track observation for this user.");
                WiredSimulationTrackTask[] tasks;
                if (node.Name == "ResetRewardTrack")
                {
                    if (track!.Tasks.Any(task => task.ResetValue is null))
                        unsupported(node, "Resetting a reward track requires an explicit reset value for every observed task.");
                    tasks = track.Tasks.Select(task => task with { Value = task.ResetValue!.Value }).ToArray();
                }
                else
                {
                    string task_id = node.Text("task_id");
                    var task = track!.Tasks.SingleOrDefault(item => item.Id == task_id);
                    if (task is null) unsupported(node, "Reward track progression requires the configured task in the captured track.");
                    long value = progress_value(node, task!.Value, score!.Value, node.Flag("add_to_existing_score"));
                    tasks = track.Tasks.Select(item => item.Id == task_id ? item with { Value = value } : item).ToArray();
                }
                updates.Add(user with { RewardTracks = user.RewardTracks!.Select(item => item.Id == track_id ? item with { Tasks = tasks } : item).ToArray() });
            }
        }
        note(WiredSimulationSeverity.Uncertain, "progress.external_projection", "Numeric progress is projected from the client contract and explicit prior state. Server eligibility, awards, completion and generated notifications are not inferred.", node.FurnitureId);
        foreach (var user in updates)
        {
            users[user.Id] = user;
            long? value = node.Name switch
            {
                "ProgressAchievement" => user.Achievements!.Single(item => item.Name == node.Text("achievement_name")).Value,
                "ProgressRewardTrack" => user.RewardTracks!.Single(item => item.Id == node.Text("track_id")).Tasks.Single(item => item.Id == node.Text("task_id")).Value,
                _ => null
            };
            string name = node.Name == "ProgressAchievement" ? node.Text("achievement_name") : node.Text("track_id");
            effects.Add(new(now, node.Name, node.FurnitureId, [user.Id], Text: name, Value: value));
        }
    }

    private long progress_value(WiredSimulationNode node, long previous, long score, bool add)
    {
        long value = add ? previous + score : score;
        if (value > int.MaxValue)
            unsupported(node, "Progress beyond the client signed 32-bit range has no verified overflow or saturation rule.");
        return value;
    }
}
