using Qx.Messages;

namespace Qx.Model;

/// <summary>
/// Represents the forum's permission levels and the viewer's access to a group forum.
/// </summary>
/// <remarks>
/// Levels run from 0, the least restrictive, to 3. An error string is empty when the viewer has the
/// matching right; otherwise it holds the reason the server gives for denying it.
/// </remarks>
/// <param name="ReadLevel">The permission level required to read the forum.</param>
/// <param name="PostMessageLevel">The permission level required to reply to threads.</param>
/// <param name="PostThreadLevel">The permission level required to start threads.</param>
/// <param name="ModerateLevel">The permission level required to moderate the forum.</param>
/// <param name="ReadError">The reason the viewer may not read, or an empty string when allowed.</param>
/// <param name="PostMessageError">The reason the viewer may not reply, or an empty string when allowed.</param>
/// <param name="PostThreadError">The reason the viewer may not start threads, or an empty string when allowed.</param>
/// <param name="ModerateError">The reason the viewer may not moderate, or an empty string when allowed.</param>
/// <param name="ReportError">The reason the viewer may not report, or an empty string when allowed.</param>
public sealed record ForumPermissions(
    int ReadLevel,
    int PostMessageLevel,
    int PostThreadLevel,
    int ModerateLevel,
    string ReadError,
    string PostMessageError,
    string PostThreadError,
    string ModerateError,
    string ReportError) : IParserComposer<ForumPermissions>
{
    private string read_error = ReadError ?? throw new ArgumentNullException(nameof(ReadError));
    private string post_message_error = PostMessageError ??
        throw new ArgumentNullException(nameof(PostMessageError));
    private string post_thread_error = PostThreadError ??
        throw new ArgumentNullException(nameof(PostThreadError));
    private string moderate_error = ModerateError ??
        throw new ArgumentNullException(nameof(ModerateError));
    private string report_error = ReportError ?? throw new ArgumentNullException(nameof(ReportError));

    /// <summary>Gets the reason the viewer may not read, or an empty string when allowed.</summary>
    public string ReadError
    {
        get => read_error;
        init => read_error = value ?? throw new ArgumentNullException(nameof(ReadError));
    }

    /// <summary>Gets the reason the viewer may not reply, or an empty string when allowed.</summary>
    public string PostMessageError
    {
        get => post_message_error;
        init => post_message_error = value ??
            throw new ArgumentNullException(nameof(PostMessageError));
    }

    /// <summary>Gets the reason the viewer may not start threads, or an empty string when allowed.</summary>
    public string PostThreadError
    {
        get => post_thread_error;
        init => post_thread_error = value ??
            throw new ArgumentNullException(nameof(PostThreadError));
    }

    /// <summary>Gets the reason the viewer may not moderate, or an empty string when allowed.</summary>
    public string ModerateError
    {
        get => moderate_error;
        init => moderate_error = value ?? throw new ArgumentNullException(nameof(ModerateError));
    }

    /// <summary>Gets the reason the viewer may not report, or an empty string when allowed.</summary>
    public string ReportError
    {
        get => report_error;
        init => report_error = value ?? throw new ArgumentNullException(nameof(ReportError));
    }

    /// <summary>Gets whether the viewer may read the forum, which is when <see cref="ReadError"/> is empty.</summary>
    public bool CanRead => ReadError.Length == 0;
    /// <summary>Gets whether the viewer may reply to threads, which is when <see cref="PostMessageError"/> is empty.</summary>
    public bool CanPostMessage => PostMessageError.Length == 0;
    /// <summary>Gets whether the viewer may start threads, which is when <see cref="PostThreadError"/> is empty.</summary>
    public bool CanPostThread => PostThreadError.Length == 0;
    /// <summary>Gets whether the viewer may moderate the forum, which is when <see cref="ModerateError"/> is empty.</summary>
    public bool CanModerate => ModerateError.Length == 0;
    /// <summary>Gets whether the viewer may report content, which is always <see langword="true"/> regardless of <see cref="ReportError"/>.</summary>
    public bool CanReport => true;

    /// <summary>Parses forum permissions from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumPermissions Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPermissions value = FlashWire.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget));
        ForumProtocol.RequireEmpty(in p, nameof(ForumPermissions));
        return value;
    }

    internal static ForumPermissions ParseFlashWire(
        in PacketReader p,
        int trailing_bytes,
        ref ForumStringBudget budget)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.PermissionsMinimumBytes,
            trailing_bytes,
            nameof(ForumPermissions));
        int read_level = p.ReadInt();
        int post_message_level = p.ReadInt();
        int post_thread_level = p.ReadInt();
        int moderate_level = p.ReadInt();
        string read_error = budget.Read(
            in p,
            nameof(ReadError),
            checked(trailing_bytes + 8));
        string post_message_error = budget.Read(
            in p,
            nameof(PostMessageError),
            checked(trailing_bytes + 6));
        string post_thread_error = budget.Read(
            in p,
            nameof(PostThreadError),
            checked(trailing_bytes + 4));
        string moderate_error = budget.Read(
            in p,
            nameof(ModerateError),
            checked(trailing_bytes + 2));
        string report_error = budget.Read(in p, nameof(ReportError), trailing_bytes);
        return new ForumPermissions(
            read_level,
            post_message_level,
            post_thread_level,
            moderate_level,
            read_error,
            post_message_error,
            post_thread_error,
            moderate_error,
            report_error);
    }

    /// <summary>Composes the forum permissions into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    internal static void PrepareFlash(
        ForumPermissions value,
        in PacketWriter p,
        ref ForumStringBudget budget)
    {
        ArgumentNullException.ThrowIfNull(value);
        budget.Require(value.ReadError, nameof(ReadError), in p);
        budget.Require(value.PostMessageError, nameof(PostMessageError), in p);
        budget.Require(value.PostThreadError, nameof(PostThreadError), in p);
        budget.Require(value.ModerateError, nameof(ModerateError), in p);
        budget.Require(value.ReportError, nameof(ReportError), in p);
    }

    internal static void ComposeFlashWire(ForumPermissions value, in PacketWriter p)
    {
        p.WriteInt(value.ReadLevel);
        p.WriteInt(value.PostMessageLevel);
        p.WriteInt(value.PostThreadLevel);
        p.WriteInt(value.ModerateLevel);
        p.WriteString(value.ReadError);
        p.WriteString(value.PostMessageError);
        p.WriteString(value.PostThreadError);
        p.WriteString(value.ModerateError);
        p.WriteString(value.ReportError);
    }

    private static void ComposeFlash(ForumPermissions value, in PacketWriter p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        PrepareFlash(value, in p, ref budget);
        ComposeFlashWire(value, in p);
    }
}

/// <summary>
/// Represents the full details of a group forum, its summary together with the viewer's permissions.
/// </summary>
/// <param name="Summary">The forum summary.</param>
/// <param name="Permissions">The forum's permission levels and the viewer's access.</param>
/// <param name="CanChangeSettings">Whether the viewer may change the forum settings.</param>
/// <param name="IsStaff">Whether the viewer is hotel staff.</param>
public sealed record ForumDetails(
    ForumSummary Summary,
    ForumPermissions Permissions,
    bool CanChangeSettings,
    bool IsStaff) : IParserComposer<ForumDetails>
{
    private ForumSummary summary = Summary ?? throw new ArgumentNullException(nameof(Summary));
    private ForumPermissions permissions = Permissions ??
        throw new ArgumentNullException(nameof(Permissions));

    /// <summary>Gets the forum summary.</summary>
    public ForumSummary Summary
    {
        get => summary;
        init => summary = value ?? throw new ArgumentNullException(nameof(Summary));
    }

    /// <summary>Gets the forum's permission levels and the viewer's access.</summary>
    public ForumPermissions Permissions
    {
        get => permissions;
        init => permissions = value ?? throw new ArgumentNullException(nameof(Permissions));
    }

    /// <summary>Gets the id of the group that owns the forum.</summary>
    public Id GroupId => Summary.GroupId;
    /// <summary>Gets the forum name.</summary>
    public string Name => Summary.Name;
    /// <summary>Gets the forum description.</summary>
    public string Description => Summary.Description;
    /// <summary>Gets the forum icon as sent by the server.</summary>
    public string Icon => Summary.Icon;
    /// <summary>Gets the number of threads in the forum.</summary>
    public int TotalThreads => Summary.TotalThreads;
    /// <summary>Gets the number of messages in the forum.</summary>
    public int TotalMessages => Summary.TotalMessages;
    /// <summary>Gets the number of messages the viewer has not read.</summary>
    public int UnreadMessages => Summary.UnreadMessages;
    /// <summary>Gets the last read message id, see <see cref="ForumSummary.LastReadMessageId"/>.</summary>
    public int LastReadMessageId => Summary.LastReadMessageId;

    /// <summary>Parses forum details from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static ForumDetails Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumDetails value = FlashWire.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget));
        ForumProtocol.RequireEmpty(in p, nameof(ForumDetails));
        return value;
    }

    internal static ForumDetails ParseFlashWire(
        in PacketReader p,
        int trailing_bytes,
        ref ForumStringBudget budget)
    {
        ForumProtocol.RequireRemaining(
            in p,
            ForumProtocol.DetailsMinimumBytes,
            trailing_bytes,
            nameof(ForumDetails));
        ForumSummary summary = ForumSummary.ParseFlashWire(
            in p,
            checked(trailing_bytes + ForumProtocol.PermissionsMinimumBytes + 2),
            ref budget);
        ForumPermissions permissions = ForumPermissions.ParseFlashWire(
            in p,
            checked(trailing_bytes + 2),
            ref budget);
        return new ForumDetails(summary, permissions, p.ReadBool(), p.ReadBool());
    }

    /// <summary>Composes the forum details into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    internal static void PrepareFlash(
        ForumDetails value,
        in PacketWriter p,
        ref ForumStringBudget budget)
    {
        ArgumentNullException.ThrowIfNull(value);
        ForumSummary.PrepareFlash(value.Summary, in p, ref budget);
        ForumPermissions.PrepareFlash(value.Permissions, in p, ref budget);
    }

    internal static void ComposeFlashWire(ForumDetails value, in PacketWriter p)
    {
        ForumSummary.ComposeFlashWire(value.Summary, in p);
        ForumPermissions.ComposeFlashWire(value.Permissions, in p);
        p.WriteBool(value.CanChangeSettings);
        p.WriteBool(value.IsStaff);
    }

    private static void ComposeFlash(ForumDetails value, in PacketWriter p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        PrepareFlash(value, in p, ref budget);
        ComposeFlashWire(value, in p);
    }
}
