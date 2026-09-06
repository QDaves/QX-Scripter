using Qx.Messages;

namespace Qx.Model;

/// <summary>
/// The detailed statistics of a single pet, as returned by <c>GetPetInfo</c>.
/// </summary>
/// <remarks>
/// This message does not carry the pet type. The type is only present on the room
/// entity (<see cref="Pet.PetType"/>), and both values are needed together: the
/// client resolves a pet's displayed breed through the localization key
/// <c>pet.breed.{Pet.PetType}.{BreedId}</c>.
/// </remarks>
public sealed class PetInfo : IParserComposer<PetInfo>
{
    private IReadOnlyList<int> skill_thresholds = Array.AsReadOnly(Array.Empty<int>());

    public Id Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public int MaxLevel { get; set; }
    public int Experience { get; set; }
    public int MaxExperience { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; }
    /// <summary>The nutrition level of the pet. The client calls this field <c>nutrition</c>.</summary>
    public int Happiness { get; set; }
    /// <summary>The nutrition cap of the pet. The client calls this field <c>maxNutrition</c>.</summary>
    public int MaxHappiness { get; set; }
    /// <summary>The respect count of the pet. The client calls this field <c>respect</c>.</summary>
    public int Scratches { get; set; }
    public Id OwnerId { get; set; }
    public int Age { get; set; }
    public string OwnerName { get; set; } = "";
    /// <summary>
    /// The breed variant of the pet within its type, not the pet type itself.
    /// </summary>
    /// <remarks>
    /// Only meaningful together with <see cref="Pet.PetType"/> from the room entity.
    /// Pet types that have no breed variants, most notably the monsterplant (type 16),
    /// report 0 here, so a zero value must not be read as "unknown pet".
    /// Use <see cref="Pet.PetType"/> to identify what kind of pet this is.
    /// </remarks>
    public int BreedId { get; set; }
    public bool HasFreeSaddle { get; set; }
    public bool IsRiding { get; set; }
    public IReadOnlyList<int> SkillThresholds
    {
        get => skill_thresholds;
        set => skill_thresholds = RoomObjectReadWire.Freeze(value);
    }
    public int AccessRights { get; set; }
    public bool CanBreed { get; set; }
    public bool CanHarvest { get; set; }
    public bool CanRevive { get; set; }
    public int RarityLevel { get; set; }
    public int MaxWellbeingSeconds { get; set; }
    public int RemainingWellbeingSeconds { get; set; }
    public int RemainingGrowingSeconds { get; set; }
    public bool HasBreedingPermission { get; set; }

    public PetInfo() { }

    public static PetInfo Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    private static PetInfo ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static PetInfo ParseUnity(in PacketReader p) => ParseMessage(in p);

    private static PetInfo ParseMessage(in PacketReader p)
    {
        var strings = new RoomObjectReadStringBudget();
        RoomObjectReadWire.RequireRemaining(
            in p,
            checked(RoomObjectReadWire.IdWidth(p.Client) + sizeof(short)),
            0,
            nameof(PetInfo));
        var value = new PetInfo
        {
            Id = p.ReadId(),
            Name = strings.Read(in p, 0, nameof(Name)),
            Level = p.ReadInt(),
            MaxLevel = p.ReadInt(),
            Experience = p.ReadInt(),
            MaxExperience = p.ReadInt(),
            Energy = p.ReadInt(),
            MaxEnergy = p.ReadInt(),
            Happiness = p.ReadInt(),
            MaxHappiness = p.ReadInt(),
            Scratches = p.ReadInt(),
            OwnerId = p.ReadId(),
            Age = p.ReadInt(),
            OwnerName = strings.Read(in p, 0, nameof(OwnerName)),
            BreedId = p.ReadInt(),
            HasFreeSaddle = p.ReadBool(),
            IsRiding = p.ReadBool(),
            SkillThresholds = ReadSkillThresholds(in p),
            AccessRights = p.ReadInt(),
            CanBreed = p.ReadBool(),
            CanHarvest = p.ReadBool(),
            CanRevive = p.ReadBool(),
            RarityLevel = p.ReadInt(),
            MaxWellbeingSeconds = p.ReadInt(),
            RemainingWellbeingSeconds = p.ReadInt(),
            RemainingGrowingSeconds = p.ReadInt(),
            HasBreedingPermission = p.ReadBool()
        };
        RoomObjectReadWire.RequireEmpty(in p, nameof(PetInfo));
        return value;
    }

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static void ComposeFlash(PetInfo value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeUnity(PetInfo value, in PacketWriter p) => ComposeMessage(value, in p);

    private static void ComposeMessage(PetInfo value, in PacketWriter p)
    {
        PetInfoWireSnapshot snapshot = Prepare(value, in p);
        p.WriteId(snapshot.Id);
        p.WriteString(snapshot.Name);
        p.WriteInt(snapshot.Level);
        p.WriteInt(snapshot.MaxLevel);
        p.WriteInt(snapshot.Experience);
        p.WriteInt(snapshot.MaxExperience);
        p.WriteInt(snapshot.Energy);
        p.WriteInt(snapshot.MaxEnergy);
        p.WriteInt(snapshot.Happiness);
        p.WriteInt(snapshot.MaxHappiness);
        p.WriteInt(snapshot.Scratches);
        p.WriteId(snapshot.OwnerId);
        p.WriteInt(snapshot.Age);
        p.WriteString(snapshot.OwnerName);
        p.WriteInt(snapshot.BreedId);
        p.WriteBool(snapshot.HasFreeSaddle);
        p.WriteBool(snapshot.IsRiding);
        RoomObjectReadWire.WriteCount(snapshot.SkillThresholds.Count, in p);
        foreach (int threshold in snapshot.SkillThresholds)
            p.WriteInt(threshold);
        p.WriteInt(snapshot.AccessRights);
        p.WriteBool(snapshot.CanBreed);
        p.WriteBool(snapshot.CanHarvest);
        p.WriteBool(snapshot.CanRevive);
        p.WriteInt(snapshot.RarityLevel);
        p.WriteInt(snapshot.MaxWellbeingSeconds);
        p.WriteInt(snapshot.RemainingWellbeingSeconds);
        p.WriteInt(snapshot.RemainingGrowingSeconds);
        p.WriteBool(snapshot.HasBreedingPermission);
    }

    private static IReadOnlyList<int> ReadSkillThresholds(in PacketReader p)
    {
        const int trailing_bytes = sizeof(int) * 5 + sizeof(bool) * 4;
        int count = RoomObjectReadWire.ReadCount(
            in p,
            sizeof(int),
            trailing_bytes,
            nameof(SkillThresholds));
        var values = new int[count];
        for (int index = 0; index < count; index++)
            values[index] = p.ReadInt();
        return Array.AsReadOnly(values);
    }

    private static PetInfoWireSnapshot Prepare(PetInfo value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        RoomObjectReadWire.RequireSupportedClient(p.Client);
        RoomObjectReadWire.RequireWireId(p.Client, value.Id, nameof(Id));
        RoomObjectReadWire.RequireWireId(p.Client, value.OwnerId, nameof(OwnerId));
        var strings = new RoomObjectReadStringBudget();
        strings.Require(value.Name, in p, nameof(Name));
        strings.Require(value.OwnerName, in p, nameof(OwnerName));
        IReadOnlyList<int> thresholds = RoomObjectReadWire.Freeze(value.SkillThresholds);
        return new PetInfoWireSnapshot(
            value.Id,
            value.Name,
            value.Level,
            value.MaxLevel,
            value.Experience,
            value.MaxExperience,
            value.Energy,
            value.MaxEnergy,
            value.Happiness,
            value.MaxHappiness,
            value.Scratches,
            value.OwnerId,
            value.Age,
            value.OwnerName,
            value.BreedId,
            value.HasFreeSaddle,
            value.IsRiding,
            thresholds,
            value.AccessRights,
            value.CanBreed,
            value.CanHarvest,
            value.CanRevive,
            value.RarityLevel,
            value.MaxWellbeingSeconds,
            value.RemainingWellbeingSeconds,
            value.RemainingGrowingSeconds,
            value.HasBreedingPermission);
    }
}

internal sealed record PetInfoWireSnapshot(
    Id Id,
    string Name,
    int Level,
    int MaxLevel,
    int Experience,
    int MaxExperience,
    int Energy,
    int MaxEnergy,
    int Happiness,
    int MaxHappiness,
    int Scratches,
    Id OwnerId,
    int Age,
    string OwnerName,
    int BreedId,
    bool HasFreeSaddle,
    bool IsRiding,
    IReadOnlyList<int> SkillThresholds,
    int AccessRights,
    bool CanBreed,
    bool CanHarvest,
    bool CanRevive,
    int RarityLevel,
    int MaxWellbeingSeconds,
    int RemainingWellbeingSeconds,
    int RemainingGrowingSeconds,
    bool HasBreedingPermission);
