using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record WallItemRemove(Id Id, Id PickerId) : IParserComposer<WallItemRemove>
{
    public static WallItemRemove Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static WallItemRemove ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 6, nameof(WallItemRemove));
        var result = new WallItemRemove(
            RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)),
            p.ReadId());
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemRemove));
        return result;
    }

    private static WallItemRemove ParseUnity(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 16, nameof(WallItemRemove));
        var result = new WallItemRemove(p.ReadId(), p.ReadId());
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemRemove));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(WallItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId), in p);
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        p.WriteId(value.PickerId);
    }

    private static void ComposeUnity(WallItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.Id, nameof(value.Id), in p);
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId), in p);
        p.WriteId(value.Id);
        p.WriteId(value.PickerId);
    }
}

/// <summary>
/// Removes several wall items in one message. Flash only; the Unity client has no counterpart.
/// </summary>
/// <param name="Ids">The identifiers of the removed wall items.</param>
/// <param name="PickerId">
/// The user who picked the items up. The singular <see cref="WallItemRemove"/> handler discards
/// this as well, so it is parsed for wire fidelity only.
/// </param>
public sealed record WallItemsRemove(IReadOnlyList<Id> Ids, Id PickerId)
    : IParserComposer<WallItemsRemove>
{
    public static WallItemsRemove Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WallItemsRemove ParseFlash(in PacketReader p)
    {
        int count = p.ReadLength();
        var ids = new Id[count];
        for (int i = 0; i < count; i++)
            ids[i] = p.ReadId();
        return new WallItemsRemove(ids, p.ReadId());
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemsRemove value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Ids.Count);
        foreach (Id id in value.Ids)
            p.WriteId(id);
        p.WriteId(value.PickerId);
    }
}
