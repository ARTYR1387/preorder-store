using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PreOrderStore.Domain;

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
    string? ImageUrl,
    int AvailableQuantity);

public sealed class DummyJsonProductProvider(HttpClient httpClient) : IProductProvider
{
    public async Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("products?limit=0", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProductListResponse>(cancellationToken: cancellationToken);
        if (payload?.Products is null)
            throw new InvalidDataException("Ответ каталога не содержит списка товаров.");

        return payload.Products
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Title) && item.Price >= 0)
            .Select(item => new ProductFeedItem(
                item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                item.Title!.Trim(),
                item.Description ?? string.Empty,
                string.IsNullOrWhiteSpace(item.Category) ? "Без категории" : item.Category,
                decimal.Round(item.Price, 2, MidpointRounding.AwayFromZero),
                item.Thumbnail,
                Math.Max(0, item.Stock)))
            .ToArray();
    }

    private sealed class ProductListResponse
    {
        [JsonPropertyName("products")]
        public List<ProductDto>? Products { get; init; }
    }

    private sealed class ProductDto
    {
        [JsonPropertyName("id")] public int Id { get; init; }
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("category")] public string? Category { get; init; }
        [JsonPropertyName("price")] public decimal Price { get; init; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; init; }
        [JsonPropertyName("stock")] public int Stock { get; init; }
    }
}
