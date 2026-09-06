namespace Qx.Model;

internal static class RoomObjectSnapshot
{
    internal static Avatar Copy(Avatar avatar)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        Avatar snapshot = avatar switch
        {
            User user => new User(user.Id, user.Index)
            {
                Gender = user.Gender,
                GroupId = user.GroupId,
                GroupStatus = user.GroupStatus,
                GroupName = user.GroupName,
                FigureExtra = user.FigureExtra,
                AchievementScore = user.AchievementScore,
                IsStaff = user.IsStaff,
                BadgeCode = user.BadgeCode,
                GroupBadge = user.GroupBadge,
                GroupPayload = [.. user.GroupPayload],
                BadgeRank = user.BadgeRank
            },
            Pet pet => new Pet(pet.Id, pet.Index)
            {
                PetType = pet.PetType,
                OwnerId = pet.OwnerId,
                OwnerName = pet.OwnerName,
                RarityLevel = pet.RarityLevel,
                HasSaddle = pet.HasSaddle,
                IsRiding = pet.IsRiding,
                CanBreed = pet.CanBreed,
                CanHarvest = pet.CanHarvest,
                CanRevive = pet.CanRevive,
                HasBreedingPermission = pet.HasBreedingPermission,
                Level = pet.Level,
                Posture = pet.Posture
            },
            Bot bot => new Bot(bot.Type, bot.Id, bot.Index)
            {
                Gender = bot.Gender,
                OwnerId = bot.OwnerId,
                OwnerName = bot.OwnerName,
                Skills = [.. bot.Skills]
            },
            _ => throw new NotSupportedException($"Unsupported room avatar type: {avatar.GetType().FullName}.")
        };

        snapshot.Name = avatar.Name;
        snapshot.Motto = avatar.Motto;
        snapshot.Figure = avatar.Figure;
        snapshot.Location = avatar.Location;
        snapshot.Direction = avatar.Direction;
        snapshot.HeadDirection = avatar.HeadDirection;
        snapshot.CurrentUpdate = avatar.CurrentUpdate?.Snapshot();
        snapshot.Dance = avatar.Dance;
        snapshot.Effect = avatar.Effect;
        snapshot.HandItem = avatar.HandItem;
        snapshot.IsIdle = avatar.IsIdle;
        snapshot.IsTyping = avatar.IsTyping;
        snapshot.IsRemoved = avatar.IsRemoved;
        return snapshot;
    }

    internal static FloorItem Copy(FloorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new FloorItem
        {
            Kind = item.Kind,
            Id = item.Id,
            OwnerId = item.OwnerId,
            OwnerName = item.OwnerName,
            SecondsToExpiration = item.SecondsToExpiration,
            Usage = item.Usage,
            Identifier = item.Identifier,
            IsHidden = item.IsHidden,
            IsRemoved = item.IsRemoved,
            Location = item.Location,
            Direction = item.Direction,
            Height = item.Height,
            Extra = item.Extra,
            Data = Copy(item.Data),
            SizeX = item.SizeX,
            SizeZ = item.SizeZ
        };
    }

    internal static WallItem Copy(WallItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new WallItem
        {
            Kind = item.Kind,
            Id = item.Id,
            OwnerId = item.OwnerId,
            OwnerName = item.OwnerName,
            SecondsToExpiration = item.SecondsToExpiration,
            Usage = item.Usage,
            Identifier = item.Identifier,
            IsHidden = item.IsHidden,
            IsRemoved = item.IsRemoved,
            Location = item.Location,
            Data = item.Data
        };
    }

    internal static RoomData Copy(RoomData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new RoomData
        {
            Id = data.Id,
            Name = data.Name,
            OwnerId = data.OwnerId,
            OwnerName = data.OwnerName,
            DoorMode = data.DoorMode,
            UserCount = data.UserCount,
            MaxUserCount = data.MaxUserCount,
            Description = data.Description,
            TradeMode = data.TradeMode,
            Score = data.Score,
            Ranking = data.Ranking,
            Category = data.Category,
            Tags = [.. data.Tags],
            OfficialRoomPicRef = data.OfficialRoomPicRef,
            HasGroup = data.HasGroup,
            GroupId = data.GroupId,
            GroupName = data.GroupName,
            GroupBadge = data.GroupBadge,
            HasEvent = data.HasEvent,
            EventName = data.EventName,
            EventDescription = data.EventDescription,
            EventMinutesRemaining = data.EventMinutesRemaining,
            ShowOwner = data.ShowOwner,
            AllowPets = data.AllowPets,
            DisplayRoomEntryAd = data.DisplayRoomEntryAd
        };
    }

    internal static RoomChatSettings Copy(RoomChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new RoomChatSettings
        {
            Flow = settings.Flow,
            BubbleWidth = settings.BubbleWidth,
            ScrollSpeed = settings.ScrollSpeed,
            TalkHearingDistance = settings.TalkHearingDistance,
            FloodProtection = settings.FloodProtection,
            ParsedLayout = settings.ParsedLayout
        };
    }

    internal static RoomResultDetails Copy(RoomResultDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return new RoomResultDetails
        {
            Forward = details.Forward,
            IsStaffPick = details.IsStaffPick,
            IsGroupMember = details.IsGroupMember,
            IsRoomMuted = details.IsRoomMuted,
            Moderation = new RoomModerationSettings
            {
                Mute = details.Moderation.Mute,
                Kick = details.Moderation.Kick,
                Ban = details.Moderation.Ban
            },
            CanMute = details.CanMute,
            Chat = Copy(details.Chat),
            ParsedLayout = details.ParsedLayout,
            OpeningConnection = details.OpeningConnection,
            UnityContextId = details.UnityContextId,
            UnityThumbnail = details.UnityThumbnail
        };
    }

    internal static ItemData Copy(ItemData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        ItemData snapshot = data switch
        {
            LegacyData => new LegacyData(),
            MapData map => Copy(map),
            StringArrayData array => Copy(array),
            VoteResultData vote => new VoteResultData { Result = vote.Result },
            EmptyItemData => new EmptyItemData(),
            IntArrayData array => Copy(array),
            HighScoreData scores => Copy(scores),
            CrackableFurniData crackable => new CrackableFurniData
            {
                Hits = crackable.Hits,
                Target = crackable.Target
            },
            _ => throw new NotSupportedException($"Unsupported item data type: {data.GetType().FullName}.")
        };

        snapshot.Flags = data.Flags;
        snapshot.UniqueSerialNumber = data.UniqueSerialNumber;
        snapshot.UniqueSeriesSize = data.UniqueSeriesSize;
        snapshot.UniqueLimitedData = data.UniqueLimitedData;
        snapshot.Value = data.Value;
        return snapshot;
    }

    private static MapData Copy(MapData data)
    {
        var snapshot = new MapData();
        foreach ((string key, string value) in data.Entries)
            snapshot.Entries[key] = value;
        return snapshot;
    }

    private static StringArrayData Copy(StringArrayData data)
    {
        var snapshot = new StringArrayData();
        snapshot.Values.AddRange(data.Values);
        return snapshot;
    }

    private static IntArrayData Copy(IntArrayData data)
    {
        var snapshot = new IntArrayData();
        snapshot.Values.AddRange(data.Values);
        return snapshot;
    }

    private static HighScoreData Copy(HighScoreData data)
    {
        var snapshot = new HighScoreData
        {
            ScoreType = data.ScoreType,
            ClearType = data.ClearType
        };
        snapshot.Scores.AddRange(data.Scores.Select(score => new HighScore
        {
            Score = score.Score,
            Names = [.. score.Names]
        }));
        return snapshot;
    }
}
