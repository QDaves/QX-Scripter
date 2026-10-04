using Qx.Messages;

namespace Qx.Model.Messages.Outgoing;

/// <summary>
/// Represents the <c>GetCraftableProducts</c> message, sent to request the products a crafting furniture can make.
/// </summary>
/// <param name="CraftingFurnitureId">The floor item identifier of the crafting furniture.</param>
public sealed record GetCraftableProducts(
    Id CraftingFurnitureId)
    : IParserComposer<GetCraftableProducts>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetCraftableProducts Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static GetCraftableProducts ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(GetCraftableProducts value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static GetCraftableProducts ParseLayout(in PacketReader p)
    {
        var value = new GetCraftableProducts(
            CraftingWire.ReadId(in p, 0, nameof(CraftingFurnitureId)));
        CraftingWire.RequireEmpty(in p, nameof(GetCraftableProducts));
        return value;
    }

    private static void ComposeLayout(GetCraftableProducts value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CraftingWire.RequireId(value.CraftingFurnitureId);
        CraftingWire.WriteId(value.CraftingFurnitureId, in p);
    }
}

/// <summary>Represents the <c>GetCraftingRecipe</c> message, sent to request the ingredients of a recipe.</summary>
/// <param name="RecipeCode">The recipe code taken from a craftable product.</param>
public sealed record GetCraftingRecipe(
    string RecipeCode)
    : IParserComposer<GetCraftingRecipe>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetCraftingRecipe Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static GetCraftingRecipe ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(GetCraftingRecipe value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static GetCraftingRecipe ParseLayout(in PacketReader p)
    {
        var strings = CraftingWire.NewStringBudget();
        string recipe_code = strings.Read(in p, nameof(RecipeCode), 0);
        CraftingWire.RequireEmpty(in p, nameof(GetCraftingRecipe));
        return new GetCraftingRecipe(recipe_code);
    }

    private static void ComposeLayout(GetCraftingRecipe value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        var strings = CraftingWire.NewStringBudget();
        strings.Require(value.RecipeCode, nameof(value.RecipeCode), in p);
        p.WriteString(value.RecipeCode);
    }
}

/// <summary>Represents the <c>Craft</c> message, sent to craft a recipe with a crafting furniture.</summary>
/// <param name="CraftingFurnitureId">The floor item identifier of the crafting furniture.</param>
/// <param name="RecipeCode">The recipe code to craft.</param>
public sealed record Craft(
    Id CraftingFurnitureId,
    string RecipeCode)
    : IParserComposer<Craft>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static Craft Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static Craft ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(Craft value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static Craft ParseLayout(in PacketReader p)
    {
        Id crafting_furniture_id = CraftingWire.ReadId(
            in p,
            CraftingWire.StringPrefixBytes,
            nameof(CraftingFurnitureId));
        var strings = CraftingWire.NewStringBudget();
        string recipe_code = strings.Read(in p, nameof(RecipeCode), 0);
        CraftingWire.RequireEmpty(in p, nameof(Craft));
        return new Craft(crafting_furniture_id, recipe_code);
    }

    private static void ComposeLayout(Craft value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CraftingWire.RequireId(value.CraftingFurnitureId);
        var strings = CraftingWire.NewStringBudget();
        strings.Require(value.RecipeCode, nameof(value.RecipeCode), in p);

        CraftingWire.WriteId(value.CraftingFurnitureId, in p);
        p.WriteString(value.RecipeCode);
    }
}

/// <summary>
/// Represents the <c>CraftSecret</c> message, sent to craft a secret recipe from a set of ingredient items.
/// </summary>
public sealed record CraftSecret : IParserComposer<CraftSecret>
{
    private IReadOnlyList<Id> _ingredient_item_ids =
        Array.AsReadOnly(Array.Empty<Id>());

    /// <summary>Initializes a new instance of the <see cref="CraftSecret"/> record.</summary>
    /// <param name="craftingFurnitureId">The floor item identifier of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item identifiers to consume, copied into a read only list.</param>
    public CraftSecret(
        Id craftingFurnitureId,
        IReadOnlyList<Id> ingredientItemIds)
    {
        CraftingFurnitureId = craftingFurnitureId;
        IngredientItemIds = ingredientItemIds;
    }

    /// <summary>Gets the floor item identifier of the crafting furniture.</summary>
    public Id CraftingFurnitureId { get; init; }

    /// <summary>Gets the inventory item identifiers to consume, as a read only copy.</summary>
    public IReadOnlyList<Id> IngredientItemIds
    {
        get => _ingredient_item_ids;
        init => _ingredient_item_ids =
            CraftingWire.FreezeValues(value, nameof(IngredientItemIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CraftSecret Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="craftingFurnitureId">The floor item identifier of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item identifiers to consume.</param>
    public void Deconstruct(
        out Id craftingFurnitureId,
        out IReadOnlyList<Id> ingredientItemIds)
    {
        craftingFurnitureId = CraftingFurnitureId;
        ingredientItemIds = IngredientItemIds;
    }

    private static CraftSecret ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(CraftSecret value, in PacketWriter p) =>
        ComposeLayout(value.CraftingFurnitureId, value.IngredientItemIds, in p);

    private static CraftSecret ParseLayout(in PacketReader p)
    {
        (Id furniture_id, Id[] item_ids) = ParseIngredientItems(
            in p,
            nameof(CraftSecret));
        return new CraftSecret(furniture_id, item_ids);
    }

    internal static (Id FurnitureId, Id[] ItemIds) ParseIngredientItems(
        in PacketReader p,
        string name)
    {
        int count_width = CraftingWire.CountWidth;
        int id_width = CraftingWire.IdWidth;
        Id furniture_id = CraftingWire.ReadId(
            in p,
            count_width,
            nameof(CraftingFurnitureId));
        int count = CraftingWire.ReadCount(
            in p,
            id_width,
            0,
            nameof(IngredientItemIds));
        var item_ids = new Id[count];
        for (int index = 0; index < item_ids.Length; index++)
        {
            int sibling_bytes = checked((item_ids.Length - index - 1) * id_width);
            item_ids[index] = CraftingWire.ReadId(
                in p,
                sibling_bytes,
                nameof(IngredientItemIds));
        }
        CraftingWire.RequireEmpty(in p, name);
        return (furniture_id, item_ids);
    }

    internal static void ComposeLayout(
        Id furniture_id,
        IReadOnlyList<Id> item_ids,
        in PacketWriter p)
    {
        CraftingWire.RequireId(furniture_id);
        int count = CraftingWire.RequireListCount(item_ids, nameof(IngredientItemIds));
        foreach (Id item_id in item_ids)
            CraftingWire.RequireId(item_id);

        CraftingWire.WriteId(furniture_id, in p);
        CraftingWire.WriteCount(count, in p);
        foreach (Id item_id in item_ids)
            CraftingWire.WriteId(item_id, in p);
    }
}

/// <summary>
/// Represents the <c>GetCraftingRecipesAvailable</c> message, sent to request the number of recipes that match a set
/// of ingredient items.
/// </summary>
public sealed record GetCraftingRecipesAvailable
    : IParserComposer<GetCraftingRecipesAvailable>
{
    private IReadOnlyList<Id> _ingredient_item_ids =
        Array.AsReadOnly(Array.Empty<Id>());

    /// <summary>Initializes a new instance of the <see cref="GetCraftingRecipesAvailable"/> record.</summary>
    /// <param name="craftingFurnitureId">The floor item identifier of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item identifiers to check, copied into a read only list.</param>
    public GetCraftingRecipesAvailable(
        Id craftingFurnitureId,
        IReadOnlyList<Id> ingredientItemIds)
    {
        CraftingFurnitureId = craftingFurnitureId;
        IngredientItemIds = ingredientItemIds;
    }

    /// <summary>Gets the floor item identifier of the crafting furniture.</summary>
    public Id CraftingFurnitureId { get; init; }

    /// <summary>Gets the inventory item identifiers to check, as a read only copy.</summary>
    public IReadOnlyList<Id> IngredientItemIds
    {
        get => _ingredient_item_ids;
        init => _ingredient_item_ids =
            CraftingWire.FreezeValues(value, nameof(IngredientItemIds));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static GetCraftingRecipesAvailable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="craftingFurnitureId">The floor item identifier of the crafting furniture.</param>
    /// <param name="ingredientItemIds">The inventory item identifiers to check.</param>
    public void Deconstruct(
        out Id craftingFurnitureId,
        out IReadOnlyList<Id> ingredientItemIds)
    {
        craftingFurnitureId = CraftingFurnitureId;
        ingredientItemIds = IngredientItemIds;
    }

    private static GetCraftingRecipesAvailable ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(
        GetCraftingRecipesAvailable value,
        in PacketWriter p) =>
        CraftSecret.ComposeLayout(
            value.CraftingFurnitureId,
            value.IngredientItemIds,
            in p);

    private static GetCraftingRecipesAvailable ParseLayout(in PacketReader p)
    {
        (Id furniture_id, Id[] item_ids) = CraftSecret.ParseIngredientItems(
            in p,
            nameof(GetCraftingRecipesAvailable));
        return new GetCraftingRecipesAvailable(furniture_id, item_ids);
    }
}
