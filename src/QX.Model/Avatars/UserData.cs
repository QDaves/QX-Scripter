using Qx.Messages;

namespace Qx.Model;

public sealed class UserData : IParserComposer<UserData>
{
    public Id Id { get; set; }
    public string Name { get; set; } = "";
    public string Figure { get; set; } = "";
    public Gender Gender { get; set; } = Gender.Unisex;
    public string Motto { get; set; } = "";
    public string RealName { get; set; } = "";
    public bool DirectMail { get; set; }
    public int RespectTotal { get; set; }
    public int RespectLeft { get; set; }
    public int PetRespectLeft { get; set; }
    public bool StreamPublishingAllowed { get; set; }
    public string LastAccessDate { get; set; } = "";
    public bool IsNameChangeable { get; set; }
    public bool IsSafetyLocked { get; set; }
    public bool IsTradeLocked { get; set; }
    public string NameColor { get; set; } = "";
    public int RespectReplenishesLeft { get; set; }
    public int MaxRespectPerDay { get; set; }

    public int TrailingFields { get; set; } = 4;

    public UserData() { }

    public static UserData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static UserData ParseFlash(in PacketReader p)
    {
        var value = new UserData
        {
            Id = p.ReadInt(),
            Name = p.ReadString(),
            Figure = p.ReadString(),
            Gender = Genders.Parse(p.ReadString()),
            Motto = p.ReadString(),
            RealName = p.ReadString(),
            DirectMail = p.ReadBool(),
            RespectTotal = p.ReadInt(),
            RespectLeft = p.ReadInt(),
            PetRespectLeft = p.ReadInt(),
            StreamPublishingAllowed = p.ReadBool(),
            LastAccessDate = p.ReadString(),
            IsNameChangeable = p.ReadBool(),
            IsSafetyLocked = p.ReadBool(),
            TrailingFields = 0
        };

        if (p.Available > 0)
        {
            value.IsTradeLocked = p.ReadBool();
            value.TrailingFields++;
        }
        if (p.Available > 0)
        {
            value.NameColor = p.ReadString();
            value.TrailingFields++;
        }
        if (p.Available > 0)
        {
            value.RespectReplenishesLeft = p.ReadInt();
            value.TrailingFields++;
        }
        if (p.Available > 0)
        {
            value.MaxRespectPerDay = p.ReadInt();
            value.TrailingFields++;
        }
        if (p.Available != 0)
            throw new InvalidDataException($"Flash user-data payload contains {p.Available} trailing bytes.");
        return value;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(UserData value, in PacketWriter p)
    {
        int id = checked((int)(long)value.Id);
        string gender = value.Gender.ToClientString().ToLowerInvariant();
        Validate(value, gender, in p);
        p.WriteInt(id);
        p.WriteString(value.Name);
        p.WriteString(value.Figure);
        p.WriteString(gender);
        p.WriteString(value.Motto);
        p.WriteString(value.RealName);
        p.WriteBool(value.DirectMail);
        p.WriteInt(value.RespectTotal);
        p.WriteInt(value.RespectLeft);
        p.WriteInt(value.PetRespectLeft);
        p.WriteBool(value.StreamPublishingAllowed);
        p.WriteString(value.LastAccessDate);
        p.WriteBool(value.IsNameChangeable);
        p.WriteBool(value.IsSafetyLocked);

        if (value.TrailingFields < 1)
            return;
        p.WriteBool(value.IsTradeLocked);

        if (value.TrailingFields < 2)
            return;
        p.WriteString(value.NameColor);

        if (value.TrailingFields < 3)
            return;
        p.WriteInt(value.RespectReplenishesLeft);

        if (value.TrailingFields < 4)
            return;
        p.WriteInt(value.MaxRespectPerDay);
    }

    private static void Validate(UserData value, string gender, in PacketWriter p)
    {
        if ((uint)value.TrailingFields > 4)
            throw new InvalidDataException($"Invalid user-data tail length {value.TrailingFields}.");

        RequireString(value.Name, nameof(Name), in p);
        RequireString(value.Figure, nameof(Figure), in p);
        RequireString(gender, nameof(Gender), in p);
        RequireString(value.Motto, nameof(Motto), in p);
        RequireString(value.RealName, nameof(RealName), in p);
        RequireString(value.LastAccessDate, nameof(LastAccessDate), in p);
        if (value.TrailingFields >= 2)
            RequireString(value.NameColor, nameof(NameColor), in p);
    }

    private static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException($"{name} exceeds the wire string limit.", name);
    }

    public override string ToString() => $"{Name} (#{Id})";
}
