using Qx.Messages;

namespace Qx.Model;

public enum Relation
{
    None,
    Heart,
    Smile,
    Skull
}

public sealed class Friend : IParserComposer<Friend>
{
    public Id Id { get; set; }
    public string Name { get; set; } = "";
    public Gender Gender { get; set; }
    public bool IsOnline { get; set; }
    public bool CanFollow { get; set; }
    public string Figure { get; set; } = "";
    public int CategoryId { get; set; }
    public string Motto { get; set; } = "";
    public string RealName { get; set; } = "";
    public string FacebookId { get; set; } = "";
    public bool IsAcceptingOfflineMessages { get; set; }
    public bool IsVipMember { get; set; }
    public bool IsPocketHabboUser { get; set; }
    public Relation Relation { get; set; }
    public long LastOnline { get; set; }

    public Friend() { }

    public static Friend Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static Friend ParseFlash(in PacketReader p)
    {
        return new Friend
        {
            Id = p.ReadId(),
            Name = p.ReadString(),
            Gender = (Gender)p.ReadInt(),
            IsOnline = p.ReadBool(),
            CanFollow = p.ReadBool(),
            Figure = p.ReadString(),
            CategoryId = p.ReadInt(),
            Motto = p.ReadString(),
            RealName = p.ReadString(),
            FacebookId = p.ReadString(),
            IsAcceptingOfflineMessages = p.ReadBool(),
            IsVipMember = p.ReadBool(),
            IsPocketHabboUser = p.ReadBool(),
            Relation = (Relation)p.ReadShort()
        };
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(Friend value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
        p.WriteInt((int)value.Gender);
        p.WriteBool(value.IsOnline);
        p.WriteBool(value.CanFollow);
        p.WriteString(value.Figure);
        p.WriteInt(value.CategoryId);
        p.WriteString(value.Motto);
        p.WriteString(value.RealName);
        p.WriteString(value.FacebookId);
        p.WriteBool(value.IsAcceptingOfflineMessages);
        p.WriteBool(value.IsVipMember);
        p.WriteBool(value.IsPocketHabboUser);
        p.WriteShort((short)value.Relation);
    }

    public override string ToString() => Name;
}
