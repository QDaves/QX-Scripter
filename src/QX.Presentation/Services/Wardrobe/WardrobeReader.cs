using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Services.Wardrobe;

public static class WardrobeReader
{
    public const int PageSize = 500;

    public static async Task<IReadOnlyList<WardrobeOutfit>> ReadAsync(IGameGateway gateway, CancellationToken cancellation_token = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ProfileWardrobePage first = await gateway.InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
            ApplicationMemberIds.ProfileWardrobeGet,
            new ProfileWardrobeRequest(Limit: PageSize),
            cancellation_token);
        var outfits = new List<WardrobeOutfit>(first.Outfits);
        ProfileWardrobePage page = first;
        int offset = first.Offset;
        while (page.NextOffset is int next)
        {
            if (next <= offset)
                throw new InvalidOperationException(WardrobeText.SnapshotChanged);
            offset = next;
            page = await gateway.InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
                ApplicationMemberIds.ProfileWardrobeGet,
                new ProfileWardrobeRequest(offset, PageSize, SnapshotRevision: first.SnapshotRevision),
                cancellation_token);
            if (!Continues(first, page, offset))
                throw new InvalidOperationException(WardrobeText.SnapshotChanged);
            outfits.AddRange(page.Outfits);
        }
        if (outfits.Count != first.Total)
            throw new InvalidOperationException(WardrobeText.IncompleteResult);
        ProfileStateView state = await gateway.QueryAsync<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest(),
            cancellation_token);
        if (!state.Connected || state.Client != first.Client || state.Generation != first.Generation)
            throw new InvalidOperationException(WardrobeText.SessionChanged);
        return outfits;
    }

    static bool Continues(ProfileWardrobePage first, ProfileWardrobePage page, int offset) =>
        page.Client == first.Client &&
        page.Generation == first.Generation &&
        page.Revision == first.Revision &&
        page.SnapshotRevision == first.SnapshotRevision &&
        page.State == first.State &&
        page.Total == first.Total &&
        page.Offset == offset;
}
