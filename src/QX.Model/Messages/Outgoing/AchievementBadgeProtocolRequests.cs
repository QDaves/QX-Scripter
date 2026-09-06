using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record AchievementsRequest : IParserComposer<AchievementsRequest>
{
    public static AchievementsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AchievementsRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    private static AchievementsRequest ParseUnity(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AchievementsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void ComposeUnity(AchievementsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static AchievementsRequest ParseEmpty(in PacketReader p)
    {
        AchievementBadgeWire.RequireEmpty(in p, nameof(AchievementsRequest));
        return new();
    }
}

public sealed record BadgePointLimitsRequest : IParserComposer<BadgePointLimitsRequest>
{
    public static BadgePointLimitsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BadgePointLimitsRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    private static BadgePointLimitsRequest ParseUnity(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BadgePointLimitsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void ComposeUnity(BadgePointLimitsRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static BadgePointLimitsRequest ParseEmpty(in PacketReader p)
    {
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgePointLimitsRequest));
        return new();
    }
}

public sealed record BadgeInventoryRequest : IParserComposer<BadgeInventoryRequest>
{
    public static BadgeInventoryRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BadgeInventoryRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    private static BadgeInventoryRequest ParseUnity(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BadgeInventoryRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static void ComposeUnity(BadgeInventoryRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static BadgeInventoryRequest ParseEmpty(in PacketReader p)
    {
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventoryRequest));
        return new();
    }
}
