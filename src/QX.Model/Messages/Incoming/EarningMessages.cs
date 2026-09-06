using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Where an earning came from.
/// </summary>
/// <remarks>
/// The values are positions in the client's own category table, which is what travels on the wire
/// and what a claim names. <see cref="All"/> is not a category the hotel ever reports; it is the
/// sentinel the client's claim-all button sends.
/// </remarks>
public enum EarningCategory
{
    /// <summary>Every category at once. Only ever sent, never received as a source.</summary>
    All = -1,
    Tutorial = 0,
    DailyGift = 1,
    Achievements = 2,
    Marketplace = 3,
    HabboClub = 4,
    LevelProgression = 5,
    RoomBundleSales = 6,
    BonusBag = 7,
    Donation = 8,
    Surprise = 9,
    Snowstorm = 10,
    Games = 11,
    WiredChest = 12,
    Agency = 13
}

/// <summary>What an earning pays out in.</summary>
public enum EarningRewardKind
{
    /// <summary>Duckets, the activity points the purse holds as type 0.</summary>
    Duckets = 0,
    /// <summary>Credits.</summary>
    Credits = 1
}

/// <summary>
/// One line of the earnings vault: a category, what it pays and how much of it.
/// </summary>
/// <remarks>
/// A category can hold several lines. The client adds up the ones that share a kind and shows one
/// figure per category per kind, which is what <see cref="EarningStatus"/> reproduces.
/// </remarks>
public sealed record EarningEntry : IParserComposer<EarningEntry>
{
    private string _product_code = "";

    /// <param name="Category">Where the earning came from.</param>
    /// <param name="Kind">Whether the amount is duckets or credits.</param>
    /// <param name="Amount">How much is waiting.</param>
    /// <param name="ProductCode">
    /// The product this line hands over, empty when the line is plain currency. The client counts these
    /// rather than adding them up, because one line is one item however large its amount reads.
    /// </param>
    public EarningEntry(
        EarningCategory Category,
        EarningRewardKind Kind,
        int Amount,
        string ProductCode)
    {
        this.Category = Category;
        this.Kind = Kind;
        this.Amount = Amount;
        this.ProductCode = ProductCode;
    }

    public EarningCategory Category { get; init; }

    public EarningRewardKind Kind { get; init; }

    public int Amount { get; init; }

    public string ProductCode
    {
        get => _product_code;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(ProductCode));
            _product_code = value;
        }
    }

    public void Deconstruct(
        out EarningCategory Category,
        out EarningRewardKind Kind,
        out int Amount,
        out string ProductCode)
    {
        Category = this.Category;
        Kind = this.Kind;
        Amount = this.Amount;
        ProductCode = this.ProductCode;
    }

    /// <summary>Whether this line hands over an item rather than currency.</summary>
    public bool IsProduct => ProductCode.Length > 0;

    public static EarningEntry Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static EarningEntry ParseFlash(in PacketReader p) => ParseRoot(in p);

    private static EarningEntry ParseUnity(in PacketReader p) => ParseRoot(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(EarningEntry value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    private static void ComposeUnity(EarningEntry value, in PacketWriter p) =>
        ComposeRoot(value, in p);

    internal static EarningEntry ParseWire(
        in PacketReader p,
        int trailing_bytes,
        ref EarningStringBudget strings)
    {
        EarningWire.RequireRemaining(
            in p,
            EarningWire.EntryMinimumBytes,
            trailing_bytes,
            nameof(EarningEntry));
        // Read as signed: the category is a byte on the wire and the client reads it back through
        // AS3's readByte, which sign-extends. Reading it unsigned would turn the claim-all sentinel
        // into category 255 instead of -1.
        EarningCategory category = (EarningCategory)(sbyte)p.ReadByte();
        EarningRewardKind kind = (EarningRewardKind)(sbyte)p.ReadByte();
        int amount = p.ReadInt();
        string product_code = strings.Read(in p, nameof(ProductCode), trailing_bytes);
        return new EarningEntry(category, kind, amount, product_code);
    }

    internal static EarningEntryWireSnapshot PrepareWire(
        EarningEntry value,
        ref EarningStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var snapshot = new EarningEntryWireSnapshot(
            value.Category,
            value.Kind,
            value.Amount,
            value.ProductCode);
        strings.Require(snapshot.ProductCode, nameof(ProductCode), in p);
        return snapshot;
    }

    internal static void WriteWire(EarningEntryWireSnapshot value, in PacketWriter p)
    {
        p.WriteByte(unchecked((byte)(sbyte)value.Category));
        p.WriteByte(unchecked((byte)(sbyte)value.Kind));
        p.WriteInt(value.Amount);
        p.WriteString(value.ProductCode);
    }

    private static EarningEntry ParseRoot(in PacketReader p)
    {
        var strings = EarningWire.NewStringBudget();
        EarningEntry value = ParseWire(in p, 0, ref strings);
        EarningWire.RequireEmpty(in p, nameof(EarningEntry));
        return value;
    }

    private static void ComposeRoot(EarningEntry value, in PacketWriter p)
    {
        var strings = EarningWire.NewStringBudget();
        EarningEntryWireSnapshot snapshot = PrepareWire(value, ref strings, in p);
        WriteWire(snapshot, in p);
    }
}

internal readonly record struct EarningEntryWireSnapshot(
    EarningCategory Category,
    EarningRewardKind Kind,
    int Amount,
    string ProductCode);

/// <summary>
/// Everything waiting to be claimed, sent in answer to a request and after every claim.
/// </summary>
/// <remarks>
/// Flash counts the lines in four bytes. Unity sends them through its generic array reader, which
/// takes its width from a field the reader is built with — two bytes unless the message says
/// otherwise, and this one does not. The lines themselves are identical on both.
/// </remarks>
public sealed record EarningStatus : IParserComposer<EarningStatus>
{
    private IReadOnlyList<EarningEntry> _entries =
        Array.AsReadOnly(Array.Empty<EarningEntry>());

    /// <param name="Entries">The lines, in the order the hotel sent them.</param>
    public EarningStatus(IReadOnlyList<EarningEntry> Entries)
    {
        this.Entries = Entries;
    }

    public IReadOnlyList<EarningEntry> Entries
    {
        get => _entries;
        init => _entries = EarningWire.FreezeReferences(value, nameof(Entries));
    }

    public void Deconstruct(out IReadOnlyList<EarningEntry> Entries)
    {
        Entries = this.Entries;
    }

    /// <summary>The categories that carry at least one line, in the order they first appear.</summary>
    public IReadOnlyList<EarningCategory> Categories
    {
        get
        {
            var seen = new List<EarningCategory>();
            foreach (EarningEntry entry in Entries)
            {
                if (!seen.Contains(entry.Category))
                    seen.Add(entry.Category);
            }
            return seen;
        }
    }

    /// <summary>How many credits one category is holding.</summary>
    /// <param name="category">The category, or <see cref="EarningCategory.All"/> for every one.</param>
    public int Credits(EarningCategory category = EarningCategory.All) =>
        Sum(category, EarningRewardKind.Credits);

    /// <summary>How many duckets one category is holding.</summary>
    /// <param name="category">The category, or <see cref="EarningCategory.All"/> for every one.</param>
    public int Duckets(EarningCategory category = EarningCategory.All) =>
        Sum(category, EarningRewardKind.Duckets);

    /// <summary>
    /// How many items one category is holding.
    /// </summary>
    /// <remarks>
    /// Counted, not added up: the client draws one product per line whatever amount the line
    /// carries, so a line's amount says nothing about how many items it is worth.
    /// </remarks>
    /// <param name="category">The category, or <see cref="EarningCategory.All"/> for every one.</param>
    public int Products(EarningCategory category = EarningCategory.All) =>
        Entries.Count(entry => entry.IsProduct && (category == EarningCategory.All || entry.Category == category));

    /// <summary>The lines of one category.</summary>
    /// <param name="category">The category, or <see cref="EarningCategory.All"/> for every one.</param>
    public IReadOnlyList<EarningEntry> For(EarningCategory category) =>
        category == EarningCategory.All
            ? [.. Entries]
            : [.. Entries.Where(entry => entry.Category == category)];

    /// <summary>
    /// Whether a category has anything worth pressing claim for.
    /// </summary>
    /// <remarks>
    /// Duckets alone do not count, which is the client's own rule: its purse indicator lights up
    /// only for a line that is not duckets and carries an amount.
    /// </remarks>
    /// <param name="category">The category, or <see cref="EarningCategory.All"/> for every one.</param>
    public bool HasClaimable(EarningCategory category = EarningCategory.All) =>
        Entries.Any(entry =>
            (category == EarningCategory.All || entry.Category == category) &&
            entry.Kind != EarningRewardKind.Duckets &&
            entry.Amount > 0);

    private int Sum(EarningCategory category, EarningRewardKind kind)
    {
        int total = 0;
        foreach (EarningEntry entry in Entries)
        {
            if (entry.Kind == kind && (category == EarningCategory.All || entry.Category == category))
                total += entry.Amount;
        }
        return total;
    }

    public static EarningStatus Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static EarningStatus ParseFlash(in PacketReader p) => ParseEntries(in p);

    private static EarningStatus ParseUnity(in PacketReader p) => ParseEntries(in p);

    private static EarningStatus ParseEntries(in PacketReader p)
    {
        int count = EarningWire.ReadCount(
            in p,
            EarningWire.EntryMinimumBytes,
            0,
            nameof(Entries));
        var strings = EarningWire.NewStringBudget();
        var entries = new EarningEntry[count];
        for (int index = 0; index < entries.Length; index++)
        {
            int sibling_bytes = checked(
                (entries.Length - index - 1) * EarningWire.EntryMinimumBytes);
            entries[index] = EarningEntry.ParseWire(
                in p,
                sibling_bytes,
                ref strings);
        }
        EarningWire.RequireEmpty(in p, nameof(EarningStatus));
        return new EarningStatus(entries);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(EarningStatus value, in PacketWriter p) =>
        ComposeEntries(value, in p);

    private static void ComposeUnity(EarningStatus value, in PacketWriter p) =>
        ComposeEntries(value, in p);

    private static void ComposeEntries(EarningStatus value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        IReadOnlyList<EarningEntry> values = value.Entries;
        int count = EarningWire.RequireListCount(values, nameof(value.Entries));
        var strings = EarningWire.NewStringBudget();
        var entries = new EarningEntryWireSnapshot[count];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = EarningEntry.PrepareWire(
                values[index],
                ref strings,
                in p);
        }

        EarningWire.WriteCount(entries.Length, in p);
        foreach (EarningEntryWireSnapshot entry in entries)
            EarningEntry.WriteWire(entry, in p);
    }
}

/// <summary>
/// The hotel's answer to a claim.
/// </summary>
/// <param name="Category">
/// The category that was claimed, echoed back. <see cref="EarningCategory.All"/> when the claim was
/// for everything.
/// </param>
/// <param name="Success">Whether the claim went through.</param>
public sealed record EarningClaimResult(EarningCategory Category, bool Success)
    : IParserComposer<EarningClaimResult>
{
    /// <summary>Whether this answers a claim that asked for every category.</summary>
    public bool IsClaimAll => Category == EarningCategory.All;

    public static EarningClaimResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static EarningClaimResult ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static EarningClaimResult ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static EarningClaimResult ParseMessage(in PacketReader p)
    {
        EarningWire.RequireRemaining(in p, sizeof(byte) * 2, 0, nameof(EarningClaimResult));
        var value = new EarningClaimResult(
            (EarningCategory)(sbyte)p.ReadByte(),
            p.ReadBool());
        EarningWire.RequireEmpty(in p, nameof(EarningClaimResult));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(EarningClaimResult value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeUnity(EarningClaimResult value, in PacketWriter p) =>
        ComposeMessage(value, in p);

    private static void ComposeMessage(EarningClaimResult value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteByte(unchecked((byte)(sbyte)value.Category));
        p.WriteBool(value.Success);
    }
}

/// <summary>
/// An unprompted note that a category has something new waiting.
/// </summary>
/// <remarks>
/// Flash only; the Unity build declares no counterpart. The client answers it by re-requesting the
/// status, because the note carries the category and nothing else.
/// </remarks>
/// <param name="Category">The category that gained something.</param>
public sealed record EarningNotification(EarningCategory Category)
    : IParserComposer<EarningNotification>
{
    public static EarningNotification Parse(in PacketReader p) =>
        ModernWireClients.ParseFlash(in p, ParseFlash);

    private static EarningNotification ParseFlash(in PacketReader p)
    {
        EarningWire.RequireRemaining(in p, sizeof(byte), 0, nameof(EarningNotification));
        var value = new EarningNotification((EarningCategory)(sbyte)p.ReadByte());
        EarningWire.RequireEmpty(in p, nameof(EarningNotification));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.ComposeFlash(this, in p, ComposeFlash);

    private static void ComposeFlash(EarningNotification value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteByte(unchecked((byte)(sbyte)value.Category));
    }
}
