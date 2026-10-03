using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the local user's own account data.</summary>
/// <remarks>Received as the Flash <c>UserObject</c> message.</remarks>
public sealed class UserData : IParserComposer<UserData>
{
    /// <summary>Gets or sets the user identifier.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the user's name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the user's figure string.</summary>
    public string Figure { get; set; } = "";
    /// <summary>Gets or sets the user's gender.</summary>
    public Gender Gender { get; set; } = Gender.Unisex;
    /// <summary>Gets or sets the user's motto.</summary>
    public string Motto { get; set; } = "";
    /// <summary>Gets or sets the user's real name, empty unless the hotel discloses it.</summary>
    public string RealName { get; set; } = "";
    /// <summary>Gets or sets the direct mail flag the hotel sends for the account.</summary>
    public bool DirectMail { get; set; }
    /// <summary>Gets or sets the total number of respects the user has received.</summary>
    public int RespectTotal { get; set; }
    /// <summary>Gets or sets how many respects the user can still give to users today.</summary>
    public int RespectLeft { get; set; }
    /// <summary>Gets or sets how many respects the user can still give to pets today.</summary>
    public int PetRespectLeft { get; set; }
    /// <summary>Gets or sets whether the account may publish to streams.</summary>
    public bool StreamPublishingAllowed { get; set; }
    /// <summary>Gets or sets the date of the user's last access as the hotel formats it.</summary>
    public string LastAccessDate { get; set; } = "";
    /// <summary>Gets or sets whether the user may change their name.</summary>
    public bool IsNameChangeable { get; set; }
    /// <summary>Gets or sets whether the account is safety locked.</summary>
    public bool IsSafetyLocked { get; set; }
    /// <summary>Gets or sets whether the account is trade locked.</summary>
    /// <remarks>An optional trailing field; see <see cref="TrailingFields"/>.</remarks>
    public bool IsTradeLocked { get; set; }
    /// <summary>Gets or sets the color of the user's name as the hotel sends it.</summary>
    /// <remarks>An optional trailing field; see <see cref="TrailingFields"/>.</remarks>
    public string NameColor { get; set; } = "";
    /// <summary>Gets or sets how many respect replenishes the user has left.</summary>
    /// <remarks>An optional trailing field; see <see cref="TrailingFields"/>.</remarks>
    public int RespectReplenishesLeft { get; set; }
    /// <summary>Gets or sets the maximum number of respects the user can give per day.</summary>
    /// <remarks>An optional trailing field; see <see cref="TrailingFields"/>.</remarks>
    public int MaxRespectPerDay { get; set; }

    /// <summary>
    /// Gets or sets how many of the four optional trailing fields the packet carries, from 0 to 4.
    /// </summary>
    /// <remarks>
    /// The fields are, in order, <see cref="IsTradeLocked"/>, <see cref="NameColor"/>,
    /// <see cref="RespectReplenishesLeft"/> and <see cref="MaxRespectPerDay"/>. Parsing counts the
    /// ones present and composing writes only that many. Defaults to 4.
    /// </remarks>
    public int TrailingFields { get; set; } = 4;

    /// <summary>Initializes a new instance of the <see cref="UserData"/> class.</summary>
    public UserData() { }

    /// <summary>Reads the user data from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when bytes remain after the last known field.</exception>
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

    /// <summary>Writes the user data to a packet, including as many trailing fields as <see cref="TrailingFields"/> says.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when <see cref="TrailingFields"/> is outside 0 to 4.</exception>
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

    /// <summary>Returns the user's name and identifier.</summary>
    /// <returns>A string in the form <c>Name (#Id)</c>.</returns>
    public override string ToString() => $"{Name} (#{Id})";
}
