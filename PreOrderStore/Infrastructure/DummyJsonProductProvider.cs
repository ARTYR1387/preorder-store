using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PreOrderStore.Infrastructure;

public interface IProductProvider
{
    Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default);
}

public sealed record ProductFeedItem(
    string ExternalId,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string? ImageAssetPath,
    DateTime? ExpectedArrivalDate,
    int AvailableQuantity,
    bool IsAvailable);

/// <summary>Reads the public DummyJSON placeholder catalogue. The displayed RUB price uses an explicit illustrative conversion, not a live exchange rate.</summary>
public sealed class DummyJsonProductProvider(HttpClient client) : IProductProvider
{
    // DummyJSON prices are sample values with no currency guarantee. This fixed multiplier is only a classroom display conversion.
    public const decimal IllustrativeRubleMultiplier = 90m;
    private const string Fields = "id,title,description,category,price,thumbnail,stock";

    public async Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync($"products?limit=0&select={Fields}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProductEnvelope>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("DummyJSON вернул пустой каталог.");
        if (payload.Products is null) throw new InvalidDataException("В ответе DummyJSON отсутствует список products.");

        return payload.Products
            .Where(p => p.Id > 0 && !string.IsNullOrWhiteSpace(p.Title) && p.Price >= 0)
            .Select(p => new ProductFeedItem(
                p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                p.Title!.Trim(),
                p.Description?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(p.Category) ? "Без категории" : p.Category,
                decimal.Round(p.Price!.Value * IllustrativeRubleMultiplier, 2, MidpointRounding.AwayFromZero),
                string.IsNullOrWhiteSpace(p.Thumbnail) ? null : p.Thumbnail,
                null,
                Math.Max(0, p.Stock),
                p.Stock > 0))
            .ToArray();
    }

    private sealed class ProductEnvelope
    {
        [JsonPropertyName("products")] public List<ProductDto>? Products { get; set; }
    }

    private sealed class ProductDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("price")] public decimal? Price { get; set; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
        [JsonPropertyName("stock")] public int Stock { get; set; }
    }
}
