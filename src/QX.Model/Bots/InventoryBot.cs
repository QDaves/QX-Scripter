using Qx.Messages;

namespace Qx.Model.Bots;

public sealed record InventoryBot(
    int Id,
    string Name,
    string Motto,
    string Gender,
    string Figure) : IParserComposer<InventoryBot>
{
    public static InventoryBot Parse(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString(),
            p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Id);
        p.WriteString(Name);
        p.WriteString(Motto);
        p.WriteString(Gender);
        p.WriteString(Figure);
    }
}

public sealed record BotSkill(int Id, string Data) : IParserComposer<BotSkill>
{
    public static BotSkill Parse(in PacketReader p) => new(p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Id);
        p.WriteString(Data);
    }
}
