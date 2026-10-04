using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents the <c>ScrSendUserInfo</c> message, received with the user's subscription info for one product.</summary>
/// <param name="ProductName">The subscription product name, such as <c>habbo_club</c> or <c>builders_club</c>.</param>
/// <param name="DaysToPeriodEnd">The number of days until the current period ends.</param>
/// <param name="MemberPeriods">The number of periods the user has been a member.</param>
/// <param name="PeriodsSubscribedAhead">The number of periods paid in advance.</param>
/// <param name="ResponseType">The response type code sent by the hotel.</param>
/// <param name="HasEverBeenMember">Whether the user has ever been a member.</param>
/// <param name="IsVip">Whether the subscription is a VIP subscription.</param>
/// <param name="PastClubDays">The number of days of past club membership.</param>
/// <param name="PastVipDays">The number of days of past VIP membership.</param>
/// <param name="MinutesUntilExpiration">The number of minutes until the subscription expires.</param>
/// <param name="MinutesSinceLastModified">
/// The number of minutes since the subscription was last modified, or <see langword="null"/> when the
/// packet ends before it.
/// </param>
public sealed record ScrSendUserInfo(
    string ProductName,
    int DaysToPeriodEnd,
    int MemberPeriods,
    int PeriodsSubscribedAhead,
    int ResponseType,
    bool HasEverBeenMember,
    bool IsVip,
    int PastClubDays,
    int PastVipDays,
    int MinutesUntilExpiration,
    int? MinutesSinceLastModified) : IParserComposer<ScrSendUserInfo>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ScrSendUserInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ScrSendUserInfo ParseFlash(in PacketReader p) => ParseInfo(in p);

    private static ScrSendUserInfo ParseInfo(in PacketReader p)
    {
        string product_name = p.ReadString();
        int days_to_period_end = p.ReadInt();
        int member_periods = p.ReadInt();
        int periods_subscribed_ahead = p.ReadInt();
        int response_type = p.ReadInt();
        bool has_ever_been_member = p.ReadBool();
        bool is_vip = p.ReadBool();
        int past_club_days = p.ReadInt();
        int past_vip_days = p.ReadInt();
        int minutes_until_expiration = p.ReadInt();
        int? minutes_since_last_modified = SubscriptionWire.ReadIntTail(
            in p,
            false,
            nameof(ScrSendUserInfo));

        return new ScrSendUserInfo(
            product_name,
            days_to_period_end,
            member_periods,
            periods_subscribed_ahead,
            response_type,
            has_ever_been_member,
            is_vip,
            past_club_days,
            past_vip_days,
            minutes_until_expiration,
            minutes_since_last_modified);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ScrSendUserInfo value, in PacketWriter p) =>
        value.ComposeInfo(in p);

    private void ComposeInfo(in PacketWriter p)
    {
        SubscriptionWire.RequireString(ProductName, nameof(ProductName), in p);

        p.WriteString(ProductName);
        p.WriteInt(DaysToPeriodEnd);
        p.WriteInt(MemberPeriods);
        p.WriteInt(PeriodsSubscribedAhead);
        p.WriteInt(ResponseType);
        p.WriteBool(HasEverBeenMember);
        p.WriteBool(IsVip);
        p.WriteInt(PastClubDays);
        p.WriteInt(PastVipDays);
        p.WriteInt(MinutesUntilExpiration);
        if (MinutesSinceLastModified is int minutes_since_last_modified)
            p.WriteInt(minutes_since_last_modified);
    }
}

/// <summary>Represents the <c>ScrSendKickbackInfo</c> message, received with the user's Habbo Club kickback summary.</summary>
/// <param name="CurrentHcStreak">The length of the current Habbo Club streak.</param>
/// <param name="FirstSubscriptionDate">The date of the first subscription as sent by the hotel.</param>
/// <param name="KickbackPercentage">The kickback percentage.</param>
/// <param name="TotalCreditsMissed">The total credits missed.</param>
/// <param name="TotalCreditsRewarded">The total credits rewarded.</param>
/// <param name="TotalCreditsSpent">The total credits spent.</param>
/// <param name="CreditRewardForStreakBonus">The credit reward for the streak bonus.</param>
/// <param name="CreditRewardForMonthlySpent">The credit reward for the credits spent this month.</param>
/// <param name="TimeUntilPayday">The time until the next payday as sent by the hotel.</param>
public sealed record ScrSendKickbackInfo(
    int CurrentHcStreak,
    string FirstSubscriptionDate,
    double KickbackPercentage,
    int TotalCreditsMissed,
    int TotalCreditsRewarded,
    int TotalCreditsSpent,
    int CreditRewardForStreakBonus,
    int CreditRewardForMonthlySpent,
    int TimeUntilPayday) : IParserComposer<ScrSendKickbackInfo>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ScrSendKickbackInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static ScrSendKickbackInfo ParseFlash(in PacketReader p) => ParseInfo(in p);

    private static ScrSendKickbackInfo ParseInfo(in PacketReader p)
    {
        var value = new ScrSendKickbackInfo(
            p.ReadInt(),
            p.ReadString(),
            p.ReadDouble(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
        SubscriptionWire.RequireEmpty(in p, nameof(ScrSendKickbackInfo));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(ScrSendKickbackInfo value, in PacketWriter p) =>
        value.ComposeInfo(in p);

    private void ComposeInfo(in PacketWriter p)
    {
        SubscriptionWire.RequireString(
            FirstSubscriptionDate,
            nameof(FirstSubscriptionDate),
            in p);
        p.WriteInt(CurrentHcStreak);
        p.WriteString(FirstSubscriptionDate);
        p.WriteDouble(KickbackPercentage);
        p.WriteInt(TotalCreditsMissed);
        p.WriteInt(TotalCreditsRewarded);
        p.WriteInt(TotalCreditsSpent);
        p.WriteInt(CreditRewardForStreakBonus);
        p.WriteInt(CreditRewardForMonthlySpent);
        p.WriteInt(TimeUntilPayday);
    }
}

/// <summary>Represents the <c>BuildersClubFurniCount</c> message, received with the number of Builders Club furni the user has placed.</summary>
/// <param name="FurniCount">The number of Builders Club furni placed.</param>
public sealed record BuildersClubFurniCount(int FurniCount)
    : IParserComposer<BuildersClubFurniCount>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BuildersClubFurniCount Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuildersClubFurniCount ParseFlash(in PacketReader p) => ParseCount(in p);

    private static BuildersClubFurniCount ParseCount(in PacketReader p)
    {
        var value = new BuildersClubFurniCount(p.ReadInt());
        SubscriptionWire.RequireEmpty(in p, nameof(BuildersClubFurniCount));
        return value;
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubFurniCount value, in PacketWriter p)
    {
        p.WriteInt(value.FurniCount);
    }
}

/// <summary>Represents the <c>BuildersClubSubscriptionStatus</c> message, received with the user's Builders Club membership status.</summary>
/// <param name="SecondsLeft">The number of seconds left in the membership.</param>
/// <param name="FurniLimit">The number of Builders Club furni the user can place.</param>
/// <param name="MaxFurniLimit">The maximum Builders Club furni limit.</param>
/// <param name="SecondsLeftWithGrace">
/// The number of seconds left including the grace period, or <see langword="null"/> when the packet
/// ends before it.
/// </param>
public sealed record BuildersClubMembershipStatus(
    int SecondsLeft,
    int FurniLimit,
    int MaxFurniLimit,
    int? SecondsLeftWithGrace) : IParserComposer<BuildersClubMembershipStatus>
{
    /// <summary>Gets <see cref="SecondsLeftWithGrace"/>, or <see cref="SecondsLeft"/> when it was not sent.</summary>
    public int EffectiveSecondsLeftWithGrace => SecondsLeftWithGrace ?? SecondsLeft;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BuildersClubMembershipStatus Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuildersClubMembershipStatus ParseFlash(in PacketReader p)
    {
        int seconds_left = p.ReadInt();
        int furni_limit = p.ReadInt();
        int max_furni_limit = p.ReadInt();
        int? seconds_left_with_grace = SubscriptionWire.ReadIntTail(
            in p,
            false,
            nameof(BuildersClubMembershipStatus));
        return new BuildersClubMembershipStatus(
            seconds_left,
            furni_limit,
            max_furni_limit,
            seconds_left_with_grace);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubMembershipStatus value, in PacketWriter p)
    {
        p.WriteInt(value.SecondsLeft);
        p.WriteInt(value.FurniLimit);
        p.WriteInt(value.MaxFurniLimit);
        if (value.SecondsLeftWithGrace is int seconds_left_with_grace)
            p.WriteInt(seconds_left_with_grace);
    }
}

/// <summary>Represents the <c>BuildersClubPlacementWarning</c> message, received when the hotel warns about a Builders Club furni placement.</summary>
/// <remarks>
/// The packet starts with a type code: 0 is followed by a floor placement and 1 by a wall placement.
/// Any other code makes parsing throw <see cref="InvalidDataException"/>.
/// </remarks>
/// <param name="PageId">The catalog page ID of the placement.</param>
/// <param name="OfferId">The offer ID of the placement.</param>
/// <param name="ExtraParam">The extra parameter of the placement.</param>
/// <param name="Placement">The placement, a <see cref="BuildersClubFloorPlacement"/> or a <see cref="BuildersClubWallPlacement"/>.</param>
public sealed record BuildersClubPlacementWarning(
    int PageId,
    int OfferId,
    string ExtraParam,
    BuildersClubPlacement Placement) : IParserComposer<BuildersClubPlacementWarning>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static BuildersClubPlacementWarning Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static BuildersClubPlacementWarning ParseFlash(in PacketReader p)
    {
        int type_code = p.ReadInt();
        int page_id = p.ReadInt();
        int offer_id = p.ReadInt();
        string extra_param = p.ReadString();
        BuildersClubPlacement placement = type_code switch
        {
            0 => new BuildersClubFloorPlacement(
                p.ReadInt(),
                p.ReadInt(),
                p.ReadInt()),
            1 => new BuildersClubWallPlacement(p.ReadString()),
            _ => throw new InvalidDataException(
                $"Unsupported Builders Club placement type: {type_code}.")
        };
        SubscriptionWire.RequireEmpty(in p, nameof(BuildersClubPlacementWarning));

        return new BuildersClubPlacementWarning(
            page_id,
            offer_id,
            extra_param,
            placement);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubPlacementWarning value, in PacketWriter p)
    {
        BuildersClubPlacement placement = PreparePlacement(value, in p);
        switch (placement)
        {
            case BuildersClubFloorPlacement floor:
                p.WriteInt(0);
                p.WriteInt(value.PageId);
                p.WriteInt(value.OfferId);
                p.WriteString(value.ExtraParam);
                p.WriteInt(floor.X);
                p.WriteInt(floor.Y);
                p.WriteInt(floor.Direction);
                break;
            case BuildersClubWallPlacement wall:
                p.WriteInt(1);
                p.WriteInt(value.PageId);
                p.WriteInt(value.OfferId);
                p.WriteString(value.ExtraParam);
                p.WriteString(wall.WallLocation);
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported Builders Club placement model: {value.Placement?.GetType().Name ?? "null"}.");
        }
    }

    private static BuildersClubPlacement PreparePlacement(
        BuildersClubPlacementWarning value,
        in PacketWriter p)
    {
        SubscriptionWire.RequireString(value.ExtraParam, nameof(ExtraParam), in p);
        if (value.Placement is BuildersClubWallPlacement wall)
        {
            SubscriptionWire.RequireString(
                wall.WallLocation,
                nameof(BuildersClubWallPlacement.WallLocation),
                in p);
        }
        else if (value.Placement is not BuildersClubFloorPlacement)
        {
            throw new InvalidDataException(
                $"Unsupported Builders Club placement model: {value.Placement?.GetType().Name ?? "null"}.");
        }

        return value.Placement;
    }
}
