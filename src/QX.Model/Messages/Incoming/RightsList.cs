using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct IdName(Id Id, string Name) : IParserComposer<IdName>
{
    public static IdName Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static IdName ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString());

    private static IdName ParseUnity(in PacketReader p) =>
        new(p.ReadLong(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(IdName value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
    }

    private static void ComposeUnity(IdName value, in PacketWriter p)
    {
        p.WriteLong(value.Id);
        p.WriteString(value.Name);
    }
}

public sealed record RightsList(Id RoomId, IReadOnlyList<IdName> Users) : IParserComposer<RightsList>
{
    public static RightsList Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RightsList ParseFlash(in PacketReader p) =>
        ParseUsers(p.ReadInt(), checked((ushort)p.ReadInt()), in p);

    private static RightsList ParseUnity(in PacketReader p) =>
        ParseUsers(p.ReadLong(), unchecked((ushort)p.ReadShort()), in p);

    private static RightsList ParseUsers(Id room_id, int count, in PacketReader p)
    {
        var users = new IdName[count];
        for (int i = 0; i < count; i++)
            users[i] = p.Parse<IdName>();
        return new RightsList(room_id, users);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RightsList value, in PacketWriter p)
    {
        ushort count = checked((ushort)value.Users.Count);
        p.WriteId(value.RoomId);
        p.WriteInt(count);
        foreach (IdName user in value.Users)
            p.Compose(user);
    }

    private static void ComposeUnity(RightsList value, in PacketWriter p)
    {
        ushort count = checked((ushort)value.Users.Count);
        p.WriteLong(value.RoomId);
        p.WriteShort(unchecked((short)count));
        foreach (IdName user in value.Users)
            p.Compose(user);
    }
}
