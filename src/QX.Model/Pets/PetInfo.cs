using Qx.Messages;

namespace Qx.Model;

/// <summary>Represents the detailed statistics of a single pet, as returned by <c>GetPetInfo</c>.</summary>
/// <remarks>
/// This message does not carry the pet type. The type is only present on the room
/// entity (<see cref="Pet.PetType"/>), and both values are needed together: the
/// client resolves a pet's displayed breed through the localization key
/// <c>pet.breed.{Pet.PetType}.{BreedId}</c>.
/// </remarks>
public sealed class PetInfo : IParserComposer<PetInfo>
{
    private IReadOnlyList<int> skill_thresholds = Array.AsReadOnly(Array.Empty<int>());

    /// <summary>Gets or sets the pet identifier.</summary>
    public Id Id { get; set; }
    /// <summary>Gets or sets the pet's name.</summary>
    public string Name { get; set; } = "";
    /// <summary>Gets or sets the pet's current level.</summary>
    public int Level { get; set; }
    /// <summary>Gets or sets the highest level the pet can reach.</summary>
    public int MaxLevel { get; set; }
    /// <summary>Gets or sets the experience the pet has gathered towards the next level.</summary>
    public int Experience { get; set; }
    /// <summary>Gets or sets the experience the pet needs for the next level.</summary>
    public int MaxExperience { get; set; }
    /// <summary>Gets or sets the pet's current energy.</summary>
    public int Energy { get; set; }
    /// <summary>Gets or sets the pet's energy cap.</summary>
    public int MaxEnergy { get; set; }
    /// <summary>Gets or sets the pet's nutrition level.</summary>
    /// <remarks>The client calls this field <c>nutrition</c>.</remarks>
    public int Happiness { get; set; }
    /// <summary>Gets or sets the pet's nutrition cap.</summary>
    /// <remarks>The client calls this field <c>maxNutrition</c>.</remarks>
    public int MaxHappiness { get; set; }
    /// <summary>Gets or sets the number of respects the pet has received.</summary>
    /// <remarks>The client calls this field <c>respect</c>.</remarks>
    public int Scratches { get; set; }
    /// <summary>Gets or sets the identifier of the pet's owner.</summary>
    public Id OwnerId { get; set; }
    /// <summary>Gets or sets the pet's age in days.</summary>
    public int Age { get; set; }
    /// <summary>Gets or sets the name of the pet's owner.</summary>
    public string OwnerName { get; set; } = "";
    /// <summary>
    /// Gets or sets the breed variant of the pet within its type, not the pet type itself.
    /// </summary>
    /// <remarks>
    /// Only meaningful together with <see cref="Pet.PetType"/> from the room entity.
    /// Pet types that have no breed variants, most notably the monsterplant (type 16),
    /// report 0 here, so a zero value must not be read as "unknown pet".
    /// Use <see cref="Pet.PetType"/> to identify what kind of pet this is.
    /// </remarks>
    public int BreedId { get; set; }
    /// <summary>Gets or sets whether the pet's saddle is unlocked without a purchase.</summary>
    public bool HasFreeSaddle { get; set; }
    /// <summary>Gets or sets whether a user is riding the pet.</summary>
    public bool IsRiding { get; set; }
    /// <summary>Gets or sets the experience thresholds at which the pet unlocks its skills; the list is copied on set.</summary>
    /// <exception cref="ArgumentNullException">Thrown when set to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a list with more than 65535 values.</exception>
    public IReadOnlyList<int> SkillThresholds
    {
        get => skill_thresholds;
        set => skill_thresholds = RoomObjectReadWire.Freeze(value);
    }
    /// <summary>Gets or sets the hotel's access rights code for who may command the pet.</summary>
    /// <remarks>The numbering is hotel-specific and is passed through unchanged.</remarks>
    public int AccessRights { get; set; }
    /// <summary>Gets or sets whether the pet may be bred right now.</summary>
    public bool CanBreed { get; set; }
    /// <summary>Gets or sets whether the pet may be harvested right now.</summary>
    public bool CanHarvest { get; set; }
    /// <summary>Gets or sets whether the pet is dead and may be revived.</summary>
    public bool CanRevive { get; set; }
    /// <summary>Gets or sets the pet's rarity tier as sent by the hotel.</summary>
    public int RarityLevel { get; set; }
    /// <summary>Gets or sets the full length of the pet's wellbeing timer in seconds.</summary>
    public int MaxWellbeingSeconds { get; set; }
    /// <summary>Gets or sets the seconds of wellbeing the pet has left.</summary>
    public int RemainingWellbeingSeconds { get; set; }
    /// <summary>Gets or sets the seconds left in the pet's current growth stage, which applies to monsterplants.</summary>
    public int RemainingGrowingSeconds { get; set; }
    /// <summary>Gets or sets whether the local user may breed the pet.</summary>
    public bool HasBreedingPermission { get; set; }

    /// <summary>Initializes a new instance of the <see cref="PetInfo"/> class.</summary>
    public PetInfo() { }

    /// <summary>Reads pet info from a packet.</summary>
    /// <param name="p">The packet to read from.</param>
    /// <exception cref="InvalidDataException">Thrown when the payload is too short or bytes remain after the last field.</exception>
    public static PetInfo Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    private static PetInfo ParseFlash(in PacketReader p) => ParseMessage(in p);

    private static PetInfo ParseMessage(in PacketReader p)
    {
        var strings = new RoomObjectReadStringBudget();
        RoomObjectReadWire.RequireRemaining(
            in p,
            checked(RoomObjectReadWire.IdWidth + sizeof(short)),
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

    /// <summary>Writes the pet info to a packet.</summary>
    /// <param name="p">The packet to write to.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static void ComposeFlash(PetInfo value, in PacketWriter p) => ComposeMessage(value, in p);

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
        RoomObjectReadWire.RequireWireId(value.Id, nameof(Id));
        RoomObjectReadWire.RequireWireId(value.OwnerId, nameof(OwnerId));
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
