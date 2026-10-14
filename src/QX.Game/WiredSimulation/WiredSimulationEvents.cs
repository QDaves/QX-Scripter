using System.Globalization;
using System.Text.RegularExpressions;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private bool trigger_matches(WiredSimulationNode node, Frame frame)
    {
        var input = frame.Event;
        bool selected() => input.FurnitureId.HasValue && (node.Furniture ?? []).Contains(input.FurnitureId.Value);
        if (input.Kind == WiredSimulationEventKind.Start)
        {
            if (timer_trigger(node) && !timers_initialized) schedule_timer(node, frame);
            return false;
        }
        return node.Name switch
        {
            "RoomInitialized" => input.Kind == WiredSimulationEventKind.RoomInitialized,
            "ReceiveLinkMessage" => link_trigger(node, frame, WiredSimulationEventKind.LinkMessage),
            "ReceiveLinkDataframe" => link_trigger(node, frame, WiredSimulationEventKind.LinkDataframe),
            "AvatarSaysSomething" => input.Kind == WiredSimulationEventKind.Chat && chat_matches(node, frame),
            "AvatarClicksFurni" => input.Kind == WiredSimulationEventKind.ClickFurniture && selected(),
            "UseStuff" => input.Kind == WiredSimulationEventKind.UseFurniture && selected(),
            "StateChange" => input.Kind == WiredSimulationEventKind.StateChanged && selected() && state_matches(node, input),
            "WalksOnFurniture" => input.Kind == WiredSimulationEventKind.Walk && input.Change != -1 && input.UserId is { } id && users.TryGetValue(id, out var user) &&
                (input.Furniture is { } entered ? entered.Count > 0 && entered.Intersect(contact_selection(node, frame)).Any() : contact_selection(node, frame).Any(item => covers(furniture[item], user.X, user.Y))),
            "WalksOffFurniture" => input.Kind == WiredSimulationEventKind.Walk && input.Change == -1 && input.Furniture is { Count: > 0 } exited && exited.Intersect(contact_selection(node, frame)).Any(),
            "AvatarEntersRoom" => input.Kind == WiredSimulationEventKind.Enter,
            "AvatarLeavesRoom" => input.Kind == WiredSimulationEventKind.Leave,
            "GameStarts" => input.Kind == WiredSimulationEventKind.GameStarts,
            "GameEnds" => input.Kind == WiredSimulationEventKind.GameEnds,
            "ScoreAchieved" => score_reached(node, input),
            "AvatarCaught" => input.Kind == WiredSimulationEventKind.AvatarCaught,
            "ClockReachTime" => input.Kind == WiredSimulationEventKind.ClockReached && input.NewValue == clock_threshold(node) && input.FurnitureId is { } clock && fs(node, frame).Contains(clock),
            "BotDestinationReached" => input.Kind == WiredSimulationEventKind.BotDestinationReached && bot_matches(node, input) &&
                (input.Furniture is null || fs(node, frame).Intersect(input.Furniture).Any()),
            "BotAvatarReached" => input.Kind == WiredSimulationEventKind.BotAvatarReached && bot_matches(node, input),
            "ReceiveSignal" => input.Kind == WiredSimulationEventKind.Signal && (node.Furniture ?? []).Intersect(input.FurnitureId is { } antenna ? [antenna] : []).Any(),
            "UserClicksUser" => input.Kind == WiredSimulationEventKind.ClickUser,
            "UserPerformsAction" => input.Kind == WiredSimulationEventKind.UserAction && input.NewValue == node.Number("action") && action_event_matches(node, input),
            "ClickTile" => input.Kind == WiredSimulationEventKind.ClickFurniture && selected(),
            "VariableUpdate" => input.Kind == WiredSimulationEventKind.VariableChanged && watched_variable(node, input) && variable_matches(node, input),
            "TransactionCompleted" => transaction_matches(node, frame, WiredSimulationEventKind.TransactionCompleted),
            "TransactionFailed" => transaction_matches(node, frame, WiredSimulationEventKind.TransactionFailed),
            "TriggerOnce" or "TriggerPeriodically" or "PeriodicLong" or "PeriodicShort" => false,
            _ => unknown_trigger(node, input)
        };
    }

    private bool transaction_matches(WiredSimulationNode node, Frame frame, WiredSimulationEventKind kind)
    {
        if (frame.Event.Kind != kind) return false;
        var source = furniture_source(node);
        if (source is not (WiredFurnitureSource.Picked or WiredFurnitureSource.Selector))
            unsupported(node, "Unknown transaction trigger furniture source.");
        var selected = source == WiredFurnitureSource.Selector ? selection_for(node, frame, []).Furniture : fs(node, frame);
        return frame.Event.FurnitureId is { } contract && selected.Contains(contract);
    }

    private bool bot_matches(WiredSimulationNode node, WiredSimulationEvent input)
    {
        if (has_user_source(node) && user_source(node) != WiredUserSource.NamedBot)
            unsupported(node, "Bot trigger selection from this source requires separate conformance.");
        if (node.Name == "BotDestinationReached" && has_furniture_source(node) && furniture_source(node) != WiredFurnitureSource.Picked)
            unsupported(node, "Bot destination trigger selection from this source requires separate conformance.");
        return input.UserId is { } id && users.TryGetValue(id, out var user) && user.IsBot &&
            user.Name.Equals(node.Text("bot_name"), StringComparison.OrdinalIgnoreCase);
    }

    private bool score_team(WiredSimulationNode node, WiredSimulationEvent input)
    {
        if (node.Number("team") == 0) return true;
        int? team = input.Team ?? (input.UserId is { } id && users.TryGetValue(id, out var user) ? user.Team : null);
        if (team is null) unsupported(node, "Team-filtered score events require a team or existing user.");
        return team == node.Number("team");
    }

    private bool state_matches(WiredSimulationNode node, WiredSimulationEvent input)
    {
        if (node.Number("state_mode") == 0) return true;
        if (node.Number("state_mode") != 1) unsupported(node, "Unknown state trigger mode.");
        var saved_rows = WiredSimulationSnapshots.read(node);
        if (saved_rows is null) unsupported(node, "Current-state triggers require saved state values.");
        return saved_rows!.Any(item => item.Id == input.FurnitureId && item.State.HasValue && item.State == input.NewValue);
    }

    private bool watched_variable(WiredSimulationNode node, WiredSimulationEvent input)
    {
        if (!node.Flag("select_variable_boxes"))
        {
            if (variables.TryGetValue(node.Text("variable_id"), out var definition) &&
                (definition.PointerAccessor is not null || definition.RequiresExecutionContext == true))
                unsupported(node, "Watching an execution-context-dependent variable requires separate server conformance.");
            return input.Variable == node.Text("variable_id");
        }
        var selected = node.Furniture ?? [];
        var definitions = variables.Values.Where(variable => variable.DefinitionFurnitureId is { } box && selected.Contains(box)).ToArray();
        if (definitions.Any(variable => variable.PointerAccessor is not null || variable.RequiresExecutionContext == true))
            unsupported(node, "Watching an execution-context-dependent variable requires separate server conformance.");
        if (selected.Any(box => definitions.All(variable => variable.DefinitionFurnitureId != box)))
            unsupported(node, "Variable-box selection requires variable definitions with DefinitionFurnitureId metadata for every selected box.");
        return definitions.Any(variable => variable.Id == input.Variable);
    }

    private bool variable_matches(WiredSimulationNode node, WiredSimulationEvent input)
    {
        long origins = node.Number("origin_mask");
        if (input.Origin is < 0 or > 3) unsupported(node, "Unknown variable change origin.");
        if (origins != -1 && (origins & (1L << input.Origin)) == 0) return false;
        if (input.Change == 1) return node.Flag("created");
        if (input.Change == 4) return node.Flag("deleted");
        if (input.Change != 2 || !node.Flag("value_changed")) return false;
        if (!input.OldValue.HasValue || !input.NewValue.HasValue) unsupported(node, "Value-change filters require both old and new values.");
        int direction = input.NewValue > input.OldValue ? 0 : input.NewValue < input.OldValue ? 1 : 2;
        return (node.Number("change_mask") & (1L << direction)) != 0;
    }

    private bool unknown_trigger(WiredSimulationNode node, WiredSimulationEvent input)
    {
        if (input.Kind != WiredSimulationEventKind.Start) unsupported(node);
        return false;
    }

    private bool chat_matches(WiredSimulationNode node, Frame frame)
    {
        if (node.Flag("owner_only") && frame.Event.UserId != initial.OwnerId) return false;
        string text = (frame.Event.Text ?? "").ToLowerInvariant();
        string pattern = node.Text("text").ToLowerInvariant();
        if (node.Number("match_mode") == 2) return true;
        var markers = Regex.Matches(pattern, @"#\(([^)]+)\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (markers.Count == 0) return node.Number("match_mode") == 1 ? text == pattern : text.Contains(pattern, StringComparison.Ordinal);
        var capturers = siblings(node).Where(box => box.Category == WiredFormCategory.Addon && box.Name == "VariableCapturer").ToArray();
        var expression = new System.Text.StringBuilder();
        int end = 0;
        for (int index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            var capture = capturers.SingleOrDefault(box => box.Text("capture_name").Equals(marker.Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            if (capture is null) { record("chat.unbound", node.FurnitureId, "Missing capturer for " + marker.Groups[1].Value); return false; }
            expression.Append(Regex.Escape(pattern[end..marker.Index]));
            bool adjacent = index + 1 < markers.Count && marker.Index + marker.Length == markers[index + 1].Index || index > 0 && markers[index - 1].Index + markers[index - 1].Length == marker.Index;
            expression.Append("(?<g").Append(index).Append('>').Append(capture.Flag("text_mode") ? adjacent ? ".{0,1}" : ".*?" : "[+-]?[0-9]+").Append(')');
            end = marker.Index + marker.Length;
        }
        expression.Append(Regex.Escape(pattern[end..]));
        string search = node.Number("match_mode") == 1 ? "\\A" + expression + "\\z" : expression.ToString();
        var match = Regex.Match(text, search, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!match.Success) return false;
        for (int index = 0; index < markers.Count; index++)
        {
            var capture = capturers.Single(box => box.Text("capture_name").Equals(markers[index].Groups[1].Value, StringComparison.OrdinalIgnoreCase));
            string captured = match.Groups["g" + index].Value;
            string variable = capture.Text("variable");
            if (variables.TryGetValue(variable, out var selected) && (selected.PointerAccessor is not null || selected.RequiresExecutionContext == true))
                unsupported(capture, "Capturing directly into an execution-context-dependent variable requires separate server conformance.");
            long number;
            if (capture.Flag("text_mode"))
            {
                if (!variables.TryGetValue(variable, out var definition) || definition.Text is null)
                { unsupported(capture, "Text capture requires its variable text connector in the snapshot."); return false; }
                var entries = definition.Text.Where(pair => pair.Value.Equals(captured, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (entries.Length != 1) return false;
                number = entries[0].Key;
            }
            else if (!long.TryParse(captured, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number)) return false;
            frame.Context[variable] = number;
        }
        return true;
    }

    private void repeat(WiredSimulationNode trigger, Frame frame)
    {
        long delay = trigger.Name switch
        {
            "TriggerPeriodically" => trigger.Number("interval_pulses") * options.PulseMilliseconds,
            "PeriodicShort" => trigger.Number("interval_units") * options.ShortPulseMilliseconds,
            "PeriodicLong" => trigger.Number("interval_units") * 5000,
            _ => 0
        };
        if (delay > 0) enqueue(checked(now + delay), copy(frame) with { Root = ++root_sequence }, trigger: trigger.FurnitureId);
    }

    private void signal(WiredSimulationNode node, Frame frame)
    {
        long[] antennas = fs(node, frame);
        long[] selected = fs(node, frame, 1);
        long[] selected_users = us(node, frame);
        IEnumerable<long[]> furniture_groups = node.Flag("split_furniture") ? selected.Select(id => new[] { id }) : [selected];
        IEnumerable<long[]> user_groups = node.Flag("split_users") ? selected_users.Select(id => new[] { id }) : [selected_users];
        foreach (long antenna in antennas)
        foreach (long[] group in furniture_groups)
        foreach (long[] people in user_groups)
        {
            var outgoing = new WiredSimulationEvent(WiredSimulationEventKind.Signal, now, frame.Event.UserId, antenna, Furniture: group, Users: people);
            var next = frame with { Event = outgoing };
            enqueue(now, next, external: true);
            if (furniture.TryGetValue(antenna, out var item) && item.ClassName is "wf_antenna1" or "wf_antenna2")
            {
                if (item.State is not (0 or 1)) unsupported(node, "The signal antenna requires a binary state.");
                set_state(item, 1 - item.State, frame);
            }
            record("signal", node.FurnitureId, "Signal scheduled.", group);
        }
    }
}
