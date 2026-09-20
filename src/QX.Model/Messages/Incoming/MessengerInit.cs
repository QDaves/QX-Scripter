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
    public bool HasCounts { get; init; }

    public static MessengerInit Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}
