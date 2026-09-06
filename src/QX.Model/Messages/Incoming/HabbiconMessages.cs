using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>What the local user may do with a habbicon.</summary>
public enum HabbiconState
{
    /// <summary>Not owned. Buyable when the icon carries a price.</summary>
    Locked = 0,
    /// <summary>Earned and waiting to be claimed.</summary>
    Claimable = 1,
    /// <summary>Owned.</summary>
    Owned = 2,
    /// <summary>Owned and marked as a favourite.</summary>
    Favorite = 3
}

/// <summary>
/// One habbicon: the small pictures that can be sent in a private conversation.
/// </summary>
/// <param name="HabbiconId">The icon's identifier.</param>
/// <param name="Name">The icon's name.</param>
/// <param name="CollectionId">The collection the icon belongs to.</param>
/// <param name="State">Whether the icon is locked, claimable, owned or favourited.</param>
/// <param name="PriceCredits">The price in credits, zero when it is not sold for credits.</param>
/// <param name="PriceActivityPoints">The price in the seasonal currency, zero when unpriced.</param>
/// <param name="ActivityPointType">Which seasonal currency <paramref name="PriceActivityPoints"/> is in.</param>
public sealed record Habbicon(
    int HabbiconId,
    string Name,
    int CollectionId,
    HabbiconState State,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType) : IParserComposer<Habbicon>
{
    private string name = Name ?? throw new ArgumentNullException(nameof(Name));

    public string Name
    {
        get => name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Name));
            name = value;
        }
    }

    /// <summary>Whether the local user owns the icon, favourited or not.</summary>
    public bool IsOwned => State is HabbiconState.Owned or HabbiconState.Favorite;

    /// <summary>Whether the icon is earned and still waiting to be claimed.</summary>
    public bool IsClaimable => State is HabbiconState.Claimable;

    /// <summary>
    /// Whether the icon can be bought right now: not owned, and carrying a price in at least one
    /// currency. An unpriced locked icon is earned rather than sold.
    /// </summary>
    public bool IsPurchasable =>
        State is HabbiconState.Locked && (PriceCredits > 0 || PriceActivityPoints > 0);

    public static Habbicon Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Habbicon ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static Habbicon ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static Habbicon ParseMessage(in PacketReader p)
    {
        var strings = HabbiconWire.NewStringBudget();
        Habbicon value = ParseWire(in p, 0, ref strings);
        HabbiconWire.RequireEmpty(in p, nameof(Habbicon));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Habbicon value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeUnity(Habbicon value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeMessage(Habbicon value, in PacketWriter p)
    {
        var strings = HabbiconWire.NewStringBudget();
        HabbiconWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static Habbicon ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref HabbiconStringBudget strings)
    {
        HabbiconWire.RequireRemaining(
            in p,
            HabbiconWire.HabbiconMinimumBytes,
            trailing_bytes,
            nameof(Habbicon));
        int habbicon_id = p.ReadInt();
        string name = strings.Read(
            in p,
            nameof(Name),
            checked(trailing_bytes + sizeof(int) * 5));
        return new Habbicon(
            habbicon_id,
            name,
            p.ReadInt(),
            (HabbiconState)p.ReadInt(),
            p.ReadInt(),
            p.ReadInt(),
            p.ReadInt());
    }

    internal static HabbiconWireSnapshot PrepareWire(
        Habbicon value,
        ref HabbiconStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var snapshot = new HabbiconWireSnapshot(
            value.HabbiconId,
            value.Name,
            value.CollectionId,
            value.State,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType);
        strings.Require(snapshot.Name, nameof(Name), in p);
        return snapshot;
    }

    internal static void WriteWire(HabbiconWireSnapshot value, in PacketWriter p)
    {
        p.WriteInt(value.HabbiconId);
        p.WriteString(value.Name);
        p.WriteInt(value.CollectionId);
        p.WriteInt((int)value.State);
        p.WriteInt(value.PriceCredits);
        p.WriteInt(value.PriceActivityPoints);
        p.WriteInt(value.ActivityPointType);
    }
}

internal readonly record struct HabbiconWireSnapshot(
    int HabbiconId,
    string Name,
    int CollectionId,
    HabbiconState State,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType);

/// <summary>
/// A habbicon collection: a themed set that pays out a reward icon once it is complete.
/// </summary>
/// <param name="CollectionId">The collection's identifier.</param>
/// <param name="Name">The collection's name.</param>
/// <param name="Completed">Whether every icon in the set is owned.</param>
/// <param name="RewardHabbiconId">
/// The icon awarded for completing the set, or zero when the set has no reward.
/// </param>
/// <param name="RewardState">Whether the reward is still locked, claimable or already taken.</param>
/// <param name="PriceCredits">The price of buying the whole set in credits.</param>
/// <param name="PriceActivityPoints">The price of buying the whole set in a seasonal currency.</param>
/// <param name="ActivityPointType">Which seasonal currency <paramref name="PriceActivityPoints"/> is in.</param>
/// <param name="Habbicons">The icons in the set.</param>
public sealed record HabbiconCollection(
    int CollectionId,
    string Name,
    bool Completed,
    int RewardHabbiconId,
    HabbiconState RewardState,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType,
    IReadOnlyList<Habbicon> Habbicons) : IParserComposer<HabbiconCollection>
{
    private string name = Name ?? throw new ArgumentNullException(nameof(Name));
    private IReadOnlyList<Habbicon> habbicons =
        HabbiconWire.FreezeReferences(Habbicons, nameof(Habbicons));

    public string Name
    {
        get => name;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Name));
            name = value;
        }
    }

    public IReadOnlyList<Habbicon> Habbicons
    {
        get => habbicons;
        init => habbicons = HabbiconWire.FreezeReferences(value, nameof(Habbicons));
    }

    /// <summary>
    /// Whether the completion reward is waiting to be claimed.
    /// </summary>
    /// <remarks>
    /// A set with no reward icon reports <see langword="false"/> whatever the state says, which is
    /// the check the client makes before it offers the claim.
    /// </remarks>
    public bool RewardIsClaimable =>
        RewardHabbiconId > 0 && RewardState is HabbiconState.Claimable;

    public static HabbiconCollection Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HabbiconCollection ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static HabbiconCollection ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static HabbiconCollection ParseMessage(in PacketReader p)
    {
        var strings = HabbiconWire.NewStringBudget();
        HabbiconCollection value = ParseWire(in p, 0, ref strings);
        HabbiconWire.RequireEmpty(in p, nameof(HabbiconCollection));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HabbiconCollection value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(HabbiconCollection value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeMessage(HabbiconCollection value, in PacketWriter p)
    {
        var strings = HabbiconWire.NewStringBudget();
        HabbiconCollectionWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }

    internal static HabbiconCollection ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref HabbiconStringBudget strings)
    {
        HabbiconWire.RequireRemaining(
            in p,
            HabbiconWire.CollectionMinimumBytes(p.Client),
            trailing_bytes,
            nameof(HabbiconCollection));
        int collection_id = p.ReadInt();
        string name = strings.Read(
            in p,
            nameof(Name),
            checked(trailing_bytes + sizeof(bool) + sizeof(int) * 5 +
                HabbiconWire.CountWidth(p.Client)));
        bool completed = p.ReadBool();
        int reward_habbicon_id = p.ReadInt();
        var reward_state = (HabbiconState)p.ReadInt();
        int price_credits = p.ReadInt();
        int price_activity_points = p.ReadInt();
        int activity_point_type = p.ReadInt();
        int count = HabbiconWire.ReadCount(
            in p,
            HabbiconWire.HabbiconMinimumBytes,
            trailing_bytes,
            nameof(Habbicons));
        var habbicons = new Habbicon[count];
        for (int index = 0; index < count; index++)
        {
            int remaining = checked((count - index - 1) * HabbiconWire.HabbiconMinimumBytes);
            habbicons[index] = Habbicon.ParseWire(
                in p,
                checked(trailing_bytes + remaining),
                ref strings);
        }
        return new HabbiconCollection(
            collection_id,
            name,
            completed,
            reward_habbicon_id,
            reward_state,
            price_credits,
            price_activity_points,
            activity_point_type,
            habbicons);
    }

    internal static HabbiconCollectionWireSnapshot PrepareWire(
        HabbiconCollection value,
        ref HabbiconStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        strings.Require(value.Name, nameof(Name), in p);
        int count = HabbiconWire.RequireListCount(value.Habbicons, nameof(Habbicons));
        var habbicons = new HabbiconWireSnapshot[count];
        for (int index = 0; index < count; index++)
            habbicons[index] = Habbicon.PrepareWire(value.Habbicons[index], ref strings, in p);
        return new HabbiconCollectionWireSnapshot(
            value.CollectionId,
            value.Name,
            value.Completed,
            value.RewardHabbiconId,
            value.RewardState,
            value.PriceCredits,
            value.PriceActivityPoints,
            value.ActivityPointType,
            habbicons);
    }

    internal static void WriteWire(HabbiconCollectionWireSnapshot value, in PacketWriter p)
    {
        p.WriteInt(value.CollectionId);
        p.WriteString(value.Name);
        p.WriteBool(value.Completed);
        p.WriteInt(value.RewardHabbiconId);
        p.WriteInt((int)value.RewardState);
        p.WriteInt(value.PriceCredits);
        p.WriteInt(value.PriceActivityPoints);
        p.WriteInt(value.ActivityPointType);
        HabbiconWire.WriteCount(value.Habbicons.Count, in p);
        foreach (HabbiconWireSnapshot habbicon in value.Habbicons)
            Habbicon.WriteWire(habbicon, in p);
    }
}

internal sealed record HabbiconCollectionWireSnapshot(
    int CollectionId,
    string Name,
    bool Completed,
    int RewardHabbiconId,
    HabbiconState RewardState,
    int PriceCredits,
    int PriceActivityPoints,
    int ActivityPointType,
    IReadOnlyList<HabbiconWireSnapshot> Habbicons);

/// <summary>
/// The local user's habbicon states, plus the icons they used most recently.
/// </summary>
/// <remarks>
/// Carries only identifier and state per icon; the names and prices come from
/// <see cref="HabbiconShopData"/>.
/// </remarks>
/// <param name="Habbicons">One entry per icon the hotel has a state for.</param>
/// <param name="RecentHabbiconIds">The icons used most recently, newest first.</param>
public sealed record UserHabbicons(
    IReadOnlyList<UserHabbiconState> Habbicons,
    IReadOnlyList<int> RecentHabbiconIds) : IParserComposer<UserHabbicons>
{
    private IReadOnlyList<UserHabbiconState> habbicons =
        HabbiconWire.FreezeReferences(Habbicons, nameof(Habbicons));
    private IReadOnlyList<int> recent_habbicon_ids =
        HabbiconWire.FreezeValues(RecentHabbiconIds, nameof(RecentHabbiconIds));

    public IReadOnlyList<UserHabbiconState> Habbicons
    {
        get => habbicons;
        init => habbicons = HabbiconWire.FreezeReferences(value, nameof(Habbicons));
    }

    public IReadOnlyList<int> RecentHabbiconIds
    {
        get => recent_habbicon_ids;
        init => recent_habbicon_ids = HabbiconWire.FreezeValues(value, nameof(RecentHabbiconIds));
    }

    public bool RecentHabbiconIdsPresent { get; init; } = true;

    public static UserHabbicons Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserHabbicons ParseFlash(in PacketReader p) => ParseMessage(in p, false);

    private static UserHabbicons ParseUnity(in PacketReader p) => ParseMessage(in p, true);

    private static UserHabbicons ParseMessage(in PacketReader p, bool allow_missing_recents)
    {
        int trailing = allow_missing_recents ? 0 : HabbiconWire.CountWidth(p.Client);
        int count = HabbiconWire.ReadCount(
            in p,
            HabbiconWire.UserStateBytes,
            trailing,
            nameof(Habbicons));
        var habbicons = new UserHabbiconState[count];
        for (int index = 0; index < count; index++)
        {
            int remaining = checked((count - index - 1) * HabbiconWire.UserStateBytes);
            habbicons[index] = UserHabbiconState.ParseWire(
                in p,
                checked(trailing + remaining));
        }

        if (allow_missing_recents && p.Available == 0)
            return new UserHabbicons(habbicons, []) { RecentHabbiconIdsPresent = false };

        int recent_count = HabbiconWire.ReadCount(
            in p,
            sizeof(int),
            0,
            nameof(RecentHabbiconIds));
        var recent = new int[recent_count];
        for (int index = 0; index < recent_count; index++)
            recent[index] = p.ReadInt();
        HabbiconWire.RequireEmpty(in p, nameof(UserHabbicons));
        return new UserHabbicons(habbicons, recent);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserHabbicons value, in PacketWriter p) =>
        ComposeMessage(value, in p, false);

    private static void ComposeUnity(UserHabbicons value, in PacketWriter p) =>
        ComposeMessage(value, in p, true);

    private static void ComposeMessage(
        UserHabbicons value,
        in PacketWriter p,
        bool allow_missing_recents)
    {
        ArgumentNullException.ThrowIfNull(value);
        int habbicon_count = HabbiconWire.RequireListCount(value.Habbicons, nameof(Habbicons));
        int recent_count = HabbiconWire.RequireListCount(
            value.RecentHabbiconIds,
            nameof(RecentHabbiconIds));
        UserHabbiconState[] habbicons = value.Habbicons.ToArray();
        int[] recent = value.RecentHabbiconIds.ToArray();
        if (allow_missing_recents && !value.RecentHabbiconIdsPresent && recent_count != 0)
        {
            throw new InvalidOperationException(
                "An absent recent habbicon list cannot contain entries.");
        }
        HabbiconWire.WriteCount(habbicon_count, in p);
        foreach (UserHabbiconState state in habbicons)
            UserHabbiconState.WriteWire(state, in p);
        if (allow_missing_recents && !value.RecentHabbiconIdsPresent)
            return;
        HabbiconWire.WriteCount(recent_count, in p);
        foreach (int id in recent)
            p.WriteInt(id);
    }
}

/// <summary>The state of one icon in the local user's collection.</summary>
/// <param name="HabbiconId">Which icon.</param>
/// <param name="State">Its state.</param>
public sealed record UserHabbiconState(int HabbiconId, HabbiconState State)
    : IParserComposer<UserHabbiconState>
{
    public static UserHabbiconState Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserHabbiconState ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static UserHabbiconState ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static UserHabbiconState ParseMessage(in PacketReader p)
    {
        UserHabbiconState value = ParseWire(in p, 0);
        HabbiconWire.RequireEmpty(in p, nameof(UserHabbiconState));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserHabbiconState value, in PacketWriter p) =>
        WriteWire(value, in p);

    private static void ComposeUnity(UserHabbiconState value, in PacketWriter p) =>
        WriteWire(value, in p);

    internal static UserHabbiconState ParseWire(in PacketReader p, int trailing_bytes)
    {
        HabbiconWire.RequireRemaining(
            in p,
            HabbiconWire.UserStateBytes,
            trailing_bytes,
            nameof(UserHabbiconState));
        return new UserHabbiconState(p.ReadInt(), (HabbiconState)p.ReadInt());
    }

    internal static void WriteWire(UserHabbiconState value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.HabbiconId);
        p.WriteInt((int)value.State);
    }
}

/// <summary>One icon's state changed, for example after a purchase or a claim.</summary>
/// <param name="HabbiconId">Which icon.</param>
/// <param name="State">Its new state.</param>
public sealed record UserHabbiconStatusChanged(int HabbiconId, HabbiconState State)
    : IParserComposer<UserHabbiconStatusChanged>
{
    public static UserHabbiconStatusChanged Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static UserHabbiconStatusChanged ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static UserHabbiconStatusChanged ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static UserHabbiconStatusChanged ParseMessage(in PacketReader p)
    {
        HabbiconWire.RequireRemaining(
            in p,
            HabbiconWire.UserStateBytes,
            0,
            nameof(UserHabbiconStatusChanged));
        var value = new UserHabbiconStatusChanged(p.ReadInt(), (HabbiconState)p.ReadInt());
        HabbiconWire.RequireEmpty(in p, nameof(UserHabbiconStatusChanged));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(UserHabbiconStatusChanged value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(UserHabbiconStatusChanged value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeMessage(UserHabbiconStatusChanged value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.HabbiconId);
        p.WriteInt((int)value.State);
    }
}

/// <summary>The habbicon shop: every collection with the icons it holds.</summary>
/// <param name="Collections">The collections on offer.</param>
public sealed record HabbiconShopData(IReadOnlyList<HabbiconCollection> Collections)
    : IParserComposer<HabbiconShopData>
{
    private IReadOnlyList<HabbiconCollection> collections =
        HabbiconWire.FreezeReferences(Collections, nameof(Collections));

    public IReadOnlyList<HabbiconCollection> Collections
    {
        get => collections;
        init => collections = HabbiconWire.FreezeReferences(value, nameof(Collections));
    }

    public static HabbiconShopData Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HabbiconShopData ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static HabbiconShopData ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static HabbiconShopData ParseMessage(in PacketReader p)
    {
        var strings = HabbiconWire.NewStringBudget();
        int minimum = HabbiconWire.CollectionMinimumBytes(p.Client);
        int count = HabbiconWire.ReadCount(in p, minimum, 0, nameof(Collections));
        var collections = new HabbiconCollection[count];
        for (int index = 0; index < count; index++)
        {
            int remaining = checked((count - index - 1) * minimum);
            collections[index] = HabbiconCollection.ParseWire(in p, remaining, ref strings);
        }
        HabbiconWire.RequireEmpty(in p, nameof(HabbiconShopData));
        return new HabbiconShopData(collections);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HabbiconShopData value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(HabbiconShopData value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeMessage(HabbiconShopData value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = HabbiconWire.NewStringBudget();
        int count = HabbiconWire.RequireListCount(value.Collections, nameof(Collections));
        var collections = new HabbiconCollectionWireSnapshot[count];
        for (int index = 0; index < count; index++)
        {
            collections[index] = HabbiconCollection.PrepareWire(
                value.Collections[index],
                ref strings,
                in p);
        }
        HabbiconWire.WriteCount(count, in p);
        foreach (HabbiconCollectionWireSnapshot collection in collections)
            HabbiconCollection.WriteWire(collection, in p);
    }
}

/// <summary>Detail for a single icon, in answer to a request for it.</summary>
/// <param name="Habbicon">The icon.</param>
public sealed record HabbiconInfo(Habbicon Habbicon) : IParserComposer<HabbiconInfo>
{
    private Habbicon habbicon = Habbicon ?? throw new ArgumentNullException(nameof(Habbicon));

    public Habbicon Habbicon
    {
        get => habbicon;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Habbicon));
            habbicon = value;
        }
    }

    public static HabbiconInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static HabbiconInfo ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static HabbiconInfo ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static HabbiconInfo ParseMessage(in PacketReader p)
    {
        var strings = HabbiconWire.NewStringBudget();
        var value = new HabbiconInfo(Habbicon.ParseWire(in p, 0, ref strings));
        HabbiconWire.RequireEmpty(in p, nameof(HabbiconInfo));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(HabbiconInfo value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeUnity(HabbiconInfo value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeMessage(HabbiconInfo value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = HabbiconWire.NewStringBudget();
        HabbiconWireSnapshot snapshot = Habbicon.PrepareWire(value.Habbicon, ref strings, in p);
        Habbicon.WriteWire(snapshot, in p);
    }
}

/// <summary>Someone in the room used a habbicon.</summary>
/// <param name="RoomIndex">The room index of the avatar who used it.</param>
/// <param name="HabbiconId">Which icon was used.</param>
public sealed record RoomUseHabbicon(int RoomIndex, int HabbiconId)
    : IParserComposer<RoomUseHabbicon>
{
    public static RoomUseHabbicon Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RoomUseHabbicon ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static RoomUseHabbicon ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static RoomUseHabbicon ParseMessage(in PacketReader p)
    {
        HabbiconWire.RequireRemaining(
            in p,
            HabbiconWire.RoomUseBytes,
            0,
            nameof(RoomUseHabbicon));
        var value = new RoomUseHabbicon(p.ReadInt(), p.ReadInt());
        HabbiconWire.RequireEmpty(in p, nameof(RoomUseHabbicon));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RoomUseHabbicon value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(RoomUseHabbicon value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeMessage(RoomUseHabbicon value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.RoomIndex);
        p.WriteInt(value.HabbiconId);
    }
}
