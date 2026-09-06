using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qx.Game;

public sealed record ProductInfo(string Code, string Name, string Description);

public sealed partial class ProductData : IReadOnlyDictionary<string, ProductInfo>
{
    private readonly Dictionary<string, ProductInfo> _products = new(StringComparer.Ordinal);

    public int Count => _products.Count;
    public IEnumerable<string> Keys => _products.Keys;
    public IEnumerable<ProductInfo> Values => _products.Values;

    public ProductInfo? this[string code] => _products.GetValueOrDefault(code);

    ProductInfo IReadOnlyDictionary<string, ProductInfo>.this[string code] => _products[code];

    public ProductInfo? GetInfo(string code) => _products.GetValueOrDefault(code);

    public bool ContainsKey(string code) => _products.ContainsKey(code);

    public bool TryGetValue(string code, [NotNullWhen(true)] out ProductInfo? info) =>
        _products.TryGetValue(code, out info);

    public IEnumerator<KeyValuePair<string, ProductInfo>> GetEnumerator() =>
        _products.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
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
