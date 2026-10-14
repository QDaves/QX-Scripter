using Qx;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Specifies the kind of a wired movement.</summary>
public enum WiredMovementType
{
    /// <summary>An avatar moves to another tile.</summary>
    Avatar = 0,
    /// <summary>A floor item moves to another tile.</summary>
    FloorItem = 1,
    /// <summary>A wall item moves to another wall location.</summary>
    WallItem = 2,
    /// <summary>An avatar turns without moving.</summary>
    AvatarDirection = 3
}

/// <summary>Represents one movement of an avatar or item caused by wired.</summary>
/// <remarks>
/// On the wire each movement starts with its <see cref="WiredMovementType"/>, which decides the
/// derived type that <see cref="Parse"/> returns.
/// </remarks>
/// <param name="type">The kind of the movement.</param>
public abstract class WiredMovement(WiredMovementType type) : IParserComposer<WiredMovement>
{
    /// <summary>Gets the kind of the movement.</summary>
    public WiredMovementType Type { get; } = type;
    /// <summary>Gets or sets the time the client animates the movement, in milliseconds.</summary>
    /// <remarks>Not sent for <see cref="AvatarDirectionWiredMovement"/>, where it stays 0.</remarks>
    public int AnimationTime { get; set; }

    /// <summary>Composes the movement type into a packet.</summary>
    /// <remarks>Derived types override this to write their own fields after the type.</remarks>
    /// <param name="p">The packet writer.</param>
    public virtual void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMovement value, in PacketWriter p) =>
        p.WriteInt((int)value.Type);

    /// <summary>Parses a movement from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <returns>An <see cref="AvatarWiredMovement"/>, <see cref="FloorItemWiredMovement"/>, <see cref="WallItemWiredMovement"/> or <see cref="AvatarDirectionWiredMovement"/>, depending on the movement type.</returns>
    /// <exception cref="Exception">Thrown when the movement type is unknown.</exception>
    public static WiredMovement Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredMovement ParseFlash(in PacketReader p)
    {
        var type = (WiredMovementType)p.ReadInt();
        return type switch
        {
            WiredMovementType.Avatar => new AvatarWiredMovement(in p),
            WiredMovementType.FloorItem => new FloorItemWiredMovement(in p),
            WiredMovementType.WallItem => new WallItemWiredMovement(in p),
            WiredMovementType.AvatarDirection => new AvatarDirectionWiredMovement(in p),
            _ => throw new Exception($"Unknown wired movement type: {type}.")
        };
    }
}

/// <summary>Represents an avatar moved to another tile by wired.</summary>
public sealed class AvatarWiredMovement : WiredMovement
{
    /// <summary>Gets or sets the tile the avatar moves from.</summary>
    public Tile Source { get; set; }
    /// <summary>Gets or sets the tile the avatar moves to.</summary>
    public Tile Destination { get; set; }
    /// <summary>Gets or sets the room index of the avatar.</summary>
    public int AvatarIndex { get; set; }
    /// <summary>Gets or sets whether the avatar slides rather than walks, sent as an integer where any value other than 0 reads as <see langword="true"/>.</summary>
    public bool IsSlide { get; set; }
    /// <summary>Gets or sets the avatar's body direction after the move.</summary>
    public int BodyDirection { get; set; }
    /// <summary>Gets or sets the avatar's head direction after the move.</summary>
    public int HeadDirection { get; set; }
    /// <summary>Gets or sets whether the move is a jump, in which case <see cref="JumpPower"/> is sent.</summary>
    public bool HasJump { get; set; }
    /// <summary>Gets or sets the jump power, 0 when <see cref="HasJump"/> is <see langword="false"/>.</summary>
    public int JumpPower { get; set; }

    /// <summary>Initializes a new instance of the <see cref="AvatarWiredMovement"/> class.</summary>
    public AvatarWiredMovement() : base(WiredMovementType.Avatar) { }

    internal AvatarWiredMovement(in PacketReader p) : this()
    {
        int srcX = p.ReadInt(), srcY = p.ReadInt(), dstX = p.ReadInt(), dstY = p.ReadInt();
        float srcZ = p.ReadFloat(), dstZ = p.ReadFloat();
        Source = new Tile(srcX, srcY, srcZ);
        Destination = new Tile(dstX, dstY, dstZ);
        AvatarIndex = p.ReadInt();
        IsSlide = p.ReadInt() != 0;
        AnimationTime = p.ReadInt();
        BodyDirection = p.ReadInt();
        HeadDirection = p.ReadInt();
        HasJump = p.ReadBool();
        if (HasJump)
            JumpPower = p.ReadInt();
    }

    /// <summary>Composes the movement into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public override void Compose(in PacketWriter p)
    {
        base.Compose(p);
        p.WriteInt(Source.X);
        p.WriteInt(Source.Y);
        p.WriteInt(Destination.X);
        p.WriteInt(Destination.Y);
        p.WriteFloat(Source.Z);
        p.WriteFloat(Destination.Z);
        p.WriteInt(AvatarIndex);
        p.WriteInt(IsSlide ? 1 : 0);
        p.WriteInt(AnimationTime);
        p.WriteInt(BodyDirection);
        p.WriteInt(HeadDirection);
        p.WriteBool(HasJump);
        if (HasJump)
            p.WriteInt(JumpPower);
    }
}

/// <summary>Represents a floor item moved to another tile by wired.</summary>
public sealed class FloorItemWiredMovement : WiredMovement
{
    /// <summary>Gets or sets the tile the item moves from.</summary>
    public Tile Source { get; set; }
    /// <summary>Gets or sets the tile the item moves to.</summary>
    public Tile Destination { get; set; }
    /// <summary>Gets or sets the ID of the floor item.</summary>
    public Id ItemId { get; set; }
    /// <summary>Gets or sets the item's direction after the move.</summary>
    public int Rotation { get; set; }
    /// <summary>Gets or sets whether the move overshoots, in which case <see cref="OvershootDistance"/> is sent.</summary>
    public bool HasOvershoot { get; set; }
    /// <summary>Gets or sets the overshoot distance, 0 when <see cref="HasOvershoot"/> is <see langword="false"/>.</summary>
    public int OvershootDistance { get; set; }
    public int OvershootAnimationTime
    {
        get => OvershootDistance;
        set => OvershootDistance = value;
    }
    /// <summary>Gets or sets whether the move follows a curve, in which case <see cref="CurveStrength"/> is sent.</summary>
    public bool HasCurve { get; set; }
    /// <summary>Gets or sets the curve strength, 0 when <see cref="HasCurve"/> is <see langword="false"/>.</summary>
    public int CurveStrength { get; set; }

    /// <summary>Initializes a new instance of the <see cref="FloorItemWiredMovement"/> class.</summary>
    public FloorItemWiredMovement() : base(WiredMovementType.FloorItem) { }

    internal FloorItemWiredMovement(in PacketReader p) : this()
    {
        int srcX = p.ReadInt(), srcY = p.ReadInt(), dstX = p.ReadInt(), dstY = p.ReadInt();
        float srcZ = p.ReadFloat(), dstZ = p.ReadFloat();
        Source = new Tile(srcX, srcY, srcZ);
        Destination = new Tile(dstX, dstY, dstZ);
        ItemId = p.ReadId();
        AnimationTime = p.ReadInt();
        Rotation = p.ReadInt();
        HasOvershoot = p.ReadBool();
        if (HasOvershoot)
            OvershootDistance = p.ReadInt();
        HasCurve = p.ReadBool();
        if (HasCurve)
            CurveStrength = p.ReadInt();
    }

    /// <summary>Composes the movement into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public override void Compose(in PacketWriter p)
    {
        base.Compose(p);
        p.WriteInt(Source.X);
        p.WriteInt(Source.Y);
        p.WriteInt(Destination.X);
        p.WriteInt(Destination.Y);
        p.WriteFloat(Source.Z);
        p.WriteFloat(Destination.Z);
        p.WriteId(ItemId);
        p.WriteInt(AnimationTime);
        p.WriteInt(Rotation);
        p.WriteBool(HasOvershoot);
        if (HasOvershoot)
            p.WriteInt(OvershootDistance);
        p.WriteBool(HasCurve);
        if (HasCurve)
            p.WriteInt(CurveStrength);
    }
}

/// <summary>Represents a wall item moved to another wall location by wired.</summary>
/// <remarks>
/// The packet carries one orientation flag for both locations, so a parsed movement has the same
/// orientation on both, and composing writes the orientation of <see cref="Destination"/>.
/// </remarks>
public sealed class WallItemWiredMovement : WiredMovement
{
    /// <summary>Gets or sets the ID of the wall item.</summary>
    public Id ItemId { get; set; }
    /// <summary>Gets or sets the wall location the item moves from.</summary>
    public WallLocation Source { get; set; }
    /// <summary>Gets or sets the wall location the item moves to.</summary>
    public WallLocation Destination { get; set; }

    /// <summary>Initializes a new instance of the <see cref="WallItemWiredMovement"/> class.</summary>
    public WallItemWiredMovement() : base(WiredMovementType.WallItem) { }

    internal WallItemWiredMovement(in PacketReader p) : this()
    {
        ItemId = p.ReadId();
        WallOrientation orientation = p.ReadBool() ? WallOrientation.Right : WallOrientation.Left;
        int srcWX = p.ReadInt(), srcWY = p.ReadInt(), srcLX = p.ReadInt(), srcLY = p.ReadInt();
        int dstWX = p.ReadInt(), dstWY = p.ReadInt(), dstLX = p.ReadInt(), dstLY = p.ReadInt();
        Source = new WallLocation(srcWX, srcWY, srcLX, srcLY, orientation);
        Destination = new WallLocation(dstWX, dstWY, dstLX, dstLY, orientation);
        AnimationTime = p.ReadInt();
    }

    /// <summary>Composes the movement into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public override void Compose(in PacketWriter p)
    {
        base.Compose(p);
        p.WriteId(ItemId);
        p.WriteBool(Destination.Orientation.IsRight);
        p.WriteInt(Source.Wall.X);
        p.WriteInt(Source.Wall.Y);
        p.WriteInt(Source.Offset.X);
        p.WriteInt(Source.Offset.Y);
        p.WriteInt(Destination.Wall.X);
        p.WriteInt(Destination.Wall.Y);
        p.WriteInt(Destination.Offset.X);
        p.WriteInt(Destination.Offset.Y);
        p.WriteInt(AnimationTime);
    }
}

/// <summary>Represents an avatar turned by wired without moving.</summary>
public sealed class AvatarDirectionWiredMovement : WiredMovement
{
    /// <summary>Gets or sets the room index of the avatar.</summary>
    public int AvatarIndex { get; set; }
    /// <summary>Gets or sets the avatar's new body direction.</summary>
    public int BodyDirection { get; set; }
    /// <summary>Gets or sets the avatar's new head direction.</summary>
    public int HeadDirection { get; set; }

    /// <summary>Initializes a new instance of the <see cref="AvatarDirectionWiredMovement"/> class.</summary>
    public AvatarDirectionWiredMovement() : base(WiredMovementType.AvatarDirection) { }

    internal AvatarDirectionWiredMovement(in PacketReader p) : this()
    {
        AvatarIndex = p.ReadInt();
        BodyDirection = p.ReadInt();
        HeadDirection = p.ReadInt();
    }

    /// <summary>Composes the movement into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public override void Compose(in PacketWriter p)
    {
        base.Compose(p);
        p.WriteInt(AvatarIndex);
        p.WriteInt(BodyDirection);
        p.WriteInt(HeadDirection);
    }
}

/// <summary>Represents the <c>WiredMovements</c> message, received when wired moves or turns avatars and items in the room.</summary>
/// <param name="Movements">The movements, each one of the <see cref="WiredMovement"/> subclasses.</param>
public sealed record WiredMovements(IReadOnlyList<WiredMovement> Movements) : IParserComposer<WiredMovements>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static WiredMovements Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static WiredMovements ParseFlash(in PacketReader p)
    {
        int count = p.ReadInt();
        var movements = new WiredMovement[count];
        for (int i = 0; i < count; i++)
            movements[i] = p.Parse<WiredMovement>();
        return new WiredMovements(movements);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMovements value, in PacketWriter p)
    {
        p.WriteInt(value.Movements.Count);
        foreach (WiredMovement movement in value.Movements)
            p.Compose(movement);
    }
}
