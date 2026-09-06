using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CustomUserNotification(int Code) : IParserComposer<CustomUserNotification>
{
    public static CustomUserNotification Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(Code);
}
