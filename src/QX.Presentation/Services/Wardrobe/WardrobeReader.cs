using Qx.Game.Application;
using Qx.Model.Messages.Incoming;
using Qx.Presentation.Services.Game;

namespace Qx.Presentation.Services.Wardrobe;

public static class WardrobeReader
{
    public const int PageSize = 500;

    public static async Task<IReadOnlyList<WardrobeOutfit>> ReadAsync(IGameGateway gateway, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ProfileWardrobePage first = await gateway.InvokeAsync<ProfileWardrobeRequest, ProfileWardrobePage>(
            ApplicationMemberIds.ProfileWardrobeGet,
            new ProfileWardrobeRequest(Limit: PageSize),
            cancellationToken);
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
                cancellationToken);
            if (!Continues(first, page, offset))
                throw new InvalidOperationException(WardrobeText.SnapshotChanged);
            outfits.AddRange(page.Outfits);
        }
        if (outfits.Count != first.Total)
            throw new InvalidOperationException(WardrobeText.IncompleteResult);
        ProfileStateView state = await gateway.QueryAsync<ProfileStateRequest, ProfileStateView>(
            ApplicationMemberIds.ProfileState,
            new ProfileStateRequest(),
            cancellationToken);
        if (!state.Connected || state.Generation != first.Generation)
            throw new InvalidOperationException(WardrobeText.SessionChanged);
        return outfits;
    }

    static bool Continues(ProfileWardrobePage first, ProfileWardrobePage page, int offset) =>
        page.Generation == first.Generation &&
        page.Revision == first.Revision &&
        page.SnapshotRevision == first.SnapshotRevision &&
        page.State == first.State &&
        page.Total == first.Total &&
        page.Offset == offset;
}
