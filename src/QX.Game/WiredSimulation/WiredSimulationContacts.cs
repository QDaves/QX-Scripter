using Qx.Model;
using Qx.Model.Wired;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private long[] wired_contacts(WiredSimulationUser user) => user.WiredContacts is { } contacts
        ? contacts.ToArray() : floor_contacts(user);

    private void remember_contacts(long id, long[] contacts)
    {
        if (users.TryGetValue(id, out var user) && !user.IsPet)
            users[id] = user with { WiredContacts = contacts.ToArray() };
    }

    private long[] floor_contacts(WiredSimulationUser user)
    {
        var contacts = furniture.Values.Where(item => covers(item, user.X, user.Y) &&
            Math.Abs(user.Z + (user.ActionHeight ?? 0) - item.Z -
                (item.Walkable || item.Stance != AvatarStance.Stand ? item.Height : 0)) < 0.01).ToArray();
        if (contacts.Length == 0) return [];
        double top = contacts.Max(item => item.Z);
        return contacts.Where(item => Math.Abs(item.Z - top) < 0.001).Select(item => item.Id).ToArray();
    }

    private long[] contact_selection(WiredSimulationNode node, Frame frame) =>
        furniture_source(node) == WiredFurnitureSource.Selector
            ? selection_for(node, frame, []).Furniture : fs(node, frame);

    private long carrier_contact_actor(IReadOnlyList<WiredSimulationUser> candidates, Frame frame)
    {
        if (options.CarrierContactChoices is { } choices && carrier_contact_choice < choices.Count)
        {
            var choice = choices[carrier_contact_choice];
            if (choice.ActionId != frame.Action || !candidates.Any(user => user.Id == choice.UserId))
                unsupported(nodes[frame.Stack!.Value], "The next CarrierContactChoices entry must name this movement action and an eligible carried user.");
            carrier_contact_choice++;
            return choice.UserId;
        }
        uncertain("movement.shared_contact_choice", "A shared carrier produces one walk-contact context. The selected user is not recoverable from room or movement-packet order. Supply CarrierContactChoices for a known branch; a permissive run samples one possible user.", frame.Action ?? frame.Stack ?? 0);
        return candidates[(int)(next_random() % (uint)candidates.Count)].Id;
    }

    private void carried_contacts(WiredSimulationUser before, WiredSimulationUser after, long[] previous, Frame frame, long carrier, bool walk = true)
    {
        if (before.X == after.X && before.Y == after.Y) return;
        var current = floor_contacts(after);
        if (current.Length > 1)
        {
            var stationary = current.Where(id => id != carrier).ToArray();
            if (current.Contains(carrier) && stationary.Length == 1)
                current = after.Stance != AvatarStance.Stand && furniture[carrier].Stance == after.Stance ? [carrier] : stationary;
            else uncertain("movement.contact_tie", "Several stationary furniture share the carried avatar's contact plane; their contact priority requires separate conformance.", frame.Stack ?? carrier);
        }
        remember_contacts(after.Id, current);
        foreach (long id in walk ? previous.Except(current) : [])
            dispatch(frame with { Event = new(WiredSimulationEventKind.Walk, now, after.Id, id,
                X: before.X, Y: before.Y, Furniture: [id], Change: -1) });
        var entering = current.Except(previous).ToArray();
        foreach (long id in walk ? entering : [])
            dispatch(frame with { Event = new(WiredSimulationEventKind.Walk, now, after.Id, id,
                X: after.X, Y: after.Y, Furniture: [id]) });
        if (after.IsBot && entering.Length > 0)
            dispatch(frame with { Event = new(WiredSimulationEventKind.BotDestinationReached, now, after.Id,
                X: after.X, Y: after.Y, Furniture: entering) });
    }
}
