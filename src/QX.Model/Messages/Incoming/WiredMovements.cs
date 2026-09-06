using Qx;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum WiredMovementType
{
    Avatar = 0,
    FloorItem = 1,
    WallItem = 2,
    AvatarDirection = 3
}

public abstract class WiredMovement(WiredMovementType type) : IParserComposer<WiredMovement>
{
    public WiredMovementType Type { get; } = type;
    public int AnimationTime { get; set; }

    public virtual void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMovement value, in PacketWriter p) =>
        p.WriteInt((int)value.Type);

    public static WiredMovement Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

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

public sealed class AvatarWiredMovement : WiredMovement
{
    public Tile Source { get; set; }
    public Tile Destination { get; set; }
    public int AvatarIndex { get; set; }
    public bool IsSlide { get; set; }
    public int BodyDirection { get; set; }
    public int HeadDirection { get; set; }
    public bool HasJump { get; set; }
    public int JumpPower { get; set; }

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

public sealed class FloorItemWiredMovement : WiredMovement
{
    public Tile Source { get; set; }
    public Tile Destination { get; set; }
    public Id ItemId { get; set; }
    public int Rotation { get; set; }
    public bool HasOvershoot { get; set; }
    public int OvershootDistance { get; set; }
    public bool HasCurve { get; set; }
    public int CurveStrength { get; set; }

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

public sealed class WallItemWiredMovement : WiredMovement
{
    public Id ItemId { get; set; }
    public WallLocation Source { get; set; }
    public WallLocation Destination { get; set; }

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

public sealed class AvatarDirectionWiredMovement : WiredMovement
{
    public int AvatarIndex { get; set; }
    public int BodyDirection { get; set; }
    public int HeadDirection { get; set; }

    public AvatarDirectionWiredMovement() : base(WiredMovementType.AvatarDirection) { }

    internal AvatarDirectionWiredMovement(in PacketReader p) : this()
    {
        AvatarIndex = p.ReadInt();
        BodyDirection = p.ReadInt();
        HeadDirection = p.ReadInt();
    }

    public override void Compose(in PacketWriter p)
    {
        base.Compose(p);
        p.WriteInt(AvatarIndex);
        p.WriteInt(BodyDirection);
        p.WriteInt(HeadDirection);
    }
}

public sealed record WiredMovements(IReadOnlyList<WiredMovement> Movements) : IParserComposer<WiredMovements>
{
    public static WiredMovements Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static WiredMovements ParseFlash(in PacketReader p)
    {
        int count = p.ReadInt();
        var movements = new WiredMovement[count];
        for (int i = 0; i < count; i++)
            movements[i] = p.Parse<WiredMovement>();
        return new WiredMovements(movements);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(WiredMovements value, in PacketWriter p)
    {
        p.WriteInt(value.Movements.Count);
        foreach (WiredMovement movement in value.Movements)
            p.Compose(movement);
    }
}
