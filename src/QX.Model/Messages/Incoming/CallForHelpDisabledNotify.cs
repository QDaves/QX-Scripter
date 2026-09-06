using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CallForHelpDisabledNotify(string InfoUrl) : IParserComposer<CallForHelpDisabledNotify>
{
    public static CallForHelpDisabledNotify Parse(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) => p.WriteString(InfoUrl);
}
