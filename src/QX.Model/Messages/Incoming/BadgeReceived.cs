using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>BadgeReceived</c> message, received when the user gets a new badge.</summary>
/// <param name="BadgeId">The identifier of the badge.</param>
/// <param name="Code">The badge code.</param>
/// <param name="OwnerCount">
/// The number of users who own the badge, or <see langword="null"/> when the message has no rarity data.
/// </param>
/// <param name="RarityId">
/// The rarity of the badge, or <see langword="null"/> when the message has no rarity data.
/// </param>
public sealed record BadgeReceived(
    Id BadgeId,
    string Code,
    int? OwnerCount,
    int? RarityId) : IParserComposer<BadgeReceived>
{
    /// <summary>Gets whether the message carries both the owner count and the rarity.</summary>
    public bool HasRarityData => OwnerCount.HasValue && RarityId.HasValue;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BadgeReceived Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BadgeReceived ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static BadgeReceived ParseMessage(in PacketReader p)
    {
        AchievementBadgeWire.RequireRemaining(
            in p,
            checked(
                AchievementBadgeWire.UserIdWidth +
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

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when only one of <see cref="OwnerCount"/> and <see cref="RarityId"/> has a value.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BadgeReceived value, in PacketWriter p) =>
        value.ComposeMessage(in p);

    private void ComposeMessage(in PacketWriter p)
    {
        if (OwnerCount.HasValue != RarityId.HasValue)
            throw new InvalidOperationException("Badge rarity data must be either complete or absent.");
        AchievementBadgeWire.RequireUserId(BadgeId);
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
