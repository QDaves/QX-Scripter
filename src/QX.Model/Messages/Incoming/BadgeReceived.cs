using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record BadgeReceived(
    Id BadgeId,
    string Code,
    int? OwnerCount,
    int? RarityId) : IParserComposer<BadgeReceived>
{
    public bool HasRarityData => OwnerCount.HasValue && RarityId.HasValue;

    public static BadgeReceived Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BadgeReceived ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static BadgeReceived ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static BadgeReceived ParseMessage(in PacketReader p)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(
                AchievementBadgeWire.UserIdWidth(p.Client) +
                AchievementBadgeWire.StringPrefixBytes),
            0,
            nameof(BadgeReceived));
        var strings = AchievementBadgeWire.NewStringBudget();
        Id badge_id = AchievementBadgeWire.ReadUserId(in p, 0, nameof(BadgeId));
        string code = strings.Read(in p, nameof(Code), 0);
        BadgeReceived value = p.Available switch
        {
            0 => new BadgeReceived(badge_id, code, null, null),
            sizeof(int) * 2 => new BadgeReceived(
                badge_id,
                code,
                p.ReadInt(),
                p.ReadInt()),
            _ => throw new InvalidDataException(
                $"BadgeReceived contains an unsupported {p.Available}-byte suffix.")
        };
        AchievementBadgeWire.RequireEmpty(in p, nameof(BadgeReceived));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BadgeReceived value, in PacketWriter p) =>
        value.ComposeMessage(in p);

    private static void ComposeUnity(BadgeReceived value, in PacketWriter p) =>
        value.ComposeMessage(in p);

    private void ComposeMessage(in PacketWriter p)
    {
        if (OwnerCount.HasValue != RarityId.HasValue)
            throw new InvalidOperationException("Badge rarity data must be either complete or absent.");
        AchievementBadgeWire.RequireUserId(BadgeId, p.Client);
        var strings = AchievementBadgeWire.NewStringBudget();
        strings.Require(Code, nameof(Code), in p);

        AchievementBadgeWire.WriteUserId(BadgeId, in p);
        p.WriteString(Code);
        if (OwnerCount is int owner_count && RarityId is int rarity_id)
        {
            p.WriteInt(owner_count);
            p.WriteInt(rarity_id);
        }
    }
}
