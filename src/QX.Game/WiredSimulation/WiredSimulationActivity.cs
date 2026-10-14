using System.Globalization;
using System.Numerics;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private static bool invalid_activity(WiredSimulationUserActivity activity) =>
        activity.Dance is < 0 or > 4 || activity.Sign is < 0 or > 17 || activity.SignRemainingMilliseconds < 0 ||
        activity.SignObservedAt > 0 || !activity.SignKnown && (activity.Sign.HasValue || activity.SignRemainingMilliseconds.HasValue) ||
        activity.Sign is null && activity.SignRemainingMilliseconds.HasValue;

    private int? filter_parameter(WiredSimulationNode node)
    {
        string text = node.Text("action_filter");
        if (text.Length == 0) return null;
        long action = node.Number("action");
        if (action == 11 && text.StartsWith("dance ", StringComparison.Ordinal)) text = text[6..];
        else if (action != 10) unsupported(node, "This action has no supported detailed filter.");
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parameter) || text != parameter.ToString(CultureInfo.InvariantCulture) ||
            action == 10 && parameter is < 0 or > 17 || action == 11 && parameter is < 1 or > 4)
            unsupported(node, "The detailed action filter is outside its client-supported values.");
        return parameter;
    }

    private bool action_event_matches(WiredSimulationNode node, WiredSimulationEvent input)
    {
        int? wanted = filter_parameter(node);
        long action = node.Number("action");
        int? parameter = action switch { 10 when input.Activity?.SignKnown == true => input.Activity.Sign, 11 => input.Activity?.Dance, _ => null };
        if (input.Text is { Length: > 0 } text && action is 10 or 11)
        {
            if (action == 11 && text.StartsWith("dance ", StringComparison.Ordinal)) text = text[6..];
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || text != parsed.ToString(CultureInfo.InvariantCulture) ||
                action == 10 && parsed is < 0 or > 17 || action == 11 && parsed is < 0 or > 4)
                unsupported(node, "The action notification detail is invalid.");
            parameter = parsed;
        }
        if (action == 11 && parameter == 0 || action == 10 && input.Activity?.SignKnown == true && input.Activity.Sign is null && input.Text is null)
            return false;
        if (wanted is null) return true;
        if (parameter is null) uncertain("action.detail_unknown", "The action notification needs its sign number or dance style.", node.FurnitureId);
        return parameter == wanted;
    }

    private int? active_sign(WiredSimulationUser user, long source)
    {
        if (user.Activity is not { SignKnown: true } activity)
        {
            uncertain("action.sign_unknown", "Current sign state requires a confirmed activity snapshot or a complete @sign readout.", source);
            return null;
        }
        if (activity.Sign is null) return null;
        var age = (BigInteger)now - activity.SignObservedAt;
        if (activity.SignRemainingMilliseconds is { } remaining)
            return age >= remaining ? null : activity.Sign;
        if (age > 0) uncertain("action.sign_lifetime", "A sign observation does not reveal its remaining lifetime. Supply a calibrated duration or a newer observation.", source);
        return activity.Sign;
    }

    private bool activity_matches(WiredSimulationNode node, WiredSimulationUser user)
    {
        int? wanted = filter_parameter(node);
        long action = node.Number("action");
        if (action == 11 && user.Activity?.Dance is { } dance)
            return dance > 0 && (wanted is null || wanted == dance);
        if (action == 10 && user.Activity is not null)
        {
            int? sign = active_sign(user, node.FurnitureId);
            return sign.HasValue && (wanted is null || wanted == sign);
        }
        if (user.Action == -1)
        {
            uncertain("action.state_unknown", "The captured avatar state does not establish this action.", node.FurnitureId);
            return false;
        }
        if (user.Action != action) return false;
        if (wanted is not null) uncertain("action.detail_unknown", "Legacy Action alone does not specify a dance style or sign number.", node.FurnitureId);
        return true;
    }

    private bool apply_activity(WiredSimulationEvent input)
    {
        if (input.Activity is not { } update) return true;
        if (input.Kind != WiredSimulationEventKind.UserAction || input.UserId is not { } id || !users.TryGetValue(id, out var user) || invalid_activity(update))
        {
            fail("event.activity", "Activity updates require a current user, a UserAction event and valid activity fields.");
            return false;
        }
        var previous = user.Activity ?? new();
        users[id] = user with { Activity = previous with { Dance = update.Dance ?? previous.Dance,
            Sign = update.SignKnown ? update.Sign : previous.Sign, SignKnown = update.SignKnown || previous.SignKnown,
            SignRemainingMilliseconds = update.SignKnown ? update.SignRemainingMilliseconds : previous.SignRemainingMilliseconds,
            SignObservedAt = update.SignKnown ? now : previous.SignObservedAt } };
        return true;
    }

    private WiredSimulationUser activity_snapshot(WiredSimulationUser user)
    {
        var result = copy(user);
        if (user.Activity is not { SignKnown: true } activity) return result;
        if (activity.SignObservedAt > now || activity.SignRemainingMilliseconds < 0 || activity.Sign is < 0 or > 17) return result;
        if (activity.Sign is null) return result with { Activity = activity with { SignObservedAt = 0 } };
        var age = (BigInteger)now - activity.SignObservedAt;
        if (activity.SignRemainingMilliseconds is { } duration)
        {
            var remaining = duration - age;
            return result with { Activity = remaining <= 0
                ? activity with { Sign = null, SignRemainingMilliseconds = null, SignObservedAt = 0 }
                : activity with { SignRemainingMilliseconds = (long)remaining, SignObservedAt = 0 } };
        }
        return result with { Activity = activity with { SignObservedAt = (long)BigInteger.Max(long.MinValue, -age) } };
    }
}
