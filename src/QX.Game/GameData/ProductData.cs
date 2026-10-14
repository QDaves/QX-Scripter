using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Game;

/// <summary>Represents one product entry from the hotel's product data.</summary>
/// <param name="Code">The product code the entry is keyed by.</param>
/// <param name="Name">The localized product name, empty when the entry has none.</param>
/// <param name="Description">The localized product description, empty when the entry has none.</param>
public sealed record ProductInfo(string Code, string Name, string Description);

/// <summary>Represents the hotel's product data, the localized names and descriptions keyed by product code.</summary>
/// <remarks>Product codes are compared case-sensitively.</remarks>
public sealed partial class ProductData : IReadOnlyDictionary<string, ProductInfo>
{
    private readonly Dictionary<string, ProductInfo> _products = new(StringComparer.Ordinal);

    /// <summary>Gets the number of products.</summary>
    public int Count => _products.Count;
    /// <summary>Gets the product codes.</summary>
    public IEnumerable<string> Keys => _products.Keys;
    /// <summary>Gets the product entries.</summary>
    public IEnumerable<ProductInfo> Values => _products.Values;

    /// <summary>Gets the product with a code, or <see langword="null"/> when the code is unknown.</summary>
    /// <param name="code">The product code, matched case-sensitively.</param>
    public ProductInfo? this[string code] => _products.GetValueOrDefault(code);

    ProductInfo IReadOnlyDictionary<string, ProductInfo>.this[string code] => _products[code];

    /// <summary>Gets the product with a code.</summary>
    /// <param name="code">The product code, matched case-sensitively.</param>
    /// <returns>The product, or <see langword="null"/> when the code is unknown.</returns>
    public ProductInfo? GetInfo(string code) => _products.GetValueOrDefault(code);

    /// <summary>Gets whether a product exists for a code.</summary>
    /// <param name="code">The product code, matched case-sensitively.</param>
    public bool ContainsKey(string code) => _products.ContainsKey(code);

    /// <summary>Gets the product with a code.</summary>
    /// <param name="code">The product code, matched case-sensitively.</param>
    /// <param name="info">The product, or <see langword="null"/> when the code is unknown.</param>
    /// <returns><see langword="true"/> when the code exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(string code, [NotNullWhen(true)] out ProductInfo? info) =>
        _products.TryGetValue(code, out info);

    /// <summary>Returns an enumerator over the code and product pairs.</summary>
    /// <returns>An enumerator over every product.</returns>
    public IEnumerator<KeyValuePair<string, ProductInfo>> GetEnumerator() =>
        _products.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Parses the hotel's <c>productdata_json</c> document.</summary>
    /// <remarks>
    /// A numeric product code is kept as its JSON text. When a code appears more than once, the
    /// last entry wins.
    /// </remarks>
    /// <param name="json">The JSON document.</param>
    /// <returns>The parsed product data.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the document is malformed or empty, has no product collection, or contains an
    /// entry without a code.
    /// </exception>
    public static ProductData LoadJson(string json)
    {
        ProductDataJson root = JsonSerializer.Deserialize(
            json,
            ProductDataJsonContext.Default.ProductDataJson)
            ?? throw new JsonException("Product data is empty.");

        ProductContainerJson container = root.Container
            ?? throw new JsonException("Product data contains no product collection.");
        List<ProductInfoJson> products = container.Products
            ?? throw new JsonException("Product data contains no products.");

        var data = new ProductData();
        foreach (ProductInfoJson entry in products)
        {
            if (entry.Code is null)
                throw new JsonException("Product entry contains no code.");
            string code = entry.Code;

            data._products[code] = new ProductInfo(
                code,
                entry.Name ?? "",
                entry.Description ?? "");
        }
        return data;
    }

    private sealed class ProductDataJson
    {
        [JsonPropertyName("productdata")] public ProductContainerJson? Container { get; set; }
    }

    private sealed class ProductContainerJson
    {
        [JsonPropertyName("product")] public List<ProductInfoJson>? Products { get; set; }
    }

    private sealed class ProductInfoJson
    {
        [JsonPropertyName("code")]
        [JsonConverter(typeof(StringValueJsonConverter))]
        public string? Code { get; set; }

        [JsonPropertyName("name"), JsonConverter(typeof(ProductTextJsonConverter))]
        public string? Name { get; set; }

        [JsonPropertyName("description"), JsonConverter(typeof(ProductTextJsonConverter))]
        public string? Description { get; set; }
    }

    private sealed class ProductTextJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString() ?? "";
            if (reader.TokenType is JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False)
                return reader.HasValueSequence
                    ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
                    : Encoding.UTF8.GetString(reader.ValueSpan);
            reader.Skip();
            return "";
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class StringValueJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() ?? "",
                JsonTokenType.Number => ReadNumber(ref reader),
                _ => throw new JsonException($"Expected product code, found {reader.TokenType}.")
            };

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);

        private static string ReadNumber(ref Utf8JsonReader reader)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetRawText();
        }
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(ProductDataJson))]
    private sealed partial class ProductDataJsonContext : JsonSerializerContext;
}
