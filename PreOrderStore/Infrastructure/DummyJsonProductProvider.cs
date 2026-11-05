using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PreOrderStore.Infrastructure;

public interface IProductProvider
{
    Task<IReadOnlyList<ProductFeedItem>> GetProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductDetailsFeed?> GetProductDetailsAsync(string externalId, CancellationToken cancellationToken = default);
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

public sealed record ProductReviewFeed(string ReviewerName, int Rating, string Comment, DateTime Date);
public sealed record ProductDetailsFeed(IReadOnlyList<string> Images, decimal? Rating, IReadOnlyList<ProductReviewFeed> Reviews);

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

    public async Task<ProductDetailsFeed?> GetProductDetailsAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(externalId, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0)
            return null;

        using var response = await client.GetAsync($"products/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductDetailDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("DummyJSON не вернул подробности товара.");

        var images = (product.Images ?? [])
            .Append(product.Thumbnail ?? string.Empty)
            .Where(IsHttpsUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var reviews = (product.Reviews ?? [])
            .Where(review => review.Rating is >= 1 and <= 5 && !string.IsNullOrWhiteSpace(review.Comment))
            .Select(review => new ProductReviewFeed(
                string.IsNullOrWhiteSpace(review.ReviewerName) ? "Покупатель DummyJSON" : review.ReviewerName.Trim(),
                review.Rating,
                review.Comment!.Trim(),
                review.Date))
            .ToArray();

        return new ProductDetailsFeed(images, product.Rating, reviews);
    }

    private static bool IsHttpsUrl(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

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

    private sealed class ProductDetailDto
    {
        [JsonPropertyName("rating")] public decimal? Rating { get; set; }
        [JsonPropertyName("images")] public List<string>? Images { get; set; }
        [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
        [JsonPropertyName("reviews")] public List<ReviewDto>? Reviews { get; set; }
    }

    private sealed class ReviewDto
    {
        [JsonPropertyName("rating")] public int Rating { get; set; }
        [JsonPropertyName("comment")] public string? Comment { get; set; }
        [JsonPropertyName("date")] public DateTime Date { get; set; }
        [JsonPropertyName("reviewerName")] public string? ReviewerName { get; set; }
    }
}
