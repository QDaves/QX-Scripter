using Qx.Messages;

namespace Qx.Model.Forums;

public sealed record ForumPost(
    Id MessageId,
    int MessageIndex,
    Id AuthorId,
    string AuthorName,
    string AuthorFigure,
    int CreationSecondsAgo,
    string Text,
    byte State,
    Id AdminId,
    string AdminName,
    int AdminOperationSecondsAgo,
    int AuthorPostCount) : IParserComposer<ForumPost>
{
    private string author_name = AuthorName ?? throw new ArgumentNullException(nameof(AuthorName));
    private string author_figure = AuthorFigure ?? throw new ArgumentNullException(nameof(AuthorFigure));
    private string text = Text ?? throw new ArgumentNullException(nameof(Text));
    private string admin_name = AdminName ?? throw new ArgumentNullException(nameof(AdminName));

    public string AuthorName
    {
        get => author_name;
        init => author_name = value ?? throw new ArgumentNullException(nameof(AuthorName));
    }

    public string AuthorFigure
    {
        get => author_figure;
        init => author_figure = value ?? throw new ArgumentNullException(nameof(AuthorFigure));
    }

    public string Text
    {
        get => text;
        init => text = value ?? throw new ArgumentNullException(nameof(Text));
    }

    public string AdminName
    {
        get => admin_name;
        init => admin_name = value ?? throw new ArgumentNullException(nameof(AdminName));
    }

    public bool IsHidden => State is 10 or 20;
    public bool IsHiddenByStaff => State == 20;

    public static ForumPost Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPost value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget),
            ParseUnity);
        ForumProtocol.RequireEmpty(in p, nameof(ForumPost));
        return value;
    }

    internal static ForumPost ParseFlashWire(
        in PacketReader p,
        int trailing_bytes,
        ref ForumStringBudget budget)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.PostMinimumBytes,
            trailing_bytes,
            nameof(ForumPost));
        Id message_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 33),
            "message");
        int message_index = p.ReadInt();
        Id author_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 25),
            "message author");
        string author_name = budget.Read(
            in p,
            nameof(AuthorName),
            checked(trailing_bytes + 23));
        string author_figure = budget.Read(
            in p,
            nameof(AuthorFigure),
            checked(trailing_bytes + 21));
        int creation_seconds_ago = p.ReadInt();
        string text = budget.Read(
            in p,
            nameof(Text),
            checked(trailing_bytes + 15));
        byte state = p.ReadByte();
        Id admin_id = ForumProtocol.ReadFlashId(
            in p,
            checked(trailing_bytes + 10),
            "message administrator");
        string admin_name = budget.Read(
            in p,
            nameof(AdminName),
            checked(trailing_bytes + 8));
        int admin_operation_seconds_ago = p.ReadInt();
        int author_post_count = p.ReadInt();
        return new ForumPost(
            message_id,
            message_index,
            author_id,
            author_name,
            author_figure,
            creation_seconds_ago,
            text,
            state,
            admin_id,
            admin_name,
            admin_operation_seconds_ago,
            author_post_count);
    }

    private static ForumPost ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumPost>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    internal static void PrepareFlash(
        ForumPost value,
        in PacketWriter p,
        ref ForumStringBudget budget)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumProtocol.RequireFlashId(value.MessageId, "message");
        ForumProtocol.RequireFlashId(value.AuthorId, "message author");
        budget.Require(value.AuthorName, nameof(AuthorName), in p);
        budget.Require(value.AuthorFigure, nameof(AuthorFigure), in p);
        budget.Require(value.Text, nameof(Text), in p);
        ForumProtocol.RequireFlashId(value.AdminId, "message administrator");
        budget.Require(value.AdminName, nameof(AdminName), in p);
    }

    internal static void ComposeFlashWire(ForumPost value, in PacketWriter p)
    {
        ForumProtocol.WriteFlashId(in p, value.MessageId);
        p.WriteInt(value.MessageIndex);
        ForumProtocol.WriteFlashId(in p, value.AuthorId);
        p.WriteString(value.AuthorName);
        p.WriteString(value.AuthorFigure);
        p.WriteInt(value.CreationSecondsAgo);
        p.WriteString(value.Text);
        p.WriteByte(value.State);
        ForumProtocol.WriteFlashId(in p, value.AdminId);
        p.WriteString(value.AdminName);
        p.WriteInt(value.AdminOperationSecondsAgo);
        p.WriteInt(value.AuthorPostCount);
    }

    private static void ComposeFlash(ForumPost value, in PacketWriter p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        PrepareFlash(value, in p, ref budget);
        ComposeFlashWire(value, in p);
    }

    private static void ComposeUnity(ForumPost value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
