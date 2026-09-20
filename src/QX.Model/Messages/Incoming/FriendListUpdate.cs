using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public enum FriendUpdateKind
{
    Removed = -1,
    Updated = 0,
    Added = 1
}

public sealed record FriendUpdateEntry(FriendUpdateKind Kind, Id RemovedId, Friend? Friend);

public sealed record FriendListUpdate(
    IReadOnlyList<FriendCategory> Categories,
    IReadOnlyList<FriendUpdateEntry> Updates) : IParserComposer<FriendListUpdate>
{
    public IEnumerable<Friend> Added => Entries(FriendUpdateKind.Added);
    public IEnumerable<Friend> Updated => Entries(FriendUpdateKind.Updated);
    public IEnumerable<long> Removed => Updates.Where(u => u.Kind == FriendUpdateKind.Removed).Select(u => (long)u.RemovedId);

    private IEnumerable<Friend> Entries(FriendUpdateKind kind) =>
        Updates.Where(u => u.Kind == kind && u.Friend is not null).Select(u => u.Friend!);

    public static FriendListUpdate Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static FriendListUpdate ParseFlash(in PacketReader p)
    {
        int categoryCount = p.ReadLength();
        var categories = new List<FriendCategory>(categoryCount);
        for (int i = 0; i < categoryCount; i++)
            categories.Add(p.Parse<FriendCategory>());

        int updateCount = p.ReadLength();
        var updates = new List<FriendUpdateEntry>(updateCount);
        for (int i = 0; i < updateCount; i++)
        {
            FriendUpdateKind kind = (FriendUpdateKind)p.ReadInt();
            updates.Add(kind switch
            {
                FriendUpdateKind.Removed =>
                    new FriendUpdateEntry(FriendUpdateKind.Removed, p.ReadId(), null),
                FriendUpdateKind.Updated or FriendUpdateKind.Added =>
                    new FriendUpdateEntry(kind, -1, p.Parse<Friend>()),
                _ => throw new InvalidDataException($"Unknown friend-list update type {(int)kind}.")
            });
        }

        return new FriendListUpdate(categories, updates);
    }

    public void Compose(in PacketWriter p)
    {
        foreach (FriendUpdateEntry entry in Updates)
            Validate(entry);

        FlashWire.Compose(this, in p, ComposeFlash);
    }

    private static void ComposeFlash(FriendListUpdate value, in PacketWriter p)
    {
        p.WriteLength((Length)value.Categories.Count);
        foreach (FriendCategory category in value.Categories)
            p.Compose(category);

        p.WriteLength((Length)value.Updates.Count);
        foreach (FriendUpdateEntry entry in value.Updates)
        {
            p.WriteInt((int)entry.Kind);
            if (entry.Kind == FriendUpdateKind.Removed)
                p.WriteId(entry.RemovedId);
            else
                p.Compose(entry.Friend!);
        }
    }

    private static void Validate(FriendUpdateEntry entry)
    {
        if (entry.Kind is not (FriendUpdateKind.Removed or FriendUpdateKind.Updated or FriendUpdateKind.Added))
            throw new InvalidDataException($"Unknown friend-list update type {(int)entry.Kind}.");
        if (entry.Kind is FriendUpdateKind.Removed && entry.Friend is not null)
            throw new InvalidDataException("A removed friend-list entry cannot contain a friend.");
        if (entry.Kind is not FriendUpdateKind.Removed && entry.Friend is null)
            throw new InvalidDataException("An added or updated friend-list entry must contain a friend.");
    }
}
