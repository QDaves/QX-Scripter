using System.Text;
using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record NewFriendRequest(Id RequestId, string RequesterName, string FigureString)
    : IParserComposer<NewFriendRequest>
{
    public Id RequesterUserId => RequestId;

    public static NewFriendRequest Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static NewFriendRequest ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(NewFriendRequest value, in PacketWriter p)
    {
        p.WriteId(value.RequestId);
        p.WriteString(value.RequesterName);
        p.WriteString(value.FigureString);
    }
}

public sealed record PendingFriendRequests(
    int Total,
    IReadOnlyList<NewFriendRequest> Requests) : IParserComposer<PendingFriendRequests>
{
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

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PendingFriendRequests value, in PacketWriter p)
    {
        Validate(value, ClientType.Flash);
        p.WriteInt(value.Total);
        p.WriteLength((Length)value.Requests.Count);
        foreach (NewFriendRequest request in value.Requests)
            p.Compose(request);
    }

    private static void Validate(PendingFriendRequests value, ClientType client)
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
