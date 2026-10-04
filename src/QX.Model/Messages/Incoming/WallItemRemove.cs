using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>ItemRemove</c> message, received when a wall item is removed from the room.</summary>
/// <param name="Id">The ID of the removed wall item, sent as a decimal string.</param>
/// <param name="PickerId">The ID of the user who picked the item up.</param>
public sealed record WallItemRemove(Id Id, Id PickerId) : IParserComposer<WallItemRemove>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WallItemRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemRemove ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 6, nameof(WallItemRemove));
        var result = new WallItemRemove(
            RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)),
            p.ReadId());
        RoomPlacementWire.RequireEmpty(in p, nameof(WallItemRemove));
        return result;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId));
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        p.WriteId(value.PickerId);
    }
}

/// <summary>Represents the <c>ItemRemoveMultiple</c> message, received when several wall items are removed from the room at once.</summary>
/// <param name="Ids">The IDs of the removed wall items.</param>
/// <param name="PickerId">The ID of the user who picked the items up.</param>
public sealed record WallItemsRemove(IReadOnlyList<Id> Ids, Id PickerId)
    : IParserComposer<WallItemsRemove>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WallItemsRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WallItemsRemove ParseFlash(in PacketReader p)
    {
        int count = p.ReadLength();
        var ids = new Id[count];
        for (int i = 0; i < count; i++)
            ids[i] = p.ReadId();
        return new WallItemsRemove(ids, p.ReadId());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WallItemsRemove value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Ids.Count);
        foreach (Id id in value.Ids)
            p.WriteId(id);
        p.WriteId(value.PickerId);
    }
}
