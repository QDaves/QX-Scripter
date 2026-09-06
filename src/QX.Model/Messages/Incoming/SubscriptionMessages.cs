using Qx.Messages;
using Qx.Model.Subscriptions;

namespace Qx.Model.Messages.Incoming;

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
    public static ScrSendUserInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ScrSendUserInfo ParseFlash(in PacketReader p) => ParseInfo(in p, false);

    private static ScrSendUserInfo ParseUnity(in PacketReader p) => ParseInfo(in p, true);

    private static ScrSendUserInfo ParseInfo(in PacketReader p, bool require_minutes)
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
            require_minutes,
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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ScrSendUserInfo value, in PacketWriter p) =>
        value.ComposeInfo(in p, false);

    private static void ComposeUnity(ScrSendUserInfo value, in PacketWriter p) =>
        value.ComposeInfo(in p, true);

    private void ComposeInfo(in PacketWriter p, bool require_minutes)
    {
        SubscriptionWire.RequireString(ProductName, nameof(ProductName), in p);
        if (require_minutes && MinutesSinceLastModified is null)
            throw new InvalidDataException(
                "Unity ScrSendUserInfo requires minutes since last modification.");

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
    public static ScrSendKickbackInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static ScrSendKickbackInfo ParseFlash(in PacketReader p) => ParseInfo(in p);

    private static ScrSendKickbackInfo ParseUnity(in PacketReader p) => ParseInfo(in p);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(ScrSendKickbackInfo value, in PacketWriter p) =>
        value.ComposeInfo(in p);

    private static void ComposeUnity(ScrSendKickbackInfo value, in PacketWriter p) =>
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

public sealed record BuildersClubFurniCount(int FurniCount)
    : IParserComposer<BuildersClubFurniCount>
{
    public static BuildersClubFurniCount Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuildersClubFurniCount ParseFlash(in PacketReader p) => ParseCount(in p);

    private static BuildersClubFurniCount ParseUnity(in PacketReader p) => ParseCount(in p);

    private static BuildersClubFurniCount ParseCount(in PacketReader p)
    {
        var value = new BuildersClubFurniCount(p.ReadInt());
        SubscriptionWire.RequireEmpty(in p, nameof(BuildersClubFurniCount));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BuildersClubFurniCount value, in PacketWriter p)
    {
        p.WriteInt(value.FurniCount);
    }

    private static void ComposeUnity(BuildersClubFurniCount value, in PacketWriter p)
    {
        p.WriteInt(value.FurniCount);
    }
}

public sealed record BuildersClubMembershipStatus(
    int SecondsLeft,
    int FurniLimit,
    int MaxFurniLimit,
    int? SecondsLeftWithGrace) : IParserComposer<BuildersClubMembershipStatus>
{
    public int EffectiveSecondsLeftWithGrace => SecondsLeftWithGrace ?? SecondsLeft;

    public static BuildersClubMembershipStatus Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(BuildersClubMembershipStatus value, in PacketWriter p)
    {
        p.WriteInt(value.SecondsLeft);
        p.WriteInt(value.FurniLimit);
        p.WriteInt(value.MaxFurniLimit);
        if (value.SecondsLeftWithGrace is int seconds_left_with_grace)
            p.WriteInt(seconds_left_with_grace);
    }
}

public sealed record BuildersClubPlacementWarning(
    int PageId,
    int OfferId,
    string ExtraParam,
    BuildersClubPlacement Placement) : IParserComposer<BuildersClubPlacementWarning>
{
    public static BuildersClubPlacementWarning Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

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

public sealed record SubscriptionGetUserInfo(string ProductName)
    : IParserComposer<SubscriptionGetUserInfo>
{
    public static SubscriptionGetUserInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SubscriptionGetUserInfo ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetUserInfo ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetUserInfo ParseRequest(in PacketReader p)
    {
        var value = new SubscriptionGetUserInfo(p.ReadString());
        SubscriptionWire.RequireEmpty(in p, nameof(SubscriptionGetUserInfo));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SubscriptionGetUserInfo value, in PacketWriter p)
    {
        SubscriptionWire.RequireString(value.ProductName, nameof(ProductName), in p);
        p.WriteString(value.ProductName);
    }

    private static void ComposeUnity(SubscriptionGetUserInfo value, in PacketWriter p)
    {
        SubscriptionWire.RequireString(value.ProductName, nameof(ProductName), in p);
        p.WriteString(value.ProductName);
    }
}

public sealed record SubscriptionGetKickbackInfo
    : IParserComposer<SubscriptionGetKickbackInfo>
{
    public static SubscriptionGetKickbackInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SubscriptionGetKickbackInfo ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetKickbackInfo ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static SubscriptionGetKickbackInfo ParseRequest(in PacketReader p)
    {
        SubscriptionWire.RequireEmpty(in p, nameof(SubscriptionGetKickbackInfo));
        return new SubscriptionGetKickbackInfo();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SubscriptionGetKickbackInfo value, in PacketWriter p) { }

    private static void ComposeUnity(SubscriptionGetKickbackInfo value, in PacketWriter p) { }
}

public sealed record BuildersClubQueryFurniCount
    : IParserComposer<BuildersClubQueryFurniCount>
{
    public static BuildersClubQueryFurniCount Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static BuildersClubQueryFurniCount ParseFlash(in PacketReader p) => ParseRequest(in p);

    private static BuildersClubQueryFurniCount ParseUnity(in PacketReader p) => ParseRequest(in p);

    private static BuildersClubQueryFurniCount ParseRequest(in PacketReader p)
    {
        SubscriptionWire.RequireEmpty(in p, nameof(BuildersClubQueryFurniCount));
        return new BuildersClubQueryFurniCount();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(BuildersClubQueryFurniCount value, in PacketWriter p) { }

    private static void ComposeUnity(BuildersClubQueryFurniCount value, in PacketWriter p) { }
}

internal static class SubscriptionWire
{
    public static int? ReadIntTail(
        in PacketReader p,
        bool required,
        string message_name)
    {
        if (p.Available == sizeof(int))
            return p.ReadInt();
        if (!required && p.Available == 0)
            return null;

        string expectation = required
            ? "exactly one trailing Int32"
            : "zero or one trailing Int32";
        throw new InvalidDataException($"{message_name} requires {expectation}.");
    }

    public static void RequireEmpty(in PacketReader p, string message_name)
    {
        if (p.Available != 0)
            throw new InvalidDataException(
                $"{message_name} contains {p.Available} unexpected bytes.");
    }

    public static void RequireString(string value, string name, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (p.Encoding.GetByteCount(value) > ushort.MaxValue)
            throw new InvalidDataException($"{name} exceeds the wire string limit.");
    }
}
