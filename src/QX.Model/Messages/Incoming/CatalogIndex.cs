using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record CatalogNode : IParserComposer<CatalogNode>
{
    private string _page_name = "";
    private string _localization = "";
    private IReadOnlyList<int> _offer_ids = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<CatalogNode> _children = Array.AsReadOnly(Array.Empty<CatalogNode>());

    public CatalogNode(
        bool Visible,
        int Icon,
        int PageId,
        string PageName,
        string Localization,
        IReadOnlyList<int> OfferIds,
        IReadOnlyList<CatalogNode> Children)
    {
        this.Visible = Visible;
        this.Icon = Icon;
        this.PageId = PageId;
        this.PageName = PageName;
        this.Localization = Localization;
        this.OfferIds = OfferIds;
        this.Children = Children;
    }

    private CatalogNode(
        bool visible,
        int icon,
        int page_id,
        string page_name,
        string localization,
        int[] offer_ids,
        CatalogNode[] children)
    {
        Visible = visible;
        Icon = icon;
        PageId = page_id;
        _page_name = page_name;
        _localization = localization;
        _offer_ids = Array.AsReadOnly(offer_ids);
        _children = Array.AsReadOnly(children);
    }

    public bool Visible { get; init; }

    public int Icon { get; init; }

    public int PageId { get; init; }

    public string PageName
    {
        get => _page_name;
        init => _page_name = CatalogWire.RequireReference(value, nameof(PageName));
    }

    public string Localization
    {
        get => _localization;
        init => _localization = CatalogWire.RequireReference(value, nameof(Localization));
    }

    public IReadOnlyList<int> OfferIds
    {
        get => _offer_ids;
        init => _offer_ids = CatalogWire.FreezeValues(
            value,
            CatalogWire.MaximumCollectionCount,
            nameof(OfferIds));
    }

    public IReadOnlyList<CatalogNode> Children
    {
        get => _children;
        init => _children = CatalogWire.FreezeReferences(
            value,
            CatalogWire.MaximumCollectionCount,
            nameof(Children));
    }

    public static CatalogNode Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogNode ParseFlash(in PacketReader p) =>
        CatalogIndexWire.ParseStandaloneNode(in p);

    private static CatalogNode ParseUnity(in PacketReader p) =>
        CatalogIndexWire.ParseStandaloneNode(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogNode value, in PacketWriter p) =>
        CatalogIndexWire.ComposeNode(value, in p);

    private static void ComposeUnity(CatalogNode value, in PacketWriter p) =>
        CatalogIndexWire.ComposeNode(value, in p);

    internal static CatalogNode FromOwned(
        bool visible,
        int icon,
        int page_id,
        string page_name,
        string localization,
        int[] offer_ids,
        CatalogNode[] children) =>
        new(visible, icon, page_id, page_name, localization, offer_ids, children);

    public void Deconstruct(
        out bool Visible,
        out int Icon,
        out int PageId,
        out string PageName,
        out string Localization,
        out IReadOnlyList<int> OfferIds,
        out IReadOnlyList<CatalogNode> Children)
    {
        Visible = this.Visible;
        Icon = this.Icon;
        PageId = this.PageId;
        PageName = this.PageName;
        Localization = this.Localization;
        OfferIds = this.OfferIds;
        Children = this.Children;
    }
}

public sealed record CatalogIndex : IParserComposer<CatalogIndex>
{
    private CatalogNode _root;
    private string _catalog_type = "";

    public CatalogIndex(CatalogNode Root, bool NewAdditionsAvailable, string CatalogType)
    {
        _root = CatalogWire.RequireReference(Root, nameof(Root));
        this.NewAdditionsAvailable = NewAdditionsAvailable;
        _catalog_type = CatalogWire.RequireReference(CatalogType, nameof(CatalogType));
    }

    public CatalogNode Root
    {
        get => _root;
        init => _root = CatalogWire.RequireReference(value, nameof(Root));
    }

    public bool NewAdditionsAvailable { get; init; }

    public string CatalogType
    {
        get => _catalog_type;
        init => _catalog_type = CatalogWire.RequireReference(value, nameof(CatalogType));
    }

    public static CatalogIndex Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static CatalogIndex ParseFlash(in PacketReader p) =>
        CatalogIndexWire.ParseIndex(in p);

    private static CatalogIndex ParseUnity(in PacketReader p) =>
        CatalogIndexWire.ParseIndex(in p);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(CatalogIndex value, in PacketWriter p) =>
        CatalogIndexWire.ComposeIndex(value, in p);

    private static void ComposeUnity(CatalogIndex value, in PacketWriter p) =>
        CatalogIndexWire.ComposeIndex(value, in p);

    public void Deconstruct(
        out CatalogNode Root,
        out bool NewAdditionsAvailable,
        out string CatalogType)
    {
        Root = this.Root;
        NewAdditionsAvailable = this.NewAdditionsAvailable;
        CatalogType = this.CatalogType;
    }
}

internal static class CatalogIndexWire
{
    internal const int MaximumDepth = 64;
    internal const int MaximumNodes = 16_384;
    internal const int MaximumOfferIds = 262_144;
    internal const int MaximumStrings = MaximumNodes * 2 + 1;
    internal const int MaximumStringBytes = 8 * 1024 * 1024;

    public static CatalogNode ParseStandaloneNode(in PacketReader p)
    {
        var budget = new CatalogIndexBudget();
        var strings = new CatalogStringBudget(MaximumStrings, MaximumStringBytes);
        return ParseNode(in p, 1, 0, ref budget, ref strings);
    }

    public static CatalogIndex ParseIndex(in PacketReader p)
    {
        var budget = new CatalogIndexBudget();
        var strings = new CatalogStringBudget(MaximumStrings, MaximumStringBytes);
        CatalogNode root = ParseNode(
            in p,
            1,
            sizeof(byte) + CatalogWire.StringMinimumBytes,
            ref budget,
            ref strings);
        bool new_additions_available = p.ReadBool();
        string catalog_type = strings.Read(in p, nameof(CatalogIndex.CatalogType));
        CatalogWire.RequireEmpty(in p, nameof(CatalogIndex));
        return new CatalogIndex(root, new_additions_available, catalog_type);
    }

    public static void ComposeNode(CatalogNode value, in PacketWriter p)
    {
        var budget = new CatalogIndexBudget();
        var strings = new CatalogStringBudget(MaximumStrings, MaximumStringBytes);
        CatalogNode prepared = PrepareNode(value, 1, ref budget, ref strings, in p);
        WriteNode(prepared, in p);
    }

    public static void ComposeIndex(CatalogIndex value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var budget = new CatalogIndexBudget();
        var strings = new CatalogStringBudget(MaximumStrings, MaximumStringBytes);
        CatalogNode root = PrepareNode(value.Root, 1, ref budget, ref strings, in p);
        strings.Require(value.CatalogType, nameof(CatalogIndex.CatalogType), in p);
        WriteNode(root, in p);
        p.WriteBool(value.NewAdditionsAvailable);
        p.WriteString(value.CatalogType);
    }

    private static CatalogNode ParseNode(
        in PacketReader p,
        int depth,
        int trailing_bytes,
        ref CatalogIndexBudget budget,
        ref CatalogStringBudget strings)
    {
        budget.TakeNode(depth);
        bool visible = p.ReadBool();
        int icon = p.ReadInt();
        int page_id = p.ReadInt();
        string page_name = strings.Read(in p, nameof(CatalogNode.PageName));
        string localization = strings.Read(in p, nameof(CatalogNode.Localization));
        int count_width = CatalogWire.CountWidth(p.Client);
        int offer_count = CatalogWire.ReadCount(
            in p,
            sizeof(int),
            checked(trailing_bytes + count_width),
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.OfferIds));
        budget.TakeOfferIds(offer_count);
        var offer_ids = new int[offer_count];
        for (int index = 0; index < offer_ids.Length; index++)
            offer_ids[index] = p.ReadInt();

        int child_count = CatalogWire.ReadCount(
            in p,
            MinimumNodeBytes(p.Client),
            trailing_bytes,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.Children));
        budget.ReserveNodes(child_count);
        var children = new CatalogNode[child_count];
        int minimum_node_bytes = MinimumNodeBytes(p.Client);
        for (int index = 0; index < children.Length; index++)
        {
            int sibling_bytes = checked((children.Length - index - 1) * minimum_node_bytes);
            children[index] = ParseNode(
                in p,
                depth + 1,
                checked(trailing_bytes + sibling_bytes),
                ref budget,
                ref strings);
        }

        return CatalogNode.FromOwned(
            visible,
            icon,
            page_id,
            page_name,
            localization,
            offer_ids,
            children);
    }

    private static CatalogNode PrepareNode(
        CatalogNode value,
        int depth,
        ref CatalogIndexBudget budget,
        ref CatalogStringBudget strings,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        budget.TakeNode(depth);
        strings.Require(value.PageName, nameof(CatalogNode.PageName), in p);
        strings.Require(value.Localization, nameof(CatalogNode.Localization), in p);

        int offer_count = CatalogWire.RequireListCount(
            value.OfferIds,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.OfferIds));
        budget.TakeOfferIds(offer_count);
        int[] offer_ids = CatalogWire.SnapshotValues(
            value.OfferIds,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.OfferIds));

        int child_count = CatalogWire.RequireListCount(
            value.Children,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.Children));
        budget.ReserveNodes(child_count);
        CatalogNode[] source_children = CatalogWire.SnapshotReferences(
            value.Children,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.Children));
        var children = new CatalogNode[source_children.Length];
        for (int index = 0; index < children.Length; index++)
        {
            children[index] = PrepareNode(
                source_children[index],
                depth + 1,
                ref budget,
                ref strings,
                in p);
        }

        return CatalogNode.FromOwned(
            value.Visible,
            value.Icon,
            value.PageId,
            value.PageName,
            value.Localization,
            offer_ids,
            children);
    }

    private static void WriteNode(CatalogNode value, in PacketWriter p)
    {
        p.WriteBool(value.Visible);
        p.WriteInt(value.Icon);
        p.WriteInt(value.PageId);
        p.WriteString(value.PageName);
        p.WriteString(value.Localization);
        CatalogWire.WriteCount(value.OfferIds.Count, in p);
        foreach (int offer_id in value.OfferIds)
            p.WriteInt(offer_id);
        CatalogWire.WriteCount(value.Children.Count, in p);
        foreach (CatalogNode child in value.Children)
            WriteNode(child, in p);
    }

    private static int MinimumNodeBytes(ClientType client) =>
        sizeof(byte) + sizeof(int) + sizeof(int) + CatalogWire.StringMinimumBytes * 2 +
        CatalogWire.CountWidth(client) * 2;
}

internal struct CatalogIndexBudget
{
    private int _nodes;
    private int _reserved_nodes;
    private int _offer_ids;

    public void TakeNode(int depth)
    {
        if (depth > CatalogIndexWire.MaximumDepth)
        {
            throw new InvalidDataException(
                $"Catalog index depth {depth} exceeds the limit {CatalogIndexWire.MaximumDepth}.");
        }
        if (_reserved_nodes > 0)
            _reserved_nodes--;
        if (_nodes >= CatalogIndexWire.MaximumNodes)
        {
            throw new InvalidDataException(
                $"Catalog index node count exceeds the limit {CatalogIndexWire.MaximumNodes}.");
        }
        _nodes++;
    }

    public void ReserveNodes(int count)
    {
        if (count > CatalogIndexWire.MaximumNodes - _nodes - _reserved_nodes)
        {
            throw new InvalidDataException(
                $"Catalog index node count exceeds the limit {CatalogIndexWire.MaximumNodes}.");
        }
        _reserved_nodes += count;
    }

    public void TakeOfferIds(int count)
    {
        if (count > CatalogIndexWire.MaximumOfferIds - _offer_ids)
        {
            throw new InvalidDataException(
                $"Catalog index offer-id count exceeds the limit {CatalogIndexWire.MaximumOfferIds}.");
        }
        _offer_ids += count;
    }
}
