using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FloorItemRemove(Id Id, bool IsExpired, Id PickerId, int Delay) : IParserComposer<FloorItemRemove>
{
    public static FloorItemRemove Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItemRemove ParseFlash(in PacketReader p)
    {
        RoomPlacementWire.RequireMinimum(in p, 11, nameof(FloorItemRemove));
        return ParseItem(
            in p,
            RoomPlacementWire.ReadFlashStringId(in p, nameof(Id)));
    }

    private static FloorItemRemove ParseUnity(in PacketReader p)
    {
        RoomPlacementWire.RequireSize(in p, 21, nameof(FloorItemRemove));
        return ParseItem(in p, p.ReadId());
    }

    private static FloorItemRemove ParseItem(in PacketReader p, Id id)
    {
        var result = new FloorItemRemove(id, p.ReadBool(), p.ReadId(), p.ReadInt());
        RoomPlacementWire.RequireEmpty(in p, nameof(FloorItemRemove));
        return result;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId), in p);
        RoomPlacementWire.WriteFlashStringId(value.Id, nameof(value.Id), in p);
        value.ComposeItem(in p);
    }

    private static void ComposeUnity(FloorItemRemove value, in PacketWriter p)
    {
        RoomPlacementWire.RequireId(value.Id, nameof(value.Id), in p);
        RoomPlacementWire.RequireId(value.PickerId, nameof(value.PickerId), in p);
        p.WriteId(value.Id);
        value.ComposeItem(in p);
    }

    private void ComposeItem(in PacketWriter p)
    {
        p.WriteBool(IsExpired);
        p.WriteId(PickerId);
        p.WriteInt(Delay);
    }
}

public sealed record PickupConfirmation(int Category, Id ItemId, string Title, string Body)
    : IParserComposer<PickupConfirmation>
{
    public static PickupConfirmation Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(PickupConfirmation value, in PacketWriter p)
    {
        int category = RoomPlacementWire.RequireCategory(
            value.Category,
            nameof(PickupConfirmation));
        RoomPlacementWire.RequireId(value.ItemId, nameof(value.ItemId), in p);
        RoomPlacementWire.RequireString(value.Title, nameof(value.Title), in p);
        RoomPlacementWire.RequireString(value.Body, nameof(value.Body), in p);
        p.WriteInt(category);
        p.WriteId(value.ItemId);
        p.WriteString(value.Title);
        p.WriteString(value.Body);
    }
}

/// <summary>
/// Removes several floor items in one message.
/// </summary>
/// <param name="Ids">The identifiers of the removed floor items.</param>
/// <param name="PickerId">
/// The user who picked the items up. The singular <see cref="FloorItemRemove"/> handler discards
/// this as well, so it is parsed for wire fidelity only.
/// </param>
public sealed record FloorItemsRemove(IReadOnlyList<Id> Ids, Id PickerId)
    : IParserComposer<FloorItemsRemove>
{
    public static FloorItemsRemove Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FloorItemsRemove ParseFlash(in PacketReader p) => ParseItems(in p);

    private static FloorItemsRemove ParseUnity(in PacketReader p) => ParseItems(in p);

    private static FloorItemsRemove ParseItems(in PacketReader p)
    {
        int count = p.ReadLength();
        var ids = new Id[count];
        for (int i = 0; i < count; i++)
            ids[i] = p.ReadId();
        return new FloorItemsRemove(ids, p.ReadId());
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FloorItemsRemove value, in PacketWriter p) =>
        value.ComposeItems(in p);

    private static void ComposeUnity(FloorItemsRemove value, in PacketWriter p) =>
        value.ComposeItems(in p);

    private void ComposeItems(in PacketWriter p)
    {
        p.WriteLength((Length)Ids.Count);
        foreach (Id id in Ids)
            p.WriteId(id);
        p.WriteId(PickerId);
    }
}
