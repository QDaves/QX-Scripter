using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents a message posted in a group forum thread.</summary>
/// <param name="MessageId">The message id.</param>
/// <param name="MessageIndex">The zero based position of the message in its thread.</param>
/// <param name="AuthorId">The user id of the author.</param>
/// <param name="AuthorName">The name of the author.</param>
/// <param name="AuthorFigure">The figure string of the author.</param>
/// <param name="CreationSecondsAgo">The number of seconds since the message was posted, at the time it was sent.</param>
/// <param name="Text">The message text.</param>
/// <param name="State">The moderation state: 0 default, 1 restored, 10 hidden by a forum admin, 20 hidden by staff.</param>
/// <param name="AdminId">The user id of the moderator who last changed the state.</param>
/// <param name="AdminName">The name of the moderator who last changed the state.</param>
/// <param name="AdminOperationSecondsAgo">The number of seconds since the state was last changed, at the time the message was sent.</param>
/// <param name="AuthorPostCount">The number of forum messages the author has posted.</param>
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

    /// <summary>Gets the name of the author.</summary>
    public string AuthorName
    {
        get => author_name;
        init => author_name = value ?? throw new ArgumentNullException(nameof(AuthorName));
    }

    /// <summary>Gets the figure string of the author.</summary>
    public string AuthorFigure
    {
        get => author_figure;
        init => author_figure = value ?? throw new ArgumentNullException(nameof(AuthorFigure));
    }

    /// <summary>Gets the message text.</summary>
    public string Text
    {
        get => text;
        init => text = value ?? throw new ArgumentNullException(nameof(Text));
    }

    /// <summary>Gets the name of the moderator who last changed the state.</summary>
    public string AdminName
    {
        get => admin_name;
        init => admin_name = value ?? throw new ArgumentNullException(nameof(AdminName));
    }

    /// <summary>Gets whether the message is hidden, which is when <see cref="State"/> is 10 or 20.</summary>
    public bool IsHidden => State is 10 or 20;
    /// <summary>Gets whether the message is hidden by staff, which is when <see cref="State"/> is 20.</summary>
    public bool IsHiddenByStaff => State == 20;

    /// <summary>Parses a forum post from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumPost Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPost value = FlashWire.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget));
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

    /// <summary>Composes the forum post into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

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
}
