using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Reports the new state of a one way door furni.
/// </summary>
/// <param name="ItemId">The floor item whose state changed.</param>
/// <param name="Status">
/// The new state. The client passes this straight into the furni's state slot and replaces the
/// furni's stuff data with an empty one.
/// </param>
public sealed record OneWayDoorStatus(Id ItemId, int Status) : IParserComposer<OneWayDoorStatus>
{
    public int? UnityTrailingValue { get; init; }

    public static OneWayDoorStatus Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static OneWayDoorStatus ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    private static OneWayDoorStatus ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt())
        {
            UnityTrailingValue = p.ReadInt()
        };

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(OneWayDoorStatus value, in PacketWriter p)
    {
        p.WriteId(value.ItemId);
        p.WriteInt(value.Status);
    }

    private static void ComposeUnity(OneWayDoorStatus value, in PacketWriter p)
    {
        int trailing_value = value.UnityTrailingValue ??
            throw new InvalidOperationException("Unity one-way door status requires its native trailing value.");
        p.WriteId(value.ItemId);
        p.WriteInt(value.Status);
        p.WriteInt(trailing_value);
    }
}
