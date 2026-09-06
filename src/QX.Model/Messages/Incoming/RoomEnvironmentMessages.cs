using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record RoomEntryTile(int X, int Y, int Direction) : IParserComposer<RoomEntryTile>
{
    public static RoomEntryTile Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomEntryTile ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    private static RoomEntryTile ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomEntryTile value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
    }

    private static void ComposeUnity(RoomEntryTile value, in PacketWriter p)
    {
        p.WriteInt(value.X);
        p.WriteInt(value.Y);
        p.WriteInt(value.Direction);
    }
}

public sealed record FlatProperty(string Key, string Value) : IParserComposer<FlatProperty>
{
    public static FlatProperty Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FlatProperty ParseFlash(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    private static FlatProperty ParseUnity(in PacketReader p) =>
        new(p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FlatProperty value, in PacketWriter p)
    {
        p.WriteString(value.Key);
        p.WriteString(value.Value);
    }

    private static void ComposeUnity(FlatProperty value, in PacketWriter p)
    {
        p.WriteString(value.Key);
        p.WriteString(value.Value);
    }
}

public sealed record RoomVisualizationSettings(
    bool WallsHidden,
    RoomThickness WallThickness,
    RoomThickness FloorThickness) : IParserComposer<RoomVisualizationSettings>
{
    public float WallThicknessMultiplier => ThicknessMultiplier(WallThickness);
    public float FloorThicknessMultiplier => ThicknessMultiplier(FloorThickness);

    public static RoomVisualizationSettings Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomVisualizationSettings ParseFlash(in PacketReader p) =>
        new(p.ReadBool(), (RoomThickness)p.ReadInt(), (RoomThickness)p.ReadInt());

    private static RoomVisualizationSettings ParseUnity(in PacketReader p) =>
        new(p.ReadBool(), (RoomThickness)p.ReadInt(), (RoomThickness)p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomVisualizationSettings value, in PacketWriter p)
    {
        p.WriteBool(value.WallsHidden);
        p.WriteInt((int)value.WallThickness);
        p.WriteInt((int)value.FloorThickness);
    }

    private static void ComposeUnity(RoomVisualizationSettings value, in PacketWriter p)
    {
        p.WriteBool(value.WallsHidden);
        p.WriteInt((int)value.WallThickness);
        p.WriteInt((int)value.FloorThickness);
    }

    private static float ThicknessMultiplier(RoomThickness value) =>
        MathF.Pow(2, Math.Clamp((int)value, -2, 1));
}

public sealed record YouAreController(Id RoomId, int RightsLevel)
    : IParserComposer<YouAreController>
{
    public static YouAreController Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static YouAreController ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    private static YouAreController ParseUnity(in PacketReader p) =>
        new(p.ReadId(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(YouAreController value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteInt(value.RightsLevel);
    }

    private static void ComposeUnity(YouAreController value, in PacketWriter p)
    {
        p.WriteId(value.RoomId);
        p.WriteInt(value.RightsLevel);
    }
}

public sealed record YouAreNotController(Id RoomId) : IParserComposer<YouAreNotController>
{
    public static YouAreNotController Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static YouAreNotController ParseFlash(in PacketReader p) => new(p.ReadId());

    private static YouAreNotController ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(YouAreNotController value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(YouAreNotController value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

public sealed record YouAreOwner(Id RoomId) : IParserComposer<YouAreOwner>
{
    public static YouAreOwner Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static YouAreOwner ParseFlash(in PacketReader p) => new(p.ReadId());

    private static YouAreOwner ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(YouAreOwner value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(YouAreOwner value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}

public sealed record YouAreSpectator(Id RoomId) : IParserComposer<YouAreSpectator>
{
    public static YouAreSpectator Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static YouAreSpectator ParseFlash(in PacketReader p) => new(p.ReadId());

    private static YouAreSpectator ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(YouAreSpectator value, in PacketWriter p) =>
        p.WriteId(value.RoomId);

    private static void ComposeUnity(YouAreSpectator value, in PacketWriter p) =>
        p.WriteId(value.RoomId);
}
