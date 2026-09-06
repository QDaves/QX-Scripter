using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

public sealed record PetInventory : IParserComposer<PetInventory>
{
    private IReadOnlyList<InventoryPet> _pets = Array.Empty<InventoryPet>();

    public PetInventory(int total, int index, IReadOnlyList<InventoryPet> pets)
    {
        Total = total;
        Index = index;
        Pets = pets;
    }

    public int Total { get; init; }

    public int Index { get; init; }

    public IReadOnlyList<InventoryPet> Pets
    {
        get => _pets;
        init => _pets = InventoryWire.FreezeReferences(value, nameof(Pets));
    }

    public static PetInventory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetInventory ParseFlash(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();
        InventoryWire.RequireFragment(total, index, nameof(PetInventory));
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available,
            32,
            nameof(Pets));
        var pets = new InventoryPet[count];
        for (int pet_index = 0; pet_index < pets.Length; pet_index++)
            pets[pet_index] = p.Parse<InventoryPet>();
        InventoryWire.RequireEmpty(in p, nameof(PetInventory));
        return new PetInventory(total, index, pets);
    }

    private static PetInventory ParseUnity(in PacketReader p)
    {
        int total = p.ReadInt();
        int index = p.ReadInt();
        InventoryWire.RequireFragment(total, index, nameof(PetInventory));
        int count = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available,
            38,
            nameof(Pets));
        var pets = new InventoryPet[count];
        for (int pet_index = 0; pet_index < pets.Length; pet_index++)
            pets[pet_index] = p.Parse<InventoryPet>();
        InventoryWire.RequireEmpty(in p, nameof(PetInventory));
        return new PetInventory(total, index, pets);
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetInventory value, in PacketWriter p)
    {
        InventoryWire.RequireFragment(value.Total, value.Index, nameof(PetInventory));
        foreach (InventoryPet pet in value.Pets)
            pet.ValidateFlash(in p);
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);
        p.WriteInt(value.Pets.Count);
        foreach (InventoryPet pet in value.Pets)
            p.Compose(pet);
    }

    private static void ComposeUnity(PetInventory value, in PacketWriter p)
    {
        InventoryWire.RequireFragment(value.Total, value.Index, nameof(PetInventory));
        InventoryWire.RequireUnityCount(value.Pets.Count, nameof(Pets));
        foreach (InventoryPet pet in value.Pets)
            pet.ValidateUnity(in p);
        p.WriteInt(value.Total);
        p.WriteInt(value.Index);
        p.WriteLength((Length)value.Pets.Count);
        foreach (InventoryPet pet in value.Pets)
            p.Compose(pet);
    }
}

public sealed record PetAddedToInventory(InventoryPet Pet, bool OpenInventory) :
    IParserComposer<PetAddedToInventory>
{
    public static PetAddedToInventory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetAddedToInventory ParseFlash(in PacketReader p)
    {
        var value = new PetAddedToInventory(p.Parse<InventoryPet>(), p.ReadBool());
        InventoryWire.RequireEmpty(in p, nameof(PetAddedToInventory));
        return value;
    }

    private static PetAddedToInventory ParseUnity(in PacketReader p)
    {
        var value = new PetAddedToInventory(p.Parse<InventoryPet>(), p.ReadBool());
        InventoryWire.RequireEmpty(in p, nameof(PetAddedToInventory));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetAddedToInventory value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Pet);
        value.Pet.ValidateFlash(in p);
        p.Compose(value.Pet);
        p.WriteBool(value.OpenInventory);
    }

    private static void ComposeUnity(PetAddedToInventory value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value.Pet);
        value.Pet.ValidateUnity(in p);
        p.Compose(value.Pet);
        p.WriteBool(value.OpenInventory);
    }
}

public sealed record PetRemovedFromInventory(Id PetId) : IParserComposer<PetRemovedFromInventory>
{
    public static PetRemovedFromInventory Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetRemovedFromInventory ParseFlash(in PacketReader p)
    {
        var value = new PetRemovedFromInventory(p.ReadInt());
        InventoryWire.RequireEmpty(in p, nameof(PetRemovedFromInventory));
        return value;
    }

    private static PetRemovedFromInventory ParseUnity(in PacketReader p)
    {
        var value = new PetRemovedFromInventory(p.ReadLong());
        InventoryWire.RequireEmpty(in p, nameof(PetRemovedFromInventory));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetRemovedFromInventory value, in PacketWriter p) =>
        p.WriteInt(InventoryWire.Int32Id(value.PetId));

    private static void ComposeUnity(PetRemovedFromInventory value, in PacketWriter p) =>
        p.WriteLong(value.PetId);
}
