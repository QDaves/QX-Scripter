using Qx.Messages;

namespace Qx.Model;

/// <summary>
/// Specifies the relationship shown against a friend, which is what the client's heart, smile and
/// bobba buttons set.
/// </summary>
public enum RelationshipType
{
    /// <summary>No relationship set.</summary>
    /// <remarks>Setting it clears whatever was there.</remarks>
    None = 0,
    /// <summary>Heart.</summary>
    Heart = 1,
    /// <summary>Smile.</summary>
    Smile = 2,
    /// <summary>Bobba.</summary>
    Bobba = 3
}

/// <summary>Represents one relationship type on a user's profile with how many friends have it.</summary>
/// <param name="Type">The relationship type, using the <see cref="RelationshipType"/> numbering.</param>
/// <param name="FriendCount">The number of friends with this relationship.</param>
/// <param name="RandomFriendId">The identifier of one friend with this relationship, picked by the hotel.</param>
/// <param name="RandomFriendName">The name of that friend.</param>
/// <param name="RandomFriendFigure">The figure string of that friend.</param>
public sealed record RelationshipEntry(
    int Type,
    int FriendCount,
    Id RandomFriendId,
    string RandomFriendName,
    string RandomFriendFigure) : IParserComposer<RelationshipEntry>
{
    /// <summary>Reads a relationship entry from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    public static RelationshipEntry Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static RelationshipEntry ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadString());

    /// <summary>Writes the relationship entry to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when <see cref="RandomFriendId"/> does not fit in 32 bits or a string is too long.</exception>
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

/// <summary>Represents the relationships shown on a user's profile.</summary>
/// <remarks>Received as the Flash <c>RelationshipStatusInfo</c> message.</remarks>
public sealed record RelationshipStatus : IParserComposer<RelationshipStatus>
{
    private IReadOnlyList<RelationshipEntry> _entries =
        Array.AsReadOnly(Array.Empty<RelationshipEntry>());

    /// <summary>Initializes a new instance of the <see cref="RelationshipStatus"/> class.</summary>
    /// <param name="userId">The user whose relationships these are.</param>
    /// <param name="entries">The relationship entries; the list is copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> or any entry is <see langword="null"/>.</exception>
    public RelationshipStatus(Id userId, IReadOnlyList<RelationshipEntry> entries)
    {
        UserId = userId;
        Entries = entries;
    }

    /// <summary>Gets the user whose relationships these are.</summary>
    public Id UserId { get; init; }

    /// <summary>Gets the relationship entries as a read-only copy.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/> or a list with a <see langword="null"/> entry.</exception>
    public IReadOnlyList<RelationshipEntry> Entries
    {
        get => _entries;
        init => _entries = PeopleWire.FreezeReferences(value, nameof(Entries));
    }

    /// <summary>Deconstructs the status into its user and entries.</summary>
    /// <param name="userId">The user whose relationships these are.</param>
    /// <param name="entries">The relationship entries.</param>
    public void Deconstruct(out Id userId, out IReadOnlyList<RelationshipEntry> entries)
    {
        userId = UserId;
        entries = Entries;
    }

    /// <summary>Reads the relationship status from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the entry count is invalid or bytes remain after the entries.</exception>
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

    /// <summary>Writes the relationship status to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="InvalidDataException">Thrown when an identifier does not fit in 32 bits or a string is too long.</exception>
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
