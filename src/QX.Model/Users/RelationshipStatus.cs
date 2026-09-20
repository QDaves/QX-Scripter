using Qx.Messages;

namespace Qx.Model;

/// <summary>
/// The relationship shown against a friend, which is what the client's heart, smile and bobba
/// buttons set.
/// </summary>
public enum RelationshipType
{
    /// <summary>No relationship set. Clears whatever was there.</summary>
    None = 0,
    /// <summary>Heart.</summary>
    Heart = 1,
    /// <summary>Smile.</summary>
    Smile = 2,
    /// <summary>Bobba.</summary>
    Bobba = 3
}

public sealed record RelationshipEntry(
    int Type,
    int FriendCount,
    Id RandomFriendId,
    string RandomFriendName,
    string RandomFriendFigure) : IParserComposer<RelationshipEntry>
{
    public static RelationshipEntry Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RelationshipEntry ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RelationshipEntry value, in PacketWriter p)
    {
        Validate(value, in p);
        p.WriteInt(value.Type);
        p.WriteInt(value.FriendCount);
        p.WriteInt(PeopleWire.RequireFlashId(value.RandomFriendId, nameof(RandomFriendId)));
        p.WriteString(value.RandomFriendName);
        p.WriteString(value.RandomFriendFigure);
    }

    internal static void Validate(RelationshipEntry value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = PeopleWire.RequireFlashId(value.RandomFriendId, nameof(RandomFriendId));
        PeopleWire.RequireString(value.RandomFriendName, nameof(RandomFriendName), in p);
        PeopleWire.RequireString(value.RandomFriendFigure, nameof(RandomFriendFigure), in p);
    }
}

public sealed record RelationshipStatus : IParserComposer<RelationshipStatus>
{
    private IReadOnlyList<RelationshipEntry> _entries =
        Array.AsReadOnly(Array.Empty<RelationshipEntry>());

    public RelationshipStatus(Id UserId, IReadOnlyList<RelationshipEntry> Entries)
    {
        this.UserId = UserId;
        this.Entries = Entries;
    }

    public Id UserId { get; init; }

    public IReadOnlyList<RelationshipEntry> Entries
    {
        get => _entries;
        init => _entries = PeopleWire.FreezeReferences(value, nameof(Entries));
    }

    public void Deconstruct(out Id UserId, out IReadOnlyList<RelationshipEntry> Entries)
    {
        UserId = this.UserId;
        Entries = this.Entries;
    }

    public static RelationshipStatus Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RelationshipStatus ParseFlash(in PacketReader p)
    {
        Id user_id = p.ReadInt();
        int count = PeopleWire.ReadFlashCount(
            in p,
            PeopleWire.FlashRelationshipEntryMinimumBytes,
            nameof(Entries));
        var entries = new RelationshipEntry[count];
        for (int index = 0; index < entries.Length; index++)
            entries[index] = p.Parse<RelationshipEntry>();
        PeopleWire.RequireEmpty(in p, nameof(RelationshipStatus));
        return new RelationshipStatus(user_id, entries);
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(RelationshipStatus value, in PacketWriter p)
    {
        RelationshipStatus prepared = Prepare(value, in p);
        p.WriteInt(PeopleWire.RequireFlashId(prepared.UserId, nameof(UserId)));
        p.WriteInt(prepared.Entries.Count);
        foreach (RelationshipEntry entry in prepared.Entries)
            p.Compose(entry);
    }

    private static RelationshipStatus Prepare(
        RelationshipStatus value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        RelationshipEntry[] entries = PeopleWire.SnapshotReferences(
            value.Entries,
            nameof(Entries));
        _ = PeopleWire.RequireFlashId(value.UserId, nameof(UserId));
        foreach (RelationshipEntry entry in entries)
            RelationshipEntry.Validate(entry, in p);
        return new RelationshipStatus(value.UserId, entries);
    }
}
