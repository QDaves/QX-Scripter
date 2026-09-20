using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record FriendInitializationRequest : IParserComposer<FriendInitializationRequest>
{
    public static FriendInitializationRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendInitializationRequest ParseFlash(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendInitializationRequest value, in PacketWriter p) { }
}

public sealed record PendingFriendRequestsRequest : IParserComposer<PendingFriendRequestsRequest>
{
    public static PendingFriendRequestsRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PendingFriendRequestsRequest ParseFlash(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PendingFriendRequestsRequest value, in PacketWriter p) { }
}

public sealed record FriendRequest(string Name) : IParserComposer<FriendRequest>
{
    public static FriendRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendRequest value, in PacketWriter p) =>
        p.WriteString(value.Name);
}

public sealed record FollowFriendRequest(Id FriendId) : IParserComposer<FollowFriendRequest>
{
    public static FollowFriendRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FollowFriendRequest ParseFlash(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FollowFriendRequest value, in PacketWriter p) =>
        p.WriteId(value.FriendId);
}

public sealed record FriendSearchRequest(string Query) : IParserComposer<FriendSearchRequest>
{
    public static FriendSearchRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendSearchRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(FriendSearchRequest value, in PacketWriter p) =>
        p.WriteString(value.Query);
}

public sealed record SetFriendRelationshipRequest(Id FriendId, RelationshipType Relationship)
    : IParserComposer<SetFriendRelationshipRequest>
{
    public static SetFriendRelationshipRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SetFriendRelationshipRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), (RelationshipType)p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SetFriendRelationshipRequest value, in PacketWriter p)
    {
        p.WriteId(value.FriendId);
        p.WriteInt((int)value.Relationship);
    }
}

public sealed record AcceptFriends(IReadOnlyList<Id> RequestIds) : IParserComposer<AcceptFriends>
{
    public static AcceptFriends Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static AcceptFriends ParseFlash(in PacketReader p) => new(p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(AcceptFriends value, in PacketWriter p) =>
        p.WriteIdArray(value.RequestIds);
}

public sealed record DeclineFriends(bool DeclineAll, IReadOnlyList<Id> RequestIds)
    : IParserComposer<DeclineFriends>
{
    public static DeclineFriends All() => new(true, []);

    public static DeclineFriends Only(IReadOnlyList<Id> requestIds)
    {
        ArgumentNullException.ThrowIfNull(requestIds);
        return new DeclineFriends(false, requestIds);
    }

    public static DeclineFriends Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static DeclineFriends ParseFlash(in PacketReader p) =>
        new(p.ReadBool(), p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(DeclineFriends value, in PacketWriter p)
    {
        p.WriteBool(value.DeclineAll);
        p.WriteIdArray(value.RequestIds);
    }
}

public sealed record RemoveFriends(IReadOnlyList<Id> FriendIds) : IParserComposer<RemoveFriends>
{
    public static RemoveFriends Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RemoveFriends ParseFlash(in PacketReader p) => new(p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RemoveFriends value, in PacketWriter p) =>
        p.WriteIdArray(value.FriendIds);
}

public sealed record SendPrivateMessage(Id RecipientId, string Text, int? MessageIndex)
    : IParserComposer<SendPrivateMessage>
{
    public static SendPrivateMessage Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static SendPrivateMessage ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(SendPrivateMessage value, in PacketWriter p)
    {
        p.WriteId(value.RecipientId);
        p.WriteString(value.Text);
        p.WriteInt(value.MessageIndex ??
            throw new InvalidOperationException("A Flash private message requires a message index."));
    }
}
