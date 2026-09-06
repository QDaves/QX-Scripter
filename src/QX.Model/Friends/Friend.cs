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
    public short UnityStatus { get; set; }
    public short UnityPlatform { get; set; }

    public Friend() { }

    public static Friend Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static Friend ParseUnity(in PacketReader p)
    {
        return new Friend
        {
            Id = p.ReadId(),
            Name = p.ReadString(),
            Gender = (Gender)p.ReadInt(),
            IsOnline = p.ReadBool(),
            CanFollow = p.ReadBool(),
            Figure = p.ReadString(),
            LastOnline = p.ReadLong(),
            Motto = p.ReadString(),
            IsAcceptingOfflineMessages = p.ReadBool(),
            IsVipMember = p.ReadBool(),
            IsPocketHabboUser = p.ReadBool(),
            Relation = (Relation)p.ReadShort(),
            UnityStatus = p.ReadShort(),
            UnityPlatform = p.ReadShort()
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(Friend value, in PacketWriter p)
    {
        p.WriteId(value.Id);
        p.WriteString(value.Name);
        p.WriteInt((int)value.Gender);
        p.WriteBool(value.IsOnline);
        p.WriteBool(value.CanFollow);
        p.WriteString(value.Figure);
        p.WriteLong(value.LastOnline);
        p.WriteString(value.Motto);
        p.WriteBool(value.IsAcceptingOfflineMessages);
        p.WriteBool(value.IsVipMember);
        p.WriteBool(value.IsPocketHabboUser);
        p.WriteShort((short)value.Relation);
        p.WriteShort(value.UnityStatus);
        p.WriteShort(value.UnityPlatform);
    }

    public override string ToString() => Name;
}
