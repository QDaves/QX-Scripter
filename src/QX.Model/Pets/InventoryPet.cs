using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents one customization part a pet wears.</summary>
/// <param name="LayerId">The layer the part is drawn on.</param>
/// <param name="PartId">The part identifier.</param>
/// <param name="PaletteId">The palette the part is colored with.</param>
public readonly record struct PetCustomPart(int LayerId, int PartId, int PaletteId) :
    IParserComposer<PetCustomPart>
{
    /// <summary>Reads a custom part from a packet as three integers.</summary>
    /// <param name="p">The packet to read from.</param>
    public static PetCustomPart Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetCustomPart ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    /// <summary>Writes the custom part to a packet as three integers.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetCustomPart value, in PacketWriter p)
    {
        p.WriteInt(value.LayerId);
        p.WriteInt(value.PartId);
        p.WriteInt(value.PaletteId);
    }
}

/// <summary>Represents a pet in the local user's inventory.</summary>
public sealed class InventoryPet : IParserComposer<InventoryPet>
{
    private IReadOnlyList<PetCustomPart> _custom_parts = Array.Empty<PetCustomPart>();

    /// <summary>Gets or sets the pet identifier.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the pet's name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the pet type, which identifies the kind of animal.</summary>
    public int TypeId { get; set; }
    /// <summary>Gets or sets the pet's palette identifier.</summary>
    public int PaletteId { get; set; }
    /// <summary>Gets or sets the pet's color as sent by the hotel.</summary>
    public string Color { get; set; } = "";
    /// <summary>Gets or sets the breed variant within the pet type.</summary>
    public int BreedId { get; set; }
    /// <summary>Gets or sets the customization parts the pet wears; the list is copied on set.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    public IReadOnlyList<PetCustomPart> CustomParts
    {
        get => _custom_parts;
        set => _custom_parts = InventoryWire.FreezeValues(value, nameof(CustomParts));
    }
    /// <summary>Gets or sets the pet's level.</summary>
    public int Level { get; set; }
    /// <summary>Gets or sets the pet's rarity tier as sent by the hotel, or -1 when none was read.</summary>
    public int RarityLevel { get; set; } = -1;
    /// <summary>Gets the pet's figure string built from its type, palette, color and custom parts.</summary>
    /// <remarks>
    /// The form is <c>type palette color count</c> followed by <c>layer part palette</c> for each custom
    /// part, all separated by spaces.
    /// </remarks>
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

    /// <summary>Initializes a new instance of the <see cref="InventoryPet"/> class.</summary>
    public InventoryPet()
    {
    }

    /// <summary>Reads an inventory pet from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the custom part count is invalid.</exception>
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

    /// <summary>Writes the inventory pet to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    /// <exception cref="OverflowException">Thrown when <see cref="Id"/> does not fit in 32 bits.</exception>
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

    /// <summary>Returns the pet's identifier and name.</summary>
    /// <returns>A string in the form <c>InventoryPet#Id/Name</c>.</returns>
    public override string ToString() => $"{nameof(InventoryPet)}#{Id}/{Name}";
}
