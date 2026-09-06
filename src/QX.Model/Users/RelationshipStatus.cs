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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static RelationshipEntry ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt(), p.ReadString(), p.ReadString());

    private static RelationshipEntry ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadLong(), p.ReadString(), p.ReadString());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RelationshipEntry value, in PacketWriter p)
    {
        Validate(value, true, in p);
        p.WriteInt(value.Type);
        p.WriteInt(value.FriendCount);
        p.WriteInt(PeopleWire.RequireFlashId(value.RandomFriendId, nameof(RandomFriendId)));
        p.WriteString(value.RandomFriendName);
        p.WriteString(value.RandomFriendFigure);
    }

    private static void ComposeUnity(RelationshipEntry value, in PacketWriter p)
    {
        Validate(value, false, in p);
        p.WriteInt(value.Type);
        p.WriteInt(value.FriendCount);
        p.WriteLong(value.RandomFriendId);
        p.WriteString(value.RandomFriendName);
        p.WriteString(value.RandomFriendFigure);
    }

    internal static void Validate(RelationshipEntry value, bool flash, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (flash)
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static RelationshipStatus ParseUnity(in PacketReader p)
    {
        Id user_id = p.ReadLong();
        int count = PeopleWire.ReadUnityCount(
            in p,
            PeopleWire.UnityRelationshipEntryMinimumBytes,
            nameof(Entries));
        var entries = new RelationshipEntry[count];
        for (int index = 0; index < entries.Length; index++)
            entries[index] = p.Parse<RelationshipEntry>();
        PeopleWire.RequireEmpty(in p, nameof(RelationshipStatus));
        return new RelationshipStatus(user_id, entries);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(RelationshipStatus value, in PacketWriter p)
    {
        RelationshipStatus prepared = Prepare(value, true, in p);
        p.WriteInt(PeopleWire.RequireFlashId(prepared.UserId, nameof(UserId)));
        p.WriteInt(prepared.Entries.Count);
        foreach (RelationshipEntry entry in prepared.Entries)
            p.Compose(entry);
    }

    private static void ComposeUnity(RelationshipStatus value, in PacketWriter p)
    {
        RelationshipStatus prepared = Prepare(value, false, in p);
        p.WriteLong(prepared.UserId);
        PeopleWire.WriteUnityCount(prepared.Entries.Count, in p);
        foreach (RelationshipEntry entry in prepared.Entries)
            p.Compose(entry);
    }

    private static RelationshipStatus Prepare(
        RelationshipStatus value,
        bool flash,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        RelationshipEntry[] entries = PeopleWire.SnapshotReferences(
            value.Entries,
            nameof(Entries));
        if (flash)
            _ = PeopleWire.RequireFlashId(value.UserId, nameof(UserId));
        else
            PeopleWire.RequireUnityCount(entries.Length, nameof(Entries));
        foreach (RelationshipEntry entry in entries)
            RelationshipEntry.Validate(entry, flash, in p);
        return new RelationshipStatus(value.UserId, entries);
    }
}
