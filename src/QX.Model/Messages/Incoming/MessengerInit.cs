using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record MessengerInit(
    int UserLimit,
    int NormalLimit,
    int ExtendedLimit,
    IReadOnlyList<FriendCategory> Categories,
    int FriendCount = 0,
    int FriendRequestCount = 0) : IParserComposer<MessengerInit>
{
    /// <summary>
    /// Whether the message carried the two trailing counts.
    /// </summary>
    /// <remarks>
    /// Unity always sends them. On Flash they depend on the revision: the decompiled client's
    /// parser stops after the category list and never reads them, yet a live Flash session sends
    /// them, and refusing to read them made this message fail to parse on every login. They are
    /// read whenever eight bytes remain. The two names are the ones the same message uses on Unity
    /// and are not established for Flash, where the client itself discards the values.
    /// </remarks>
    public bool HasCounts { get; init; }

    public static MessengerInit Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static MessengerInit ParseFlash(in PacketReader p)
    {
        int userLimit = p.ReadInt();
        int normalLimit = p.ReadInt();
        int extendedLimit = p.ReadInt();

        int count = p.ReadLength();
        var categories = new List<FriendCategory>(count);
        for (int i = 0; i < count; i++)
            categories.Add(p.Parse<FriendCategory>());

        bool has_counts = p.Available >= 8;
        int friendCount = has_counts ? p.ReadInt() : 0;
        int friendRequestCount = has_counts ? p.ReadInt() : 0;
        return new MessengerInit(userLimit, normalLimit, extendedLimit, categories, friendCount, friendRequestCount)
        {
            HasCounts = has_counts
        };
    }

    private static MessengerInit ParseUnity(in PacketReader p)
    {
        int user_limit = p.ReadInt();
        int normal_limit = p.ReadInt();
        int extended_limit = p.ReadInt();
        int count = p.ReadLength();
        var categories = new FriendCategory[count];
        for (int i = 0; i < count; i++)
            categories[i] = p.Parse<FriendCategory>();
        return new MessengerInit(
            user_limit,
            normal_limit,
            extended_limit,
            categories,
            p.ReadInt(),
            p.ReadInt())
        {
            HasCounts = true
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(MessengerInit value, in PacketWriter p)
    {
        p.WriteInt(value.UserLimit);
        p.WriteInt(value.NormalLimit);
        p.WriteInt(value.ExtendedLimit);

        p.WriteLength((Length)value.Categories.Count);
        foreach (FriendCategory category in value.Categories)
            p.Compose(category);

        if (value.HasCounts)
        {
            p.WriteInt(value.FriendCount);
            p.WriteInt(value.FriendRequestCount);
        }
    }

    private static void ComposeUnity(MessengerInit value, in PacketWriter p)
    {
        p.WriteInt(value.UserLimit);
        p.WriteInt(value.NormalLimit);
        p.WriteInt(value.ExtendedLimit);

        p.WriteLength((Length)value.Categories.Count);
        foreach (FriendCategory category in value.Categories)
            p.Compose(category);

        p.WriteInt(value.FriendCount);
        p.WriteInt(value.FriendRequestCount);
    }
}
