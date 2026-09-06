using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record LoginFailedHotelClosed(int OpenHour, int OpenMinute) : IParserComposer<LoginFailedHotelClosed>
{
    public static LoginFailedHotelClosed Parse(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(OpenHour);
        p.WriteInt(OpenMinute);
    }
}
