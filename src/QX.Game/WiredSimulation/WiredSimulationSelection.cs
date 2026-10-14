using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private Frame select(WiredSimulationNode node, Frame frame, HashSet<long>? path = null)
    {
        if (node.Name == "RemoteSelector") return remote_selection(node, frame, path ?? []);
        bool user_selection = node.Name.StartsWith("Users", StringComparison.Ordinal);
        IEnumerable<long> result;
        var input = fs(node, frame);
        var objects = input.Select(id => furniture[id]).ToArray();
        var people = us(node, frame).Select(id => users[id]).ToArray();
        var variable_candidates = node.Filter && (node.Name is "FurnitureWithVariable" or "UsersWithVariable")
            ? user_selection ? frame.SelectedUsers : frame.SelectedFurniture : null;
        int candidate_count = variable_candidates?.Length ?? (user_selection ? users.Count : furniture.Count);
        candidates += candidate_count;
        charge(frame, candidate_count, node.FurnitureId);
        switch (node.Name)
        {
            case "PickedFurniture": result = input; break;
            case "FurniFromSignal": result = frame.Event.Furniture ?? []; break;
            case "UsersFromSignal": result = frame.Event.Users ?? []; break;
            case "FurniByType": result = furniture.Values.Where(item => objects.Any(reference => reference.Kind == item.Kind && (!node.Flag("match_state") || reference.State == item.State))).Select(item => item.Id); break;
            case "FurnitureInArea": result = furniture.Values.Where(item => in_area(node, item.X, item.Y)).Select(item => item.Id); break;
            case "UsersInArea": result = users.Values.Where(user => in_area(node, user.X, user.Y)).Select(user => user.Id); break;
            case "FurniWithAltitude": result = furniture.Values.Where(item => compare(WiredSimulationGeometry.height_value(item.Z), node.Number("comparison"), node.Number("altitude_hundredths"))).Select(item => item.Id); break;
            case "UsersByType": result = users.Values.Where(user => user_type(user, node.Number("user_type"))).Select(user => user.Id); break;
            case "UsersInTeam": result = users.Values.Where(user => node.Number("team") == 0 ? user.Team > 0 : user.Team == node.Number("team")).Select(user => user.Id); break;
            case "UsersByName":
                var names = node.Text("usernames").Split(['\t', '\n', '\r', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                result = users.Values.Where(user => names.Contains(user.Name)).Select(user => user.Id); break;
            case "UsersWithHanditem": result = users.Values.Where(user => user.HandItem == node.Number("hand_item")).Select(user => user.Id); break;
            case "UsersInGroup":
                long group = group_id(node);
                result = users.Values.Where(user => user.Groups?.Contains(group) == true).Select(user => user.Id); break;
            case "UsersPerformingAction": result = users.Values.Where(user => activity_matches(node, user)).Select(user => user.Id); break;
            case "UsersOnFurni": result = users.Values.Where(user => objects.Any(item => on_furniture(user, item))).Select(user => user.Id); break;
            case "FurniOnFurni":
                result = furniture.Values.Where(item => objects.Any(reference => item.Id != reference.Id && overlaps(item, reference) &&
                    (node.Number("relation") switch { 0 => item.Z >= reference.Z + reference.Height,
                        1 => reference.Z >= item.Z + item.Height, 2 => Math.Abs(item.Z - reference.Z) < 0.001, 3 => true, _ => false }))).Select(item => item.Id); break;
            case "FurnitureInNeighborhood": case "UsersInNeighborhood":
                int root_x = checked((int)node.Number("root_x")), root_y = checked((int)node.Number("root_y"));
                var centers = node.Flag("reference_is_user") ? people.Select(user => (user.X, user.Y)) : objects.Select(item => (item.X, item.Y));
                var offsets = node.Fields.GetValueOrDefault("tile_mask")?.Tiles ?? [];
                var tiles = centers.SelectMany(center => offsets.Select(offset => (X: center.X + offset.X - root_x, Y: center.Y + offset.Y - root_y))).ToHashSet();
                result = user_selection ? users.Values.Where(user => tiles.Contains((user.X, user.Y))).Select(user => user.Id) : furniture.Values.Where(item => tiles.Contains((item.X, item.Y))).Select(item => item.Id); break;
            case "FurnitureWithVariable": case "UsersWithVariable":
                var domain = user_selection ? WiredSourceDomain.User : WiredSourceDomain.Furniture;
                var threshold = node.Number("value_filter") == 2 ? operand(node, frame, "value_filter", "reference_value", "reference_target", "reference_variable", 0)
                    : unchecked((node.Number("reference_high") << 32) | (uint)node.Number("reference_value"));
                result = (variable_candidates ?? (user_selection ? users.Keys.AsEnumerable() : furniture.Keys).ToArray()).Where(id => value(domain, id, node.Text("selected_variable"), frame, require_exact: node.Number("value_filter") != 0) is { } current &&
                    (node.Number("value_filter") == 0 || threshold.HasValue && compare(current.Value, node.Number("comparison"), threshold.Value))); break;
            default: unsupported(node); return frame;
        }
        var universe = user_selection ? users.Keys.Where(id => id != frame.DepartedUser?.Id).ToHashSet() : furniture.Keys.ToHashSet();
        var selected = result.Where(universe.Contains).ToHashSet();
        if (node.Invert) selected = universe.Except(selected).ToHashSet();
        var previous = user_selection ? frame.SelectedUsers : frame.SelectedFurniture;
        selected = node.Filter ? previous.Intersect(selected).ToHashSet() : previous.Union(selected).ToHashSet();
        selected.IntersectWith(universe);
        record("selector", node.FurnitureId, node.Name, selected.Order().ToArray());
        return user_selection ? frame with { SelectedUsers = selected.Order().ToArray() } : frame with { SelectedFurniture = selected.Order().ToArray() };
    }

    private Frame filter(IReadOnlyList<WiredSimulationNode> boxes, Frame frame)
    {
        foreach (var node in boxes.Where(node => node.Category == WiredFormCategory.Addon && node.Name is "FurniSelectorFilter" or "UserSelectorFilter" or "FurniVariableFilter" or "UserVariableFilter"))
        {
            bool people = node.Name.StartsWith("User", StringComparison.Ordinal);
            long[] selected = people ? frame.SelectedUsers : frame.SelectedFurniture;
            var amount = operand(node, frame, "operand_kind", "filter_amount", "reference_target", "reference_variable", 0);
            if (!amount.HasValue) continue;
            if (node.Name.Contains("Variable", StringComparison.Ordinal))
            {
                var domain = people ? WiredSourceDomain.User : WiredSourceDomain.Furniture;
                var order = node.Number("sort_order");
                Func<long, WiredSimulationValue?> read = id => value(domain, id, node.Text("sort_variable"), frame,
                    require_exact: order is not (2 or 3 or 4 or 5));
                long key(long id) => read(id) is { } current ? order switch
                {
                    2 or 3 => known_timestamp(current, true, node),
                    4 or 5 => known_timestamp(current, false, node),
                    _ => current.Value
                } : long.MinValue;
                selected = (order is 0 or 3 or 5 ? selected.OrderByDescending(key) : selected.OrderBy(key)).ToArray();
            }
            else selected = selected.OrderBy(_ => next_random()).ToArray();
            selected = selected.Take((int)Math.Clamp(amount.Value, 0, selected.Length)).ToArray();
            frame = people ? frame with { SelectedUsers = selected } : frame with { SelectedFurniture = selected };
        }
        return frame;
    }

    private static bool in_area(WiredSimulationNode node, int x, int y) => x >= node.Number("x") && y >= node.Number("y") && x < node.Number("x") + node.Number("width") && y < node.Number("y") + node.Number("height");
    private static bool overlaps(WiredSimulationFurniture first, WiredSimulationFurniture second) =>
        Enumerable.Range(first.X, first.Direction % 4 == 2 ? first.Length : first.Width).Any(x => Enumerable.Range(first.Y, first.Direction % 4 == 2 ? first.Width : first.Length).Any(y => covers(second, x, y)));

    private bool condition(WiredSimulationNode node, Frame frame)
    {
        charge(frame, 1, node.FurnitureId);
        var objects = fs(node, frame).Select(id => furniture[id]).ToArray();
        var people = us(node, frame).Select(id => users[id]).ToArray();
        bool result;
        bool quantify(IEnumerable<bool> values) => node.Quantifier switch { 0 => values.All(value => value), 1 => values.Any(value => value), 2 => !values.All(value => value), 3 => !values.Any(value => value), _ => false };
        switch (node.Name)
        {
            case "CanPerformMove": result = can_perform_move(node, frame); break;
            case "VariableAge": result = quantify(targets(node, frame, "target").Select(target => variable_age(node, frame, target.Domain, target.Id))); break;
            case "TriggererIsOnFurni": result = people.Any(user => objects.Any(item => on_furniture(user, item))); break;
            case "FurnisHaveAvatars": case "NotFurnisHaveAvatars":
                var occupied = objects.Select(item => users.Values.Any(user => on_furniture(user, item))).ToArray();
                result = node.Name == "FurnisHaveAvatars" ? node.Flag("require_all") ? occupied.All(value => value) : occupied.Any(value => value)
                    : node.Flag("require_any_without_avatar") ? occupied.Any(value => !value) : occupied.All(value => !value); break;
            case "UserCountIn":
                int population = users.Values.Count(user => !user.IsBot && !user.IsPet && user.Id != frame.DepartedUser?.Id);
                result = population >= node.Number("minimum_users") && population <= node.Number("maximum_users");
                break;
            case "ActorIsInTeam": result = quantify(people.Select(user => node.Number("team") == 0 ? user.Team != 0 : user.Team == node.Number("team"))); break;
            case "ActorIsGroupMember":
                long group = group_id(node);
                result = quantify(people.Select(user => user.Groups?.Contains(group) == true)); break;
            case "ActorIsWearingBadge": result = quantify(people.Select(user => user.Badges?.Contains(node.Text("badge_code")) == true)); break;
            case "ActorIsWearingEffect": result = quantify(people.Select(user => user.Effect == node.Number("effect_id"))); break;
            case "ActorHasHanditem": result = quantify(people.Select(user => user.HandItem == node.Number("hand_item"))); break;
            case "UserLevel": result = quantify(people.Select(user => compare(user.Level, node.Number("comparison"), node.Number("level")))); break;
            case "UserDirection": result = quantify(people.Select(user => (node.Number("direction_mask") & (1L << user.Direction)) != 0)); break;
            case "PerformingAction": result = quantify(people.Select(user => activity_matches(node, user))); break;
            case "TriggererMatches": result = quantify(people.Select(user => user_type(user, node.Number("user_type")) &&
                (node.Text("name").Length == 0 || user.Name.Equals(node.Text("name"), StringComparison.OrdinalIgnoreCase)))); break;
            case "FurniHasAltitude": result = quantify(objects.Select(item => compare(WiredSimulationGeometry.height_value(item.Z), node.Number("comparison"), node.Number("altitude_hundredths")))); break;
            case "InputSourceQuantity": result = compare(node.Flag("reference_is_user") ? people.Length : objects.Length, node.Number("comparison"), node.Number("amount")); break;
            case "StuffTypeMatches": result = objects.Length > 0 && objects.Select(item => item.Kind).Distinct().Count() == 1; break;
            case "HasStackedFurnis": case "NotHasStackedFurnis":
                var stacked = objects.Select(item => furniture.Values.Any(other => other.Id != item.Id && overlaps(item, other) && other.Z >= item.Z + item.Height)).ToArray();
                result = node.Name == "NotHasStackedFurnis"
                    ? node.Flag("require_all") ? stacked.All(value => !value) : stacked.Any(value => !value)
                    : node.Flag("require_all") ? stacked.All(value => value) : stacked.Any(value => value); break;
            case "StatesMatch":
                var saved_rows = WiredSimulationSnapshots.read(node);
                if (saved_rows is null) { unsupported(node, "Snapshot comparison requires the captured furniture states, not only their IDs."); return false; }
                result = quantify(objects.Select(item => saved_rows.FirstOrDefault(saved => saved.Id == item.Id) is { } saved &&
                    (!node.Flag("match_state") || saved.State is null || item.State == saved.State) && (!node.Flag("match_direction") || saved.Direction is null || item.Direction == saved.Direction) &&
                    (!node.Flag("match_position") || saved.X is null || item.X == saved.X && item.Y == saved.Y) && (!node.Flag("match_altitude") || saved.Z is null || item.Z == saved.Z))); break;
            case "ChestHasItems":
                var chest_amount = operand(node, frame, "operand_kind", "amount", "reference_target", "reference_variable");
                result = chest_amount.HasValue && quantify(objects.Select(item => compare(chest_count(item, node), node.Number("comparison"), chest_amount.Value)));
                break;
            case "ChestHasItemTypes":
                if (node.Quantifier is 2 or 3)
                    unsupported(node, "The negative quantifiers for filtered chest contents have unresolved live behavior.");
                var type_amount = operand(node, frame, "operand_kind", "amount", "reference_target", "reference_variable", 2);
                var selected_chests = fs(node, frame, 1).Select(id => furniture[id]).ToArray();
                result = type_amount.HasValue && quantify(objects.Select(item => compare(chest_type_count(item, selected_chests, node), node.Number("comparison"), type_amount.Value)));
                break;
            case "HasVariable": result = quantify(targets(node, frame, "target").Select(target => value(target.Domain, target.Id, node.Text("variable"), frame, require_exact: false) is not null)); break;
            case "VariableValue":
                var threshold = node.Flag("reference_same_holder") ? null : operand(node, frame, "operand_kind", "reference_value", "reference_target", "reference_variable");
                if (!node.Flag("reference_same_holder") && !threshold.HasValue) { result = false; break; }
                result = quantify(targets(node, frame, "target").Select(target =>
                {
                    var paired_threshold = node.Flag("reference_same_holder") ? operand(node, frame, "operand_kind", "reference_value", "reference_target", "reference_variable", holder: target) : threshold;
                    return paired_threshold.HasValue && value(target.Domain, target.Id, node.Text("variable"), frame) is { } current && compare(current.Value, node.Number("comparison"), paired_threshold.Value);
                })); break;
            case "TimeElapsedMore": result = timer_condition(node, true); break;
            case "TimeElapsedLess": result = timer_condition(node, false); break;
            case "ClockTimeMatches": result = quantify(objects.Select(item => compare(clock_state(item, node).ElapsedMilliseconds, node.Number("comparison"), clock_threshold(node)))); break;
            case "TimeMatches": case "DateMatches": result = calendar_matches(node); break;
            case "DateRangeActive":
                if (!node.Fields.ContainsKey("start_timestamp") || !node.Fields.ContainsKey("end_timestamp"))
                    unsupported(node, "Open date ranges require explicit bound-presence metadata.");
                if (initial.Epoch is null) unsupported(node, "Date comparisons require an explicit snapshot epoch.");
                long seconds = (initial.Epoch ?? DateTimeOffset.UnixEpoch).AddMilliseconds(now).ToUnixTimeSeconds();
                result = seconds >= node.Number("start_timestamp") && seconds <= node.Number("end_timestamp"); break;
            case "TeamHasScore":
                int team = (int)node.Number("team");
                if (team == 0) team = frame.Event.UserId is { } id && users.TryGetValue(id, out var actor) ? actor.Team : 0;
                result = team != 0 && compare(team_scores.GetValueOrDefault(team), node.Number("comparison"), node.Number("score")); break;
            case "TeamIsWinning":
                result = team_placement(node, frame);
                break;
            default: unsupported(node); return false;
        }
        if (node.Code < 0) result = !result;
        record("condition", node.FurnitureId, result ? "true" : "false");
        return result;
    }

    private static bool user_type(WiredSimulationUser user, long type) => type switch
    { 1 => !user.IsBot && !user.IsPet, 2 => user.IsPet, 4 => user.IsBot, _ => false };

    private long group_id(WiredSimulationNode node)
    {
        string text = node.Text("group_id");
        if (text.Length == 0)
        {
            if (initial.GroupId is not { } group) unsupported(node, "Current-room group selection requires GroupId in the snapshot.");
            return initial.GroupId.GetValueOrDefault();
        }
        if (long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out long id) && id > 0) return id;
        unsupported(node, "Group ID must be a positive numeric identifier.");
        return 0;
    }
}
