using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public readonly record struct SlideObject(Id Id, float FromZ, float ToZ);

/// <summary>
/// Represents an avatar being moved by a roller.
/// </summary>
/// <param name="Index">
/// The room index of the avatar. The wire width follows the client's identifier width:
/// four bytes on Flash, eight bytes on Unity, where the client stores it in the same
/// identifier slot it uses for the sliding floor items.
/// </param>
/// <param name="FromZ">The avatar's height before the slide.</param>
/// <param name="ToZ">The avatar's height after the slide.</param>
public sealed record SlideAvatar(Id Index, float FromZ, float ToZ);

public enum AvatarSlideType
{
    None = 0,
    WalkingAvatar = 1,
    StandingAvatar = 2
}

public sealed record SlideObjectBundle(
    Point From,
    Point To,
    IReadOnlyList<SlideObject> Objects,
    Id RollerId,
    AvatarSlideType Type,
    SlideAvatar? Avatar) : IParserComposer<SlideObjectBundle>
{
    public bool HasAvatarSlideData { get; init; } = true;
    public int MoveType => (int)Type;

    public SlideObjectBundle(
        Point From,
        Point To,
        IReadOnlyList<SlideObject> Objects,
        Id RollerId,
        int MoveType,
        SlideAvatar? Avatar)
        : this(From, To, Objects, RollerId, (AvatarSlideType)MoveType, Avatar)
    {
    }

    public static SlideObjectBundle Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static SlideObjectBundle ParseUnity(in PacketReader p)
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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(SlideObjectBundle value, in PacketWriter p)
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
