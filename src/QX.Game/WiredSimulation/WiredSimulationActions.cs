using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void action_run(WiredSimulationNode node, Frame frame)
    {
        if (!furniture.ContainsKey(node.FurnitureId)) return;
        frame = frame with { Action = node.FurnitureId };
        if (node.Name == "RemoveFurni" && furniture_source(node) is not
            (WiredFurnitureSource.Triggering or WiredFurnitureSource.Selector or WiredFurnitureSource.Signal))
            unsupported(node, "Remove furniture accepts triggering furniture, selector output or signal payload; saved picked targets are not supported.");
        charge(frame, 1, node.FurnitureId); actions++;
        long[] objects = fs(node, frame), people = us(node, frame);
        if (node.Name is "BotTeleport" or "BotMove" or "BotChangeFigure")
            people = has_user_source(node)
                ? people.Where(id => users[id].IsBot).ToArray()
                : users.Values.Where(user => user.IsBot && user.Name.Equals(node.Text("bot_name"), StringComparison.OrdinalIgnoreCase)).Select(user => user.Id).ToArray();
        if (node.Name == "JoinTeam" && node.Number("game_type") != 0) unsupported(node, "Non-Wired team games are not modeled.");
        if (node.Name == "GiveEffect" && (node.Number("priority") != 0 || node.Number("effect_type") != 0))
            unsupported(node, "Effect priority and alternate effect types are not modeled.");
        record("action", node.FurnitureId, node.Name, objects);
        switch (node.Name)
        {
            case "MoveFurniTo":
                if (node.Number("movement") != 0)
                    unsupported(node, "The nonzero legacy movement modes have no verified destination-search mapping. Mode zero is the client No movement option.");
                break;
            case "GiveReward": give_legacy_reward(node, people); break;
            case "ProgressAchievement": case "ProgressRewardTrack": case "ResetRewardTrack":
                progress_users(node, frame, people); break;
            case "SendLinkMessage": case "SendLinkDataframe": send_link(node, frame); break;
            case "SetFurniTransparency": case "SetFurniHighlight": visual_action(node, frame); break;
            case "GiveVariable":
                foreach (var target in targets(node, frame, "target"))
                    if (node.Flag("override_existing") || value(target.Domain, target.Id, node.Text("variable"), frame, require_exact: false) is null)
                    {
                        var initial_value = variables.TryGetValue(resolve_reference(node.Text("variable"), frame), out var definition) && !definition.HasValue
                            ? -1 : operand(node, frame, "initial_value_kind", "initial_value", "initial_value_target", "initial_variable", holder: target);
                        if (initial_value is { } initial_number)
                            write(target.Domain, target.Id, node.Text("variable"), initial_number, frame, create: true, replace: node.Flag("override_existing"));
                    }
                break;
            case "RemoveVariable":
                foreach (var target in targets(node, frame, "target")) write(target.Domain, target.Id, node.Text("variable"), 0, frame, remove: true);
                break;
            case "GiveFurniFromChest": reward_furniture(node, frame, objects, people); break;
            case "GiveCurrencyFromChest": reward_currency(node, frame, objects, people); break;
            case "InitiateTransaction": initiate_transaction(node, frame, objects, people); break;
            case "CancelTransaction": cancel_transaction(node, frame, people); break;
            case "TeleportToRoom": transfer_room(node, frame, objects, people); break;
            case "ChangeVariable":
                long? right = node.Flag("reference_same_holder") ? null : operand(node, frame, "operand_kind", "operand_value", "operand_target", "operand");
                foreach (var target in targets(node, frame, "destination_target"))
                {
                    var paired_right = node.Flag("reference_same_holder") ? operand(node, frame, "operand_kind", "operand_value", "operand_target", "operand", holder: target) : right;
                    if (!paired_right.HasValue) continue;
                    if (value(target.Domain, target.Id, node.Text("destination"), frame) is { } previous)
                        write(target.Domain, target.Id, node.Text("destination"), arithmetic(previous.Value, node.Number("operation"), paired_right.Value, node), frame);
                    else record("variable.absent", node.FurnitureId, "Change did not create the absent variable " + node.Text("destination"), [target.Id]);
                }
                break;
            case "SendSignal": signal(node, frame); break;
            case "Chase": pursue(node, frame, false); break;
            case "Flee": pursue(node, frame, true); break;
            case "MoveToDirection": move_direction(node, frame); break;
            case "ControlClock": control_clock(node, frame); break;
            case "AdjustClock": adjust_clock(node, frame); break;
            case "Reset": reset_timers(node); break;
            case "CallAnotherStack":
                foreach (var target in stack_targets(node, frame).Reverse()) stack(target, frame, called: true);
                break;
            case "ToggleFurniState":
                foreach (long id in objects)
                {
                    if (furniture[id].ClassName == "wf_floor_switch1")
                    {
                        record("state.ignored", node.FurnitureId, "Floor Switch 1 ignores the Wired state-toggle effect.", [id]);
                        continue;
                    }
                    set_state(furniture[id], next_state(furniture[id], node.Number("toggle_type") == 1 ? -1 : 1), frame);
                }
                break;
            case "ToggleToRandomState":
                foreach (long id in objects)
                {
                    var item = furniture[id];
                    if (item.StateCount is not > 0) { unsupported(node, "Random state selection requires StateCount metadata."); return; }
                    set_state(item, (int)(next_random() % item.StateCount.Value), frame);
                }
                break;
            case "MatchSnapshot":
                var saved_rows = WiredSimulationSnapshots.read(node);
                if (saved_rows is null) { unsupported(node, "MatchSnapshot requires captured furniture values."); return; }
                foreach (long id in objects)
                {
                    if (saved_rows.FirstOrDefault(item => item.Id == id) is not { } saved) continue;
                    var item = furniture[id];
                    bool position = node.Flag("position") && saved.X.HasValue && saved.Y.HasValue;
                    bool altitude = node.Flag("altitude") && saved.Z.HasValue;
                    if (position || altitude)
                        move_item(item, position ? saved.X!.Value : item.X, position ? saved.Y!.Value : item.Y, altitude ? saved.Z!.Value : null, frame);
                    if (node.Flag("state") && saved.State is { } state) set_state(furniture[id], state, frame);
                    if (node.Flag("direction") && saved.Direction is { } direction) rotate_item(furniture[id], direction, frame);
                }
                break;
            case "SetFurniAltitude":
                foreach (long id in objects)
                {
                    var item = furniture[id];
                    double height = node.Number("altitude_hundredths") / 100.0;
                    double destination = node.Number("operation") switch { 0 => item.Z + height, 1 => item.Z - height, 2 => height, _ => double.NaN };
                    if (!double.IsFinite(destination)) unsupported(node, "Unknown altitude operation.");
                    move_item(item, item.X, item.Y, destination, frame, vertical_movement: true);
                }
                break;
            case "RelativeFurniMove":
                foreach (long id in objects)
                { var item = furniture[id]; move_item(item, checked(item.X + (int)node.Number("x_offset")), checked(item.Y + (int)node.Number("y_offset")), null, frame, relative: true); }
                break;
            case "MoveFurniToFurni": transfer_furniture(node, frame, objects); break;
            case "MoveFurniToUser":
                var target_people = us(node, frame, 1);
                foreach (long id in objects)
                {
                    if (target_people.Length > 0)
                    { var target = users[choose(target_people)]; move_item(furniture[id], target.X, target.Y, null, frame); }
                }
                break;
            case "MoveFurni":
                foreach (long id in objects)
                {
                    var item = furniture[id]; var offset = movement((int)node.Number("movement"), node);
                    if (offset != (0, 0)) move_item(item, item.X + offset.X, item.Y + offset.Y, null, frame);
                    int rotation = (int)node.Number("rotation");
                    if (rotation != 0) rotate_item(furniture[id], (item.Direction + (rotation == 1 ? 2 : rotation == 2 ? 6 : (int)(next_random() % 4) * 2)) % 8, frame);
                }
                break;
            case "TeleportToFurniture": case "MoveUserToFurni":
                transfer_users(node, frame, objects, people);
                break;
            case "BotTeleport": case "BotMove":
                move_bots(node, frame, objects, people);
                break;
            case "BotFollowAvatar":
                follow_bots(node, frame);
                break;
            case "MoveUser":
                move_users(node, frame, people);
                break;
            case "MoveAsGroup": move_group(node, frame, objects); break;
            case "FreezeUser": freeze_users(node, people); break;
            case "UnfreezeUser": release_users(node, people); break;
            case "ClickConfiguration":
                int user_click = (int)node.Number("user_click"), furniture_click = (int)node.Number("furniture_click");
                if (user_click is < 0 or > 2 || furniture_click is < 0 or > 1)
                    unsupported(node, "Unknown user or furniture click option.");
                if (people.Any(id => users[id].IsBot || users[id].IsPet))
                    unsupported(node, "Click configuration for bots and pets requires separate conformance.");
                var click_settings = new WiredClickSettings(user_click, furniture_click);
                foreach (long id in people) users[id] = users[id] with { ClickSettings = click_settings };
                if (people.Length > 0) effects.Add(new(now, "click_settings", node.FurnitureId, people, ClickSettings: click_settings));
                break;
            case "JoinTeam": foreach (long id in people) users[id] = users[id] with { Team = (int)node.Number("team") }; break;
            case "LeaveTeam": leave_team(node, people); break;
            case "GiveScore": give_score(node, frame, people); break;
            case "GiveScoreToPredefinedTeam": give_team_score(node, frame); break;
            case "GiveEffect":
                foreach (long id in people)
                {
                    if (pending_teleport(id)) unsupported(node, "Effect priority while a classic teleport is pending requires separate conformance.");
                    if (users[id].Frozen) unsupported(node, "Effect priority while frozen requires separate conformance.");
                    users[id] = users[id] with { Effect = (int)node.Number("effect_id") };
                }
                break;
            case "BotGiveHandItem": give_hand_item(node, frame); break;
            case "BotChangeFigure": foreach (long id in people) users[id] = users[id] with { Figure = node.Text("figure") }; break;
            case "PlaceFurni": place(node, frame); break;
            case "RemoveFurni":
            {
                var removed = objects.Where(id => furniture[id].Temporary).ToHashSet();
                if (removed.Count == 0) break;
                if (removed.Any(id => furniture[id].Projectile is not null))
                    unsupported(node, "Removing a projectile holder and its intrinsic variables requires conformance.");
                var occupants = users.Values.Where(user => floor_contacts(user).Any(removed.Contains)).ToArray();
                foreach (long id in removed)
                {
                    furniture.Remove(id);
                    random_histories.Remove(id);
                    limited.Remove(id);
                    foreach (var source in nodes.Values.Where(source => source.MovementDirections?.ContainsKey(id) == true).ToArray())
                        nodes[source.FurnitureId] = source with { MovementDirections = source.MovementDirections!.Where(pair => pair.Key != id).ToDictionary() };
                }
                foreach (var occupant in occupants)
                {
                    users[occupant.Id] = located(occupant, occupant.X, occupant.Y, walkable_only: true);
                    record("user.support_removed", node.FurnitureId, "Temporary support removed without horizontal movement.", [occupant.Id]);
                }
                break;
            }
            case "BotTalk": case "BotTalkDirectToAvtr": bot_speech(node, frame); break;
            case "Chat": case "Log":
                effects.Add(new(now, node.Name, node.FurnitureId, people, output_text(node, frame))); break;
            case "KickFromRoom":
                foreach (long id in people)
                {
                    if (id == initial.OwnerId)
                    {
                        record("kick.protected", node.FurnitureId, "The room owner is not removed by the kick effect.", [id]);
                        continue;
                    }
                    effects.Add(new(now, "Kick", node.FurnitureId, [id], node.Text("message")));
                    users.Remove(id);
                }
                break;
            case "MuteUser": effects.Add(new(now, "Mute", node.FurnitureId, people, node.Text("message"), node.Number("minutes"))); break;
            default: unsupported(node); break;
        }
    }

    private void place(WiredSimulationNode node, Frame frame)
    {
        bool captured = furniture_source(node, 0) == WiredFurnitureSource.Snapshot;
        var saved_rows = captured ? WiredSimulationSnapshots.read(node) : null;
        if (captured && saved_rows is null)
            unsupported(node, "Snapshot-source copying requires saved furniture values.");
        if (captured && saved_rows!.Any(item => !furniture.ContainsKey(item.Id)))
            unsupported(node, "Snapshot templates removed from the room require separate copy conformance.");
        long[] sources = fs(node, frame);
        if (sources.Length == 0) return;
        var prototypes = sources.ToDictionary(id => id, id => furniture[id]);
        if (captured)
        {
            foreach (long id in sources)
            {
                var saved = saved_rows!.Single(item => item.Id == id);
                var source = prototypes[id];
                if (saved.X is null || saved.Y is null || saved.Z is null || saved.Direction is null || saved.State is null && source.State >= 0)
                    unsupported(node, "Snapshot-source copying requires saved position, altitude, direction and available state.");
                prototypes[id] = surface(source with { X = saved.X!.Value, Y = saved.Y!.Value, Z = saved.Z!.Value,
                    Direction = saved.Direction!.Value, State = saved.State ?? source.State });
            }
        }
        int origin_x = sources.Min(id => prototypes[id].X), origin_y = sources.Min(id => prototypes[id].Y);
        double origin_z = sources.Min(id => prototypes[id].Z);
        var locations = node.Flag("custom_target_is_user")
            ? us(node, frame, 0).Select(id => (users[id].X, users[id].Y, users[id].Z)).ToArray()
            : fs(node, frame, 1).Select(id => (furniture[id].X, furniture[id].Y, furniture[id].Z)).ToArray();
        int location_mode = checked((int)node.Number("location_mode")), altitude_mode = checked((int)node.Number("altitude_mode"));
        if (location_mode is < 0 or > 1 || altitude_mode is < 0 or > 2)
            unsupported(node, "Unknown temporary furniture location or altitude mode.");
        if (altitude_mode == 2 && locations.Length == 0)
            unsupported(node, "Custom-reference altitude requires an available reference; the missing-reference execution path is not modeled.");
        if (altitude_mode == 2 && location_mode == 0 && locations.Length > 1)
            unsupported(node, "Source-location copying with several custom altitude references requires separate conformance.");
        var group_heights = new Dictionary<(int X, int Y), double>();
        var blocked_groups = new HashSet<(int X, int Y)>();
        bool initialize = node.Flag("spawn_with_variable"), initial_value_read = false;
        long? initial_value = null;
        if (sources.Length > 1 || location_mode == 1 && locations.Length > 1)
        {
            var anchors = location_mode == 0 ? new[] { (X: origin_x, Y: origin_y) } : locations.Select(position => (position.X, position.Y));
            foreach (var anchor in anchors)
            {
                double highest = 0;
                foreach (long id in sources)
                {
                    var item = prototypes[id];
                    int x = checked(anchor.X + (int)node.Number("x_offset") + item.X - origin_x);
                    int y = checked(anchor.Y + (int)node.Number("y_offset") + item.Y - origin_y);
                    if (!fits(item, x, y))
                    {
                        if (sources.Length == 1) note(WiredSimulationSeverity.Warning, "placement.no_floor", "The temporary furniture footprint has no complete floor.", node.FurnitureId);
                        blocked_groups.Add(anchor);
                        record("temporary.blocked", node.FurnitureId, "A missing floor cell prevents placement of the complete group.");
                        break;
                    }
                    double? height = placement_surface(item, x, y, node, altitude_mode == 0);
                    if (height is null)
                    {
                        blocked_groups.Add(anchor);
                        record("temporary.blocked", node.FurnitureId, "A blocked member prevents placement of the complete group.");
                        break;
                    }
                    highest = Math.Max(highest, height.GetValueOrDefault());
                }
                if (!blocked_groups.Contains(anchor)) group_heights[anchor] = highest;
            }
        }
        foreach (long id in sources)
        {
            var source = prototypes[id];
            if (source.RoomLink is not null || source.ClassName == "wf_room_linker")
                unsupported(node, "Temporary copies of paired room linkers require separate link-identity conformance.");
            if (source.Chest is not null || source.ClassName.StartsWith("wf_storage", StringComparison.OrdinalIgnoreCase))
                unsupported(node, "Temporary copies of chests require separate storage and ownership conformance.");
            if (source.Clock is not null || source.Projectile is not null || source.FleeDirection.HasValue || nodes.Values.Any(box => box.MovementDirections?.ContainsKey(id) == true))
                unsupported(node, "Temporary copies of clocks or furniture with retained movement state require separate server conformance.");
            if (!captured && state_changes.TryGetValue(id, out var change) && change.At == now && change.PublicationRoot == publication_root(frame))
            {
                record("temporary.source_state", node.FurnitureId, "Copy uses the source state from before this execution's changes.", [id], source.State, change.PreviousState);
                source = surface(source with { State = change.PreviousState });
            }
            foreach (var position in location_mode == 0 ? new[] { (source.X, source.Y, source.Z) } : locations)
            {
                if (blocked_groups.Contains(location_mode == 0 ? (origin_x, origin_y) : (position.X, position.Y))) continue;
                if (furniture.Count >= options.MaximumFurniture) { fail("simulation.furniture_limit", "Temporary furniture exceeds the simulation limit.", node.FurnitureId); return; }
                int x = checked(position.X + (int)node.Number("x_offset") + (location_mode == 1 ? source.X - origin_x : 0));
                int y = checked(position.Y + (int)node.Number("y_offset") + (location_mode == 1 ? source.Y - origin_y : 0));
                if (!fits(source, x, y)) { note(WiredSimulationSeverity.Warning, "placement.no_floor", "The temporary furniture footprint has no complete floor.", node.FurnitureId); continue; }
                double? support_height = group_heights.TryGetValue(location_mode == 0 ? (origin_x, origin_y) : (position.X, position.Y), out double group_height)
                    ? group_height : placement_surface(source, x, y, node, altitude_mode == 0);
                if (support_height is null)
                {
                    record("temporary.blocked", node.FurnitureId, "A destination cell does not allow stacking.");
                    continue;
                }
                double z = node.Number("altitude_offset") / 100.0 + (altitude_mode switch
                { 0 => support_height.Value + source.Z - origin_z, 1 => source.Z,
                    2 => (location_mode == 0 ? locations[0].Z : position.Z) + source.Z - origin_z, _ => double.NaN });
                if (!double.IsFinite(z)) { unsupported(node, "Unknown temporary furniture altitude mode."); return; }
                if (initialize && !initial_value_read)
                {
                    initial_value = spawn_value(node, frame);
                    initial_value_read = true;
                }
                long created = next_temporary--;
                furniture.Add(created, source with { Id = created, Key = "temporary:" + (-created), X = x, Y = y, Z = z, Temporary = true, IntrinsicType = 2, Variables = new Dictionary<string, WiredSimulationValue>() });
                placements++; charge(frame, 1, node.FurnitureId);
                if (initialize)
                {
                    write(WiredSourceDomain.Furniture, created, node.Text("spawn_variable"), initial_value ?? -1, frame, create: true, notify: false);
                }
                record("temporary.created", node.FurnitureId, source.ClassName, [created]);
            }
        }
    }

    private long? spawn_value(WiredSimulationNode node, Frame frame)
    {
        int slot = node.Number("spawn_value_target") == (int)WiredSourceDomain.Furniture ? 2 : 1;
        if (node.Number("spawn_value_kind") == 0)
            return operand(node, frame, "spawn_value_kind", "spawn_value", "spawn_value_target", "spawn_value_variable", slot);
        var references = targets(node, frame, "spawn_value_target", slot, include_departed: true);
        if (references.Length <= 1)
            return references.Length == 0 ? null : value(references[0].Domain, references[0].Id, node.Text("spawn_value_variable"), frame)?.Value;
        if (references[0].Domain != WiredSourceDomain.Furniture)
            unsupported(node, "Multiple user holders for a temporary initial value require separate conformance.");
        long selected;
        if (options.SpawnValueChoices is { } choices && spawn_value_choice < choices.Count)
        {
            var choice = choices[spawn_value_choice];
            if (choice.ActionId != node.FurnitureId || !references.Any(reference => reference.Domain == choice.Domain && reference.Id == choice.HolderId))
                unsupported(node, "The next spawn-value choice must identify a selected holder for this action and domain.");
            selected = choice.HolderId;
            spawn_value_choice++;
        }
        else
        {
            uncertain("temporary.value_source.choice", "Several initial-value holders are available. Supply SpawnValueChoices for a known branch; a permissive run samples one possible holder without predicting the hotel's random selection.", node.FurnitureId);
            selected = choose(references.Select(reference => reference.Id).ToArray());
        }
        record("temporary.value_source", node.FurnitureId, "Selected initial-value holder.", [selected]);
        return value(references[0].Domain, selected, node.Text("spawn_value_variable"), frame)?.Value;
    }

    private double? placement_surface(WiredSimulationFurniture source, int x, int y, WiredSimulationNode node, bool check_floor)
    {
        var supports = furniture.Values.Where(item => overlaps(source with { X = x, Y = y }, item)).ToArray();
        int width = source.Direction % 4 == 2 ? source.Length : source.Width;
        int length = source.Direction % 4 == 2 ? source.Width : source.Length;
        double highest = 0;
        for (int row = y; row < y + length; row++)
        for (int column = x; column < x + width; column++)
        {
            double floor_height = floor(column, row);
            if (check_floor && Math.Abs(floor_height - floor(x, y)) > 0.000001)
                unsupported(node, "Temporary on-top copying over uneven floor heights requires separate conformance.");
            var cell = supports.Where(item => covers(item, column, row)).ToArray();
            double top = cell.Select(item => item.Z + item.Height).Append(Math.Max(0, floor_height)).Max();
            var highest_items = cell.Where(item => Math.Abs(item.Z + item.Height - top) < 0.001).ToArray();
            bool blocked = highest_items.Any(item => !item.Stackable || item.ClassName == "exe_gate");
            if (blocked && highest_items.Any(item => item.Stackable && item.ClassName != "exe_gate"))
                unsupported(node, "Tied temporary-placement surfaces with different stacking permissions require separate conformance.");
            if (blocked) return null;
            highest = Math.Max(highest, top);
        }
        return highest;
    }

    private int next_state(WiredSimulationFurniture item, int change = 1)
    {
        if (item.StateCount is not > 0) { fail("metadata.state_count", $"State count is unknown for {item.ClassName}; supply furniture metadata.", item.Id); throw new SimulationFailure(); }
        return (item.State + change + item.StateCount.Value) % item.StateCount.Value;
    }
    private long publication_root(Frame frame) => publication_roots.GetValueOrDefault(frame.Root, frame.Root);

    private void set_state(WiredSimulationFurniture item, int state, Frame frame)
    {
        if (item.StateCount is { } count && (state < 0 || state >= count))
        { fail("state.out_of_range", "The requested state is outside the supplied furniture metadata.", item.Id); throw new SimulationFailure(); }
        if (item.States is { } states && !states.ContainsKey(state))
        { fail("metadata.furniture_state", $"No geometry supplied for state {state} of {item.ClassName}.", item.Id); throw new SimulationFailure(); }
        furniture[item.Id] = surface(item with { State = state }); charge(frame, 1, frame.Stack);
        if (item.State != state && (!state_changes.TryGetValue(item.Id, out var change) || change.At != now || change.PublicationRoot != publication_root(frame)))
            state_changes[item.Id] = new(now, publication_root(frame), item.State);
        record("state", frame.Stack, item.Key, [item.Id], item.State, state);
        if (item.State != state) enqueue(now, frame with { Event = new(WiredSimulationEventKind.StateChanged, now, frame.Event.UserId, item.Id, OldValue: item.State, NewValue: state) }, external: true);
    }
    private bool walkable(int x, int y, long user, bool bot_teleport = false) =>
        floor(x, y) >= 0 && !furniture.Values.Any(item => covers(item, x, y) && !item.Walkable)
        && !users.Values.Any(other => other.Id != user && other.X == x && other.Y == y &&
            (!bot_teleport || !bot_teleport_overlap(other)));
    private double standing(int x, int y, bool walkable_only = false) => furniture.Values
        .Where(item => covers(item, x, y) && (!walkable_only || item.Walkable || item.Stance != Qx.Model.AvatarStance.Stand))
        .Select(item => item.Z + item.Height).Append(Math.Max(0, floor(x, y))).Max();
    private WiredSimulationUser located(WiredSimulationUser user, int x, int y, bool walkable_only = false, bool align_posture = false)
    {
        var top = furniture.Values.Where(item => covers(item, x, y) && (!walkable_only || item.Walkable || item.Stance != Qx.Model.AvatarStance.Stand))
            .OrderByDescending(item => item.Z + item.Height).ThenBy(item => item.Id).FirstOrDefault();
        bool seated = top is { Stance: not Qx.Model.AvatarStance.Stand };
        return user with { X = x, Y = y, Z = seated ? top!.Z : standing(x, y, walkable_only),
            Stance = top?.Stance ?? Qx.Model.AvatarStance.Stand, ActionHeight = seated ? top!.Height : null,
            Direction = seated && align_posture ? top!.Direction : user.Direction };
    }
    private static bool on_furniture(WiredSimulationUser user, WiredSimulationFurniture item) =>
        covers(item, user.X, user.Y) && Math.Abs(user.Z + (user.ActionHeight ?? 0) - item.Z - item.Height) < 0.01;
    private void move_user(WiredSimulationUser user, int x, int y, Frame frame, bool align_posture = false, bool changed_contacts_only = false, bool refresh_same_tile = false)
    {
        if (user.X == x && user.Y == y && !refresh_same_tile) return;
        if (floor(x, y) < 0) { note(WiredSimulationSeverity.Warning, "movement.no_floor", "User destination has no floor.", frame.Stack); return; }
        users[user.Id] = located(user, x, y, align_posture: align_posture);
        var previous = wired_contacts(user);
        var leaving = changed_contacts_only ? previous.Except(floor_contacts(users[user.Id])).ToArray() : previous;
        if (!changed_contacts_only || leaving.Length > 0)
            dispatch(frame with { Event = new(WiredSimulationEventKind.Walk, now, user.Id, X: user.X, Y: user.Y, Furniture: leaving, Change: -1) });
        var current = floor_contacts(users[user.Id]);
        remember_contacts(user.Id, current);
        var entering = changed_contacts_only ? current.Except(previous).ToArray() : current;
        if (!changed_contacts_only || entering.Length > 0)
            dispatch(frame with { Event = new(WiredSimulationEventKind.Walk, now, user.Id, X: x, Y: y, Furniture: entering) });
        if (user.IsBot && (!changed_contacts_only || entering.Length > 0))
            dispatch(frame with { Event = new(WiredSimulationEventKind.BotDestinationReached, now, user.Id,
                X: x, Y: y, Furniture: changed_contacts_only ? entering : floor_contacts(users[user.Id])) });
        refresh_followers(user.Id, frame);
    }
    private void rotate_item(WiredSimulationFurniture item, int direction, Frame frame)
    {
        var rotated = item with { Direction = direction };
        if (!fits(rotated, item.X, item.Y))
        { note(WiredSimulationSeverity.Warning, "rotation.no_floor", "The rotated furniture footprint has no complete floor.", frame.Stack); return; }
        furniture[item.Id] = rotated;
    }
    private void move_item(WiredSimulationFurniture item, int x, int y, double? z, Frame frame, bool relative = false, bool transfer = false, bool group_movement = false, int? target_direction = null, bool packed_movement = false, bool vertical_movement = false)
    {
        int count = root_moves.GetValueOrDefault(frame.Root);
        if (count >= options.MaximumMovesPerExecution)
        { fail("profile.movement_limit", "The movement allowance of the selected simulation profile was reached; execution is incomplete.", frame.Stack); throw new SimulationFailure(); }
        var destination = item with { X = x, Y = y, Direction = target_direction ?? item.Direction };
        if (!fits(destination, x, y)) { note(WiredSimulationSeverity.Warning, "movement.no_floor", "The destination furniture footprint has no complete floor.", frame.Stack); return; }
        if (transfer && (item.Width > 1 || item.Length > 1))
        {
            int width = item.Direction % 4 == 2 ? item.Length : item.Width;
            int length = item.Direction % 4 == 2 ? item.Width : item.Length;
            for (int row = y; row < y + length; row++)
            for (int column = x; column < x + width; column++)
                if (Math.Abs(floor(column, row) - floor(x, y)) > 0.000001)
                    unsupported(nodes[frame.Stack!.Value], "Larger furniture transfers over uneven floor heights require separate conformance.");
        }
        var addons = frame.Stack is { } stack && nodes.TryGetValue(stack, out var root) ? siblings(root) : [];
        bool curved = addons.Any(node => node.Name == "JumpStrength") && !addons.Any(node => node.Name == "NoMoveAnimation");
        int? curve = movement_curve(addons, frame);
        if (curved && (group_movement || !packed_movement && (item.Width != 1 || item.Length != 1)))
            unsupported(addons.First(node => node.Name == "JumpStrength"), "Grouped or multi-tile movement curves require separate conformance.");
        var physics = addons.SingleOrDefault(node => node.Name == "MovementPhysics");
        var carry = addons.SingleOrDefault(node => node.Name == "CarryUsers");
        bool stationary = packed_movement && item.X == x && item.Y == y && item.Direction == destination.Direction;
        if (stationary && (physics?.Flag("keep_altitude") == true || furniture.Values
            .Where(other => other.Id != item.Id && overlaps(destination, other))
            .All(other => other.Z + other.Height <= item.Z + item.Height + 0.0001))) return;
        if (relative && carry is not null && (item.X != x || item.Y != y) && users.Values.Any(user => user.IsBot && (covers(item, user.X, user.Y) || covers(destination, user.X, user.Y)) &&
            !movable_bot(user, item, destination, addons)))
            unsupported(nodes[frame.Stack!.Value], "Relative furniture movement with an unsupported bot requires separate conformance.");
        var affected = users.Values.Where(user => covers(item, user.X, user.Y) || covers(destination, user.X, user.Y)).ToArray();
        bool shared_support = shared_carrier(item, destination, affected, addons);
        long? support_actor = shared_support ? affected.MinBy(user => user.Index)!.Id : null;
        if (relative && affected.Length > 1 && !shared_support)
            unsupported(nodes[frame.Stack!.Value], "Relative furniture movement with multiple occupants outside the verified shared carrier requires separate conformance.");
        if (transfer || packed_movement || vertical_movement)
        {
            if (affected.Length > 1 && !shared_support || affected.Any(user => user.IsPet || user.NextStep is not null || user.Stance == Qx.Model.AvatarStance.Lay ||
                user.IsBot && !movable_bot(user, item, destination, addons, stationary || vertical_movement, allow_curve: vertical_movement)))
                unsupported(nodes[frame.Stack!.Value], "Furniture movement with multiple occupants, pets, walking or lying users, or an unsupported bot transfer requires separate conformance.");
            if (carry is not null && carry.Number("carry_mode") is not (0 or 1))
                unsupported(carry, "Unknown carry mode.");
            if (carry is not null && affected.Any(user => !covers(item, user.X, user.Y)))
                unsupported(carry, "Furniture movement with a carry modifier and a stationary destination occupant requires separate conformance.");
            if (affected.Any(user => covers(item, user.X, user.Y) || physics is not null) && addons.Any(node => node.Name == "Projectile"))
                unsupported(nodes[frame.Stack!.Value], "Furniture projectile transfers involving occupants require separate conformance.");
        }
        bool track_support = relative || transfer || packed_movement || vertical_movement;
        var riders = vertical_movement ? users.Values.Where(user => on_furniture(user, item) ||
            carry?.Number("carry_mode") == 1 && covers(item, user.X, user.Y)).ToArray()
            : carry is null ? [] : users.Values.Where(user => carry.Number("carry_mode") == 1
            ? covers(item, user.X, user.Y) : on_furniture(user, item)).ToArray();
        int? carried_curve = vertical_movement && riders.Length > 0 && addons.Any(node => node.Name == "NoMoveAnimation")
            ? movement_curve(addons, frame, ignore_cancellation: true) : curve;
        var left_behind = track_support ? users.Values.Where(user => covers(item, user.X, user.Y) && !riders.Any(rider => rider.Id == user.Id)).ToArray() : [];
        bool horizontal = item.X != x || item.Y != y;
        if (horizontal && !relative && !transfer && !group_movement && !packed_movement && addons.Any(node => node.Name == "NoMoveAnimation"))
            unsupported(addons.First(node => node.Name == "NoMoveAnimation"), "Canceling this movement family's animation requires separate conformance.");
        if ((relative || transfer || packed_movement) && relative_stack_blocked(item with { Direction = destination.Direction }, x, y, physics))
        { record("move.blocked", frame.Stack, "Furniture movement requires a stackable destination or MoveThroughFurni.", [item.Id]); return; }
        if ((relative || transfer || packed_movement) && horizontal && physics is null && users.Values.Any(user => covers(destination, user.X, user.Y) && !riders.Any(rider => rider.Id == user.Id)))
        { record("move.blocked", frame.Stack, "The destination is occupied by a user; this movement requires MoveThroughUsers.", [item.Id]); return; }
        if (physics is not null && horizontal)
        {
            if (physics.Flag("block_by_furni") && furniture.Values.Any(other => other.Id != item.Id && overlaps(destination, other)))
            { record("move.blocked", frame.Stack, "Movement physics blocked the occupied furniture destination.", [item.Id]); return; }
            if (!physics.Flag("move_through_users") && users.Values.Any(user => covers(destination, user.X, user.Y) && !riders.Any(rider => rider.Id == user.Id)))
            { record("move.blocked", frame.Stack, "Movement physics blocked an occupied user destination.", [item.Id]); return; }
            if (!relative && !transfer && !packed_movement && (Math.Abs(x - item.X) > 1 || Math.Abs(y - item.Y) > 1))
                unsupported(physics, "Movement physics for multi-tile paths requires verified swept-collision rules.");
        }
        double altitude = vertical_movement ? Math.Max(0, z ?? item.Z) : z ?? item.Z;
        if (z is null && physics?.Flag("keep_altitude") != true)
        {
            double support = furniture.Values.Where(other => other.Id != item.Id && overlaps(destination, other))
                .Select(other => other.Z + other.Height).Append(floor(x, y)).Max();
            altitude = (transfer || stationary) && overlaps(item, destination) && support <= item.Z + item.Height + 0.0001 ? item.Z : support;
        }
        if (!double.IsFinite(altitude)) { fail("movement.altitude", "Invalid destination altitude.", frame.Stack); return; }
        if (!track_support && riders.Length > 0)
        {
            var contacts = furniture.Values.Where(other => other.Id != item.Id && riders.Any(rider =>
                covers(other, rider.X, rider.Y) || covers(other, rider.X + x - item.X, rider.Y + y - item.Y)))
                .Select(other => other.Id).ToHashSet();
            if (nodes.Values.Any(node => node.Name is "WalksOnFurniture" or "WalksOffFurniture" &&
                ((node.Furniture ?? []).Any(contacts.Contains) || node.FurnitureSources?.Any(source => source != WiredFurnitureSource.Picked) == true)))
                unsupported(carry!, "Walk-trigger delivery for stationary or dynamically selected surfaces during carry remains unverified.");
        }
        var prior_contacts = track_support ? riders.ToDictionary(user => user.Id, wired_contacts) : null;
        var moved = item with { X = x, Y = y, Z = altitude, Direction = destination.Direction };
        var projectile = projectile_motion(addons, item, moved, frame, transfer);
        var shooters = projectile_shooters(addons, item, moved, frame);
        if (projectile is not null) moved = moved with { Direction = projectile.Direction };
        moved = start_projectile(item, moved, projectile, addons);
        furniture[item.Id] = moved;
        queue_projectile(item.Id);
        foreach (var shooter in shooters)
        {
            users[shooter.User.Id] = shooter.User with { Direction = shooter.Direction };
            effects.Add(new(now, "MovementAnimation", frame.Stack ?? 0, [shooter.User.Id], Value: 0,
                Movement: new(Qx.Model.Messages.Incoming.WiredMovementType.AvatarDirection,
                    position(shooter.User), position(shooter.User), 0, shooter.Direction, shooter.Direction)));
            record("user.rotated", frame.Stack, "Projectile shooter direction", [shooter.User.Id]);
        }
        foreach (var rider in riders)
        {
            var carried = rider with { X = rider.X + x - item.X, Y = rider.Y + y - item.Y, Z = rider.Z + altitude - item.Z };
            var supported = shared_support && vertical_movement && rider.Id != support_actor ? rider :
                track_support ? located(carried, carried.X, carried.Y, walkable_only: true) : carried;
            if (track_support && !item.Walkable && item.Stance == Qx.Model.AvatarStance.Stand && supported.Stance == Qx.Model.AvatarStance.Stand)
                supported = supported with { Z = Math.Max(supported.Z, altitude) };
            users[rider.Id] = supported;
            if (track_support)
            {
                long duration = movement_duration(addons);
                float posture_height = (float)(rider.ActionHeight ?? 0);
                float surface_height = item.Walkable && item.Stance == Qx.Model.AvatarStance.Stand ? (float)item.Height : 0;
                var source = new Qx.Model.Tile(rider.X, rider.Y, (float)item.Z + surface_height + posture_height);
                var target = new Qx.Model.Tile(carried.X, carried.Y, (float)altitude + surface_height + posture_height);
                effects.Add(new(now, "MovementAnimation", frame.Stack ?? 0, [rider.Id], Value: duration,
                    Movement: new(Qx.Model.Messages.Incoming.WiredMovementType.Avatar,
                        source, target, checked((int)duration), rider.Direction, rider.Direction, IsSlide: true,
                        JumpPower: vertical_movement ? carried_curve : null)));
            }
            record("user.carried", frame.Stack, item.Key, [rider.Id]);
            if (track_support && !shared_support) carried_contacts(rider, users[rider.Id], prior_contacts![rider.Id], frame, item.Id);
        }
        if (track_support && shared_support)
        {
            var changing = riders.Where(rider => !prior_contacts![rider.Id].ToHashSet().SetEquals(floor_contacts(users[rider.Id])) &&
                (rider.X != users[rider.Id].X || rider.Y != users[rider.Id].Y)).ToArray();
            long? contact_actor = changing.Length == 0 ? null : changing.Length == 1 ? changing[0].Id : carrier_contact_actor(changing, frame);
            foreach (var rider in riders)
                carried_contacts(rider, users[rider.Id], prior_contacts![rider.Id], frame, item.Id, walk: rider.Id == contact_actor);
        }
        foreach (var user in left_behind)
            if (!shared_support || user.Id == support_actor)
                users[user.Id] = located(user, user.X, user.Y, walkable_only: true);
        if (item.Stance != Qx.Model.AvatarStance.Stand || transfer || packed_movement)
            foreach (var occupant in users.Values.Where(user => covers(destination, user.X, user.Y) && !riders.Any(rider => rider.Id == user.Id)).ToArray())
            {
                var supported = located(occupant, occupant.X, occupant.Y, walkable_only: packed_movement);
                if (packed_movement && !item.Walkable && item.Stance == Qx.Model.AvatarStance.Stand && supported.Stance == Qx.Model.AvatarStance.Stand)
                    supported = supported with { Z = Math.Max(supported.Z, altitude) };
                users[occupant.Id] = supported;
            }
        if (relative || transfer || group_movement || packed_movement || vertical_movement || curved || projectile is not null || addons.Any(node => node.Name is "AnimationTime" or "NoMoveAnimation"))
        {
            long duration = projectile?.AnimationTime ?? movement_duration(addons);
            effects.Add(new(now, "MovementAnimation", frame.Stack ?? 0, [item.Id], Value: duration,
                Movement: duration == 0 ? null : projectile ?? (relative || transfer || group_movement || curved || packed_movement || vertical_movement ? furniture_motion(item, furniture[item.Id], duration, curve) : null)));
        }
        if (!frame.Moved.Contains(item.Id)) frame.Moved.Add(item.Id);
        root_moves[frame.Root] = count + 1; moves++; charge(frame, 1, frame.Stack);
        record("move", frame.Stack, $"{item.X},{item.Y},{item.Z} -> {x},{y},{altitude}", [item.Id]);
    }
    private (int X, int Y) movement(int option, WiredSimulationNode node) => option switch
    {
        0 => (0, 0), 1 => direction_offset((int)(next_random() % 8)),
        2 => direction_offset(next_random() % 2 == 0 ? 2 : 6), 3 => direction_offset(next_random() % 2 == 0 ? 0 : 4),
        4 => direction_offset(0), 5 => direction_offset(2), 6 => direction_offset(4), 7 => direction_offset(6),
        8 => direction_offset(1), 9 => direction_offset(3), 10 => direction_offset(5), 11 => direction_offset(7),
        _ => invalid_movement(node)
    };
    private (int X, int Y) invalid_movement(WiredSimulationNode node) { unsupported(node); return (0, 0); }
    private static (int X, int Y) direction_offset(int direction) => direction switch
    { 0 => (0, -1), 1 => (1, -1), 2 => (1, 0), 3 => (1, 1), 4 => (0, 1), 5 => (-1, 1), 6 => (-1, 0), 7 => (-1, -1), _ => (0, 0) };
}
