using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record UserNameChanged(Id WebId, int Index, string NewName)
    : IParserComposer<UserNameChanged>
{
    /// <summary>Reads the message from the packet.</summary>
    public static UserNameChanged Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserNameChanged ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt(), p.ReadString());

    /// <summary>Writes the message to the packet.</summary>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserNameChanged value, in PacketWriter p)
    {
        p.WriteId(value.WebId);
        p.WriteInt(value.Index);
        p.WriteString(value.NewName);
    }
}
