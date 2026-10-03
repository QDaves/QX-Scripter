using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>ObjectRemove</c> message, received when a floor item is removed from the room.</summary>
/// <param name="Id">The identifier of the removed floor item, sent on the wire as a decimal string.</param>
/// <param name="IsExpired">Whether the item was removed because it expired.</param>
/// <param name="PickerId">The identifier of the user who picked the item up.</param>
/// <param name="Delay">The delay before the client removes the item.</param>
public sealed record FloorItemRemove(Id Id, bool IsExpired, Id PickerId, int Delay) : IParserComposer<FloorItemRemove>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FloorItemRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FloorItemRemove ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 11, nameof(FloorItemRemove));
        return ParseItem(
            in p,
            RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)));
    }

    private static FloorItemRemove ParseItem(in PacketReader p, Id id)
    {
        var result = new FloorItemRemove(id, p.ReadBool(), p.ReadId(), p.ReadInt());
        RoomPlacementWire.RequireEmpty(in p, nameof(FloorItemRemove));
        return result;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FloorItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId));
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        value.ComposeItem(in p);
    }

    private void ComposeItem(in PacketWriter p)
    {
        p.WriteBool(IsExpired);
        p.WriteId(PickerId);
        p.WriteInt(Delay);
    }
}

/// <summary>
/// Represents the <c>ObjectRemoveConfirm</c> message, received when the server asks the user to confirm picking up an
/// item.
/// </summary>
/// <param name="Category">The item category, 1 for a wall item or 2 for a floor item.</param>
/// <param name="ItemId">The identifier of the item to pick up.</param>
/// <param name="Title">The title of the confirmation dialog.</param>
/// <param name="Body">The body text of the confirmation dialog.</param>
public sealed record PickupConfirmation(int Category, Id ItemId, string Title, string Body)
    : IParserComposer<PickupConfirmation>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PickupConfirmation Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PickupConfirmation ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 12, nameof(PickupConfirmation));
        var result = new PickupConfirmation(
            RoomPlacementWire.RequireCategory(p.ReadInt(), nameof(PickupConfirmation)),
            p.ReadId(),
            p.ReadString(),
            p.ReadString());
        RoomPlacementWire.RequireEmpty(in p, nameof(PickupConfirmation));
        return result;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PickupConfirmation value, in PacketWriter p)
    {
        int category = RoomPlacementWire.RequireCategory(
            value.Category,
            nameof(PickupConfirmation));
        RoomPlacementWire.RequireId(value.ItemId, nameof(value.ItemId));
        RoomPlacementWire.RequireString(value.Title, nameof(value.Title), in p);
        RoomPlacementWire.RequireString(value.Body, nameof(value.Body), in p);
        p.WriteInt(category);
        p.WriteId(value.ItemId);
        p.WriteString(value.Title);
        p.WriteString(value.Body);
    }
}

/// <summary>
/// Represents the <c>ObjectRemoveMultiple</c> message, received when several floor items are removed at once.
/// </summary>
/// <param name="Ids">The identifiers of the removed floor items.</param>
/// <param name="PickerId">
/// The identifier of the user who picked the items up. The room state ignores it and only removes the items.
/// </param>
public sealed record FloorItemsRemove(IReadOnlyList<Id> Ids, Id PickerId)
    : IParserComposer<FloorItemsRemove>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static FloorItemsRemove Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FloorItemsRemove ParseFlash(in PacketReader p) => ParseItems(in p);

    private static FloorItemsRemove ParseItems(in PacketReader p)
    {
        int count = p.ReadLength();
        var ids = new Id[count];
        for (int i = 0; i < count; i++)
            ids[i] = p.ReadId();
        return new FloorItemsRemove(ids, p.ReadId());
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FloorItemsRemove value, in PacketWriter p) =>
        value.ComposeItems(in p);

    private void ComposeItems(in PacketWriter p)
    {
        p.WriteLength((Length)Ids.Count);
        foreach (Id id in Ids)
            p.WriteId(id);
        p.WriteId(PickerId);
    }
}
