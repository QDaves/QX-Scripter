using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>Dance</c> message, received when an avatar in the room starts or stops dancing.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="Dance">The dance the avatar now performs, 0 when it stopped dancing. The values match <see cref="Dances"/>.</param>
public sealed record AvatarDanceUpdate(int Index, int Dance) : IParserComposer<AvatarDanceUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarDanceUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarDanceUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarDanceUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Dance);
    }
}

/// <summary>Represents the <c>AvatarEffect</c> message, received when the effect of an avatar in the room changes.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="Effect">The effect the avatar now has, 0 for none.</param>
/// <param name="Delay">The delay before the effect is shown, as sent by the server.</param>
public sealed record AvatarEffectUpdate(int Index, int Effect, int Delay) : IParserComposer<AvatarEffectUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarEffectUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarEffectUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarEffectUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Effect);
        p.WriteInt(value.Delay);
    }
}

/// <summary>Represents the <c>CarryObject</c> message, received when the hand item of an avatar in the room changes.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="ItemType">The hand item type the avatar now carries, 0 for none.</param>
public sealed record AvatarCarryUpdate(int Index, int ItemType) : IParserComposer<AvatarCarryUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarCarryUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarCarryUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarCarryUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.ItemType);
    }
}

/// <summary>Represents the <c>Sleep</c> message, received when an avatar in the room goes idle or becomes active again.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="Sleeping">Whether the avatar is idle.</param>
public sealed record AvatarSleepUpdate(int Index, bool Sleeping) : IParserComposer<AvatarSleepUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarSleepUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarSleepUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarSleepUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteBool(value.Sleeping);
    }
}

/// <summary>Represents the <c>UserTyping</c> message, received when an avatar in the room starts or stops typing.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="TypingState">The raw typing state, 0 when the avatar is not typing.</param>
public sealed record AvatarTypingUpdate(int Index, int TypingState) : IParserComposer<AvatarTypingUpdate>
{
    /// <summary>Gets whether the avatar is typing, which is the case when <see cref="TypingState"/> is not 0.</summary>
    public bool Typing => TypingState != 0;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarTypingUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarTypingUpdate ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarTypingUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.TypingState);
    }
}

/// <summary>Represents the <c>Expression</c> message, received when an avatar in the room performs an expression such as a wave.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="Action">The expression ID.</param>
public sealed record AvatarAction(int Index, int Action) : IParserComposer<AvatarAction>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static AvatarAction Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AvatarAction ParseFlash(in PacketReader p) => new(p.ReadInt(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AvatarAction value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteInt(value.Action);
    }
}

/// <summary>Represents the structured pet figure carried by <see cref="PetFigureUpdate"/>.</summary>
/// <param name="TypeId">The pet type.</param>
/// <param name="PaletteId">The palette the pet is rendered with.</param>
/// <param name="Color">The pet's color.</param>
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
    /// <summary>Gets the figure string the client builds from these fields before assigning it to the avatar.</summary>
    /// <remarks>
    /// The format is <c>"{TypeId} {PaletteId} {Color} {part count}"</c> followed by the layer, part and
    /// palette of every custom part, all separated by spaces.
    /// </remarks>
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

    /// <summary>Parses the figure from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PetFigureData Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the figure into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}

/// <summary>Represents the <c>PetFigureUpdate</c> message, received when a pet in the room changes figure, saddle or rider.</summary>
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
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PetFigureUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetFigureUpdate ParseFlash(in PacketReader p) =>
        new(
            p.ReadInt(),
            p.ReadId(),
            p.Parse<PetFigureData>(),
            p.ReadBool(),
            p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetFigureUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.Compose(value.Figure);
        p.WriteBool(value.HasSaddle);
        p.WriteBool(value.IsRiding);
    }
}

/// <summary>Represents the <c>PetStatusUpdate</c> message, received when the breeding, harvesting or reviving state of a pet in the room changes.</summary>
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
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PetStatusUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetStatusUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadBool(), p.ReadBool(), p.ReadBool(), p.ReadBool());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetStatusUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteBool(value.CanBreed);
        p.WriteBool(value.CanHarvest);
        p.WriteBool(value.CanRevive);
        p.WriteBool(value.HasBreedingPermission);
    }
}

/// <summary>Represents the <c>PetLevelUpdate</c> message, received when a pet in the room gains a level.</summary>
/// <param name="Index">The room index of the pet.</param>
/// <param name="PetId">The pet's own identifier, carried for the dispatched event only.</param>
/// <param name="Level">The pet's new level.</param>
public sealed record PetLevelUpdate(int Index, Id PetId, int Level)
    : IParserComposer<PetLevelUpdate>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static PetLevelUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetLevelUpdate ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadId(), p.ReadInt());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetLevelUpdate value, in PacketWriter p)
    {
        p.WriteInt(value.Index);
        p.WriteId(value.PetId);
        p.WriteInt(value.Level);
    }
}

/// <summary>Represents the <c>UserChange</c> message, received when the figure, gender or motto of an avatar in the room changes.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="Figure">The avatar's figure string.</param>
/// <param name="Gender">The avatar's gender.</param>
/// <param name="Motto">The avatar's motto.</param>
/// <param name="AchievementScore">The user's achievement score.</param>
/// <param name="GroupBadge">The favorite group's badge code.</param>
/// <param name="GroupPayload">
/// The favorite group's badge parts as a flat list of integers, three per part. Composing throws
/// <see cref="InvalidOperationException"/> when the count is not a multiple of three.
/// </param>
/// <param name="BadgesRank">The user's badge rank, or -1 when none was sent.</param>
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
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static UserChanged Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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

}
