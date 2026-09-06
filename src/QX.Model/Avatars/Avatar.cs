using Qx;
using Qx.Messages;

namespace Qx.Model;

public abstract class Avatar : IParserComposer<Avatar>
{
    public AvatarType Type { get; }
    public Id Id { get; }
    public int Index { get; }

    public string Name { get; set; } = "";
    public string Motto { get; set; } = "";
    public string Figure { get; set; } = "";

    public Tile Location { get; set; }
    public int Direction { get; set; }
    public int HeadDirection { get; set; }

    public int X => Location.X;
    public int Y => Location.Y;
    public Point XY => Location.XY;

    public AvatarStatus? CurrentUpdate { get; set; }
    public int Dance { get; set; }
    public int Effect { get; set; }
    public int HandItem { get; set; }
    public bool IsIdle { get; set; }
    public bool IsTyping { get; set; }
    public bool IsRemoved { get; internal set; }

    protected Avatar(AvatarType type, Id id, int index)
    {
        Type = type;
        Id = id;
        Index = index;
    }

    public virtual void Compose(in PacketWriter p)
    {
        p.WriteId(Id);
        p.WriteString(Name);
        p.WriteString(Motto);
        p.WriteString(Figure);
        p.WriteInt(Index);
        if (p.Client is ClientType.Unity)
        {
            p.WriteInt(Location.X);
            p.WriteInt(Location.Y);
            p.WriteString((FloatString)Location.Z);
        }
        else
        {
            p.Compose(Location);
        }
        p.WriteInt(Direction);
        p.WriteInt((int)Type);
    }

    public static Avatar Parse(in PacketReader p)
    {
        Id id = p.ReadId();
        string name = p.ReadString();
        string motto = p.ReadString();
        string figure = p.ReadString();
        int index = p.ReadInt();
        Tile location = p.Client is ClientType.Unity
            ? new Tile(p.ReadInt(), p.ReadInt(), (float)(FloatString)p.ReadString())
            : p.Parse<Tile>();
        int direction = p.ReadInt();
        var type = (AvatarType)p.ReadInt();

        Avatar avatar = type switch
        {
            AvatarType.User => new User(id, index, in p),
            AvatarType.Pet => new Pet(id, index, in p),
            AvatarType.PublicBot or AvatarType.PrivateBot => new Bot(type, id, index, in p),
            _ => throw new Exception($"Unknown avatar type: {type}.")
        };

        if (p.Client is ClientType.Unity && avatar is User user)
        {
            user.BadgeCode = p.ReadString();
            user.GroupBadge = p.ReadString();
            int count = p.ReadLength();
            user.GroupPayload.Capacity = checked(count * 3);
            for (int i = 0; i < count * 3; i++)
                user.GroupPayload.Add(p.ReadInt());
        }

        avatar.Name = name;
        avatar.Motto = motto;
        avatar.Figure = figure;
        avatar.Location = location;
        avatar.Direction = direction;
        avatar.HeadDirection = direction;
        return avatar;
    }

    public override string ToString() => Name;
}

public sealed class User : Avatar
{
    public Gender Gender { get; set; } = Gender.Unisex;
    public Id GroupId { get; set; } = -1;
    public int GroupStatus { get; set; }
    public string GroupName { get; set; } = "";
    public string FigureExtra { get; set; } = "";
    public int AchievementScore { get; set; }
    public bool IsStaff { get; set; }
    public string BadgeCode { get; set; } = "";
    public string GroupBadge { get; set; } = "";
    public List<int> GroupPayload { get; set; } = [];
    public int BadgeRank { get; set; } = -1;

    public User(Id id, int index) : base(AvatarType.User, id, index) { }

    internal User(Id id, int index, in PacketReader p) : this(id, index)
    {
        Gender = Genders.Parse(p.ReadString());
        GroupId = p.ReadId();
        GroupStatus = p.ReadInt();
        GroupName = p.ReadString();
        FigureExtra = p.ReadString();
        AchievementScore = p.ReadInt();
        IsStaff = p.ReadBool();
        if (p.Client is ClientType.Flash)
            BadgeRank = p.ReadInt();
    }

    public override void Compose(in PacketWriter p)
    {
        base.Compose(in p);
        p.WriteString(Gender.ToClientString().ToLowerInvariant());
        p.WriteId(GroupId);
        p.WriteInt(GroupStatus);
        p.WriteString(GroupName);
        p.WriteString(FigureExtra);
        p.WriteInt(AchievementScore);
        p.WriteBool(IsStaff);
        if (p.Client is ClientType.Unity)
        {
            if (GroupPayload.Count % 3 != 0)
                throw new InvalidOperationException("The group payload must contain complete groups of three integers.");

            p.WriteString(BadgeCode);
            p.WriteString(GroupBadge);
            p.WriteLength((Length)(GroupPayload.Count / 3));
            foreach (int value in GroupPayload)
                p.WriteInt(value);
        }
        else if (p.Client is ClientType.Flash)
        {
            p.WriteInt(BadgeRank);
        }
    }
}

public sealed class Pet : Avatar
{
    public int PetType { get; set; }
    public int Breed
    {
        get => PetType;
        set => PetType = value;
    }
    public Id OwnerId { get; set; } = -1;
    public string OwnerName { get; set; } = "";
    public int RarityLevel { get; set; }
    public bool HasSaddle { get; set; }
    public bool IsRiding { get; set; }
    public bool CanBreed { get; set; }
    public bool CanHarvest { get; set; }
    public bool CanRevive { get; set; }
    public bool HasBreedingPermission { get; set; }
    public int Level { get; set; }
    public string Posture { get; set; } = "";

    public Pet(Id id, int index) : base(AvatarType.Pet, id, index) { }

    internal Pet(Id id, int index, in PacketReader p) : this(id, index)
    {
        PetType = p.ReadInt();
        OwnerId = p.ReadId();
        OwnerName = p.ReadString();
        RarityLevel = p.ReadInt();
        HasSaddle = p.ReadBool();
        IsRiding = p.ReadBool();
        CanBreed = p.ReadBool();
        CanHarvest = p.ReadBool();
        CanRevive = p.ReadBool();
        HasBreedingPermission = p.ReadBool();
        Level = p.ReadInt();
        Posture = p.ReadString();
    }

    public override void Compose(in PacketWriter p)
    {
        base.Compose(in p);
        p.WriteInt(PetType);
        p.WriteId(OwnerId);
        p.WriteString(OwnerName);
        p.WriteInt(RarityLevel);
        p.WriteBool(HasSaddle);
        p.WriteBool(IsRiding);
        p.WriteBool(CanBreed);
        p.WriteBool(CanHarvest);
        p.WriteBool(CanRevive);
        p.WriteBool(HasBreedingPermission);
        p.WriteInt(Level);
        p.WriteString(Posture);
    }
}

public sealed class Bot : Avatar
{
    public bool IsPublicBot => Type == AvatarType.PublicBot;
    public bool IsPrivateBot => Type == AvatarType.PrivateBot;

    public Gender Gender { get; set; } = Gender.Unisex;
    public Id OwnerId { get; set; } = -1;
    public string OwnerName { get; set; } = "";
    public List<short> Skills { get; set; } = [];

    public Bot(AvatarType type, Id id, int index) : base(type, id, index)
    {
        if (type is not (AvatarType.PublicBot or AvatarType.PrivateBot))
            throw new ArgumentException($"Invalid avatar type for Bot: {type}.");
    }

    internal Bot(AvatarType type, Id id, int index, in PacketReader p) : this(type, id, index)
    {
        if (type is AvatarType.PrivateBot)
        {
            Gender = Genders.Parse(p.ReadString());
            OwnerId = p.ReadId();
            OwnerName = p.ReadString();
            Skills = [.. p.ReadShortArray()];
        }
    }

    public override void Compose(in PacketWriter p)
    {
        base.Compose(in p);
        if (Type is AvatarType.PrivateBot)
        {
            p.WriteString(Gender.ToClientString().ToLowerInvariant());
            p.WriteId(OwnerId);
            p.WriteString(OwnerName);
            p.WriteShortArray(Skills);
        }
    }
}
