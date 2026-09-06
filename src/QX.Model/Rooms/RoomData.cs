using Qx.Messages;

namespace Qx.Model;

public sealed class RoomData : IParserComposer<RoomData>
{
    public Id Id { get; set; }
    public string Name { get; set; } = "";
    public Id OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public RoomDoorMode DoorMode { get; set; }
    public int UserCount { get; set; }
    public int MaxUserCount { get; set; }
    public string Description { get; set; } = "";
    public RoomTradeMode TradeMode { get; set; }
    public int Score { get; set; }
    public int Ranking { get; set; }
    public int Category { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];

    public string? OfficialRoomPicRef { get; set; }

    public bool HasGroup { get; set; }
    public Id GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public string GroupBadge { get; set; } = "";

    public bool HasEvent { get; set; }
    public string EventName { get; set; } = "";
    public string EventDescription { get; set; } = "";
    public int EventMinutesRemaining { get; set; }

    public bool ShowOwner { get; set; }
    public bool AllowPets { get; set; }
    public bool DisplayRoomEntryAd { get; set; }

    public RoomData() { }

    private RoomData(in PacketReader p)
    {
        Id = p.ReadId();
        Name = p.ReadString();
        OwnerId = p.ReadId();
        OwnerName = p.ReadString();
        DoorMode = (RoomDoorMode)p.ReadInt();
        UserCount = p.ReadInt();
        MaxUserCount = p.ReadInt();
        Description = p.ReadString();
        TradeMode = (RoomTradeMode)p.ReadInt();
        Score = p.ReadInt();
        Ranking = p.ReadInt();
        Category = p.ReadInt();

        int tagCount = p.ReadLength();
        var tags = new string[tagCount];
        for (int i = 0; i < tagCount; i++)
            tags[i] = p.ReadString();
        Tags = tags;

        int flags = p.ReadInt();
        if ((flags & 1) != 0)
            OfficialRoomPicRef = p.ReadString();
        if ((flags & 2) != 0)
        {
            HasGroup = true;
            GroupId = p.ReadId();
            GroupName = p.ReadString();
            GroupBadge = p.ReadString();
        }
        if ((flags & 4) != 0)
        {
            HasEvent = true;
            EventName = p.ReadString();
            EventDescription = p.ReadString();
            EventMinutesRemaining = p.ReadInt();
        }
        ShowOwner = (flags & 8) != 0;
        AllowPets = (flags & 16) != 0;
        DisplayRoomEntryAd = (flags & 32) != 0;
    }

    public static RoomData Parse(in PacketReader p) => new(in p);

    public void Compose(in PacketWriter p)
    {
        p.WriteId(Id);
        p.WriteString(Name);
        p.WriteId(OwnerId);
        p.WriteString(OwnerName);
        p.WriteInt((int)DoorMode);
        p.WriteInt(UserCount);
        p.WriteInt(MaxUserCount);
        p.WriteString(Description);
        p.WriteInt((int)TradeMode);
        p.WriteInt(Score);
        p.WriteInt(Ranking);
        p.WriteInt(Category);

        p.WriteLength((Length)Tags.Count);
        foreach (string tag in Tags)
            p.WriteString(tag);

        int flags = 0;
        if (OfficialRoomPicRef is not null) flags |= 1;
        if (HasGroup) flags |= 2;
        if (HasEvent) flags |= 4;
        if (ShowOwner) flags |= 8;
        if (AllowPets) flags |= 16;
        if (DisplayRoomEntryAd) flags |= 32;
        p.WriteInt(flags);

        if (OfficialRoomPicRef is not null)
            p.WriteString(OfficialRoomPicRef);
        if (HasGroup)
        {
            p.WriteId(GroupId);
            p.WriteString(GroupName);
            p.WriteString(GroupBadge);
        }
        if (HasEvent)
        {
            p.WriteString(EventName);
            p.WriteString(EventDescription);
            p.WriteInt(EventMinutesRemaining);
        }
    }

    public override string ToString() => $"{Name} ({Id})";
}
