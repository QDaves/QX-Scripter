using Qx.Messages;

namespace Qx.Model;

/// <summary>Specifies the relationship the local user has set on a friend.</summary>
/// <remarks>The values match <see cref="RelationshipType"/>.</remarks>
public enum Relation
{
    /// <summary>No relationship.</summary>
    None,
    /// <summary>Heart.</summary>
    Heart,
    /// <summary>Smile.</summary>
    Smile,
    /// <summary>Skull, which the client calls bobba.</summary>
    Skull
}

/// <summary>Represents a user on the local user's friend list.</summary>
public sealed class Friend : IParserComposer<Friend>
{
    /// <summary>Gets or sets the friend's user identifier.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the friend's name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the friend's gender, cast from the integer the hotel sends.</summary>
    public Gender Gender { get; set; }
    /// <summary>Gets or sets whether the friend is online.</summary>
    public bool IsOnline { get; set; }
    /// <summary>Gets or sets whether the local user may follow the friend into their room.</summary>
    public bool CanFollow { get; set; }
    /// <summary>Gets or sets the friend's figure string.</summary>
    public string Figure { get; set; } = "";
    /// <summary>Gets or sets the identifier of the friend list category the friend is filed under.</summary>
    public int CategoryId { get; set; }
    /// <summary>Gets or sets the friend's motto.</summary>
    public string Motto { get; set; } = "";
    /// <summary>Gets or sets the friend's real name, empty unless the hotel discloses it.</summary>
    public string RealName { get; set; } = "";
    /// <summary>Gets or sets the friend's Facebook identifier as sent by the hotel.</summary>
    public string FacebookId { get; set; } = "";
    /// <summary>Gets or sets whether the friend accepts messages while offline.</summary>
    public bool IsAcceptingOfflineMessages { get; set; }
    /// <summary>Gets or sets whether the hotel flags the friend as a VIP member.</summary>
    public bool IsVipMember { get; set; }
    /// <summary>Gets or sets whether the hotel flags the friend as a Pocket Habbo user.</summary>
    public bool IsPocketHabboUser { get; set; }
    /// <summary>Gets or sets the relationship the local user has set on the friend.</summary>
    public Relation Relation { get; set; }
    /// <summary>Gets or sets when the friend was last online.</summary>
    /// <remarks>The friend list packet does not carry it, so it stays 0 unless assigned.</remarks>
    public long LastOnline { get; set; }

    /// <summary>Initializes a new instance of the <see cref="Friend"/> class.</summary>
    public Friend() { }

    /// <summary>Reads a friend from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
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

    /// <summary>Writes the friend to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
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

    /// <summary>Returns the friend's name.</summary>
    /// <returns>The value of <see cref="Name"/>.</returns>
    public override string ToString() => Name;
}
