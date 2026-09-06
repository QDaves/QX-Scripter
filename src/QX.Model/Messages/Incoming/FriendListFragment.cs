using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record FriendListFragment(int Total, int Index, IReadOnlyList<Friend> Friends) : IParserComposer<FriendListFragment>
{
    public static FriendListFragment Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendListFragment ParseFlash(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();

        int count = p.ReadLength();
        var friends = new List<Friend>(count);
        for (int i = 0; i < count; i++)
            friends.Add(p.Parse<Friend>());

        return new FriendListFragment(total, index, friends);
    }

    private static FriendListFragment ParseUnity(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();

        int count = p.ReadLength();
        var friends = new List<Friend>(count);
        for (int i = 0; i < count; i++)
            friends.Add(p.Parse<Friend>());

        return new FriendListFragment(total, index, friends);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendListFragment value, in PacketWriter p)
    {
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);

        p.WriteLength((Length)value.Friends.Count);
        foreach (Friend friend in value.Friends)
            p.Compose(friend);
    }

    private static void ComposeUnity(FriendListFragment value, in PacketWriter p)
    {
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);

        p.WriteLength((Length)value.Friends.Count);
        foreach (Friend friend in value.Friends)
            p.Compose(friend);
    }
}
