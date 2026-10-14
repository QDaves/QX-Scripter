using Qx.Model;

namespace Qx.Game.WiredSimulation;

public sealed partial class WiredSimulation
{
    private void transfer_room(WiredSimulationNode node, Frame frame, long[] objects, long[] people)
    {
        if (objects.Length == 0 || people.Length == 0) return;
        if (objects.Length != 1 || people.Length != 1)
            unsupported(node, "Room teleport currently requires one room linker and one human; multiple targets need selection-order conformance.");
        var source = furniture[objects[0]];
        if (source.ClassName != "wf_room_linker")
            unsupported(node, "Teleport to Room requires a room linker; an ordinary furniture target is rejected by the hotel.");
        if (source.RoomLink is not { RoomId: not null, Destination: not null, DepartureMilliseconds: not null, EffectId: not null, Accessible: true } link)
            unsupported(node, "Supply the counterpart's room and tile, verified access and calibrated departure/effect values in RoomLink metadata; its paired ID alone is insufficient.");
        var destination = source.RoomLink!;
        if (destination.RoomId == initial.RoomId)
            unsupported(node, "A room linker pointing into the same room requires separate conformance.");
        var user = users[people[0]];
        if (user.IsBot || user.IsPet || user.Frozen || user.NextStep is not null || user.Effect != 0 || user.Stance != AvatarStance.Stand ||
            user.Trade is { Active: true } || pending_teleport(user.Id))
            unsupported(node, "Room-link transfers currently require an idle standing human without another effect, freeze, active trade or pending teleport.");
        enqueue(checked(now + destination.DepartureMilliseconds!.Value), frame,
            room_departure: new(user.Id, node.FurnitureId, source.Id, destination));
        users[user.Id] = user with { Effect = destination.EffectId!.Value };
        effects.Add(new(now, "RoomTeleportStarted", node.FurnitureId, [user.Id], RoomLink: destination));
        record("room.teleport", node.FurnitureId, "Teleporting through a verified paired room linker.", [user.Id]);
    }

    private void leave_through_link(RoomDeparture departure, Frame frame)
    {
        if (!users.TryGetValue(departure.UserId, out var user)) return;
        if (!furniture.ContainsKey(departure.SourceId))
        {
            fail("room.link_removed", "The source linker disappeared during its pending transfer.", departure.ActionId);
            return;
        }
        if (user.Effect != departure.Destination.EffectId)
        {
            fail("room.link_effect_changed", "Effect changes during a room-link transfer require separate conformance.", departure.ActionId);
            return;
        }
        stimulus(frame with { Root = ++root_sequence,
            Event = new(WiredSimulationEventKind.Leave, now, departure.UserId), Context = new(StringComparer.Ordinal) });
        effects.Add(new(now, "RoomTransfer", departure.ActionId, [departure.UserId], RoomLink: departure.Destination));
        record("room.left", departure.ActionId, "The user left this simulation room for the linked destination.", [departure.UserId], after: departure.Destination.RoomId);
    }
}
