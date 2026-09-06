using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record InfoHotelClosing(int MinutesUntilClosing) : IParserComposer<InfoHotelClosing>
{
    public static InfoHotelClosing Parse(in PacketReader p) => new(p.ReadInt());

    public void Compose(in PacketWriter p) => p.WriteInt(MinutesUntilClosing);
}
