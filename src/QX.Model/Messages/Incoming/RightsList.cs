using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct IdName(Id Id, string Name) : IParserComposer<IdName>
{
    public static IdName Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static IdName ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(IdName value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }
}

public sealed record RightsList(Id RoomId, IReadOnlyList<IdName> Users) : IParserComposer<RightsList>
{
    public static RightsList Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RightsList ParseFlash(in PacketReader p) =>
        ParseUsers(p.ReadInt(), checked((ushort)p.ReadInt()), in p);

    private static RightsList ParseUsers(Id room_id, int count, in PacketReader p)
    {
        var users = new IdName[count];
        for (int i = 0; i < count; i++)
            users[i] = p.Parse<IdName>();
        return new RightsList(room_id, users);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RightsList value, in PacketWriter p)
    {
        ushort count = checked((ushort)value.Users.Count);
        p.WriteId(value.RoomId);
        p.WriteInt(count);
        foreach (IdName user in value.Users)
            p.Compose(user);
    }
}
