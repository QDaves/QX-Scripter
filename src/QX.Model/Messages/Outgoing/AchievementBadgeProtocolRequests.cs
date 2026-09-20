using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record AchievementsRequest : IParserComposer<AchievementsRequest>
{
    public static AchievementsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AchievementsRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AchievementsRequest value, in PacketWriter p) =>
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
        FlashWire.Parse(in p, ParseFlash);

    private static BadgePointLimitsRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BadgePointLimitsRequest value, in PacketWriter p) =>
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
        FlashWire.Parse(in p, ParseFlash);

    private static BadgeInventoryRequest ParseFlash(in PacketReader p) => ParseEmpty(in p);

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BadgeInventoryRequest value, in PacketWriter p) =>
        ArgumentNullException.ThrowIfNull(value);

    private static BadgeInventoryRequest ParseEmpty(in PacketReader p)
    {
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeInventoryRequest));
        return new();
    }
}
