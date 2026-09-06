using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record UserNftWardrobeSelection(string CurrentTokenId, string FallbackFigureString, string FallbackFigureGender)
    : IParserComposer<UserNftWardrobeSelection>
{
    public static UserNftWardrobeSelection Parse(in PacketReader p) => new(p.ReadString(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteString(CurrentTokenId);
        p.WriteString(FallbackFigureString);
        p.WriteString(FallbackFigureGender);
    }
}
