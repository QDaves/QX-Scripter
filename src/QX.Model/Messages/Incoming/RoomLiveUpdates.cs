using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record AvatarDanceUpdate(int Index, int Dance) : IParserComposer<AvatarDanceUpdate>
{
    public static AvatarDanceUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarDanceUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    private static AvatarDanceUpdate ParseUnity(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarDanceUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Dance);
    }

    private static void ComposeUnity(AvatarDanceUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Dance);
    }
}

public sealed record AvatarEffectUpdate(int Index, int Effect, int Delay) : IParserComposer<AvatarEffectUpdate>
{
    public static AvatarEffectUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarEffectUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    private static AvatarEffectUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarEffectUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Effect);
        p.WriteInt(value.Delay);
    }

    private static void ComposeUnity(AvatarEffectUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Effect);
        p.WriteInt(value.Delay);
    }
}

public sealed record AvatarCarryUpdate(int Index, int ItemType) : IParserComposer<AvatarCarryUpdate>
{
    public static AvatarCarryUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarCarryUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    private static AvatarCarryUpdate ParseUnity(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarCarryUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.ItemType);
    }

    private static void ComposeUnity(AvatarCarryUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.ItemType);
    }
}

public sealed record AvatarSleepUpdate(int Index, bool Sleeping) : IParserComposer<AvatarSleepUpdate>
{
    public static AvatarSleepUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarSleepUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadBool());

    private static AvatarSleepUpdate ParseUnity(in PacketReader p) => new(p.ReadInt(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarSleepUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.Sleeping);
    }

    private static void ComposeUnity(AvatarSleepUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.Sleeping);
    }
}

public sealed record AvatarTypingUpdate(int Index, int TypingState) : IParserComposer<AvatarTypingUpdate>
{
    public bool Typing => TypingState != 0;

    public static AvatarTypingUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarTypingUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    private static AvatarTypingUpdate ParseUnity(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarTypingUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.TypingState);
    }

    private static void ComposeUnity(AvatarTypingUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.TypingState);
    }
}

public sealed record AvatarAction(int Index, int Action) : IParserComposer<AvatarAction>
{
    public static AvatarAction Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarAction ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    private static AvatarAction ParseUnity(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarAction value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Action);
    }

    private static void ComposeUnity(AvatarAction value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Action);
    }
}

/// <summary>
/// Sent when a user in the room changes the group they display as their favourite.
/// </summary>
/// <param name="Index">The room index of the affected avatar.</param>
/// <param name="GroupId">
/// The group the avatar now displays. Both clients transmit this as a 32 bit value, unlike most
/// identifiers.
/// </param>
/// <param name="Status">
/// The membership status. <c>RoomUsersHandler.onFavoriteMembershipUpdate</c> forwards this on the
/// dispatched event only and never stores it on the avatar, so it is not mirrored onto
/// <see cref="User.GroupStatus"/>.
/// </param>
/// <param name="GroupName">The name of the group the avatar now displays.</param>
public sealed record FavoriteMembershipUpdate(int Index, int GroupId, int Status, string GroupName)
    : IParserComposer<FavoriteMembershipUpdate>
{
    public static FavoriteMembershipUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FavoriteMembershipUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    private static FavoriteMembershipUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FavoriteMembershipUpdate value, in PacketWriter p)
    {
        ValidateGroupName(value, in p);
        p.WriteInt(value.Index);
        p.WriteInt(value.GroupId);
        p.WriteInt(value.Status);
        p.WriteString(value.GroupName);
    }

    private static void ComposeUnity(FavoriteMembershipUpdate value, in PacketWriter p)
    {
        ValidateGroupName(value, in p);
        p.WriteInt(value.Index);
        p.WriteInt(value.GroupId);
        p.WriteInt(value.Status);
        p.WriteString(value.GroupName);
    }

    private static void ValidateGroupName(FavoriteMembershipUpdate value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.GroupName, nameof(GroupName));
        if (p.Encoding.GetByteCount(value.GroupName) > ushort.MaxValue)
            throw new ArgumentException("String exceeds the protocol limit.", nameof(GroupName));
    }
}

/// <summary>
/// The structured pet figure carried by <see cref="PetFigureUpdate"/>.
/// </summary>
/// <param name="TypeId">The pet type.</param>
/// <param name="PaletteId">The palette the pet is rendered with.</param>
/// <param name="Color">The pet's colour.</param>
/// <param name="BreedId">The pet's breed.</param>
/// <param name="CustomParts">
/// Custom part triples in the order the client reads them.
/// </param>
public sealed record PetFigureData(
    int TypeId,
    int PaletteId,
    string Color,
    int BreedId,
    IReadOnlyList<PetCustomPart> CustomParts) : IParserComposer<PetFigureData>
{
    /// <summary>
    /// The figure string the client builds from these fields before assigning it to the avatar:
    /// <c>"{TypeId} {PaletteId} {Color} {part count}"</c> followed by every custom part value.
    /// </summary>
    public string FigureString =>
        string.Join(
            ' ',
            new[]
            {
                TypeId.ToString(),
                PaletteId.ToString(),
                Color,
                CustomParts.Count.ToString()
            }.Concat(CustomParts.SelectMany(part => new[]
            {
                part.LayerId.ToString(),
                part.PartId.ToString(),
                part.PaletteId.ToString()
            })));

    public static PetFigureData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetFigureData ParseFlash(in PacketReader p)
    {
        int type_id = p.ReadInt();
        int palette_id = p.ReadInt();
        string color = p.ReadString();
        int breed_id = p.ReadInt();
        int count = p.ReadInt();
        var parts = new PetCustomPart[count];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = p.Parse<PetCustomPart>();
        return new PetFigureData(type_id, palette_id, color, breed_id, parts);
    }

    private static PetFigureData ParseUnity(in PacketReader p)
    {
        int type_id = p.ReadInt();
        int palette_id = p.ReadInt();
        string color = p.ReadString();
        int breed_id = p.ReadInt();
        int count = p.ReadLength();
        var parts = new PetCustomPart[count];
        for (int i = 0; i < parts.Length; i++)
            parts[i] = p.Parse<PetCustomPart>();
        return new PetFigureData(type_id, palette_id, color, breed_id, parts);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetFigureData value, in PacketWriter p)
    {
        p.WriteInt(value.TypeId);
        p.WriteInt(value.PaletteId);
        p.WriteString(value.Color);
        p.WriteInt(value.BreedId);
        p.WriteInt(value.CustomParts.Count);
        foreach (PetCustomPart part in value.CustomParts)
            p.Compose(part);
    }

    private static void ComposeUnity(PetFigureData value, in PacketWriter p)
    {
        p.WriteInt(value.TypeId);
        p.WriteInt(value.PaletteId);
        p.WriteString(value.Color);
        p.WriteInt(value.BreedId);
        p.WriteLength((Length)value.CustomParts.Count);
        foreach (PetCustomPart part in value.CustomParts)
            p.Compose(part);
    }
}

/// <summary>
/// Sent when a pet in the room changes figure, saddle or rider.
/// </summary>
/// <param name="Index">The room index of the pet.</param>
/// <param name="PetId">The pet's own identifier, carried for the dispatched event only.</param>
/// <param name="Figure">The pet's new figure.</param>
/// <param name="HasSaddle">Whether the pet is saddled.</param>
/// <param name="IsRiding">Whether the pet is currently being ridden.</param>
public sealed record PetFigureUpdate(
    int Index,
    Id PetId,
    PetFigureData Figure,
    bool HasSaddle,
    bool IsRiding) : IParserComposer<PetFigureUpdate>
{
    public static PetFigureUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetFigureUpdate ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadId(),
            p.Parse<PetFigureData>(),
            p.ReadBool(),
            p.ReadBool());

    private static PetFigureUpdate ParseUnity(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadId(),
            p.Parse<PetFigureData>(),
            p.ReadBool(),
            p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetFigureUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.Compose(value.Figure);
        p.WriteBool(value.HasSaddle);
        p.WriteBool(value.IsRiding);
    }

    private static void ComposeUnity(PetFigureUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.Compose(value.Figure);
        p.WriteBool(value.HasSaddle);
        p.WriteBool(value.IsRiding);
    }
}

/// <summary>
/// Sent when the breeding, harvesting or reviving state of a pet in the room changes.
/// </summary>
/// <param name="Index">The room index of the pet.</param>
/// <param name="PetId">The pet's own identifier, carried for the dispatched event only.</param>
/// <param name="CanBreed">Whether the pet can be bred.</param>
/// <param name="CanHarvest">Whether the pet can be harvested.</param>
/// <param name="CanRevive">Whether the pet can be revived.</param>
/// <param name="HasBreedingPermission">Whether the local user may breed this pet.</param>
public sealed record PetStatusUpdate(
    int Index,
    Id PetId,
    bool CanBreed,
    bool CanHarvest,
    bool CanRevive,
    bool HasBreedingPermission) : IParserComposer<PetStatusUpdate>
{
    public static PetStatusUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetStatusUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadBool(), p.ReadBool(), p.ReadBool(), p.ReadBool());

    private static PetStatusUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadBool(), p.ReadBool(), p.ReadBool(), p.ReadBool());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetStatusUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteBool(value.CanBreed);
        p.WriteBool(value.CanHarvest);
        p.WriteBool(value.CanRevive);
        p.WriteBool(value.HasBreedingPermission);
    }

    private static void ComposeUnity(PetStatusUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteBool(value.CanBreed);
        p.WriteBool(value.CanHarvest);
        p.WriteBool(value.CanRevive);
        p.WriteBool(value.HasBreedingPermission);
    }
}

/// <summary>
/// Sent when a pet in the room gains a level.
/// </summary>
/// <param name="Index">The room index of the pet.</param>
/// <param name="PetId">The pet's own identifier, carried for the dispatched event only.</param>
/// <param name="Level">The pet's new level.</param>
public sealed record PetLevelUpdate(int Index, Id PetId, int Level)
    : IParserComposer<PetLevelUpdate>
{
    public static PetLevelUpdate Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetLevelUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadInt());

    private static PetLevelUpdate ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetLevelUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteInt(value.Level);
    }

    private static void ComposeUnity(PetLevelUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteInt(value.Level);
    }
}

public sealed record UserChanged(
    int Index,
    string Figure,
    string Gender,
    string Motto,
    int AchievementScore,
    string GroupBadge,
    IReadOnlyList<int> GroupPayload,
    int BadgesRank = -1) : IParserComposer<UserChanged>
{
    public static UserChanged Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserChanged ParseFlash(in PacketReader p)
    {
        int index = p.ReadInt();
        string figure = p.ReadString();
        string gender = p.ReadString();
        string motto = p.ReadString();
        int achievement_score = p.ReadInt();
        string group_badge = p.ReadString();

        int count = p.ReadInt();
        var payload = new int[checked(count * 3)];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = p.ReadInt();

        int badges_rank = p.ReadInt();
        return new UserChanged(index, figure, gender, motto, achievement_score, group_badge, payload, badges_rank);
    }

    private static UserChanged ParseUnity(in PacketReader p)
    {
        int index = p.ReadInt();
        string figure = p.ReadString();
        string gender = p.ReadString();
        string motto = p.ReadString();
        int achievement_score = p.ReadInt();
        string group_badge = p.ReadString();

        int count = p.ReadLength();
        var payload = new int[checked(count * 3)];
        for (int i = 0; i < payload.Length; i++)
            payload[i] = p.ReadInt();

        int badges_rank = UnityHasBadgeRank(in p) ? p.ReadInt() : -1;
        return new UserChanged(index, figure, gender, motto, achievement_score, group_badge, payload, badges_rank);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserChanged value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
        p.WriteString(value.Motto);
        p.WriteInt(value.AchievementScore);
        p.WriteString(value.GroupBadge);

        if (value.GroupPayload.Count % 3 != 0)
            throw new InvalidOperationException("The group payload must contain complete groups of three integers.");

        p.WriteInt(value.GroupPayload.Count / 3);
        foreach (int entry in value.GroupPayload)
            p.WriteInt(entry);

        p.WriteInt(value.BadgesRank);
    }

    private static void ComposeUnity(UserChanged value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteString(value.Figure);
        p.WriteString(value.Gender);
        p.WriteString(value.Motto);
        p.WriteInt(value.AchievementScore);
        p.WriteString(value.GroupBadge);

        if (value.GroupPayload.Count % 3 != 0)
            throw new InvalidOperationException("The group payload must contain complete groups of three integers.");

        p.WriteLength((Length)(value.GroupPayload.Count / 3));
        foreach (int entry in value.GroupPayload)
            p.WriteInt(entry);

        if (UnityHasBadgeRank(in p))
            p.WriteInt(value.BadgesRank);
    }

    private static bool UnityHasBadgeRank(in PacketReader p) =>
        p.Context?.WireProfile.RequireUnityUpdateAvatarBadgeRank() ??
        throw new NotSupportedException("The active Unity session has no compatible avatar update wire layout.");

    private static bool UnityHasBadgeRank(in PacketWriter p) =>
        p.Context?.WireProfile.RequireUnityUpdateAvatarBadgeRank() ??
        throw new NotSupportedException("The active Unity session has no compatible avatar update wire layout.");

}
