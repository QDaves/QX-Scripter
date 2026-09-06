using System.Globalization;
using System.Text;
using Qx;
using Qx.Messages;

namespace Qx.Model;

public enum AvatarStance
{
    Stand,
    Sit,
    Lay
}

public sealed class AvatarStatus : IParserComposer<AvatarStatus>
{
    private readonly Dictionary<string, string[]> _fragments = new(StringComparer.OrdinalIgnoreCase);

    public int Index { get; set; }
    public Tile Location { get; set; }
    public int HeadDirection { get; set; }
    public int Direction { get; set; }
    public int JumpingPower { get; set; }
    public int TargetId { get; set; }
    public int StatusId
    {
        get => TargetId != 0 ? TargetId : JumpingPower;
        set
        {
            JumpingPower = value;
            TargetId = value;
        }
    }

    public IReadOnlyDictionary<string, string[]> Fragments => _fragments;

    public AvatarStance Stance =>
        _fragments.ContainsKey("sit") ? AvatarStance.Sit :
        _fragments.ContainsKey("lay") ? AvatarStance.Lay :
        AvatarStance.Stand;

    public bool IsController => _fragments.ContainsKey("flatctrl");

    public int RightsLevel =>
        _fragments.TryGetValue("flatctrl", out string[]? args) && args.Length > 0 && int.TryParse(args[0], out int level)
            ? level
            : 0;

    public bool IsTrading => _fragments.ContainsKey("trd");

    public int ControlLevel => RightsLevel;

    public bool SittingOnFloor =>
        _fragments.TryGetValue("sit", out string[]? arguments) &&
        arguments.Length > 1 &&
        arguments[1] == "1";

    public double? ActionHeight
    {
        get
        {
            string key = Stance switch
            {
                AvatarStance.Sit => "sit",
                AvatarStance.Lay => "lay",
                _ => ""
            };
            if (key.Length == 0 ||
                !_fragments.TryGetValue(key, out string[]? arguments) ||
                arguments.Length == 0 ||
                !double.TryParse(
                    arguments[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double height))
            {
                return null;
            }
            return double.IsFinite(height) ? height : null;
        }
    }

    public int Sign =>
        _fragments.TryGetValue("sign", out string[]? args) && args.Length > 0 && int.TryParse(args[0], out int sign)
            ? sign
            : 0;

    public Tile? MovingTo =>
        _fragments.TryGetValue("mv", out string[]? args) && args.Length > 0 && Tile.TryParseString(args[0], out Tile tile)
            ? tile
            : null;

    public AvatarStatus() { }

    private AvatarStatus(in PacketReader p)
    {
        Index = p.ReadInt();
        Location = p.Client is ClientType.Unity
            ? new Tile(p.ReadInt(), p.ReadInt(), (float)(FloatString)p.ReadString())
            : p.Parse<Tile>();
        HeadDirection = p.ReadInt();
        Direction = p.ReadInt();
        if (p.Client is ClientType.Flash)
            JumpingPower = p.ReadInt();
        else if (p.Client is ClientType.Unity &&
                 (p.Context is null || p.Context.WireProfile.RequireUnityAvatarStatusTargetId()))
            TargetId = p.ReadInt();
        ParseStatus(p.ReadString());
    }

    public static AvatarStatus Parse(in PacketReader p) => new(in p);

    public void Compose(in PacketWriter p)
    {
        p.WriteInt(Index);
        if (p.Client is ClientType.Unity)
        {
            p.WriteInt(Location.X);
            p.WriteInt(Location.Y);
            p.WriteString((FloatString)Location.Z);
        }
        else
        {
            p.Compose(Location);
        }
        p.WriteInt(HeadDirection);
        p.WriteInt(Direction);
        if (p.Client is ClientType.Flash)
            p.WriteInt(JumpingPower);
        else if (p.Client is ClientType.Unity &&
                 (p.Context is null || p.Context.WireProfile.RequireUnityAvatarStatusTargetId()))
            p.WriteInt(TargetId);
        p.WriteString(CompileStatus());
    }

    private void ParseStatus(string status)
    {
        _fragments.Clear();
        foreach (string part in status.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            int space = part.IndexOf(' ');
            if (space > 0)
                _fragments[part[..space]] = part[(space + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            else
                _fragments[part] = [];
        }
    }

    public string CompileStatus()
    {
        var sb = new StringBuilder();
        foreach ((string key, string[] args) in _fragments)
        {
            sb.Append('/').Append(key);
            foreach (string arg in args)
                sb.Append(' ').Append(arg);
        }
        return sb.Append('/').ToString();
    }

    internal AvatarStatus Snapshot()
    {
        var snapshot = new AvatarStatus
        {
            Index = Index,
            Location = Location,
            HeadDirection = HeadDirection,
            Direction = Direction,
            JumpingPower = JumpingPower,
            TargetId = TargetId
        };
        foreach ((string key, string[] args) in _fragments)
            snapshot._fragments[key] = [.. args];
        return snapshot;
    }

    public override string ToString() => CompileStatus();
}
