using System.Text;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents the <c>NewFriendRequest</c> message, received when another user sends a friend request.
/// </summary>
/// <param name="RequestId">The identifier of the request, which is the user identifier of the requester.</param>
/// <param name="RequesterName">The name of the user who sent the request.</param>
/// <param name="FigureString">The figure string of the user who sent the request.</param>
public sealed record NewFriendRequest(Id RequestId, string RequesterName, string FigureString)
    : IParserComposer<NewFriendRequest>
{
    /// <summary>Gets the user identifier of the requester, the same value as <see cref="RequestId"/>.</summary>
    public Id RequesterUserId => RequestId;

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static NewFriendRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NewFriendRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NewFriendRequest value, in PacketWriter p)
    {
        p.WriteId(value.RequestId);
        p.WriteString(value.RequesterName);
        p.WriteString(value.FigureString);
    }
}

/// <summary>Represents the <c>FriendRequests</c> message, received with the user's pending friend requests.</summary>
/// <param name="Total">
/// The total number of pending friend requests, which is never smaller than the number of requests in the message.
/// </param>
/// <param name="Requests">The pending friend requests included in the message.</param>
public sealed record PendingFriendRequests(
    int Total,
    IReadOnlyList<NewFriendRequest> Requests) : IParserComposer<PendingFriendRequests>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <exception cref="InvalidDataException">Thrown when the total is smaller than the number of requests.</exception>
    public static PendingFriendRequests Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PendingFriendRequests ParseFlash(in PacketReader p)
    {
        int total = p.ReadInt();
        NewFriendRequest[] requests = p.ParseArray<NewFriendRequest>();
        if (total < requests.Length)
        {
            throw new InvalidDataException(
                "The total pending friend-request count cannot be smaller than the returned request count.");
        }
        return new PendingFriendRequests(total, requests);
    }

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PendingFriendRequests value, in PacketWriter p)
    {
        Validate(value);
        p.WriteInt(value.Total);
        p.WriteLength((Length)value.Requests.Count);
        foreach (NewFriendRequest request in value.Requests)
            p.Compose(request);
    }

    private static void Validate(PendingFriendRequests value)
    {
        ArgumentNullException.ThrowIfNull(value.Requests);
        ArgumentOutOfRangeException.ThrowIfNegative(value.Total);
        _ = (Length)value.Requests.Count;
        if (value.Total < value.Requests.Count)
            throw new InvalidDataException("The total pending friend-request count cannot be smaller than the returned request count.");
        foreach (NewFriendRequest request in value.Requests)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.RequesterName);
            ArgumentNullException.ThrowIfNull(request.FigureString);
            _ = checked((int)(long)request.RequestId);
            if (Encoding.UTF8.GetByteCount(request.RequesterName) > ushort.MaxValue ||
                Encoding.UTF8.GetByteCount(request.FigureString) > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pending friend-request strings exceed the wire limit.");
            }
        }
    }
}
