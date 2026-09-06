using Qx.Messages;

namespace Qx.Model.Forums;

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

    public string ReadError
    {
        get => read_error;
        init => read_error = value ?? throw new ArgumentNullException(nameof(ReadError));
    }

    public string PostMessageError
    {
        get => post_message_error;
        init => post_message_error = value ??
            throw new ArgumentNullException(nameof(PostMessageError));
    }

    public string PostThreadError
    {
        get => post_thread_error;
        init => post_thread_error = value ??
            throw new ArgumentNullException(nameof(PostThreadError));
    }

    public string ModerateError
    {
        get => moderate_error;
        init => moderate_error = value ?? throw new ArgumentNullException(nameof(ModerateError));
    }

    public string ReportError
    {
        get => report_error;
        init => report_error = value ?? throw new ArgumentNullException(nameof(ReportError));
    }

    public bool CanRead => ReadError.Length == 0;
    public bool CanPostMessage => PostMessageError.Length == 0;
    public bool CanPostThread => PostThreadError.Length == 0;
    public bool CanModerate => ModerateError.Length == 0;
    public bool CanReport => true;

    public static ForumPermissions Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumPermissions value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget),
            ParseUnity);
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

    private static ForumPermissions ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumPermissions>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(ForumPermissions value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}

public sealed record ForumDetails(
    ForumSummary Summary,
    ForumPermissions Permissions,
    bool CanChangeSettings,
    bool IsStaff) : IParserComposer<ForumDetails>
{
    private ForumSummary summary = Summary ?? throw new ArgumentNullException(nameof(Summary));
    private ForumPermissions permissions = Permissions ??
        throw new ArgumentNullException(nameof(Permissions));

    public ForumSummary Summary
    {
        get => summary;
        init => summary = value ?? throw new ArgumentNullException(nameof(Summary));
    }

    public ForumPermissions Permissions
    {
        get => permissions;
        init => permissions = value ?? throw new ArgumentNullException(nameof(Permissions));
    }

    public Id GroupId => Summary.GroupId;
    public string Name => Summary.Name;
    public string Description => Summary.Description;
    public string Icon => Summary.Icon;
    public int TotalThreads => Summary.TotalThreads;
    public int TotalMessages => Summary.TotalMessages;
    public int UnreadMessages => Summary.UnreadMessages;
    public int LastReadMessageId => Summary.LastReadMessageId;

    public static ForumDetails Parse(in PacketReader p)
    {
        ForumStringBudget budget = ForumProtocol.NewStringBudget();
        ForumDetails value = ModernWireClients.Parse(
            in p,
            (in PacketReader reader) => ParseFlashWire(in reader, 0, ref budget),
            ParseUnity);
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

    private static ForumDetails ParseUnity(in PacketReader p) =>
        ForumProtocol.UnsupportedUnity<ForumDetails>(p.Client);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(ForumDetails value, in PacketWriter p) =>
        ForumProtocol.UnsupportedUnity(p.Client);
}
