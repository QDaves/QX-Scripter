using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

public sealed record FurniInventoryRequest : IParserComposer<FurniInventoryRequest>
{
    public static FurniInventoryRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static FurniInventoryRequest ParseFlash(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(FurniInventoryRequest));
        return new();
    }

    private static FurniInventoryRequest ParseUnity(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(FurniInventoryRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(FurniInventoryRequest value, in PacketWriter p) { }

    private static void ComposeUnity(FurniInventoryRequest value, in PacketWriter p) { }
}

public sealed record PetInventoryRequest : IParserComposer<PetInventoryRequest>
{
    public static PetInventoryRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetInventoryRequest ParseFlash(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(PetInventoryRequest));
        return new();
    }

    private static PetInventoryRequest ParseUnity(in PacketReader p)
    {
        InventoryWire.RequireEmpty(in p, nameof(PetInventoryRequest));
        return new();
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetInventoryRequest value, in PacketWriter p) { }

    private static void ComposeUnity(PetInventoryRequest value, in PacketWriter p) { }
}

public sealed record AvatarEffectActivationRequest(int Effect)
    : IParserComposer<AvatarEffectActivationRequest>
{
    public static AvatarEffectActivationRequest Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static AvatarEffectActivationRequest ParseFlash(in PacketReader p)
    {
        var value = new AvatarEffectActivationRequest(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(AvatarEffectActivationRequest));
        return value;
    }

    private static AvatarEffectActivationRequest ParseUnity(in PacketReader p)
    {
        var value = new AvatarEffectActivationRequest(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(AvatarEffectActivationRequest));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(AvatarEffectActivationRequest value, in PacketWriter p) =>
        p.WriteInt(value.Effect);

    private static void ComposeUnity(AvatarEffectActivationRequest value, in PacketWriter p) =>
        p.WriteInt(value.Effect);
}
