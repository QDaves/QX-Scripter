using Qx.Game.Application;
using Qx.Game.Snapshots;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Services.Friends;

public static class FriendListReader
{
    public const int PageSize = 500;
    public const string ChangedWhileReading = "The friend list changed continuously while it was being read.";

    public static Task<FriendListPage> ReadAsync(IGameGateway gateway, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        return gateway.ReadStableAsync(token => OnePassAsync(gateway, token), cancellation_token);
    }

    static async ValueTask<FriendListPage> OnePassAsync(IGameGateway gateway, CancellationToken cancellation_token)
    {
        var friends = new List<FriendSnapshot>();
        FriendListPage? first = null;
        int offset = 0;
        while (true)
        {
            FriendListPage page = await gateway.QueryAsync<FriendsListRequest, FriendListPage>(
                ApplicationMemberIds.FriendsList,
                new FriendsListRequest(Offset: offset, Limit: PageSize),
                cancellation_token);
            first ??= page;
            if (page.Generation != first.Generation || page.Revision != first.Revision)
                throw new InvalidOperationException(ChangedWhileReading);
            friends.AddRange(page.Friends);
            if (page.NextOffset is not int next || next <= offset)
            {
                return first with
                {
                    Matched = friends.Count,
                    Offset = 0,
                    NextOffset = null,
                    Friends = [.. friends]
                };
            }
            offset = next;
        }
    }
}
