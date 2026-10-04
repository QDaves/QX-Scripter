using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>Represents a node in the catalog index tree.</summary>
public sealed record CatalogNode : IParserComposer<CatalogNode>
{
    private string _page_name = "";
    private string _localization = "";
    private IReadOnlyList<int> _offer_ids = Array.AsReadOnly(Array.Empty<int>());
    private IReadOnlyList<CatalogNode> _children = Array.AsReadOnly(Array.Empty<CatalogNode>());

    /// <summary>Initializes a new instance of the <see cref="CatalogNode"/> record.</summary>
    /// <param name="visible">Whether the node is visible in the catalog.</param>
    /// <param name="icon">The icon identifier of the node.</param>
    /// <param name="pageId">The identifier of the catalog page.</param>
    /// <param name="pageName">The internal name of the page.</param>
    /// <param name="localization">The localized caption of the page.</param>
    /// <param name="offerIds">The identifiers of the offers on the page, copied into a read only list.</param>
    /// <param name="children">The child nodes, copied into a read only list.</param>
    public CatalogNode(
        bool visible,
        int icon,
        int pageId,
        string pageName,
        string localization,
        IReadOnlyList<int> offerIds,
        IReadOnlyList<CatalogNode> children)
    {
        Visible = visible;
        Icon = icon;
        PageId = pageId;
        PageName = pageName;
        Localization = localization;
        OfferIds = offerIds;
        Children = children;
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

    /// <summary>Gets whether the node is visible in the catalog.</summary>
    public bool Visible { get; init; }

    /// <summary>Gets the icon identifier of the node.</summary>
    public int Icon { get; init; }

    /// <summary>Gets the identifier of the catalog page.</summary>
    public int PageId { get; init; }

    /// <summary>Gets the internal name of the page.</summary>
    public string PageName
    {
        get => _page_name;
        init => _page_name = CatalogWire.RequireReference(value, nameof(PageName));
    }

    /// <summary>Gets the localized caption of the page.</summary>
    public string Localization
    {
        get => _localization;
        init => _localization = CatalogWire.RequireReference(value, nameof(Localization));
    }

    /// <summary>Gets the identifiers of the offers on the page, as a read only copy.</summary>
    public IReadOnlyList<int> OfferIds
    {
        get => _offer_ids;
        init => _offer_ids = CatalogWire.FreezeValues(
            value,
            CatalogWire.MaximumCollectionCount,
            nameof(OfferIds));
    }

    /// <summary>Gets the child nodes, as a read only copy.</summary>
    public IReadOnlyList<CatalogNode> Children
    {
        get => _children;
        init => _children = CatalogWire.FreezeReferences(
            value,
            CatalogWire.MaximumCollectionCount,
            nameof(Children));
    }

    /// <summary>Parses a catalog node and its children from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CatalogNode Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogNode ParseFlash(in PacketReader p) =>
        CatalogIndexWire.ParseStandaloneNode(in p);

    /// <summary>Composes the catalog node and its children into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogNode value, in PacketWriter p) =>
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

    /// <summary>Deconstructs the node into its parts.</summary>
    /// <param name="visible">Whether the node is visible in the catalog.</param>
    /// <param name="icon">The icon identifier of the node.</param>
    /// <param name="pageId">The identifier of the catalog page.</param>
    /// <param name="pageName">The internal name of the page.</param>
    /// <param name="localization">The localized caption of the page.</param>
    /// <param name="offerIds">The identifiers of the offers on the page.</param>
    /// <param name="children">The child nodes.</param>
    public void Deconstruct(
        out bool visible,
        out int icon,
        out int pageId,
        out string pageName,
        out string localization,
        out IReadOnlyList<int> offerIds,
        out IReadOnlyList<CatalogNode> children)
    {
        visible = Visible;
        icon = Icon;
        pageId = PageId;
        pageName = PageName;
        localization = Localization;
        offerIds = OfferIds;
        children = Children;
    }
}

/// <summary>Represents the <c>CatalogIndex</c> message, received with the page tree of the catalog.</summary>
public sealed record CatalogIndex : IParserComposer<CatalogIndex>
{
    private CatalogNode _root;
    private string _catalog_type = "";

    /// <summary>Initializes a new instance of the <see cref="CatalogIndex"/> record.</summary>
    /// <param name="root">The root node of the page tree.</param>
    /// <param name="newAdditionsAvailable">Whether the catalog has new additions.</param>
    /// <param name="catalogType">The type of the catalog, such as <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="root"/> or <paramref name="catalogType"/> is <see langword="null"/>.
    /// </exception>
    public CatalogIndex(CatalogNode root, bool newAdditionsAvailable, string catalogType)
    {
        _root = CatalogWire.RequireReference(root, nameof(root));
        NewAdditionsAvailable = newAdditionsAvailable;
        _catalog_type = CatalogWire.RequireReference(catalogType, nameof(catalogType));
    }

    /// <summary>Gets the root node of the page tree.</summary>
    public CatalogNode Root
    {
        get => _root;
        init => _root = CatalogWire.RequireReference(value, nameof(Root));
    }

    /// <summary>Gets whether the catalog has new additions.</summary>
    public bool NewAdditionsAvailable { get; init; }

    /// <summary>
    /// Gets the type of the catalog, such as <c>NORMAL</c> or <c>BUILDERS_CLUB</c>.
    /// </summary>
    public string CatalogType
    {
        get => _catalog_type;
        init => _catalog_type = CatalogWire.RequireReference(value, nameof(CatalogType));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    /// <remarks>The parser accepts at most 64 levels of nesting and 16384 nodes.</remarks>
    public static CatalogIndex Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static CatalogIndex ParseFlash(in PacketReader p) =>
        CatalogIndexWire.ParseIndex(in p);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(CatalogIndex value, in PacketWriter p) =>
        CatalogIndexWire.ComposeIndex(value, in p);

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="root">The root node of the page tree.</param>
    /// <param name="newAdditionsAvailable">Whether the catalog has new additions.</param>
    /// <param name="catalogType">The type of the catalog.</param>
    public void Deconstruct(
        out CatalogNode root,
        out bool newAdditionsAvailable,
        out string catalogType)
    {
        root = Root;
        newAdditionsAvailable = NewAdditionsAvailable;
        catalogType = CatalogType;
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
        int count_width = CatalogWire.CountWidth;
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
            MinimumNodeBytes,
            trailing_bytes,
            CatalogWire.MaximumCollectionCount,
            nameof(CatalogNode.Children));
        budget.ReserveNodes(child_count);
        var children = new CatalogNode[child_count];
        int minimum_node_bytes = MinimumNodeBytes;
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

    private const int MinimumNodeBytes =
        sizeof(byte) + sizeof(int) + sizeof(int) + CatalogWire.StringMinimumBytes * 2 +
        CatalogWire.CountWidth * 2;
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
