using Qx.Messages;

namespace Qx.Model.Messages.Incoming;

/// <summary>
/// Represents the <c>CraftableProducts</c> message, received with the products a crafting furniture can make.
/// </summary>
public sealed record CraftableProducts : IParserComposer<CraftableProducts>
{
    private IReadOnlyList<CraftingProduct> _products =
        Array.AsReadOnly(Array.Empty<CraftingProduct>());
    private IReadOnlyList<string> _usable_inventory_furniture_classes =
        Array.AsReadOnly(Array.Empty<string>());

    /// <summary>Initializes a new instance of the <see cref="CraftableProducts"/> record.</summary>
    /// <param name="products">The products that can be crafted, copied into a read only list.</param>
    /// <param name="usableInventoryFurnitureClasses">
    /// The inventory furniture class names that can be used as ingredients, copied into a read only list.
    /// </param>
    public CraftableProducts(
        IReadOnlyList<CraftingProduct> products,
        IReadOnlyList<string> usableInventoryFurnitureClasses)
    {
        Products = products;
        UsableInventoryFurnitureClasses = usableInventoryFurnitureClasses;
    }

    /// <summary>Gets the products that can be crafted, as a read only copy.</summary>
    public IReadOnlyList<CraftingProduct> Products
    {
        get => _products;
        init => _products = CraftingWire.FreezeReferences(value, nameof(Products));
    }

    /// <summary>
    /// Gets the inventory furniture class names that can be used as ingredients, as a read only copy.
    /// </summary>
    public IReadOnlyList<string> UsableInventoryFurnitureClasses
    {
        get => _usable_inventory_furniture_classes;
        init => _usable_inventory_furniture_classes =
            CraftingWire.FreezeStrings(value, nameof(UsableInventoryFurnitureClasses));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CraftableProducts Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    /// <summary>Deconstructs the message into its parts.</summary>
    /// <param name="products">The products that can be crafted.</param>
    /// <param name="usableInventoryFurnitureClasses">
    /// The inventory furniture class names that can be used as ingredients.
    /// </param>
    public void Deconstruct(
        out IReadOnlyList<CraftingProduct> products,
        out IReadOnlyList<string> usableInventoryFurnitureClasses)
    {
        products = Products;
        usableInventoryFurnitureClasses = UsableInventoryFurnitureClasses;
    }

    private static CraftableProducts ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(CraftableProducts value, in PacketWriter p) =>
        ComposeLayout(value, in p);

    private static CraftableProducts ParseLayout(in PacketReader p)
    {
        var strings = CraftingWire.NewStringBudget();
        int count_width = CraftingWire.CountWidth;
        int product_minimum_bytes = CraftingWire.StringPrefixBytes * 3;
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

    private static void ComposeLayout(
        CraftableProducts value,
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
        foreach (CraftingProduct product in value.Products)
        {
            ArgumentNullException.ThrowIfNull(product, nameof(value.Products));
            CraftingWire.PrepareProduct(product, ref strings, in p);
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

/// <summary>Represents the <c>CraftingRecipe</c> message, received with the ingredients of a recipe.</summary>
public sealed record CraftingRecipe : IParserComposer<CraftingRecipe>
{
    private IReadOnlyList<CraftingIngredient> _ingredients =
        Array.AsReadOnly(Array.Empty<CraftingIngredient>());

    /// <summary>Initializes a new instance of the <see cref="CraftingRecipe"/> record.</summary>
    /// <param name="ingredients">The ingredients of the recipe, copied into a read only list.</param>
    public CraftingRecipe(IReadOnlyList<CraftingIngredient> ingredients)
    {
        Ingredients = ingredients;
    }

    /// <summary>Gets the ingredients of the recipe, as a read only copy.</summary>
    public IReadOnlyList<CraftingIngredient> Ingredients
    {
        get => _ingredients;
        init => _ingredients = CraftingWire.FreezeReferences(value, nameof(Ingredients));
    }

    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CraftingRecipe Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    /// <summary>Deconstructs the message into its ingredients.</summary>
    /// <param name="ingredients">The ingredients of the recipe.</param>
    public void Deconstruct(out IReadOnlyList<CraftingIngredient> ingredients)
    {
        ingredients = Ingredients;
    }

    private static CraftingRecipe ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(CraftingRecipe value, in PacketWriter p) =>
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

/// <summary>Represents the <c>CraftingResult</c> message, received with the result of a craft.</summary>
/// <param name="Success">Whether the craft succeeded.</param>
/// <param name="Product">
/// The crafted product when <paramref name="Success"/> is <see langword="true"/>; otherwise, <see langword="null"/>.
/// </param>
public sealed record CraftingResult(
    bool Success,
    CraftingProduct? Product)
    : IParserComposer<CraftingResult>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CraftingResult Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when <see cref="Product"/> is set on a failed result or missing on a successful one.
    /// </exception>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static CraftingResult ParseFlash(in PacketReader p)
    {
        CraftingWire.RequireRemaining(in p, sizeof(byte), 0, nameof(CraftingResult));
        bool success = p.ReadBool();
        CraftingProduct? product = success
            ? ParseProductRoot(in p)
            : null;
        CraftingWire.RequireEmpty(in p, nameof(CraftingResult));
        return new CraftingResult(success, product);
    }

    private static CraftingProduct ParseProductRoot(in PacketReader p)
    {
        var strings = CraftingWire.NewStringBudget();
        CraftingProduct product = CraftingWire.ParseProduct(
            in p,
            0,
            ref strings);
        CraftingWire.RequireEmpty(in p, nameof(CraftingResult));
        return product;
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
}

/// <summary>
/// Represents the <c>CraftingRecipesAvailable</c> message, received with the number of recipes that match a set of
/// ingredients.
/// </summary>
/// <param name="Count">The number of recipes that match the ingredients.</param>
/// <param name="IsRecipeComplete">Whether the ingredients form a complete recipe.</param>
public sealed record CraftingRecipesAvailable(
    int Count,
    bool IsRecipeComplete)
    : IParserComposer<CraftingRecipesAvailable>
{
    /// <summary>Parses the message from a packet.</summary>
    /// <param name="p">The packet reader.</param>
    public static CraftingRecipesAvailable Parse(in PacketReader p) =>
        FlashWire.Parse(in p, ParseFlash);

    /// <summary>Composes the message into a packet.</summary>
    /// <param name="p">The packet writer.</param>
    public void Compose(in PacketWriter p) =>
        FlashWire.Compose(this, in p, ComposeFlash);

    private static CraftingRecipesAvailable ParseFlash(in PacketReader p) =>
        ParseLayout(in p);

    private static void ComposeFlash(
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
