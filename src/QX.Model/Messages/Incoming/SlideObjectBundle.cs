using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a floor item moved by a roller.</summary>
/// <param name="Id">The ID of the floor item.</param>
/// <param name="FromZ">The item's height before the move.</param>
/// <param name="ToZ">The item's height after the move.</param>
public readonly record struct SlideObject(Id Id, float FromZ, float ToZ);

/// <summary>Represents an avatar moved by a roller.</summary>
/// <param name="Index">The room index of the avatar.</param>
/// <param name="FromZ">The avatar's height before the move.</param>
/// <param name="ToZ">The avatar's height after the move.</param>
public sealed record SlideAvatar(Id Index, float FromZ, float ToZ);

/// <summary>Specifies whether a roller move carries an avatar and how it moves.</summary>
public enum AvatarSlideType
{
    /// <summary>No avatar moves.</summary>
    None = 0,
    /// <summary>A walking avatar moves.</summary>
    WalkingAvatar = 1,
    /// <summary>A standing avatar moves.</summary>
    StandingAvatar = 2
}

/// <summary>Represents the <c>SlideObjectBundle</c> message, received when a roller moves floor items and an avatar to the next tile.</summary>
/// <param name="From">The tile the objects move from.</param>
/// <param name="To">The tile the objects move to.</param>
/// <param name="Objects">The floor items that move.</param>
/// <param name="RollerId">The ID of the roller that moves the objects.</param>
/// <param name="Type">The kind of avatar move, <see cref="AvatarSlideType.None"/> when no avatar moves.</param>
/// <param name="Avatar">The avatar that moves, or <see langword="null"/> when the type is <see cref="AvatarSlideType.None"/>.</param>
public sealed record SlideObjectBundle(
    Point From,
    Point To,
    IReadOnlyList<SlideObject> Objects,
    Id RollerId,
    AvatarSlideType Type,
    SlideAvatar? Avatar) : IParserComposer<SlideObjectBundle>
{
    /// <summary>Gets whether the packet carries the avatar part after the roller ID.</summary>
    /// <remarks>
    /// The parser sets this to <see langword="false"/> when the packet ends after the roller ID. Composing
    /// then leaves the avatar part out, and throws <see cref="InvalidDataException"/> if
    /// <see cref="SlideObjectBundle.Type"/> is not <see cref="AvatarSlideType.None"/> or
    /// <see cref="SlideObjectBundle.Avatar"/> is set.
    /// </remarks>
    public bool HasAvatarSlideData { get; init; } = true;
    /// <summary>Gets the avatar move type as its integer value.</summary>
    public int MoveType => (int)Type;

    /// <summary>Initializes a new instance of the <see cref="SlideObjectBundle"/> class with the avatar move type as an integer.</summary>
    /// <param name="From">The tile the objects move from.</param>
    /// <param name="To">The tile the objects move to.</param>
    /// <param name="Objects">The floor items that move.</param>
    /// <param name="RollerId">The ID of the roller that moves the objects.</param>
    /// <param name="moveType">The avatar move type as its <see cref="AvatarSlideType"/> integer value.</param>
    /// <param name="Avatar">The avatar that moves, or <see langword="null"/> when no avatar moves.</param>
    public SlideObjectBundle(
        Point From,
        Point To,
        IReadOnlyList<SlideObject> Objects,
        Id RollerId,
        int moveType,
        SlideAvatar? Avatar)
        : this(From, To, Objects, RollerId, (AvatarSlideType)moveType, Avatar)
    {
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static SlideObjectBundle Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SlideObjectBundle ParseFlash(in PacketReader p)
    {
        var from = new Point(p.ReadInt(), p.ReadInt());
        var to = new Point(p.ReadInt(), p.ReadInt());

        int count = p.ReadLength();
        var objects = new SlideObject[count];
        for (int i = 0; i < count; i++)
            objects[i] = new SlideObject(p.ReadId(), p.ReadFloat(), p.ReadFloat());

        Id roller_id = p.ReadId();

        var type = AvatarSlideType.None;
        SlideAvatar? avatar = null;
        bool has_avatar_slide_data = p.Available > 0;
        if (p.Available > 0)
        {
            type = (AvatarSlideType)p.ReadInt();
            if (type is AvatarSlideType.WalkingAvatar or AvatarSlideType.StandingAvatar)
                avatar = new SlideAvatar(p.ReadId(), p.ReadFloat(), p.ReadFloat());
        }

        return new SlideObjectBundle(from, to, objects, roller_id, type, avatar)
        {
            HasAvatarSlideData = has_avatar_slide_data
        };
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SlideObjectBundle value, in PacketWriter p)
    {
        Validate(value);

        p.WriteInt(value.From.X);
        p.WriteInt(value.From.Y);
        p.WriteInt(value.To.X);
        p.WriteInt(value.To.Y);

        p.WriteLength((Length)value.Objects.Count);
        foreach (SlideObject slide in value.Objects)
        {
            p.WriteId(slide.Id);
            p.WriteFloat(slide.FromZ);
            p.WriteFloat(slide.ToZ);
        }

        p.WriteId(value.RollerId);
        if (value.HasAvatarSlideData)
        {
            p.WriteInt((int)value.Type);
            if (value.Avatar is { } avatar)
            {
                p.WriteId(avatar.Index);
                p.WriteFloat(avatar.FromZ);
                p.WriteFloat(avatar.ToZ);
            }
        }
    }

    private static void Validate(SlideObjectBundle value)
    {
        bool requires_avatar = value.Type is AvatarSlideType.WalkingAvatar or AvatarSlideType.StandingAvatar;
        if (!value.HasAvatarSlideData && (value.Type is not AvatarSlideType.None || value.Avatar is not null))
            throw new InvalidDataException("A slide bundle without an avatar tail cannot contain avatar slide data.");
        if (value.HasAvatarSlideData && requires_avatar != (value.Avatar is not null))
            throw new InvalidDataException("The avatar slide type and avatar payload must agree.");
    }
}
