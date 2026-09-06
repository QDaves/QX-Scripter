using Qx.Messages;

namespace Qx.Model;

public readonly record struct PetCustomPart(int LayerId, int PartId, int PaletteId) :
    IParserComposer<PetCustomPart>
{
    public static PetCustomPart Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetCustomPart ParseFlash(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    private static PetCustomPart ParseUnity(in PacketReader p) =>
        new(p.ReadInt(), p.ReadInt(), p.ReadInt());

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetCustomPart value, in PacketWriter p)
    {
        p.WriteInt(value.LayerId);
        p.WriteInt(value.PartId);
        p.WriteInt(value.PaletteId);
    }

    private static void ComposeUnity(PetCustomPart value, in PacketWriter p)
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
    public Id RoomId { get; set; } = -1;
    public string RoomName { get; set; } = "";
    public string RoomContext { get; set; } = "";

    public bool HasRoomContext => (long)RoomId != -1;
    public bool IsInRoom => HasRoomContext && (long)RoomId != 0;
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
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

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

    private static InventoryPet ParseUnity(in PacketReader p)
    {
        var pet = new InventoryPet
        {
            Id = p.ReadLong(),
            Name = p.ReadString(),
            TypeId = p.ReadInt(),
            PaletteId = p.ReadInt(),
            Color = p.ReadString(),
            BreedId = p.ReadInt()
        };
        int count = InventoryWire.RequireCount(
            p.ReadLength(),
            p.Available - 12,
            12,
            nameof(CustomParts));
        var custom_parts = new PetCustomPart[count];
        for (int index = 0; index < custom_parts.Length; index++)
            custom_parts[index] = p.Parse<PetCustomPart>();
        pet.CustomParts = custom_parts;
        pet.Level = p.ReadInt();
        pet.RoomId = p.ReadLong();
        if (pet.HasRoomContext)
        {
            pet.RoomName = p.ReadString();
            pet.RoomContext = p.ReadString();
        }
        return pet;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

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

    private static void ComposeUnity(InventoryPet value, in PacketWriter p)
    {
        value.ValidateUnity(in p);
        p.WriteLong(value.Id);
        p.WriteString(value.Name);
        p.WriteInt(value.TypeId);
        p.WriteInt(value.PaletteId);
        p.WriteString(value.Color);
        p.WriteInt(value.BreedId);
        p.WriteLength((Length)value.CustomParts.Count);
        foreach (PetCustomPart custom_part in value.CustomParts)
            p.Compose(custom_part);
        p.WriteInt(value.Level);
        p.WriteLong(value.RoomId);
        if (value.HasRoomContext)
        {
            p.WriteString(value.RoomName);
            p.WriteString(value.RoomContext);
        }
    }

    internal void ValidateFlash(in PacketWriter p)
    {
        _ = InventoryWire.Int32Id(Id);
        InventoryWire.RequireString(Name, nameof(Name), in p);
        InventoryWire.RequireString(Color, nameof(Color), in p);
        InventoryWire.RequireString(RoomName, nameof(RoomName), in p);
        InventoryWire.RequireString(RoomContext, nameof(RoomContext), in p);
        if ((long)RoomId != -1 || RoomName.Length != 0 || RoomContext.Length != 0)
            throw new InvalidDataException("Flash inventory pets cannot carry Unity room metadata.");
    }

    internal void ValidateUnity(in PacketWriter p)
    {
        InventoryWire.RequireString(Name, nameof(Name), in p);
        InventoryWire.RequireString(Color, nameof(Color), in p);
        InventoryWire.RequireString(RoomName, nameof(RoomName), in p);
        InventoryWire.RequireString(RoomContext, nameof(RoomContext), in p);
        InventoryWire.RequireUnityCount(CustomParts.Count, nameof(CustomParts));
        if (RarityLevel != -1)
            throw new InvalidDataException("Unity inventory pets cannot carry a rarity level.");
        if (!HasRoomContext && (RoomName.Length != 0 || RoomContext.Length != 0))
        {
            throw new InvalidDataException(
                "Inventory pet room strings require a Unity room identifier.");
        }
    }

    public override string ToString() => $"{nameof(InventoryPet)}#{Id}/{Name}";
}
