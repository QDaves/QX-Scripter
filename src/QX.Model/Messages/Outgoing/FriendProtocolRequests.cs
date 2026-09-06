using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record FriendInitializationRequest : IParserComposer<FriendInitializationRequest>
{
    public static FriendInitializationRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendInitializationRequest ParseFlash(in PacketReader p) => new();

    private static FriendInitializationRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendInitializationRequest value, in PacketWriter p) { }

    private static void ComposeUnity(FriendInitializationRequest value, in PacketWriter p) { }
}

public sealed record PendingFriendRequestsRequest : IParserComposer<PendingFriendRequestsRequest>
{
    public static PendingFriendRequestsRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PendingFriendRequestsRequest ParseFlash(in PacketReader p) => new();

    private static PendingFriendRequestsRequest ParseUnity(in PacketReader p) => new();

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PendingFriendRequestsRequest value, in PacketWriter p) { }

    private static void ComposeUnity(PendingFriendRequestsRequest value, in PacketWriter p) { }
}

public sealed record FriendRequest(string Name) : IParserComposer<FriendRequest>
{
    public static FriendRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    private static FriendRequest ParseUnity(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendRequest value, in PacketWriter p) =>
        p.WriteString(value.Name);

    private static void ComposeUnity(FriendRequest value, in PacketWriter p) =>
        p.WriteString(value.Name);
}

public sealed record FollowFriendRequest(Id FriendId) : IParserComposer<FollowFriendRequest>
{
    public static FollowFriendRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FollowFriendRequest ParseFlash(in PacketReader p) => new(p.ReadId());

    private static FollowFriendRequest ParseUnity(in PacketReader p) => new(p.ReadId());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FollowFriendRequest value, in PacketWriter p) =>
        p.WriteId(value.FriendId);

    private static void ComposeUnity(FollowFriendRequest value, in PacketWriter p) =>
        p.WriteId(value.FriendId);
}

public sealed record FriendSearchRequest(string Query) : IParserComposer<FriendSearchRequest>
{
    public static FriendSearchRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FriendSearchRequest ParseFlash(in PacketReader p) => new(p.ReadString());

    private static FriendSearchRequest ParseUnity(in PacketReader p) => new(p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FriendSearchRequest value, in PacketWriter p) =>
        p.WriteString(value.Query);

    private static void ComposeUnity(FriendSearchRequest value, in PacketWriter p) =>
        p.WriteString(value.Query);
}

public sealed record SetFriendRelationshipRequest(Id FriendId, RelationshipType Relationship)
    : IParserComposer<SetFriendRelationshipRequest>
{
    public static SetFriendRelationshipRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SetFriendRelationshipRequest ParseFlash(in PacketReader p) =>
        new(p.ReadId(), (RelationshipType)p.ReadInt());

    private static SetFriendRelationshipRequest ParseUnity(in PacketReader p) =>
        new(p.ReadId(), (RelationshipType)p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SetFriendRelationshipRequest value, in PacketWriter p)
    {
        p.WriteId(value.FriendId);
        p.WriteInt((int)value.Relationship);
    }

    private static void ComposeUnity(SetFriendRelationshipRequest value, in PacketWriter p)
    {
        p.WriteId(value.FriendId);
        p.WriteInt((int)value.Relationship);
    }
}

public sealed record AcceptFriends(IReadOnlyList<Id> RequestIds) : IParserComposer<AcceptFriends>
{
    public static AcceptFriends Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AcceptFriends ParseFlash(in PacketReader p) => new(p.ReadIdArray());

    private static AcceptFriends ParseUnity(in PacketReader p) => new(p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AcceptFriends value, in PacketWriter p) =>
        p.WriteIdArray(value.RequestIds);

    private static void ComposeUnity(AcceptFriends value, in PacketWriter p) =>
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static DeclineFriends ParseFlash(in PacketReader p) =>
        new(p.ReadBool(), p.ReadIdArray());

    private static DeclineFriends ParseUnity(in PacketReader p) =>
        new(p.ReadBool(), p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(DeclineFriends value, in PacketWriter p)
    {
        p.WriteBool(value.DeclineAll);
        p.WriteIdArray(value.RequestIds);
    }

    private static void ComposeUnity(DeclineFriends value, in PacketWriter p)
    {
        p.WriteBool(value.DeclineAll);
        p.WriteIdArray(value.RequestIds);
    }
}

public sealed record RemoveFriends(IReadOnlyList<Id> FriendIds) : IParserComposer<RemoveFriends>
{
    public static RemoveFriends Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RemoveFriends ParseFlash(in PacketReader p) => new(p.ReadIdArray());

    private static RemoveFriends ParseUnity(in PacketReader p) => new(p.ReadIdArray());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RemoveFriends value, in PacketWriter p) =>
        p.WriteIdArray(value.FriendIds);

    private static void ComposeUnity(RemoveFriends value, in PacketWriter p) =>
        p.WriteIdArray(value.FriendIds);
}

public sealed record SendPrivateMessage(Id RecipientId, string Text, int? MessageIndex)
    : IParserComposer<SendPrivateMessage>
{
    public static SendPrivateMessage Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static SendPrivateMessage ParseFlash(in PacketReader p) =>
        new(p.ReadId(), p.ReadString(), p.ReadInt());

    private static SendPrivateMessage ParseUnity(in PacketReader p)
    {
        Id recipient_id = p.ReadId();
        string text = p.ReadString();
        int? message_index = p.Available == 0 ? null : p.ReadInt();
        return new SendPrivateMessage(recipient_id, text, message_index);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(SendPrivateMessage value, in PacketWriter p)
    {
        p.WriteId(value.RecipientId);
        p.WriteString(value.Text);
        p.WriteInt(value.MessageIndex ??
            throw new InvalidOperationException("A Flash private message requires a message index."));
    }

    private static void ComposeUnity(SendPrivateMessage value, in PacketWriter p)
    {
        p.WriteId(value.RecipientId);
        p.WriteString(value.Text);
        if (value.MessageIndex is int message_index)
            p.WriteInt(message_index);
    }
}
