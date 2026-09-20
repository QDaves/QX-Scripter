using Qx.Messages;

namespace Qx.Model;

public readonly record struct PetCustomPart(int LayerId, int PartId, int PaletteId) :
    IParserComposer<PetCustomPart>
{
    public static PetCustomPart Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetCustomPart ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetCustomPart value, in PacketWriter p)
    {
        p.WriteInt(value.LayerId);
        p.WriteInt(value.PartId);
        p.WriteInt(value.PaletteId);
    }
}

public sealed class InventoryPet : IParserComposer<InventoryPet>
{
    private IReadOnlyList<PetCustomPart> _custom_parts = Array.Empty<PetCustomPart>();

    public Id Id { get; set; }
    public string Name { get; set; } = "";
    public int TypeId { get; set; }
    public int PaletteId { get; set; }
    public string Color { get; set; } = "";
    public int BreedId { get; set; }
    public IReadOnlyList<PetCustomPart> CustomParts
    {
        get => _custom_parts;
        set => _custom_parts = InventoryWire.FreezeValues(value, nameof(CustomParts));
    }
    public int Level { get; set; }
    public int RarityLevel { get; set; } = -1;
    public string FigureString => string.Join(' ',
        new[]
        {
            TypeId.ToString(),
            PaletteId.ToString(),
            Color,
            CustomParts.Count.ToString()
        }.Concat(CustomParts.SelectMany(part => new[]
        {
            part.LayerId.ToString(),
            part.PartId.ToString(),
            part.PaletteId.ToString()
        })));

    public InventoryPet()
    {
    }

    public static InventoryPet Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static InventoryPet ParseFlash(in PacketReader p)
    {
        var pet = new InventoryPet
        {
            Id = p.ReadInt(),
            Name = p.ReadString(),
            TypeId = p.ReadInt(),
            PaletteId = p.ReadInt(),
            Color = p.ReadString(),
            BreedId = p.ReadInt()
        };
        int count = InventoryWire.RequireCount(
            p.ReadInt(),
            p.Available - 8,
            12,
            nameof(CustomParts));
        var custom_parts = new PetCustomPart[count];
        for (int index = 0; index < custom_parts.Length; index++)
            custom_parts[index] = p.Parse<PetCustomPart>();
        pet.CustomParts = custom_parts;
        pet.Level = p.ReadInt();
        pet.RarityLevel = p.ReadInt();
        return pet;
    }

    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(InventoryPet value, in PacketWriter p)
    {
        value.ValidateFlash(in p);
        p.WriteInt(InventoryWire.Int32Id(value.Id));
        p.WriteString(value.Name);
        p.WriteInt(value.TypeId);
        p.WriteInt(value.PaletteId);
        p.WriteString(value.Color);
        p.WriteInt(value.BreedId);
        p.WriteInt(value.CustomParts.Count);
        foreach (PetCustomPart custom_part in value.CustomParts)
            p.Compose(custom_part);
        p.WriteInt(value.Level);
        p.WriteInt(value.RarityLevel);
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        _ = InventoryWire.Int32Id(Id);
        InventoryWire.RequireString(Name, nameof(Name), in p);
        InventoryWire.RequireString(Color, nameof(Color), in p);
    }

    public override string ToString() => $"{nameof(InventoryPet)}#{Id}/{Name}";
}
