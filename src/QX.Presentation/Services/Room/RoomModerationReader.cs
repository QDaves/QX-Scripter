using Qx.Game.Application;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Services.Room;

public static class RoomModerationReader
{
    public const int PageLimit = 500;
    public const int RefreshTimeoutMilliseconds = 10000;

    public static async Task<RoomModerationStateView> ReadAsync(
        IGameGateway gateway,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        RoomModerationStateView first = await gateway
            .QueryAsync<RoomModerationStateRequest, RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationState,
                new RoomModerationStateRequest(Limit: PageLimit),
                cancellation_token);
        return await CompleteAsync(gateway, first, cancellation_token);
    }

    public static async Task<RoomModerationStateView> RefreshAsync(
        IGameGateway gateway,
        RoomModerationStateView state,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(state);
        RoomModerationStateView first = await gateway
            .InvokeAsync<RoomModerationRefreshRequest, RoomModerationStateView>(
                ApplicationMemberIds.RoomModerationRefresh,
                new RoomModerationRefreshRequest(
                    PageLimit,
                    RefreshTimeoutMilliseconds,
                    state.SessionGeneration,
                    state.RoomId,
                    state.RoomGeneration),
                cancellation_token);
        return await CompleteAsync(gateway, first, cancellation_token);
    }

    public static async Task<RoomModerationStateView> CompleteAsync(
        IGameGateway gateway,
        RoomModerationStateView first,
        CancellationToken cancellation_token)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(first);
        if (first.BanList.Offset != 0)
            throw new InvalidOperationException("The room-ban snapshot did not start at offset zero.");

        var bans = new List<RoomBanView>(first.BanList.TotalBans);
        bans.AddRange(first.BanList.Bans);
        int? next_offset = first.BanList.NextOffset;
        while (next_offset is int offset)
        {
            RoomModerationStateView page = await gateway
                .QueryAsync<RoomModerationStateRequest, RoomModerationStateView>(
                    ApplicationMemberIds.RoomModerationState,
                    new RoomModerationStateRequest(offset, PageLimit, first.BanList.SnapshotRevision),
                    cancellation_token);
            if (page.SessionGeneration != first.SessionGeneration ||
                page.Revision != first.Revision ||
                page.RoomGeneration != first.RoomGeneration ||
                page.RoomId != first.RoomId ||
                page.BanList.SnapshotRevision != first.BanList.SnapshotRevision ||
                page.BanList.Offset != offset)
            {
                throw new InvalidOperationException("The room-ban snapshot changed between pages.");
            }
            bans.AddRange(page.BanList.Bans);
            if (page.BanList.NextOffset is int following && following <= offset)
                throw new InvalidOperationException("The room-ban snapshot returned an invalid continuation offset.");
            next_offset = page.BanList.NextOffset;
        }

        if (bans.Count != first.BanList.TotalBans)
            throw new InvalidOperationException("The room-ban snapshot ended before every entry was read.");

        return first with
        {
            BanList = first.BanList with
            {
                Offset = 0,
                NextOffset = null,
                Bans = [.. bans]
            }
        };
    }

    public static RoomBanEntry[] Entries(RoomModerationStateView state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return [.. state.BanList.Bans.Select(ban => new RoomBanEntry(ban.UserId, ban.Name, state.RoomGeneration))];
    }
}
