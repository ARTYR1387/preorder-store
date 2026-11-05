using System.Net;
using System.Text;
using PreOrderStore.Infrastructure;

namespace PreOrderStore.Tests;

public sealed class ProductDetailsProviderTests
{
    [Fact]
    public async Task GetProductDetails_LoadsSecureImagesAndUsableReviews()
    {
        const string payload = """
            {
              "rating": 4.7,
              "thumbnail": "https://cdn.example.test/product/thumb.webp",
              "images": [
                "https://cdn.example.test/product/full.webp",
                "https://cdn.example.test/product/thumb.webp",
                "http://insecure.example.test/image.jpg"
              ],
              "reviews": [
                { "rating": 5, "comment": "Отличное качество", "date": "2025-01-02T10:00:00.000Z", "reviewerName": "Анна" },
                { "rating": 0, "comment": "Некорректная оценка", "date": "2025-01-02T10:00:00.000Z", "reviewerName": "Тест" },
                { "rating": 4, "comment": "   ", "date": "2025-01-02T10:00:00.000Z", "reviewerName": "Тест" }
              ]
            }
            """;
        using var client = new HttpClient(new JsonResponseHandler(payload))
        {
            BaseAddress = new Uri("https://dummyjson.com/")
        };
        var provider = new DummyJsonProductProvider(client);

        var details = await provider.GetProductDetailsAsync("42");

        Assert.NotNull(details);
        Assert.Equal(4.7m, details.Rating);
        Assert.Equal(["https://cdn.example.test/product/full.webp", "https://cdn.example.test/product/thumb.webp"], details.Images);
        var review = Assert.Single(details.Reviews);
        Assert.Equal("Анна", review.ReviewerName);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Отличное качество", review.Comment);
    }

    [Fact]
    public async Task GetProductDetails_SkipsLocalIdsWithoutAnHttpRequest()
    {
        using var client = new HttpClient(new JsonResponseHandler("{}"))
        {
            BaseAddress = new Uri("https://dummyjson.com/")
        };
        var provider = new DummyJsonProductProvider(client);

        var details = await provider.GetProductDetailsAsync("local-42");

        Assert.Null(details);
    }

    private sealed class JsonResponseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.EndsWith("/products/42", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
