using Qx.Messages;
using Qx.Model.Crafting;

namespace Qx.Model.Messages.Incoming;

public sealed record CraftableProducts : IParserComposer<CraftableProducts>
{
    private IReadOnlyList<CraftingProduct> _products =
        Array.AsReadOnly(Array.Empty<CraftingProduct>());
    private IReadOnlyList<string> _usable_inventory_furniture_classes =
        Array.AsReadOnly(Array.Empty<string>());

    public CraftableProducts(
        IReadOnlyList<CraftingProduct> Products,
        IReadOnlyList<string> UsableInventoryFurnitureClasses)
    {
        this.Products = Products;
        this.UsableInventoryFurnitureClasses = UsableInventoryFurnitureClasses;
    }

    public IReadOnlyList<CraftingProduct> Products
    {
        get => _products;
        init => _products = CraftingWire.FreezeReferences(value, nameof(Products));
    }

    public IReadOnlyList<string> UsableInventoryFurnitureClasses
    {
        get => _usable_inventory_furniture_classes;
        init => _usable_inventory_furniture_classes =
            CraftingWire.FreezeStrings(value, nameof(UsableInventoryFurnitureClasses));
    }

    public static CraftableProducts Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    public void Deconstruct(
        out IReadOnlyList<CraftingProduct> Products,
        out IReadOnlyList<string> UsableInventoryFurnitureClasses)
    {
        Products = this.Products;
        UsableInventoryFurnitureClasses = this.UsableInventoryFurnitureClasses;
    }

    private static CraftableProducts ParseFlash(in PacketReader p) =>
        ParseLayout(in p, true);

    private static void ComposeFlash(CraftableProducts value, in PacketWriter p) =>
        ComposeLayout(value, true, in p);

    private static void ComposeUnity(CraftableProducts value, in PacketWriter p) =>
        ComposeLayout(value, false, in p);

    private static CraftableProducts ParseUnity(in PacketReader p)
    {
        int start = p.Pos;
        CraftableProducts? legacy = TryParseLayout(
            in p,
            false,
            out bool legacy_valid);
        p.Pos = start;
        CraftableProducts? current = TryParseLayout(
            in p,
            true,
            out bool current_valid);
        p.Pos = start;

        if (legacy_valid && current_valid)
        {
            if (legacy!.Products.Count == 0 && current!.Products.Count == 0)
                return ParseLayout(in p, false);
            throw new InvalidDataException(
                "The Unity craftable-product layout is ambiguous.");
        }
        if (!legacy_valid && !current_valid)
        {
            throw new InvalidDataException(
                "The Unity craftable-product layout is unsupported.");
        }
        return ParseLayout(in p, current_valid);
    }

    private static CraftableProducts ParseLayout(
        in PacketReader p,
        bool has_product_code)
    {
        var strings = CraftingWire.NewStringBudget();
        int count_width = CraftingWire.CountWidth(p.Client);
        int product_minimum_bytes = has_product_code
            ? CraftingWire.StringPrefixBytes * 3
            : CraftingWire.StringPrefixBytes * 2;
        int product_count = CraftingWire.ReadCount(
            in p,
            product_minimum_bytes,
            count_width,
            nameof(Products));
        var products = new CraftingProduct[product_count];
        for (int index = 0; index < products.Length; index++)
        {
            int sibling_bytes = checked(
                (products.Length - index - 1) * product_minimum_bytes);
            products[index] = CraftingWire.ParseProduct(
                in p,
                has_product_code,
                checked(sibling_bytes + count_width),
                ref strings);
        }

        int usable_count = CraftingWire.ReadCount(
            in p,
            CraftingWire.StringPrefixBytes,
            0,
            nameof(UsableInventoryFurnitureClasses));
        var usable_inventory_furniture_classes = new string[usable_count];
        for (int index = 0; index < usable_inventory_furniture_classes.Length; index++)
        {
            int sibling_bytes = checked(
                (usable_inventory_furniture_classes.Length - index - 1) *
                CraftingWire.StringPrefixBytes);
            usable_inventory_furniture_classes[index] = strings.Read(
                in p,
                nameof(UsableInventoryFurnitureClasses),
                sibling_bytes);
        }
        CraftingWire.RequireEmpty(in p, nameof(CraftableProducts));
        return new CraftableProducts(
            products,
            usable_inventory_furniture_classes);
    }

    private static CraftableProducts? TryParseLayout(
        in PacketReader p,
        bool has_product_code,
        out bool valid)
    {
        try
        {
            CraftableProducts products = ParseLayout(in p, has_product_code);
            valid = true;
            return products;
        }
        catch (Exception error) when (
            error is InvalidDataException or
                IndexOutOfRangeException or
                ArgumentOutOfRangeException or
                OverflowException)
        {
            valid = false;
            return null;
        }
    }

    private static void ComposeLayout(
        CraftableProducts value,
        bool flash,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int product_count = CraftingWire.RequireListCount(
            value.Products,
            nameof(value.Products));
        int usable_count = CraftingWire.RequireListCount(
            value.UsableInventoryFurnitureClasses,
            nameof(value.UsableInventoryFurnitureClasses));
        var strings = CraftingWire.NewStringBudget();
        bool? has_product_code = null;
        foreach (CraftingProduct product in value.Products)
        {
            ArgumentNullException.ThrowIfNull(product, nameof(value.Products));
            if (has_product_code is bool expected && expected != product.HasProductCode)
            {
                throw new InvalidDataException(
                    "Crafting products cannot mix two-string and three-string layouts.");
            }
            has_product_code = product.HasProductCode;
            CraftingWire.PrepareProduct(product, ref strings, in p);
        }
        if (flash && has_product_code is false)
        {
            throw new InvalidDataException(
                "Flash craftable products require product codes.");
        }
        foreach (string furniture_class in value.UsableInventoryFurnitureClasses)
        {
            strings.Require(
                furniture_class,
                nameof(value.UsableInventoryFurnitureClasses),
                in p);
        }

        CraftingWire.WriteCount(product_count, in p);
        foreach (CraftingProduct product in value.Products)
            CraftingWire.WriteProduct(product, in p);
        CraftingWire.WriteCount(usable_count, in p);
        foreach (string furniture_class in value.UsableInventoryFurnitureClasses)
            p.WriteString(furniture_class);
    }
}

public sealed record CraftingRecipe : IParserComposer<CraftingRecipe>
{
    private IReadOnlyList<CraftingIngredient> _ingredients =
        Array.AsReadOnly(Array.Empty<CraftingIngredient>());

    public CraftingRecipe(IReadOnlyList<CraftingIngredient> Ingredients)
    {
        this.Ingredients = Ingredients;
    }

    public IReadOnlyList<CraftingIngredient> Ingredients
    {
        get => _ingredients;
        init => _ingredients = CraftingWire.FreezeReferences(value, nameof(Ingredients));
    }

    public static CraftingRecipe Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    public void Deconstruct(out IReadOnlyList<CraftingIngredient> Ingredients)
    {
        Ingredients = this.Ingredients;
    }

    private static CraftingRecipe ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static CraftingRecipe ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(CraftingRecipe value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static void ComposeUnity(CraftingRecipe value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static CraftingRecipe ParseLayout(in PacketReader p)
    {
        int minimum_bytes = checked(sizeof(int) + CraftingWire.StringPrefixBytes);
        int count = CraftingWire.ReadCount(
            in p,
            minimum_bytes,
            0,
            nameof(Ingredients));
        var strings = CraftingWire.NewStringBudget();
        var ingredients = new CraftingIngredient[count];
        for (int index = 0; index < ingredients.Length; index++)
        {
            int sibling_bytes = checked(
                (ingredients.Length - index - 1) * minimum_bytes);
            ingredients[index] = CraftingWire.ParseIngredient(
                in p,
                sibling_bytes,
                ref strings);
        }
        CraftingWire.RequireEmpty(in p, nameof(CraftingRecipe));
        return new CraftingRecipe(ingredients);
    }

    private static void ComposeLayout(CraftingRecipe value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = CraftingWire.RequireListCount(
            value.Ingredients,
            nameof(value.Ingredients));
        var strings = CraftingWire.NewStringBudget();
        foreach (CraftingIngredient ingredient in value.Ingredients)
            CraftingWire.PrepareIngredient(ingredient, ref strings, in p);

        CraftingWire.WriteCount(count, in p);
        foreach (CraftingIngredient ingredient in value.Ingredients)
            CraftingWire.WriteIngredient(ingredient, in p);
    }
}

public sealed record CraftingResult(
    bool Success,
    CraftingProduct? Product)
    : IParserComposer<CraftingResult>
{
    public static CraftingResult Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static CraftingResult ParseFlash(in PacketReader p)
    {
        CraftingWire.RequireRemaining(in p, sizeof(byte), 0, nameof(CraftingResult));
        bool success = p.ReadBool();
        CraftingProduct? product = success
            ? ParseProductRoot(in p, true)
            : null;
        CraftingWire.RequireEmpty(in p, nameof(CraftingResult));
        return new CraftingResult(success, product);
    }

    private static CraftingResult ParseUnity(in PacketReader p)
    {
        CraftingWire.RequireRemaining(in p, sizeof(byte), 0, nameof(CraftingResult));
        bool success = p.ReadBool();
        return new CraftingResult(success, ParseUnityProduct(in p));
    }

    private static CraftingProduct ParseUnityProduct(in PacketReader p)
    {
        int start = p.Pos;
        CraftingProduct? legacy = TryParseProductRoot(
            in p,
            false,
            out bool legacy_valid);
        p.Pos = start;
        CraftingProduct? current = TryParseProductRoot(
            in p,
            true,
            out bool current_valid);
        p.Pos = start;

        if (legacy_valid == current_valid)
        {
            throw new InvalidDataException(legacy_valid
                ? "The Unity crafting-result product layout is ambiguous."
                : "The Unity crafting-result product layout is unsupported.");
        }
        return ParseProductRoot(in p, current_valid);
    }

    private static CraftingProduct ParseProductRoot(
        in PacketReader p,
        bool has_product_code)
    {
        var strings = CraftingWire.NewStringBudget();
        CraftingProduct product = CraftingWire.ParseProduct(
            in p,
            has_product_code,
            0,
            ref strings);
        CraftingWire.RequireEmpty(in p, nameof(CraftingResult));
        return product;
    }

    private static CraftingProduct? TryParseProductRoot(
        in PacketReader p,
        bool has_product_code,
        out bool valid)
    {
        try
        {
            CraftingProduct product = ParseProductRoot(in p, has_product_code);
            valid = true;
            return product;
        }
        catch (Exception error) when (
            error is InvalidDataException or
                IndexOutOfRangeException or
                ArgumentOutOfRangeException or
                OverflowException)
        {
            valid = false;
            return null;
        }
    }

    private static void ComposeFlash(CraftingResult value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Success != (value.Product is not null))
        {
            throw new InvalidDataException(
                "Flash crafting results contain a product exactly when crafting succeeds.");
        }
        var strings = CraftingWire.NewStringBudget();
        if (value.Product is CraftingProduct product)
            CraftingWire.PrepareProduct(product, ref strings, in p);

        p.WriteBool(value.Success);
        if (value.Product is CraftingProduct composed_product)
            CraftingWire.WriteProduct(composed_product, in p);
    }

    private static void ComposeUnity(CraftingResult value, in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        CraftingProduct product = value.Product ??
            throw new InvalidDataException(
                "Unity crafting results always contain a product.");
        var strings = CraftingWire.NewStringBudget();
        CraftingWire.PrepareProduct(product, ref strings, in p);

        p.WriteBool(value.Success);
        CraftingWire.WriteProduct(product, in p);
    }
}

public sealed record CraftingRecipesAvailable(
    int Count,
    bool IsRecipeComplete)
    : IParserComposer<CraftingRecipesAvailable>
{
    public static CraftingRecipesAvailable Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static CraftingRecipesAvailable ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static CraftingRecipesAvailable ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(
        CraftingRecipesAvailable value,
        in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static void ComposeUnity(
        CraftingRecipesAvailable value,
        in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static CraftingRecipesAvailable ParseLayout(in PacketReader p)
    {
        CraftingWire.RequireRemaining(
            in p,
            checked(sizeof(int) + sizeof(byte)),
            0,
            nameof(CraftingRecipesAvailable));
        var value = new CraftingRecipesAvailable(p.ReadInt(), p.ReadBool());
        CraftingWire.RequireEmpty(in p, nameof(CraftingRecipesAvailable));
        return value;
    }

    private static void ComposeLayout(
        CraftingRecipesAvailable value,
        in PacketWriter p)
    {
        ArgumentNullException.ThrowIfNull(value);
        p.WriteInt(value.Count);
        p.WriteBool(value.IsRecipeComplete);
    }
}

public sealed record GetCraftableProducts(
    Id CraftingFurnitureId)
    : IParserComposer<GetCraftableProducts>
{
    public static GetCraftableProducts Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static GetCraftableProducts ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static GetCraftableProducts ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(GetCraftableProducts value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static void ComposeUnity(GetCraftableProducts value, in PacketWriter p) =>
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
        CraftingWire.RequireId(value.CraftingFurnitureId, p.Client);
        CraftingWire.WriteId(value.CraftingFurnitureId, in p);
    }
}

public sealed record GetCraftingRecipe(
    string RecipeCode)
    : IParserComposer<GetCraftingRecipe>
{
    public static GetCraftingRecipe Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static GetCraftingRecipe ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static GetCraftingRecipe ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(GetCraftingRecipe value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static void ComposeUnity(GetCraftingRecipe value, in PacketWriter p) =>
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

public sealed record Craft(
    Id CraftingFurnitureId,
    string RecipeCode)
    : IParserComposer<Craft>
{
    public static Craft Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    private static Craft ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static Craft ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(Craft value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static void ComposeUnity(Craft value, in PacketWriter p) =>
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
        CraftingWire.RequireId(value.CraftingFurnitureId, p.Client);
        var strings = CraftingWire.NewStringBudget();
        strings.Require(value.RecipeCode, nameof(value.RecipeCode), in p);

        CraftingWire.WriteId(value.CraftingFurnitureId, in p);
        p.WriteString(value.RecipeCode);
    }
}

public sealed record CraftSecret : IParserComposer<CraftSecret>
{
    private IReadOnlyList<Id> _ingredient_item_ids =
        Array.AsReadOnly(Array.Empty<Id>());

    public CraftSecret(
        Id CraftingFurnitureId,
        IReadOnlyList<Id> IngredientItemIds)
    {
        this.CraftingFurnitureId = CraftingFurnitureId;
        this.IngredientItemIds = IngredientItemIds;
    }

    public Id CraftingFurnitureId { get; init; }

    public IReadOnlyList<Id> IngredientItemIds
    {
        get => _ingredient_item_ids;
        init => _ingredient_item_ids =
            CraftingWire.FreezeValues(value, nameof(IngredientItemIds));
    }

    public static CraftSecret Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    public void Deconstruct(
        out Id CraftingFurnitureId,
        out IReadOnlyList<Id> IngredientItemIds)
    {
        CraftingFurnitureId = this.CraftingFurnitureId;
        IngredientItemIds = this.IngredientItemIds;
    }

    private static CraftSecret ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static CraftSecret ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(CraftSecret value, in PacketWriter p) =>
        ComposeLayout(value.CraftingFurnitureId, value.IngredientItemIds, in p);

    private static void ComposeUnity(CraftSecret value, in PacketWriter p) =>
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
        int count_width = CraftingWire.CountWidth(p.Client);
        int id_width = CraftingWire.IdWidth(p.Client);
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
        CraftingWire.RequireId(furniture_id, p.Client);
        int count = CraftingWire.RequireListCount(item_ids, nameof(IngredientItemIds));
        foreach (Id item_id in item_ids)
            CraftingWire.RequireId(item_id, p.Client);

        CraftingWire.WriteId(furniture_id, in p);
        CraftingWire.WriteCount(count, in p);
        foreach (Id item_id in item_ids)
            CraftingWire.WriteId(item_id, in p);
    }
}

public sealed record GetCraftingRecipesAvailable
    : IParserComposer<GetCraftingRecipesAvailable>
{
    private IReadOnlyList<Id> _ingredient_item_ids =
        Array.AsReadOnly(Array.Empty<Id>());

    public GetCraftingRecipesAvailable(
        Id CraftingFurnitureId,
        IReadOnlyList<Id> IngredientItemIds)
    {
        this.CraftingFurnitureId = CraftingFurnitureId;
        this.IngredientItemIds = IngredientItemIds;
    }

    public Id CraftingFurnitureId { get; init; }

    public IReadOnlyList<Id> IngredientItemIds
    {
        get => _ingredient_item_ids;
        init => _ingredient_item_ids =
            CraftingWire.FreezeValues(value, nameof(IngredientItemIds));
    }

    public static GetCraftingRecipesAvailable Parse(in PacketReader p) =>
        ModernWireClients.Parse(in p, ParseFlash, ParseUnity);

    public void Compose(in PacketWriter p) =>
        ModernWireClients.Compose(this, in p, ComposeFlash, ComposeUnity);

    public void Deconstruct(
        out Id CraftingFurnitureId,
        out IReadOnlyList<Id> IngredientItemIds)
    {
        CraftingFurnitureId = this.CraftingFurnitureId;
        IngredientItemIds = this.IngredientItemIds;
    }

    private static GetCraftingRecipesAvailable ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static GetCraftingRecipesAvailable ParseUnity(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(
        GetCraftingRecipesAvailable value,
        in PacketWriter p) =>
        CraftSecret.ComposeLayout(
            value.CraftingFurnitureId,
            value.IngredientItemIds,
            in p);

    private static void ComposeUnity(
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
