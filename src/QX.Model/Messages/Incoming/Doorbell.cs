using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record Doorbell(string UserName) : IParserComposer<Doorbell>
{
    public Id? UnityUserId { get; init; }
    public bool? UnityFlagA { get; init; }
    public bool? UnityFlagB { get; init; }

    public static Doorbell Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static Doorbell ParseFlash(in PacketReader p) => new(p.ReadString());

    private static Doorbell ParseUnity(in PacketReader p)
    {
        Id user_id = p.ReadId();
        return new Doorbell(p.ReadString())
        {
            UnityUserId = user_id,
            UnityFlagA = p.ReadBool(),
            UnityFlagB = p.ReadBool()
        };
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(Doorbell value, in PacketWriter p) =>
        p.WriteString(value.UserName);

    private static void ComposeUnity(Doorbell value, in PacketWriter p)
    {
        p.WriteId(value.UnityUserId ?? throw new InvalidOperationException("Unity Doorbell requires its native user identifier."));
        p.WriteString(value.UserName);
        p.WriteBool(value.UnityFlagA ?? throw new InvalidOperationException("Unity Doorbell requires its first native flag."));
        p.WriteBool(value.UnityFlagB ?? throw new InvalidOperationException("Unity Doorbell requires its second native flag."));
    }
}
