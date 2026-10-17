using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record EventLogRequest(string Category, string Type, string Action, string Extra, int Value)
    : IParserComposer<EventLogRequest>
{
    public static EventLogRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static EventLogRequest ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString(), p.ReadString(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(EventLogRequest value, in PacketWriter p)
    {
        NavigatorProtocolWire.ValidateString(value.Category, nameof(Category), in p);
        NavigatorProtocolWire.ValidateString(value.Type, nameof(Type), in p);
        NavigatorProtocolWire.ValidateString(value.Action, nameof(Action), in p);
        NavigatorProtocolWire.ValidateString(value.Extra, nameof(Extra), in p);
        p.WriteString(value.Category);
        p.WriteString(value.Type);
        p.WriteString(value.Action);
        p.WriteString(value.Extra);
        p.WriteInt(value.Value);
    }
}
