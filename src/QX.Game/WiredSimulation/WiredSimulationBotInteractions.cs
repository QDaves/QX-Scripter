using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private long[] selected_bots(WiredSimulationNode node, Frame frame) =>
        has_user_source(node)
            ? us(node, frame).Where(id => users[id].IsBot).ToArray()
            : users.Values.Where(user => user.IsBot && user.Name.Equals(
                node.Text("bot_name", node.Text("optional_bot_name")), StringComparison.OrdinalIgnoreCase)).Select(user => user.Id).ToArray();

    private void hand_item(WiredSimulationNode node, long id, int item)
    {
        var user = users[id];
        if (user.HandItem == item) return;
        users[id] = user with { HandItem = item };
        effects.Add(new(now, "HandItem", node.FurnitureId, [id], Value: item));
    }

    private void give_hand_item(WiredSimulationNode node, Frame frame)
    {
        int item = checked((int)node.Number("hand_item"));
        if (item < 0) unsupported(node, "A negative hand item is not a valid captured item ID.");
        var recipients = us(node, frame, 1);
        if (recipients.Length == 0) return;
        if (recipients.Length != 1 || users[recipients[0]].IsBot || users[recipients[0]].IsPet)
            unsupported(node, "Hand-item delivery to multiple users, bots or pets requires separate conformance.");
        var target = users[recipients[0]];
        if (node.Text("optional_bot_name").Length == 0)
        {
            hand_item(node, target.Id, item);
            return;
        }
        var bots = selected_bots(node, frame);
        if (bots.Length == 0) return;
        if (bots.Length != 1) unsupported(node, "Delivery from multiple bots requires separate conformance.");
        var bot = users[bots[0]];
        if (bot.NextStep is not null || bot.Follow is not null || bot.Delivery is not null || pending_teleport(bot.Id) || bot.Frozen || bot.Effect != 0 || bot.Stance != AvatarStance.Stand)
            unsupported(node, "Hand-item delivery during another bot movement or effect requires conformance.");
        if (target.Stance != AvatarStance.Stand || Math.Abs(bot.Z - target.Z) > 0.2001)
            unsupported(node, "Hand-item delivery to seated or elevated users requires separate conformance.");
        if (Math.Max(Math.Abs(bot.X - target.X), Math.Abs(bot.Y - target.Y)) > 1)
        {
            start_delivery(node, frame, bot, target, item);
            return;
        }
        hand_item(node, bot.Id, item);
        hand_item(node, target.Id, item);
        hand_item(node, bot.Id, 0);
        dispatch(frame with { Root = ++root_sequence, Context = new(StringComparer.Ordinal),
            Event = new(WiredSimulationEventKind.BotAvatarReached, now, bot.Id, Users: [target.Id]) });
    }

    private void bot_speech(WiredSimulationNode node, Frame frame)
    {
        var bots = selected_bots(node, frame);
        if (bots.Length == 0) return;
        if (bots.Length != 1) unsupported(node, "Speech from multiple bots requires separate conformance.");
        bool directed = node.Name == "BotTalkDirectToAvtr";
        var recipients = directed ? us(node, frame, 1) : [];
        if (directed && recipients.Length == 0) return;
        if (directed && (recipients.Length != 1 || users[recipients[0]].IsBot || users[recipients[0]].IsPet))
            unsupported(node, "Speech directed at multiple users, bots or pets requires separate conformance.");
        long mode = node.Number("chat_mode");
        long width = node.Number("bubble_width", -1);
        if (mode is not (0 or 1) || width is < -1 or > 2)
            unsupported(node, "Unknown bot speech mode or bubble width.");
        string message = output_text(node, frame);
        var speech = mode == 0 ? WiredSimulationSpeechMode.Talk
            : directed ? WiredSimulationSpeechMode.Whisper : WiredSimulationSpeechMode.Shout;
        if (directed && mode == 0) message = users[recipients[0]].Name + ": " + message;
        effects.Add(new(now, node.Name, node.FurnitureId, recipients, message,
            Speech: new(bots[0], speech, checked((int)width), directed && mode == 1, node.Flag("hide_bot_name"))));
    }
}
